---
name: level-geometry-authoring
description: Generates and edits level geometry by script in Unity 6 - Terrain (TerrainData heightmaps, SetHeights / GetHeights, TerrainLayer splat painting with SetAlphamaps, tree and detail placement, holes, neighbor stitching, SampleHeight), ProBuilder meshes (com.unity.probuilder ShapeGenerator, ProBuilderMesh, Extrude, DeleteFaces, SetMaterial, ToMesh / Refresh), and LODGroup setup (SetLODs, transition heights, cross-fade). Use when the user wants a procedural or scripted terrain, hills, rivers or flattened areas, slope-based texturing, tree scattering, multi-tile terrain worlds, greybox / blockout geometry, platforms, ramps and stairs, or LOD levels, or reports terrain seams between tiles, a TerrainCollider not matching the visual, heights written in the wrong axis, pink terrain in URP, ProBuilder edits not showing, or LODs popping.
license: MIT
metadata:
  category: gameplay-systems
  sources: "AlexeyPerov/Unity-Open-MCP/skills/extensions/terrain, AlexeyPerov/Unity-Open-MCP/skills/extensions/probuilder, AlexeyPerov/Unity-Open-MCP/skills/extensions/constraints"
  unity: "6000.0+"
---

# Level Geometry Authoring

Three tools, one goal (playable space): **Terrain** for large organic ground, **ProBuilder** for blockout and architectural pieces, **LODGroup** for distance-based mesh swapping.

## Workflow

1. **Inspect** existing terrains (`Terrain.activeTerrains`, each `terrainData` resolution, size, layers, prototypes) or ProBuilder objects (`ProBuilderMesh.faceCount`, face normals) before changing anything.
2. **Record Undo** in Editor scripts: `Undo.RegisterCompleteObjectUndo(terrainData, ...)` for terrain (heightmaps are large; one undo record per operation), `Undo.RecordObject(proBuilderMesh, ...)` for ProBuilder.
3. **Write in bounded regions.** Edit heightmaps and alphamaps in tiles (e.g. 513 x 513 or smaller) rather than whole 4K arrays, so each step is fast and reviewable.
4. **Save**: TerrainData and TerrainLayers are assets; create them with `AssetDatabase.CreateAsset` or they live only in the scene. ProBuilder data is serialized on the component in the scene/prefab.
5. **Verify** in the Scene view and with gameplay queries (`terrain.SampleHeight`, raycasts against the collider).

Code and coordinate details: [references/terrain-api.md](references/terrain-api.md) (read before any Terrain script), [references/probuilder-and-lod.md](references/probuilder-and-lod.md) (read before ProBuilder or LODGroup scripts).

## Terrain rules

- **Set `heightmapResolution` before `size`**: changing the resolution rescales `size`. Valid resolutions are 2^n + 1 (33 ... 4097).
- **Heights are normalized** 0..1 of `terrainData.size.y`, and arrays are indexed **`[y, x]`** (row = Z). `SetHeights(xBase, yBase, heights)` takes x first. Swapping axes is the most common bug.
- **Alphamaps** are `float[y, x, layer]`, weights per cell sum to 1, and their resolution differs from the heightmap; convert through normalized coordinates.
- **Trees** use normalized positions (0..1 on X and Z, Y as normalized height). Rotation is in radians. Prototype prefabs need a renderer (MeshRenderer or LODGroup); for collisions they need a root `CapsuleCollider` and the TerrainCollider must have tree colliders enabled.
- **Terrain and TerrainCollider each reference TerrainData.** When swapping data, assign both, or the collider keeps the old shape.
- **World <-> terrain**: `terrain.SampleHeight(worldPos)` returns height relative to the terrain's own Y; add `terrain.transform.position.y`. Normalized coords = `(world - terrain.GetPosition()) / size`.
- **Neighbors** for seamless LOD: `terrain.SetNeighbors(left, top, right, bottom)` (that order), or `allowAutoConnect = true` with a shared `groupingID`. Tiles must share heightmap resolution and edge heights.
- **URP / HDRP**: a pink terrain means the material is a Built-in shader. Assign a material using `Universal Render Pipeline/Terrain/Lit` (or the HDRP equivalent) to `terrain.materialTemplate`.
- **Live editing** many small regions: `SetHeightsDelayLOD` then one `terrainData.SyncHeightmap()`.
- For brush-based sculpting, erosion and noise in the Editor, use the Terrain Tools package (`com.unity.terrain-tools`).

## ProBuilder rules

- After any mesh operation call `mesh.ToMesh()` then `mesh.Refresh()`; otherwise nothing changes on screen.
- Never edit the `MeshFilter.sharedMesh` of a ProBuilder object directly; ProBuilder regenerates it from its own data. To hand a plain mesh to other tools, export it (ProBuilder Export) or strip the ProBuilder component.
- Select faces by normal for robust scripts (top faces = normal close to `Vector3.up`) instead of hard-coded indices.
- Refuse to delete every face; a mesh with zero faces breaks the component.
- Update a `MeshCollider.sharedMesh` after edits, since it does not follow automatically.
- ProBuilder is for blockout and simple architecture. Replace final art with authored meshes; ProBuilder meshes are not optimized for complex detail.

## LODGroup rules

- LOD `screenRelativeTransitionHeight` values must be strictly descending. The last LOD's value is the cull threshold: below it the object is culled. Use a small value (e.g. 0.01-0.02) rather than 0 unless it should never cull.
- Maximum 8 LOD levels. Call `RecalculateBounds()` after `SetLODs`.
- Cross-fade needs shader support (URP Lit supports it; enable **LOD Cross Fade** on the URP asset).
- Unity 6.2+ can also generate per-mesh LODs at import (Mesh LOD), which needs no LODGroup for single meshes.

## Related skills

- `initialize-ai-navigation`: bake NavMesh on terrain and ProBuilder geometry; NavMeshModifier for areas.
- `physics-3d-collision`: TerrainCollider, MeshCollider convexity and queries against level geometry.
- `splines-paths`: roads and rivers along splines, flattening terrain under a spline.
