---
name: ui-effect
description: Applies and configures mob-sakai UIEffect v5 (com.coffee.ui-effect, namespace Coffee.UIEffects) on uGUI Image, RawImage, Text and TextMeshProUGUI - grayscale, sepia, blur, pixelation, dissolve, shiny, fade, shadow/outline, gradation, edge and detail-texture effects. Covers install via OpenUPM or git URL with ?path=Packages/src, the UIEffect component and its filter groups, UIEffectPreset and runtime presets, UIEffectProjectSettings and shader variant registration (missing variants in player builds), UIEffectTweener, UIEffectReplica, TextMeshPro and ShaderGraph support samples, Timeline tracks, batching cost, and migrating v4 components (UIDissolve, UIShiny, UIHsvModifier, UITransitionEffect, UIShadow, UIGradient, UIFlip). Use when the user mentions UIEffect, UI dissolve/shiny/grayscale/blur effects, UIEffectTweener, UIEffectReplica, LoadPreset, or an effect that works in the Editor but is pink, missing or default-looking in a build.
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/mob-sakai/UIEffect"
  unity: "6000.0+"
---

# UIEffect v5 (mob-sakai)

Package id `com.coffee.ui-effect`, namespace `Coffee.UIEffects`. Grounded in v5.11.x. All effects are one component (`UIEffect`) driving one shader per base shader, with keyword-selected variants. For uGUI hierarchy work see `ui-ugui`; for TMP font/atlas/material tuning see `optimize-text-mesh-pro`.

## Workflow

1. Install (pick one; pin a tag in production):
   - OpenUPM: `openupm add com.coffee.ui-effect` (or `openupm add com.coffee.ui-effect@<version>`).
   - Git URL in `Packages/manifest.json`. The `?path=Packages/src` suffix is required for v5:
     ```json
     "com.coffee.ui-effect": "https://github.com/mob-sakai/UIEffect.git?path=Packages/src#5.11.7"
     ```
   - Embedded: copy `Packages/src` from a release zip into the project's `Packages/` folder.
   The package needs Unity 2020.3+ and depends only on `com.unity.ugui`; TextMeshPro and Timeline integration switch on automatically through asmdef version defines (`TMP_ENABLE` for `com.unity.textmeshpro` or `com.unity.ugui` 2.0+, `TIMELINE_ENABLE` for `com.unity.timeline`).
2. Add `UIEffect` to a GameObject that has a `Graphic` (`Add Component`, search "UIEffect"; the README mentions a `Component > UI > UIEffect` menu, but v5.11 source declares no `AddComponentMenu`, so search by name). One per GameObject (`[DisallowMultipleComponent]`).
3. Enable filters in the Inspector (for example Tone Filter = `Grayscale`, Tone Intensity = 1). Use the header menu to `Load` (overwrite all) or `Append` (only enabled filters/modes) an editor preset, or `Save As New`.
4. Exercise every effect combination the game will use at least once in the Editor (Edit or Play mode). That is what records the shader variant into the project's ShaderVariantCollection. See "Shader variants" below; skipping this is the main cause of build-only failures.
5. To animate, add `UIEffectTweener` to the same GameObject (it `[RequireComponent(typeof(UIEffectBase))]`), or use AnimationClips/Timeline on the `UIEffect` properties.
6. To share one look across many graphics, put `UIEffectReplica` on them pointing at a `UIEffect` or a `UIEffectPreset` asset.
7. For TextMeshPro, import the TMP support sample (step below) before expecting effects on text.

Property names, enum values, tweener and replica members: read `references/properties.md` when writing code against the API or picking filter values.
v4 to v5 component mapping and conversion: read `references/migration-v4.md` when a project has `UIDissolve`, `UIShiny`, `UIShadow`, `UIGradient`, `UIHsvModifier`, `UITransitionEffect`, `UIFlip`, or a v4 `UIEffect`.

## Code usage

```csharp
using Coffee.UIEffects;
using UnityEngine;
using UnityEngine.UI;

public sealed class DissolveOnHide : MonoBehaviour
{
    [SerializeField] private Graphic _graphic;

    private UIEffectTweener _tweener;

    private void Awake()
    {
        var effect = _graphic.gameObject.AddComponent<UIEffect>();
        effect.LoadPreset("Dissolve");
        effect.transitionWidth = 0.1f;
        effect.transitionColor = Color.red;

        _tweener = _graphic.gameObject.AddComponent<UIEffectTweener>();
        _tweener.cullingMask = UIEffectTweener.CullingMask.Transition;
        _tweener.wrapMode = UIEffectTweener.WrapMode.Once;
        _tweener.updateMode = UIEffectTweener.UpdateMode.Unscaled;
        _tweener.playOnEnable = UIEffectTweener.PlayOnEnable.None;
        _tweener.onComplete.AddListener(OnHidden);
    }

    public void Hide() => _tweener.PlayForward(true);

    private void OnHidden() => _graphic.gameObject.SetActive(false);
}
```

