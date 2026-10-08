# ModelImporter Model tab reference

Read when choosing or scripting Model tab settings (`UnityEditor.ModelImporter`) for FBX from Blender. Inspector label, then the scripting property. Rig, animation and material properties are in [rig-and-animation.md](rig-and-animation.md) and [materials-and-naming.md](materials-and-naming.md).

## Scene

| Inspector | API | Blender FBX value | Notes |
|---|---|---|---|
| Scale Factor | `globalScale` (float) | 1 | Multiplies on top of file scale. Keep 1 and fix scale at export |
| Convert Units | `useFileScale` (bool) | On | Applies the file's unit scale (`fileScale`, read-only). With Apply Scalings FBX All the file scale is 1 m |
| | `useFileUnits` | | Only meaningful for some formats (`isUseFileUnitsSupported`) |
| Bake Axis Conversion | `bakeAxisConversion` | On | Bakes the Z-up to Y-up conversion into vertex and animation data, so roots and children get rotation 0. Turning it on for an already-used model changes child transforms; re-check prefabs and animation bindings |
| Import BlendShapes | `importBlendShapes` | On for meshes with shape keys | Off otherwise; empty blend-shape data still costs import time |
| Import Deform Percent | `importBlendShapeDeformPercent` | Only if animating shape keys from Blender | |
| Import Visibility | `importVisibility` | Off | On imports visibility curves and adds renderers toggled by animation |
| Import Cameras | `importCameras` | Off | |
| Import Lights | `importLights` | Off | Blender light units do not map to URP; place lights in Unity |
| Preserve Hierarchy | `preserveHierarchy` | On for single-mesh files you parent elsewhere | Keeps an explicit root even with one child |
| Sort Hierarchy By Name | `sortHierarchyByName` | On | |

## Meshes

| Inspector | API | Value | Notes |
|---|---|---|---|
| Mesh Compression | `meshCompression` (`ModelImporterMeshCompression.Off/Low/Medium/High`) | Off for skinned characters, Low/Medium for props | Quantizes positions/normals/UVs in the stored asset to shrink build size. Does not reduce runtime memory. High causes visible seams and wobble on skinned meshes |
| Read/Write | `isReadable` | Off | On keeps a CPU copy (double memory). Needed only for runtime mesh reads (`Mesh.vertices`), non-convex MeshCollider baking at runtime from a scaled mesh, `Mesh.CombineMeshes` at runtime, some VFX sampling |
| Optimize Mesh | `meshOptimizationFlags` (`MeshOptimizationFlags.Everything`), or `optimizeMeshPolygons` / `optimizeMeshVertices` | Everything | Reorders for GPU cache. Turn off only if code depends on vertex order (vertex animation textures, blend shape tools) |
| Generate Colliders | `addCollider` | Off | Adds a MeshCollider to every mesh. Use `_COL` / `UCX_` naming instead |
| Index Format | `indexFormat` | Auto | 32-bit only above 65535 vertices |
| Generate Mesh LODs | `generateMeshLods`, `maximumMeshLod`, `meshLodGenerationFlags` | 6.2+ only | Mesh LOD inside one mesh. Do not combine with `_LODn` meshes on the same asset |

## Geometry

| Inspector | API | Value | Notes |
|---|---|---|---|
| Keep Quads | `keepQuads` | Off | On only for tessellation shaders |
| Weld Vertices | `weldVertices` | On | Off only when you need exact Blender vertex count/order |
| Normals | `importNormals` (`ModelImporterNormals.Import/Calculate/None`) | Import | Keeps Blender smoothing and custom normals. Calculate ignores them and uses Smoothing Angle |
| Blend Shape Normals | `importBlendShapeNormals` | Calculate (or Import if exported) | |
| Normals Mode | `normalCalculationMode` | Area And Angle Weighted | Only when Normals = Calculate |
| Smoothness Source | `normalSmoothingSource` | From Smoothing Groups | Requires Blender Smoothing Face/Normals Only |
| Smoothing Angle | `normalSmoothingAngle` | 60 | Only when Calculate |
| Tangents | `importTangents` (`ModelImporterTangents.CalculateMikk`) | Calculate Mikktspace | Matches Blender/Substance bakers. Use None for meshes with no normal map to save memory |
| Swap UVs | `swapUVChannels` | Off | |
| Generate Lightmap UVs | `generateSecondaryUV` | On for baked static meshes without an authored UV2 | Margin method and pack margin: `secondaryUVMarginMethod`, `secondaryUVPackMargin`, `secondaryUVMinLightmapResolution`, `secondaryUVMinObjectScale`. Off for characters and dynamic props. Lightmap setup: `unity-lighting` |

## Read/Write and other memory notes

- Every imported mesh with `isReadable` on keeps vertex data on CPU and GPU. Audit with `ModelImporter.isReadable` across `Assets/Art` in an editor script, or with the Memory Profiler (`memory-snapshot-profiling`).
- Mesh compression shrinks AssetBundles / Addressables builds; runtime memory is the same.
- Vertex Compression in Player Settings is a separate, runtime vertex format setting.
- Blend shapes cost memory per shape per vertex. Strip unused shape keys in Blender.

## Scripting pattern

Change settings in `OnPreprocessModel` (before import), never by `AssetImporter.GetAtPath(...).SaveAndReimport()` inside an import callback. For one-off batch fixes outside import, from a menu item:

```csharp
var importer = (ModelImporter)AssetImporter.GetAtPath(path);
importer.isReadable = false;
importer.meshCompression = ModelImporterMeshCompression.Low;
importer.SaveAndReimport();
```

Wrap batch loops in `AssetDatabase.StartAssetEditing()` / `StopAssetEditing()` (in `try/finally`) so Unity imports once at the end.
