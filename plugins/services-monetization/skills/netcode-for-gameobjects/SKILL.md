---
name: netcode-for-gameobjects
description: Builds host-client co-op multiplayer with Netcode for GameObjects 2.x (com.unity.netcode.gameobjects) in Unity 6 - NetworkManager and UnityTransport setup, NetworkObject spawning and ownership, owner vs server authority with NetworkTransform AuthorityMode, [Rpc(SendTo.Server)] RPCs and RpcInvokePermission, NetworkVariable and NetworkList state, NetworkSceneManager scene loads, connection approval, late join, disconnects and Multiplayer Play Mode testing. Use for "make this multiplayer", "sync this to clients", NetworkBehaviour or OnNetworkSpawn code, ServerRpc/ClientRpc migration, or host shutdown and returning clients to the menu when the host leaves. Not for Unity Gaming Services sessions, lobbies, Relay or matchmaking (setup-multiplayer-services), Steam lobbies and transport (steam-lobbies-p2p), save files (save-system), health and damage replication (combat-damage-system), diagnosing desync, missing objects or late-join bugs (network-debugging) or voice chat (setup-vivox-voice-chat).
license: MIT
metadata:
  category: services-monetization
  sources: "https://github.com/vuthelong/unity-csharp-skills (original), https://github.com/Unity-Technologies/com.unity.netcode.gameobjects/tree/develop-2.0.0/com.unity.netcode.gameobjects, https://github.com/Unity-Technologies/com.unity.netcode.gameobjects/blob/develop-2.0.0/com.unity.netcode.gameobjects/CHANGELOG.md, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/index.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/advanced-topics/message-system/rpc.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/basics/networkvariable.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/basics/connection-approval.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/components/helper/networktransform.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/basics/scenemanagement/using-networkscenemanager.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/basics/object-spawning.html, https://docs.unity3d.com/Packages/com.unity.multiplayer.playmode@1.6/manual/index.html"
  unity: "6000.0+"
---

# Netcode for GameObjects (co-op)

Package: `com.unity.netcode.gameobjects` | Namespace: `Unity.Netcode` | Entry: `NetworkManager.Singleton`

Target: 2-4 player co-op, one player hosts (listen server), slow to mid-paced play (exploration, survival, crafting, puzzle, RPG). No client-side prediction. For fast competitive combat that needs prediction and rollback, evaluate Netcode for Entities or a third-party stack (for example FishNet) before building on NGO.

## Versions

| Line | Unity | Notes |
|---|---|---|
| 2.x (2.13.3 at time of writing) | 6000.0+ | This skill. Depends on `com.unity.transport` 2.x |
| 3.x (3.0.0, 3.1.0) | 6000.7+ | Adds Netcode for Entities as a hard dependency, renames editor assemblies, moves `NetworkTime` / `NetworkTickSystem` to `Unity.Netcode.GameObjects.Timing`, turns some `[Obsolete]` warnings into errors. Read its CHANGELOG before upgrading |

Check `Packages/manifest.json` first and match the installed line. Feature availability inside 2.x: `NetworkTransform.AuthorityMode` since 2.4.0, `RpcInvokePermission` since 2.7.0, `NetworkAnimator.AuthorityMode` since 2.8.0.

## Workflow

