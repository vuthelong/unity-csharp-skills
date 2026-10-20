# Hit detection

Read when choosing melee sweeps vs arcs vs triggers, writing hitscan or projectile hits, setting up hurtbox layers, or debugging missed and double hits.

## Layers

| Layer | Contents | Queried by |
|---|---|---|
| `Hurtbox` | Trigger primitives on characters and destructibles, each with a `Hurtbox` component | Melee, hitscan, projectiles (`QueryTriggerInteraction.Collide`) |
| `Perceivable` | Trigger on players for AI sight (`enemy-ai-behavior`) | AI perception only |
| Environment layers | Walls, terrain, props | Occlusion rays (`QueryTriggerInteraction.Ignore`) |
| Character movement | `CharacterController` / capsule colliders | Movement only; not used for damage |

Keep damage colliders separate from movement colliders: movement capsules are sized for navigation, are often disabled on remote copies in co-op, and would make headshots impossible.

## Choosing a method

| Method | Use for | Notes |
|---|---|---|
| `StrikeArc` (one `OverlapSphereNonAlloc` + angle + optional occlusion ray) | Slow, telegraphed melee; AI attacks; co-op server checks | Cheapest, independent of bones and frame rate, easy to make generous |
| Swept blade (`BeginSwing`, `OverlapCapsuleNonAlloc` from last to current position per blade point) | Weapon-accurate melee, long sweeping attacks | Needs bones animated where detection runs |
| Hitscan (`Physics.Raycast`) | Guns, beams, instant spells | Nearest hit only; walls block automatically if they are in the mask |
| Swept projectile (`Physics.SphereCastNonAlloc` from last to next position) | Arrows, fireballs, thrown items | No tunnelling at high speed, no Rigidbody needed |
| Trigger hitbox (`OnTriggerEnter`) | Persistent hazards (fire area, spikes) with `OnTriggerStay` ticks | Needs a Rigidbody (kinematic is fine) on one side and the hazard layer enabled against `Hurtbox` in the collision matrix; poor for fast swings |

## Swept melee details

- Blade points: 2-4 transforms along the weapon (hilt, middle, tip). Each point sweeps a capsule from its previous position to its current one with radius `bladeRadius`, so a fast swing that crosses a target between frames still hits.
- Dedupe per `Health` per swing; reset in `BeginSwing`.
- Hit point: `Collider.ClosestPoint(reference)` on the hurtbox. It returns the input position for a disabled collider and a point inside when the reference is inside, so keep hurtboxes primitive and enabled.
- Detection runs in `LateUpdate`, after the Animator has posed the bones for this frame. Physics colliders of the *targets* reflect the transforms as of the last physics sync, so they can trail by one physics step. For slow games ignore it; if needed, call `Physics.SyncTransforms()` once before a batch of queries, not per detector.
- Animation events: `BeginSwing` on the active-frames start, `EndSwing` on the end. Also call `EndSwing` from the state exit or when the attack is interrupted (stagger, death), otherwise the blade stays live.

## Arc details

`StrikeArc(origin, forward, range, angle, damage, type)`:

1. `OverlapSphereNonAlloc(origin, range, buffer, hurtboxMask, Collide)`.
2. For each hurtbox, flatten `ClosestPoint(origin) - origin` and compare its angle to `forward` with `angle / 2`.
3. Optional occlusion ray from origin to the closest point against `occluderMask` (leave the mask empty to skip).
4. Apply once per `Health`.

## Hitscan

```csharp
using UnityEngine;

public class HitscanWeapon : MonoBehaviour
{
    [SerializeField] private Health owner;
    [SerializeField] private Transform muzzle;
    [SerializeField] private float range = 60f;
    [SerializeField] private float damage = 18f;
    [SerializeField] private LayerMask hitMask;

    public bool Fire(Vector3 direction, out Vector3 endPoint)
    {
        var origin = this.muzzle.position;
        endPoint = origin + direction * this.range;
        if (!Physics.Raycast(origin, direction, out var hit, this.range, this.hitMask, QueryTriggerInteraction.Collide)) return false;

        endPoint = hit.point;
        if (!hit.collider.TryGetComponent<Hurtbox>(out var hurtbox) || hurtbox.Owner == this.owner) return false;

        var info = new DamageInfo(this.damage, DamageType.Physical, this.owner.gameObject, this.owner.Team, hit.point, direction);
        return hurtbox.ApplyHit(info) > 0f;
    }
}
```

