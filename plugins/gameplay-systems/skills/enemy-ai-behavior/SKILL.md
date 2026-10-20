---
name: enemy-ai-behavior
description: Builds enemy and NPC combat AI for Unity 6 3D games - choosing a finite state machine, Unity Behavior graphs (com.unity.behavior) or utility AI, perception (vision cone with Physics.Raycast line of sight, hearing through noise events, last-known-position memory, non-alloc queries on staggered ticks), threat/aggro targeting across co-op players with retarget hysteresis, NavMeshAgent chase, attack windows and leashing, AI tick budgets and LOD, and server-only AI for Netcode for GameObjects co-op. Use for "make an enemy that patrols, chases and attacks", aggro tables, enemies hearing footsteps or gunshots, enemies that cannot see the player or retarget every frame, AI eating CPU, or AI behaving differently on host and clients. Not for NavMesh baking, agent types or links (initialize-ai-navigation), collider and query debugging (physics-3d-collision), Animator controller authoring (animation-authoring), damage and health (combat-damage-system) or general NGO setup (netcode-for-gameobjects).
license: MIT
metadata:
  category: gameplay-systems
  sources: "https://github.com/vuthelong/unity-csharp-skills (original), https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.OverlapSphereNonAlloc.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.OverlapSphere.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.Raycast.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshAgent.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshAgent.SetDestination.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshAgent-remainingDistance.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshAgent-isStopped.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMesh.SamplePosition.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AnimatorCullingMode.html, https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.behavior.html, https://docs.unity3d.com/Packages/com.unity.behavior@1.0/manual/index.html, https://docs.unity3d.com/Packages/com.unity.behavior@1.0/changelog/CHANGELOG.html, https://docs.unity3d.com/Packages/com.unity.behavior@1.0/api/Unity.Behavior.BehaviorGraphAgent.html, https://docs.unity3d.com/Packages/com.unity.behavior@1.0/api/Unity.Behavior.Node.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/basics/object-spawning.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/advanced-topics/message-system/rpc.html"
  unity: "6000.0+"
---

# Enemy AI Behavior

Target: 3D action/exploration/survival games with a handful of enemy archetypes, 2-4 co-op players, slow to medium pace. The reference scripts are plain MonoBehaviours; networking is a thin wrapper shown in [references/coop-networking.md](references/coop-networking.md).

## Workflow

1. **Navigation first.** A baked NavMesh and a working `NavMeshAgent` come from `initialize-ai-navigation`. Do not start AI work until an agent can walk to a clicked point.
2. **Pick the decision architecture** with the table below. Default to the FSM in `EnemyBrain`.
3. **Set up layers** (see `physics-3d-collision`): a `Perceivable` layer for the trigger collider that carries `PerceptionTarget` on each player, an occluder mask for walls and terrain that excludes characters, and the enemy's own colliders on a layer the occluder mask leaves out.
4. **Copy the scripts** onto the enemy prefab root (`NavMeshAgent`, `Perception`, `ThreatTable`, `EnemyBrain`) and `PerceptionTarget` onto each player:
   - [scripts/EnemyBrain.cs](scripts/EnemyBrain.cs): enum FSM (Idle, Patrol, Investigate, Chase, Attack, Return, Dead), staggered and budgeted ticks with distance LOD, windup/strike/recovery attack window, leash, `ResetBrain` for pooling.
   - [scripts/Perception.cs](scripts/Perception.cs): vision cone + LOS raycast with a reused collider buffer, hearing, fixed-size memory with last-known position.
   - [scripts/ThreatTable.cs](scripts/ThreatTable.cs): per-target threat with decay, distance-weighted scoring, retarget margin and minimum switch interval.
   - [scripts/PerceptionTarget.cs](scripts/PerceptionTarget.cs) and [scripts/NoiseEvents.cs](scripts/NoiseEvents.cs): what enemies can sense, a registry of players for AI LOD, and a static noise bus.
