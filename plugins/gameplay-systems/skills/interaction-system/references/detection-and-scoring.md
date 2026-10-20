# Detection and scoring

Read when choosing a detection mode, tuning scores and hysteresis, fixing the wrong object being picked, or profiling detection.

## Modes

| Mode | How | Best for | Weak spots |
|---|---|---|---|
| `CameraRay` | `Physics.SphereCast` from the output camera along its forward, length = camera-to-eye distance + `rayReach`, first hit wins | First-person, aimed over-the-shoulder, small objects on shelves | Third-person with a far orbit camera: the player must aim the camera at things |
| `Proximity` | `Physics.OverlapSphereNonAlloc` around the body, every candidate scored | Third-person, gamepad players, large objects (doors, beds, crafting benches) | Dense clutter: needs angle weight and priorities |
| `CameraRayThenProximity` | Ray first; if it finds nothing usable, proximity | Mixed games (survival, crafting) | Two code paths to tune |

The ray uses `interactableMask | occluderMask`, so a wall in front of an item is the first hit and blocks it. The sweep then needs the hit collider (or a parent) to implement `IInteractable`.

`SphereCast` does not report colliders that already overlap the sphere at its start point. Keep `rayRadius` small (0.05-0.15) and start the cast at the camera, never inside the character.

## Reach

- Reach is measured from the body (`Interactor.Body`) to `IInteractable.InteractionPoint`, not from the camera. A far third-person camera must not extend the player's reach.
- Each object has its own `MaxDistance`: 1.5 m for small pickups, 2.5 m for doors and containers, larger for big machines.
- Place `InteractionPoint` (a child transform) where the hand would go: door handle, chest lid, item centre. It is used for reach, scoring, line of sight and prompt placement.

## Scoring (proximity)

```
score = priority * priorityWeight
      + (1 - distance / proximityRadius) * distanceWeight
      + (1 - angle / maxViewAngle) * angleWeight
      + (candidate is current focus ? switchMargin : 0)
```

- `distance` and `angle` are measured on the horizontal plane against the flattened camera forward (or the body forward without a camera).
- Candidates outside `maxViewAngle` or without line of sight are skipped.
- `priority` dominates by default (`priorityWeight` 10): a quest item (priority 1) beats a nearby rock (priority 0). Use it sparingly; most objects stay at 0.
- `switchMargin` is hysteresis: the current focus keeps a bonus so two similar candidates do not swap every frame. 0.1-0.25 is usually enough.

## Line of sight

`Physics.Linecast(eye, InteractionPoint, occluderMask, QueryTriggerInteraction.Ignore)`. A hit on the candidate's own collider (or a child of the same interactable) still counts as visible. Keep the interactable's own solid collider on a layer the linecast may hit, or move `InteractionPoint` slightly in front of the surface.

## Colliders

- Use a dedicated trigger collider on the `Interactable` layer for detection, sized generously (a door's whole panel, a sphere around a small item), and a separate solid collider for physics.
- A single interactable may have several colliders; `GetComponentInParent<IInteractable>()` maps each to the same object, and scoring is identical for each.
- Pickups lying on the ground often fall partly into terrain. Put the trigger above the mesh so the linecast from the eye reaches it.

## Performance

- Fixed `Collider[]` buffer (32). `OverlapSphereNonAlloc` returns how many were written and does not grow the buffer; a full buffer means some colliders were missed. Raise the size or shrink the radius if `count == buffer.Length` happens in dense scenes.
- `GetComponentInParent<T>()` walks up the hierarchy; keep interactables shallow or cache a `Collider -> IInteractable` dictionary filled in `OnEnable`.
- Detection every frame is cheap for one local player. If profiling shows otherwise, run it every 2-3 frames or at 10-20 Hz; keep input reading every frame.
- Only the local player's `Interactor` runs detection; `NetworkInteractor` disables it on remote players.

## Interactor locks

```csharp
private void OnEnable() => this.interactor.AddLock(this);

private void OnDisable() => this.interactor.RemoveLock(this);
```

Any system that should stop interaction adds itself as a lock owner: inventory screen, dialogue, crafting menu, mounted vehicle, being knocked down. Locks are a set; adding the same owner twice is harmless, and the interactor resumes only when every owner has removed itself.