`hitMask` = `Hurtbox` + environment layers, so the nearest collider wins and walls stop bullets. With `Collide`, any trigger on a layer in the mask is hittable; keep gameplay triggers (pickups, zones) on layers outside it. Start the ray at the muzzle, outside the shooter's own hurtboxes: a raycast does not report colliders that contain its origin, but it does report the shooter's other hurtboxes further along the ray.

## Swept projectile

```csharp
using UnityEngine;

public class SweptProjectile : MonoBehaviour
{
    [SerializeField] private float radius = 0.1f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private LayerMask hitMask;

    private readonly RaycastHit[] _hits = new RaycastHit[8];
    private Vector3 _velocity;
    private DamageInfo _damage;
    private Health _owner;
    private float _despawnTime;

    public void Launch(Vector3 velocity, in DamageInfo damage, Health owner)
    {
        this._velocity = velocity;
        this._damage = damage;
        this._owner = owner;
        this._despawnTime = Time.time + this.lifetime;
    }

    private void FixedUpdate()
    {
        if (Time.time >= this._despawnTime)
        {
            gameObject.SetActive(false);
            return;
        }

        var dt = Time.fixedDeltaTime;
        this._velocity.y += this.gravity * dt;
        var step = this._velocity * dt;
        var distance = step.magnitude;
        if (distance <= 0f) return;

        var direction = step / distance;
        var count = Physics.SphereCastNonAlloc(transform.position, this.radius, direction, this._hits, distance, this.hitMask, QueryTriggerInteraction.Collide);
        var nearest = -1;
        for (var i = 0; i < count; i++)
        {
            var hit = this._hits[i];
            if (hit.collider.TryGetComponent<Hurtbox>(out var hurtbox) && hurtbox.Owner == this._owner) continue;
            if (nearest < 0 || hit.distance < this._hits[nearest].distance) nearest = i;
        }

        if (nearest < 0)
        {
            transform.position += step;
            return;
        }

        var best = this._hits[nearest];
        var point = best.distance <= 0f ? transform.position : best.point;
        if (best.collider.TryGetComponent<Hurtbox>(out var target)) target.ApplyHit(this._damage.WithHit(point, direction));
        gameObject.SetActive(false);
    }
}
```

- The `SphereCastNonAlloc` result order is undefined, so pick the nearest hit yourself.
- A collider already overlapping the sphere at the start of the sweep reports distance 0, point zero and a normal opposite the sweep (per the `SphereCastAll` docs); the snippet substitutes the projectile position.
- Replace `SetActive(false)` with a pool release (`ObjectPool<T>`, see `csharp-unity`). In co-op, simulate projectiles on the server and show cosmetic copies on clients, or spawn them as server-owned `NetworkObject`s for slow, visible projectiles.

## Debugging misses and double hits

| Check | How |
|---|---|
| Query mask | Log `LayerMask.LayerToName(collider.gameObject.layer)` of the hurtbox and compare with the mask |
| Trigger interaction | Hurtboxes are triggers: the query must pass `Collide` |
| Buffer full | `count == buffer.Length` means hits were dropped; enlarge the buffer |
| Swing never ends | Missing `EndSwing` on interrupted attacks |
| Wrong owner | `Hurtbox` found the wrong `Health` via `GetComponentInParent`; assign `owner` explicitly |
| Server copy | In co-op, hurtboxes must stay enabled on non-owner copies of players |
| Gizmos | `MeleeHitDetector.OnDrawGizmosSelected` shows blade points; draw the arc in your own gizmo while tuning |
