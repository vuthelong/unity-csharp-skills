---
name: optimize-text-mesh-pro
description: Optimizes TextMeshPro memory, quality, CPU cost, and build size in Unity 6 — static main font plus dynamic fallback chains, atlas size and multi-atlas, Clear Dynamic Data On Build, padding-to-sampling ratios, SDF16 render mode, font asset Scale, Dynamic OS atlas population with system fonts, AutoSize cost, per-Canvas rebuild isolation, world-space `TextMeshPro` vs `TextMeshProUGUI`, material presets, sprite assets, and Memory Profiler font captures. Use for TMP font assets, atlas memory bloat, fuzzy or inconsistent glyphs, missing glyphs (tofu) from fallbacks, CJK or mixed-script alignment, text-driven CPU spikes or Canvas rebuilds, or shipped font size. Not for UI Toolkit text (`ui-uitk`) or per-locale font swapping (`localization`).
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: ui
  sources: "Unity-Technologies/skills/skills/optimize-text-mesh-pro"
  unity: "6000.0+"
---

# Optimize TextMeshPro

In Unity 6, TextMeshPro ships inside `com.unity.ugui` (2.x); the old `com.unity.textmeshpro` package is merged and should be removed from manifests. Namespaces (`TMPro`) and asset formats are unchanged. UI Toolkit uses its own TextCore `FontAsset` type — optimizations here apply to TMP components only.

## Triage

Identify the symptom before advising. If none is given, ask: memory/atlas bloat, visual quality, CPU, build size, or localization/alignment?

| Symptom | Section |
|---|---|
| Large or many TMP atlases in Memory Profiler | Font stack, Memory Profiler |
| Inconsistent weight, fuzzy edges | Padding and sampling, Font asset Scale, SDF16 |
| CPU spikes on text change or Canvas rebuild | AutoSize, Text updates and Canvas isolation |
| Large build from font files | Dynamic OS |
| Mixed Latin + CJK misaligned | Font normalization |
| Italic/outline/glow variants of one font | Material presets |
| Tofu (□) for some characters | Font stack, then `localization` for per-locale fonts |

## Core rules

- **Main font = static atlas with the required glyphs baked; everything else via dynamic fallbacks.** Keep dynamic atlases at 512–1024 to bound peak memory; enable Multi Atlas Textures for large scripts instead of one huge atlas.
- **Enable `Clear Dynamic Data On Build` on every dynamic font asset.** Otherwise glyphs rasterized while testing in the Editor ship in the player.
- **Keep the padding-to-sampling-point-size ratio identical across a fallback chain.**
- **Sampling point size: Latin 70–90, CJK 36–50.**
- **Font asset Scale = 1.**
- **No AutoSize on text that changes at runtime.**
- **World-space text uses `TextMeshPro` (3D), not `TextMeshProUGUI` in a world-space Canvas,** unless it must take pointer input.
- **Isolate frequently changing text under its own nested `Canvas`.**
- **Material presets, not duplicated font assets,** for style variants.
- **Evaluate Atlas Population Mode `Dynamic OS`** for multilingual mobile builds.

## Font stack and dynamic fallbacks

```
Main font asset (Static, Latin set baked)
  -> Fallback 1: Dynamic, 1024, multi-atlas — CJK
  -> Fallback 2: Dynamic, 512 — symbols / emoji
```

Set fallbacks per font asset (Fallback Font Assets list) or globally in TMP Settings (`Default Fallback Font Assets`). Fallbacks are searched in order on every missing glyph; keep chains short.

Fallback chain vs per-locale swapping: use a **fallback chain** when one screen can mix scripts (player names, chat, user content). Use **per-locale font swapping** through Localization Asset Tables (see `localization`) when each locale shows a single script and you want its own metrics and weight. Many games combine both: a per-locale primary plus a small universal fallback.

## Padding and sampling

Ratio = `Padding / Sampling Point Size` (Padding 9 at size 90 = 10%). Different ratios between primary and fallback produce different stroke widths on the same line; outlines and glow also scale with padding. Pick one ratio (10% is a safe default) and apply it to every asset in the chain. CJK glyphs are dense, so smaller sampling sizes still give clean SDF and save atlas space.

