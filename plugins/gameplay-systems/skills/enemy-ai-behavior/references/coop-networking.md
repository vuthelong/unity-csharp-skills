# Co-op networking for enemy AI (Netcode for GameObjects 2.x)

Read when the game uses Netcode for GameObjects: server-only AI, the driver component, spawning, despawning and pooling enemies. General NGO setup (NetworkManager, authority modes, RPC rules) lives in `netcode-for-gameobjects`.

## Model

- The **server (host)** runs perception, threat, decisions, NavMeshAgent movement and hit detection.
- **Clients** see the result: `NetworkTransform` (Authority Mode = Server) for position and rotation, `NetworkAnimator` (Authority Mode = Server) for the `Speed` and `Attacking` parameters, `NetworkVariable`s for health (see `combat-damage-system`).
- Clients never run `EnemyBrain`, `Perception`, `ThreatTable` or the `NavMeshAgent`. An enabled agent on a client moves the transform locally and fights the replicated position.

## Prefab setup

Enemy prefab root:

1. `NetworkObject`, `NetworkTransform` (Authority Mode Server; sync position and Y rotation; X/Z rotation and scale off), `NetworkAnimator` (Authority Mode Server, 2.8.0+; older 2.x is server-authoritative by default).
2. `NavMeshAgent`, `Perception`, `ThreatTable`, `EnemyBrain` **saved disabled** in the prefab, so nothing runs before the network decides the role.
3. `EnemyNetworkDriver` below, plus `Health` / `NetworkHealth` from `combat-damage-system`.
4. Register the prefab in the Network Prefabs List.

```csharp
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class EnemyNetworkDriver : NetworkBehaviour
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Perception perception;
    [SerializeField] private ThreatTable threatTable;
    [SerializeField] private EnemyBrain brain;
    [SerializeField] private Health health;
    [SerializeField] private float corpseSeconds = 5f;

    public override void OnNetworkSpawn()
    {
        SetSimulated(IsServer);
        if (IsServer) this.health.Died += this.OnDied;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer) this.health.Died -= this.OnDied;
        SetSimulated(false);
    }

    private void SetSimulated(bool simulated)
    {
        this.agent.enabled = simulated;
        this.perception.enabled = simulated;
        this.threatTable.enabled = simulated;
        this.brain.enabled = simulated;
    }

    private void OnDied(DamageInfo info)
    {
        this.brain.Kill();
        StartCoroutine(DespawnAfterDelay());
    }

    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(this.corpseSeconds);
        if (IsSpawned) NetworkObject.Despawn();
    }
}
```

The agent is enabled before the brain so `EnemyBrain.OnEnable` finds an active agent. A single-player session should run as a host with no clients, so the same prefab and code path work offline.

## Players as perception targets on the server

The server perceives remote players through their replicated copies. With owner-authoritative players (see `netcode-for-gameobjects`), the `CharacterController` is usually disabled on non-owner copies so it does not fight the synced position, which also removes that collider from physics queries. Therefore:

- Put `PerceptionTarget` on a separate trigger collider (capsule on the `Perceivable` layer) that stays enabled on every copy.
- Keep hurtboxes (`combat-damage-system`) enabled on every copy as well.
- Remote player positions on the server are slightly behind the owner's view (network latency plus interpolation). For slow games this is acceptable; compensate in design with generous attack telegraphs, not code.

## Noise and other inputs on the server

`NoiseEvents.Emit` must run on the server, because only the server has subscribed `Perception` components:

- Footsteps and sprinting: emit on the server from the replicated movement of each player (speed from position delta per tick), or have the owner send an unreliable `[Rpc(SendTo.Server)]` with the noise radius and validate it.
- Shots, explosions, breaking props: the server already processes these requests (`combat-damage-system`), so emit there.
- Never let clients choose an enemy's target directly; send intents (taunt ability) to the server and let it call `ForceTarget`.

## Spawning and despawning

- Spawn on the server only: `Instantiate` then `NetworkObject.Spawn()`, or `NetworkManager.SpawnManager.InstantiateAndSpawn(...)`. Set the position before spawning so `EnemyBrain` records the right home.
- Despawn on the server: `NetworkObject.Despawn()` destroys by default; `Despawn(false)` keeps the GameObject for pooling. Clients never `Destroy` a spawned enemy.
- Leftover AI when the host quits: the session ends for everyone; there is no host migration in this topology.

## Pooling with NGO

Pool networked enemies with a class implementing `INetworkPrefabInstanceHandler` (`NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)` and `void Destroy(NetworkObject networkObject)`), registered with `NetworkManager.PrefabHandler.AddHandler(prefab, handler)` on every peer. Per the spawning docs, `Instantiate` is invoked on non-authority peers and `Destroy` runs on all peers; return pooled instances and deactivate instead of destroying. On the server, take an instance from the pool, call `EnemyBrain.ResetBrain` after enabling the simulated components, then `Spawn()`; release with `Despawn(false)`.

## Testing

- Multiplayer Play Mode with 2-3 players (see `netcode-for-gameobjects` testing reference): verify enemies pick different players, retarget when one leaves or goes down, and that clients' enemies do not jitter.
- Add latency (100-200 ms) with the Network Simulator and check attack telegraphs still read fairly from the client's view.
- Check that the host's camera position does not change AI behavior (Animator culling, see `performance.md`).
