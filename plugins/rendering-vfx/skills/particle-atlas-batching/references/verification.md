# Verifying packed VFX

## render_compare.cs

Renders the old (native) and new (packed) prefab side by side at 0.15, 0.4 and 0.9 s, one PNG per atlas key.

1. Write `Temp/particle-atlas/cmp_list.txt`, one tab-separated line per prefab: `<key>\t<packed prefab path>\t<old prefab path>`.
2. Put the old prefab under `Assets/` temporarily: `git show HEAD:<path> > Assets/__cmp/old_0.prefab`, or copy `<backup>/<index>.bin` from `Library/ParticleAtlas/Backups/<run>/manifest.json`. Unity must import it (Refresh).
3. Enter Play mode in any scene, then `sh run.sh render_compare.cs`.
4. Open `Temp/particle-atlas/compare_<key>.png`: columns are old, new for each time; rows are prefabs. The runner prints the ortho size per row.
5. Exit Play mode and **delete `Assets/__cmp/`**: copies left under `Assets/` reference the atlas material and join the group on the next scan.

## Three rules

- **Render in Play mode.** In Play mode `ParticleAtlasBatchRenderer` uses the native path: each source renderer draws itself with a runtime copy of the atlas material and normal Unity sorting. That is what players see. Edit mode uses the CPU combined-mesh path, which sorts differently in edge cases.
- **Force alpha to 1 before saving the PNG.** Legacy shaders blend the alpha channel with `SrcAlpha`, the atlas shader writes premultiplied alpha with `One`. Raw `ReadPixels` PNGs therefore make the old side look washed out in image viewers although the screen colors match. Without this, a whole batch of prefabs can look like a regression (dark discs, shifted colors) that does not exist.
- **Fix random seeds**: `useAutoRandomSeed = false; randomSeed = 1234` on every system before `Simulate(t, true, true, true)`, so both sides spawn identical particles.

The compare camera is a plain orthographic camera with no canvas: UI-layer VFX and `UIParticle` content do not show; check those in game.

## What to look for

- Brightness and hue of additive cores (tint × factor, HDR clamp on LDR targets).
- Layer order between additive and alpha sources at the same order and depth: `additiveBeforeAlphaAtSameDepth` flips the tie.
- Blur or bleeding on small or pixel-art textures (half resolution): keep those prefabs native.
- Sprite-sheet frames: wrong frame or bleeding means the sheet was reduced too far; cap other textures with the texture policy instead.

## Audit heuristics (audit_unoptimized.py)

Reads prefab YAML only, no Editor needed. A renderer is live when enabled, on an active GameObject chain, and a particle renderer's render mode is not None. Non-atlas renderers are those whose first material's shader is not the atlas shader. Statuses:

- `should pack`: more than 2 live renderers and at least 2 non-atlas ones.
- `optional (little to merge)`: within budget already; the optimizer skips these unless you `PlanRepack` them explicitly.
- `needs review`: a shader outside `--supported`, Animator/Animation, or a nested prefab.

Shader Graph shaders show as `Shader Graphs/<file name>`; built-in materials and missing materials are reported as such.
