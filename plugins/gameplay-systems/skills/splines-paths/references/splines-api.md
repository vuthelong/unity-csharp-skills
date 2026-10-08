# Splines 2.x API

```csharp
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
```

## Build a path from world points

```csharp
public static SplineContainer BuildPath(string name, Vector3[] worldPoints, bool closed)
{
    var go = new GameObject(name);
    var container = go.AddComponent<SplineContainer>();
    var spline = container.Spline;
    spline.Clear();

    foreach (var p in worldPoints)
    {
        float3 local = go.transform.InverseTransformPoint(p);
        spline.Add(new BezierKnot(local), TangentMode.AutoSmooth);
    }

    spline.Closed = closed;
    return container;
}
```

In the Editor wrap with `Undo.RegisterCreatedObjectUndo(go, "Create path")`, and use `Undo.RecordObject(container, "Edit path")` before modifying an existing spline.

Other constructors: `SplineFactory.CreateLinear(points, closed)` and `SplineFactory.CreateCatmullRom(points, closed)` (points in local space). Add a prebuilt spline with `container.AddSpline(spline)`.

## Edit knots

```csharp
var spline = container.Spline;
BezierKnot k = spline[2];
k.Position += new float3(0, 1, 0);
spline[2] = k;

spline.SetTangentMode(2, TangentMode.Broken);
spline.SetKnot(2, new BezierKnot(k.Position, new float3(0, 0, -1), new float3(0, 0, 2), k.Rotation));

spline.SetTangentMode(TangentMode.Linear);
spline.SetAutoSmoothTension(1, 0.3f);
spline.Insert(1, new BezierKnot(float3.zero), TangentMode.AutoSmooth);
spline.RemoveAt(0);
```

## Sample (world space through the container)

```csharp
float3 pos = container.EvaluatePosition(t);
float3 tangent = container.EvaluateTangent(t);
float3 up = container.EvaluateUpVector(t);
container.Evaluate(t, out float3 p, out float3 tan, out float3 upv);
float length = container.CalculateLength();

Quaternion facing = Quaternion.LookRotation(math.normalize(tangent), up);
```

For a container with several splines: `container.EvaluatePosition(splineIndex, t)`.

## Constant-speed mover

```csharp
public class SplineMover : MonoBehaviour
{
    [SerializeField] SplineContainer container;
    [SerializeField] float speed = 5f;
    [SerializeField] bool loop = true;

    float distance;
    float length;
    float localLength;

    void OnEnable()
    {
        CacheLengths();
        Spline.Changed += OnSplineChanged;
    }

    void OnDisable() => Spline.Changed -= OnSplineChanged;

    void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
    {
        if (spline == container.Spline) CacheLengths();
    }

    void CacheLengths()
    {
        length = container.CalculateLength();
        localLength = container.Spline.GetLength();
    }

    void Update()
    {
        if (length <= 0f) return;
        distance += speed * Time.deltaTime;
        distance = loop ? Mathf.Repeat(distance, length) : Mathf.Min(distance, length);

        float localDistance = distance / length * localLength;
        float t = SplineUtility.ConvertIndexUnit(container.Spline, localDistance, PathIndexUnit.Distance, PathIndexUnit.Normalized);
        container.Evaluate(t, out float3 pos, out float3 tangent, out float3 up);
        transform.SetPositionAndRotation(pos, Quaternion.LookRotation(math.normalize(tangent), up));
    }
}
```

`ConvertIndexUnit` on a raw `Spline` works in local units, so the world distance is rescaled to local before converting. For a ready-made component use `SplineAnimate` with `AnimationMethod = Speed`.

## Nearest point (local space in, local space out)

```csharp
public static Vector3 ClosestPointOnPath(SplineContainer container, Vector3 worldPoint, out float t)
{
    float3 local = container.transform.InverseTransformPoint(worldPoint);
    SplineUtility.GetNearestPoint(container.Spline, local, out float3 nearestLocal, out t);
    return container.transform.TransformPoint(nearestLocal);
}
```

Increase the optional `resolution` / `iterations` arguments for long or tightly curved splines.

## Distance walking

```csharp
float3 next = SplineUtility.GetPointAtLinearDistance(container.Spline, fromT, 2.5f, out float nextT);
```

Returns a local-space point 2.5 local units along the spline from `fromT`; useful for evenly spaced placement.

## Place objects every N metres

```csharp
public static void Scatter(SplineContainer container, GameObject prefab, float spacing, Transform parent)
{
    float length = container.CalculateLength();
    int count = Mathf.FloorToInt(length / spacing);
    for (int i = 0; i <= count; i++)
    {
        float t = count == 0 ? 0f : (float)i / count;
        container.Evaluate(t, out float3 pos, out float3 tangent, out float3 up);
        Object.Instantiate(prefab, pos, Quaternion.LookRotation(math.normalize(tangent), up), parent);
    }
}
```

`t` steps are distance-approximated; use `GetPointAtLinearDistance` when spacing must be exact. `SplineInstantiate` does the same without code.

## SplineAnimate from code

```csharp
var anim = cart.AddComponent<SplineAnimate>();
anim.Container = container;
anim.AnimationMethod = SplineAnimate.Method.Speed;
anim.MaxSpeed = 8f;
anim.Loop = SplineAnimate.LoopMode.PingPong;
anim.Alignment = SplineAnimate.AlignmentMode.SplineElement;
anim.Completed += () => Debug.Log("Arrived");
anim.Play();
```

## Jobs / Burst

```csharp
using Unity.Collections;

using var native = new NativeSpline(container.Spline, container.transform.localToWorldMatrix, Allocator.TempJob);
```

`NativeSpline` is world-space when built with a matrix and implements `ISpline`, so `SplineUtility.EvaluatePosition(native, t)` and friends work inside Burst jobs. Dispose it after the job completes.

## Multiple splines and links

- `container.AddSpline()` returns a new empty spline; `container.RemoveSplineAt(i)`.
- `container.KnotLinkCollection.Link(new SplineKnotIndex(0, 3), new SplineKnotIndex(1, 0))` makes knots move together (junctions).
- Per-knot data (road width, speed limits): `SplineData<float>` fields on your own component, keyed by `PathIndexUnit`.
