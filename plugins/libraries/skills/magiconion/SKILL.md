---
name: magiconion
description: Builds client-server networking for Unity with Cysharp MagicOnion (gRPC + MessagePack RPC over HTTP/2): a shared C# interface project, an ASP.NET Core server and a Unity client. Covers install (NuGetForUnity MagicOnion.Client, the com.cysharp.magiconion.client.unity UPM git URL, YetAnotherHttpHandler, GrpcChannelx, GrpcChannelProviderHost), the [MagicOnionClientGeneration] source generator required for IL2CPP, Unary services (IService<T>, UnaryResult<T>, ServiceBase<T>, MagicOnionClient.Create), StreamingHub (IStreamingHub<THub,TReceiver>, StreamingHubBase, Group.AddAsync, IGroup All/Except/Only/Single, StreamingHubClient.ConnectAsync, DisposeAsync, WaitForDisconnectAsync, heartbeat), filters, JWT auth, cancellation, reconnect, main-thread receivers, AddMagicOnion / MapMagicOnionService, HTTP/2 and TLS, WebGL, and MessagePack vs MemoryPack. Use when the user mentions MagicOnion, StreamingHub, UnaryResult, gRPC in Unity, or real-time RPC between Unity and a .NET server.
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/Cysharp/MagicOnion"
  unity: "6000.0+"
---

# MagicOnion

Code-first RPC for Unity and .NET: define C# interfaces once, implement them on an ASP.NET Core gRPC server, call them from Unity through generated proxies. Verified against MagicOnion 7.11.0 (`src/`, `docs/docs/`). Server needs .NET 8+; Unity client needs 2022.3+ (Unity 6: 6000.0.34f1+, earlier 6000.0 builds have source generator issues).

Reference files, read when needed:

- `references/server-setup.md` - read when creating the solution, the shared project, `Program.cs`, Kestrel HTTP/2/TLS, auth, filters, options, or Redis/NATS group backplanes.
- `references/hub-recipes.md` - read when writing StreamingHub interfaces, server hubs, groups, heartbeat, disconnect handling, or the Unity reconnecting client.
- `references/transport-platforms.md` - read when picking the transport/serializer per platform (Editor, Mono, IL2CPP, iOS, Android, WebGL, consoles) or comparing against Netcode for GameObjects / Multiplayer Services.

## Architecture

```
MyApp.Shared  (netstandard2.1 classlib + package.json + MyApp.Shared.Unity.asmdef)
   |  interfaces (IService<T>, IStreamingHub<,>), receiver interfaces, [MessagePackObject] DTOs
   +--> MyApp.Server  (ASP.NET Core, MagicOnion.Server, ProjectReference)
   +--> MyApp.Unity   (Packages/manifest.json: "file:../../MyApp.Shared")
```

- Shared holds only interfaces, DTOs and enums, references `MagicOnion.Abstractions`, and is consumed by Unity as a local UPM package (preferred) or by copying files (fragile). Give the asmdef a `.Unity` suffix so it does not clash with the csproj assembly name; set `ArtifactsPath` in `Directory.Build.props` so `bin`/`obj` never appear inside the Unity package.
- Do not reference Unity types (`UnityEngine.Vector3`) from Shared unless the server also references the MessagePack Unity extension package.

## Workflow

1. Server and Shared: follow `references/server-setup.md` (`dotnet new grpc`, replace `Grpc.AspNetCore` with `MagicOnion.Server`, `builder.Services.AddMagicOnion()`, `app.MapMagicOnionService()`).
2. Unity packages:
   - NuGetForUnity: `https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity` (UPM git URL).
   - NuGet > Manage NuGet Packages: `MagicOnion.Client` (depends on `MagicOnion.Abstractions`, `MagicOnion.Shared`, `MagicOnion.Serialization.MessagePack`, `MessagePack`, `Grpc.Net.Client`, `Grpc.Core.Api`), plus `System.IO.Pipelines` for YetAnotherHttpHandler. The Shared package compiles against `MagicOnion.Abstractions` from the same NuGet install.
   - UPM git URL, pin the same version as the NuGet package: `https://github.com/Cysharp/MagicOnion.git?path=src/MagicOnion.Client.Unity/Assets/Scripts/MagicOnion.Client.Unity#7.11.0` (package `com.cysharp.magiconion.client.unity`, namespaces `MagicOnion` / `MagicOnion.Unity`).
   - UPM git URL: `https://github.com/Cysharp/YetAnotherHttpHandler.git?path=src/YetAnotherHttpHandler#1.11.6` (`Cysharp.Net.Http.YetAnotherHttpHandler`, native HTTP/2 via Rust/hyper; the old C-core `Grpc.Core` Unity package is discontinued).
   - Optional, for Unity types in DTOs: UPM `https://github.com/MessagePack-CSharp/MessagePack-CSharp.git?path=src/MessagePack.UnityClient/Assets/Scripts/MessagePack#v3.1.7`, matching the `MessagePack` NuGet version (MagicOnion 7.11.0 builds against 3.1.7); the server then needs NuGet `MessagePack.UnityShims`.
   - Shared: `"com.yourco.myapp.shared.unity": "file:../../MyApp.Shared"` in `Packages/manifest.json` (not "Add package from disk", which stores an absolute path).
