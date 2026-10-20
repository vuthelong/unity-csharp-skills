---
name: spine-unity
description: Integrates Spine 2D skeletal animation into Unity 6 with spine-unity 4.2 or 4.3 and the Spine URP Shaders - install, export settings, texture workflow, SkeletonAnimation / SkeletonGraphic setup, AnimationState tracks, mixing and events, skins, bone following, Spine in a 3D URP scene, pooling, and the 4.2 to 4.3 upgrade. Use when the user mentions Spine, spine-unity, SkeletonAnimation, .skel.bytes or .atlas.txt, or Spine characters rendering wrong.
license: MIT
metadata:
  category: asset-pipeline
  sources: "github.com/EsotericSoftware/spine-runtimes/spine-unity (branch 4.3), github.com/EsotericSoftware/spine-runtimes/spine-csharp/src (branch 4.3), esotericsoftware.com/spine-unity, esotericsoftware.com/spine-unity-download, esotericsoftware.com/spine-unity-assets, esotericsoftware.com/spine-unity-rendering"
  unity: "6000.0+"
---

# spine-unity

Verified against the spine-runtimes `4.3` branch: spine-unity 4.3.114, spine-csharp 4.3.41, URP Shaders 4.3.25. The README states compatibility with Unity 2017.1-6000.4. 4.3 is the current release on the download page; 4.2 is listed under older versions.

## Licensing

The Spine Runtimes are distributed under the Spine Runtimes License Agreement. You may integrate them into a game for free, but anyone who uses the Spine Editor to create or modify the animations needs their own Spine Editor license. Studios distributing software that contains the runtimes to people without a license need a license at the time of integration. Read esotericsoftware.com/spine-runtimes-license before shipping.

## Detect the runtime version first

Many shipping projects are still on 4.2, and 4.2 and 4.3 differ in component layout and core API names. Before writing any code, find the installed version: `Packages/manifest.json` (the `#4.x` suffix or commit of the spine git URLs, or a `com.esotericsoftware.spine.*` version), `Packages/packages-lock.json`, or, for the unitypackage install, `Assets/Spine/version.txt`. Write code for that version only; never mix names. If it cannot be determined, ask.

| Concept | 4.2 | 4.3 |
|---|---|---|
| Components | `SkeletonAnimation : SkeletonRenderer` (one component); `SkeletonGraphic` has its own `AnimationState` | `SkeletonAnimation` + `SkeletonRenderer` (or `SkeletonGraphic`) as two components |
| Current track entry | `state.GetCurrent(i)` | `state.GetTrack(i)` |
| Reset pose | `SetToSetupPose()` / `SetBonesToSetupPose()` / `SetSlotsToSetupPose()` | `SetupPose()` / `SetupPoseBones()` / `SetupPoseSlots()` |
| Bone local / world | `bone.X`, `bone.WorldX` | `bone.Pose.X`, `bone.AppliedPose.WorldX` |
| Slot attachment / color | `slot.Attachment`, `slot.R/G/B/A` | `slot.AppliedPose.Attachment`, `slot.AppliedPose.GetColor()` |
| Mesh settings | `skeletonAnimation.zSpacing` | `skeletonAnimation.Renderer.MeshSettings.zSpacing` |
| Track mixing | `MixBlend`, `HoldPrevious` | removed; `TrackEntry.Additive` |
| Default texture workflow | PMA | straight alpha |

Full rename list: [references/versions-and-upgrade.md](references/versions-and-upgrade.md). The rest of this skill and its references use 4.3 names with 4.2 noted where they differ; translate through this table for 4.2 projects.

## Workflow

1. **Match versions.** The runtime's major.minor must equal the Spine Editor version used to export (4.3 runtime reads only 4.3 exports; 4.2 and older exports will not load). Patch versions may differ, but re-export everything when changing major.minor. Pin the editor version in the team's Spine launcher settings.
2. **Install** (pick one, do not mix):
   - **unitypackage** from esotericsoftware.com/spine-unity-download, imported into `Assets/Spine` and `Assets/Spine Examples`. Updating means deleting both folders first when changing major.minor.
   - **UPM git URLs** (Package Manager > + > Add package from git URL), in this order, because UPM cannot resolve git dependencies itself:
     1. `https://github.com/EsotericSoftware/spine-runtimes.git?path=spine-csharp/src#4.3`
     2. `https://github.com/EsotericSoftware/spine-runtimes.git?path=spine-unity/Assets/Spine#4.3`
     3. URP: `https://github.com/EsotericSoftware/spine-runtimes.git?path=spine-unity/Modules/com.esotericsoftware.spine.urp-shaders#4.3`. This one requires spine-unity installed via UPM; with the unitypackage, use the URP Shaders zip from the download page instead.
     Replace `#4.3` with a commit hash in `manifest.json` for reproducible builds; the branch moves.
   - The base runtime ships Built-in RP shaders only. **URP projects need the URP Shaders package**, otherwise skeletons render pink or unlit.
