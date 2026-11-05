# Spec format

One JSON file per VFX, kept next to the project's other design data (for example `VfxSpecs/<prefab name>.json`). `vfxgen.sh build` turns it into a prefab. The prefab's `.meta` `userData` remembers the spec (`vfxgen:<path>`), so `Assets > Particle VFX > Rebuild From Spec` and `preview` find it again.

Unknown keys are errors, so typos fail loudly. Units are world units. The preview stand-in unit is `referenceHeight` tall with its feet on y = 0 at the prefab origin. The format is 2D: x right, y up, z ignored.

A value written `range` is a number or `[min, max]` (uniform random per particle). Curves are `[[t, value], ...]` over the particle's normalized lifetime 0..1, ascending. Colors are `"#rrggbb"` or `"#rrggbbaa"`.

## Root

| key | type | default | meaning |
|---|---|---|---|
| `name` | string | required | root GameObject name (= file name) |
| `out` | string | required | `Assets/...prefab` path to write |
| `materialFolder` | string | `Assets/VFX/Materials/Gen` | where generated materials go; one material per texture and blend, shared by every spec |
| `sorting` | string | `Default` | default sorting layer for every layer; must exist in Tags and Layers |
| `duration` | number | 1 | root and default layer duration (s) |
| `loop` | bool | false | looping effect (auras, buffs, projectiles in flight); no `ParticleVfxLifetime` |
| `despawn` | number | auto | `ParticleVfxLifetime.deathTimer`; auto = end of last particle + 0.1, rounded up to 0.1 |
| `hitTime` | number | none | the impact moment (s). Shown as a red tick and border in previews. The skill code deals damage then |
| `referenceHeight` | number | 1.3 | stand-in unit height for previews and the overdraw estimate. Set it to your game's measured unit height |
| `notes` | string | none | the one-line art direction. Keep it: it is the brief for the next person |
| `layers` | array | required | one ParticleSystem child each, drawn by `sorting` / `order` |

## Layer

| key | type | default | meaning |
|---|---|---|---|
| `name` | string | required | child name; say what it is (`flash`, `ring`, `sparks`, `smoke`, `ground_glow`) |
| `texture` | path | none | texture; builds or reuses a material `M_gen_<tex>_<add\|ab>.mat` in `materialFolder` |
| `material` | path | none | use an existing material instead. A non-packable shader is a build warning |
| `blend` | `add` \| `alpha` | `add` | additive (light, glow, sparks, energy) or alpha blended (smoke, dust, debris, dark magic) |
| `sorting` | string | root `sorting` | sorting layer name |
| `order` | int | 0 | order in layer. Behind the unit: the unit's layer with a negative order, or a ground layer below it |
| `position` | [x, y] | [0, 0] | child local position |
| `duration` | number | root | emission window of this layer |
| `loop` | bool | root | |
| `delay` | range | 0 | start delay (s); the main tool for choreography |
| `maxParticles` | int | auto | auto = bursts + rate × lifetime + 4, capped at 200 |
| `space` | `local` \| `world` | `local` | world: particles stay behind when the parent moves (projectile trails) |
| `prewarm` | bool | false | loops only: start full |
| `lifetime` | range | 1 | |
| `speed` | range | 0 | start speed along the shape's direction |
| `size` | range | 1 | start size (world units) |
| `aspect` | number | 1 | height / width. Below 1 flattens to a ground ellipse (0.35–0.5 reads as "on the floor") |
| `rotation` | range | 0 | start rotation, degrees |
| `spin` | range | 0 | degrees per second |
| `flip` | 0..1 | 0 | share of particles spinning or rotating the other way |
| `color` | color or [colorA, colorB] | white | start color; two = random between them |
| `gravity` | number | 0 | gravity modifier (1 = −9.81 y) |
| `drag` | number | 0 | velocity drag (fast burst, slow drift: 2–6) |
| `rate` | number | 0 | particles per second |
| `rateOverDistance` | number | 0 | particles per world unit moved (`space: world` trails) |
| `bursts` | array | [] | `[t, n]`, `[t, [nMin, nMax]]`, `[t, n, cycles, interval]` or `{t, n, cycles, interval}` |
| `shape` | object | point | see below |
| `velocity` | object | none | see below |
| `noise` | object | none | `{strength, frequency, scroll}`; quality is forced Low |
| `sheet` | object | none | flipbook: `{tiles: [x, y], fps \| cycles, randomStart, randomRow}` |
| `sizeOverLife` | curve | none | size multiplier (values may exceed 1) |
| `colorOverLife` | [[t, color]] | none | max 8 keys, multiplies `color` |
| `alphaOverLife` | curve | none | 0..1, max 8 keys |
| `render` | `billboard` \| `stretch` \| `local` \| `world` | `billboard` | `stretch`: along velocity (sparks, rain, speed lines). `local`: quads follow the layer's rotation (slashes) |
| `stretch` | object | `{speedScale: 0.1, lengthScale: 1}` | stretched length = size × lengthScale + speed × speedScale |