- The upstream README sample writes `UICullingMask.Tone` and `UIWrapMode.PingPongLoop`; those types do not exist in source. The enums are nested: `UIEffectTweener.CullingMask`, `UIEffectTweener.WrapMode`, `UIEffectTweener.UpdateMode` (`Normal`, `Unscaled`, `Manual`), `UIEffectTweener.Direction`, `UIEffectTweener.PlayOnEnable`.
- `LoadPreset(string)` only finds presets registered as runtime presets in Project Settings (or via `UIEffectProjectSettings.RegisterRuntimePreset`). An unregistered name silently does nothing. `LoadPreset(UIEffectPreset)` works with a direct asset reference and needs no registration.
- `LoadPreset(name, append: true)` merges only the preset's enabled filters/modes. `Clear()` resets to defaults.
- Setting properties from code marks the material/vertices dirty itself; do not call `SetMaterialDirty()` yourself after normal property writes.

## Shader variants and builds

UIEffect shaders use `#pragma shader_feature_local_fragment` for every filter group, so the player contains only variants listed in the ShaderVariantCollection stored inside `UIEffectProjectSettings` (`Edit > Project Settings > UI > UIEffect`, asset usually at `Assets/ProjectSettings/UIEffectProjectSettings.asset`).

- In the Editor, whenever a UIEffect material is applied, its variant is added to that collection automatically (unless `Error On Unregistered Variant` is enabled, in which case it is logged as an error and listed under Unregistered Variants instead, for you to add with "+").
- A combination that is only ever set from runtime code paths never seen in the Editor is not registered and renders wrong in a player build. Fix: reproduce it once in the Editor, or add it under Unregistered Variants with "+". Turn on `Error On Unregistered Variant` in CI-like QA passes to surface gaps.
- "-" on Registered Variants removes unused ones to cut build time and size; do not remove variants still used at runtime.
- Commit `UIEffectProjectSettings.asset`. It is auto-added to `PlayerSettings.preloadedAssets`; leave that alone unless you deliberately disable `Pre Load Settings In Build` and load the settings asset yourself (Resources/AssetBundles/Addressables) before any UIEffect renders.
- Warm up to avoid first-use hitches: `UIEffectProjectSettings.shaderVariantCollection.WarmUp();` or spread it over frames with `ShaderVariantCollection.WarmUpProgressively(int)` in a loop until it returns true.
- Shader lookup for a base material: entries in Optional Shaders (UIEffect) first, then a shader whose name contains `(UIEffect)`, then `Hidden/<base shader name> (UIEffect)`, then `Hidden/UI/Default (UIEffect)`. A custom base shader without a `(UIEffect)` counterpart therefore falls back to the default UI look.

## TextMeshPro, ShaderGraph, SoftMask, Timeline

- TMP: import TMP Essential Resources first, then in Package Manager import the `TextMeshPro Support (Unity 6)` sample (Unity 2023.2/6000.0+ or TMP 3.2/4.0+); the plain `TextMeshPro Support` sample is for older setups. An import dialog also appears automatically when a sample shader is first requested. Samples land under `Assets/Samples/UI Effect/{version}`; re-import after upgrading the package.
- If `TMPro.cginc` / `TMPro_Properties.cginc` were moved from `Assets/TextMesh Pro/Shaders/`, the sample shaders' include paths must be fixed.
- `SamplingFilter.BlurMedium` and `BlurDetail` fall back to `BlurFast` on TextMeshProUGUI.
- `<font>` and `<sprite>` tags (TMP_SubMeshUI) are supported.
- ShaderGraph (Unity 6 only): import `ShaderGraph Support (Unity 6 BuiltIn)` or `ShaderGraph Support (Unity 6 URP)`, then set the graph's material sub target to `Canvas (UIEffect)`. Keep `(UIEffect)` in the shader name or register it under Optional Shaders.
- SoftMaskForUGUI 3.3.0+ works with UIEffect 5.7.0+ through an auto-prompted sample import.
- Timeline: use a `Control Track` to enable the GameObject, or the UIEffect tracks (`ToneIntensityTrack`, `ColorTrack`, `ColorIntensityTrack`, `SamplingIntensityTrack`, `TransitionRateTrack`, `TransitionColorTrack`, `EdgeColorTrack`, `EdgeShinyRateTrack`, `GradationIntensityTrack`, `GradationOffsetTrack`, `GradationRotationTrack`, `GradationScaleTrack`, `DetailColorTrack`, `DetailIntensityTrack`).

