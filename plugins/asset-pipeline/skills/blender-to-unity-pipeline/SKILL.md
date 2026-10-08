---
name: blender-to-unity-pipeline
description: Sets up and audits the Blender to Unity 6 URP model pipeline. Covers Blender FBX export (Apply Scalings FBX All, Forward -Z / Up Y, Apply Transform caveats, Selected Objects, Add Leaf Bones off, Bake Animation with NLA strips / actions, smoothing), glTF via com.unity.cloud.gltfast, why .blend import breaks CI, ModelImporter settings (scale, Convert Units, Bake Axis Conversion, mesh compression, Read/Write, normals/tangents, blend shapes, lightmap UVs), Generic vs Humanoid rigs, Avatar copy-from-other, retargeting, Optimize Game Objects, clip splitting, loop pose, root motion and Bake Into Pose, events, ModelImporter.clipAnimations, material remapping to URP Lit, _LODn and collider naming, and an AssetPostprocessor plus Presets enforcing import rules by folder. Use when the user imports FBX/GLB/.blend models, sees scale 100 or -89.98 rotations, broken rigs, pink or duplicated materials, or wants import settings automated.
license: MIT
metadata:
  category: asset-pipeline
  sources: "docs.unity3d.com/6000.0/Documentation/Manual/FBXImporter-Model.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/ModelImporter.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetPostprocessor.html, docs.unity3d.com/6000.0/Documentation/Manual/Presets.html, github.com/Unity-Technologies/UnityCsReference/Modules/AssetPipelineEditor/Public/ModelImporting, docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.20, docs.blender.org/manual/en/latest/addons/import_export/scene_fbx.html"
  unity: "6000.0+"
---

# Blender to Unity pipeline

Target: Unity 6 URP, characters and props authored in Blender, FBX as the interchange format. glTF (`.glb`) is a valid alternative for static props only.

## Workflow

1. **Decide the format per asset class.**
   - Rigged/animated characters, anything Humanoid, anything that needs `ModelImporter` features: **FBX**.
   - Static props where you want PBR materials to come across automatically: FBX or **glTF via `com.unity.cloud.gltfast`**. glTFast imports through its own `ScriptedImporter`, so `ModelImporter` settings, Avatars and `OnPreprocessModel` do not apply, and skinned animation only plays through the legacy `Animation` component.
   - Never commit `.blend` files under `Assets/` (see "Direct .blend import").
2. **Export from Blender** with the settings in [references/blender-export.md](references/blender-export.md). The minimum: Apply Scalings `FBX All`, Forward `-Z Forward`, Up `Y Up`, Apply Unit on, Use Space Transform on, Add Leaf Bones off, Selected Objects on, Smoothing `Face`.
3. **Drop the file into the folder that matches its class** (`Assets/Art/Characters`, `Props`, `Environment`, `Animations`). The postprocessor in [scripts/Editor/ModelImportRules.cs](scripts/Editor/ModelImportRules.cs) applies the folder's `ModelImport.preset` on first import, then enforces the folder rule on every import.
4. **Check the import** in the Inspector Model / Rig / Animation / Materials tabs. Per-property guidance: [references/model-importer.md](references/model-importer.md).
5. **Rig and animation**: choose Generic or Humanoid, create or copy the Avatar, split clips, set loop and root motion. Details: [references/rig-and-animation.md](references/rig-and-animation.md).
6. **Materials**: extract or remap to project URP Lit materials. Textures follow `unity-texture-import`. Details: [references/materials-and-naming.md](references/materials-and-naming.md).
7. **Make a prefab** from the model (Prefab Variant of the model asset), add components there, never on the model asset itself.
8. Run the validation checklist at the end of this file.

## Coordinate systems and scale

| | Blender | FBX file | Unity |
|---|---|---|---|
| Handedness | Right | Right | Left |
| Up | +Z | per export setting | +Y |
| Character faces | -Y (Front view, numpad 1) | | +Z |
| Unit | 1 BU = 1 m (Unit Scale 1.0) | cm by convention | 1 unit = 1 m |

With Forward `-Z` / Up `Y`, Blender's -Y facing becomes Unity's +Z facing. Everything below is about getting **scale 1, rotation 0** on the root and children in Unity.

### The two classic symptoms

