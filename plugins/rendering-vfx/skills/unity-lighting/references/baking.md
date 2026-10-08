# Baking lightmaps, probes, and reflections

## Contents

- [Mixed lighting modes](#mixed-lighting-modes)
- [LightingSettings assets](#lightingsettings-assets)
- [Baking from scripts](#baking-from-scripts)
- [Reflection probe baking](#reflection-probe-baking)
- [Lightmap sizing](#lightmap-sizing)
- [Artifacts](#artifacts)

## Mixed lighting modes

Set on the `LightingSettings` asset (`mixedBakeMode`); affects Mixed lights only.

| Mode | Static receives | Dynamic receives | Cost |
|---|---|---|---|
| Baked Indirect | Realtime direct + baked indirect | Realtime direct + probe indirect | Realtime shadows everywhere up to shadow distance |
| Shadowmask | Baked shadows from static casters (up to 4 overlapping mixed lights per texel) | Realtime | Cheap far shadows; *Distance Shadowmask* (Quality settings) switches to realtime near the camera |
| Subtractive | Baked direct + indirect + shadows; only the main light casts realtime shadows of dynamic objects | Realtime main light | Cheapest; least accurate, mobile |

## LightingSettings assets

The active `LightingSettings` is referenced by the **scene**. Creating the asset is not enough; assign it and save the scene.

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LightingSetup
{
    [MenuItem("Tools/Lighting/Create Baked Lighting Settings")]
    static void CreateBakedSettings()
    {
        var settings = new LightingSettings
        {
            name = "BakedLighting",
            bakedGI = true,
            realtimeGI = false,
            lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
            mixedBakeMode = MixedLightingMode.Shadowmask,
            lightmapResolution = 20f,
            lightmapPadding = 4,
            lightmapMaxSize = 2048,
            directionalityMode = LightmapsMode.CombinedDirectional,
            ao = true
        };
        AssetDatabase.CreateAsset(settings, "Assets/Settings/BakedLighting.lighting");
        Lightmapping.lightingSettings = settings;

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
```

- `Lightmapping.lightingSettings` throws if the active scene has none; use `Lightmapping.TryGetLightingSettings(out var s)` to read safely.
- Progressive GPU falls back to CPU when the GPU lacks memory; check the Console after a bake.
- Enlighten *baked* GI no longer exists; Enlighten *Realtime* GI is legacy. Prefer baked lightmaps + APV.

## Baking from scripts

```csharp
using UnityEditor;
using UnityEngine;

public static class LightingBake
{
    [MenuItem("Tools/Lighting/Bake Active Scene")]
    static void Bake()
    {
        if (Lightmapping.isRunning) return;
        Lightmapping.bakeCompleted -= OnBakeCompleted;
        Lightmapping.bakeCompleted += OnBakeCompleted;
        if (!Lightmapping.BakeAsync())
            Debug.LogError("Bake failed to start. Check that the scene is saved and has a LightingSettings asset.");
    }

    static void OnBakeCompleted()
    {
        Lightmapping.bakeCompleted -= OnBakeCompleted;
        Debug.Log($"Bake finished: {LightmapSettings.lightmaps.Length} lightmaps");
    }
}
```

- `Lightmapping.Bake()` blocks the Editor; use it only in batch mode / CI (`-batchmode -executeMethod`), where blocking is what you want.
- Bake multiple scenes together with `Lightmapping.BakeMultipleScenes(paths)` so they share lightmap atlases and probe data.
- `Lightmapping.Clear()` removes baked data from the scene; `Lightmapping.ClearLightingDataAsset()` detaches the Lighting Data asset. Save the scene afterwards, or the clear is not persisted.
- The bake writes a `LightingData.asset`, lightmap EXRs, and reflection probe EXRs next to the scene in a folder named after it. Commit them or bake in CI; do not leave stale copies from a different pipeline.
- APV bakes run as part of Generate Lighting when the scene belongs to a Baking Set.

## Reflection probe baking

- Baked probes bake with Generate Lighting. To bake one probe: `Lightmapping.BakeReflectionProbe(probe, "Assets/Lighting/Probe.exr")` (Editor only).
- Realtime probes at runtime: `probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting; int id = probe.RenderProbe();` then check `probe.IsFinishedRendering(id)`.
- Bake probes after lights, skybox, and post-processing exposure are final; probes capture lit scene color.

## Lightmap sizing

- Texel density is `lightmapResolution` texels per unit, scaled per renderer by *Scale In Lightmap*. Start around 10-20 for environments, lower for distant or large flat surfaces, 0 (excluded, probe-lit) for small props.
- Small props and foliage: *Receive GI = Light Probes* instead of lightmaps saves atlas space and bake time.
- Check atlas count and memory in the Lighting window > Baked Lightmaps tab. Mobile targets: aim for a few 1024-2048 atlases with compression on.
- Directional lightmaps double memory; use Non-Directional on mobile if normal-mapped detail under baked light is not needed.

## Artifacts

| Artifact | Cause | Fix |
|---|---|---|
| Seams across UV islands | Insufficient padding, bilinear bleed | Raise *Pack Margin* / `lightmapPadding`; enable *Generate Lightmap UVs* with larger margin |
| Black splotches | Overlapping lightmap UVs or backfaces seen by texels | Scene view *UV Overlap* draw mode; fix UVs; raise backface tolerance on the Lightmap Parameters asset |
| Light leaking at wall/floor junction | Single-sided or thin geometry, low resolution | Thicker walls, close gaps, higher resolution at junctions |
| Noisy lightmaps | Too few samples | Raise indirect/direct samples, enable denoising (OptiX/OIDN/Radeon Pro) and filtering |
| Dynamic objects too dark/bright vs static | Probes missing or poorly placed | APV with adequate density, or more Light Probe Group probes near lighting changes |
| Bake ignores an object | Not *Contribute GI* static, or renderer disabled | Set static flag; Scene view *Contributors/Receivers* draw mode |
