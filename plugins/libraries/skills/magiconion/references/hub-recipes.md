# StreamingHub recipes

Verified against MagicOnion 7.11.0 `src/MagicOnion.Server/Hubs/*`, `src/MagicOnion.Client/StreamingHubClient*.cs`, `docs/docs/streaminghub/*`.

## Shared contract

```csharp
using System;
using System.Threading.Tasks;
using MagicOnion;
using MessagePack;

namespace MyApp.Shared.Hubs
{
    public interface IRoomHub : IStreamingHub<IRoomHub, IRoomHubReceiver>
    {
        ValueTask<RoomSnapshot> JoinAsync(string roomName, string userName);
        ValueTask LeaveAsync();
        ValueTask MoveAsync(PlayerPose pose);
    }

    public interface IRoomHubReceiver
    {
        void OnJoin(Guid connectionId, string userName);
        void OnLeave(Guid connectionId);
        void OnMove(Guid connectionId, PlayerPose pose);
    }

    [MessagePackObject]
    public readonly struct PlayerPose
    {
        [Key(0)] public readonly float X;
        [Key(1)] public readonly float Y;
        [Key(2)] public readonly float Z;
        [Key(3)] public readonly float Yaw;

        public PlayerPose(float x, float y, float z, float yaw)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.Yaw = yaw;
        }
    }

    [MessagePackObject]
    public sealed class RoomSnapshot
    {
        [Key(0)] public Guid[] ConnectionIds { get; set; } = Array.Empty<Guid>();
        [Key(1)] public string[] UserNames { get; set; } = Array.Empty<string>();
    }
}
```

- Plain floats keep Shared Unity-free, so the server needs no MessagePack Unity extension. Convert to `Vector3` at the Unity edge.
- Up to 15 parameters per hub method. Hub interfaces can inherit plain interfaces to share methods.
- `[Ignore]` excludes a method; `[MethodId(n)]` pins an ID (default: FNV1a32 of the method name).

## Server hub

```csharp
using MagicOnion.Server.Hubs;
using MyApp.Shared.Hubs;

public sealed class RoomHub : StreamingHubBase<IRoomHub, IRoomHubReceiver>, IRoomHub
{
    IGroup<IRoomHubReceiver>? room;
    string userName = "";

    public async ValueTask<RoomSnapshot> JoinAsync(string roomName, string userName)
    {
        this.userName = userName;
        this.room = await Group.AddAsync(roomName);
        this.room.Except([ConnectionId]).OnJoin(ConnectionId, userName);
        return new RoomSnapshot();
    }

    public async ValueTask LeaveAsync()
    {
        if (this.room is null) return;
        this.room.Except([ConnectionId]).OnLeave(ConnectionId);
        await this.room.RemoveAsync(Context);
        this.room = null;
    }

    public ValueTask MoveAsync(PlayerPose pose)
    {
        this.room?.Except([ConnectionId]).OnMove(ConnectionId, pose);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnConnected() => ValueTask.CompletedTask;

    protected override ValueTask OnDisconnected()
    {
        this.room?.Except([ConnectionId]).OnLeave(ConnectionId);
        return ValueTask.CompletedTask;
    }
}
```

| Member | Purpose |
|---|---|
| `Group.AddAsync(string)` | join/create a hub-managed group, returns `IGroup<TReceiver>` |
| `IGroup<T>.All` / `Except(IEnumerable<Guid>)` / `Only(IEnumerable<Guid>)` / `Single(Guid)` | receiver proxies for broadcast |
| `IGroup<T>.RemoveAsync(ServiceContext)` / `CountAsync()` | leave group / member count |
| `Client` | receiver proxy for this connection only |
| `ConnectionId` (protected) | `Guid` of this connection |
| `Context` | `StreamingServiceContext`; `Context.CallContext.GetHttpContext()` for user, headers |
| `OnConnecting()` | before connection completes; no client/group calls yet |
| `OnConnected()` | ready; send initial state, join default groups |
| `OnDisconnected()` | connection gone; client calls are no-ops, groups auto-removed |

- Hub methods execute sequentially per connection, in arrival order, never lost. Do not block in one, and never await another hub call from the same client inside a hub method (deadlock). Different connections run concurrently, so shared room state needs its own synchronization.
- Broadcast calls (`room.All.OnX(...)`) are non-blocking enqueues; they do not wait for delivery.
- Hub groups are destroyed when empty. For rooms that must exist before players or across hubs, inject `IMulticastGroupProvider` (`GetOrAddSynchronousGroup<Guid, IRoomHubReceiver>(name)`, `group.Add(ConnectionId, Client)`, `group.Remove(ConnectionId)`, `group.Dispose()`).
- Client results: a receiver method returning `Task` / `Task<T>` can be awaited from the server (`await Client.AskAsync()`), 5 s default timeout (`ClientResultsDefaultTimeout`), only on `Client` / `Single`, v7+ on both sides.

## Heartbeat

Server: `options.EnableStreamingHubHeartbeat = true` with `StreamingHubHeartbeatInterval` / `StreamingHubHeartbeatTimeout`, or per hub `[Heartbeat]` / `[Heartbeat(Interval = 10000, Timeout = 1000)]` (milliseconds). Optional `IStreamingHubHeartbeatMetadataProvider.TryWriteMetadata(IBufferWriter<byte>)` attaches data.

Client (`StreamingHubClientOptions`, all return a new options instance):