5. **Wire combat.** Subscribe to `EnemyBrain.AttackStrike` and run the hit query from `combat-damage-system` (`MeleeHitDetector.StrikeArc`); on the enemy's `Health.Died` call `EnemyBrain.Kill()`; on `Health.Damaged` call `ForceTarget` so an unseen attacker gets aggro. Glue below.
6. **Emit noise** from gameplay: footsteps when sprinting, gunshots, breaking objects, with `NoiseEvents.Emit(position, radius, source)`. In co-op, emit on the server only.
7. **Co-op:** run steps 4-6 on the server only; clients render through `NetworkTransform` and `NetworkAnimator`. Read [references/coop-networking.md](references/coop-networking.md).
8. **Profile** with 3-5x the expected enemy count: [references/performance.md](references/performance.md).

## Choosing an architecture

| | FSM (`EnemyBrain`) | Behavior tree (Unity Behavior) | Utility AI |
|---|---|---|---|
| Best for | 3-8 clear states: patrol, chase, attack, flee | Designer-authored sequences, bosses, NPC routines, many archetypes sharing subtrees | Many competing options scored by context (survival animals, companions, colony NPCs) |
| Cost to build | Lowest; plain C# | Package + custom action nodes | Medium; needs curve tuning tools |
| Debugging | Log or gizmo the current state | Live graph debugging in Play mode | Hard: log scores per option |
| Allocation and tick control | Full control | Ticked by the package's agent component; profile it | Full control |
| Package | none | `com.unity.behavior` (released, 1.0.x line listed for 6000.0) | none |

Decide in [references/architecture-choice.md](references/architecture-choice.md) (read when the FSM grows past about 8 states, designers want to edit behaviors, or you need a utility scorer).

## Glue with combat-damage-system

```csharp
public class EnemyCombatGlue : MonoBehaviour
{
    [SerializeField] private EnemyBrain brain;
    [SerializeField] private Health health;
    [SerializeField] private MeleeHitDetector melee;
    [SerializeField] private float damage = 15f;
    [SerializeField] private float reach = 2.2f;
    [SerializeField] private float damageThreat = 2f;

    private void OnEnable()
    {
        this.brain.AttackStrike += this.OnAttackStrike;
        this.health.Damaged += this.OnDamaged;
        this.health.Died += this.OnDied;
    }

    private void OnDisable()
    {
        this.brain.AttackStrike -= this.OnAttackStrike;
        this.health.Damaged -= this.OnDamaged;
        this.health.Died -= this.OnDied;
    }

    private void OnAttackStrike(PerceptionTarget target) =>
        this.melee.StrikeArc(transform.position + Vector3.up, transform.forward, this.reach, 90f, this.damage, DamageType.Physical);

    private void OnDamaged(DamageInfo info, float applied)
    {
        if (info.Source != null && info.Source.TryGetComponent<PerceptionTarget>(out var attacker))
            this.brain.ForceTarget(attacker, applied * this.damageThreat);
    }

    private void OnDied(DamageInfo info) => this.brain.Kill();
}
```

`TryGetComponent<PerceptionTarget>` on `info.Source` assumes the attacker's `Health` and `PerceptionTarget` share a GameObject; adjust if yours differ. Mark downed or dead players untargetable (`PerceptionTarget.IsTargetable = false`) from `Health.StateChanged` so threat tables drop them.

## Rules

