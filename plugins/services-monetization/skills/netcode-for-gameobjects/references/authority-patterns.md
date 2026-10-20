# Authority patterns

Read when choosing owner vs server authority, setting up NetworkTransform, NetworkAnimator or NetworkRigidbody, or handing ownership of an object (carrying, vehicles).

All of this assumes the client-server topology with a host. NGO 2.x also offers a distributed authority topology, which needs the Unity Gaming Services session backend (see `setup-multiplayer-services`); it is out of scope here.

## Terms

| Term | Meaning in client-server |
|---|---|
| Server / host | The only peer that spawns, despawns, parents and writes server-permission `NetworkVariable`s |
| Owner | The client listed in `NetworkObject.OwnerClientId`; server-owned objects have `OwnerClientId == NetworkManager.ServerClientId` |
| Authority | Whoever pushes state for a component. `HasAuthority` on a `NetworkBehaviour`; for `NetworkTransform` it follows `AuthorityMode` |

Owner authority gives the player instant response at the cost of trusting that client. In friends-only co-op that trade is usually right for the player's own movement and animation. Keep everything with shared consequences (damage, loot, world changes) on the server.

## Owner-authoritative player movement

1. Player prefab root: `NetworkObject`, `NetworkTransform` with Authority Mode = Owner, your motor (for example from `character-controller-3d`).
2. Turn off position sync you do not need: usually `SyncRotAngleX` and `SyncRotAngleZ` off for an upright character, all `SyncScale*` off.
3. Run input and the motor only on the owner. Disable them on everyone else so the `NetworkTransform` is the only writer:

```csharp
using Unity.Netcode;
using UnityEngine;

public class OwnerOnlyComponents : NetworkBehaviour
{
    [SerializeField] private Behaviour[] ownerOnly;
    [SerializeField] private GameObject[] ownerOnlyObjects;

    public override void OnNetworkSpawn()
    {
        foreach (var component in this.ownerOnly) component.enabled = IsOwner;
        foreach (var go in this.ownerOnlyObjects) go.SetActive(IsOwner);
    }
}
```

Put the player's `CharacterController`, input reader and camera rig in those arrays. A `CharacterController` left enabled on remote copies still collides and can fight the synced position.

4. Teleport (respawn, fast travel) with `NetworkTransform.Teleport(position, rotation, scale)` from the authority instance (the owner here). Calling it from a non-authority instance throws. For server-decided teleports, send an `[Rpc(SendTo.Owner)]` and let the owner call `Teleport`.
5. Spawn position: set the transform before `Spawn`, or from `ConnectionApprovalResponse.Position` / `Rotation` for player objects.

Before NGO 2.4.0 `NetworkTransform` had no `AuthorityMode` field. On those versions subclass it and override `OnIsServerAuthoritative()` to return `false` (the old `ClientNetworkTransform` sample pattern). Prefer upgrading within 2.x.

## NetworkTransform settings worth changing

| Setting | Guidance |
|---|---|
| `Interpolate` | Keep on for remote objects; it hides tick-rate steps |
| `PositionThreshold`, `RotAngleThreshold` | Raise for slow objects to cut bandwidth; thresholds are not synchronized, so set them on the authority |
| `UseHalfFloatPrecision` | Halves position bandwidth; 2.13.3 fixed its precision loss over distance and stationary jitter. Use on 2.13.3+ |
| `UseUnreliableDeltas` | Lower latency, more full-state resends; leave off unless you measured a need |
| `InLocalSpace` | Needed when a networked object is parented and should move relative to its parent |
| `SlerpPosition` | For orbital motion only |

`NetworkManager` Tick Rate (default 30) sets the update ceiling. 30 is fine for slow co-op; lower it only after measuring bandwidth.

## Animation

- `NetworkAnimator` on the same object as the `Animator`. NGO 2.8.0+ has `AuthorityMode`; set Owner for the player so locomotion parameters come from the owner's input. Before 2.8.0, subclass and override `OnIsServerAuthoritative()` to return `false`.
- Triggers: call `NetworkAnimator.SetTrigger` on the authority instead of `Animator.SetTrigger`, otherwise remote peers miss them.
- Enemy animation stays server authority.

## Physics objects

- `NetworkRigidbody` requires a `NetworkTransform` on the same object and mirrors its authority: the authority simulates, non-authority bodies are made kinematic.
- Pushable crates and physics puzzles: server authority. Players push them by colliding on the server copy; owner-authoritative players appear on the server through their `NetworkTransform`, so expect a little latency in pushes.
- Do not give two peers authority over bodies that touch every frame; it jitters.

## Requests and validation

Owner wants something with shared consequences → owner sends `[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]` → server validates (distance, cooldown, inventory, object state) → server writes `NetworkVariable`s or spawns → clients render the result.

```csharp
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
public void InteractRpc(NetworkObjectReference target, RpcParams rpcParams = default)
{
    if (!target.TryGet(out var targetObject)) return;
    if (!NetworkManager.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client)) return;
    if (client.PlayerObject == null) return;
    if ((client.PlayerObject.transform.position - targetObject.transform.position).sqrMagnitude > this.maxInteractDistance * this.maxInteractDistance) return;
    if (targetObject.TryGetComponent<Door>(out var door)) door.ToggleOnServer();
}
```

Use `rpcParams.Receive.SenderClientId` to find who asked; never trust a client id passed as an argument.

## Ownership transfer

- `NetworkObject.ChangeOwnership(clientId)` and `RemoveOwnership()` on the server only. `OnGainedOwnership` / `OnLostOwnership` fire on the affected peers.
- Carrying an item: server parents it under the player with `NetworkObject.TrySetParent(playerNetworkObject, worldPositionStays)`; both objects must be spawned. Parenting is server-only unless `AllowOwnerToParent` is set.
- Vehicles: change the vehicle's ownership to the driver so an owner-authoritative `NetworkTransform` on it uses the driver's input; give it back to the server on exit.
- `DontDestroyWithOwner = true` keeps an object when its owner disconnects (ownership returns to the server); otherwise the owner's objects despawn with them.

## Quick decision

| Question | Yes → | No → |
|---|---|---|
| Does a wrong value hurt other players (damage, loot, progression)? | Server authority | Next question |
| Does the player feel input lag if the server decides? | Owner authority + server sanity checks | Server authority |
