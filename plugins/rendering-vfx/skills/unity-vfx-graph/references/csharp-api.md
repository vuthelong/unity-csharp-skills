# VisualEffect C# API reference (VFX Graph 17.x)

Namespaces: `UnityEngine.VFX` (runtime), `UnityEngine.VFX.Utility` (binders, output-event handlers), `UnityEngine.VFX.SDF` (runtime SDF baking).

## Contents

- [Property access](#property-access)
- [Playback and time](#playback-and-time)
- [Events and payloads](#events-and-payloads)
- [Output events](#output-events)
- [Property binders](#property-binders)
- [GraphicsBuffer data](#graphicsbuffer-data)
- [Runtime SDF baking](#runtime-sdf-baking)
- [Custom spawner callbacks](#custom-spawner-callbacks)
- [Auditing assets](#auditing-assets)

## Property access

Each exposed Blackboard type has a `Has*`, `Get*`, and `Set*` triple. All accept `int nameID`, `string name`, or `ExposedProperty`.

| Blackboard type | C# setter |
|---|---|
| bool | `SetBool` |
| int / uint | `SetInt` / `SetUInt` |
| float | `SetFloat` |
| Vector2 / Vector3 / Vector4 | `SetVector2` / `SetVector3` / `SetVector4` |
| Color | `SetVector4` (Color converts implicitly) |
| Position / Direction / Vector (spaceable types) | `SetVector3` (the space is set in the graph) |
| Matrix4x4 / Transform | `SetMatrix4x4` |
| Texture2D / Texture3D / Cubemap / Texture2DArray | `SetTexture` |
| Mesh | `SetMesh` |
| SkinnedMeshRenderer | `SetSkinnedMeshRenderer` |
| Gradient | `SetGradient` |
| AnimationCurve | `SetAnimationCurve` |
| GraphicsBuffer | `SetGraphicsBuffer` |

`ExposedProperty` is a serializable name holder that hashes once and converts implicitly from `string`:

```csharp
[SerializeField] ExposedProperty _spawnRate = "SpawnRate";
_vfx.SetFloat(_spawnRate, 120f);
```

`ResetOverride(id)` returns a property to the asset default. `HasFloat` and friends return false for a wrong name *or* a wrong type.

Gradients and curves are copied to GPU textures on set; do not call `SetGradient` / `SetAnimationCurve` every frame.

## Playback and time

| Member | Use |
|---|---|
| `Play()` / `Stop()` | Send the asset's play/stop events (`OnPlay`/`OnStop` by default). `Stop` halts spawning only. |
| `Reinit()` | Kill all particles, reset spawners and seed, re-send the initial event. Use when reusing a pooled instance. |
| `initialEventName` | Event sent on enable/Reinit. Set to empty to make the effect wait for an explicit event. |
| `pause` / `playRate` | Freeze or scale simulation for this instance. |
| `Simulate(float stepDeltaTime, uint stepCount = 1)` | Advance simulation manually (prewarm, scrubbing). |
| `resetSeedOnPlay` / `startSeed` | Deterministic replays. |
| `aliveParticleCount` / `HasAnyAliveParticles()` | Asynchronous readback; lags a few frames. |
| `culled` | True when the instance was culled last frame. |
| `visualEffectAsset` | Swap the asset at runtime (re-allocates). |

Pool return pattern: `Stop()`, wait until `!HasAnyAliveParticles()` (poll in a coroutine or Update), then deactivate. On reuse: activate, `Reinit()`, set properties, `Play()`.

## Events and payloads

```csharp
VFXEventAttribute payload = vfx.CreateVFXEventAttribute();
payload.SetVector3(Shader.PropertyToID("position"), hitPoint);
payload.SetVector3(Shader.PropertyToID("color"), new Vector3(1f, 0.4f, 0.1f));
payload.SetFloat(Shader.PropertyToID("size"), 0.2f);
vfx.SendEvent(Shader.PropertyToID("OnHit"), payload);
```

- Create the payload from the same `VisualEffect` that receives it; it is sized for that asset's attributes.
- `SendEvent` copies the payload, so reuse one instance.
- Built-in attribute names are lower camelCase: `position`, `velocity`, `color`, `alpha`, `size`, `scale`, `lifetime`, `targetPosition`, `direction`, `angle`, `texIndex`, `spawnCount`. Custom attributes (declared in the Blackboard in 17.x) use their declared name.
- In the graph the payload arrives as **source** attributes on the Spawn -> Initialize link. Read them in Initialize with *Get Attribute: X (Source)* or *Inherit Source X* / *Set X* with Source composition. Without that block, the payload is ignored.
- Sending to an event name that no Event context listens for is a silent no-op. `VisualEffectAsset.GetEvents` lists valid names.

## Output events

An *Output Event* context in the graph sends a named CPU event when its spawn flow fires.

```csharp
static readonly int OnLandId = Shader.PropertyToID("OnLand");

void OnEnable() => _vfx.outputEventReceived += HandleOutput;
void OnDisable() => _vfx.outputEventReceived -= HandleOutput;

void HandleOutput(VFXOutputEventArgs args)
{
    if (args.nameId != OnLandId) return;
    Vector3 p = args.eventAttribute.GetVector3(Shader.PropertyToID("position"));
    AudioSource.PlayClipAtPoint(_landClip, p);
}
```

Prebuilt handlers in `UnityEngine.VFX.Utility` (add *VFX Output Event* components): Play Audio, Prefab Spawn (pooled), Rigidbody force, Cinemachine impulse, Unity Event. Output events break instancing batching for that asset.

## Property binders

Add a **VFX Property Binder** component next to the `VisualEffect`, then add binders in its list. Each binder writes an exposed property every frame (also in Edit mode).

Common built-ins: Transform (position/angles/scale), Position, Previous Position, Rigidbody Velocity, Light (color/brightness/radius), Plane, Sphere Collider, Box Collider, Raycast, Terrain, Hierarchy to Attribute Map, Multiple Position (to GraphicsBuffer), Audio Spectrum, UI Slider/Toggle, Enabled, Input Axis/Button/Key/Mouse/Touch (legacy Input Manager only; with the Input System package, drive values from your own script instead).

Custom binder:

```csharp
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

[AddComponentMenu("VFX/Property Binders/Health Binder")]
[VFXBinder("Gameplay/Health")]
public sealed class HealthBinder : VFXBinderBase
{
    [VFXPropertyBinding("System.Single")]
    public ExposedProperty HealthProperty = "Health";

    public Health Target;

    public override bool IsValid(VisualEffect component) =>
        Target != null && component.HasFloat(HealthProperty);

    public override void UpdateBinding(VisualEffect component) =>
        component.SetFloat(HealthProperty, Target.Normalized);

    public override string ToString() => $"Health : '{HealthProperty}' -> {(Target ? Target.name : "(null)")}";
}
```

Binders cost a C# call per frame per binder. For hundreds of instances, write properties from one manager script only when values change.

## GraphicsBuffer data

Pass arbitrary structured data (targets, bone positions, flow-field samples) to the graph.

```csharp
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.VFX;

[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
public struct AttractorData
{
    public Vector3 Position;
    public float Strength;
}

public sealed class AttractorFeed : MonoBehaviour
{
    static readonly int BufferId = Shader.PropertyToID("Attractors");
    static readonly int CountId = Shader.PropertyToID("AttractorCount");

    [SerializeField] VisualEffect _vfx;
    [SerializeField] int _maxCount = 64;

    GraphicsBuffer _buffer;
    AttractorData[] _data;

    void OnEnable()
    {
        _data = new AttractorData[_maxCount];
        _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _maxCount, Marshal.SizeOf<AttractorData>());
        _vfx.SetGraphicsBuffer(BufferId, _buffer);
    }

    public void Upload(int count)
    {
        _buffer.SetData(_data, 0, 0, count);
        _vfx.SetInt(CountId, count);
    }

    void OnDisable()
    {
        _buffer?.Release();
        _buffer = null;
    }
}
```

- `[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]` makes the struct selectable in the *Sample Graphics Buffer* operator. Recompile, then pick the type in the operator.
- Stride must be a multiple of 4 and match the struct layout. Prefer `float`/`Vector*` fields; avoid `bool`.
- The graph cannot query the element count; pass it as a separate exposed `int` and clamp indices (`particleId % count`).
- Never resize a bound buffer. Allocate for the maximum, or release, recreate, and rebind.
- Release in `OnDisable`/`OnDestroy`; leaked buffers show up in the Memory Profiler and as warnings on domain reload.

## Runtime SDF baking

Bake SDFs in the Editor with *Window > Visual Effects > Utilities > SDF Bake Tool* (outputs a Texture3D asset). For meshes that change at runtime:

```csharp
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.VFX.SDF;

public sealed class RuntimeSdf : MonoBehaviour
{
    static readonly int SdfId = Shader.PropertyToID("SDF");

    [SerializeField] VisualEffect _vfx;
    [SerializeField] Mesh _mesh;
    [SerializeField] int _maxResolution = 64;

    MeshToSDFBaker _baker;

    void Start()
    {
        Bounds b = _mesh.bounds;
        _baker = new MeshToSDFBaker(b.size * 1.2f, b.center, _maxResolution, _mesh);
        _baker.BakeSDF();
        _vfx.SetTexture(SdfId, _baker.SdfTexture);
    }

    void OnDestroy() => _baker?.Dispose();
}
```

Baking is GPU work proportional to resolution cubed; bake once or at low frequency, not per frame at high resolution. Pair the SDF with a Transform property describing its box so *Conform to SDF* / *Collision Shape (SDF)* / *Set Position (SDF)* place it correctly.

## Custom spawner callbacks

For spawn logic that blocks cannot express, derive from `VFXSpawnerCallbacks` (a ScriptableObject) and add it as a *Custom Spawner* block. Override `OnPlay`, `OnUpdate`, and `OnStop(VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)`; write `state.spawnCount` and `state.vfxEventAttribute`. Runs on the CPU per instance per frame.

## Auditing assets

Editor-only audit of what C# can drive, without opening the graph:

```csharp
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine.VFX;

public static class VfxAudit
{
    [MenuItem("Tools/VFX/Audit Exposed Properties")]
    static void Audit()
    {
        var sb = new StringBuilder();
        var props = new List<VFXExposedProperty>();
        var events = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:VisualEffectAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            if (asset == null) continue;
            props.Clear();
            events.Clear();
            asset.GetExposedProperties(props);
            asset.GetEvents(events);
            sb.AppendLine(path);
            foreach (var p in props) sb.AppendLine($"  {p.type.Name} {p.name}");
            sb.AppendLine($"  events: {string.Join(", ", events)}");
        }
        UnityEngine.Debug.Log(sb.ToString());
    }
}
```
