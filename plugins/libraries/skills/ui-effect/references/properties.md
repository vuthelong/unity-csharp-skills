# UIEffect v5 API reference

Namespace `Coffee.UIEffects`. Verified against `Packages/src/Runtime` (v5.11.x). Ranges are the clamps applied by the C# setters.

## Enums (Enums.cs)

| Enum | Values |
|---|---|
| `ToneFilter` | `None`, `Grayscale`, `Sepia`, `Negative`, `Retro`, `Posterize` (README labels Negative as "Nega") |
| `ColorFilter` | `None`, `Multiply`, `Additive`, `Subtractive`, `Replace`, `MultiplyLuminance`, `MultiplyAdditive`, `HsvModifier`, `Contrast` |
| `GradationColorFilter` | `None`, `Multiply`, `Additive`, `Subtractive`, `Replace`, `MultiplyLuminance`, `MultiplyAdditive` |
| `SamplingFilter` | `None`, `BlurFast`, `BlurMedium`, `BlurDetail`, `Pixelation`, `RgbShift`, `EdgeLuminance`, `EdgeAlpha` |
| `TransitionFilter` | `None`, `Fade`, `Cutoff`, `Dissolve`, `Shiny`, `Mask`, `Melt`, `Burn`, `Blaze`, `Pattern` |
| `TargetMode` | `None`, `Hue`, `Luminance` |
| `BlendType` | `Custom`, `AlphaBlend`, `Multiply`, `Additive`, `SoftAdditive`, `MultiplyAdditive` |
| `ShadowMode` | `None`, `Shadow`, `Shadow3`, `Outline`, `Outline8`, `Mirror` |
| `EdgeMode` | `None`, `Plain`, `Shiny` |
| `PatternArea` | `All`, `Inner`, `Edge` |
| `GradationMode` | `None`, `Horizontal`, `HorizontalGradient`, `Vertical`, `VerticalGradient`, `Radial`, `RadialGradient`, `Diagonal`, `DiagonalToRightBottom`, `DiagonalToLeftBottom`, `Angle`, `AngleGradient` (`RadialFast`, `RadialDetail` obsolete) |
| `DetailFilter` | `None`, `Masking`, `Multiply`, `Additive`, `Subtractive`, `Replace`, `MultiplyAdditive` |
| `Flip` ([Flags]) | `Horizontal`, `Vertical`, `Effect`, `Shadow` |

## UIEffect properties

| Group | Properties (type, range) |
|---|---|
| Tone | `toneFilter` (ToneFilter), `toneIntensity` (0-1) |
| Color | `colorFilter` (ColorFilter), `colorIntensity` (0-1), `color` (setter forces a=1), `colorAlpha`, `colorGlow`; HSV/contrast helpers `colorHueShift` (-0.5..0.5), `colorSaturationShift`, `colorValueShift`, `colorContrastShift`, `colorBrightnessShift` (-1..1) |
| Sampling | `samplingFilter` (SamplingFilter), `samplingIntensity` (0-1), `samplingWidth` (0.5-10), `samplingScale` (0.01-100) |
| Transition | `transitionFilter`, `transitionRate` (0-1), `transitionReverse`, `transitionTexture`, `transitionTextureScale`, `transitionTextureOffset`, `transitionTextureSpeed` (Vector2), `transitionRotation`, `transitionKeepAspectRatio`, `transitionWidth` (0-1), `transitionSoftness` (0-1), `transitionRange` (MinMax01, pattern range), `transitionPatternReverse`, `transitionAutoPlaySpeed` (-5..5, shader `_Time`, scaled) |
| Transition color | `transitionColorFilter`, `transitionColor`, `transitionColorAlpha`, `transitionColorGlow`, `transitionColorHueShift`, `transitionColorSaturationShift`, `transitionColorValueShift`, `transitionColorContrastShift`, `transitionColorBrightnessShift`; gradient via `SetTransitionGradientKeys(Gradient)` / `GetTransitionGradientKeys(...)` |
| Target | `targetMode`, `targetColor`, `targetRange` (0-1), `targetSoftness` (0-1) |
| Blend | `blendType`, `srcBlendMode`, `dstBlendMode` (UnityEngine.Rendering.BlendMode, used when `blendType = Custom`) |
| Shadow | `shadowMode`, `shadowDistance` (Vector2, clamped to +-600 when applied), `shadowIteration` (1-5), `shadowFade` (0-1), `shadowBlurIntensity` (0-1, needs a Blur sampling filter), `shadowMirrorScale` (0-2, Mirror only) |
| Shadow color | `shadowColorFilter`, `shadowColor`, `shadowColorAlpha`, `shadowColorGlow` (`shadowGlow` obsolete), `shadowColorHueShift`, `shadowColorSaturationShift`, `shadowColorValueShift`, `shadowColorContrastShift`, `shadowColorBrightnessShift` |
| Edge | `edgeMode`, `edgeWidth` (0-1), `edgeShinyRate` (0-1), `edgeShinyWidth` (0-1), `edgeShinyAutoPlaySpeed` (-5..5), `patternArea` |
| Edge color | `edgeColorFilter`, `edgeColor`, `edgeColorAlpha`, `edgeColorGlow`, `edgeColorHueShift`, `edgeColorSaturationShift`, `edgeColorValueShift`, `edgeColorContrastShift`, `edgeColorBrightnessShift` |
| Gradation | `gradationMode`, `gradationIntensity` (0-1), `gradationColorFilter`, `gradationColor1`..`gradationColor4`, `gradationOffset`, `gradationScale` (0.01-10), `gradationRotation` (wrapped 0-360, Angle modes), `gradationWrapMode` (TextureWrapMode), `gradationReverse`; gradient via `SetGradientKeys(Gradient)` or `SetGradientKeys(GradientColorKey[], GradientAlphaKey[], ...)` / `GetGradientKeys(...)` |
| Detail | `detailFilter`, `detailIntensity` (0-1), `detailThreshold` (MinMax01, Masking only), `detailColor`, `detailColorAlpha`, `detailTexture`, `detailTextureScale`, `detailTextureOffset`, `detailTextureSpeed` |
| Other | `flip` (Flip flags), `allowToModifyMeshShape`, `customRoot` (RectTransform), `replicas` (List<UIEffectReplica>, read-only) |

