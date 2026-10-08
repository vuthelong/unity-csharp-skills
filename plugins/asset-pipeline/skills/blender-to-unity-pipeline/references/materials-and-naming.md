# Materials, textures and naming

Read when a Blender model imports with pink, duplicated or wrong materials, when setting up remapping to project URP Lit materials, or when defining naming rules for meshes, colliders, LODs and textures.

## Materials tab

| Inspector | API | Recommended |
|---|---|---|
| Material Creation Mode | `materialImportMode` (`ModelImporterMaterialImportMode.None/ImportStandard/ImportViaMaterialDescription`) | `ImportViaMaterialDescription` while authoring; `None` for models whose prefab assigns materials explicitly |
| sRGB Albedo Colors | `useSRGBMaterialColor` | On (Linear color space projects) |
| Location | `materialLocation` (`ModelImporterMaterialLocation.InPrefab` = Use Embedded Materials, `External` = legacy external) | InPrefab, then remap |
| Naming | `materialName` (`ModelImporterMaterialName.BasedOnMaterialName`...) | Based On Material Name |
| Search | `materialSearch` (`ModelImporterMaterialSearch.Local/RecursiveUp/Everywhere`) | Recursive-Up or Everywhere with unique names |
| Remapped Materials | `AddRemap`, `RemoveRemap`, `GetExternalObjectMap` | One entry per Blender material slot |

With URP installed, `ImportViaMaterialDescription` creates **Universal Render Pipeline/Lit** materials through URP's material description preprocessor, filling base color, normal, metallic and emission from what the FBX carries. FBX holds only a Phong-like subset of Blender's Principled BSDF, so treat these as placeholders.

## Recommended flow: project materials plus remap

1. In Blender, name material slots exactly like the Unity material you want (`M_Rock`, `M_Bark`). One material per distinct surface; avoid `Material.001`.
2. In Unity, create the URP Lit (or custom Shader Graph) materials once in `Assets/Art/Materials`, named the same.
3. Import the model. Either run **Assets > Model Import > Remap Materials By Name** (calls `SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere)`), or click Search and Remap in the Materials tab.
4. The remaps are stored in the model's `.meta`. Reimports keep them; new slots show up unmapped.

Remapping a slot in code (menu item, not inside an import callback):

```csharp
var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_Rock");
importer.AddRemap(id, AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_Rock.mat"));
importer.SaveAndReimport();
```

`ModelImporter.sourceMaterials` is internal; to list slot names, load the model's sub-assets with `AssetDatabase.LoadAllAssetRepresentationsAtPath(modelPath)` and collect the `Material` entries, or read `GetExternalObjectMap()` for the ones already remapped.

### Extraction

- **Extract Materials...** writes every embedded material to `.mat` files and remaps to them. Use once for a hero asset you will hand-tune; avoid for kits (one material copy per model).
- **Extract Textures...** (`ExtractTextures(folderPath)`) only matters when textures were embedded in the FBX. Prefer exporting textures as files (Path Mode Copy, Embed off).

### Converting existing materials to URP

- Pink materials after import mean the material uses a Built-in shader (`Standard`). Run **Window > Rendering > Render Pipeline Converter** > Material Upgrade, or reassign to URP Lit. Full migration: `migrate-birp-to-urp`.
- Materials created by `ImportViaMaterialDescription` in a URP project are already URP Lit.

## Texture naming and channel packing for URP Lit

| Suffix | URP Lit slot | Content | Import |
|---|---|---|---|
| `_BaseColor` (`_Albedo`) | Base Map | RGB color, A alpha | sRGB on |
| `_Normal` | Normal Map | Tangent-space OpenGL (Y+), as exported by Blender | Texture Type Normal map |
| `_MetallicSmoothness` | Metallic Map | R metallic, A smoothness (1 - roughness) | sRGB off |
| `_AO` | Occlusion Map | G channel used | sRGB off |
| `_Emission` | Emission Map | RGB | sRGB on |
| `_Mask` | custom Shader Graph | R metallic, G AO, B detail mask, A smoothness (HDRP-style mask) | sRGB off |

Blender works in roughness; URP Lit wants smoothness in the alpha of the metallic map. Invert roughness when baking or packing (Substance's URP export preset does this). Use `T_<Asset>_<Suffix>` names, for example `T_Rock_Normal`, so a texture postprocessor can set the type from the suffix. Import settings, compression and the folder-based texture postprocessor: `unity-texture-import`.

## Object naming rules

| Pattern | Meaning |
|---|---|
| `<Name>_LOD0`, `<Name>_LOD1`... | LOD levels, siblings under one parent. Unity builds the `LODGroup` itself. LOD0 must exist; levels must be contiguous |
| `<Name>_COL` | Non-convex mesh collider source, renderer removed by `ModelImportRules` |
| `UCX_<Name>` / `UCX_<Name>_NN` | Convex collider source (max 255 triangles for PhysX convex), renderer removed |
| `socket_*` / `fx_*` | Empty attachment points; expose them when Optimize Game Objects is on |
| `SM_<Name>.fbx` / `SK_<Name>.fbx` / `A_<Name>_<Action>.fbx` | Static mesh, skinned mesh, animation-only file. Lets folder rules and searches tell them apart |

Non-convex `MeshCollider`s cannot be used on non-kinematic Rigidbodies; use `UCX_` (convex) for dynamic props and keep `_COL` for static environment. Collider authoring at runtime and physics settings: `physics-3d-collision`.
