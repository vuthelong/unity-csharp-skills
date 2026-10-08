---
name: migrate-birp-to-urp
description: Plans, executes, and troubleshoots moving a Unity project from the Built-in Render Pipeline (BiRP) to URP on Unity 6 in safe, verified phases - URP asset and renderer setup, Graphics and per-Quality-level assignment, Render Pipeline Converter runs, material conversion with texture/color preservation, particle and foliage materials, PPv2 to URP Volume profiles, baked lighting, lightmaps and reflection probes, custom shader triage (Surface Shaders, GrabPass, OnRenderImage, replacement shaders), URP 2D, and saved-state validation. Use when the user asks to upgrade/convert/switch a project, scene, material, or shader to URP, has pink/magenta materials after switching, lost post-processing or lighting parity, or wants an audit of migration risk before changing anything.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: rendering-vfx
  sources: "Unity-Technologies/skills/skills/migrate-birp-to-urp"
  unity: "6000.0+"
---

# Migrate Built-in Render Pipeline to URP

Classify the request, inspect the project, migrate in phases, and claim only what saved project state proves. Unity has announced deprecation of the Built-in Render Pipeline in the 6000.x line, so new work should target URP.

## Reference loading

| Read | When |
|---|---|
| [references/migration-workflow.md](references/migration-workflow.md) | Always, on activation |
| [references/implementation-patterns.md](references/implementation-patterns.md) | **Mandatory** before the first `eval` that edits settings, materials, post-processing, lighting, probes, or scenes, and when resuming a partial migration |
| [references/custom-shader-triage.md](references/custom-shader-triage.md) | Materials, shaders, magenta, custom rendering, image effects |
| [references/complex-shader-situations.md](references/complex-shader-situations.md) | Surface Shaders, `GrabPass`, `OnRenderImage`, replacement shaders, multi-pass/deferred, package shaders, foliage |
| [references/quality-settings-map.md](references/quality-settings-map.md) | Shadows, quality levels, lighting/lightmaps/probes, visual mismatch, performance |
| [references/capturing-the-editor.md](references/capturing-the-editor.md) | Capturing Scene/Game view images for validation |

If `implementation-patterns.md` cannot be loaded, use the rules here and lower confidence: never claim PPv2 conversion, lighting/probe refresh, or completion from intent or tool logs.

## Phases

A generic "migrate this project to URP" starts or resumes this flow. The skill discovers migration surfaces; the user does not have to list them.

| Phase | Goal | Gate |
|---|---|---|
| 0 Inspect and plan | Pipeline state, representative scenes, rollback safety, PPv2, material/shader risks, Quality levels, lighting/lightmaps/probes, custom render code | Findings reported; rollback point confirmed before any mutation |
| 1 URP setup and materials | Install/reuse URP, create/assign URP asset + renderer in Graphics and every relevant Quality level, convert supported opaque, particle, and foliage materials | Saved Graphics/Quality assignments and material shaders verified; textures/colors preserved |
| 2 Post-processing and cameras | Persistent URP `VolumeProfile` with non-null overrides, scene `Volume` wired, camera post-processing on, legacy PPv2 disabled for validation, unsupported effects classified | Saved profile and scene references verified |
| 3 Lighting and probes | Resolve stale Built-in bakes, URP lighting/probe settings, rebake and refresh probes when feasible | Saved scene lighting references verified, or phase explicitly partial |
| 4 Final validation | Save, reload, re-query, capture representative scenes, check Console | Every success gate passes before saying "complete" |

Rules for phases:

- End each phase with `Phase complete`, `Phase partial`, or `Blocked`, listing Completed / Incomplete / Manual follow-up / Next phase.
- Package install, compilation, domain reload, or a long bake is a phase boundary. Resume from saved partial state; do not restart or reinstall.
- **Stop before any pipeline-mutating step** (installing URP, assigning a URP asset, running converters, editing materials or scenes) until the user confirms a rollback point: branch, backup, archive, or disposable copy. Do not leave the project magenta just to reach that question.
- Once rollback safety is confirmed, it covers the whole pass. Do not ask "continue?" for routine work inside a phase. Ask only for new costly or destructive decisions (long bakes, deleting legacy assets, custom shader rewrites).
- If the user says "do not modify files", stay in audit/planning mode.
- If the project is also changing Unity version, upgrade the engine first, then the pipeline.

