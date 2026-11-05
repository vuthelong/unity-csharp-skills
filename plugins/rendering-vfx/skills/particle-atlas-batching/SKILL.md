---
name: particle-atlas-batching
description: "Packs Shuriken particle VFX prefabs into a shared texture atlas per group so each effect draws in one batch on mobile URP, and keeps packed prefabs in sync after edits. Use when VFX cost too many draw or SetPass calls, or a packed VFX was edited. Triggers: \"pack these VFX into an atlas\", \"I edited a packed VFX, check it\", \"which VFX are unoptimized\", \"repack the hero's effects\". Not for authoring new effects (particle-vfx-authoring), general Shuriken scripting (unity-particle-system), sprite atlases for sprites (manage-sprite-atlas)."
license: MIT
metadata:
  category: rendering-vfx
  sources: "original"
  unity: "6000.0+"
---

# Particle Atlas Batching

A packed VFX prefab keeps every `ParticleSystem` simulating as authored, but its particle renderers are disabled. A `ParticleAtlasBatchRenderer` child named `Combined Atlas <sorting layer>` draws them with one shared material `M_<key>_AB_Add` (shader `ParticleVfx/Atlas AB Add`) whose texture is the group atlas `<key>_AB_Add.png`. Each batch `Source` stores a **snapshot** of its material taken at pack time: UV region, tiling, tint, blend mode, filter and wrap. Editing the prefab, its materials or its textures afterwards does **not** update the atlas until you repack.

## Install the tooling

Copy from this skill into the project:

| From | To | Notes |
|---|---|---|
| `scripts/Runtime/*.cs` | any runtime folder, e.g. `Assets/ParticleAtlas/Runtime/` | Needs URP (`Unity.RenderPipelines.Universal.Runtime` in an asmdef) |
| `scripts/Shaders/*.shader` | e.g. `Assets/ParticleAtlas/Shaders/` | `ParticleVfx/Atlas AB Add`, `ParticleVfx/Legacy Additive`, `ParticleVfx/Legacy Alpha Blended`, `ParticleVfx/Legacy AB Add` |
| `scripts/Editor/*.cs` | an `Editor` folder or editor-only asmdef | All wrapped in `#if UNITY_EDITOR` |
| `scripts/run.sh`, `scripts/render_compare.cs`, `scripts/audit_unoptimized.py` | keep in the skill, run from the project root | |

Add `ParticleVfx/Atlas AB Add` to Always Included Shaders or make sure an atlas material references it in a build; the native path enables the `_ATLAS_NATIVE` keyword on runtime copies, so the variant must not be stripped (`multi_compile_local`, not `shader_feature`).

Settings live in `ProjectSettings/ParticleAtlasSettings.json` (all optional; defaults shown in `ParticleAtlasSettings.cs`): `vfxRoot`, `atlasFolder`, `materialFolder`, `backupRoot`, `texturePolicyPath`, `halfResolution` (true), `defaultBudget` (2), `keepNativeMeshVertexCount` (256), `requireGammaColorSpace` (true), `compressedPlatforms` + `platformFormat` (Android/iPhone ASTC 8x8), `removeObjectsWithShaders`, `packableShaders`. Read [references/packing-rules.md](references/packing-rules.md) before changing `packableShaders` or turning off `requireGammaColorSpace`.

## Tools

| Need | Menu | API (`ParticleVfx.Atlas.Editor`) |
|---|---|---|
| Is a packed VFX out of date? | Assets > Particle Atlas > Check Packed VFX · Tools > Particle Atlas > Check All Packed VFX | `ParticleAtlasChecker.Check(paths, includeAtlas)` |
| Edit a packed VFX | Assets > Particle Atlas > Unpack For Edit | `ParticleAtlasEditing.Unpack(path)` |
| Rebuild after edits / pack one prefab | Assets > Particle Atlas > Repack | `ParticleAtlasEditing.PlanRepack(paths, key)` → `PlanError(plan, paths)` → `ParticleAtlasOptimizer.Optimize(plan)` |
| Pack many new VFX | Tools > Particle Atlas > Optimizer | `ParticleAtlasOptimizer.Scan(paths, budget, key)` → `Optimize(plan)` |
| Convert blocking materials first | Optimizer window: preparation buttons | `ParticleAtlasPreparation.Scan(paths)` → `Describe(plan)` → `Apply(plan)` |
| Undo a run | Optimizer window: Restore last run | `ParticleAtlasOptimizer.Restore("<backupRoot>/<run>")` |
| Find unpacked VFX | — | `python -I <skill>/scripts/audit_unoptimized.py <folder> <out.tsv> [--referenced]` |

