---
name: unity-lighting
description: Sets up, scripts, bakes, and debugs lighting in Unity 6 URP scenes. Covers Light components (types, modes, shadows, cookies, rendering layers), URP Forward/Forward+/Deferred light limits, the URP Asset shadow settings, ambient and skybox (RenderSettings, DynamicGI.UpdateEnvironment), fog, reflection probes (baked/realtime, box projection, RenderProbe), light probes and Adaptive Probe Volumes, lightmapping (LightingSettings assets, Lightmapping.BakeAsync, mixed lighting modes, lightmap UVs), and common artifacts. Use when the user mentions lights, shadows, shadow acne, light leaking, baked GI, lightmaps, APV, probe volumes, reflection probes, skybox, ambient light, fog, time of day, "scene too dark", or lights not affecting objects.
license: MIT
metadata:
  category: rendering-vfx
  sources: "AlexeyPerov/Unity-Open-MCP/skills/extensions/lighting"
  unity: "6000.0+"
---

# Unity Lighting (URP, Unity 6)

Lighting is split across four places. Know which one owns a setting before changing it:

| Owner | Holds |
|---|---|
| `Light` component (+ `UniversalAdditionalLightData`) | Per-light type, color, intensity, range, mode, shadows, cookie, rendering layers |
| URP Asset + Universal Renderer asset | Rendering path, light limits, shadow distance/cascades/resolution, probe blending, APV on/off |
| Scene Lighting (`RenderSettings` + Lighting window) | Skybox, ambient source, environment reflections, fog, sun source |
| `LightingSettings` asset (`.lighting`, per scene) | Baked GI, lightmapper, lightmap resolution, mixed lighting mode |

## Workflow

1. **Decide the GI strategy** before placing lights:
   - Fully realtime (dynamic time of day, procedural levels): realtime lights + skybox ambient + APV or light probes baked for static geometry only if layout is fixed.
   - Baked/mixed (static levels, mobile): Mixed or Baked lights, lightmaps on static geometry, APV or Light Probe Groups for dynamic objects.
2. **Pick the rendering path** on the Universal Renderer asset (see "Rendering path and light limits").
3. **Set the environment**: skybox material, ambient source, environment reflections, fog (see "Environment").
4. **Place lights**: one Directional sun (set it as *Sun Source*), then local lights. Set each light's mode.
5. **Configure shadows** on the URP Asset, then per light.
6. **Mark static geometry**: *Contribute GI* static flag, lightmap UVs (*Generate Lightmap UVs* on the model importer), *Receive GI* = Lightmaps or Light Probes.
7. **Bake** (Lighting window > Generate Lighting, or `Lightmapping.BakeAsync`) and verify in the Scene view *Baked Lightmap* / *Contributors/Receivers* debug draw modes.
8. **Add reflection probes** where glossy surfaces need local reflections; bake them after lighting.

## Light component

| Property | Notes |
|---|---|
| `type` | Directional, Point, Spot, Rectangle/Disc (area). **Area lights are baked-only in URP.** |
| Mode (`lightmapBakeType`, Editor) | Realtime, Mixed, Baked. Baked lights do not light dynamic objects except through probes. |
| `intensity`, `color`, `useColorTemperature` + `colorTemperature` | URP uses physical (inverse-square) falloff; BiRP-era intensities often look too dim (see `migrate-birp-to-urp`). |
| `range`, `spotAngle`, `innerSpotAngle` | Range clips influence; it does not change falloff curve. |
| `shadows` | None / Hard / Soft. Soft shadow quality is per light in 6000.x (Low/Medium/High) on the URP light data. |
| `shadowStrength`, bias | Bias per light or "Use settings from Render Pipeline Asset". |
| `cookie` | Supported in URP for Directional, Spot, Point. |
| `cullingMask` | Which layers the light affects. Prefer **Rendering Layers** for lighting selection in URP: `UniversalAdditionalLightData.renderingLayers` vs `Renderer.renderingLayerMask` (enable *Use Rendering Layers* on the renderer). |

```csharp
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class LightFactory
{
    public static Light CreateLamp(Vector3 position, Color color, float intensity, float range)
    {
        var go = new GameObject("Lamp");
        go.transform.position = position;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;
        var data = go.GetUniversalAdditionalLightData();
        data.renderingLayers = 1u;
        return light;
    }
}
```

`GetUniversalAdditionalLightData()` is an extension in `UnityEngine.Rendering.Universal`; it adds the component if missing. The older `lightLayerMask` property is obsolete; use `renderingLayers`.

## Rendering path and light limits

| Path | Additional lights | Notes |
|---|---|---|
| Forward | Per-object limit (URP Asset > Lighting > Additional Lights > Per Object Limit, max 8) | Cheapest; objects touched by more lights drop the least important ones (visible popping) |
| Forward+ | No per-object limit; per-camera visible-light cap (higher on desktop than mobile) | Default choice for many lights; also lifts the per-object reflection-probe limit |
| Deferred | Many lights, cost per lit pixel | No MSAA; transparent objects still render forward |
| Deferred+ (6000.1+) | Clustered deferred | Combines Forward+ light lists with the G-buffer |

Symptom "a light does not affect this object": check the per-object limit (Forward), the light's culling mask / rendering layers, the light mode (Baked lights do not hit dynamic objects), and the URP Asset *Additional Lights* setting (Disabled / Per Vertex / Per Pixel).

