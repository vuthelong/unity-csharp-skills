---
name: combat-damage-system
description: 'Builds health and damage for Unity 6 3D games - Health with a DamageInfo pipeline (amount, type, source, team, hit point), resistances, invulnerability frames, death, downed-and-revive and respawn for co-op, friendly-fire rules, hitbox/hurtbox layers, melee hit detection with OverlapCapsuleNonAlloc sweeps or arc queries, hitscan raycasts and swept projectiles, and damage events decoupled from UI, audio and VFX via C# events or ScriptableObject channels. Covers Netcode for GameObjects co-op: attack intent RPCs, server-side hit detection, networked Health (damage-authority rules), hit feedback RPCs and lag tolerance. Use for "add health and damage", "melee hits twice or misses", "damage numbers", "players hurt each other", "revive a downed teammate", or clients and host disagreeing about hits. Not for enemy decision logic (enemy-ai-behavior), collider or query debugging (physics-3d-collision), attack animation authoring (animation-authoring) or general NGO setup (netcode-for-gameobjects).'
license: MIT
metadata:
  category: gameplay-systems
  sources: "https://github.com/vuthelong/unity-csharp-skills (original), https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.OverlapSphereNonAlloc.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.OverlapCapsuleNonAlloc.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.SphereCastNonAlloc.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.SphereCast.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.SphereCastAll.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.Raycast.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.RaycastNonAlloc.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.SyncTransforms.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Collider.ClosestPoint.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AnimatorCullingMode.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/basics/networkvariable.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/advanced-topics/message-system/rpc.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/basics/object-spawning.html"
  unity: "6000.0+"
---

# Combat and Damage System

Target: melee-first or mixed combat in a slow to medium paced 3D game, 2-4 player host-client co-op. The scripts are plain MonoBehaviours; the networking layer is a set of thin `NetworkBehaviour` wrappers in [references/coop-networking.md](references/coop-networking.md).

## Workflow

1. **Layers** (see `physics-3d-collision`): `Hurtbox` (trigger colliders on characters), plus an occluder mask for walls. Hit queries use explicit masks, so the Layer Collision Matrix can disable `Hurtbox` against everything (keep it on only against a `Hazard` layer if you use trigger-callback hazards); hurtboxes then never generate stray trigger callbacks or contacts.
2. **Copy the scripts:**
   - [scripts/DamageInfo.cs](scripts/DamageInfo.cs): `DamageInfo` readonly struct, `DamageType`, `CombatTeam`.
   - [scripts/Health.cs](scripts/Health.cs): current/max, resistances, i-frames, Alive/Downed/Dead, bleed-out, `Revive`, `ResetHealth`, `ApplyNetworkState` for replicated clients, events.
   - [scripts/Hurtbox.cs](scripts/Hurtbox.cs): trigger collider routing hits to its `Health` with a multiplier (weak spots).
   - [scripts/MeleeHitDetector.cs](scripts/MeleeHitDetector.cs): swept capsules between frames along blade points (`BeginSwing`/`EndSwing`) and a one-shot `StrikeArc`; one hit per target per swing.
   - [scripts/CombatRules.cs](scripts/CombatRules.cs): friendly fire on/off and multiplier, self damage.
   - [scripts/DamageEventChannel.cs](scripts/DamageEventChannel.cs): ScriptableObject channel for global listeners (damage numbers, hit sounds, stats).
3. **Character setup:** `Health` on the root (team, rules, channel), one or more `Hurtbox` children (capsule for body, sphere for head with multiplier 1.5-2) on the `Hurtbox` layer, `MeleeHitDetector` on the weapon or root with `hurtboxMask = Hurtbox`.
4. **Attack timing:** call `BeginSwing` at the animation's active frames and `EndSwing` after, or call `StrikeArc` once at the impact moment. For enemies, drive it from `EnemyBrain.AttackStrike` (see `enemy-ai-behavior`). Detection details: [references/hit-detection.md](references/hit-detection.md).
5. **Feedback:** UI, audio and VFX subscribe to `Health` events or the `DamageEventChannel`; combat code never references them. See [references/events-and-feedback.md](references/events-and-feedback.md).
6. **Co-op death flow:** players `canBeDowned`, revive and respawn rules: [references/downed-revive-respawn.md](references/downed-revive-respawn.md).
7. **Networking:** hit detection and `ApplyDamage` on the server only; read [references/coop-networking.md](references/coop-networking.md).

## Damage pipeline

```text
attack intent → hit query (server) → Hurtbox.ApplyHit (multiplier)
  → Health.ApplyDamage: alive? → i-frames? → resistance → CombatRules (team, self) → subtract
  → Damaged / HealthChanged / channel.Raise → Downed or Died
```

```csharp
var info = new DamageInfo(25f, DamageType.Fire, attacker.gameObject, CombatTeam.Players, hitPoint, direction);
var applied = hurtbox.ApplyHit(info);
```

## Setup per character type

