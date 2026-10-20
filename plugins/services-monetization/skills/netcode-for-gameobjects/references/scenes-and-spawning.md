# Scenes, spawning, approval and disconnects

Read when spawning or despawning objects, loading scenes, handling in-scene placed objects, writing connection approval, or handling disconnects.

Verified against NGO 2.13.3 (`NetworkObject.cs`, `NetworkSpawnManager.cs`, `NetworkSceneManager.cs`, `NetworkManager.cs`) and the 2.13 manual.

## Prefab registration

- Every networked prefab has exactly one `NetworkObject`, on its root. Child `NetworkBehaviour`s are found automatically.
- Register prefabs in a Network Prefabs List asset assigned to the `NetworkManager`. A spawned prefab that is missing from the list on a client fails to spawn there.
- Player prefab: the `NetworkManager` Player Prefab field, or per-player `ConnectionApprovalResponse.PlayerPrefabHash`.
- With `ForceSamePrefabs` on (default), the prefab list is part of the connection handshake hash: host and client builds must have identical lists.

## Spawning (server only)

```csharp
var instance = NetworkObject.InstantiateAndSpawn(enemyPrefab, NetworkManager.Singleton, position: spawnPoint.position, rotation: spawnPoint.rotation);
```

| Call | Use |
|---|---|
| `NetworkObject.InstantiateAndSpawn(GameObject prefab, NetworkManager nm, ulong ownerClientId = ServerClientId, bool destroyWithScene = false, bool isPlayerObject = false, bool forceOverride = false, Vector3 position = default, Quaternion rotation = default)` | Preferred: validates the prefab, applies prefab overrides, instantiates and spawns |
| `networkObject.Spawn(bool destroyWithScene = false)` | You instantiated it yourself and set fields first |
| `networkObject.SpawnWithOwnership(ulong clientId, bool destroyWithScene = false)` | Spawn owned by a client (a thrown item that client controls) |
| `networkObject.SpawnAsPlayerObject(ulong clientId, bool destroyWithScene = false)` | Custom player spawning when the default player prefab is off |
| `networkObject.Despawn(bool destroy = true)` | Remove on all peers; `false` keeps the GameObject on the server for pooling |

- Set position, rotation and serialized fields before spawning; they go out in the spawn message.
- `destroyWithScene = true` despawns the object when its scene unloads (loot dropped in a dungeon).
- Never call `Destroy` on a spawned object on a client. On the server prefer `Despawn()`.
- Pool frequently spawned objects with `NetworkManager.PrefabHandler` and an `INetworkPrefabInstanceHandler` instead of instantiating each time.

## In-scene placed NetworkObjects

- Objects with a `NetworkObject` saved in a scene are spawned automatically when that scene is loaded through `NetworkSceneManager` (or present at `StartHost`). They keep a stable `GlobalObjectIdHash` shared by all builds.
- Their `Start` runs before `OnNetworkSpawn`; dynamically spawned objects get `OnNetworkSpawn` first. Do network setup in `OnNetworkSpawn` either way.
- `OnInSceneObjectsSpawned()` (protected virtual on `NetworkBehaviour`) fires once all in-scene objects of a load are spawned.
- Despawn in-scene objects with `Despawn(false)` and deactivate the GameObject in `OnNetworkDespawn`; plain `Despawn()` destroys it, which the docs advise against for in-scene objects. To bring it back, reactivate the server instance and `Spawn()` it again.

## NetworkSceneManager

Requires `NetworkConfig.EnableSceneManagement = true` (default). Loads are server-only:

```csharp
var status = NetworkManager.Singleton.SceneManager.LoadScene("Dungeon01", LoadSceneMode.Single);
if (status != SceneEventProgressStatus.Started) Debug.LogWarning($"Scene load failed: {status}");
```

