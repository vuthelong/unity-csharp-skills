---
name: unity-vfx-graph
description: Builds, drives, and debugs GPU particle effects with Visual Effect Graph (com.unity.visualeffectgraph 17.x) on Unity 6 URP. Covers .vfx assets, Spawn/Initialize/Update/Output contexts and blocks, exposed properties, the VisualEffect C# API (SetFloat, SetVector3, SetGraphicsBuffer, SendEvent, VFXEventAttribute, Reinit), property binders and custom VFXBinderBase, GraphicsBuffer and Point Cache/SDF data, GPU and output events, capacity, bounds and culling pitfalls, URP compatibility, and GPU performance. Use when the user mentions VFX Graph, Visual Effect Graph, VisualEffect, .vfx, VFXEventAttribute, VFXPropertyBinder, pCache, SDF Bake Tool, MeshToSDFBaker, millions of particles, or particles that vanish off-screen. For CPU Shuriken particles use `unity-particle-system`.
license: MIT
metadata:
  category: rendering-vfx
  sources: "a5c-ai/babysitter/library/specializations/game-development/skills/unity-vfx-graph, AlexeyPerov/Unity-Open-MCP/skills/extensions/vfx"
  unity: "6000.0+"
---

# Unity Visual Effect Graph (VFX Graph 17.x)

GPU-simulated particles authored as a node graph in a `.vfx` asset (`VisualEffectAsset`) and played by a `VisualEffect` component. Unity 6000.0 ships VFX Graph 17.0; each 6000.x minor ships the matching 17.x (6000.3 -> 17.3, and so on).

## Workflow

1. **Confirm the platform can run it.** VFX Graph needs compute shaders and an SRP (URP or HDRP). It does not run on the Built-in pipeline or on WebGL. On low-end mobile or WebGL fall back to `unity-particle-system`. Guard at runtime with `SystemInfo.supportsComputeShaders`.
2. **Install** `com.unity.visualeffectgraph` (see `unity-package-management`). Use the version that matches the Editor; do not pin a 17.x from a different minor.
3. **Create the asset** with *Assets > Create > Visual Effects > Visual Effect Graph*. Start from a template in the template picker rather than an empty graph.
4. **Author systems**: Spawn -> Initialize Particle -> Update Particle -> Output (see "Contexts" below). Set **Capacity** and **Bounds** in Initialize before anything else.
5. **Expose** every value that gameplay drives: create it in the Blackboard and tick *Exposed*. Name it in PascalCase without spaces, because C# addresses it by exact, case-sensitive name.
6. **Drive it from C#** via the `VisualEffect` API or property binders (see "Driving from C#").
7. **Profile** with the VFX window's *Profiling and Debug* panel (GPU time per context, alive count, capacity use) and the Frame Debugger, then tune capacity, overdraw, and culling.

Do not script edits to the graph itself. The editor graph model (`UnityEditor.VFX.VFXGraph`, `VFXContext`, `VFXBlock`, `VFXSlot`) is internal and changes between versions; a `.vfx` file is serialized YAML that is not safe to hand-edit. Use the public runtime surface (`VisualEffectAsset`, `VisualEffect`) from code and do graph edits in the VFX Graph window.

## Contexts and blocks

A **system** is a chain of contexts; each context holds a stack of **blocks** that run top to bottom. **Operators** (free-floating nodes) compute values that feed block inputs.

| Context | Runs | Typical blocks |
|---|---|---|
| Event | When an event name fires (`OnPlay`, `OnStop`, or a custom name) | none; wires into Spawn Start/Stop flow inputs |
| Spawn | CPU, per frame | Constant Spawn Rate, Single Burst, Periodic Burst, Variable Spawn Rate, Set Spawn Event attributes |
| Initialize Particle | GPU, once per new particle | Set Lifetime (Random), Set Position (Shape: Sphere/Box/Cone/Torus/Line/Mesh/SDF), Set Velocity (Random), Set Color, Set Size, Inherit/Get Source attributes |
| Update Particle | GPU, every frame per particle | Gravity, Linear Drag, Turbulence / Vector Field Force, Conform to Sphere/SDF, Collision Shape (sphere, box, plane, cylinder, SDF), Collide with Depth Buffer, Trigger Event (GPU events), Age/Reap (implicit) |
| Output | GPU draw | Output Particle Quad / Mesh / Lit Quad / Shader Graph / Strip; Orient, Set Size over Life, Color over Life, Flipbook Player |