- **Atlas key**: an existing packed prefab keeps the key of its material; a new one gets `Vfx_<folder path>` unless you pass a key (e.g. one atlas per character). Every prefab that already uses the atlas material is repacked with it; only their UVs change.
- Every `Optimize` / `Apply` writes a backup under `Library/ParticleAtlas/Backups/` (restorable until a file changes again) and rolls back on any validation failure. `BuildAtlas` writes a source manifest (texture GUID, content hash, region) into the atlas `.png.meta` `userData`; the checker uses it to detect repainted or swapped textures.
- `Optimize` validates: atlas size matches the preview, particle simulation YAML (`!u!198` documents) unchanged byte for byte, renderer count equals the plan and fits the budget, all UV regions inside 0..1, and sampled meshes at 0.1/0.3/0.8 s are finite. It does not prove visual parity: always render-compare.

## Workflows

**A packed VFX was edited** (particles added/removed, material or texture changed):
1. `git diff --stat` the prefab(s); run `Check(paths, true)` on them.
2. Findings → `PlanRepack(paths)` → read `ParticleAtlasOptimizer.Report(plan)` (renderers before → after, atlas size) → `PlanError` must be null → `Optimize(plan)`.
3. Re-run `Check` on the whole folder: expect 0 findings besides `Untracked`.
4. Render-compare (below), then list which other prefabs changed (UV only).

Finding kinds and their fixes: [references/checker-findings.md](references/checker-findings.md).

