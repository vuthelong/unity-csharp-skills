# Shader checklist

Read before calling a shader, a variant plan or a shader fix done. Score each line 0 (missing), 1 (claimed), 2 (measured), 3 (measured and looked at). Ship at 2 or more on every line that applies; a 0 on a "must" line blocks it. Write the evidence (log line, number, capture path) next to the score.

## A. Correctness (must)

| # | Check | How | Pass |
|---|---|---|---|
| A1 | Compiles for the target API | `ShaderUtil.GetShaderMessages(shader)` after import; Editor.log | 0 errors on every pass |
| A2 | Passes agree | Opaque/cutout: UniversalForward, ShadowCaster, DepthOnly, DepthNormals present; displacement and clip in one shared function | a shadow follows the clipped or displaced shape |
| A3 | Depth Priming safe | URP asset Depth Priming Mode forced on | object still renders |
| A4 | Lights under the project's paths | extra point light and a second directional light, Forward and Forward+ | lit side changes in both paths |
| A5 | Shadows cast and received | sample luminance under the object vs open ground | shadow clearly darker (about 0.75x or less) |
| A6 | SRP Batcher | Frame Debugger draw lands in an SRP Batch; Inspector shows "SRP Batcher: compatible" | compatible |
| A7 | Camera textures exist | Depth or Opaque Texture on every URP asset that renders it; test with SSAO off | effect unchanged with SSAO off |
| A8 | No magenta, no blank | look at every capture | none |

## B. Look (must for visible work)

| # | Check | Pass |
|---|---|---|
| B1 | Every capture opened | the change is visible, nothing else changed |
| B2 | Foliage | no shadow acne or peter-panning, wind sways the shadow too, back faces lit, cutout edge stable at distance (no shimmer) |
| B3 | Terrain and ground | layer blend has no visible tiling at mid distance, wet or snow state reads in both day and overcast light |
| B4 | Water | shallow-to-deep hue shift, calm shore, motion between two times, no normal-map grid at distance |
| B5 | Emissive or HDR edges | keep their hue under the project's tonemapping |

## C. Cost and variants (must before a build)

| # | Check | How | Pass |
|---|---|---|---|
| C1 | Variant budget | build with Strict Shader Variant Matching; read the stripping funnel in the build log | under budget, no growth over the last build without a reason |
| C2 | Keyword types | `multi_compile` only for runtime toggles; no `shader_feature` toggled from C#; `dynamic_branch` tested with `if` | grep the project for `EnableKeyword`/`SetKeyword` on `shader_feature` keywords: none |
| C3 | GPU cost | GPU ms A/B in a development player at the same resolution, interleaved | faster beyond run-to-run noise (about 10 %) |
| C4 | First-use stutter | `GraphicsStateCollection` traced per API, warmed during loading | warm run shows 0 `Shader.CreateGPUProgram` samples in gameplay |
| C5 | Overdraw | alpha-tested foliage over the worst viewpoint | within the frame budget; opaque LOD beats cutout far away on tile GPUs |

## Verdict format

`<shader>: A <scores>, B <scores>, C <scores>. Ship / Fix <lines> first. Not checked: <lines and why>.`