| Method | Effect |
|---|---|
| `CreateWithDefault(host, callOptions, serializerProvider, logger)` | base options; `callOptions` carries auth headers |
| `WithClientHeartbeatInterval(TimeSpan?)` | client pings server; `null` disables (default) |
| `WithClientHeartbeatTimeout(TimeSpan?)` | disconnect with `DisconnectionType.TimedOut` if no ack |
| `WithClientHeartbeatResponseReceived(Action<ClientHeartbeatEvent>)` | `RoundTripTime` per ack |
| `WithServerHeartbeatReceived(Action<ServerHeartbeatEvent>)` | `ServerTime` (UTC `DateTimeOffset`), `Metadata` |
| `WithSerializerProvider(...)`, `WithLogger(...)`, `WithTimeProvider(...)` | misc |

MagicOnion uses its own heartbeat rather than HTTP/2 PING because load balancers may answer PINGs themselves. Client heartbeat does not work on WebGL (no threads for `System.Threading.Timer`). Do not enable heartbeat while v6 clients or servers are still deployed.

## Unity reconnecting client

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Client;
using MyApp.Shared.Hubs;
using UnityEngine;

public sealed class RoomConnection : MonoBehaviour, IRoomHubReceiver
{
    #region Constants
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);
    #endregion

    #region Serialized Fields
    [SerializeField] private string serverUrl = "http://localhost:5000";
    [SerializeField] private string roomName = "lobby";
    [SerializeField] private string userName = "player";
    #endregion

    #region Private Fields
    private GrpcChannelx _channel;
    private IRoomHub _hub;
    private string _accessToken = "";
    #endregion

    #region Events
    public event Action<Guid, PlayerPose> PlayerMoved;
    public event Action<Guid> PlayerLeft;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        this._channel = GrpcChannelx.ForAddress(this.serverUrl);
        this.RunAsync(this.destroyCancellationToken).Forget();
    }

    private void OnDestroy()
    {
        var hub = this._hub;
        this._hub = null;
        if (hub != null)
        {
            hub.DisposeAsync().AsUniTask().Forget();
        }

        if (this._channel != null)
        {
            this._channel.Dispose();
            this._channel = null;
        }
    }
    #endregion

    #region Public API
    public void SendPose(PlayerPose pose)
    {
        if (this._hub == null)
        {
            return;
        }

        this._hub.FireAndForget().MoveAsync(pose);
    }
    #endregion

    #region Connection
    private async UniTaskVoid RunAsync(CancellationToken cancellationToken)
    {
        var delay = InitialRetryDelay;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var headers = new Metadata { { "authorization", "Bearer " + this._accessToken } };
                var options = StreamingHubClientOptions.CreateWithDefault(callOptions: new CallOptions(headers))
                    .WithClientHeartbeatInterval(TimeSpan.FromSeconds(10))
                    .WithClientHeartbeatTimeout(TimeSpan.FromSeconds(5));

                this._hub = await StreamingHubClient.ConnectAsync<IRoomHub, IRoomHubReceiver>(
                    this._channel, this, options, cancellationToken: cancellationToken);

                var snapshot = await this._hub.JoinAsync(this.roomName, this.userName);
                this.ApplySnapshot(snapshot);
                delay = InitialRetryDelay;

                var reason = await this._hub.WaitForDisconnectAsync();
                if (reason.Type == DisconnectionType.CompletedNormally && cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                Debug.LogWarning($"Room hub disconnected: {reason.Type} {reason.Exception?.Message}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            await this.DisposeHubAsync();

            if (await UniTask.Delay(delay, cancellationToken: cancellationToken).SuppressCancellationThrow())
            {
                return;
            }

            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRetryDelay.Ticks));
        }
    }

    private async UniTask DisposeHubAsync()
    {
        var hub = this._hub;
        this._hub = null;
        if (hub == null)
        {
            return;
        }

        try
        {
            await hub.DisposeAsync();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private void ApplySnapshot(RoomSnapshot snapshot)
    {
    }
    #endregion

    #region IRoomHubReceiver
    public void OnJoin(Guid connectionId, string joinedUserName)
    {
    }

    public void OnLeave(Guid connectionId)
    {
        this.PlayerLeft?.Invoke(connectionId);
    }

    public void OnMove(Guid connectionId, PlayerPose pose)
    {
        this.PlayerMoved?.Invoke(connectionId, pose);
    }
    #endregion
}
```

Why it is shaped this way:

- `ConnectAsync` is awaited from `Start`'s continuation on the main thread, so `SynchronizationContext.Current` is Unity's and every receiver method is posted to the main thread.
- One `GrpcChannelx` per server, reused across reconnects; channels multiplex many calls over one HTTP/2 connection.
- `WaitForDisconnectAsync()` (extension on `IStreamingHubMarker`) is the single disconnect signal; the loop disposes the dead client, backs off exponentially, reconnects and re-joins. `DisconnectionType.TimedOut` means the client heartbeat expired.
- `OnDestroy` disposes the hub and then the channel; `GrpcChannelx.Dispose()` would also dispose the hub, and on quit `GrpcChannelProviderHost` shuts down any channel you missed.
- With Enter Play Mode Options set to skip domain reload, keep connection state in instance fields, not statics, so a second Play session starts clean.
- Receiver interfaces can be implemented on a plain C# class instead of the MonoBehaviour (better with `vcontainer`); forward to game systems via C# events or `messagepipe`.

## Unary call from Unity

```csharp
private async UniTask<int> SumAsync(int x, int y, CancellationToken cancellationToken)
{
    var client = MagicOnionClient.Create<IMyFirstService>(this._channel)
        .WithCancellationToken(cancellationToken)
        .WithDeadline(DateTime.UtcNow.AddSeconds(5));
    try
    {
        return await client.SumAsync(x, y);
    }
    catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
    {
        Debug.LogWarning("Server unavailable");
        throw;
    }
}
```

`UnaryResult<T>` is awaitable directly inside a UniTask method; no conversion needed.
