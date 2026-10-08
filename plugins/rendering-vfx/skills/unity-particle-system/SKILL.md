---
name: unity-particle-system
description: Creates, tunes, scripts, and optimizes Unity's built-in CPU Particle System (Shuriken) on Unity 6 with URP particle shaders. Covers the module struct API (main, emission, shape, color/size over lifetime, noise, collision, trails, sub-emitters, renderer), MinMaxCurve/MinMaxGradient modes, bursts, Emit with EmitParams, Play/Stop/Clear and stopAction, simulation and scaling space, collision and trigger callbacks, GetParticles/SetParticles and particle jobs, pooling, procedural culling, and overdraw. Use when the user mentions ParticleSystem, Shuriken, ParticleSystemRenderer, emission bursts, sub emitters, OnParticleCollision, OnParticleTrigger, particle trails, or fire/smoke/explosion effects that must run on WebGL or low-end mobile. For GPU particles at scale use `unity-vfx-graph`.
license: MIT
metadata:
  category: rendering-vfx
  sources: "AlexeyPerov/Unity-Open-MCP/skills/extensions/particlesystem"
  unity: "6000.0+"
---

# Unity Particle System (Shuriken)

CPU-simulated particles on the `ParticleSystem` component, drawn by `ParticleSystemRenderer`. Works on every platform and pipeline, interacts with physics, and supports per-particle C# access. Prefer it over VFX Graph for WebGL, low-end mobile, physics collisions/triggers with gameplay callbacks, and effects under a few thousand particles. Prefer `unity-vfx-graph` for tens of thousands of particles or GPU-driven data.

## Workflow

1. **Inspect before editing.** Read the current module values (Inspector or code) and runtime state (`isPlaying`, `particleCount`, `time`) before changing anything.
2. **Pick the material first.** In URP use `Universal Render Pipeline/Particles/Unlit`, `/Simple Lit`, or `/Lit`. Built-in `Particles/Standard Unlit` or legacy `Particles/*` shaders render magenta in URP (see `migrate-birp-to-urp`).
3. **Set the main module**: duration, looping, start lifetime/speed/size/color, simulation space, scaling mode, max particles, play on awake.
4. **Shape the emission**: Emission rate or bursts, Shape module.
5. **Animate over life**: Color/Size/Rotation over Lifetime, Velocity over Lifetime, Noise, Limit Velocity.
6. **Add interaction only if needed**: Collision, Triggers, Sub Emitters, Lights, Trails.
7. **Verify in Play mode** with the Particle Effect overlay (Scene view) and the Profiler (`ParticleSystem.Update`, `ParticleSystem.Draw` markers). Verify overdraw with the Scene view *Overdraw* draw mode or the Rendering Debugger.

## Module API rule

Modules are struct proxies onto the native system. Copy the module into a local, then assign its properties; the write goes straight through. You never assign the module back.

```csharp
var ps = GetComponent<ParticleSystem>();

var main = ps.main;
main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
main.startSpeed = 3f;
main.maxParticles = 500;
main.simulationSpace = ParticleSystemSimulationSpace.World;

var emission = ps.emission;
emission.rateOverTime = 0f;
emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });

var shape = ps.shape;
shape.shapeType = ParticleSystemShapeType.Cone;
shape.angle = 25f;
shape.radius = 0.1f;
```

- Do not use the legacy per-property shortcuts (`ps.startSize`, `ps.startColor`, `ps.emissionRate`, `ps.enableEmission`); they are obsolete since 5.5 and fail to compile or warn in Unity 6. Use the modules.
- `main.duration` can only change while the system is stopped (`Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear)` first), otherwise Unity logs a warning and ignores it.
- Cache the `ParticleSystem`; module getters are cheap but `GetComponent` per frame is not.

## MinMaxCurve and MinMaxGradient

| Mode | Constructor | Notes |
|---|---|---|
| Constant | `new MinMaxCurve(2f)` or implicit `= 2f` | |
| Random Between Two Constants | `new MinMaxCurve(1f, 3f)` | |
| Curve | `new MinMaxCurve(multiplier, curve)` | Curve values are scaled by `multiplier` |
| Random Between Two Curves | `new MinMaxCurve(multiplier, minCurve, maxCurve)` | |

