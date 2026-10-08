---
name: 2d-pixel-perfect
description: Sets up, diagnoses, and fixes pixel-perfect 2D rendering with the Pixel Perfect Camera in URP (UnityEngine.Rendering.Universal.PixelPerfectCamera) or Built-in (com.unity.2d.pixel-perfect, UnityEngine.U2D.PixelPerfectCamera). Use when a retro or pixel-art 2D game looks blurry, shimmers, jitters while the camera moves, shows tilemap seams, scales by non-integer factors, or when choosing a reference resolution, assets PPU, grid snapping, crop frame, or Cinemachine pixel-perfect setup. Not for HD 2D, high-resolution art, UI-only scenes, or HDRP.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: 2d
  sources: "Unity-Technologies/skills/skills/2d-pixel-perfect"
  unity: "6000.0+"
---

# 2D Pixel Perfect

## Workflow

1. **Detect the render pipeline first** with `DetectPipeline()` in [scripts/PipelineDetection.cs](scripts/PipelineDetection.cs). The two Pixel Perfect Camera components are separate, incompatible implementations:

   | | URP | Built-in |
   |---|---|---|
   | Component | `UnityEngine.Rendering.Universal.PixelPerfectCamera` | `UnityEngine.U2D.PixelPerfectCamera` |
   | Package | Ships with URP (2D Renderer) | `com.unity.2d.pixel-perfect` |
   | API style | Enums (`gridSnapping`, `cropFrame`) | Booleans (`pixelSnapping`, `upscaleRT`, `cropFrameX/Y`, `stretchFill`) |

   Never install `com.unity.2d.pixel-perfect` in a URP project. HDRP is unsupported (see Fallbacks).
2. **Diagnose before changing anything.** Run the checklist below in scene scope (project-wide only when asked), report what is wrong, then fix only that.
3. **Fix sprite import settings** with [scripts/SpriteImportSettings.cs](scripts/SpriteImportSettings.cs) (editor-only). For general texture import rules see `unity-texture-import`.
4. **Configure the camera** with [scripts/CameraSetupURP.cs](scripts/CameraSetupURP.cs) or [scripts/CameraSetupBuiltIn.cs](scripts/CameraSetupBuiltIn.cs).
5. **Verify in Play Mode** at the target resolution: no blur, no shimmer when the camera pans, integer `pixelRatio`.

Read [references/api-reference.md](references/api-reference.md) when writing or reviewing camera code (full property/method tables, enum mapping between URP and Built-in, recommended configurations).

## Diagnostic checklist

**Sprite import (the usual culprit)**
- Filter Mode = `Point (no filter)` on every in-scope sprite. Bilinear is Unity's default and is the #1 cause of blur.
- Generate Mip Maps off.
- Compression = None (RGBA32). Block compression smears pixel edges.
- Pixels Per Unit identical across the scene and equal to the camera's `assetsPPU`.
- Pivot in Custom/Pixels mode on whole pixels. A Center pivot on an odd-sized sprite (15x15) lands on 7.5 px and misaligns by half a pixel.

**Editor snapping**
- Grid Size = `1 / assetsPPU` on all axes (PPU 16 -> 0.0625, PPU 100 -> 0.01), Grid Snapping on in the Grid and Snap overlay.
- Snap existing objects: select them, Align Selected -> All Axes.

**Camera**
- Orthographic projection.
- Correct Pixel Perfect Camera type for the pipeline.
- `allowHDR`, `allowMSAA`, `allowDynamicResolution` all false.
- Scene view gizmo shows two green boxes: solid = visible area, dotted = reference resolution.

**Quality settings**
- Anti Aliasing = Disabled (0), Anisotropic Textures = Disabled. On URP also set MSAA off on the URP Asset.

## Choosing a reference resolution

Pick it before producing art; never change it afterwards.

| Reference | 1080p | 1440p | 4K |
|---|---|---|---|
| 320 x 180 | 6x | 8x | 12x |
| 480 x 270 | 4x | ~5.3x | 8x |
| 640 x 360 | 3x | 4x | 6x |

320x180 is the safest general choice. For screens without an integer fit (1366x768, ultrawide) use `CropFrame.Windowbox` to add bars instead of stretching to a fractional scale.

Quick enums (URP): `GridSnapping` = `None` | `PixelSnapping` (standard) | `UpscaleRenderTexture` (authentic low-res; conflicts with post-processing and UI text). `CropFrame` = `None` | `Pillarbox` | `Letterbox` | `Windowbox` (safest) | `StretchFill`.

## Common issues

### Blurry sprites
Point filtering, no mipmaps, no compression, AA disabled. Run `SpriteImportSettings.FixSpriteImportSettings`.

### Tilemap seams / gaps
Work through in order. Hacks such as negative cell gap or PPU = 31.99 break as soon as the camera moves.