3. **Pick the texture workflow before importing.** Unity 6 URP projects use Linear color space, and premultiplied-alpha (PMA) textures are incompatible with Linear. Use **straight alpha**, which is the spine-unity 4.3 default (4.2 defaults to PMA; switch it). The Spine Editor's Texture Packer still defaults to PMA, so disable `Premultiply alpha` and enable `Bleed` there. In Unity, Edit > Preferences > Spine > `Switch Texture Workflow` switches the texture preset (`StraightAlphaTexturePreset` / `PMATexturePreset`) and the blend-mode materials together. Straight-alpha materials need `Straight Alpha Texture` enabled.
4. **Export** binary (`.skel.bytes`) for production, JSON while learning. Atlas extension `.atlas.txt`. spine-unity cannot load `.skel` without `.bytes`. Drop the skeleton file, `.atlas.txt` and `.png` pages into one folder.
5. **Check the generated assets**: `<name>_Atlas` (SpineAtlasAsset), `<name>_Material` (one per atlas page), `<name>_SkeletonData` (SkeletonDataAsset). Set the `Scale` on the SkeletonDataAsset (default 0.01, meaning 100 Spine px per Unity unit; use 1/pixelsPerUnit to match art), default mix and per-pair mix durations.
6. **Instantiate**: drag `_SkeletonData` into the scene and choose SkeletonAnimation (or SkeletonMecanim), or into a Canvas for SkeletonGraphic. Assign URP materials (see [references/urp-3d-rendering.md](references/urp-3d-rendering.md)).
7. **Drive it from code** with `AnimationState` ([references/playback-api.md](references/playback-api.md)).
8. Pool instances and prewarm skeleton data (Performance below).

## Components (4.3 split architecture)

In 4.2, `SkeletonAnimation` and `SkeletonMecanim` derive from `SkeletonRenderer` and `SkeletonGraphic` animates itself; there is one component per skeleton. In 4.3, animation and rendering are separate components on the same GameObject:

| Animation component | Renderer component | Use |
|---|---|---|
| `SkeletonAnimation` | `SkeletonRenderer` (MeshRenderer + MeshFilter) | World-space characters, driven by `AnimationState` from code. Default choice |
| `SkeletonMecanim` | `SkeletonRenderer` | Driven by an Animator Controller (Spine animations appear as clips under the SkeletonDataAsset). Use only when you need Mecanim state machines; less control over Spine mixing |
| `SkeletonAnimation` | `SkeletonGraphic` (CanvasRenderer) | uGUI. Masks, layout and Canvas sorting. Not for world space. Use `SkeletonGraphic` materials, never URP 3D shaders |

Access: `skeletonAnimation.Renderer` (an `ISkeletonRenderer`), `skeletonRenderer.Animation` (an `ISkeletonAnimation`), `skeletonAnimation.AnimationState`, `skeletonAnimation.Skeleton`. Enabling or disabling a skeleton means toggling **both** components. Code creation: `SkeletonAnimation.NewSkeletonAnimationGameObject(skeletonDataAsset)` returns a `SkeletonComponents<SkeletonRenderer, SkeletonAnimation>`; for UI, `SkeletonGraphic.NewSkeletonGraphicGameObject(...)`.

Upgrading a 4.2 project: components auto-split when scenes/prefabs are opened and saved, and serialized references from your scripts to `SkeletonRenderer`/`SkeletonGraphic` can be lost. Read [references/versions-and-upgrade.md](references/versions-and-upgrade.md) first.

## Key rules

- `AnimationState` and `Skeleton` are not guaranteed to exist in `Awake`. Cache them in `Start` (the getter calls `Initialize(false)`).
- `TrackEntry` objects are pooled. Never keep a reference after its `Dispose` event; re-query `AnimationState.GetTrack(trackIndex)` (4.3; `GetCurrent` in 4.2).
- Flip with `skeleton.ScaleX = -1`, not a negative Transform scale. Negative scale flips winding, which breaks lighting, culling and shadows.
- After changing skins or attachments, call `skeleton.SetupPoseSlots()` (4.3 name; `SetSlotsToSetupPose()` in 4.2) so slots show the new skin's attachments, then `animationState.Apply(skeleton)` if you need the pose this frame.
- Use `[SpineAnimation]`, `[SpineSkin]`, `[SpineSlot]`, `[SpineBone]`, `[SpineEvent]`, `[SpineAttachment]` on string fields for Inspector dropdowns, or `AnimationReferenceAsset` for typo-proof references. Resolve names to `Spine.Animation` / `EventData` once and compare references, not strings, every frame.
- Spine events (keyed in the Spine Editor) are not Unity `AnimationEvent`s. Subscribe to `AnimationState.Event` or `TrackEntry.Event`.

## Using 2D Spine in a 3D URP scene

Summary; details and shader settings in [references/urp-3d-rendering.md](references/urp-3d-rendering.md).

