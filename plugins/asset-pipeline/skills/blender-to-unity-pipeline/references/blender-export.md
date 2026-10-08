# Blender FBX export settings for Unity

Read when exporting from Blender (File > Export > FBX), writing an export preset, or debugging scale, axis, leaf-bone or missing-animation problems after import. Labels match Blender 4.x's FBX exporter.

Save the settings below as an operator preset (the `+` next to "Operator Presets" in the export dialog) and share it with the team so every export is identical.

## Include

| Setting | Value | Why |
|---|---|---|
| Limit to: Selected Objects | On | Exports only what you selected; hidden helpers, cameras, reference meshes stay out |
| Limit to: Visible Objects / Active Collection | Optional | Use Active Collection when each asset has its own collection |
| Object Types | Armature, Mesh, (Empty for sockets) | Leave Camera, Lamp, Other off |
| Custom Properties | Off unless used | When on, read them in `OnPostprocessGameObjectWithUserProperties` |

## Transform

| Setting | Value | Why |
|---|---|---|
| Scale | 1.00 | |
| Apply Scalings | **FBX All** | Bakes Blender unit scale into the file so Unity sees scale 1 instead of 100 / 0.01 |
| Forward | **-Z Forward** | Blender's -Y facing becomes Unity's +Z facing |
| Up | **Y Up** | |
| Apply Unit | On | |
| Use Space Transform | On | |
| Apply Transform | Off by default; On only for static meshes | Experimental. Bakes the axis conversion into mesh data so children do not get X -89.98, but breaks armatures and animation. For rigs, leave off and enable **Bake Axis Conversion** in Unity |

Scene unit: keep Scene Properties > Units > Unit Scale 1.0, Length Metric/Meters. Changing Unit Scale (for example to 0.01 to model in cm) multiplies through Apply Scalings and is the usual root cause of "everything is 100x" reports.

## Geometry

| Setting | Value | Why |
|---|---|---|
| Smoothing | **Face** (or Normals Only) | Writes smoothing data so Unity does not warn "mesh has no smoothing groups" and keeps hard edges. Set Unity Normals to Import |
| Export Subdivision Surface | Off | Apply the level you want in Blender |
| Apply Modifiers | On | Mirror, Solidify, Weighted Normal, etc. are baked. Armature modifier is ignored (skinning is exported separately) |
| Loose Edges | Off | |
| Triangulate Faces | Optional | On gives the exact triangulation you saw in Blender (important for baked normal maps). Off lets Unity triangulate; enable Keep Quads in Unity only for tessellation |
| Tangent Space | Off | Let Unity calculate Mikktspace tangents, same as Blender/Substance bakers |
| Vertex Colors | sRGB or Linear, matching the shader | |

Blender 4.1+ removed mesh Auto Smooth. Use the **Smooth by Angle** modifier (and Weighted Normal if wanted); with Apply Modifiers on, custom normals export and Unity's Normals `Import` keeps them.

## Armature

| Setting | Value | Why |
|---|---|---|
| Primary Bone Axis / Secondary | Y / X (defaults) | Changing them rotates bone local axes and breaks Humanoid mapping |
| Armature FBXNode Type | Null | |
| Only Deform Bones | On for game rigs | Drops IK / control bones. Off if gameplay needs a control bone transform |
| **Add Leaf Bones** | **Off** | Otherwise every chain gets an extra `_end` bone that shows up in Unity, clutters Avatar mapping and adds transforms |

Before export:

- Armature object at origin, rotation 0, scale 1 (Ctrl+A > All Transforms, then re-check skinning).
- Rest pose is a T-pose (or A-pose) for Humanoid; Unity's Avatar Configure can enforce T-pose but starting close avoids muscle range errors.
- One armature per FBX. Name the root bone (for example `Root`) and parent the hips to it if you want root motion driven by a dedicated bone.
- Bone names stable across all animation files; Unity binds curves by transform path.

## Bake Animation

| Setting | Value | Why |
|---|---|---|
| Bake Animation | On | |
| Key All Bones | On | Every bone gets keys; avoids bones left at bind pose in clips that do not key them |
| NLA Strips | On if each action is pushed to its own NLA track | Each strip becomes its own take |
| All Actions | On to export every action as a take, Off when using NLA strips only | Leaving both on with stray actions exports junk takes. Delete unused actions or give them no fake user |
| Force Start/End Keying | On | Clip length matches the action range |
| Sampling Rate | 1.00 | |
| Simplify | 0.0 to 1.0 | Higher values lose subtle motion; Unity's Anim. Compression handles size |

Takes are named `ArmatureName|ActionName`. Unity shows them under Animation > Clips; rename the clips in Unity (or in `OnPreprocessAnimation`).

Workflow choices:

- **One FBX per character with all actions**: simple, but every action edit reimports the mesh.
- **Mesh + rig FBX, plus one animation-only FBX per action set**: animation FBXs export the armature only (Object Types: Armature), use Humanoid or Generic with **Copy From Other Avatar**. Scales better for teams and keeps reimports small.

Frame rate: set the scene frame rate (30 or 60) before animating. Unity samples at the file's rate.

## Path Mode and textures

- Path Mode `Copy` with Embed Textures off. Ship textures as separate files into `Assets/Art/.../Textures` and set them up per `unity-texture-import`.
- Embedded textures get extracted by Unity into the model folder and create duplicates.

## glTF 2.0 export (props only)

File > Export > glTF 2.0: Format `glTF Binary (.glb)`, Include Selected Objects, Transform +Y Up on, Data > Mesh Apply Modifiers on, Materials Export, Compression off (Draco requires the matching decoder package in Unity). Import in Unity with `com.unity.cloud.gltfast`.

## Common export problems

| Problem in Unity | Fix in Blender |
|---|---|
| Model is tiny or 100x | Apply Scalings FBX All; Unit Scale 1.0; apply object scale |
| Children rotated -89.98 | Unity Bake Axis Conversion, or Apply Transform for static meshes |
| `_end` bones | Add Leaf Bones off |
| Animation missing or only one clip | Bake Animation on; All Actions on or NLA Strips with actions pushed down |
| Extra garbage clips | Delete unused actions; clear fake users; or turn All Actions off and use NLA |
| Character faces backwards | Model must face -Y in Blender with Forward -Z |
| Faceted or wrong shading | Smoothing Face; Smooth by Angle modifier; Unity Normals Import |
| Normal map seams | Triangulate on export to match the bake; Unity tangents Calculate Mikktspace |
| Animation drifts or scales | Unapplied armature scale; apply transforms on the armature before skinning |