3. Add one bootstrap file to the Unity client (below): channel provider + source generator + resolver registration.
4. Define a Unary service or StreamingHub in Shared, implement on the server, call from Unity.
5. Make an IL2CPP player build early. The Editor runs on Mono, where MagicOnion falls back to `DynamicMagicOnionClientFactoryProvider` (Reflection.Emit); only an IL2CPP build proves the generated code and resolvers are wired.

## Unity bootstrap

```csharp
using Cysharp.Net.Http;
using Grpc.Net.Client;
using MagicOnion.Client;
using MagicOnion.Unity;
using MessagePack;
using MessagePack.Resolvers;
using UnityEngine;

[MagicOnionClientGeneration(typeof(MyApp.Shared.Services.IMyFirstService))]
internal partial class MagicOnionGeneratedClientInitializer { }

internal static class MagicOnionBootstrap
{
    #region Fields
    private static bool _resolversRegistered;
    #endregion

    #region Initialization
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (!_resolversRegistered)
        {
            StaticCompositeResolver.Instance.Register(
                MagicOnionGeneratedClientInitializer.Resolver,
                MessagePack.Unity.UnityResolver.Instance,
                StandardResolver.Instance);
            _resolversRegistered = true;
        }

        MessagePackSerializer.DefaultOptions = MessagePackSerializer.DefaultOptions
            .WithResolver(StaticCompositeResolver.Instance);

        GrpcChannelProviderHost.Initialize(new DefaultGrpcChannelProvider(() => new GrpcChannelOptions
        {
            HttpHandler = new YetAnotherHttpHandler { Http2Only = true },
            DisposeHttpClient = true,
        }));
    }
    #endregion
}
```

- `[MagicOnionClientGeneration(params Type[])]` takes any type from each assembly that contains service/hub interfaces; it scans the whole assembly. The source generator ships inside the `MagicOnion.Client` NuGet package (no `moc` tool, no build step). The class must be `partial`.
- Generated code auto-registers its client factory on Unity (`DisableAutoRegistration = false` default). You still register `.Resolver` with MessagePack yourself. The official sample uses `StaticCompositeResolver` (its comment says `CompositeResolver` does not work on IL2CPP).
- `StaticCompositeResolver.Register` throws `InvalidOperationException` once any formatter has been resolved, and it survives Play sessions when domain reload is disabled; the static guard keeps the second Play session from throwing.
- Replacing the resolver drops the `UnityResolver` that the MessagePack.Unity package installs at `SubsystemRegistration`; include `MessagePack.Unity.UnityResolver.Instance` (needs the `com.github.messagepack-csharp` UPM package, see `messagepack-csharp`) if any DTO uses `Vector3`, `Quaternion`, `Color`, or remove that line if you only use plain types.
- `Http2Only = true` is for `http://` (h2c). For `https://` drop it; HTTP/2 is negotiated via ALPN. YetAnotherHttpHandler has its own root store: dev certificates fail with `invalid peer certificate: UnknownIssuer` unless you configure its root-certificate options (see the YetAnotherHttpHandler README) or use the h2c port in development.
- `GrpcChannelProviderHost` creates a `DontDestroyOnLoad` GameObject that calls `ShutdownAllChannels()` in `OnDestroy` and `OnApplicationQuit`; never destroy it. Its `Initialize` calls `FindObjectsOfType`, which compiles with an obsolete warning (CS0618) on Unity 6.
- Create channels with `GrpcChannelx.ForAddress("http://host:5000")` or `GrpcChannelx.ForTarget(new GrpcChannelTarget(host, port, isInsecure: true))` so they are tracked (and visible under `Window > MagicOnion > gRPC Channels`) and shut down on quit.

## Unary services

```csharp
public interface IMyFirstService : IService<IMyFirstService>
{
    UnaryResult<int> SumAsync(int x, int y);
}
```