- **Shaders**: `Universal Render Pipeline/Spine/Skeleton` (unlit), `.../Spine/Skeleton Lit` (simple Lambert lighting, optional receive shadows), `.../Spine/Sprite` (per-pixel lit, normal maps, receives shadows, fixed normals). Never the `Universal Render Pipeline/2D/Spine/*` shaders with the 3D Forward/Forward+ renderer.
- **Billboarding**: the mesh lives in the local XY plane. Rotate the root around world Y to face the camera (cylindrical billboard) so characters stay upright on the ground.
- **Sorting**: Spine materials are transparent by default and sort by distance like other transparents. Add a `SortingGroup` to the root so all submeshes (atlas pages, blend modes) sort as one unit, or switch to depth write (`_ZWrite` on, Render Queue `AlphaTest`) with a non-zero **Z Spacing** so the skeleton depth-tests against the 3D world.
- **Lighting**: Skeleton Lit and Sprite need normals. Enable `Add Normals` on the renderer's Advanced settings, or use the Sprite shader's fixed normals. Normal maps also need `Solve Tangents`.
- **Shadows**: cast via the shaders' ShadowCaster pass with `Shadow alpha cutoff`. Set the MeshRenderer's Cast Shadows to `Two Sided` for a flat billboard. Receive with Sprite, or Skeleton Lit with `Receive Shadows`.

## Performance

| Cost | Guidance |
|---|---|
| Draw calls | One submesh and material per atlas page and per blend mode switch in draw order. Pack each character into one page (2048x2048 on mobile, 4096 max), group additive/multiply slots together in draw order |
| Atlas memory | Pack with `Strip whitespace`, `Rotation`, polygons, and scale (export at 0.5 for distant/background characters). Compress pages per `unity-texture-import`; ASTC with alpha works for straight alpha |
| Mesh generation | Every visible skeleton rebuilds its mesh each frame on the CPU. Lower vertex counts in Spine (weighted meshes, fewer bones). Set `UpdateWhenInvisible` to `OnlyAnimationStatus` or `EverythingExceptMesh`, and `UpdateMode` to `Nothing` for far-off actors |
| Clipping attachments | CPU triangle clipping every frame against the polygon. Keep clipping polygons to few vertices, clip few slots, and avoid them on crowds; `MeshSettings.useClipping` can disable them globally per renderer |
| Skeleton data parsing | The first instance parses the `.skel.bytes`/`.json` (JSON is much slower). Prewarm on a loading screen with `skeletonDataAsset.GetSkeletonData(false)` |
| Instantiation | Pool skeleton GameObjects (`zbase-pooling` or `UnityEngine.Pool`). On release: `AnimationState.ClearTracks()`, `Skeleton.SetupPose()`, reset skin and color, unsubscribe events |
| Threading | 4.3 can run animation updates on worker threads (`ThreadedAnimation` per component or the Spine preferences). Listener callbacks are queued and issued on the main thread; profile before enabling |
| Repacked skins | `GetRepackedSkin` creates new textures and materials that you must `Destroy`; call `AtlasUtilities.ClearCache()` after repacking |

Addressables: atlas textures can be loaded on demand with the `com.esotericsoftware.spine.addressables` module (Spine Addressables Extensions, with low-res placeholders), or by making the whole `_SkeletonData` + atlas + materials Addressable together in one group (see `addressables-asset-loading`). Keep a skeleton's `_SkeletonData`, `_Atlas`, materials and pages in the same group to avoid duplicated textures.

## Pitfalls

| Symptom | Cause / fix |
|---|---|
| "Skeleton data file not found" / version error on import | Exported with a different Spine major.minor; re-export with the runtime's version |
| `.skel` file shows as unknown asset | Rename to `.skel.bytes` or set the export extension |
| Dark or white fringes around attachments | PMA/straight mismatch between export, texture import (`sRGB`, `Alpha Is Transparency`) and material `Straight Alpha Texture`; in Linear use straight alpha |
| Pink skeleton in URP | URP Shaders package missing, or Built-in `Spine/Skeleton` material in use |
| Skeleton invisible in front of/behind 3D objects unexpectedly | Transparent sorting by pivot distance; use SortingGroup or depth write + Z Spacing |
| Z-fighting flicker between attachments | Depth write on with Z Spacing 0 |
| Black or flat lit skeleton | No normals: enable Add Normals or fixed normals in the Sprite shader |
| Callbacks fire for the wrong animation | Stored `TrackEntry` after Dispose, now reused by the pool |
| Skin change shows stale attachments | Missing `skeleton.SetupPoseSlots()` after `SetSkin` |
| Lost references after upgrading to 4.3 | Split components; follow the upgrade guide before saving scenes |
| `AnimationState` null in Awake | Access in Start |
| Mix-and-match textures leak | Repacked skin textures/materials not destroyed |

## Related skills

- `animation-authoring`: Animator controllers when using SkeletonMecanim.
- `unity-texture-import`: atlas page import and compression.
- `unity-lighting`: URP light and shadow settings the Spine lit shaders depend on.
- `manage-sprite-atlas`: Unity SpriteAtlas, which is separate from Spine atlases (do not pack Spine pages into a SpriteAtlas).
- `zbase-pooling`: pooling skeleton instances.
- `addressables-asset-loading`: loading Spine assets on demand.
- `csharp-unity`: code style for the examples.