## Shadows

URP Asset > Shadows:

- **Max Distance**: shadows end here. Lowering it is the single biggest quality-per-texel win.
- **Cascade Count** 1-4 and splits: more cascades = sharper near shadows, more draw calls.
- **Depth Bias / Normal Bias**: raise to fix shadow acne (stripes); lower to fix peter-panning (shadows detached). Tune per light only when one light misbehaves.
- **Main light / additional light shadow resolution** and the additional-light shadow atlas. Point light shadows cost 6 shadow maps each.
- **Soft Shadows** toggle plus per-light quality.
- Shadowmask / Distance Shadowmask (Quality settings) control how mixed lights blend baked and realtime shadows.

## Environment

```csharp
using UnityEngine;
using UnityEngine.Rendering;

public static class EnvironmentSwitcher
{
    public static void Apply(Material skybox, float ambientIntensity)
    {
        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        DynamicGI.UpdateEnvironment();
    }
}
```

- Changing `RenderSettings.skybox` at runtime does **not** update ambient or reflections until `DynamicGI.UpdateEnvironment()` runs. It is not free; throttle it in time-of-day systems (every few seconds, not every frame).
- Ambient modes: `Skybox` (spherical harmonics from the sky), `Trilight` (`ambientSkyColor`/`ambientEquatorColor`/`ambientGroundColor`), `Flat` (`ambientLight`).
- Custom environment reflection: `RenderSettings.defaultReflectionMode = Custom` + `RenderSettings.customReflectionTexture`.
- Fog: `RenderSettings.fog`, `fogMode` (Linear/Exponential/ExponentialSquared), `fogColor`, `fogDensity`, `fogStartDistance`/`fogEndDistance`. Only shaders that include URP fog apply it; custom shaders must call `MixFog`.
- Edits to `RenderSettings` in the Editor are scene data: mark the scene dirty and save it, or they are lost.

## Reflection probes

| Type | Use |
|---|---|
| Baked | Static scenery; baked with lighting or `Lightmapping.BakeReflectionProbe` |
| Realtime | Moving objects or changing lighting; `refreshMode` OnAwake / EveryFrame / ViaScripting, `timeSlicingMode` to spread cost |
| Custom | You supply a cubemap |

- Enable **Probe Blending** and **Box Projection** on the URP Asset (Lighting > Reflection Probes) for interiors; set each probe's *Box Projection* and fit its box to the room.
- Realtime probes with `ViaScripting`: `int id = probe.RenderProbe();` then poll `probe.IsFinishedRendering(id)`. Every realtime refresh renders the scene six times; keep resolution low (64-128) and refresh rarely.
- Importance and blend distance decide overlaps. Objects use probes based on their bounds center; large objects can pick the wrong probe.

## Light probes and Adaptive Probe Volumes

Dynamic objects get baked indirect light from probes. Unity 6 URP offers two systems; use one per project:

- **Adaptive Probe Volumes (APV)**: URP Asset > Lighting > *Light Probe System* = Adaptive Probe Volumes. Add an *Adaptive Probe Volume* (global mode covers the scene), bake from the Lighting window's APV tab. Per-pixel probe lighting, automatic placement, streaming, lighting scenarios (day/night blending, enable on the URP Asset), and sky occlusion (6000.0+). Fix leaks with *Virtual Offset*, *Probe Adjustment Volumes*, and rendering-layer masks; increase density only where needed.
- **Light Probe Groups** (legacy): hand-placed probes, per-object interpolation. Still valid for small scenes or when APV is too heavy for the target.

Large dynamic objects lit by Light Probe Groups get one interpolated sample and look flat. Light Probe Proxy Volumes are not supported in URP; use APV (per-pixel sampling) or split the object.

## Baking

Read [references/baking.md](references/baking.md) when creating `LightingSettings` assets, baking from scripts or CI, choosing mixed lighting modes, sizing lightmaps, or debugging lightmap artifacts (seams, leaks, splotches).

## Common problems

| Symptom | Check |
|---|---|
| Scene too dark after switching skybox | `DynamicGI.UpdateEnvironment()`; ambient intensity; exposure/tonemapping in the Volume (`urp-postprocessing`) |
| Shadow acne / stripes | Raise depth or normal bias; check URP Asset shadow resolution and max distance |
| Light leaks through walls | Wall thickness vs probe spacing (APV), lightmap UV overlap, backface ratio in bake |
| Dynamic object unlit next to baked lights | No probes baked, or light mode Baked; add APV / probes or use Mixed |
| Lightmap seams or black spots | Overlapping/insufficient lightmap UVs; raise *Pack Margin*; check "UV Overlap" debug mode |
| Point light popping on objects | Forward per-object limit; switch to Forward+ or reduce overlapping lights |
| Reflections look wrong indoors | Missing box projection or probe blending on the URP Asset |
| Lighting change lost after Editor restart | Scene not saved (RenderSettings and lighting-settings reference are scene data) |

## Related skills

- `urp-postprocessing` for exposure, tonemapping, and bloom that shape perceived brightness.
- `migrate-birp-to-urp` for lighting parity after a pipeline switch.
- `unity-texture-import` for cookie, HDRI, and lightmap texture settings.
