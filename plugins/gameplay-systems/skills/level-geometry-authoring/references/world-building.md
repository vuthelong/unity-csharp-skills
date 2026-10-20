# World building: procedural terrain, foliage, streaming and proof

Read when generating or regenerating a whole level or open world from code (terrain, splat, trees, grass, props, roads), when a world pops in, hitches or runs slow, or before calling a world "done". The API mechanics are in [terrain-api.md](terrain-api.md); this file is the order of work and the checks.

## Order of work

1. **Spec with numbers.** Platform and frame budget (desktop 60 fps = 16.7 ms; mobile 30 fps with about 65 % headroom = 21.7 ms), world size, traversal speed, the character (radius, height, slope limit, step, jump) and its camera (pivot, boom length, pitch), memory of the lowest device. Every later check needs a number from this step.
2. **Lock structure before content.** Heightmap resolution (2^n + 1; 1025 or more if you erode), terrain size, alphamap and detail resolution, Detail Resolution Per Patch (16 unless grass is sparse and far), the layer list, the prop grid. Changing these later rescales or resamples everything painted.
3. **Generate in ordered, re-runnable steps.** Height (noise), erosion, levelers (flatten under roads, buildings, water), roads, splat rules, trees, details, props. Each step reads only earlier steps and a seed, so it can be skipped or re-run from clean. Same seed, same result: hash the heightmap and assert it in an EditMode test.
4. **Look, then measure.** Captures fog off (layout) and fog on (look), the worst viewpoints, the pop distances.
5. **Stream only what does not fit.** Most single-island worlds fit in memory; measure before splitting.
6. **Tour and compare.** Profile the worst viewpoints, change one thing, compare.

## Rules

- **Persist assets before writing data.** `AssetDatabase.CreateAsset(terrainData, path)` before `SetAlphamaps`, `terrainLayers` and detail layers; created after, the splat is lost in a fresh Editor and the terrain shows only the first layer.
- **Keep the recipe, not the edited result.** Store seeds, curves, rule tables and spline knots in a ScriptableObject and regenerate; hand edits on generated data are lost on the next run. Mark generated scenes or assets as generated (a folder or label) so nobody hand-edits them. A ProBuilder PolyShape rebuilt from its footprint drops every later extrusion.
- **Raw noise needs a second layer.** fBm alone reads as fake: add hydraulic or thermal erosion, then levelers. Roads need routing: a slope-aware A* path over the heightmap cuts far less terrain than straight spline knots (keep road grade at 10 % or less).
- **Splat follows nature.** Sand only in the shore band, rock on steep faces and bare tops, dirt on roads and paths, grass elsewhere. Weights per cell sum to 1. More than 4 layers per splat map costs an extra pass per terrain.
- **Scatter with rules, not spray.** Trees in clusters with varied height and width scale, none on roads, shore, clearings or building footprints; grass breaks up trunk bases and hill shapes without carpeting everything, and never grows through floors. Reject placements by raycast and by mask, not by hope. Polybrush does not work on Terrain; scatter by script.
- **Tree distance is the LOD Group.** For trees with an LODGroup prefab, `Terrain.treeDistance` does nothing; tune the LOD Group transition and cull heights. Billboard distance only affects SpeedTree/billboard trees.
- **Detail color variation lives in the shader.** Healthy/Dry Color is ignored by instanced detail meshes; vary color in the grass shader (`urp-hlsl-shaders`).
- **Light probes for trees.** Tree renderers take GI from Light Probes; a probe grid over the forest is needed, the "Bake Light Probes For Trees" toggle alone does nothing visible.
- **Put content at the edges.** Players test the edge of the world first. Give every stretch of coast or boundary something to see and a way back, instead of an invisible wall.
- **Profile the worst viewpoint.** Teleport to anchor points, turn 360 degrees, keep the worst heading. Judge by counts (batches, triangles, SetPass) in the Editor and by milliseconds only in a development player; Editor ms drift with other processes.
- **Compare statistically.** About 200 frames per side, interleaved runs, a change counts only when the median moves consistently. Per-system cost comes from toggling the system (trees off, grass off) and diffing, never from `FrameTimingManager`, which reports the whole frame.
- **Distance levers need a look check.** A lower cull height can halve batches and leave the hills bald; every performance change gets a before/after capture from the same bookmark.
- **Mobile foliage.** On tile-based GPUs, opaque LOD meshes beat alpha-tested impostors and cards; avoid alpha-tested grass carpets on mobile.

