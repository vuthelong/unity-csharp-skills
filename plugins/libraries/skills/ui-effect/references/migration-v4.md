# Migrating UIEffect v4 to v5

Verified against `Packages/src/Samples~/v4 Compatible Components` and the v5 README.

## Steps

1. Change the git URL. v5 lives under `Packages/src` and the default branch moved from `upm` to `main`:
   ```json
   "com.coffee.ui-effect": "https://github.com/mob-sakai/UIEffect.git?path=Packages/src#5.11.7"
   ```
   A v4-style URL without `?path=Packages/src` (or with `#upm`) does not resolve to v5.
2. In Package Manager, select UI Effect and import the `v4 Compatible Components` sample. It restores the v4 component types (namespace `Coffee.UIEffects`, assembly `UIEffect`) so existing scenes and prefabs keep their references.
3. In code, rename references to the v4 `UIEffect` component to `UIEffectV4`. In v5 the name `UIEffect` is the new all-in-one component.
4. Convert each component with its Inspector context menu `Convert To UIEffect` (available on `UIDissolve`, `UIShiny`, `UIHsvModifier`, `UITransitionEffect`, `UIEffectV4`). Convert prefabs at the source asset, not per instance.
5. Rebuild the look manually for components without a converter (table below), then delete the compatibility sample once nothing references it.
6. Open every converted UI once in the Editor so the new shader variants are registered (see SKILL.md, Shader variants), then make a player build to confirm.

## Component map

| v4 component | v5 equivalent | Conversion |
|---|---|---|
| `UIEffect` (v4: `effectMode`, `effectFactor`, `colorMode`, `colorFactor`, `blurMode`, `blurFactor`) | `UIEffect`: `toneFilter` + `toneIntensity`, `colorFilter` + `colorIntensity`, `samplingFilter` + `samplingIntensity` | Compat type renamed `UIEffectV4`; `Convert To UIEffect` |
| `UIDissolve` (`effectFactor`, `width`, `softness`, `color`, `colorMode`, `transitionTexture`, `keepAspectRatio`) | `transitionFilter = Dissolve`, `transitionRate`, `transitionWidth`, `transitionSoftness`, `transitionColor`, `transitionColorFilter`, `transitionTexture` | `Convert To UIEffect` |
| `UIShiny` (`effectFactor`, `width`, `rotation`, `softness`, `brightness`, `gloss`) | `transitionFilter = Shiny`, `transitionRate`, `transitionWidth`, `transitionRotation`, `transitionSoftness`, `transitionColor`, `transitionColorFilter` (`MultiplyAdditive` when gloss > 0.5, else `Additive`) | `Convert To UIEffect` |
| `UIHsvModifier` (`targetColor`, `range`, `hue`, `saturation`, `value`) | `colorFilter = HsvModifier` with `colorHueShift`/`colorSaturationShift`/`colorValueShift`, `targetMode = Hue`, `targetColor`, `targetRange` | `Convert To UIEffect` |
| `UITransitionEffect` (`effectMode` Fade/Cutoff/Dissolve, `effectFactor`, `dissolveWidth`, `dissolveSoftness`, `dissolveColor`, `passRayOnHidden`) | `transitionFilter` Fade/Cutoff/Dissolve, `transitionRate`, `transitionWidth`, `transitionSoftness`, `transitionColor` | `Convert To UIEffect` |
| `UIShadow` (`style`, `effectColor`, `effectDistance`, blur) | `shadowMode` (`Shadow`, `Shadow3`, `Outline`, `Outline8`), `shadowColor`, `shadowDistance`, `shadowBlurIntensity`, `shadowIteration` | Not supported in v5; the compat stub is an empty `ObsoleteMonoBehaviour`. Rebuild by hand on the graphic's `UIEffect` |
| `UIGradient` | `gradationMode`, `gradationColor1..4`, `SetGradientKeys(...)`, `gradationRotation`, `gradationOffset`, `gradationScale` | Not supported in v5; rebuild by hand |
| `UIFlip` (`horizontal`, `vertical`) | `UIEffect.flip` (`Flip.Horizontal`, `Flip.Vertical`, plus `Effect`/`Shadow` to flip those layers) | Compat component still works; no converter |
| Shared effect setups (v4 `UISyncEffect`, not in the v5 compat sample) | `UIEffectReplica` (target = a `UIEffect` or a `UIEffectPreset`) | Manual |
| v4 `EffectPlayer` settings (`play`, `duration`, `loop`, `loopDelay`, `initialPlayDelay`, `updateMode`) | `UIEffectTweener` (`duration`, `delay`, `interval`, `wrapMode`, `updateMode`, `playOnEnable`) | Manual |

Enum mappings used by the converters: v4 `ColorMode` `Multiply`/`Fill`/`Add`/`Subtract` -> `ColorFilter` `Multiply`/`Replace`/`Additive`/`Subtractive`; v4 `EffectMode` `Grayscale`/`Sepia`/`Nega`/`Pixel` -> `ToneFilter.Grayscale`/`Sepia`/`Negative` or `SamplingFilter.Pixelation`; v4 `BlurMode` `FastBlur` -> `BlurFast`, `MediumBlur` and `DetailBlur` -> `BlurDetail`.

## Behaviour changes to check

- `effectArea` (v4) is gone. Transition/gradation/detail effects span the graphic's own RectTransform, or `customRoot` if set; for a shared span over several graphics, use `UIEffectReplica` with `useTargetTransform`.
- Multiple v4 components stacked on one object (for example `UIEffect` + `UIShiny` + `UIShadow`) collapse into one v5 `UIEffect` (it is `[DisallowMultipleComponent]`). Combine the filter groups on one component.
- `UITransitionEffect.passRayOnHidden` has no flag; v5 `UIEffect` already stops accepting raycasts when `transitionRate` reaches 1 for Fade/Cutoff/Dissolve/Melt/Burn/Blaze.
- Code that animated `effectFactor` should set `transitionRate`/`toneIntensity`/etc., or use `UIEffectTweener` with the matching `cullingMask`.
