# Desync, missing objects and error logs

Read when state differs between peers, objects are missing or duplicated, a late joiner sees stale state, or a log line mentions hashes, NetworkConfig or write permissions.

## Logging setup

- `NetworkManager.LogLevel`: `Developer` (most verbose), `Normal` (default), `Error`, `Nothing`. Set it in the Inspector or in code before starting the session.
- `NetworkConfig` mismatch and incomplete connection request warnings print only at `Normal` or `Developer`.
- `NetworkLog.LogInfoServer` / `LogWarningServer` / `LogErrorServer` from a client also prints on the host, which puts both sides of an exchange in one log. NGO's docs warn that clients can forge these logs and that they cost bandwidth; keep them out of release builds.
- Player logs: on Windows `%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\Player.log` (`Player-prev.log` holds the previous run). Collect them from every peer of the failing session.
- Stamp your own logs with `NetworkManager.LocalClientId`, `NetworkManager.ServerTime.Tick` and the object's `NetworkObjectId` so lines from different peers can be lined up.

## Desync causes

| Cause | How it shows | Fix |
|---|---|---|
| Two writers | Value flips between two numbers; owner snaps back | One writer per value. Owner-authoritative `NetworkTransform` must not also be moved by the server, and the other way round |
| Write without permission | `Write permissions (...) for this client instance is not allowed!` on that client; the assignment is ignored (in-place edits to a collection value are reverted on the next update) | Send a `[Rpc(SendTo.Server)]` request and let the server write, or change `NetworkVariableWritePermission` deliberately |
| State only sent by RPC | Late joiner sees doors closed, chests full | Move persistent state into `NetworkVariable` / `NetworkList` / spawned objects; keep RPCs for one-off events |
| Collection edited in place | Peers never see the change | Call `CheckDirtyState()` after editing a managed collection inside a `NetworkVariable`, or use `NetworkList<T>` |
| Local simulation on non-authority | Physics props drift apart | `NetworkRigidbody` makes non-authority bodies kinematic; do not add forces on non-authority peers |
| Local clocks | Timers end at different moments | Use `NetworkManager.ServerTime` for durations and start times shared between peers |
| Random without shared seed | Loot or AI differ per peer | Roll on the server and replicate results, or replicate the seed |
| Scene loaded with `SceneManager` | Clients in another scene, duplicated in-scene objects | Load with `NetworkManager.SceneManager.LoadScene` on the server only |
| Logic in `Start` / `Awake` reading `IsOwner`, `IsServer` | Wrong branch on some peers | Move networked setup to `OnNetworkSpawn` |

Diagnosis loop: log the value with the tick on every peer at the moment it diverges, identify which peer wrote it, then check that peer's authority and permissions. NGO's debugging guide also suggests drawing network vs visual positions with `Debug.DrawLine` and recording each peer's screen side by side.

## Missing or duplicated objects

| Log substring | Cause | Fix |
|---|---|---|
| `Failed to create object locally. NetworkPrefab could not be found. Is the prefab registered with this NetworkManager` | The client does not have the prefab hash the host spawned | Same Network Prefabs List in both builds; rebuild both; keep `ForceSamePrefabs` on so mismatched builds are rejected at connect instead |
| `NetworkPrefab hash was not found! In-Scene placed NetworkObject soft synchronization failure!` | An in-scene placed `NetworkObject` on the host has no match in the client's scene | Same scene version on both builds; do not add or remove in-scene `NetworkObject`s per platform or per build define |
| `In-Scene NetworkObject GlobalObjectIdHash (...) could not be found!` | Same family during scene event synchronization | As above; also check additive scenes are loaded through `NetworkSceneManager` |
| `Detected NetworkObject GlobalObjectIdHash value of 0!` | A `NetworkObject` was created at runtime (`AddComponent<NetworkObject>()` or a generated prefab) | Spawn only from prefab assets registered in the prefab list |
| `NetworkPrefab (...) has a duplicate GlobalObjectIdHash source entry value of` | Two entries resolve to the same hash | Remove the duplicate, or use prefab overrides intentionally |
| Object visible on host only, no error | Spawned with `Instantiate` but never `Spawn()`ed, spawned on a client, or hidden by observers (`SpawnWithObservers`, `CheckObjectVisibility`) | Spawn on the server; check visibility rules |

`GlobalObjectIdHash` is baked into prefabs and scenes when they are saved. Duplicating a prefab or scene file outside Unity, or merging scene YAML by hand, can produce collisions or stale hashes; reopen and resave in Unity, then rebuild both peers.

## Connection rejected

The host compares a hash of these `NetworkConfig` fields: `ProtocolVersion`, NGO's internal protocol version, the sorted prefab hashes (only when `ForceSamePrefabs` is on), `TickRate`, `ConnectionApproval`, `ForceSamePrefabs`, `EnableSceneManagement`, `EnsureNetworkVariableLengthSafety`, `RpcHashSize`. Any difference produces `NetworkConfig mismatch. The configuration between the server and client does not match` on the host and disconnects the client.

- Bump `ProtocolVersion` deliberately when you break compatibility, and also gate joins on `Application.version` (see `steam-lobbies-p2p`).
- A client-side approval rejection reason arrives in `NetworkManager.DisconnectReason` on the client; log it in `OnClientStopped`.
- Debug-only differences (an extra prefab registered under `#if DEVELOPMENT_BUILD`) change the prefab hash. Keep prefab lists identical across build types or turn `ForceSamePrefabs` off and accept late errors.

## Disconnects

- Client: log `NetworkManager.DisconnectReason` and the transport-level reason (Steam end reason, see [steam-p2p-checks.md](steam-p2p-checks.md)).
- Host: `OnConnectionEvent` with `ClientDisconnected`, and `OnTransportFailure` when the transport itself fails.
- Timeouts during loading: a long synchronous scene load on the host can exceed the transport timeout for clients. Profile loading hitches with `cpu-performance-profiling` and load asynchronously.
