# Co-op interactions (Netcode for GameObjects 2.x)

Read when making interactions networked, writing pickups, doors or containers for co-op, or debugging rejected or duplicated interactions.

Authority model assumed: owner authority for player characters (owner-authoritative `NetworkTransform`), server authority for world objects and AI. General NGO setup, prefab registration and RPC rules are in `netcode-for-gameobjects`.

## Request flow

```
Owner client                           Server (host)
Interactor: hold completes
NetworkInteractor.Execute(target)
  RequestInteractRpc(NetworkBehaviourReference)  ──►  ValidateAndInteract
                                                    sender == OwnerClientId
                                                    rate limit
                                                    reference resolves to IInteractable
                                                    distance (server-side player position)
                                                    CanInteract(request)
                                                    Interact(request) -> NetworkVariables / spawn / despawn
  InteractionResultRpc(result)  ◄──  [Rpc(SendTo.Owner)]
Interactor.Complete(target, accepted)
                                   NetworkVariable changes reach every client, including late joiners
```

- The RPC lives on the player's `NetworkInteractor`, which the requesting client owns. World objects are server-owned, so a client cannot call an `InvokePermission.Owner` RPC on them; routing through the player object keeps the permission check meaningful.
- `[Rpc(SendTo.Server)]` runs locally when called on the host, so the host player uses the same path with zero latency.
- `RpcParams.Receive.SenderClientId` identifies the caller. `InvokePermission = RpcInvokePermission.Owner` (NGO 2.7.0+) already rejects non-owners; the sender check is a second line of defence.
- `NetworkBehaviourReference` serializes the target's `NetworkObject` id plus behaviour index. It only resolves for spawned objects; in-scene placed objects must have a `NetworkObject` and be spawned by the scene manager.
- The result goes back with `[Rpc(SendTo.Owner)]` on the same player object.

## Server validation checklist

- [ ] Sender owns the interactor object.
- [ ] Target resolves, is spawned, implements `IInteractable`.
- [ ] Distance: `(InteractionPoint - serverPlayerPosition).sqrMagnitude <= (MaxDistance + tolerance)^2`. With owner-authoritative movement the server's copy of the player lags by roughly the client's latency plus interpolation; 0.5-1 m tolerance at walking speed. Never accept a client-sent position.
- [ ] `CanInteract(request)` with the server's state (locked, in use, already taken).
- [ ] Rate limit per player (`minRequestInterval`).
- [ ] Domain checks inside `OnInteractServer`: inventory space, required tool, quest state.
- [ ] Return `false` from `OnInteractServer` when nothing happened, so the client receives `Rejected`.

This is a co-op game among friends, so validation is about consistency (no duplicated items, no conflicting state) more than anti-cheat. Keep it anyway: it also catches lag and race conditions.

## Doors (`NetworkDoor`)

- `NetworkVariable<bool>` for open and locked; written by the server only (default write permission).
- Every peer animates the hinge toward the replicated value in `Update`, so the collider is in the same place everywhere and late joiners see the right state without extra code.
- If the door blocks AI navigation, carve or toggle the `NavMeshObstacle` on the server and clients from the same value (see `initialize-ai-navigation`).
- Keys or switches: call `SetLockedServer(false)` from the server-side logic that consumed the key.
- Do not add a `NetworkTransform` to the door panel; replicating a bool is cheaper and exact.

## Pickups (`NetworkPickup`)

- `OnInteractServer` hands the item to the instigator through `IPickupReceiver.TryReceive(itemId, amount)` (implemented by whatever holds the player's items) and despawns the pickup with `NetworkObject.Despawn(true)` only when the hand-off succeeded.
- The `_taken` flag guards the window between two requests processed in the same server frame.
- Spawn dropped items on the server only (`NetworkObject.Spawn`). The pickup prefab must be in the Network Prefabs list.
- Item ids come from static data (see `game-data-pipeline`); the pickup never carries item stats.
- Feedback: the requesting player reacts to `Interactor.Completed(target, true)`; everyone else sees the object disappear. For a shared sound or VFX at the pickup position, send it from a long-lived object (the player or a world effects manager) with `[Rpc(SendTo.ClientsAndHost)]`, not from the pickup that is being despawned in the same frame.

## Containers (`NetworkContainer`)

- One user at a time: `NetworkVariable<ulong> _user`, `NoUser` = `ulong.MaxValue`. `CanInteract` is true when free or already used by the requester; interacting again closes it.
- The opening client sees `LocalOpenChanged(container, true)` through `OnValueChanged` and opens its container UI; contents and transfers belong to the item/inventory layer and go through their own server RPCs.
- While open, add an interactor lock from the container UI so the player does not interact with something else.
- Release paths: `Close()` (client asks with `ReleaseRpc`; the server checks the sender is the user), walking beyond `autoCloseDistance` (checked on the server), user disconnect (`NetworkManager.OnConnectionEvent` with `ConnectionEvent.ClientDisconnected`), and despawn.
- Shared stashes that several players may browse at once need a different model (contents in a `NetworkList`, per-transfer validation) instead of the occupancy lock.

## Local-only interactables

Things that change nothing shared (reading a note, inspecting an item model, a radio only you hear) derive from `Interactable` and run on the client. `NetworkInteractor.Execute` returns `false` for targets that are not `NetworkBehaviour`s, and the interactor calls `Interact` locally.

## Testing

- Multiplayer Play Mode (see `netcode-for-gameobjects`) with the host plus one or two clients.
- Simulate latency and packet loss on the transport (Network Simulator in the Multiplayer Tools package, or the transport's debug simulator; check the installed versions for the exact component) and confirm prompts dim, then resolve.
- Race: two clients press on the same pickup at once. Exactly one receives the item; the other gets `Rejected`.
- Late join: open doors and occupied containers appear correctly for a player who joins after the change.
- Disconnect while a container is open: it frees itself on the host.

## Debugging

| Symptom | Cause |
|---|---|
| `RpcException` on interact | `NetworkInteractor` not spawned yet, or `Interactor.Executor` left pointing at a despawned player |
| Always `OutOfRange` on clients | `InteractionPoint` differs between server and client (animated child not synced), or tolerance too small for latency |
| Always `Missing` | Target has no `NetworkObject`, is not spawned, or the reference was built from a prefab instead of the scene instance |
| Host works, clients never get results | Result RPC target is wrong; it must be `SendTo.Owner` on the player object |
| Door state correct, collider wrong on clients | Collider driven by a client-only animation instead of the replicated value |
