# Lighting and shadow cost

Read when lights, shadows or the rendering path dominate GPU time. Light setup, baking workflow and artifacts are in `unity-lighting`; this file is about cost.

## Rendering path

| | Forward | Forward+ | Deferred | Deferred+ |
|---|---|---|---|---|
| Additional lights per object | Limited (Per Object Limit, max 8) | No per-object limit | No per-object limit | No per-object limit |
| Lights per camera | Limited by visible-light cap | Higher cap (clustered) | Limited by visible-light cap | Clustered |
| Cost model | Shading cost × lights touching each object | Light clustering (CPU jobs) + per-pixel cluster lookup | G-buffer write + per-light screen-space shading | G-buffer + clustered lighting |
| Bandwidth | Lowest | Low | High (G-buffer). Acceptable on tile GPUs only when the G-buffer stays in tile memory (native render pass) | High |
| MSAA | Yes | Yes | No | No |
| GPU Resident Drawer | No | Yes | No | Yes |
| Available | 6000.0 | 6000.0 | 6000.0 | 6000.1+ (verify in your editor version) |

Visible-light caps differ by platform (desktop vs mobile vs GLES) and URP version; check the URP "Rendering paths comparison" page for your version instead of hard-coding numbers.

Choosing:

- **Mobile, few lights**: Forward with a low Per Object Limit (2-4), or Forward+ if you need GRD.
- **Many small lights** (interiors, neon): Forward+.
- **Many lights with expensive materials on desktop/console**: Deferred or Deferred+. Not for mobile unless you have measured native render pass merging in the Render Graph Viewer.
- **Transparent objects** are always forward-shaded, even in Deferred.

## Additional lights

URP Asset > Lighting:

- **Additional Lights**: Per Pixel / Per Vertex / Disabled. Per Vertex is much cheaper on mobile and acceptable for fill lights.
- **Per Object Limit** (Forward only): lower to cap cost on dense lighting.
- **Cast Shadows** for additional lights: each spot light shadow is one shadow map; each **point light shadow is six**. Shadow atlas resolution and the Low/Medium/High resolution tiers control size.
- Light **Range**: smaller ranges touch fewer objects and fewer Forward+ clusters.
- **Culling mask / Rendering Layers**: restrict which objects a light affects.
- Light cookies add a texture sample per light.

## Main light shadows

URP Asset > Shadows and Lighting > Main Light:

| Setting | Cost driver | Mobile guidance | Desktop guidance |
|---|---|---|---|
| Max Distance | Number of shadow casters and texel density | 20-50 m | 80-150 m |
| Cascade Count | One shadow pass per cascade (draw calls × cascades) | 1-2 | 2-4 |
| Cascade splits | Texel distribution | Tight first cascade | Default or tuned |
| Shadow Resolution | Shadow map fill and memory | 1024-2048 | 2048-4096 |
| Soft Shadows | Sampling taps per pixel (Low/Medium/High quality) | Off or Low | Medium |
| Depth / Normal Bias | Quality only | Tune for acne | Tune for acne |

- Shadow caster draws are counted separately in the Frame Debugger (`Shadows.Draw` / `MainLightShadow`). Turn off **Cast Shadows** on small props, grass and particles.
- **Conservative Enclosing Sphere** and cascade blending options change stability and cost; verify availability per URP version.
- Screen-space shadows (renderer feature) resolve the cascade lookup once per pixel; it helps only when the shading cost of cascade selection is significant and costs an extra fullscreen pass and depth texture.

## Baked vs mixed vs realtime

| Mode | Runtime cost | Memory | Dynamic objects |
|---|---|---|---|
| Baked | Lightmap sample only | Lightmaps (and directional maps) | Lit via light probes / APV |
| Mixed: Subtractive | Cheapest mixed; main light realtime only on dynamic objects | Lightmaps | Shadow from main light only, blended |
| Mixed: Shadowmask | Shadowmask texture lookups; realtime shadows only within distance with Distance Shadowmask | Lightmaps + shadowmask | Full realtime shadows near the camera |
| Mixed: Baked Indirect | Realtime direct light and shadows everywhere | Indirect lightmaps | Full realtime |
| Realtime | Full cost | None | Full |

- Mobile default: Baked or Mixed Subtractive / Shadowmask for static geometry, a single realtime directional light for dynamic objects.
- **Shadowmask mode** (Quality settings): Shadowmask (baked shadows at all distances, cheapest) vs Distance Shadowmask (realtime within shadow distance, more draws).
- Adaptive Probe Volumes cost a few texture samples per pixel and memory per cell; set the probe density and streaming per tier. Legacy light probes cost less but light per object, not per pixel.
- Reflection probes: baked only on mobile; realtime probes re-render the scene (6 faces) when they update. Box projection and probe blending have per-pixel cost.
- Lightmap memory: lower Lightmap Resolution, Max Lightmap Size and use compressed lightmaps (Lighting settings) on mobile; see `unity-texture-import` for compression formats.

## Quick wins

1. Disable shadows on everything that does not need them.
2. Reduce shadow distance and cascades per tier.
3. Bake static lighting; keep one realtime directional light.
4. Lower additional lights to Per Vertex or Disabled on low tiers.
5. Shrink light ranges and use Rendering Layers.