| Symptom in Unity | Cause | Fix |
|---|---|---|
| Root or children have scale `100` (or `0.01`) | Apply Scalings left at `All Local` | Blender: Apply Scalings `FBX All`. Unity: keep Scale Factor 1, Convert Units on |
| Mesh children have rotation X `-89.98` | Axis conversion stored on objects instead of in the data | Unity: **Bake Axis Conversion** on, or Blender: **Apply Transform** (experimental, static meshes only) |

Prefer Unity's `Bake Axis Conversion` for rigged models: Blender's `Apply Transform` is flagged experimental and is known to break armatures and baked animation.

Also apply object transforms in Blender before export (`Ctrl+A` > All Transforms) on static meshes, and on the armature object before skinning. Unapplied scale on an armature is the most common source of animations that drift or scale at runtime.

## Direct .blend import

Unity can import `.blend` files by launching Blender in the background to convert them to FBX on import. Do not rely on it:

- Every machine that imports the project, including CI, build agents and teammates who only touch code, must have a compatible Blender installed and discoverable. Without it the asset imports as nothing and every reference breaks.
- The conversion uses Unity's bundled FBX export script with fixed settings: no control over Apply Scalings, leaf bones, NLA baking or smoothing.
- Import is slow and reruns whenever the `.blend` changes or the Library is rebuilt.
- `.blend` files carry everything (hidden objects, high-poly sources, reference images) into the project.

Keep `.blend` sources outside `Assets/` (or in a folder ending with `~`, which Unity ignores) and export FBX into `Assets/`.

## glTF with glTFast

- Install `com.unity.cloud.gltfast` from the Unity registry (6.x supports Unity 6). It registers as the default importer for `.gltf` / `.glb`. If another package also claims the extension, you get "Multiple scripted importers are targeting the extension"; add `GLTFAST_FORCE_DEFAULT_IMPORTER_OFF` to make glTFast an alternative importer instead.
- Export from Blender with File > Export > glTF 2.0, format `glTF Binary (.glb)`, +Y Up on (default). No scale or axis fix-ups are needed: glTF is metres and Y-up by spec.
- Materials map to glTFast's Shader Graphs for URP, not to URP Lit. For runtime-loaded glTF, include the needed shader variants in the build (see glTFast Project Setup docs).
- Animation imports as legacy clips. Mecanim-compatible clips can be generated but are not assigned and need extra work. Use FBX for anything played by an Animator.
- Copy `.bin` and texture files of a `.gltf` together with it, keeping relative paths.

## Enforcing import settings by folder

Two layers, both checked in:

1. **Presets** (`Assets > Create > Preset` from a configured FBX's importer, or the preset icon in the Inspector). Either register them in **Project Settings > Preset Manager** with a filter such as `glob:"Assets/Art/Props/**"`, or place one named `ModelImport.preset` in the folder for the script below to find. Default presets apply only when an asset is first imported; they never touch existing assets.
2. **`AssetPostprocessor`** for rules that must always hold and for logic presets cannot express (clip loop flags by name, collider generation, LOD validation).

[scripts/Editor/ModelImportRules.cs](scripts/Editor/ModelImportRules.cs) does both:

- `OnPreprocessModel`: on first import only (`assetImporter.importSettingsMissing`) applies the nearest `ModelImport.preset`, walking up from the asset's folder. This covers per-folder defaults a rule cannot express, such as `CopyFromOther` with a specific source Avatar for `Animations/`. Then, on every import, it enforces the folder rule: scale, axis baking, Read/Write off, cameras/lights off, normals/tangents, rig type, Avatar mode, Optimize Game Objects, lightmap UVs, blend shapes, mesh compression, material import mode.
- `OnPreprocessAnimation`: on first setup, builds `clipAnimations` from `defaultClipAnimations` and strips the `Armature|` prefix from the names. On every import, it sets `loopTime` for clips whose name ends in `_Loop` or contains a loop keyword (Idle, Walk, Run...). Loop Pose is left to you, because it depends on whether Loop Match is green.
- `OnPostprocessModel`: turns `*_COL` children into `MeshCollider`s and `UCX_*` children into convex ones (renderer removed), and warns when `_LOD1+` meshes exist without `_LOD0`.
- `GetVersion()` returns a constant; bump it after editing the rules so Unity reimports affected models.
- Menu **Assets > Model Import > Remap Materials By Name** runs `SearchAndRemapMaterials` on the selected models.

Editing a preset does not change models that are already imported. To push it to them, select the models and apply the preset from the Inspector's preset icon. That also overwrites the clip and material-remap settings stored in the preset, so re-check those afterwards.

Edit the `Rules` table at the top to match your folder layout. Keep the script in an `Editor` folder or an Editor-only asmdef (see `csharp-unity`). Background on writing importers safely: `unity-editor-safety`.

Rules for postprocessors:

- Never call `AssetDatabase.ImportAsset`, `SaveAndReimport` or `CreateAsset` from inside an import callback; it recurses or is ignored. Change `assetImporter` in preprocess, change the produced GameObject in postprocess.
- If a rule reads another asset on every import (for example a settings asset), declare it with `context.DependsOnSourceAsset` / `context.DependsOnArtifact`, or cached imports go stale.
- Report problems with `context.LogImportWarning` / `LogImportError` so they show on the asset, not just the Console.
- Bumping `GetVersion()` reimports every model the postprocessor touches; on large projects schedule it.

## Naming conventions (Blender object names)

| Name | Unity result |
|---|---|
| `Rock_LOD0`, `Rock_LOD1`, `Rock_LOD2` siblings under one parent | Unity adds a `LODGroup` to the parent automatically, levels in suffix order |
| `Rock_COL` | `MeshCollider` (non-convex), renderer removed (by `ModelImportRules`) |
| `UCX_Rock`, `UCX_Rock_01` | Convex `MeshCollider`, renderer removed (by `ModelImportRules`) |
| `socket_hand_R`, `fx_muzzle` | Empty objects kept as attachment points; list them in Extra Transforms to Expose when Optimize Game Objects is on |
| Material `M_Rock` | Remaps to `M_Rock.mat` with Remap Materials By Name |

Unity has no built-in collider naming convention; only `_LODn` is native. LOD transition heights and cross-fade: `level-geometry-authoring`. Unity 6.2+ can also generate Mesh LODs inside a single mesh (Model tab `Generate Mesh LODs`, `ModelImporter.generateMeshLods`); use either that or `_LODn` meshes, not both on the same asset.

## Validation checklist

Model tab
- [ ] Root and children: position 0, rotation 0, scale 1 (no `-89.98`, no `100`).
- [ ] A 2 m Blender character measures about 2 units (compare against a default Cube).
- [ ] Read/Write off unless runtime code reads mesh data (CPU copy doubles memory).
- [ ] Import Cameras / Lights / Visibility off.
- [ ] Normals `Import` (smoothing came from Blender), Tangents `Calculate Mikktspace` when normal maps are used.
- [ ] Blend Shapes on only for meshes that have shape keys.
- [ ] Generate Lightmap UVs on for static environment meshes that will be baked (see `unity-lighting`), or a second UV authored in Blender.
- [ ] `_LOD0..n` produced a LODGroup; colliders produced by naming have no renderer.

Rig tab
- [ ] Correct Animation Type (None for props).
- [ ] Humanoid: Avatar Configure shows all required bones green, T-pose enforced, no muscle errors.
- [ ] Animation-only FBX copies the character Avatar (`Copy From Other Avatar`).
- [ ] Optimize Game Objects on for characters, with sockets listed as exposed transforms.
- [ ] No `_end` leaf bones in the hierarchy.

Animation tab
- [ ] One clip per action, named, frame ranges correct, no stray NLA tracks.
- [ ] Loop Time on cycles; Loop Match lights green (Loop Pose on when it is not).
- [ ] Root Transform Rotation / Y / XZ Bake Into Pose set deliberately (see rig reference).
- [ ] Events present where gameplay needs them (footsteps, hit frames).

Materials tab
- [ ] Either remapped to project materials or `None`; no duplicated `Standard` / `Lit` materials per model.
- [ ] No pink materials in URP.

Prefab
- [ ] Gameplay components live on a Prefab Variant of the model, not on the model.
- [ ] Animator uses the model's Avatar; Apply Root Motion matches the clip setup.

## Related skills

- `animation-authoring`: AnimatorController, blend trees, runtime Animator, root motion handlers.
- `level-geometry-authoring`: LODGroup tuning, ProBuilder, terrain.
- `unity-texture-import`: texture import settings for exported maps (normal maps, masks, compression).
- `unity-lighting`: lightmap UVs, static flags, baking.
- `addressables-asset-loading`: shipping imported prefabs as Addressables.
- `csharp-unity`: code style for the postprocessor.