1. **Install** `com.unity.netcode.gameobjects` (2.x) and `com.unity.multiplayer.playmode` (see `unity-package-management`). Add `com.unity.multiplayer.tools` for the Network Simulator.
2. **NetworkManager**: one GameObject in the boot scene with `NetworkManager`, a transport (`UnityTransport` while developing, the Steam transport when shipping on Steam - see `steam-lobbies-p2p`), the Player Prefab, and a Network Prefabs List containing every prefab you will spawn. Every prefab spawned at runtime needs a `NetworkObject` on its root.
3. **Authority**: decide per object with the table below before writing sync code. Details: [references/authority-patterns.md](references/authority-patterns.md).
4. **Sync**: `NetworkVariable<T>` for state that must survive late join, `[Rpc(SendTo.Server)]` for requests, `[Rpc(SendTo.ClientsAndHost)]` for one-off events (sound, VFX). Details: [references/rpc-and-variables.md](references/rpc-and-variables.md).
5. **Spawn and scenes**: spawn and despawn only on the server; load gameplay scenes with `NetworkManager.SceneManager.LoadScene`. Details: [references/scenes-and-spawning.md](references/scenes-and-spawning.md).
6. **Late join**: anything a joining player must see lives in `NetworkVariable`s, `NetworkList`s or spawned objects, never only in an RPC that already fired.
7. **Connection approval**: enable `NetworkConfig.ConnectionApproval`, check player count and build version in `ConnectionApprovalCallback`, set `response.Reason` on reject. See the approval section in `references/scenes-and-spawning.md`.
8. **Disconnects**: handle `OnClientDisconnectCallback` on the host (clean up that player's objects) and `OnClientStopped` on clients (host gone, read `DisconnectReason`, return to menu). There is no host migration in this topology.
9. **Test** with Multiplayer Play Mode and the Network Simulator: [references/testing.md](references/testing.md).

## Authority defaults for co-op

| Object | Authority | How |
|---|---|---|
| Player character movement | Owner | `NetworkTransform` Authority Mode = Owner; owner reads input and moves locally |
| Player animation | Owner | `NetworkAnimator` Authority Mode = Owner (2.8.0+) |
| Player health, inventory, currency | Server | `NetworkVariable` written by server; owner asks through `[Rpc(SendTo.Server)]` |
| Enemies, AI, NPCs | Server | Server-owned `NetworkObject`, `NetworkTransform` Authority Mode = Server |
| Loot, pickups, doors, world state | Server | Server validates every request, then writes state |
| Physics props | Server | `NetworkRigidbody` follows the `NetworkTransform` authority; non-authority bodies become kinematic |

## Minimal player script

```csharp
using System;
using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int potionHeal = 25;
    [SerializeField] private ParticleSystem healEffect;

    private readonly NetworkVariable<int> _health = new(100);

    public event Action<int, int> HealthChanged;

    public int Health => this._health.Value;

    public override void OnNetworkSpawn()
    {
        if (IsServer) this._health.Value = this.maxHealth;
        this._health.OnValueChanged += this.OnHealthChanged;
        this.HealthChanged?.Invoke(this._health.Value, this._health.Value);
    }

    public override void OnNetworkDespawn()
    {
        this._health.OnValueChanged -= this.OnHealthChanged;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void UseHealthPotionRpc()
    {
        if (this._health.Value <= 0 || this._health.Value >= this.maxHealth) return;
        this._health.Value = Mathf.Min(this.maxHealth, this._health.Value + this.potionHeal);
        this.PlayHealEffectRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayHealEffectRpc()
    {
        if (this.healEffect != null) this.healEffect.Play();
    }

    private void OnHealthChanged(int previous, int current)
    {
        this.HealthChanged?.Invoke(previous, current);
    }
}
```

The owner calls `UseHealthPotionRpc()`; the server validates and writes the `NetworkVariable`; every peer, including late joiners, gets the value; the effect is a one-off event and is not replayed for late joiners.

## Rules

- Do networked setup in `OnNetworkSpawn`, not `Awake` / `Start`. `IsOwner`, `IsServer`, `IsHost` and `OwnerClientId` are not valid before spawn. Unsubscribe in `OnNetworkDespawn`.
- RPC methods need the `[Rpc]` attribute and a name ending in `Rpc`. Calling one before the object is spawned throws `RpcException`.
- `[ServerRpc]` / `[ClientRpc]` still compile but `RequireOwnership` is deprecated; write new code with `[Rpc(SendTo...)]` and `InvokePermission` (2.7.0+).
- `InvokePermission` defaults to `Everyone`: any client can call a `SendTo.Server` RPC on any object. Set `Owner` for per-player requests and validate arguments on the server anyway.
- `NetworkVariable` default permissions: read `Everyone`, write `Server`. A write without permission logs an error and is ignored.
- Initialize `NetworkList<T>` in `Awake`, not at the field declaration (the docs warn this leaks native memory).
- Keep payloads small: `FixedString32Bytes`/`64Bytes` instead of `string` in hot state, ids instead of whole objects, `NetworkObjectReference` to point at spawned objects.
- Never load gameplay scenes with `UnityEngine.SceneManagement.SceneManager` while a session runs; use `NetworkManager.SceneManager.LoadScene` on the server.
- Every runtime `Instantiate` of a networked prefab goes through `NetworkObject.Spawn` / `InstantiateAndSpawn` on the server. Clients never `Instantiate` or `Destroy` spawned objects.
- No `async void` except UI button handlers wrapped in try/catch; pass `destroyCancellationToken` to awaited work inside a `NetworkBehaviour`.
- Unity 6 API: `FindAnyObjectByType` / `FindFirstObjectByType`, not `FindObjectOfType`; `GetEntityId()` instead of `GetInstanceID()` on 6000.5+.

## References

- [references/authority-patterns.md](references/authority-patterns.md): read when choosing owner vs server authority, setting up NetworkTransform, NetworkAnimator or NetworkRigidbody, or handing ownership of an object (carrying, vehicles).
- [references/rpc-and-variables.md](references/rpc-and-variables.md): read when writing RPCs, choosing SendTo targets, sending custom structs, or syncing state with NetworkVariable or NetworkList.
- [references/scenes-and-spawning.md](references/scenes-and-spawning.md): read when spawning or despawning objects, loading scenes, handling in-scene placed objects, writing connection approval, or handling disconnects.
- [references/testing.md](references/testing.md): read when testing with Multiplayer Play Mode, simulating latency, or debugging desync and missing objects.

## Pitfalls

| Symptom | Cause / fix |
|---|---|
| `RpcException: The NetworkBehaviour must be spawned` | RPC called from `Awake`/`Start` or after despawn; call it after `OnNetworkSpawn` |
| `IsOwner` false on the local player in `Start` | Read ownership in `OnNetworkSpawn` |
| Late joiner sees doors closed that are open | State sent only by RPC; move it into a `NetworkVariable` |
| Client cannot move its own character, it snaps back | `NetworkTransform` still server authority; set Authority Mode = Owner |
| Objects appear on host but not on clients | Spawned with `Instantiate` only, prefab missing from the Network Prefabs List, or spawned on a client |
| Player object stays after its client leaves | Expected only with `DontDestroyWithOwner`; otherwise despawn leftovers in `OnClientDisconnectCallback` |
| Clients load a different scene or duplicate objects | Mixed `SceneManager.LoadScene` with `NetworkSceneManager`; use the network one only |
| Bandwidth spikes, hitches on change | Large `NetworkVariable` payloads (big structs, strings, lists set wholesale); split state, use `NetworkList` deltas |
| Client rejected on connect with no reason | Config hash mismatch (protocol version, tick rate, prefab list with `ForceSamePrefabs`, scene management flag) or approval callback missing while approval is on |

## Related skills

- `steam-lobbies-p2p`: Steam lobbies, invites and the Steam transport for this NGO session.
- `setup-multiplayer-services`: Unity Gaming Services sessions, Relay and join codes; its `With*Network` options start NGO for you.
- `save-system`: host-owned world saves; clients save only their own data.
- `csharp-unity`: house style for NetworkBehaviour code.
- `unity-package-management`: installing the packages headlessly.