## Running C# in the Editor

Every step runs as C# in a live Editor through the Unity CLI. The `unity-cli` skill owns setup (CLI install, connected Editor, `com.unity.pipeline`, Safe Mode, command catalog). Additionally:

- You need the `eval` command. If the catalog lacks it, say so and stop.
- **`com.unity.pipeline` requires Unity 6000.3+.** It uses `IPreprocessBuildWithContext` / `BuildCallbackContext`, introduced in 6000.3, but declares `"unity": "6000.0"`, so on 6000.0-6000.2 it installs and then fails with CS0246 in the Editor log while `unity status` shows nothing. Projects leaving BiRP are often on older Editors; diagnose this instead of retrying the CLI.
- A migration is not safely authorable blind. An unreachable Editor is a stop, not a cue to hand-edit `ProjectSettings/GraphicsSettings.asset`.
- `unity command eval --code '<snippet>'` has a 30 s default timeout. Installing URP outlasts it; treat that as a phase boundary.

`eval` compiles a **statement block**: no `using` directives (CS0210), fully qualify types (bare `Object` is CS0104), no extension methods (use `GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()`, `System.Linq.Enumerable.FirstOrDefault(seq, pred)`).

Snippets that declare a `class`, `static` method, or `[MenuItem]` are project files: save under `Assets/Editor/`, let Unity compile, then call the entry point with a one-line `eval`. Prefer this for multi-step work; scripts survive domain reloads, long `eval` payloads do not.

### Detecting the pipeline

```csharp
var rp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
var names = UnityEngine.QualitySettings.names;
var rows = new System.Collections.Generic.List<string>();
for (int i = 0; i < names.Length; i++)
{
    var a = UnityEngine.QualitySettings.GetRenderPipelineAssetAt(i);
    rows.Add($"{i}:{names[i]}={(a == null ? "inherits Graphics" : a.GetType().Name + ":" + a.name)}");
}
return $"graphics={(rp == null ? "NULL (Built-in)" : rp.GetType().Name + ":" + rp.name)}; quality: {string.Join(", ", rows)}";
```

`UniversalRenderPipelineAsset` = URP; `HDRenderPipelineAsset` = HDRP (out of scope: explain and give comparison advice only); null everywhere = Built-in. A project can be switched in Graphics while a Quality level points elsewhere. Do not call `SetQualityLevel` just to inspect; it mutates project state.

## Phase 0: assess

1. Run the detection snippet. If already on URP, switch to troubleshooting.
2. Deliberately open the representative scene(s). Never infer absence of PPv2, bakes, or probes from a default or test scene.
3. Inventory with `AssetDatabase.FindAssets`: materials (including particle/VFX and vegetation), shaders, scenes, key prefabs, existing URP/renderer assets, PPv2 profiles, Volume profiles, Lighting Settings and Lighting Data assets, lightmaps, light/reflection probes, mixed/baked lights.
4. Search code and shaders for risk markers: `PostProcessLayer`, `PostProcessVolume`, `UnityEngine.Rendering.PostProcessing`, `OnRenderImage(`, `RenderWithShader`, `SetReplacementShader`, `#pragma surface`, `GrabPass`, `CGPROGRAM`, `CommandBuffer`, package shader includes, vegetation markers (`Nature/`, `SpeedTree`, `TreeCreator`, `_Cutoff`, wind keywords). If PPv2 is installed but text search finds nothing, inspect scenes and profiles through Unity APIs by type.
5. Decide 3D, 2D, or mixed, and classify the request:

| User says | Path |
|---|---|
| "Upgrade this project to URP" | A: full 3D migration |
| "Move this 2D project to URP" | B: Built-in 2D -> URP 2D Renderer |
| "Convert these materials" | C: targeted material conversion (project already on URP) |
| "My materials turned pink" / "lighting looks wrong" | D: troubleshooting |
| "Don't change anything yet" | Audit/plan only |

6. Report findings, e.g. "Still Built-in, no URP asset, PPv2 in 3 scenes, 12 surface shaders, baked lighting in Level01."

## Phase 1: URP setup and materials

