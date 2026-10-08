---
name: urp-postprocessing
description: Sets up, configures, scripts, and debugs URP post-processing with the Volume framework on Unity 6 (URP 17). Covers global/local Volumes, VolumeProfile overrides (Bloom, Tonemapping, ColorAdjustments, DepthOfField, Vignette, MotionBlur, FilmGrain, ChromaticAberration, LensDistortion, WhiteBalance, LiftGammaGain, ColorCurves, Screen Space Lens Flare), overrideState, profile vs sharedProfile, the Default Volume Profile, camera renderPostProcessing and volume layer masks, Volume Update Mode, HDR and grading mode, and anti-aliasing. Use when the user asks about bloom, glow, tonemapping, color grading, exposure, depth of field, vignette, motion blur, "post-processing not showing", PostProcessVolume/PPv2 replacements, or animating post effects from C#. For custom fullscreen passes use `validate-urp-render-graph-renderer-feature`.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: rendering-vfx
  sources: "Unity-Technologies/skills/skills/urp-postprocessing"
  unity: "6000.0+"
---

# URP Post-Processing (Volume framework)

Goal: a visible result in the Game view with zero Console errors.

## Workflow

1. Run the pre-flight checks below and fix failures first.
2. Create or reuse a Volume and a `VolumeProfile` asset.
3. Add overrides with `profile.Add<T>()`, setting `overrideState = true` on every parameter you change.
4. Enable post-processing on the rendering camera and include the Volume's layer in the camera's Volume Mask.
5. Verify in the Game view, then save assets and the scene.

## Running C# in the Editor

Editor-side checks and edits run as C# in a live Editor. The `unity-cli` skill owns getting there (CLI install, connected Editor, `com.unity.pipeline`, Safe Mode, command catalog). You need the `eval` command specifically; if the catalog lacks it, say so and stop. Inline form: `unity command eval --code '<snippet>'` (30 s default timeout; check the catalog before assuming `eval_file` exists).

`eval` compiles a **statement block, not a file**:

- No `using` directives (`using UnityEngine;` becomes CS0210).
- Fully qualify every type (`UnityEngine.Rendering.Volume`); a bare `Object` is ambiguous (CS0104).
- Extension methods are unavailable; use `GetComponent<UniversalAdditionalCameraData>()` instead of `GetUniversalAdditionalCameraData()`.
- `return` a string to get results back; `throw` for hard stops.

Code saved as a `.cs` file in the project keeps normal `using` directives.

## Pre-flight checks

1. **URP is active**: `UniversalRenderPipeline.asset != null`. Otherwise stop.
2. **HDR** on the URP Asset: required for Tonemapping; Bloom works in LDR only with `threshold < 1`.
3. **Camera post-processing**: `UniversalAdditionalCameraData.renderPostProcessing` defaults to `false`. In a camera stack, enable it on the Base camera (or the last Overlay). The renderer's *Post-processing* data must not be null.
4. **Volume Mask**: the Volume GameObject's layer must be in the camera's `volumeLayerMask` (defaults to Default only).
5. **Volume**: enabled, non-null profile, at least one override with `overrideState = true`.
6. **Default Volume Profile** (Unity 6): *Project Settings > Graphics > Pipeline Specific Settings > URP > Default Volume Profile* (and an optional profile on the URP Asset) applies to every camera beneath scene Volumes. Overrides there can mask or fight scene settings; check it when a value "won't change".

