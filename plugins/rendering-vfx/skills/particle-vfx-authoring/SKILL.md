---
name: particle-vfx-authoring
description: "Designs new game VFX as Particle System prefabs from a JSON spec, renders contact sheets to judge by eye, refines over several rounds and checks a mobile budget. Use when someone wants a new hit, impact, explosion, slash, cast, charge, aura, buff, heal, trail or ground mark, or wants a generated effect tuned. Triggers: \"make a hit VFX\", \"punchier\", \"less floaty\", \"too bright\", \"poison cloud loop\". Not for packing/repacking existing VFX (particle-atlas-batching), Shuriken scripting/collision (unity-particle-system), GPU particles (unity-vfx-graph)."
license: MIT
metadata:
  category: rendering-vfx
  sources: "https://github.com/patch-gg/pixelfx (method)"
  unity: "6000.0+"
---

# Particle VFX authoring

Treat a particle VFX as a spec. Each effect is one JSON file. The editor tool in `scripts/Editor/` builds it into a prefab:

- a silent root `ParticleSystem` (with `ParticleVfxLifetime` for one-shots, so a pool gets it back);
- one child system per layer, all `scalingMode = Hierarchy`;
- shared materials (one per texture and blend mode) on the packable `ParticleVfx/Legacy Additive` / `Legacy Alpha Blended` shaders.

Textures come from the project. When a needed shape is missing (glow, ring, spark, streak, slash, smoke puff, noise, cells, pixel noise, gradient, ring or smoke flipbook), generate it with `scripts/texgen.py`; hand-painted looks (detailed fire, stylized slashes) still need an artist or an asset pack.

The method comes from pixelfx and Hugo Duprez: a spec is clay, trust your eyes, feed your references. One-shot specs make mediocre effects. Drafts you render, look at and refine make good ones.

## Setup (once per project)

1. Install `com.unity.nuget.newtonsoft-json` (the spec parser uses `Newtonsoft.Json.Linq`). See `unity-package-management`.
2. Copy `scripts/Editor/*.cs` into an `Editor` folder (for example `Assets/VFX/Editor/ParticleVfxGen/`). Copy `scripts/Runtime/ParticleVfxLifetime.cs` into a runtime folder. If you use asmdefs, the editor assembly must reference the runtime one and Newtonsoft.
3. For atlas-packable materials, install the shaders from `particle-atlas-batching`. Without them the builder falls back to `Universal Render Pipeline/Particles/Unlit`, logs one warning, and `check` flags every layer as not packable.
4. To run commands from a terminal, install the `unity` CLI and `com.unity.pipeline` (see `unity-cli`). Otherwise use the editor menus `Assets > Particle VFX > Rebuild From Spec` and `Preview + Check`.
5. If the project pools VFX, set `ParticleVfxLifetime.DespawnHandler = go => pool.Return(go);` once at boot. Without a handler, the component calls `Destroy`.

## Measure the game first

Effects must be designed for the real camera, not the preview zoom. Before the first spec, find and note:

