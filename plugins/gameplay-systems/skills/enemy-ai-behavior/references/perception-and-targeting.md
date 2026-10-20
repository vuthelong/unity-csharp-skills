# Perception and target selection

Read when tuning sight, hearing, stealth, memory or threat values, or designing aggro rules for co-op (tanking, healing threat, downed players).

## Vision

`Perception.CanSee` runs cheapest checks first:

1. Distance: `viewDistance * target.Visibility`, so stealth (crouching, darkness, camouflage) scales detection range through `PerceptionTarget.Visibility` (0..1).
2. Proximity: inside `proximityRadius` the cone is skipped; enemies "feel" someone standing behind them.
3. Cone: `Vector3.Angle(eyeForward, toTarget) <= viewAngle / 2`.
4. Line of sight: one `Physics.Raycast` from the eye to `PerceptionTarget.AimPosition` (chest or head) against `occluderMask`, `QueryTriggerInteraction.Ignore`.

Starting values for a slow game:

| Archetype | viewDistance | viewAngle | proximity | memory |
|---|---|---|---|---|
| Wildlife (skittish) | 25 | 200 | 4 | 4 s |
| Humanoid guard | 20 | 120 | 2.5 | 8 s |
| Undead / slow brute | 12 | 90 | 2 | 15 s |
| Boss | 40 | 360 | n/a | whole fight |

Variations:

- **Detection meter** (stealth games): accumulate `seen * rate / distance` per tick into a 0..1 value per target and only add threat once it reaches 1. Store it next to the memory entry.
- **Multiple rays**: cast to head and pelvis, count visible if either is clear. Doubles raycasts; only for stealth-heavy games.
- **Lighting**: compute a visibility value on the player side (light probes, being in a shadow volume) and write `PerceptionTarget.Visibility`; enemies stay cheap.

Candidate gathering uses `Physics.OverlapSphereNonAlloc(eye, viewDistance, buffer, targetMask, QueryTriggerInteraction.Collide)`. `Collide` is used because the recommended `PerceptionTarget` collider is a trigger on a dedicated `Perceivable` layer. With only 2-4 players, iterating `PerceptionTarget.Active` instead of the physics query is also valid and avoids any dependency on colliders.

## Hearing

- `NoiseEvents.Emit(position, radius, source)` broadcasts to every subscribed `Perception`; each checks distance against `radius * hearingMultiplier`. One event fans out to all enemies, which is fine for tens of enemies. For hundreds, bucket enemies in a coarse grid and dispatch only to nearby cells.
- With a `source`, the noise refreshes that target's memory at the noise position (not the target's live position) and adds `hearingThreat`. Without a source (a thrown rock, an explosion), it becomes an investigate point consumed by the next decision tick.
- Typical radii: walk 0-2 m, sprint 8 m, jump landing 6 m, melee hit 10 m, gunshot 40 m, explosion 60 m.
- Walls do not attenuate in this model. For indoor maps, scale radius down when a raycast between emitter and listener hits the occluder mask, or use NavMesh path distance (`NavMesh.CalculatePath`, which is synchronous, so only a few calls per frame) for important listeners only; both cost more.

## Memory

Each `PerceivedTarget` entry keeps `LastKnownPosition`, `LastSeenTime`, `LastHeardTime`, `LastSensedTime` and `VisibleNow`. Entries expire after `memoryDuration` without any sense, or when the target becomes untargetable. The brain:

- Chases the live position while visible.
- Walks to `LastKnownPosition` when sight is lost.
- On arrival without re-acquiring, forgets the target, drops its threat and investigates for `investigateWait` seconds.
- Returns home afterwards.

To search smarter, pick 2-3 random points around the last-known position with `NavMesh.SamplePosition` (max distance about twice the agent height per the docs' guidance) and visit them before returning.

## Threat and target selection

Score = `threat / (1 + distance * distanceFalloff)`. Sources of threat:

| Event | Threat (start here) |
|---|---|
| Seen, per second | 10 |
| Heard (per noise) | 3 |
| Damage dealt to this enemy | applied damage x 2 |
| Healing an ally in sight (co-op tank/healer play) | heal amount x 0.5 |
| Taunt ability | set to current top threat x 1.5 |

Decay (`decayPerSecond`) lets enemies forget players who stop engaging. Retarget hysteresis: switch only if the best score beats the current by `retargetMargin` (25% default) and at least `minRetargetInterval` seconds passed; switch immediately when the current target becomes invalid.

Co-op rules worth adding:

- **Downed players**: set `PerceptionTarget.IsTargetable = false` while downed so enemies peel off to standing players (friendlier for revive play). For harsher games keep them targetable but let the brain prefer standing targets with a score multiplier.
- **Spread pressure**: give each player a small "already targeted by N enemies" penalty so a pack does not all pick the same player. Keep a static count per `PerceptionTarget` updated from `ThreatTable.TargetChanged`.
- **Late join / disconnect**: a disconnected player's object despawns; `ThreatTable.Tick` drops destroyed or disabled targets on the next tick.

## Leash

Leash from `Home` (spawn or camp point), not from the target. On leash: clear threat and memory, run home at chase speed, ignore threat until home. Optional: regenerate health to full during the return (call `Health.ResetHealth()` on arrival) to stop players kiting enemies to death from outside the leash.