```csharp
var report = new System.Text.StringBuilder();
var urpAsset = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset;
if (urpAsset == null) throw new System.Exception("URP is not the active render pipeline.");
if (!urpAsset.supportsHDR) report.AppendLine("HDR is off on the URP Asset: Tonemapping inactive, Bloom needs threshold < 1.");

var cam = UnityEngine.Camera.main;
if (cam == null) throw new System.Exception("No Main Camera found.");
if (!cam.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(out var camData))
    throw new System.Exception("Missing UniversalAdditionalCameraData on the camera.");
if (!camData.renderPostProcessing) report.AppendLine("Camera renderPostProcessing is false.");

foreach (var vol in UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(UnityEngine.FindObjectsSortMode.None))
{
    if (!vol.enabled) { report.AppendLine($"Volume '{vol.name}' is disabled."); continue; }
    if ((camData.volumeLayerMask & (1 << vol.gameObject.layer)) == 0)
        report.AppendLine($"Volume '{vol.name}' layer {vol.gameObject.layer} is not in the camera volumeLayerMask.");
    var profile = vol.sharedProfile;
    if (profile == null) { report.AppendLine($"Volume '{vol.name}' has no profile."); continue; }
    if (profile.components.Count == 0) report.AppendLine($"Volume '{vol.name}' profile has no overrides.");
}
return report.Length == 0 ? "Post-processing setup looks correct." : report.ToString();
```

## Volumes

- **Global Volume**: `isGlobal = true`; affects every camera whose mask includes its layer.
- **Local Volume**: `isGlobal = false` plus a trigger `Collider`. `priority` (higher wins when overlapping), `blendDistance` (world units of fade outside the collider), `weight` (0-1 influence). The camera's *Volume Trigger* transform (default: the camera) decides "inside".
- **Volume Update Mode** (camera `volumeFrameworkUpdateMode`, default from the URP Asset): `EveryFrame` or `ViaScripting`. With `ViaScripting` the camera only re-blends when you call `camera.UpdateVolumeStack()` (extension in `UnityEngine.Rendering.Universal`), which saves CPU on mobile but makes runtime changes appear frozen if forgotten.

## Overrides

Every effect is a `VolumeComponent` on a `VolumeProfile`: `profile.Add<T>(overrides: false)`, `profile.Has<T>()`, `profile.TryGet<T>(out var t)`, `profile.Remove<T>()`. Every property is a `VolumeParameter`; **set `overrideState = true` before `value`** or the Volume system ignores it. `Add<T>(true)` enables `overrideState` on all parameters of the new component.

Property names, types, ranges, and defaults for each effect: [references/effect-reference.md](references/effect-reference.md). Editor templates for creating volumes, enabling camera post-processing, editing saved profiles, and undo through `eval`: [references/code-templates.md](references/code-templates.md).