1. **Install URP** if `Packages/manifest.json` lacks `com.unity.render-pipelines.universal` (see `unity-package-management`). Do not spin-wait on `PackageManager.Client.Add` inside `eval`; request it, then verify after the reload.
2. **Create or reuse** the URP asset and Universal Renderer (or 2D Renderer for path B). Do not create duplicates.
3. **Assign** the URP asset in Graphics and in every relevant Quality level. No per-index setter exists (do not invent `QualitySettings.SetRenderPipelineAssetAt`): cache `QualitySettings.GetQualityLevel()`, for each level `SetQualityLevel(i)` and set `QualitySettings.renderPipeline = urpAsset`, restore the original level, save, then verify `customRenderPipeline` in `ProjectSettings/QualitySettings.asset` is not `{fileID: 0}`.
4. If the Console reports "Default Renderer is missing", re-query the asset's renderer list, repair, and re-validate; do not stop at "setup complete".
5. **Snapshot material data before any conversion** (pattern in implementation-patterns.md). After a shader change, `_MainTex` may already be lost.
6. **Run the Render Pipeline Converter** (*Window > Rendering > Render Pipeline Converter*): "Built-in Render Pipeline to URP" with Rendering Settings, Material Upgrade, Animation Clip Converter, Read-only Material Converter, Post-processing Stack v2 Converter; or "Built-in Render Pipeline 2D to URP 2D" with Material and Material Reference Upgrade. Initialize, review warnings, then convert.
7. **Read back every converted material's `shader.name`.** On a 3D project the converter can silently pick `Universal Render Pipeline/2D/Mesh2D-Lit-Default` for `Standard` materials because 2D and 3D upgraders claim it at equal priority (observed on 6000.5.8f1). Restore those from the rollback point and convert with the explicit 3D pattern.
8. **Verify against the snapshot**: `_BaseMap`/`_BaseColor`, tiling/offset, normal, metallic/specular, emission, cutoff restored. White/grey untextured results are not success.
9. **Particles and effects**: fog, smoke, steam, additive, transparent, decal, and VFX materials go to `Universal Render Pipeline/Particles/Unlit` (or Simple Lit/Lit) with blend mode preserved, never blindly to URP Lit. Inspect off-screen and inactive systems by asset.
10. **Foliage**: preserve alpha clipping, two-sided rendering, tint, normals; check that grass and leaves are not solid quads and report lost wind/billboard behavior.
11. Skip immutable assets under `Packages/` and `Library/PackageCache/`; use local copies if needed.

Targeted path C: select materials, *Edit > Rendering > Materials > Convert Selected Built-in Materials to URP*, then validate one or two scenes.

## Phase 2: post-processing and cameras

1. Inventory `PostProcessVolume`, `PostProcessLayer`, and `PostProcessProfile` usage before converting. Prefer the PPv2 converter over hand-built profiles.
2. Verify the saved URP `VolumeProfile` has non-null components. `components: []` or `{fileID: 0}` entries mean failure. Script-created overrides need `AssetDatabase.AddObjectToAsset(component, profile)`, dirtying, saving, and reloading.
3. Map common effects when present: Bloom -> `Bloom`, Color Grading -> `ColorAdjustments` + `Tonemapping` (+ `ColorCurves`/`LiftGammaGain`), Vignette -> `Vignette`, Depth of Field -> `DepthOfField`. Ambient Occlusion -> SSAO renderer feature. Screen Space Reflections and Auto Exposure have no URP Volume equivalent: report as manual.
4. Wire a URP `Volume` in the scene to the new profile (add a URP `Volume`; do not assign a URP profile to the PPv2 component). Enable `renderPostProcessing` on target cameras and include the Volume layer in their Volume Mask (see `urp-postprocessing`).
5. Disable, do not delete, legacy PPv2 components during validation to avoid double post-processing. Remove them only after parity is accepted.
6. `OnRenderImage`, custom blits, and replacement shaders move to a `ScriptableRendererFeature` with Render Graph (see complex-shader-situations.md and `validate-urp-render-graph-renderer-feature`).

## Phase 3: lighting and probes