**Pack a VFX that was never packed**: `PlanRepack` also handles it and forces single-renderer prefabs in (the optimizer's default selection skips prefabs already within budget). Expect rejections for Animator/Animation, nested or variant prefabs, missing scripts, invalid sorting layers, unsupported shaders, SpriteMask interaction and the renderer budget; see [references/packing-rules.md](references/packing-rules.md).

Before `PlanRepack`, run preparation yourself; `Repack`, `Scan` and `Optimize` never call it:
1. `ParticleAtlasPreparation.Scan(paths)` → read `Describe(plan)`. It converts non-packable `Mobile/Particles/Additive` (tint compensated ×0.5) and other listed legacy shaders to `ParticleVfx/Legacy *`, and deletes objects using shaders in `removeObjectsWithShaders` (e.g. screen distortion).
2. Keep only the materials that actually block packing. Converting a material that is already packable, or that other atlases already packed, is `MaterialChanged` drift in **every other atlas that uses it**. Edit `plan.materials` before `Apply`, or `git checkout` the extra `.mat` afterwards.
3. `Apply(plan)` edits the shared material for all consumers (backup written). Then run `Check(<every consumer>, false)`, expect 0 findings, and render-compare a few consumers that are not in the atlas.

**Renderer budget** (`groups + native > budget`, budget 1..3): groups = distinct (sorting layer, GameObject layer) pairs among sources; every other enabled renderer, including dense mesh-mode particles kept native, counts as native. Fix the prefab rather than skip it, e.g. move a ground ring to the same sorting layer as the rest while keeping its relative order.

**Audit unpacked VFX**: run `audit_unoptimized.py <folder> <out.tsv>`; pass `--atlas-shader` / `--supported` when the project uses other shader names. Status: `should pack` (> 2 live renderers, ≥ 2 non-atlas), `optional (little to merge)` (the optimizer skips prefabs within budget by design), `needs review` (unsupported shaders, Animator/Animation, nested prefab). It is a YAML-level estimate: confirm with `ParticleAtlasOptimizer.Scan(paths, 3)` before packing; expect a large share to still be rejected (SpriteMask, custom vertex streams, nested prefabs, budget). `--referenced` adds GUID references from outside the folder and Addressables entries; no reference is not proof of unused (string keys, dynamic loads). Pack per atlas key so each group gets its own backup.

**Decide whether to pack at all**:
- Atlases are built at half resolution (`halfResolution: true`). Small pixel-art sprites (e.g. an 80×51 icon) visibly blur and bleed at odd sizes: keep those native.
- A prefab used only under a Coffee `UIParticle` gains nothing: `UIParticle` bakes each renderer with its own material, so `ParticleAtlasBatchRenderer` stands down when it finds one in its parents (detected by type name, no package reference). Without that, the native path swaps materials and shows white squares.
- An Animator that only swaps sprites blocks packing. Replace it with a ParticleSystem Texture Sheet Animation (Grid, `timeMode = FPS`, `scalingMode = Hierarchy`, size = sprite bounds) and its own material; it then batches across instances with no Animator.

## Driving Unity from the terminal

Use `sh <skill>/scripts/run.sh <snippet.cs> [timeout]` from the project root with the Editor open and not in Play mode (the render compare is the exception). It runs `unity command eval_file` (see `unity-cli`) and prints `Temp/particle-atlas/out.txt` (`PARTICLE_ATLAS_OUT_DIR` overrides the folder). Snippet rules:
- No `using` directives; fully qualify (`UnityEditor.AssetDatabase`, `ParticleVfx.Atlas.ParticleAtlasBatchRenderer`).
- `internal` members need reflection.
- Write the result to `Temp/particle-atlas/out.txt`, then `return "done";`. The eval HTTP reply times out after a few seconds but the snippet keeps running on the main thread; the runner polls the file.
- `EditorApplication.delayCall` does not run while Unity is unfocused: do the work synchronously.
- After adding or editing editor scripts: `AssetDatabase.Refresh()` via eval, `unity command recompile`, poll `recompile_status` until `"completed"`.

```csharp
var paths = new[] { "Assets/VFX/Hero/Knight/vfx_knight_skill_2_aura.prefab" };
string r;
try
{
    var plan = ParticleVfx.Atlas.Editor.ParticleAtlasEditing.PlanRepack(paths, "Knight");
    var error = ParticleVfx.Atlas.Editor.ParticleAtlasEditing.PlanError(plan, paths);
    r = ParticleVfx.Atlas.Editor.ParticleAtlasOptimizer.Report(plan) + "\n"
        + (error ?? "Backup: " + ParticleVfx.Atlas.Editor.ParticleAtlasOptimizer.Optimize(plan));
    foreach (var i in ParticleVfx.Atlas.Editor.ParticleAtlasChecker.Check(paths, true)) r += "\n" + i;
}
catch (System.Exception e) { r = "EXC " + e; }
System.IO.Directory.CreateDirectory("Temp/particle-atlas");
System.IO.File.WriteAllText("Temp/particle-atlas/out.txt", r);
return "done";
```

## Verifying visually

Use `scripts/render_compare.cs`; inputs, outputs and the three rules (render in Play mode, force alpha to 1, fix random seeds) are in [references/verification.md](references/verification.md). Old prefabs come from `git show HEAD:<path>` or a backup `.bin` copied under `Assets/` temporarily: **delete them afterwards**, or the next scan treats them as atlas consumers.

## Before committing

- Commit the prefab(s), every other prefab the repack touched, and the atlas `.png`, `.png.meta` (holds the source manifest) and `M_<key>_AB_Add.mat`. A prefab committed without its atlas renders garbage UVs for teammates.
- Leave out unrelated assets Unity re-serialized while you worked (fonts, settings). Never commit `Library/` backups or `Temp/`.

## Common mistakes

| Mistake | Result | Fix |
|---|---|---|
| Edit a packed prefab or its material, skip Repack | Old look in game, `MaterialChanged` / `NotPacked` / `EnabledSource` | Check → Repack |
| Run preparation on every material it lists | Drift in other atlases sharing the material | Convert only blockers; Check all consumers |
| Compare in Edit mode | CPU mesh path differs from what players see | Render in Play mode |
| Judge raw `ReadPixels` PNGs | Legacy cells look washed out (alpha channel), false regression | Force alpha = 1 |
| Leave old prefab copies under `Assets/` | They join the atlas group on the next scan | Delete after comparing |
| Pack tiny pixel-art sprites | Blur and bleed at half resolution | Keep native |
| Pack a prefab used only under `UIParticle` | No gain; atlas renderer stands down | Leave native |

## References

- [references/packing-rules.md](references/packing-rules.md): read when a prefab is rejected, when changing settings or packable shaders, or when working in Linear color space.
- [references/checker-findings.md](references/checker-findings.md): read when `Check` reports findings.
- [references/verification.md](references/verification.md): read before render-comparing or when a packed VFX looks different in game.
- [references/batch-renderer-design.md](references/batch-renderer-design.md): read when debugging runtime rendering, sorting, memory, or extending the shader.

## Related skills

- particle-vfx-authoring: design a new effect; pack it here afterwards.
- unity-particle-system: Shuriken modules, scripting and pooling.
- manage-sprite-atlas: sprite atlases for SpriteRenderer/UI, not particles.
- unity-cli: `unity command eval_file`, recompile and Editor connection.
- unity-texture-import: source texture import settings and compression.
