# texgen.py: procedural particle textures

`python -I scripts/texgen.py <kind> <out.png> [options]`. Standard library only, so it runs anywhere Python 3.8+ runs and does not need the Editor. Coordinates: the shape is centered, `radius` and `width` are fractions of half the texture size, angles are degrees counter-clockwise from +x (right).

## Kinds

| kind | use for | main options |
|---|---|---|
| `glow` | cores, flashes, soft dots, sparks (stretched) | `--falloff` (2 soft … 6 tight), `--core` (hot center 0..1) |
| `ring` | shockwaves, ground rings, magic circles | `--radius`, `--width`, `--inner-fill` |
| `star` | glints, lens flares | `--spikes` (lines through the center), `--width`, `--falloff`, `--core` |
| `streak` | speed lines, trails, stretched sparks | `--width`, `--falloff` |
| `slash` | sword arcs, swipes | `--radius`, `--width`, `--arc`, `--arc-start` (tapered both ends, brighter at the head) |
| `smoke` | puffs, dust, mist volumes | `--scale`, `--octaves`, `--threshold`, `--falloff`, `--seed` |
| `noise` | dissolve masks, distortion, scrolling energy | `--scale`, `--octaves`, `--persistence`, `--contrast`; tileable |
| `cells` | crystals, scales, cracked energy | `--scale`, `--edges` (borders instead of blobs); tileable |
| `pixel` | pixel-art noise ("8-bit mess") | `noise` with `--pixelate` (default size/32) and `--levels` (default 4); tileable |
| `gradient` | ramps, fades, beam falloff | `--dir h\|v`, `--falloff` |

Shared options: `--size` (8..2048, cell size with `--frames`), `--seed`, `--gamma`, `--invert`, `--pixelate N`, `--levels N`, `--rgb white|mask|opaque`.

## Flipbooks

`--frames N [--cols C]` renders N cells over t = 0..1 in a grid (default square). `glow` expands and fades, `ring` expands, thickens and fades, `smoke` grows, thins and dissipates, `noise` scrolls one full period (loops seamlessly). Other kinds repeat the same frame. The script prints `tiles CxR`: use it as the layer's `sheet.tiles` and set `sheet.cycles: 1` for one-shots.

## Output channels

| `--rgb` | RGB | alpha | blend |
|---|---|---|---|
| `white` (default) | 255 | shape | `add` or `alpha`; the particle color tints it |
| `mask` | shape | shape | `add` over black, or shaders that read RGB |
| `opaque` | shape | 255 | masks and noise read by a custom shader |

## Import in Unity

- Shapes (`glow`, `ring`, `star`, `streak`, `slash`, `smoke`, `gradient`): Wrap Mode **Clamp**, Alpha Is Transparency on, mipmaps off for VFX on an orthographic camera.
- Tileable kinds (`noise`, `cells`, `pixel`): Wrap Mode **Repeat**.
- `pixel` and anything with `--pixelate`: Filter Mode **Point**, compression off or a high-quality format, or the blocks smear. Atlas packing halves resolution and blurs pixel art; keep those native (see `particle-atlas-batching`).
- The script warns `border alpha` when a shape touches the edge: with Clamp that edge smears across the quad. Shrink `--radius`/`--width` or raise `--falloff`.

Set these by hand in the Inspector or with a `TextureImporter` script (see `unity-texture-import`).

## Judge before using

Open the PNG (read the image) or run `demo` first. Procedural textures look clean and generic: they are good for glows, rings, sparks, smoke and noise, and weak for stylized hand-painted shapes. Vary `--seed` for alternatives and keep 2–3 variants of smoke so repeated particles do not look identical.