- The scene must be in the active Build Profile's scene list (the build settings scene list).
- `LoadScene(string sceneName, LoadSceneMode mode)` and `UnloadScene(Scene scene)` return a `SceneEventProgressStatus`: `Started`, or a failure such as `SceneEventInProgress`, `InvalidSceneName`, `SceneFailedVerification`, `SceneManagementNotEnabled`, `ServerOnlyAction`.
- One scene event at a time: wait for completion before the next load.
- Completion: `SceneManager.OnLoadEventCompleted(sceneName, loadSceneMode, clientsCompleted, clientsTimedOut)` on the server, or `OnSceneEvent` with `SceneEventType` (`Load`, `LoadComplete`, `LoadEventCompleted`, `Unload`, `UnloadEventCompleted`, `Synchronize`, `SynchronizeComplete`, ...).
- `VerifySceneBeforeLoading` (server) rejects scenes that must never be network-loaded.
- Late joiners are synchronized to the server's loaded scenes. `SetClientSynchronizationMode(LoadSceneMode.Additive)` keeps scenes the client already has (useful with a persistent additive "core" scene).
- `ActiveSceneSynchronizationEnabled` makes the server's active-scene changes replicate.
- Loading a scene with `UnityEngine.SceneManagement.SceneManager` on the server during a session is picked up for late joiners but not for already connected clients unless you handle it; on clients it desyncs. Use `NetworkSceneManager` for everything gameplay-related.
- Keep the `NetworkManager` on a root GameObject in the boot scene; it calls `DontDestroyOnLoad` on itself when it has no parent. Do not nest it under another object.

## Connection approval

Enable `NetworkConfig.ConnectionApproval` and assign the callback before starting:

```csharp
private void OnEnable()
{
    this.networkManager.NetworkConfig.ConnectionApproval = true;
    this.networkManager.ConnectionApprovalCallback = this.ApproveConnection;
}

private void OnDisable()
{
    if (this.networkManager != null) this.networkManager.ConnectionApprovalCallback = null;
}

private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
{
    var version = request.Payload is { Length: > 0 } ? Encoding.UTF8.GetString(request.Payload) : string.Empty;
    var isHostSelf = request.ClientNetworkId == NetworkManager.ServerClientId;

    response.CreatePlayerObject = true;
    response.Approved = isHostSelf || (version == Application.version && this.networkManager.ConnectedClientsIds.Count < this.maxPlayers);
    if (!response.Approved) response.Reason = version != Application.version ? "Version mismatch" : "Session full";
}
```

Client side, before `StartClient`:

```csharp
this.networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(Application.version);
```

- `ConnectionApprovalRequest`: `byte[] Payload` (the client's `NetworkConfig.ConnectionData`), `ulong ClientNetworkId`.
- `ConnectionApprovalResponse`: `Approved`, `CreatePlayerObject`, `PlayerPrefabHash` (`uint?`), `Position` / `Rotation` (nullable), `Pending`, `Reason`.
- The host's own local client also goes through the callback, with `ClientNetworkId == NetworkManager.ServerClientId` and the host's own `ConnectionData`. Approve it.
- Only one callback can be assigned; assigning a second throws `InvalidOperationException`. Clear it with `null` when your component goes away.
- `Pending = true` delays the decision (for example while validating a Steam auth ticket); set `Pending = false` and the result fields later.
- Rejections with `Reason` arrive on the client as `NetworkManager.DisconnectReason`.
- Before the callback runs, NGO compares a config hash covering `ProtocolVersion`, tick rate, `ConnectionApproval`, `ForceSamePrefabs` (and the prefab list when it is on), `EnableSceneManagement`, `EnsureNetworkVariableLengthSafety` and `RpcHashSize`. Bump `NetworkConfig.ProtocolVersion` when you break network compatibility.
- `Payload` is client-supplied and unauthenticated. Treat it as a hint unless you validate a ticket.

## Disconnects

| Event | Where | Use |
|---|---|---|
| `OnConnectionEvent(NetworkManager, ConnectionEventData)` | All peers | `EventType` is `ClientConnected`, `PeerConnected`, `ClientDisconnected` or `PeerDisconnected`; `ClientId` |
| `OnClientConnectedCallback(ulong clientId)` | Server, and the client for itself | Spawn or restore that player's state |
| `OnClientDisconnectCallback(ulong clientId)` | Server for any client; client for itself | Server: save and clean up that player's data. Client: show `DisconnectReason` |
| `OnClientStopped(bool wasHost)` | Any peer that was a client (including host) | Return to menu after the session ends for any reason |
| `OnServerStopped(bool wasHost)` | Host / server | Hosting ended |
| `OnTransportFailure` | Any | Transport died; NGO shuts down |

- `NetworkManager.DisconnectClient(clientId, reason)` kicks with a message (server only).
- `NetworkManager.Shutdown()` ends the session; the actual teardown happens later in the frame loop, and `OnClientStopped` / `OnServerStopped` fire then.
- On client disconnect the server despawns objects owned by that client unless `DontDestroyWithOwner` is set (then ownership returns to the server).
- Host quitting ends the session for everyone; there is no host migration in client-server NGO. Save on the host first (see `save-system`).