## Runtime scripting

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class DamageVignette : MonoBehaviour
{
    [SerializeField] Volume _volume;
    Vignette _vignette;

    void Awake()
    {
        if (!_volume.profile.TryGet(out _vignette))
            _vignette = _volume.profile.Add<Vignette>();
        _vignette.intensity.overrideState = true;
    }

    public void SetDamage(float normalized) => _vignette.intensity.value = Mathf.Lerp(0f, 0.5f, normalized);

    void OnDestroy()
    {
        if (_volume != null && _volume.HasInstantiatedProfile()) Destroy(_volume.profile);
    }
}
```

- `volume.profile` clones the shared asset on first access (runtime-safe; destroy the clone when done). `volume.sharedProfile` edits the asset itself (persists to disk in the Editor). For Editor-time edits use `sharedProfile` plus `SetDirty` and `SaveAssets`.
- For fades, prefer a dedicated Volume with the effect fully set and animate `volume.weight` 0->1. It avoids touching profile data at all.
- To **read** the final blended value the camera uses, read `VolumeManager.instance.stack.GetComponent<T>()` (or the camera's own stack). Do not write to the stack; writes are overwritten next blend.

## Wrong -> correct API

| Wrong (PPv2 / guessed) | Correct (URP) |
|---|---|
| `PostProcessVolume` | `UnityEngine.Rendering.Volume` |
| `PostProcessLayer` | `UniversalAdditionalCameraData.renderPostProcessing` + `volumeLayerMask` |
| `UnityEngine.Rendering.PostProcessing` | `UnityEngine.Rendering` + `UnityEngine.Rendering.Universal` |
| `profile.GetSetting<T>()` | `profile.TryGet<T>(out var t)` |
| `profile.AddSettings<T>()` | `profile.Add<T>()` (throws if present; check `Has<T>()`) |
| `ColorGrading` | `ColorAdjustments` + `Tonemapping` + `ColorCurves` etc. |
| `AutoExposure` | No URP equivalent; use `ColorAdjustments.postExposure` or a custom pass |
| `ScreenSpaceReflections` (PPv2) | No Volume override; reflection probes or a custom renderer feature |
| `AmbientOcclusion` (PPv2) | *Screen Space Ambient Occlusion* renderer feature on the Universal Renderer |
| Writing `VolumeManager.instance.stack` components | Write a profile or `volume.weight`; read the stack only |

## Pipeline settings that change the look

- URP Asset > Post-processing > **Grading Mode**: HDR (grade before tonemapping, recommended with HDR) vs LDR; **LUT size**.
- **HDR Output** (Player settings, HDR displays): Tonemapping gains HDR-specific settings (paper white, min/max nits). Validate on an HDR display.
- **Anti-aliasing** is per camera: FXAA, SMAA, or TAA. TAA/STP need motion vectors and can blur fast VFX; see `unity-vfx-graph`.
- **Custom post effects**: use a *Full Screen Pass Renderer Feature* with a Fullscreen Shader Graph, or a Render Graph `ScriptableRendererFeature` (review it with `validate-urp-render-graph-renderer-feature`).

## Debugging checklist

1. `UniversalAdditionalCameraData` exists and `renderPostProcessing` is true on the rendering (Base) camera.
2. Volume has a non-null profile; overrides added and `overrideState = true` on the changed parameters.
3. Volume layer in the camera's `volumeLayerMask`.
4. Global volume `isGlobal = true`, or the camera's volume trigger is inside the local volume's collider.
5. Camera Volume Update Mode is not `ViaScripting` without `camera.UpdateVolumeStack()` calls.
6. Default Volume Profile and higher-priority volumes are not overriding the same parameter.
7. HDR enabled for Bloom/Tonemapping.
8. Looking at the Game view; the Scene view has its own post-processing toggle.
9. Frame Debugger shows the `UberPost` / `Bloom` passes; if missing, the post-process pass was not scheduled (camera flag or null renderer post-process data).

## Recipes

Format: Effect property=value. Bloom is threshold/intensity/scatter.

- **Cinematic**: Tonemapping ACES; ColorAdjustments contrast=15 saturation=-10; Bloom 0.9/0.5/0.7; Vignette intensity=0.3 smoothness=0.4; FilmGrain Medium1 intensity=0.2.
- **Stylized / vibrant**: Tonemapping Neutral; ColorAdjustments saturation=20 contrast=10; Bloom 0.8/1.5/0.6; SplitToning warm highlights, cool shadows.
- **Horror**: ColorAdjustments postExposure=-0.5 saturation=-30 contrast=20; Vignette 0.5/0.3 dark red; FilmGrain Large01 0.4; ChromaticAberration 0.15.
- **Mobile**: Tonemapping Neutral; ColorAdjustments postExposure=0.2; Bloom 1.0/0.3 with downscale Quarter. Avoid FilmGrain, MotionBlur, DepthOfField Bokeh, and high-quality bloom filtering.

## Report back

```
Post-Processing Setup Complete
- Volume: [Global/Local] on "[GameObject]"
- Profile: [asset path]
- Effects: [effect: key=value, ...]
- Camera: [name] renderPostProcessing=true, volumeLayerMask includes layer [N]
- Saved: profile asset [yes/no], scene [saved/unsaved]
View in the Game view.
```

## Related skills

- `unity-cli` to reach a live Editor.
- `migrate-birp-to-urp` for converting PPv2 projects.
- `unity-lighting` for exposure problems that are really lighting problems.
- `validate-urp-render-graph-renderer-feature` for custom fullscreen effects.