- **Tick decisions, not every frame.** `EnemyBrain` decides every 0.1-2 s depending on state and distance to the nearest player, with a random first offset so enemies spawned together do not tick on the same frame, and a static per-frame budget (`maxTicksPerFrame`). The agent keeps moving between ticks; only the attack window and facing run in `Update`.
- **Non-alloc queries.** `Physics.OverlapSphereNonAlloc` with a buffer allocated once in `Awake`; it never grows the buffer and reports at most `buffer.Length` colliders, so size it for the worst crowd. `Physics.OverlapSphere` (no buffer overload in 6000.0) allocates a new array per call.
- **Vision = range + cone + line of sight.** Distance check first, then angle, then one `Physics.Raycast` against the occluder mask with `QueryTriggerInteraction.Ignore`. A raycast does not report colliders that contain its origin, so an eye point inside the enemy's own capsule is safe, but keep characters out of the occluder mask anyway.
- **Memory, not omniscience.** Lost targets keep a last-known position for `memoryDuration`; the brain walks there, then investigates, then returns. Never read a player's live position for an enemy that cannot sense it.
- **Retarget with hysteresis.** Switch only when the new score beats the current by `retargetMargin` and `minRetargetInterval` has passed. Without it, enemies ping-pong between two players at similar distance.
- **Repath only when the goal moved** more than `repathDistance`; `SetDestination` may take several frames (`pathPending`), and `remainingDistance` is `Infinity` while unknown.
- **Leash.** Beyond `leashRadius` from home, clear threat and memory and run home ignoring threat until arrival. Optionally heal on leash (combat skill) to stop kiting exploits.
- **Attack windows commit.** Windup (telegraph) → strike check → recovery → cooldown. The strike rechecks range, so a player who dodges out during the windup is missed. Stop the agent (`isStopped = true`) and turn manually while attacking.
- **Server-time timers.** Do not drive hit timing from animation events on the server when the Animator may be culled (`AnimatorCullingMode.CullCompletely` stops animation off-screen, and a host's screen is not the enemy's). `EnemyBrain` uses timers; animation follows the `Attacking` parameter.
- **Statics reset** with `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` so the scripts work with domain reload disabled.

## References

- [references/architecture-choice.md](references/architecture-choice.md): read when choosing FSM vs Unity Behavior vs utility AI, installing `com.unity.behavior`, writing a custom action node, or sketching a utility scorer.
- [references/perception-and-targeting.md](references/perception-and-targeting.md): read when tuning sight, hearing, stealth, memory or threat values, or designing aggro rules for co-op (tanking, healing threat, downed players).
- [references/performance.md](references/performance.md): read when AI shows in the Profiler, when scaling enemy counts, adding AI LOD or pooling enemies.
- [references/coop-networking.md](references/coop-networking.md): read when the game uses Netcode for GameObjects: server-only AI, the driver component, spawning, despawning and pooling enemies.

## Pitfalls

| Symptom | Fix |
|---|---|
| Enemy never sees the player | `targetMask` misses the player's `PerceptionTarget` collider layer, the collider is disabled on the server's copy (see coop reference), or the occluder mask contains the player's own layer |
| Sees through walls | Occluder mask lacks the wall layer, or walls are triggers (query uses `Ignore`) |
| Enemy flips between players | Raise `retargetMargin` / `minRetargetInterval`; check threat decay is not zero |
| Enemies stutter or hitch in groups | Repathing every tick; raise `repathDistance`; check the tick budget |
| Enemy stands still after chase | `remainingDistance` read while `pathPending`; destination off the NavMesh (snap with `NavMesh.SamplePosition`, radius about 2x agent height) |
| Attack hits players who dodged | Strike check missing or strike range tolerance too large; lengthen the windup |
| AI runs on clients, enemies jitter | AI not gated to the server; agent enabled on clients fights `NetworkTransform` |
| Enemy is kited forever | Leash radius too large, or threat added while returning |

## Related skills

- `initialize-ai-navigation`: NavMesh baking, agent types, links, areas, agent-animation coupling.
- `combat-damage-system`: `Health`, `DamageInfo`, hit detection and damage events used by the glue above.
- `physics-3d-collision`: layer matrix and why a query misses.
- `animation-authoring`: the locomotion blend tree and attack states driven by `Speed` and `Attacking`.
- `netcode-for-gameobjects`: NetworkObject, NetworkTransform and NetworkAnimator authority setup.
- `csharp-unity`: `ObjectPool<T>` and house style.
