# Common CPU hotspots and fixes

Read when a capture points at Update fan-out, allocations, component lookups, physics queries, Animator, uGUI rebuilds or logging, and you need the fix pattern. Re-profile after each fix; skip any fix the capture does not justify.

## Update fan-out

Each `MonoBehaviour.Update` is a call from native code into C#. Thousands of small `Update`s show as a long `BehaviourUpdate` with a high Calls count and little Self time per call. Drive them from one loop instead.

```csharp
using System.Collections.Generic;
using UnityEngine;

public interface ITickable
{
    void Tick(float deltaTime);
}

public sealed class TickManager : MonoBehaviour
{
    private readonly List<ITickable> _tickables = new(256);

    public void Register(ITickable tickable) => _tickables.Add(tickable);

    public void Unregister(ITickable tickable)
    {
        int index = _tickables.IndexOf(tickable);
        if (index < 0)
        {
            return;
        }

        int last = _tickables.Count - 1;
        _tickables[index] = _tickables[last];
        _tickables.RemoveAt(last);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        for (int i = _tickables.Count - 1; i >= 0; i--)
        {
            _tickables[i].Tick(deltaTime);
        }
    }
}
```

- Register in `OnEnable`, unregister in `OnDisable`. The swap-remove changes order; use a stable removal if order matters.
- Tick things that do not need every frame (AI decisions, sensors, UI refresh) at a lower rate or round-robin a slice per frame.
- On a co-op host, server-side systems run for every player; budget them per connected player.

## Per-frame allocations

| Source | Replace with |
|---|---|
| LINQ (`Where`, `Select`, `OrderBy`, `ToList`) in `Update` | `for` loops over cached `List<T>` or arrays |
| String concatenation, interpolation, `string.Format` for HUD text | Update text only when the value changes; `TMP_Text.SetText("{0}", value)` formats without a new string |
| `foreach` over an interface (`IEnumerable<T>`, `IList<T>`) | `for` over the concrete `List<T>` or array |
| Boxing: value types passed as `object`, `params object[]`, struct cast to an interface | Generic methods, typed overloads, no `object` APIs in hot code |
| Lambdas capturing locals or `this` created per call | Cache the delegate in a field, or use a static lambda with state passed in |
| `new WaitForSeconds(x)` inside a coroutine loop | Cache the instance |
| `GetComponents<T>()`, `Physics.RaycastAll`, `Physics.OverlapSphere` returning arrays | `GetComponents(List<T>)`, `RaycastNonAlloc`, `OverlapSphereNonAlloc` with a reused buffer |
| `mesh.vertices`, `renderer.materials`, `gameObject.name`, `tag ==` | Cache, `GetVertices(List<Vector3>)`, `sharedMaterial`, `CompareTag` |
| `new List<T>()` / `new T[]` per call | Reuse a field and `Clear()`, or `UnityEngine.Pool.ListPool<T>` |

Verify with the GC Alloc column in a player: the target is 0 B in steady gameplay.

## Component and object lookups

- Cache `GetComponent<T>()` results in `Awake`. Use `TryGetComponent` for optional components (no Editor allocation when the component is missing).
- `Camera.main` searches for the tagged camera (it shows as `FindMainCamera`); cache it.
- Never call `FindAnyObjectByType`, `FindObjectsByType`, `GameObject.Find` in per-frame code. Pass references through serialized fields or a registry.
- Unity 6 API: `FindObjectOfType` is obsolete; see `csharp-unity`.

## Physics

- `Physics.Simulate` or `FixedBehaviourUpdate` appearing two or more times in one frame means the fixed timestep is catching up after a long frame, which makes the next frame long too. Fix the long frame first; then review Project Settings > Time > Fixed Timestep and Maximum Allowed Timestep.
- High `Physics.Simulate`: too many active rigidbodies or contacts, MeshColliders on moving bodies, overlapping triggers. Use primitive colliders, the Layer Collision Matrix, and let bodies sleep.
- High `Physics.ProcessReports`: heavy work in `OnCollisionStay` / `OnTriggerStay`. Move logic to Enter/Exit or poll on a timer.
- Queries: use `NonAlloc` variants with a reused buffer, always pass a `LayerMask` and a max distance, and do not repeat the same query in several components each frame.
- `Rigidbody.SetKinematic` warnings: toggling kinematic on a body with a non-convex MeshCollider rebuilds it.
- Details and collider setup: `physics-3d-collision`.

## Animator

- Set **Culling Mode** to Cull Update Transforms or Cull Completely for characters that can be off-screen. `Director.PrepareFrame` still evaluates every active, enabled Animator's state machine regardless of culling, so disable Animators on far-away or hidden objects.
- Fewer layers and simpler state machines reduce `Director.PrepareFrameJob` time. `StateMachineBehaviour`s implementing `OnStateMachineEnter`/`OnStateMachineExit` force that evaluation onto the main thread.
- Avoid several Animators in one transform hierarchy; transform writes are grouped by hierarchy root and parallelize poorly.
- Use `Animator.StringToHash` once and pass the int to `SetFloat`/`SetTrigger`.
- Crowds of simple props: consider baked or vertex animation instead of an Animator each. See `animation-authoring`.

## uGUI Canvas rebuilds

- Any change to a graphic (text, color, fill, enabling) marks its Canvas dirty and rebuilds that Canvas's batches. Put frequently changing elements (timers, health bars, damage numbers) on their own nested Canvas, separate from static panels.
- Layout Groups and Content Size Fitters recalculate up the hierarchy on change; avoid them on elements that change every frame.
- Turn off **Raycast Target** on graphics that are not interactive.
- Hide panels by disabling the Canvas component (or a CanvasGroup with alpha 0 and blocking off) instead of toggling many GameObjects.
- Update HUD text only when the value changes. TextMeshPro specifics (AutoSize, atlas, rebuilds): `optimize-text-mesh-pro`; layout: `ui-ugui`.

## Logging in builds

- `Debug.Log` formats the message, captures a stack trace and writes to the player log on every call. A few per frame show up as both CPU time and `GC.Alloc`.
- Set Player Settings > Other Settings > **Stack Trace** for Log and Warning to None in release builds.
- Remove debug logs from release builds at compile time with a wrapper marked `[System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]` and `[System.Diagnostics.Conditional("UNITY_EDITOR")]`, so the arguments are not even evaluated. `Debug.unityLogger.logEnabled = false` stops output but still evaluates the arguments.
- Structured, allocation-free logging: `zlogger`.

## Burst and jobs (pointer only)

Consider the Job System with Burst when one data-parallel loop dominates the main thread (hundreds of agents doing the same math, per-vertex or per-cell work) and the data can live in `NativeArray`s. Schedule early in the frame, complete as late as possible, and check `JobHandle.Complete` / `WaitForJobGroupID` samples in the Timeline with Flow Events for sync points that stall the main thread. Do not port `MonoBehaviour` logic that touches GameObjects; restructure the data first.