Shared serialized values: `transitionRotation` also rotates the detail texture; `transitionKeepAspectRatio` also applies to gradation and detail.

### UIEffect methods

- `LoadPreset(string presetName)`, `LoadPreset(string presetName, bool append)` - runtime presets registered in Project Settings only.
- `LoadPreset(UIEffectPreset preset)`, `LoadPreset(UIEffectPreset src, bool append)`, `LoadPreset(UIEffect src)`, `LoadPreset(UIEffect src, bool append)`.
- `SavePreset(UIEffectPreset dst, bool append)`.
- `Clear()` - reset to the default preset; also resets `samplingScale`, `allowToModifyMeshShape`, `customRoot`.
- `SetVerticesDirty()`, `SetMaterialDirty()`, `SetMaterialContextDirty()` - inherited overrides; rarely needed.
- `SetRate(float rate, UIEffectTweener.CullingMask mask)` - what the tweener calls.
- Inherited from `UIEffectBase`: `graphic`, `effectMaterial`, `transitionRoot`.

## UIEffectPreset (ScriptableObject)

`[CreateAssetMenu]` asset whose public fields mirror the serialized UIEffect fields (`m_ToneFilter`, `m_TransitionFilter`, `m_TransitionTex`, `m_ShadowMode`, `m_GradationMode`, `m_Flip`, ...). Editor presets live in `UIEffectPresets` folders (subfolders shape the preset menu). Register for name lookup with `UIEffectProjectSettings.RegisterRuntimePreset(preset)` or the Runtime Presets list in Project Settings.

## UIEffectProjectSettings (static)

- `UIEffectProjectSettings.shaderRegistry` (ShaderVariantRegistry), `UIEffectProjectSettings.shaderVariantCollection` (ShaderVariantCollection).
- `UIEffectProjectSettings.LoadPreset(string)` returns `Object` (UIEffectPreset or legacy UIEffect). `LoadRuntimePreset(string)` is obsolete.
- `UIEffectProjectSettings.RegisterRuntimePreset(UIEffectPreset)`.
- `UIEffectProjectSettings.useHdrColorPicker`.
- Inspector-only: Runtime Presets, Optional Shaders (UIEffect), Registered/Unregistered Variants, Error On Unregistered Variant, Pre Load Settings In Build, Convert All Legacy Presets, Delete All Legacy Presets.

## UIEffectTweener

Requires a `UIEffectBase` (UIEffect or UIEffectReplica) on the same GameObject.

| Member | Notes |
|---|---|
| `cullingMask` (`UIEffectTweener.CullingMask` flags) | `Tone`, `Color`, `Sampling`, `Transition`, `GradiationOffset`, `GradiationRotation`, `EdgeShiny`, `Event` (spelling "Gradiation" is upstream's) |
| `direction` (`Direction`) | `Forward`, `Reverse` |
| `curve`, `separateReverseCurve`, `reverseCurve` | AnimationCurve, default linear 0-1 |
| `delay`, `duration` (default 1), `interval` | seconds |
| `playOnEnable` (`PlayOnEnable`) | `None`, `Forward` (default), `Reverse`, `KeepDirection` |
| `resetTimeOnEnable` | default true |
| `wrapMode` (`WrapMode`) | `Once`, `Loop` (default), `PingPongOnce`, `PingPongLoop` |
| `updateMode` (`UpdateMode`) | `Normal`, `Unscaled`, `Manual` |
| `rate`, `time`, `totalTime` | current state |
| `isTweening`, `isPaused`, `isDelaying` | read-only |
| `onComplete` (UnityEvent), `onChangedRate` (UnityEvent<float>, needs `Event` in cullingMask) | events |
| `Play()`, `Play(bool resetTime)`, `PlayForward(...)`, `PlayReverse(...)`, `Stop()`, `SetPause(bool)`, `ResetTime()`, `ResetTime(Direction)`, `SetTime(float sec)`, `UpdateTime(float deltaSec)` | `Restart()` obsolete -> `ResetTime()` |

Wrap sequences: Once = delay, 0->1, onComplete. Loop = delay, 0->1, interval, repeat. PingPongOnce = delay, 0->1, interval, 1->0, onComplete. PingPongLoop = delay, 0->1, interval, 1->0, interval, repeat.

## UIEffectReplica

| Member | Notes |
|---|---|
| `target` (UIEffect) | instance to copy; `SetTarget(UIEffectBase)` |
| `preset` (UIEffectPreset) | alternative to target |
| `useTargetTransform` | use the target's transition root (canvas root when following a preset) |
| `customRoot`, `samplingScale`, `allowToModifyMeshShape` | per-replica overrides |

`UIEffectReplica` has no `flip`; the CHANGELOG deprecated it. Set `flip` on the target `UIEffect`.
