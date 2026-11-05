# Checker findings

`ParticleAtlasChecker.Check(paths, includeAtlas)` only looks at prefabs that contain a `ParticleAtlasBatchRenderer`. `includeAtlas: true` also compares each atlas with its build manifest (slower: it lists every prefab's dependencies once). A save watcher (`AssetPostprocessor`) runs the fast check on every imported prefab under `vfxRoot` and logs a warning.

| Kind | Meaning | Fix |
|---|---|---|
| `Unpacked` | A root-level output is disabled but still lists sources: mid-edit after Unpack For Edit. Other checks are skipped | Finish editing, Repack |
| `DeletedSource` | A packed particle's renderer was deleted | Repack (the source is dropped) |
| `NotPacked` | An enabled particle or trail draws natively outside the atlas, usually added after packing | Repack |
| `EnabledSource` | A packed source renderer was re-enabled: it draws twice | Repack (or disable it) |
| `Sorting` | A source moved to another sorting layer / GameObject layer than its output, or the output order no longer equals the highest source order | Repack |
| `MaterialChanged` | Re-describing the source from its current material differs from the snapshot: tiling, filter/wrap, blend, tint | Repack. If preparation changed a shared material, every atlas using it reports this |
| `UnsupportedMaterial` | The current material is not packable any more, or textures cannot be listed | Restore the material or unpack and keep it native |
| `AtlasLayout` | A source now uses a texture whose GUID is not at its packed region, or the group's texture set changed (added / no longer used) | Repack |
| `TextureEdited` | A source texture's content hash differs from the manifest: repainted after packing | Repack |
| `Untracked` | The atlas has no source manifest in `.png.meta` `userData` (built by another tool or stripped) | Repack once to start tracking; not an error by itself |

After a repack, re-run `Check` on the whole folder: expect 0 findings other than `Untracked`.