1. Inventory `Lightmapping.lightingSettings`, `LightmapSettings`, the Lighting Data asset, mixed/baked lights, light probes, and reflection probes in the representative scene.
2. Isolate exposure first: disable legacy PPv2, check URP Volume exposure/tonemapping/bloom before touching lights.
3. If clearing baked data fixes blow-out, the old bake is stale active data: keep it as reference, then rebake under URP with final assets, renderer, Volumes, and Quality settings.
4. Assigning a new `.lighting` asset is scene state: mark the scene dirty, save, reload, and confirm the saved `m_LightingSettings` reference. `AssetDatabase.SaveAssets()` alone does not prove it.
5. Enable Probe Blending and Box Projection on the URP asset for probe-heavy scenes; then refresh probes. Unchanged EXR files mean probes were not refreshed.
6. URP light falloff differs from Built-in; treat brightness differences as tuning, not conversion failure (see `unity-lighting`).

## Phase 4: success gate

Use "complete" only when every item passes on saved state:

1. Graphics and every relevant Quality level reference the intended URP asset; the asset has a valid default renderer; no render-pipeline errors in the Console.
2. Supported materials (including particle/fog/smoke) use URP shaders with source textures and colors preserved, or are listed as unresolved custom/package cases.
3. If PPv2 existed: saved profile has the mapped overrides (including `DepthOfField` if the source had it), the scene URP `Volume.sharedProfile` references it, legacy PPv2 is disabled, SSR/AO are handled or listed as manual.
4. If baked lighting existed: the scene no longer references the old Lighting Settings GUID or stale Lighting Data, a URP bake ran (or lighting is reported partial), probe settings are enabled and probes refreshed (or partial).
5. A representative scene was captured after save/reload, and exposure is balanced or reported partial.
6. Console checked (read the Editor log or the CLI's log command via `unity-cli`); errors repaired or listed.

Iterate at most 3 repair rounds per phase, then report remaining blockers.

## Troubleshooting

| Symptom | First checks |
|---|---|
| Still behaves like Built-in | Graphics and Quality-level assignment; active pipeline really URP |
| Stopped after installing URP | Phase boundary: re-check manifest, partial assets, then continue from the first incomplete item; do not reinstall |
| Magenta materials | Console/Inspector shader errors; supported Built-in shader (re-run conversion) vs custom/package shader (triage reference) |
| Darker/brighter/wrong | Double post-processing (PPv2 + URP Volume); exposure/tonemapping; stale lightmaps; ambient; Quality-level URP asset differences; light falloff |
| Post-processing gone | Empty saved profile; camera `renderPostProcessing`; Volume Mask; leftover active PPv2; `OnRenderImage` effects not ported |
| Refraction/distortion broken | `GrabPass` -> Opaque Texture / Scene Color (complex-shader-situations.md) |
| 2D lights ignore sprites | 2D Renderer assigned; sprite materials upgraded to `Sprite-Lit-Default` |
| Slower after migration | Render scale, shadows, additional lights, MSAA, post-processing, opaque/depth textures, SRP Batcher-incompatible ported shaders |
| Custom shader compiles but looks wrong | Parameter drift vs structural incompatibility (Surface Shader, custom lighting, multi-pass); validate one material first |

## Guardrails

- Never mutate before rollback safety is confirmed.
- Tool logs and created assets are not proof; save, reload, and re-query.
- Never bulk search-and-replace shader code; port one representative shader first.
- Never claim the converter handles custom shaders.
- Never present a phase result as project-level completion.

## Report

State: path used; pipeline state (Built-in / partial / URP); converters run; what was fixed; what remains manual; scenes/materials/cameras validated; post-processing migrated vs partial vs documented; Quality levels explicit vs Graphics fallback; particle/VFX and foliage materials status; baked lighting preserved / cleared / rebaked / manual; exposure balanced or partial. If any item is partial or manual, call the overall migration **partial** and name the phases that passed.

## Related skills

- `unity-cli` for Editor access; `unity-package-management` for installing URP.
- `urp-postprocessing` for Volume setup details.
- `unity-lighting` for rebaking, APV, and probe settings.
- `validate-urp-render-graph-renderer-feature` for ported fullscreen effects.
- `shader-graph-create-custom-node` for rebuilding simple custom shaders in Shader Graph.
- `unity-particle-system` for particle material and renderer settings.
