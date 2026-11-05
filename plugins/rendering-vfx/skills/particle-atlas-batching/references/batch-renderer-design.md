# Batch renderer design

## Two render paths

`ParticleAtlasBatchRenderer` (`[ExecuteAlways]`, on a `MeshFilter` + `MeshRenderer` child) has two paths. `ParticleAtlasBatchRenderer.NativeBatching` (static, default true) switches all instances at runtime.

**CPU combined mesh** (Edit mode, or when native is off or incompatible): before each camera renders (`RenderPipelineManager.beginCameraRendering`, or `Camera.onPreCull` without an SRP), every enabled instance bakes each source with `ParticleSystemRenderer.BakeMesh` / `BakeTrailsMesh` for that camera, sorts sources like Unity would (sorting order, render queue, camera distance or sort axis + `sortingFudge`, then additive/alpha tie rule, then authored order), and appends them into one dynamic mesh: UV0 = main UV after tiling + blend + sampling flags, UV1 = atlas rect, UV2 = tint. Static camera hook and swap-remove registration keep hundreds of pooled instances allocation-free.

**Native** (Play mode, float textures supported, no trails, no Custom2 color mode): each source renderer is re-enabled with a shared runtime copy of the atlas material (`_ATLAS_NATIVE`), vertex streams `Position, Color, UV, Custom2X`, and Custom2.x = a row index in `ParticleAtlasSourceTable`. All sources of one atlas share one material per render queue, so Unity's own particle batching draws them together and its transparent sort orders them exactly as before packing. Live particles emitted before the switch get their row patched with `SetCustomParticleData`. Leaving native mode restores the authored material, streams, custom data and sprites.

## ParticleAtlasSourceTable

A global `RGBAFloat` texture (`_ParticleAtlasTable`, 5 columns × rows): main rect, main tiling/offset, flags (blend, sampling), tint × factor, sprite proxy mapping. Identical source descriptions share a row (hash + exact compare). Rows upload once per frame in `beginContextRendering`.

Sprite-mode Texture Sheet Animation needs all sprites on one texture to batch, so the table creates proxy sprites on one shared 2048² `Alpha8` texture that the shader never samples (CPU copy dropped). Each source texture gets a uniformly scaled block; the shader maps proxy UVs back with the proxy column. Rotated or tight-packed sprites cannot be remapped linearly and keep their originals. Blocks are keyed by texture name and size so reloaded Addressables bundles reuse them.

`ReleaseUnusedNativeMaterials()` destroys native material copies no `ParticleSystemRenderer` uses any more. They are `DontSave`, so `Resources.UnloadUnusedAssets` never frees them and each one keeps its atlas texture alive: call it at a quiet point (scene change, after clearing pools).

## Shader

`ParticleVfx/Atlas AB Add`: `Blend One OneMinusSrcAlpha`, `Cull Off`, `ZWrite Off`, target 3.5. `SampleRegion` applies each source's wrap mode (repeat, clamp, mirror, mirror-once) inside its rect and picks point or linear clamp samplers per source, at LOD 0 (no mipmaps). `_ClampSource` = 1 on LDR targets saturates the legacy `2×` source before blending, matching what the original shaders produced on an 8-bit target; the C# side sets it per camera (CPU path) or from `Camera.main` and the URP asset (native path).

## Stand-down

On enable the renderer looks for a parent component whose type is `Coffee.UIExtensions.UIParticle` (resolved by name once via reflection). If found, it disables its output and does nothing: `UIParticle` bakes the original renderers with their own materials, and atlas rendering would draw a second copy with swapped materials.

## Extending

To pack another shader family, add the math to the fragment shader keyed by a new `ParticleAtlasBlend` value, add a `packableShaders` entry, and extend `ParticleAtlasBuilder.Describe` plus the checker's `CheckMaterial` comparison for any new snapshot field. Extra textures (masks, dissolve) need more rect columns and UV channels; Unity meshes have 8 UV channels in total.