A layer needs exactly one of `texture` or `material`, and must emit something (`rate`, `rateOverDistance` or `bursts`).

### shape

| key | default | meaning |
|---|---|---|
| `type` | `point` | `point` (radial burst from one spot), `circle` (ring or disc in the screen plane), `cone` (a spray), `edge` (a line), `rect`, `box`, `sphere`, `hemisphere` |
| `radius` | 0.01 | circle/cone/sphere radius, edge half-length |
| `thickness` | 1 | 0 = emit from the rim only, 1 = whole area |
| `arc` | 360 | degrees of the circle used (counter-clockwise from +x) |
| `arcLoop` | false | emit around the arc in order instead of randomly |
| `angle` | 25 | cone spread (degrees) |
| `dir` | 90 (cone) | 2D direction in degrees: 0 right, 90 up, 180 left, 270 down (cone axis, edge orientation) |
| `position`, `rotation`, `scale` | | shape transform (`rotation` is added after `dir`) |
| `randomDirection` | 0 | 0..1 blend toward random directions |
| `alignToDirection` | false | rotate each particle to its direction of travel (needs `render: billboard`) |

### velocity

`{x, y, radial, orbital, space, speedOverLife}`: extra velocity over the whole lifetime.

- `radial` pushes away from the center; negative implodes.
- `orbital` swirls around z (radians/s, both signs).
- `speedOverLife` is a curve multiplying all velocity, e.g. `[[0,1],[0.3,0.2],[1,0]]` = burst then stop.

## Color math

With the `ParticleVfx/Legacy *` shaders (shipped by `particle-atlas-batching`), generated materials keep `_TintColor` at 0.5 grey and the shader computes `2 × tint × vertex × texture`. With the URP `Particles/Unlit` fallback, `_BaseColor` is white and the shader computes `base × vertex × texture`. Either way the particle color is the color you see (× texture).

Nothing goes above 1. Brightness comes from stacking additive layers (a white core over a tinted glow), not from HDR values.

## Example

This layer setup was verified in a shipped 2D URP game with that game's textures: builds, previews, `check` PASS. The texture paths are placeholders: point each one at a texture of that kind in your project (use `catalog` to find them), and set `sorting` to your layer names.

```json
{
  "name": "vfx_frost_nova",
  "out": "Assets/VFX/Prefabs/Gen/vfx_frost_nova.prefab",
  "sorting": "Effects",
  "duration": 0.6,
  "hitTime": 0.08,
  "notes": "Frost nova at the feet: white flash, an ice ring racing outward, shards flung up, cold mist lingering.",
  "layers": [
    {"name": "flash", "texture": "Assets/VFX/Textures/Light/soft_glow.png", "blend": "add", "bursts": [[0.05, 1]], "lifetime": 0.16, "size": 1.6, "color": "#e8fbff", "position": [0, 0.4], "sizeOverLife": [[0, 0.5], [0.25, 1], [1, 1.1]], "alphaOverLife": [[0, 1], [1, 0]]},
    {"name": "ring", "texture": "Assets/VFX/Textures/Radial/thin_ring.png", "blend": "add", "sorting": "Units", "order": -5, "aspect": 0.4, "bursts": [[0.05, 1]], "lifetime": 0.45, "size": 3.4, "color": "#7fd6ff", "sizeOverLife": [[0, 0.15], [0.35, 0.9], [1, 1]], "alphaOverLife": [[0, 1], [0.5, 0.8], [1, 0]]},
    {"name": "shards", "texture": "Assets/VFX/Textures/Light/small_dot.png", "blend": "add", "bursts": [[0.06, [8, 11]]], "lifetime": [0.3, 0.45], "speed": [3, 6], "size": [0.12, 0.22], "shape": {"type": "cone", "dir": 90, "angle": 70, "radius": 0.3}, "gravity": 0.8, "drag": 3, "render": "stretch", "stretch": {"speedScale": 0.05, "lengthScale": 1.5}, "color": ["#ffffff", "#9fe6ff"], "alphaOverLife": [[0, 1], [0.7, 1], [1, 0]], "position": [0, 0.1]},
    {"name": "mist", "texture": "Assets/VFX/Textures/Smoke/soft_puff.png", "blend": "alpha", "delay": 0.1, "bursts": [[0, 5]], "lifetime": [0.6, 0.9], "speed": [0.2, 0.5], "size": [0.8, 1.3], "shape": {"type": "circle", "radius": 0.9, "thickness": 0.3, "scale": [1, 0.4, 1]}, "rotation": [0, 360], "spin": [-40, 40], "color": "#cdeeff80", "sizeOverLife": [[0, 0.6], [1, 1.3]], "alphaOverLife": [[0, 0], [0.2, 1], [1, 0]], "position": [0, 0.1], "velocity": {"y": [0.1, 0.3]}}
  ]
}
```

Here `Effects` is a sorting layer above the units and `Units` is the units' own layer; the ring uses a negative order on it so it draws behind the body.
