# RPCs and NetworkVariables

Read when writing RPCs, choosing SendTo targets, sending custom structs, or syncing state with NetworkVariable or NetworkList.

Verified against NGO 2.13.3 source (`RpcAttributes.cs`, `RpcTarget.cs`, `NetworkVariable.cs`, `NetworkVariablePermission.cs`).

## Choosing a mechanism

| Need | Use |
|---|---|
| State a late joiner must see (health, door open, quest step) | `NetworkVariable<T>` |
| Growing or shrinking collection (inventory slots, party list) | `NetworkList<T>` |
| Client asks the server to do something | `[Rpc(SendTo.Server)]` |
| One-off cosmetic event (sound, hit spark, emote) | `[Rpc(SendTo.ClientsAndHost)]` or `SendTo.NotServer` / `NotMe` |
| Server tells one client something | `[Rpc(SendTo.SpecifiedInParams)]` with `RpcTarget.Single(clientId, RpcTargetUse.Temp)` |
| Large or rare blobs (save data, map seed plus edits) | Custom messages or chunked RPCs; never one huge `NetworkVariable` |

## The `[Rpc]` attribute

```csharp
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Reliable)]
private void RequestCraftRpc(int recipeId, RpcParams rpcParams = default)
{
}
```

- The method name must end in `Rpc`; the ILPostProcessor rewrites every call site into a network send.
- The constructor argument is the default target (`SendTo`). Attribute fields: `Delivery` (`Reliable` default, or `Unreliable`), `InvokePermission` (2.7.0+), `DeferLocal`, `AllowTargetOverride`. `RequireOwnership` is obsolete in favour of `InvokePermission`.
- Calling the method before the `NetworkBehaviour` is spawned throws `RpcException`. Calling with `InvokePermission.Server` from a client, or `Owner` from a non-owner, also throws, and the receiving side checks the permission table too.
- An optional last parameter `RpcParams rpcParams = default` gives `rpcParams.Receive.SenderClientId` on the receiver and lets the caller override the target when `AllowTargetOverride = true` or the target is `SpecifiedInParams`.
- Return type must be `void`.

### `SendTo` values

| Value | Runs on | Runs locally when |
|---|---|---|
| `Owner` | The object's owner | Caller is the owner |
| `NotOwner` | Everyone but the owner (observers only) | Caller is not the owner |
| `Server` | The server | Called on the server |
| `NotServer` | All clients except the server; a host is treated as the server and excluded | Called on a client |
| `Me` | Only the caller (optionally deferred one tick with `DeferLocal`) | Always |
| `NotMe` | Everyone but the caller (observers only) | Never |
| `Everyone` | Everyone (observers only) | Always |
| `ClientsAndHost` | All clients including the host; same as `Everyone` in host mode, same as `NotServer` on a dedicated server | Caller is a client or host |
| `Authority` | The authority; in client-server, the server | As `Server` |
| `NotAuthority` | Non-authority peers; in client-server, the clients | As `NotServer` |
| `SpecifiedInParams` | Whatever `RpcParams.Send.Target` says; must be supplied | Depends on target |

For co-op on a host, prefer `ClientsAndHost` over `NotServer` for visual events, or the host player never sees them.

### Runtime targets

`NetworkBehaviour.RpcTarget` (also on `NetworkManager`) builds targets: `RpcTarget.Single(clientId, RpcTargetUse.Temp)`, `RpcTarget.Not(clientId, RpcTargetUse.Temp)`, `RpcTarget.Group(ids, RpcTargetUse.Temp)`, plus fields such as `RpcTarget.Owner`, `RpcTarget.NotMe`. `Temp` targets are reused internally and must not be stored; use `RpcTargetUse.Persistent` and cache it if you keep the target.

```csharp
[Rpc(SendTo.SpecifiedInParams)]
private void ShowRewardRpc(int itemId, RpcParams rpcParams)
{
}

private void GrantReward(ulong clientId, int itemId)
{
    this.ShowRewardRpc(itemId, RpcTarget.Single(clientId, RpcTargetUse.Temp));
}
```

### Allowed parameter types

Unmanaged primitives and enums, `Vector2/3/4`, `Quaternion`, `Color`, `Ray`, `FixedString32Bytes` (and 64/128/512/4096), `string` (allocates), arrays of those, `NetworkObjectReference`, `NetworkBehaviourReference`, and any struct implementing `INetworkSerializable`:

```csharp
public struct CraftRequest : INetworkSerializable
{
    public int RecipeId;
    public int Quantity;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref this.RecipeId);
        serializer.SerializeValue(ref this.Quantity);
    }
}
```

Pass references to spawned objects as `NetworkObjectReference` and resolve with `reference.TryGet(out NetworkObject obj)`; it fails if the object was despawned meanwhile.

## NetworkVariable

```csharp
private readonly NetworkVariable<bool> _isOpen = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
```

- Constructor: `(T value = default, NetworkVariableReadPermission readPerm = Everyone, NetworkVariableWritePermission writePerm = Server)`.
- Read permissions: `Everyone`, `Owner`. Write permissions: `Server`, `Owner`.
- A write from a peer without permission logs an error and is ignored.
- Set initial values at declaration, in `Awake`, or on the writer in `OnNetworkSpawn`. Setting the value while the object is not spawned is not recommended.
- Subscribe `OnValueChanged(previous, current)` in `OnNetworkSpawn` and unsubscribe in `OnNetworkDespawn`. The value is already synchronized when `OnNetworkSpawn` runs on a client, so apply the current value there as well as in the callback.
- `OnValueChanged` also fires on the writer locally when the value changes.
- Value types are compared by value; setting the same value sends nothing.
- Managed contents (a class, `List<T>` inside a `NetworkVariable`) are not tracked when mutated in place. Assign a new value or call `CheckDirtyState()` after changing them. Prefer structs.
- Owner-writable variables (`WritePermission.Owner`) are trusted client state; use them only for cosmetic or self-only data (selected emote, aim pitch).

Execution order note from the docs: for in-scene placed objects `Start` runs before `OnNetworkSpawn`; for dynamically spawned objects `OnNetworkSpawn` runs before `Start`. Do not rely on either; do network setup in `OnNetworkSpawn`.

## NetworkList

```csharp
private NetworkList<int> _inventory;

private void Awake()
{
    this._inventory = new NetworkList<int>();
}

public override void OnNetworkSpawn()
{
    this._inventory.OnListChanged += this.OnInventoryChanged;
}

public override void OnNetworkDespawn()
{
    this._inventory.OnListChanged -= this.OnInventoryChanged;
}

private void OnInventoryChanged(NetworkListEvent<int> change)
{
}
```

- `T` must be `unmanaged` and `IEquatable<T>`: ints, enums, `FixedString*`, custom structs implementing both `INetworkSerializable` and `IEquatable<T>`.
- Construct it in `Awake`, not at the field declaration; the docs warn that declaration-time construction produces memory leak errors.
- Changes replicate as deltas (add, insert, remove, value changed, clear), so it is the right tool for inventories.

## Bandwidth habits

- Ids over names: send `int itemId`, look up data locally.
- Quantize: a `byte` percentage instead of a `float` health ratio when it only drives UI.
- Batch: one struct `NetworkVariable` for a door (`isOpen`, `isLocked`) is fine; one per field is fine too. A 2 KB struct that changes every frame is not.
- Unreliable RPCs (`Delivery = RpcDelivery.Unreliable`) for frequent cosmetic updates that can be dropped.
