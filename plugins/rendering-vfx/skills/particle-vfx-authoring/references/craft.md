# Craft: what makes a game VFX good

Adapted for Unity particle systems from the method of [pixelfx](https://github.com/patch-gg/pixelfx) (MIT) and Hugo Duprez's "How to make (good) pixel art effects with Claude": a spec is clay, trust your eyes, feed your references. One-shot specs make mediocre effects. Drafts you render, look at and refine make good ones.

## The bar

1. **Layers, not one emitter.** A good hit is 4–7 layers, each with one job: flash, core, shockwave, sparks, debris, smoke, ground mark, lingering embers. One emitter with a nice texture looks like a sticker.
2. **Light from stacking.** A white-hot core (small, additive, short) sits over a tinted glow (bigger, softer, lower alpha), which sits over the colored body. Put deep, saturated color only at the edges and in what fades last. A big flat area of saturated color is almost always wrong.
3. **Translucent volumes.** Smoke, mist and dust use `alpha` blend and soft textures. They swell (`sizeOverLife` up), thin out (`alphaOverLife` down), spin slowly and use drag. Never use additive smoke over a light background: it turns into fog.
4. **Real motion.** Sparks are fast, stretched (`render: stretch`), slowed by `drag` and pulled by `gravity`. Debris tumbles (`spin`) and falls. Fire rises and cools into smoke (`colorOverLife` from white/yellow to deep red to transparent). Things that land kick up dust. Fast burst, slow drift: `drag` 2–6, or a `speedOverLife` that drops early.
5. **Choreography in phases.**
   - Anticipation: gathering in (`velocity.radial` negative, a charge glow growing).
   - Release: a 1–2 frame white flash at `hitTime`; the shockwave starts.
   - Spread, then hold: glints, embers, a fading mark.
   - Long decay.
   Use many small events with `delay` ranges, each varied in size, speed and lifetime.
6. **Depth.** Ground rings and cracks are flat ellipses (`aspect` 0.35–0.5) behind the unit: the unit's sorting layer with a negative order, or a ground layer below it. What flies at the camera (sparks, flash) goes on a front layer. Back layers are dimmer.
7. **Timing.** Fast attack, slow release: a `sizeOverLife` that jumps to 0.8 in the first 20 % then creeps. Hits are snappy (0.3–0.8 s total) but still have follow-through. Ultimates linger 1.5–3 s while smoke clears and sparkles fade. The peak frame (the one that sells the hit) comes within 0.05–0.1 s of `hitTime`.
8. **Reads small.** On a phone the playfield is tiny. Give it one clear focal point, a strong silhouette at the busiest frame, and no mush. If you need the 256 px preview cell to tell what it is, it fails in game.
9. **Variety.** Every layer randomizes something: size, speed, rotation, lifetime, start delay, or color between two values. Identical particles read as mechanical.
10. **Cheap.** Stay within budget (`check`). Use as few layers as the look allows, and only textures already in the project.

## The body around the effect

An effect without the character's timing is a sticker. The VFX does not drive the character, but its `hitTime` must match the attack's animation event. Coordinate with whoever wires the skill:

- **Wind-up**: if the attack has a charge, make it its own prefab (`*_charge`, looping or one-shot). It plays during the wind-up animation and gathers inward. The release prefab starts on the animation's attack frame.
- **Release frame**: the biggest silhouette change lands on `hitTime`: flash, shockwave start and the densest spark burst. Damage and the target's hit reaction must happen on that same frame.
- **Freeze on contact** is a code feature (hit-stop or time scale), not VFX. If the hit feels weak and the VFX is already punchy, suggest a hit-stop of 0.03–0.12 s or a camera shake instead of more particles.
- **Follow-through**: something keeps moving after the hit: embers drifting, smoke rolling, a ring still expanding while it fades.

## Look, judge, fix

| you see | fix |
|---|---|
| reads as mush at small size | fewer layers, one focal point, bigger and fewer particles, less glow around the core |
| floaty, no punch | fast in, slow out: `sizeOverLife` `[[0,0.3],[0.15,1],[1,1.1]]`; a 2–3 frame white flash at `hitTime`; shorter lifetimes; `drag` |
| stiff, mechanical | ranges on everything; `delay` ranges; a secondary layer (sparks, smoke, debris) |
| washed out | lower glow alpha (0.3–0.5), smaller flash, pure white for the core only; check the `light=1` preview |
| flat | add a dim, larger back layer and a bright, small front layer; a ground mark behind the unit |
| smoke looks like fog or glows | `blend: alpha`, a smoke texture, darker color with alpha below 1 |
| pops off at the end | shrink (`sizeOverLife` to 0) or fade (`alphaOverLife` to 0) before the lifetime ends |
| pops in at the start | alpha from 0 over the first 5–10 %, unless it is the flash |
| one particle hides the unit | it is too big or too opaque at the busiest frame; lower cover (see `check`) |
| sparks look like dots | `render: stretch`, higher speed, `speedScale` 0.03–0.08, small width |
| ring looks like a sticker on the screen | `aspect` 0.4, behind the unit, alpha fades from the inside out |
| loop has a visible seam | `prewarm: true`, constant `rate` instead of bursts, lifetimes shorter than the duration |
| hit lands out of sync | move the flash and shockwave `bursts` time to `hitTime`, not to a time eyeballed from the preview |
| over budget | drop the least visible layer first, then halve counts and raise size; never ship a FAIL |

## Element palettes

Start points; tune by eye.

| element | core | body | edge / fade |
|---|---|---|---|
| fire | `#fff6d0` | `#ffb340` `#ff6a1a` | `#b3220a` → smoke `#3a2a2a80` |
| ice | `#ffffff` | `#9fe6ff` `#4fb8f0` | `#2a5fb0` → mist `#cdeeff80` |
| lightning | `#ffffff` | `#c8f0ff` `#7ab8ff` | `#5a4dff` |
| holy | `#fffbe8` | `#ffe28a` `#ffc94a` | `#e89a2a` |
| arcane | `#f6e8ff` | `#c58cff` `#8a4dff` | `#4a1fa8` |
| shadow (alpha) | `#c9a8ff` glints | `#3a1f55` `#24123a` | `#0d0614` |
| poison / nature | `#f0ffd0` | `#9ce65a` `#4fb83a` | `#2a6b2a` |
| water | `#ffffff` | `#8ad8ff` `#3a9ee8` | `#1f5fa8` |
| physical / earth | `#fff3e0` sparks | `#c8a07a` dust | `#6b4a33` debris |