## Custom shaders

To make a custom UI shader UIEffect-aware, copy the `==== UIEFFECT START/END ====` blocks from `Packages/com.coffee.ui-effect/Shaders/UIEffect.shader`: `Blend [_SrcBlend] [_DstBlend]`, the `shader_feature_local_fragment` keyword lines, a `float4 uvMask` TEXCOORD in vertex input and v2f plus `worldPosition`, `#define UIEFFECT_FRAG_STRUCT v2f`, a `half4 uieffect_frag(v2f IN, float2 uv)` function, `#include "Packages/com.coffee.ui-effect/Shaders/UIEffect.cginc"`, and `half4 c = uieffect(IN.texcoord, IN.uvMask, IN.worldPosition, IN);` in the fragment. Name it `... (UIEffect)` or register it as an optional shader.

## Performance and batching

- Each `UIEffect` instance gets its own material (the material cache key includes the instance's id, sampling scale and transition root), so every effected graphic breaks uGUI batching with its neighbours. Ten identical effected icons cost about ten batches.
- `UIEffectReplica` pointing at the same target or the same `UIEffectPreset` reuses that id. The cache key also includes the transition root, which defaults to each replica's own RectTransform, so enable `useTargetTransform` (or give all replicas the same `customRoot`) when you want them to share one material and batch. Prefer one source `UIEffect` (or preset asset) plus replicas for lists, grids and repeated icons.
- Blur (`BlurMedium`/`BlurDetail`), `Shadow3`/`Outline8` and high `shadowIteration` are the costly options: sampling filters multiply texture taps, and shadow modes copy the mesh once per direction per iteration (`Outline8` with 3 iterations is 24 extra copies). Use `BlurFast` and `Shadow`/`Outline` on mobile and for large areas.
- `transitionAutoPlaySpeed` and `edgeShinyAutoPlaySpeed` animate in the shader using `_Time` (scaled time, no CPU cost, no rebuilds). `UIEffectTweener` and Animator/Timeline drive properties from the CPU and dirty the material each frame.
- Mesh-modifying options (shadows, `allowToModifyMeshShape`) rebuild the canvas mesh when changed; avoid tweening them per frame on large canvases.

## Pitfalls

- Pink, plain or wrong effect only in a player build: variant not registered (see Shader variants), settings asset not preloaded, or a stripped optional shader.
- Raycasts: with a transition filter other than `None`, `Shiny`, `Mask` or `Pattern`, the graphic stops receiving raycasts once `transitionRate` reaches 1. A fully dissolved/faded button is not clickable; that is by design.
- `transitionTexture`/`detailTexture` scale, offset or speed need the texture's Wrap Mode set to Repeat. Transition textures use the alpha channel.
- `Rotation` is shared by the transition and detail textures; `Keep Aspect Ratio` is shared by transition, gradation and detail. Changing one changes the other.
- Effects that reference the RectTransform (transition, gradation, detail) misplace when the mesh extends beyond it (shadows, TMP overflow). Set `customRoot`.
- Sampling filters clamp to each quad's UV rect, so packed Sprite Atlas neighbours do not bleed into blur/edge effects; the effect is still bounded by the sprite rect, so leave transparent padding in the sprite if a glow/edge must extend outward.
- `color` setter forces alpha to 1; set `colorAlpha` separately.
- `UIEffectTweener.UpdateMode.Manual` does not stop `Update()` from advancing time while playing; for fully manual control also set `playOnEnable = None`, do not call `Play*`, and drive it with `SetTime(sec)` / `UpdateTime(delta)`.
- `UIEffectTweener` does not run in Edit Mode outside its own inspector preview; `Restart()` is obsolete, use `ResetTime()`.
- `shadowGlow` is obsolete; use `shadowColorGlow`. `GradationMode.RadialFast` / `RadialDetail` are obsolete aliases of `Radial`.
- The legacy prefab-based preset system is deprecated since 5.8.0; convert with `Convert All Legacy Presets` in Project Settings. `UIEffectProjectSettings.LoadRuntimePreset` is obsolete; use `UIEffectProjectSettings.LoadPreset` or `UIEffect.LoadPreset`.
- Supported graphics are `Image`, `RawImage`, `Text` and `TextMeshProUGUI` (plus `TMP_SubMeshUI`). Not for UI Toolkit, SpriteRenderer or world-space `TextMeshPro` (non-UGUI).
- Render pipelines: the package advertises Built-in, URP, HDRP and VR support; there is no pipeline-specific setup for uGUI canvases. ShaderGraph integration ships separate BuiltIn and URP samples only.