- The gameplay camera: orthographic size (screen height = 2 × size) or perspective distance.
- The unit height in world units (a character's body). Put it in every spec as `referenceHeight`.
- The sorting layers, bottom to top: which one is the ground, which one the units, which one is in front. Ground marks go behind the unit (the unit's layer with a negative order, or a ground layer). Flashes, sparks and anything in front of the body go on a front layer.
- Whether VFX are atlas-batched (`particle-atlas-batching`), and its renderer-group limit.

## Commands

Run from the project root with the Editor open, in **edit mode** (not Play mode). The report is also written to `Temp/particle-vfx/out.txt`.

```bash
sh <skill>/scripts/vfxgen.sh catalog Assets/VFX/Textures [filter] [offset]
sh <skill>/scripts/vfxgen.sh build VfxSpecs/<name>.json [force]
sh <skill>/scripts/vfxgen.sh preview <prefab> [frames=12] [light=1] [fps=30 from=0.05 to=0.4] [view=2] [cell=256]
sh <skill>/scripts/vfxgen.sh check <prefab> [ref=1.3] [peakwarn=60 peakfail=120 coverwarn=12 coverfail=30 groups=3 texmax=512]
sh <skill>/scripts/vfxgen.sh compile    # after editing the editor scripts
```

- `catalog` writes `Temp/particle-vfx/catalog.png`: 64 numbered thumbnails per page over a dark background. The report maps `N (rR cC)` to paths and says `alpha` or `no-alpha`. Page with `offset`. Look for these kinds of textures: soft glows and flares, rings and shockwaves, stars and glints, smoke puffs, flipbook sheets (fire, explosions), slashes and swirls, noise, ground cracks and decals.
- `preview` writes `Temp/particle-vfx/<name>.png` (and `_light.png` with `light=1`). Frames run left to right, top to bottom, over a dark background, with a grey stand-in unit (`ref` tall, feet on the faint ground line).
  - The bar under each cell is progress through the effect. The red tick is `hitTime`; the red-bordered cell is the frame nearest to it.
  - `fps=30 from= to=` shows consecutive frames: judge motion and the hit from those, not from evenly spaced samples.
  - The report lists alive particles and cover per cell.
  - Other options: `view=` (fixed half-height, else auto-framed), `refsorting=` / `reforder=` (stand-in sorting), `noref=1`, `noground=1`, `bg=#hex`, `bglight=#hex`, `seed=`, `cols=`, `out=`.
  - Works on any native (unpacked) prefab, so preview existing project VFX for reference.
- `check` prints `FAIL` / `WARN` findings with numbers, or `PASS no findings`. Defaults are mobile budgets: ≤ 60 alive particles at peak (FAIL > 120), quad cover ≤ 12 unit areas (FAIL > 30), textures ≤ 512 px, noise quality Low, ≤ 3 atlas renderer groups. Raise them for PC or console with the options above.
- `build` refuses to overwrite a prefab it did not generate (the `.meta` `userData` starts with `vfxgen:`); `force` overrides that.

## Procedural textures

`python -I <skill>/scripts/texgen.py <kind> <out.png> [options]` writes an RGBA PNG with the standard library only (no Unity, no numpy). Kinds: `glow`, `ring`, `star`, `streak`, `slash`, `smoke`, `noise`, `cells`, `pixel`, `gradient`. `demo <out.png>` renders every kind on one sheet; `--help` lists the options.

```bash
python -I <skill>/scripts/texgen.py glow Assets/VFX/Textures/Gen/tex_glow_core.png --size 128 --core 0.6
python -I <skill>/scripts/texgen.py ring Assets/VFX/Textures/Gen/tex_ring_thin.png --width 0.05
python -I <skill>/scripts/texgen.py smoke Assets/VFX/Textures/Gen/tex_smoke_4x4.png --size 128 --frames 16
python -I <skill>/scripts/texgen.py pixel Assets/VFX/Textures/Gen/tex_pixel_mess.png --size 64 --levels 3
```

- Default output is white RGB with the shape in alpha, so particle color tints it and both `add` and `alpha` blends work. `--rgb mask` also writes the shape into RGB (black background, `add` only).
- `--frames N` makes a flipbook (`glow`, `ring`, `smoke` and `noise` animate over it); put the printed `tiles` into the layer's `sheet.tiles`.
- Keep sizes small: 64–128 for sparks and dots, 128–256 for glows, rings and smoke, 512 only for big ground marks. The atlas halves them again.
- Import settings and the per-kind options: [references/texgen.md](references/texgen.md).

## 1. Brief

Write the brief as one line with art direction before you touch anything. Find out (ask if it matters and is unknown):

- what triggers it and where it spawns (caster, target, ground, projectile);
- one-shot or loop, and the hit moment;
- element and palette;
- size relative to a unit;
- behind or in front of the body;
- the output path, following the existing names in that folder.

Example: "Frost nova at the feet: white flash on the hit, an ice ring racing outward, shards flung up, cold mist lingering; 0.6 s, hit at 0.08 s, ring behind the unit."

## 2. References first

- Preview 2–3 existing VFX of the same character or element to match the game's look, scale and brightness. If one is close, read its layer setup from the prefab.
- Pick textures with `catalog` and look at the sheet. `no-alpha` textures have a black background: use `blend: add` only.
- Typical picks: soft round glows for cores and flashes, thin rings for shockwaves, stars and flares for glints, small soft dots for sparks (stretched), smoke puffs for volumes, flipbook sheets for fire and explosions (`sheet.tiles`).
- Nothing fits? Generate it with `texgen.py` (below) into the project's texture folder, then `catalog` it like any other texture.

## 3. Draft

Copy the closest example from `references/spec.md` and write the spec. Then `build` and `preview`. When the direction is open, write 2–3 genuinely different drafts (`<name>_a.json`… each with a different `out`). Preview all of them and show the user the strongest before you refine.

## 4. Look, judge, fix

Open the PNG (read the image) every round. Judge it like a picky VFX artist against `references/craft.md`:

- the busiest frame's silhouette;
- the hit frame;
- how it ends;
- the light-background version.

Fix the worst thing, rebuild, and look again. Do **three rounds minimum**; a showpiece (ultimate) takes more. Use `fps=30` around `hitTime` at least once. Check that nothing falls below the ground line or flies off the frame by accident.

## 5. Check and hand over

1. Run `check` until `PASS`, or until only `WARN`s remain that you can justify to the user.
2. Show the user the final contact sheet(s) and the numbers: peak particles, cover, end time, `hitTime`.
3. Tell whoever wires the effect:
   - the prefab path;
   - `hitTime` (deal damage and play the hit reaction then);
   - the `ParticleVfxLifetime` despawn time, or that it loops (the caller stops it);
   - the spawn point (feet, center, muzzle).
4. If the game's other VFX are atlas-batched, pack this one with `particle-atlas-batching` (`PlanRepack`, then `Optimize`). After packing, `build` would overwrite the packed prefab: edit the spec, rebuild, then repack.
5. Commit the spec, the prefab, any new `M_gen_*.mat` and any texture made with `texgen.py`. Never commit `Temp/`.

## Pitfalls

- Edit the spec, not the prefab. A rebuild discards hand edits. If an artist takes over a prefab by hand, clear its `.meta` `userData` so nobody rebuilds over their work.
- `sizeOverLife`, `alphaOverLife` and `colorOverLife` multiply the start values. Brightness never exceeds the color (no HDR): stack additive layers for a hotter core.
- `shape.circle` lies in the screen plane. For a ground ring of particles use `"scale": [1, 0.4, 1]`; for a single ground decal use `aspect` 0.35–0.5.
- `cone` and `edge` take `dir` in 2D degrees (0 right, 90 up). Author directional effects facing right; the game flips or rotates the instance.
- Use `space: world` for trails behind moving projectiles, and `local` for everything that should follow the unit.
- The preview simulates in fixed 0.02 s steps, so a burst at `t` first shows in frames at about `t + 0.02`. In the game it shows on the next rendered frame. Put the flash burst about 0.02 s before `hitTime` so it peaks on the hit.
- The preview disables MonoBehaviours and fixes random seeds (`seed=` shows another roll). The game uses random seeds.
- Batch-rendered (atlas-packed) prefabs preview through the edit-mode path, which can differ from the game. For those, use the Play-mode render-compare in `particle-atlas-batching`.
- A sorting layer name in the spec must exist in Tags and Layers; `build` fails on unknown names.
- `ParticleVfxLifetime.DespawnHandler` is cleared at `SubsystemRegistration` on every Play, including Enter Play Mode without domain reload. Set it at boot, not once in the editor.

## References

- [references/spec.md](references/spec.md): read when writing or editing a spec (every key, defaults, shapes, color math, an example).
- [references/craft.md](references/craft.md): read before judging a preview (the quality bar, choreography, the look-judge-fix table, element palettes).
- [references/texgen.md](references/texgen.md): read when generating a texture (kinds, options, import settings).

## Related skills

- particle-atlas-batching: packing generated VFX into a shared atlas, and the packable shaders.
- unity-particle-system: Shuriken module API, collision, sub-emitters, scripting.
- unity-vfx-graph: GPU particles at scale.
- unity-cli: the `unity command eval` bridge the runner script uses.
- unity-texture-import: import settings for particle textures and flipbook sheets.
