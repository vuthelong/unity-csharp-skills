---
name: splines-paths
description: Creates, edits and samples Unity Splines 2.x (com.unity.splines, namespace UnityEngine.Splines) in Unity 6 - SplineContainer, Spline, BezierKnot and TangentMode, world vs local evaluation (EvaluatePosition / EvaluateTangent / EvaluateUpVector), arc-length and nearest-point queries with SplineUtility, NativeSpline for jobs, and the SplineAnimate, SplineExtrude and SplineInstantiate components. Use when the user wants a path, rail, track, road, river or patrol route, an object or camera that moves along a curve at constant speed, objects placed along a path, mesh extruded along a curve, closest point on a path, or reports objects drifting off the spline, wrong positions after moving the container, or uneven speed.
license: MIT
metadata:
  category: gameplay-systems
  sources: "AlexeyPerov/Unity-Open-MCP/skills/extensions/splines"
  unity: "6000.0+"
---

# Splines and Paths (Splines 2.x)

## Vocabulary

- **SplineContainer**: MonoBehaviour holding one or more `Spline`s (`container.Spline` is index 0, `container.Splines` all) plus knot links between them.
- **Spline**: ordered list of `BezierKnot`s, optionally `Closed`.
- **BezierKnot**: `Position`, `TangentIn`, `TangentOut`, `Rotation`; tangents are in the knot's local frame (rotated by `Rotation`). Positions are local to the container's transform.
- **TangentMode** per knot: `AutoSmooth` (Catmull-Rom style, tension via `SetAutoSmoothTension`), `Linear` (sharp corners), `Mirrored`, `Continuous` (same direction, independent lengths), `Broken` (independent).

## Workflow

1. **Ensure the package** `com.unity.splines` is installed (Unity 6 resolves 2.x). Scripts need `using UnityEngine.Splines;` and `using Unity.Mathematics;` (`float3`, `quaternion`).
2. **Build** the container and knots: `spline.Add(new BezierKnot(pos), TangentMode.AutoSmooth)`. A spline needs two or more knots to evaluate; a closed loop needs three or more.
3. **Shape** with `spline.SetTangentMode(index, mode)` (or `SetTangentMode(mode)` for all) and `spline[i] = knot` to move a knot. In the Editor, `Undo.RecordObject(container, ...)` first.
4. **Use it**: drive objects with `SplineAnimate`, generate meshes with `SplineExtrude`, scatter with `SplineInstantiate`, or sample in code.
5. **Verify** visually in the Scene view (splines draw with knot gizmos when the container is selected) and by sampling `t = 0, 0.5, 1`.

Code for every step, constant-speed movement, nearest point, and jobs: [references/splines-api.md](references/splines-api.md) (read before writing spline code).

## Space rules (most common bug)

- `container.EvaluatePosition(t)`, `EvaluateTangent`, `EvaluateUpVector`, `container.Evaluate(...)`, `container.CalculateLength()` return **world space**.
- `spline.EvaluatePosition(t)` and all `SplineUtility` functions on a raw `Spline` work in the **container's local space**. Convert with `container.transform.TransformPoint` / `InverseTransformPoint`, or pass `container.transform.localToWorldMatrix` where an overload accepts it.
- Knot positions you write are local. Convert world points with `InverseTransformPoint` before `Add`.

## Parameter rules

- `t` is normalized 0..1 over the whole spline. It is distance-approximated across curves, but for exact constant speed advance by distance: `SplineUtility.GetPointAtLinearDistance` or convert with `ConvertIndexUnit(..., PathIndexUnit.Distance, ...)`.
- Cache `CalculateLength()`; recompute only after the spline changes (`Spline.Changed` event).
- Tangent vectors are not normalized; normalize before using as a facing direction.

## Components

| Component | Use | Key fields |
|---|---|---|
| `SplineAnimate` | Move a GameObject along a container | `Container`, `AnimationMethod` (Time / Speed), `Duration` or `MaxSpeed`, `Loop` (Once, Loop Continuous, Ease In Then Continuous, Ping Pong), `Easing`, `Alignment` (None, Spline Element, Spline Object, World), `ObjectForwardAxis`, `ObjectUpAxis`, `PlayOnAwake`; methods `Play`, `Pause`, `Restart(bool)`; `NormalizedTime`, `ElapsedTime`; `Completed` event |
| `SplineExtrude` | Tube/road mesh along a spline | `Container`, `Radius`, `Sides`, `SegmentsPerUnit`, `Capped`, `Range`; needs MeshFilter + MeshRenderer; `Rebuild()` |
| `SplineInstantiate` | Place prefabs along a spline | Items with probability, spacing mode (count / distance), offsets, alignment |

## Pitfalls

- Moving or rotating the container moves the whole path; knots stay local. Scale the container uniformly.
- `BezierKnot` is a struct: `var k = spline[i]; k.Position = p;` edits a copy. Assign it back with `spline[i] = k` or `spline.SetKnot(i, k)`.
- `TangentMode.AutoSmooth` overwrites hand-set tangents whenever neighbours move. Switch the knot to `Broken`/`Mirrored`/`Continuous` before setting tangents manually.
- `SplineAnimate` writes the transform every frame; do not also move that object with physics or another script. For physics bodies, sample positions and call `Rigidbody.MovePosition` in `FixedUpdate`.
- For hundreds of evaluations per frame, convert to `NativeSpline` and evaluate in a Burst job; managed `Spline` evaluation allocates nothing but is not cache-friendly.

## Related skills

- `cinemachine-cameras`: `CinemachineSplineDolly` / `CinemachineSplineCart` use a SplineContainer.
- `initialize-ai-navigation`: spline-defined patrol routes sampled into NavMeshAgent destinations.
- `level-geometry-authoring`: roads and rivers carved into terrain along a spline.