Key settings live in the Inspector of a selected context, not on the node face:

- **Initialize > Capacity**: fixed GPU allocation. Particles spawned beyond capacity are dropped silently. Memory is paid for the full capacity even when no particles are alive.
- **Initialize > Bounds Setting Mode**: `Manual` (you type the box), `Recorded` (record in the VFX window with the *Bounds Recording* toggle, then apply), or `Automatic` (computed on GPU each frame; costs GPU time). Bounds drive culling, so too-small bounds make the effect disappear when its origin leaves the screen.
- **System Space**: Local or World, per system (the L/W toggle on the context). In Local space, moving the GameObject drags every live particle with it; trails and smoke usually need World.
- **Output > Blend Mode / Sort / Use Alpha Clipping / Cast Shadows / Generate Motion Vectors**. Sorting costs a GPU sort pass; turn it off for additive effects.

Data flow beyond one system:

- **GPU events**: `Trigger Event On Die`, `Trigger Event Rate`, `Trigger Event Always` in Update feed the **GPU Event** input of another system's Initialize (sparks on impact, sub-emitters). The receiving system needs its own capacity.
- **Output events** (CPU): an *Output Event* context sends named events back to C#; handle them with `VisualEffect.outputEventReceived` or the `VFXOutputEventAbstractHandler` components (play audio, spawn prefabs, rigidbody force).
- **Subgraphs**: Subgraph Operator, Subgraph Block, and Subgraph Context (`.vfxoperator`, `.vfxblock`, `.vfx`) package reusable logic. Prefer them over copy-pasted node clusters.
- **Custom HLSL**: the Custom HLSL block/operator (17.0+) runs an HLSL function inline or from an `.hlsl` include for logic that is awkward in nodes.

Read [references/graph-authoring.md](references/graph-authoring.md) when building a specific effect, wiring Point Cache or SDF data, using Shader Graph outputs, strips, or Timeline.

## Driving from C#

```csharp
using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public sealed class ImpactVfx : MonoBehaviour
{
    static readonly int SpawnRateId = Shader.PropertyToID("SpawnRate");
    static readonly int TintId = Shader.PropertyToID("Tint");
    static readonly int OnImpactId = Shader.PropertyToID("OnImpact");
    static readonly int PositionAttr = Shader.PropertyToID("position");
    static readonly int VelocityAttr = Shader.PropertyToID("velocity");

    VisualEffect _vfx;
    VFXEventAttribute _payload;

    void Awake()
    {
        _vfx = GetComponent<VisualEffect>();
        _payload = _vfx.CreateVFXEventAttribute();
    }

    public void SetIntensity(float rate, Color tint)
    {
        if (_vfx.HasFloat(SpawnRateId)) _vfx.SetFloat(SpawnRateId, rate);
        if (_vfx.HasVector4(TintId)) _vfx.SetVector4(TintId, tint);
    }

    public void Impact(Vector3 point, Vector3 normal)
    {
        _payload.SetVector3(PositionAttr, point);
        _payload.SetVector3(VelocityAttr, normal * 4f);
        _vfx.SendEvent(OnImpactId, _payload);
    }
}
```

Rules:

- **Cache IDs** with `Shader.PropertyToID` (or serialize an `ExposedProperty` field). The string overloads hash every call.
- **Check before set.** `Set*` on a missing or mistyped name does nothing and logs nothing. Use `HasFloat` / `HasVector3` / `HasTexture` and friends while integrating, and enumerate the asset's real names with `VisualEffectAsset.GetExposedProperties` and `GetEvents` (see the reference).
- **Match types exactly.** A `Color` property is set with `SetVector4`; a `Vector3` property with `SetVector3`; an `int` with `SetInt`, `uint` with `SetUInt`. Wrong-type setters fail silently.
- **Overrides persist** on the component until `ResetOverride(id)`; they also show as overrides in the Inspector.
- **Event payloads**: `CreateVFXEventAttribute()` once and reuse it; `SendEvent` copies the payload. In the graph, read the payload in Initialize with *Get Attribute (Source)* / *Inherit Source* blocks; a payload attribute nothing reads is ignored. `spawnCount` in the payload overrides the burst count for Single Burst spawners that use it.
- **Play/Stop semantics**: `Play()` sends `OnPlay`, `Stop()` sends `OnStop`, which stops spawning only; live particles finish their lifetime. `Reinit()` clears all particles and restarts. Events sent while the component is disabled are lost.
- `aliveParticleCount` and `HasAnyAliveParticles()` come from an asynchronous GPU readback and lag a few frames. Use them for pooling or debug, not frame-exact logic.
- `pause`, `playRate`, `Simulate(stepDeltaTime, stepCount)`, `resetSeedOnPlay`, and `startSeed` control time and determinism. `Simulate` is how you prewarm or scrub from code.