- Shared interface: inherit `IService<TSelf>`; every method returns `UnaryResult` or `UnaryResult<T>`; up to 15 parameters; parameters and results must be MessagePack-serializable.
- Server: `public class MyFirstService : ServiceBase<IMyFirstService>, IMyFirstService` with `public async UnaryResult<int> SumAsync(int x, int y)`. A new instance is created per call; inject dependencies through the constructor (ASP.NET Core DI).
- Client: `var client = MagicOnionClient.Create<IMyFirstService>(channel); var sum = await client.SumAsync(1, 2);`. Clients are cheap, immutable wrappers; `WithHeaders(Metadata)`, `WithCancellationToken(ct)`, `WithDeadline(DateTime)`, `WithOptions(CallOptions)` and `WithHost(string)` return new instances.
- Errors arrive as `Grpc.Core.RpcException`. Server-side `throw new ReturnStatusException(StatusCode, detail)` maps to a status; an unhandled server exception becomes `StatusCode.Unknown`; network failures are `StatusCode.Unavailable`.

## StreamingHub (real-time)

```csharp
public interface IRoomHub : IStreamingHub<IRoomHub, IRoomHubReceiver>
{
    ValueTask JoinAsync(string roomName, string userName);
    ValueTask LeaveAsync();
    ValueTask MoveAsync(PlayerPose pose);
}

public interface IRoomHubReceiver
{
    void OnJoin(string userName);
    void OnLeave(string userName);
    void OnMove(Guid connectionId, PlayerPose pose);
}
```

- Hub methods return `ValueTask`, `ValueTask<T>`, `Task`, `Task<T>` or `void`. Receiver methods return `void` (unless using client results).
- Server: `StreamingHubBase<IRoomHub, IRoomHubReceiver>`; one instance per connection, kept alive for the connection. `Group.AddAsync(name)` returns `IGroup<TReceiver>` (`All`, `Except(IEnumerable<Guid>)`, `Only(IEnumerable<Guid>)`, `Single(Guid)`, `RemoveAsync(Context)`, `CountAsync()`). `Client` targets only the caller. Disconnected clients leave their groups automatically.
- Client: `var hub = await StreamingHubClient.ConnectAsync<IRoomHub, IRoomHubReceiver>(channel, receiver, options, cancellationToken: ct);` then `await hub.DisposeAsync()` when done. `hub.WaitForDisconnectAsync()` (extension in `MagicOnion.Client`) returns `DisconnectionReason` with `Type` = `CompletedNormally`, `Faulted` or `TimedOut`.
- Heartbeat (off by default): client `StreamingHubClientOptions.CreateWithDefault().WithClientHeartbeatInterval(...).WithClientHeartbeatTimeout(...)`, server `options.EnableStreamingHubHeartbeat` or `[Heartbeat]`. See `references/hub-recipes.md`.

## Unity client lifecycle

Rules, with a full reconnecting component in `references/hub-recipes.md`:

- Connect from `Start` with an `async UniTaskVoid` method (see `unitask`) and pass `destroyCancellationToken`.
- Call `ConnectAsync` on the main thread. The client captures `SynchronizationContext.Current` at connect time and `Post`s every receiver callback to it, so receiver methods run on the Unity main thread and may touch `Transform`/UI. If you connect after `UniTask.SwitchToThreadPool()` or `ConfigureAwait(false)`, callbacks arrive on pool threads.
- Receiver callbacks are posted, so they run one player-loop tick later than the network read. Keep them short; copy data into fields or publish through `messagepipe` rather than doing heavy work inline.
- `OnDestroy`: `this._hub?.DisposeAsync()` (fire and forget) and dispose the `GrpcChannelx`. Disposing a `GrpcChannelx` also disposes every StreamingHub client it created.
- Reconnect: a disconnected hub client is dead. Await `WaitForDisconnectAsync()`, dispose it, back off (1 s, 2 s, 4 s... capped), call `ConnectAsync` again, then re-join groups and resync state, because the server-side hub instance and its group memberships are gone.
- With `vcontainer`, register the channel and a connection service as singletons in a `LifetimeScope`; implement `IAsyncStartable` / `IDisposable` on the service instead of using a MonoBehaviour.

## Filters, auth, cancellation