Over-lifetime curves use normalized time (0..1 of each particle's lifetime). Size over Lifetime multiplies start size, it does not replace it.

```csharp
var gradient = new Gradient();
gradient.SetKeys(
    new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.05f), 0.5f), new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 1f) },
    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });

var col = ps.colorOverLifetime;
col.enabled = true;
col.color = new ParticleSystem.MinMaxGradient(gradient);

var sol = ps.sizeOverLifetime;
sol.enabled = true;
sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1.5f));
```

## Playback control

| Call | Effect |
|---|---|
| `Play(withChildren: true)` | Starts this system and child systems |
| `Stop(true, ParticleSystemStopBehavior.StopEmitting)` | Stops spawning; live particles finish |
| `Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear)` | Stops and removes all particles |
| `Clear(true)` | Removes particles, keeps playing state |
| `Pause()` / `Simulate(t, withChildren, restart, fixedTimeStep)` | Freeze or fast-forward (scrubbing, prewarm) |
| `Emit(count)` / `Emit(emitParams, count)` | Spawn immediately, outside emission rate |
| `IsAlive(withChildren: true)` | Any particles or emission remaining |

`main.stopAction` (`Disable`, `Destroy`, `Callback`) fires when a non-looping system and its particles finish. Use `Disable` for pooled effects and `Callback` to receive `OnParticleSystemStopped()` on the same GameObject. Prefer `Disable` + pool over `Destroy`.

Spawning at arbitrary points without one system per hit:

```csharp
var emitParams = new ParticleSystem.EmitParams
{
    position = hitPoint,
    velocity = hitNormal * 2f,
    applyShapeToPosition = true
};
_impactSystem.Emit(emitParams, 12);
```

This keeps a single shared, world-space system for all impacts of a type, which is far cheaper than instantiating a prefab per hit.

## Space and scale pitfalls

- **Simulation Space**: `Local` particles move with the transform (good for auras); `World` particles stay behind (trails, smoke from a moving object). `Custom` uses another transform.
- **Scaling Mode**: default `Local` ignores parent scale, so scaling a prefab root does nothing. Use `Hierarchy` to scale the whole effect with its parents; `Shape` scales only the emitter shape.
- **Max Particles** silently caps emission. Set it to roughly `rate * maxLifetime` plus burst sizes.
- **Prewarm** only works on looping systems.
- **Unscaled time**: `main.useUnscaledTime = true` keeps UI/pause-menu effects running when `Time.timeScale = 0`.
- **Renderer Min/Max Particle Size** clamp particle size as a fraction of the screen. Large close-up particles that look capped are hitting `maxParticleSize` (default 0.5).

## Rendering in URP

- Soft Particles and Camera Fading in URP particle shaders need *Depth Texture* on the URP Asset; distortion needs *Opaque Texture*.
- Render Mode Mesh: enable *Enable Mesh GPU Instancing* on the renderer (URP particle shaders support it) to batch mesh particles.
- Sorting: transparent particle systems sort by renderer distance plus *Sorting Fudge*; within a system by *Sort Mode*. Use Sorting Layer/Order or fudge to fix systems drawing through each other.
- Custom Vertex Streams (renderer) feed extra per-particle data (random, velocity, custom data) into Shader Graph shaders; the material must read the same streams in the same order.
- Lights module spawns real-time lights per particle; cap *Maximum Lights* tightly, and remember URP's per-object light limit.

## Collision, triggers, and sub-emitters

Read [references/scripting.md](references/scripting.md) when handling `OnParticleCollision`, `OnParticleTrigger`, sub-emitters, per-particle reads/writes, or particle jobs.

## Performance

- **Overdraw** dominates: fewer, smaller, more opaque particles beat many large transparent ones. Use flipbooks (Texture Sheet Animation) instead of stacking layers.
- **Procedural mode**: a system with only predictable modules is simulated procedurally and can be culled off-screen (`main.cullingMode = Automatic`). World-space collision, Noise, Limit Velocity, External Forces, Trails, and some curve modes make it non-procedural; the Inspector shows a warning icon. Non-procedural systems keep simulating off-screen unless `cullingMode` is `Pause` or `PauseAndCatchup`.
- **Collision**: World collision with High quality raycasts per particle. Use Planes, or World with Medium/Low quality and a restricted *Collides With* layer mask.
- **Noise**: quality High (3D) is the most expensive module; Low (1D) is often enough.
- **Pooling**: pool effect GameObjects with `stopAction = Disable`; avoid `Instantiate`/`Destroy` per shot. Or use `Emit(EmitParams)` on shared systems.
- **Many systems**: Unity updates systems on worker threads; keep `Play On Awake` off for effects that start disabled so they do not tick.

## Recipes

| Effect | Main | Emission / Shape | Over life |
|---|---|---|---|
| Fire | lifetime 1-1.5, speed 2-3, size 0.4-0.6, World | rate 20-40, Cone angle 25 radius 0.1 | Color yellow->orange->dark smoke fading to 0 alpha; Size grows; Noise strength 0.5 |
| Smoke | lifetime 3-5, speed 0.5, size 1, World | rate 10, Cone angle 10 | Alpha fade in/out, Size 1->2.5, Noise strength 1 frequency 0.5, Rotation over Lifetime |
| Explosion burst | lifetime 0.3-0.8, speed 5-12, not looping | rate 0, Burst 40 at t=0, Sphere | Size to 0, Color white->orange->transparent; sub-emitter Death for embers |
| Sparks | speed 6-10, size 0.03-0.06, gravity 1 | Burst 20, Cone or Hemisphere | Renderer Stretched Billboard (speed scale 0.05), Collision Planes with bounce 0.3 |
| Magic aura | Local space, lifetime 1, speed 0 | rate 30, Circle edge | Orbital velocity via Velocity over Lifetime, Additive Unlit |

## Related skills

- `unity-vfx-graph` for GPU-scale effects.
- `unity-texture-import` for flipbook sheets and particle textures.
- `urp-postprocessing` for Bloom on HDR particle colors.
- `migrate-birp-to-urp` for converting legacy particle materials.