## Streaming (only if it does not fit)

- A persistent core scene owns RenderSettings, the sun, fog, lightmaps and probe settings; chunk scenes hold no lights.
- Load by scene path with `SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive)`; do not hold `allowSceneActivation = false` during play (it stalls the queue, including unloads); use it only behind a loading screen.
- Unload radius larger than load radius (hysteresis). After unloads, `Resources.UnloadUnusedAssets()` and check memory returns near baseline.
- The streamer scans already-open scenes in `Start`, or a chunk opened in the Editor loads twice.
- A visible but unloaded chunk shows a merged low-poly proxy.
- `Application.backgroundLoadingPriority` budgets (Low 2 ms, BelowNormal 4 ms, Normal 10 ms, High 50 ms per frame) only apply in a built player; Editor counters read 0.
- Lighting across scenes: one APV Baking Set holding the core and every chunk, baked with all of them loaded.

## Common mistakes

| Mistake | What it looks like | Fix |
|---|---|---|
| `CreateAsset(terrainData)` after `SetAlphamaps` | terrain is all first layer in a fresh Editor | asset first, then layers and splat |
| Resolution changed after painting | heights rescaled, splat and details resampled and blurred | lock resolutions in the spec step |
| Generated data hand-edited | edits vanish on the next regeneration | edit the recipe, regenerate |
| Tuning `treeDistance` for LOD trees | no change at all | tune the LODGroup |
| Healthy/Dry Color on instanced details | no color variation | vary color in the grass shader |
| `ReplacePrefabAssetOfPrefabInstance` after an unrecorded move | replaced pieces stack at the parent origin | `PrefabUtility.RecordPrefabInstancePropertyModifications` after placing |
| Two NavMesh Surfaces over one area | paths "complete" through walls on the stale surface | one surface per area, rebake overlaps (`initialize-ai-navigation`) |
| Holding scene activation to preload | unloads freeze | load immediately, gate only a loading screen |
| Streaming or memory judged in the Editor | counters 0, priority ignored | development player |
| `new ProfilerRecorder(...)` without `StartImmediately` | marker reads 0 ms forever | `ProfilerRecorder.StartNew` or `ProfilerRecorderOptions.StartImmediately` |
| Layout judged through fog or a top-down ortho camera | washed aerial; LOD trees culled | fog-off perspective aerial captures |

## Done checklist

Score pass, warn or fail with the number or capture behind it; a fail blocks the next stage.

1. **Structure:** resolutions set before size; heightmap 2^n + 1; same seed gives the same height hash; every step re-runs from clean.
2. **Terrain look:** splat follows nature (no single-layer terrain); no staircasing under a low sun; no spikes or pits after erosion; tiling not obvious from altitude; road grade at 10 % or less with no canyon cuts.
3. **Foliage:** varied silhouettes, clustered, nothing on roads, shore or floors; pop pairs captured at the detail distance, the LOD switch and the cull, with fog off and on.
4. **Atmosphere:** fog color matches the sky's horizon; far views wash out, eye-level views barely change.
5. **Edges:** every boundary stretch has content and a way back.
6. **Streaming (if used):** core owns lighting; memory back near baseline after unload; loading budget met in a player.
7. **Performance:** every anchor's p95 under budget, worst heading reported; before/after compared over about 200 frames; every lever change has a look check.

End with `Verified:` (what ran, captures opened) and `Assumed:` (device numbers, bakes, anything not run).