- Server filter: subclass `MagicOnionFilterAttribute` (`Invoke(ServiceContext, Func<ServiceContext, ValueTask>)`) for Unary; on a hub it runs only once at connect. Per-hub-method filter: `StreamingHubFilterAttribute` (`Invoke(StreamingHubContext, Func<StreamingHubContext, ValueTask>)`). Global: `options.GlobalFilters.Add<T>()`, `options.GlobalStreamingHubFilters.Add<T>()`.
- Client filter (Unary only): implement `IClientFilter.SendAsync(RequestContext, Func<RequestContext, ValueTask<ResponseContext>>)` and pass `new IClientFilter[] { ... }` to `MagicOnionClient.Create`. `CallOptions` is shared per client instance, so check before adding a header in a retry filter.
- JWT: server `AddAuthentication().AddJwtBearer(...)`, `app.UseAuthentication(); app.UseAuthorization();` before `MapMagicOnionService()`, `[Authorize]` / `[AllowAnonymous]` on services or hubs, `Context.CallContext.GetHttpContext().User`. Client Unary: `.WithHeaders(new Metadata { { "authorization", "Bearer " + token } })`. Client hub: `StreamingHubClientOptions.CreateWithDefault(callOptions: new CallOptions(headers))`. A hub authenticates only at connect; token expiry mid-session is not re-checked, so reconnect with a fresh token.
- Cancellation: Unary client `WithCancellationToken(ct)` or `WithDeadline`; server reads `Context.CallContext.CancellationToken`. `StreamingHubClient.ConnectAsync` accepts a `CancellationToken`. Hub methods have no per-call client token; stop pending hub calls by disposing the hub client. Server-to-client "client results" (receiver methods returning `Task<T>`) time out after `MagicOnionOptions.ClientResultsDefaultTimeout` (5 s) or a `CancellationToken` argument on the server side; the receiver always gets `default(CancellationToken)`.

## Serializer choice

- Default: MessagePack-CSharp v3 (`[MessagePackObject]`, `[Key(n)]`). See `messagepack-csharp` for contracts and resolvers.
- MemoryPack (preview): `MagicOnion.Serialization.MemoryPack` on both sides, `MagicOnionSerializerProvider.Default = MemoryPackMagicOnionSerializerProvider.Instance`, `[MagicOnionClientGeneration(typeof(...), Serializer = MagicOnionClientGenerationAttribute.GenerateSerializerType.MemoryPack)]`, and call `MagicOnionMemoryPackFormatterProvider.RegisterFormatters()` at startup. Faster, C#-only; see `memorypack`. Client and server must use the same serializer.

## Pitfalls

- **Interface drift**: hub method IDs are FNV1a32 hashes of the method name; Unary routes are `IService/Method`. Renaming, reordering parameters or changing types on one side only breaks calls at runtime (`Unimplemented`, deserialization errors), not at compile time. Keep one Shared source of truth, deploy server first with additive changes, use `[MethodId]` only to keep an old ID after a rename. Mixed v6/v7 peers lose heartbeat and client results.
- **Works in Editor, fails on device**: missing `[MagicOnionClientGeneration]` or resolver registration. IL2CPP has no Reflection.Emit, so the dynamic client and `DynamicGenericResolver` paths throw (`NotSupportedException`, "formatter not found") only in the player. Register the generated resolver in `StaticCompositeResolver` before the first call (`BeforeSceneLoad`).
- **Leaked connections**: every `ConnectAsync` holds an HTTP/2 stream and a server hub instance until `DisposeAsync`. Forgetting it on scene unload leaves zombie players in groups until the server notices (heartbeat timeout or TCP reset). Always dispose in `OnDestroy` and keep one hub per feature, not per object.
- **Blocking calls**: never `.Result`, `.Wait()` or `GetAwaiter().GetResult()` on `UnaryResult`/`Task` on the main thread; the continuation needs the main thread and deadlocks the Editor. Do not `await` a hub method from inside a receiver callback and wait for a reply that needs another callback.
- **Large payloads**: gRPC's default max receive size is 4 MB (`GrpcChannelOptions.MaxReceiveMessageSize`, server `AddGrpc(o => o.MaxReceiveMessageSize = ...)`), and each hub connection queue is capped at 1,024 messages / 16 MiB (`StreamingHubResponseQueueMaxLength` / `StreamingHubResponseQueueMaxSize`); exceeding the queue aborts the connection. Send assets over HTTP/CDN, chunk large data, and keep per-tick hub messages small (deltas, `struct` DTOs, MessagePack int keys).
- **Fire-and-forget spam**: `hub.FireAndForget().MoveAsync(pose)` skips the response round-trip; use it for high-frequency state, but it still queues on the server. Throttle to your tick rate.
- **h2c vs TLS mismatch**: `http://` + no `Http2Only` gives HTTP/1.1 and a protocol error; Kestrel must expose an `Http2` endpoint for cleartext. See `references/server-setup.md`.
- **Group race**: removing the last member and adding a new one concurrently can destroy and recreate a named hub group; hold your own lock or use application-managed groups (`IMulticastGroupProvider`) for authoritative room state.

## Related skills

- `unitask` - async entry points, cancellation, `Forget()`.
- `messagepack-csharp` / `memorypack` - DTO contracts and AOT resolvers.
- `messagepipe` - fan receiver callbacks out to game systems.
- `vcontainer` - composition root for channels and connection services.
- `setup-multiplayer-services` - Unity Sessions/Relay/Lobby; compare in `references/transport-platforms.md`.