For property binders, custom `VFXBinderBase`, `GraphicsBuffer` data, output-event handling, and runtime SDF baking, read [references/csharp-api.md](references/csharp-api.md).

## URP compatibility (Unity 6)

- Supported on the Universal Renderer in Forward, Forward+, and Deferred. Lit outputs require the Universal Renderer; on the 2D Renderer use Unlit outputs and verify behavior on your exact minor version.
- **Depth Buffer collision and camera-dependent operators** need *Depth Texture* enabled on the URP Asset (and *Opaque Texture* if you sample scene color). Without it, Collide with Depth Buffer does nothing.
- **Shader Graph outputs**: the Shader Graph must target URP with *Support VFX Graph* enabled in Graph Settings, then it appears in the Output's *Shader Graph* field. Exposed graph properties become output inputs.
- **Motion vectors** (TAA, motion blur, STP upscaling in 6000.x): enable *Generate Motion Vectors* on outputs that move fast, or they smear under TAA/STP.
- **Sorting with other transparents**: VFX outputs sort as renderers. Use the output's *Sorting Priority* and the renderer's sorting settings; per-particle sorting does not interleave with other meshes.
- **Volumes and post-processing** apply normally; HDR color plus Bloom is the usual glow recipe (see `urp-postprocessing`).

## Capacity, bounds, and culling pitfalls

| Symptom | Cause | Fix |
|---|---|---|
| Effect pops out when its origin leaves the screen | Bounds too small or at origin only | Record or enlarge bounds; use `Automatic` only for unpredictable motion |
| Particles freeze off-screen, then jump | Asset culling flag simulates only when visible | Asset Inspector > *Culling Flags* > "Always recompute bounds and simulate" for gameplay-critical effects |
| Fewer particles than the spawn rate implies | Capacity reached | Raise capacity to `rate * maxLifetime` (+ burst headroom), or shorten lifetime |
| Burst spawns nothing | Event never reached the Spawn context, or Single Burst count is 0 | Wire the event into Spawn *Start*; send after `OnEnable` |
| Effect follows the object it should leave behind | System in Local space | Switch the system (or just the trail system) to World |
| Nothing renders in a build | Compute unsupported, or the asset was stripped | Check `SystemInfo.supportsComputeShaders`; reference the asset from a scene/prefab/Addressable |
| Effect plays with a startup pop | No prewarm | Asset Inspector > *PreWarm Total Time*, or `Simulate` before showing |

## Performance

- Budget on GPU time, not particle count. Fill rate (overdraw of large transparent quads) dominates; shrink quads, use alpha clipping or flipbooks with less transparent area, and keep soft particles off on mobile.
- Keep capacity close to real need. Capacity costs memory and some per-frame work even when particles are dead.
- Disable *Sort* on additive outputs. Avoid `Automatic` bounds on many instances.
- Use one `.vfx` asset for many identical effects: VFX Graph batches instances of the same asset when the asset's *Instancing* setting allows it (some features such as output events or GPU events can disable it; the asset Inspector reports this).
- Pool `VisualEffect` GameObjects. Reuse with `Reinit()` + `Play()` instead of `Instantiate`/`Destroy`, which re-allocates GPU buffers.
- Scale cost with distance: lower `SpawnRate` through an exposed property, or disable the component beyond a distance. VFX has no built-in LOD.
- `VFXManager.fixedTimeStep` and `maxDeltaTime` (Project Settings > VFX) bound simulation cost on frame spikes.

## Inspecting assets from code

Enumerate effects with `AssetDatabase.FindAssets("t:VisualEffectAsset")` and read exposed property names and events from the `VisualEffectAsset` (code in [references/csharp-api.md](references/csharp-api.md)). That is the stable way to audit what C# can drive without opening the graph window.

## Related skills

- `unity-particle-system` for CPU particles, WebGL, and low-end targets.
- `shader-graph-create-custom-node` for custom Shader Graph nodes used in VFX outputs.
- `urp-postprocessing` for Bloom/tonemapping that sells HDR effects.
- `unity-texture-import` for flipbook and noise texture import settings.