| Character | Health | Hurtboxes | Detector | Extra |
|---|---|---|---|---|
| Co-op player | Team Players, `canBeDowned`, `invulnerabilityAfterHit` 0.3 | Body capsule 1.0, head sphere 1.5; enabled on every network copy | `MeleeHitDetector` on root, `StrikeArc` from the attack intent | `NetworkHealth`, `PlayerMeleeNetwork`, `ReviveNetwork` (coop reference) |
| Enemy | Team Enemies, resistances per archetype, no i-frames | Body 1.0, weak spot 2.0 | `MeleeHitDetector`; `StrikeArc` from `EnemyBrain.AttackStrike` | `NetworkHealth`; glue from `enemy-ai-behavior` |
| Destructible / barrel | Team Neutral | One box | none | `Died` swaps model, barrels `StrikeArc` an explosion |
| Trap / hazard | none | none | `StrikeArc` on a timer (`self` left empty) | Damage type Fire/Poison; trigger callbacks need the matrix entry from step 1 |

`ApplyDamage` returns the amount actually removed (0 when blocked by state, i-frames, rules or resistance) so callers can skip feedback for blocked hits. `DamageType.True` ignores resistances; `IgnoresInvulnerability` bypasses i-frames (kill volumes, fall damage).

## Rules

- **One writer.** Only the authority (server in co-op, local in single player) calls `ApplyDamage`, `Heal`, `Revive`, `Kill`. Everyone else mirrors with `ApplyNetworkState` and listens to events.
- **Hurtboxes are triggers on their own layer**, primitive colliders (sphere, capsule, box) so `Collider.ClosestPoint` gives a meaningful hit point. Hit queries pass `QueryTriggerInteraction.Collide` explicitly; occluder rays pass `Ignore`.
- **Queries, not trigger callbacks, for attacks.** A trigger-based hitbox needs a Rigidbody on one side, fires `OnTriggerEnter` once per overlap start, misses fast swings between physics steps and is hard to time to animation. Sweeps between last and current positions cover the whole arc at any frame rate.
- **Non-alloc buffers** allocated once; `OverlapCapsuleNonAlloc` / `OverlapSphereNonAlloc` never grow the buffer and stop at its length, and result order is not defined for the cast variants.
- **One hit per target per swing:** dedupe by `Health`, not by collider, or multi-hurtbox characters take damage per collider.
- **i-frames are time stamps** (`SetInvulnerable(seconds)`), checked against `Time.time`; use them for dodge rolls, post-hit grace (`invulnerabilityAfterHit`, 0.2-0.5 s for players, 0 for enemies hit by groups) and revive protection.
- **Friendly fire** is a `CombatRules` asset decision, not a per-weapon check. `Neutral` team damages and is damaged by everyone (traps, explosive barrels).
- **Events carry data, listeners decide.** No `FindObjectOfType`, no UI calls inside `Health`. Unsubscribe in `OnDisable`.
- **Pooling:** call `Health.ResetHealth()` when taking a character or destructible from a pool; it clears i-frames, bleed-out and state.

## References

- [references/hit-detection.md](references/hit-detection.md): read when choosing melee sweeps vs arcs vs triggers, writing hitscan or projectile hits, setting up hurtbox layers, or debugging missed and double hits.
- [references/events-and-feedback.md](references/events-and-feedback.md): read when wiring health bars, damage numbers, hit sounds, hit stop, camera shake or VFX to damage events.
- [references/downed-revive-respawn.md](references/downed-revive-respawn.md): read when designing death, downed and revive, bleed-out, team wipes, checkpoints and respawn.
- [references/coop-networking.md](references/coop-networking.md): read when the game uses Netcode for GameObjects: attack intent RPCs, server hit detection, `NetworkHealth`, hit feedback RPCs, revive requests and latency tolerance.

## Pitfalls

| Symptom | Fix |
|---|---|
| Melee hits twice | Dedupe per `Health` per swing (`MeleeHitDetector` does); `BeginSwing` called twice by duplicated animation events |
| Fast swing passes through enemies | Use swept `OverlapCapsuleNonAlloc` between previous and current blade positions, not a per-frame overlap at the current pose |
| No hits at all | `hurtboxMask` wrong, hurtbox colliders not triggers while the query uses `Ignore`, or hurtbox disabled on the server's copy |
| Attacker damages itself | `self` not set on the detector, or hurtbox owner resolved to a different `Health` |
| Health bar wrong on clients | Clients called `ApplyDamage` locally; only mirror replicated state |
| Damage numbers twice on the host | Host gets both the local event and the feedback RPC; send feedback with `SendTo.NotServer` or skip on the server |
| Dodge did not save the client | Server sees the client slightly late; extend dodge i-frames by about one round trip and lengthen enemy windups |
| Hits follow last frame's pose | Physics sees transforms as of the last sync; see `Physics.SyncTransforms` notes in `physics-3d-collision` |

## Related skills

- `enemy-ai-behavior`: when enemies attack (`AttackStrike`), threat from damage, death handling for AI.
- `physics-3d-collision`: layers, collision matrix, query misses, SyncTransforms.
- `animation-authoring`: attack clips, animation events for active frames, hit reactions.
- `netcode-for-gameobjects`: authority, RPC and NetworkVariable rules used by the wrappers.
- `interaction-system`: hold-to-interact prompts that start and cancel revives.
- `audio-playback-system`, `unity-particle-system`, `cinemachine-cameras`: hit sounds, impact VFX, camera shake listeners.
- `csharp-unity`: `ObjectPool<T>` for projectiles and damage numbers.
