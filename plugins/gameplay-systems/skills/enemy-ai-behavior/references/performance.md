# AI performance

Read when AI shows in the Profiler, when scaling enemy counts, adding AI LOD or pooling enemies.

## Where the time goes

| Cost | Per enemy | Control |
|---|---|---|
| Decision tick (perception query, raycasts, threat, FSM) | Every 0.1-2 s | Tick interval, LOD, budget |
| NavMeshAgent steering and avoidance | Every frame while enabled | Agent count, `obstacleAvoidanceType`, disabling far agents |
| Path requests (`SetDestination`) | On repath | `repathDistance`, never per frame |
| Animator | Every frame | `AnimatorCullingMode`, Animator LOD, fewer layers |
| `Update` call overhead | Every frame | One `Update` per enemy is fine at tens; for hundreds use a manager that ticks a list |

Profile a worst-case scene (3-5x expected enemies, all players present) in a development build with the CPU Profiler; look for `EnemyBrain.Update`, `Physics.Raycast`, `NavMeshManager` and `Animator.Update`. Add `ProfilerMarker`s around `Tick` if you need a per-system number.

## Tick scheduling in `EnemyBrain`

- **Stagger**: the first tick is `Time.time + Random.Range(0, nearInterval)`, so a wave spawned on one frame spreads out.
- **LOD by distance** to the nearest player (`PerceptionTarget.TryGetNearestPlayerSqrDistance`): `combatInterval` (0.1 s) while chasing or attacking, `nearInterval` (0.2 s) within `nearDistance`, `farInterval` (0.75 s) up to `sleepDistance`, `sleepInterval` (2 s) beyond.
- **Budget**: a static counter allows at most `maxTicksPerFrame` decision ticks per frame across all brains; extra brains try again next frame. Raise it if reaction delays become visible, lower it if ticks spike.
- **Continuous work stays cheap**: `Update` only advances attack phase timers, turns toward the target and sets two Animator parameters.

## AI LOD beyond tick rate

For large open maps (survival):

- **Far** (beyond `sleepDistance`, out of all players' relevance): disable the `NavMeshAgent` and the Animator, or deactivate the enemy and keep only its spawn record. Re-enable with `agent.enabled = true` then `agent.Warp(position)`; an agent placed off the NavMesh logs errors on path calls.
- **Mid**: `obstacleAvoidanceType = LowQualityObstacleAvoidance` or `NoObstacleAvoidance`, Animator culling `CullCompletely` on clients only.
- **Near**: full quality.
- **Spawner-driven**: for survival games, despawn enemies that are far from every player and respawn from spawn points near players instead of simulating the whole world.

## Animator culling and the server

`AnimatorCullingMode.CullCompletely` stops animation entirely when renderers are not visible; `CullUpdateTransforms` keeps the state machine but stops transform writes, retargeting and IK. On a host, "visible" means visible to the host's camera, which is unrelated to what remote players see. Consequences:

- Do not trigger server-side gameplay (damage, footstep noise) from animation events on enemies. `EnemyBrain` uses timers.
- Melee sweeps that follow weapon bones (`MeleeHitDetector.BeginSwing`) need bones that move on the server: set `AlwaysAnimate` on enemies that use bone-driven sweeps, or use the arc query (`StrikeArc`) that does not depend on bones.

## Pooling enemies

Use `UnityEngine.Pool.ObjectPool<T>` (see `csharp-unity`) for frequently respawned enemies:

- `actionOnGet`: position the object, `SetActive(true)`, call `EnemyBrain.ResetBrain(home, rotation)` (warps the agent, clears threat and memory, resets the FSM) and `Health.ResetHealth()`.
- `actionOnRelease`: `SetActive(false)`. `Perception.OnDisable` unsubscribes from noise and clears memory; `ThreatTable.OnDisable` clears threat.
- Never keep references to pooled enemies in threat tables or perception memory of others; both drop disabled targets on their next tick.
- In co-op, pooling goes through NGO's prefab handler (see `coop-networking.md`); a plain `ObjectPool` alone breaks spawn replication.

## Allocation checklist

- Physics buffers allocated once (`Collider[]` in `Awake`); `Physics.OverlapSphere` without a buffer allocates per call.
- No LINQ, `foreach` over interfaces in hot paths, string building or `GetComponent` per tick; `TryGetComponent` on a candidate collider is fine at tick rate.
- Events with `Action<T>` fields: subscribe once in `OnEnable`, never create lambdas per tick.
- `PerceptionTarget.Active` is a `List<T>` exposed as `IReadOnlyList<T>`; index it with `for`, which does not allocate.