| # | Check | Fix |
|---|---|---|
| 1 | Tiles packed in a Sprite Atlas with Padding >= 4, Tight Packing off, and Sprite Packer Mode enabled? | See `manage-sprite-atlas` |
| 2 | Mipmaps off on tileset textures and the atlas? | Disable Generate Mip Maps |
| 3 | AA = 0 and MSAA off? | Disable globally and on the camera |
| 4 | Compression = None? | RGBA32 |
| 5 | Tile sprites have even pixel dimensions? | Odd sizes cause 0.5 px offsets |
| 6 | PPU = tile width in pixels (16x16 -> 16)? | Mismatch leaves physical gaps |
| 7 | Gaps only while the camera moves? | Use Pixel Perfect Camera snapping; do not use `cellGap = -0.01f` |

Tilemap setup itself is covered by `tilemap-authoring`.

### Cinemachine fights the Pixel Perfect Camera
Both write the orthographic size every frame. Add the **CinemachinePixelPerfect** extension to each `CinemachineCamera` (Cinemachine 3; Virtual Camera in 2.x) from the Add Extension dropdown. Limitations: blends between cameras are not pixel-perfect mid-transition; `UpscaleRenderTexture` reduces the valid ortho sizes so framing may drift; Target Group + Position Composer (Framing Transposer in 2.x) can look choppy. See `cinemachine-cameras`.

### Post-processing blurs with `UpscaleRenderTexture`
Post-processing runs after the upscale. Simple fix: switch to `PixelSnapping`. Advanced (URP): inject a render pass after post-processing so it runs at screen resolution; on 6000.1+ the 2D Renderer also provides `ScriptableRenderPass2D` / `RenderPassEvent2D` for 2D-aware injection. Confirm in the Frame Debugger that the pass actually executes on the 2D Renderer. Review the feature with `validate-urp-render-graph-renderer-feature`.

### Blurry UI text with `UpscaleRenderTexture`
Known Unity issue, still present in Unity 6. Causes: the Canvas renders into the low-res buffer, and TMP's SDF threshold is miscalibrated at low reference resolutions. Fixes, most reliable first:
1. Canvas in `Screen Space - Overlay` (bypasses the camera).
2. A dedicated UI camera without a Pixel Perfect Camera, Canvas in `Screen Space - Camera`.
3. TMP font material Debug Settings -> Sharpness = 1 (mitigates the SDF issue only). See `optimize-text-mesh-pro`.

### Micro-stutter on physics objects
Physics runs on a fixed step; interpolated positions snap to different pixels per frame.
- `Rigidbody2D.interpolation = RigidbodyInterpolation2D.Interpolate` on visible physics bodies.
- Match `Time.fixedDeltaTime` to the target frame rate where possible (e.g. `1f / 60f`).
- Move the camera in `LateUpdate`, not `FixedUpdate`. For custom controllers snap with `RoundToPixel` (URP).

### Non-integer scaling / pixel decimation
Screen is not an integer multiple of the reference resolution. Pick a reference from the table or use `Windowbox`.

### Missing URP 2D Renderer
`Assets > Create > Rendering > URP 2D Renderer`, then assign it in the URP Asset's Renderer List (and set it as default).

## Migration and compatibility

**URP project using the Built-in component.** Symptom: `DetectPipeline()` returns URP but the camera has `UnityEngine.U2D.PixelPerfectCamera` (`PipelineDetection.HasPipelineMismatch`). Fix: remove the package, remove the component, add `UnityEngine.Rendering.Universal.PixelPerfectCamera`, and map the booleans to enums (table in the API reference).

**Experimental namespace in upgraded projects.** `UnityEngine.Experimental.Rendering.Universal` no longer exists; use `UnityEngine.Rendering.Universal`. `[MovedFrom]` keeps serialized components intact; update any assembly-qualified type strings by hand.

## Fallbacks (HDRP or custom SRP)

The Pixel Perfect Camera is unsupported in HDRP. Render the scene into a low-res `RenderTexture` with `filterMode = FilterMode.Point`, then upscale by an integer factor (a fullscreen pass or a UI `RawImage`). `OnRenderImage` and `Graphics.Blit` in camera callbacks do not run under SRP render graph. Set `UnityEngine.U2D.PixelPerfectRendering.pixelSnapSpacing` manually (see `CameraSetupURP.SetManualPixelSnapSpacing`) if you still want sprite snapping.

## Rules

- Detect the pipeline before writing, diagnosing, or fixing anything.
- Diagnose first; report; then change only what is broken.
- Do not use this skill for HD 2D or high-resolution art: point filtering and snapping make smooth art look wrong. For UI-only scenes use the Canvas Scaler (see `ui-ugui`).
