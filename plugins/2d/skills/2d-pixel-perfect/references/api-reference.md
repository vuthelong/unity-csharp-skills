# Pixel Perfect Camera API Reference

## URP `PixelPerfectCamera` (`UnityEngine.Rendering.Universal`)

Stable across Unity 6 (6000.0 to 6000.6). Lives in the URP 2D runtime assembly; needs the 2D Renderer.

### Properties

| Property | Type | Access | Notes |
|---|---|---|---|
| `assetsPPU` | `int` | get/set | Must equal Pixels Per Unit on all sprites in view |
| `refResolutionX` | `int` | get/set | Reference width |
| `refResolutionY` | `int` | get/set | Reference height |
| `cropFrame` | `PixelPerfectCamera.CropFrame` | get/set | Aspect-ratio handling |
| `gridSnapping` | `PixelPerfectCamera.GridSnapping` | get/set | Replaces old `pixelSnapping` / `upscaleRT` booleans |
| `orthographicSize` | `float` | get | Size the component computed (differs from `Camera.orthographicSize`) |
| `pixelRatio` | `int` | get | Integer scale applied (6 at 1080p with 320x180) |
| `requiresUpscalePass` | `bool` | get | True when `gridSnapping = UpscaleRenderTexture` |

### Methods

| Method | Returns | Notes |
|---|---|---|
| `RoundToPixel(Vector3)` | `Vector3` | Snaps a world position to the pixel grid; respects current `gridSnapping` |
| `CorrectCinemachineOrthoSize(float)` | `float` | Nearest pixel-perfect ortho size; used by the Cinemachine extension |

### Static helper

`UnityEngine.U2D.PixelPerfectRendering.pixelSnapSpacing` (float): world size of one screen pixel, `orthographicSize * 2 / pixelHeight`. Set manually only in custom setups without the component.

## `GridSnapping`

| Value | Built-in equivalent | Behaviour |
|---|---|---|
| `None` | `pixelSnapping = false`, `upscaleRT = false` | No snapping |
| `PixelSnapping` | `pixelSnapping = true` | Snaps SpriteRenderers at render time; transforms keep float precision |
| `UpscaleRenderTexture` | `upscaleRT = true` | Renders at reference resolution then upscales; conflicts with post-processing and UI text |

## `CropFrame`

| Value | Built-in equivalent | Behaviour |
|---|---|---|
| `None` | both crop flags false | No crop |
| `Pillarbox` | `cropFrameX = true` | Bars left/right |
| `Letterbox` | `cropFrameY = true` | Bars top/bottom |
| `Windowbox` | both true, `stretchFill = false` | Bars on all sides. Safest default |
| `StretchFill` | both true, `stretchFill = true` | Fills screen keeping aspect; enables the component Filter Mode |

Component Filter Mode (Inspector, only with `StretchFill`; separate from per-texture Filter Mode): `Retro AA` (default, recommended: integer upscale then bilinear to screen) or `Point` (nearest all the way; can lose pixel-perfectness at fractional scale).

## Recommended configurations

| Use case | `gridSnapping` | `cropFrame` | Notes |
|---|---|---|---|
| Standard pixel art | `PixelSnapping` | `Windowbox` | Works with post-processing and UI |
| Authentic low-res look | `UpscaleRenderTexture` | `Windowbox` | No post-processing; separate UI camera |
| Fill rate relief on 4K / HiDPI | `UpscaleRenderTexture` | `Windowbox` | Same UI caveat |
| Full-screen stretch | `PixelSnapping` | `StretchFill` | Retro AA filter |

## Built-in `PixelPerfectCamera` (`UnityEngine.U2D`, package `com.unity.2d.pixel-perfect`)

Booleans only. No `RoundToPixel`, `CorrectCinemachineOrthoSize`, `requiresUpscalePass`, or `orthographicSize`. Assembly name: `Unity.2D.PixelPerfect` (no `Runtime` suffix).

| Property | Type | Constraint |
|---|---|---|
| `assetsPPU` | `int` | |
| `refResolutionX` / `refResolutionY` | `int` | |
| `pixelSnapping` | `bool` | Ignored when `upscaleRT = true` |
| `upscaleRT` | `bool` | |
| `cropFrameX` / `cropFrameY` | `bool` | Bars left/right, top/bottom |
| `stretchFill` | `bool` | Only effective when both crop flags are true |
| `pixelRatio` | `int` (get) | Same as URP |
