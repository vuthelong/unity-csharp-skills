# Constraint components (UnityEngine.Animations)

| Component | Drives | Key fields |
|---|---|---|
| `PositionConstraint` | position | `translationAxis`, `translationOffset`, `translationAtRest` |
| `RotationConstraint` | rotation | `rotationAxis`, `rotationOffset`, `rotationAtRest` |
| `ScaleConstraint` | local scale | `scalingAxis`, `scaleOffset`, `scaleAtRest` |
| `ParentConstraint` | position + rotation as if parented | per-source `SetTranslationOffset(i, v)`, `SetRotationOffset(i, euler)`, `translationAxis`, `rotationAxis` |
| `AimConstraint` | rotation to face sources | `aimVector`, `upVector`, `worldUpType`, `worldUpObject`, `worldUpVector`, `rotationOffset` |
| `LookAtConstraint` | rotation, +Z faces source | `roll`, `useUpObject`, `worldUpObject`, `rotationOffset` |

All implement `IConstraint`: `AddSource`, `SetSource`, `RemoveSource`, `GetSources`, `sourceCount`, `weight`, `constraintActive`, `locked`.

- `constraintActive`: whether the constraint drives the transform.
- `locked`: when false, editing the transform updates offsets / at-rest values instead of being overridden (Inspector authoring mode). Lock it for runtime use.
- Sources are `Transform`s; each `ConstraintSource` has its own `weight`. With several sources the result is the weighted blend.

## Aim a turret

```csharp
using UnityEngine;
using UnityEngine.Animations;

public static class TurretSetup
{
    public static AimConstraint AimAt(GameObject turret, Transform target)
    {
        if (!turret.TryGetComponent(out AimConstraint aim))
            aim = turret.AddComponent<AimConstraint>();
        aim.AddSource(new ConstraintSource { sourceTransform = target, weight = 1f });
        aim.aimVector = Vector3.forward;
        aim.upVector = Vector3.up;
        aim.worldUpType = AimConstraint.WorldUpType.SceneUp;
        aim.rotationAxis = Axis.Y;
        aim.constraintActive = true;
        aim.locked = true;
        return aim;
    }
}
```

`rotationAxis = Axis.Y` restricts a turret base to yaw; give the barrel its own AimConstraint on `Axis.X`.

## Attach a prop to a bone without reparenting, keeping its current pose

```csharp
using UnityEngine;
using UnityEngine.Animations;

public static class PropAttach
{
    public static ParentConstraint Attach(Transform prop, Transform bone)
    {
        if (!prop.TryGetComponent(out ParentConstraint pc))
            pc = prop.gameObject.AddComponent<ParentConstraint>();
        int index = pc.AddSource(new ConstraintSource { sourceTransform = bone, weight = 1f });

        Quaternion inv = Quaternion.Inverse(bone.rotation);
        pc.SetTranslationOffset(index, inv * (prop.position - bone.position));
        pc.SetRotationOffset(index, (inv * prop.rotation).eulerAngles);

        pc.constraintActive = true;
        pc.locked = true;
        return pc;
    }
}
```

To drop the prop, set `pc.constraintActive = false` (it stays where it is) or set the source weight to 0.

## Pitfalls

- Never write `GetComponent<T>() ?? AddComponent<T>()`: `??` bypasses Unity's null check and, in the Editor, a missing component is a fake-null object, so nothing gets added. Use `TryGetComponent`.
- Adding a constraint with `constraintActive = true` and zero offsets snaps the object onto the source. Compute offsets first, as above.
- Constraints evaluate after the Animator in the same frame; an animated property that a constraint also drives is overwritten by the constraint.
- Do not chain constraints in a cycle (A sources B, B sources A); results are undefined.
- For per-bone IK (two-bone IK, multi-aim on a spine, damped transforms) use Animation Rigging (`com.unity.animation.rigging`) `RigBuilder` + `Rig` + rig constraints; those run inside the Animator's job graph and can be weighted per rig.