## Font asset Scale

Some imported font assets carry `Scale = 0.9`. Scale feeds the point-size math, so sizes stop matching design specs. Set Scale = 1 on all font assets before tuning padding.

## Atlas render mode: SDF16

For static fonts rendered at large sizes (point size 72+) that look soft, switch Atlas Render Mode to SDF16 for higher-precision distance fields, at slightly more atlas memory.

## AutoSize

`enableAutoSizing` runs an iterative fit every time the text changes — expensive for timers, counters, chat, and names. Lock the size once layout is final. Keep AutoSize only on static labels that must fit varying locale strings.

## Text updates and Canvas isolation

- Use `TMP_Text.SetText("{0:0}", value)` (format overloads with numeric args) instead of `text = value.ToString()` to avoid per-update string allocations.
- Any `TextMeshProUGUI` change dirties its Canvas. Put volatile fields under a child GameObject with its own `Canvas` (and no extra `GraphicRaycaster` unless it needs input) so the rest of the UI is not rebuilt.
- Disable `Rich Text` and `Parse Escape Characters` on labels that never use them; disable `raycastTarget` on non-interactive text.
- Many short-lived world labels (damage numbers): pool `TextMeshPro` objects instead of instantiating.

## Font normalization (mixed scripts)

1. **Window > TextMeshPro > Import TMP Examples and Extras** (once per project).
2. Add `TMP_TextInfoDebugTool` to the misaligned text object and enable **Show Lines** to draw ascender, descender, and baseline.
3. Display a mixed Latin + CJK string; adjust ascender/descender (Face Info) on the fallback font asset until the lines align.

Importing Examples & Extras can occasionally loop on import; restarting the Editor resolves it.

## Material presets

1. Select a TMP text object; in the Material section header, right-click → **Create Material Preset**.
2. Rename and adjust (outline, underlay, glow, face dilate).
3. Pick it from the component's Material Preset dropdown.

Presets share the font atlas; each preset is a separate material and therefore a separate batch.

## Sprite assets

Set the sprite asset's source texture **Texture Type = Default**, not Sprite. Sprite import creates sub-assets TMP does not use and slows loading on mobile.

## Dynamic OS

Atlas Population Mode **Dynamic OS** keeps the source font in the Editor but excludes it from the player; at runtime TMP looks for an installed system font with the same Family and Style names and rasterizes from it.

| Platform | CJK system font |
|---|---|
| Android | Noto Sans CJK (broad Chinese/Japanese/Korean coverage) |
| iOS | PingFang (SC/TC/HK) for Chinese; Hiragino for Japanese; Apple SD Gothic Neo for Korean — iOS uses separate families per language, so chain fallbacks accordingly |

Wins: smaller build (no bundled CJK font) and lower memory (OS font data shared). Risks: device font availability varies by OEM and OS version; test on low-end and older devices, and keep a small bundled fallback for critical UI.

## Memory Profiler: Include Font Data

The `.ttf`/`.otf` importer's **Include Font Data** (on by default) embeds the source font in the Font asset, so Editor captures show that cost even when the device does not pay it (notably with Dynamic OS). To make an Editor capture comparable to device, untick it on those fonts before capturing, or profile a device build instead.

## TMP Essential Resources

If `TMPro.TMP_Settings.instance` is null, TMP Essential Resources are missing and TMP APIs throw `NullReferenceException`. Import non-interactively:

```csharp
TMPro.TMP_PackageResourceImporter.ImportResources(importEssentials: true, importExamples: false, interactive: false);
```

Avoid `EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources")` in automation — it opens a modal dialog.

## Common pitfalls

- One giant dynamic font for all languages instead of static main + dynamic fallbacks.
- Mismatched padding ratios across a fallback chain.
- Font asset Scale 0.9 inherited from import.
- AutoSize on live counters.
- `TextMeshProUGUI` in a world-space Canvas for non-interactive labels.
- Missing `Clear Dynamic Data On Build` on dynamic fonts.
- Comparing an Editor capture with Include Font Data on against a device build.
- Sprite asset textures imported as Sprite.
- Leftover `com.unity.textmeshpro` entry in `Packages/manifest.json` after upgrading to Unity 6.
