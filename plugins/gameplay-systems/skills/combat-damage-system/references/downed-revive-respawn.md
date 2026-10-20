# Death, downed, revive and respawn

Read when designing death, downed and revive, bleed-out, team wipes, checkpoints and respawn.

## States

```text
Alive --(health 0, canBeDowned)--> Downed --(Revive)--> Alive
  |                                  |
  +--(health 0, !canBeDowned)--> Dead <--(bleed-out timer or Kill)
Dead --(ResetHealth on respawn)--> Alive
```

| Character | `canBeDowned` | Notes |
|---|---|---|
| Co-op player | true | Bleed-out 20-40 s; revive restores 25-50% health with 2 s i-frames |
| Solo player (single player session) | false, or true with a self-revive item | A downed state with nobody to revive is just a slower death |
| Enemy | false | `Died` → `EnemyBrain.Kill()`, loot, despawn after a corpse delay |
| Destructible | false | `Died` → swap to broken prefab |

`Health` behavior while downed: further damage is ignored and the bleed-out coroutine runs; `Revive()` stops it. If downed players should be finishable, add a "downed health" pool or let damage shorten the bleed-out timer in a subclass or an extra component listening to `StateChanged`.

## Wiring the player

```csharp
using UnityEngine;

public class PlayerLifeGlue : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private PerceptionTarget perceptionTarget;
    [SerializeField] private Behaviour[] disabledWhileDown;
    [SerializeField] private float dodgeInvulnerability = 0.35f;

    private void OnEnable() => this.health.StateChanged += this.OnStateChanged;

    private void OnDisable() => this.health.StateChanged -= this.OnStateChanged;

    public void OnDodgeStarted() => this.health.SetInvulnerable(this.dodgeInvulnerability);

    private void OnStateChanged(HealthState previous, HealthState next)
    {
        var alive = next == HealthState.Alive;
        this.perceptionTarget.IsTargetable = alive;
        for (var i = 0; i < this.disabledWhileDown.Length; i++) this.disabledWhileDown[i].enabled = alive;
    }
}
```

`disabledWhileDown` holds combat input, weapon detectors and interaction components; keep a crawl or camera component enabled if downed players can move or look around. `PerceptionTarget` is from `enemy-ai-behavior`; drop that line if the project does not use it.

## Revive

- Reviver stands within 2 m of the downed player and holds Interact for 3-5 s. Cancel on moving away, taking damage (optional) or the reviver going down.
- Show progress on both players' HUDs.
- On completion: `health.Revive()` (uses `reviveHealthFraction`, grants `invulnerabilityAfterRevive`).
- In co-op the server owns the timer; clients only send start and stop requests. See `coop-networking.md`.

## Team wipe and respawn

| Model | Flow | Fits |
|---|---|---|
| Checkpoint wipe | All players Downed or Dead → fade → reload checkpoint state → `ResetHealth()` and teleport everyone | Story co-op, dungeons |
| Individual respawn | Dead player waits N seconds or until a teammate reaches a shrine → `ResetHealth()` at a respawn point | Open-world survival |
| Corpse run | Drop inventory in a container at the death point, respawn at bed/base | Survival crafting |

Server-side check after any `StateChanged`: if no player is `Alive`, start the wipe flow. Respawn steps:

1. Server picks the respawn point.
2. `Health.ResetHealth()` on the server; `NetworkHealth` replicates the state.
3. Teleport: for owner-authoritative players the owner must move itself; send an owner-targeted RPC and call `NetworkTransform.Teleport` there (see `netcode-for-gameobjects`). For a `CharacterController`, disable it, set the pose, re-enable (see `character-controller-3d`).
4. Grant spawn protection with `SetInvulnerable(3f)`.
5. Re-enable components the glue disabled (the glue does it on `Alive`).

Enemies on wipe: leash them home (clear threat) so players do not respawn into a fight, or reset the encounter. Persistent losses (dropped inventory, durability) belong to the save model in `save-system`.

## Fall damage and kill volumes

- Fall damage: measure the vertical speed at landing in the motor (`character-controller-3d`) and apply `DamageType.Fall` over a threshold (for example `(speed - 12) * 8`).
- Kill volumes (out of world): `Kill(null)` bypasses downed; for co-op, consider teleporting players back to the last safe ground with a small damage instead.
