# Spine versions and the 4.2 to 4.3 upgrade

Read when installing or updating spine-unity, when an export fails to load, or when moving a project from spine-unity 4.2 to 4.3. Sources: `spine-unity/Assets/Spine/Documentation/4.3-split-component-upgrade-guide.md` and the 4.3 section of `CHANGELOG.md` in EsotericSoftware/spine-runtimes.

## Version rule

- Spine Editor export version major.minor == runtime major.minor. 4.3 runtime: exports from Spine 4.3.00 or newer (the in-repo `version.txt` also accepts 4.3.74-beta). 4.2 and older exports are rejected; re-export them.
- Patch updates within a major.minor are compatible with existing exports, but read the CHANGELOG; the 4.3 line has shipped API renames during its lifetime.
- Everyone on the team uses the same Spine Editor version (the launcher can pin it). A newer-minor export from one artist breaks the build for everyone.
- Pin the runtime: commit hash in `Packages/manifest.json` for UPM git installs (`...?path=spine-unity/Assets/Spine#<hash>`), or keep the unitypackage version in the repo notes.

## Install layouts

| Method | Packages | Update |
|---|---|---|
| unitypackage | `Assets/Spine`, `Assets/Spine Examples` | Close Unity, delete both folders on major.minor change, import the new package, reimport skeleton assets |
| UPM git | `com.esotericsoftware.spine.spine-csharp`, `com.esotericsoftware.spine.spine-unity`, optional `com.esotericsoftware.spine.urp-shaders`, `...timeline`, `...addressables`, `...on-demand-loading`, `...ui-toolkit` | Change the hash / branch in `manifest.json` |

Optional modules in `spine-unity/Modules`: Timeline (`com.esotericsoftware.spine.timeline`, Spine tracks for Unity Timeline), Addressables on-demand texture loading, UI Toolkit (`SpineVisualElement`, requires Unity 6000.3+), URP Shaders.

## 4.3 texture workflow change

4.3 switched the default atlas workflow from PMA to straight alpha because straight alpha works in both Gamma and Linear color space. Projects upgraded from 4.2 with PMA textures keep working only if the material and texture settings stay PMA; mixing the two produces dark or bright fringes. Pick one with Edit > Preferences > Spine > Switch Texture Workflow, then re-export atlases accordingly.

## Component split (4.3)

| 4.2 | 4.3 |
|---|---|
| `SkeletonAnimation : SkeletonRenderer` | `SkeletonAnimation` + `SkeletonRenderer` on the same GameObject |
| `SkeletonMecanim : SkeletonRenderer` | `SkeletonMecanim` + `SkeletonRenderer` |
| `SkeletonGraphic` with built-in AnimationState | `SkeletonGraphic` + `SkeletonAnimation` |
| `skeletonAnimation.zSpacing` | `skeletonAnimation.Renderer.MeshSettings.zSpacing` |
| `skeletonGraphic.AnimationState` | `((SkeletonAnimation)skeletonGraphic.Animation).AnimationState`, or reference the `SkeletonAnimation` directly |
| `skeletonGraphic.startingAnimation` / `startingLoop` / `timeScale` | `skeletonAnimation.AnimationName` / `loop` / `timeScale` |
| `skeletonAnimation.state` (public field) | `AnimationState` property |
| `valid` | `IsValid` |
| public `Update()` | `UpdateOncePerFrame(deltaTime)` or `Update(0)` to force |
| `SkeletonRenderer.SkeletonRendererDelegate` | top-level `SkeletonRendererDelegate(ISkeletonRenderer)` |
| `LateUpdateMesh()` | `UpdateMesh()` |
| `UpdateLocal` / `UpdateWorld` / `UpdateComplete` on the animation component | on the renderer component (still forwarded from the animation component) |
| `initialSkinName`, `initialFlipX/Y` | `InitialSkinName`, `InitialFlipX/Y` properties |
| `IHasSkeletonRenderer.SkeletonRenderer` | `IHasSkeletonRenderer.Renderer` (`ISkeletonRenderer`) |

`SkeletonRenderer` and `SkeletonGraphic` now run at `[DefaultExecutionOrder(1)]`, after default scripts, so animation is applied before the mesh update.

### Upgrade procedure

1. Back up (commit) the project.
2. Fix the spine-csharp API changes below until scripts compile. Optionally upgrade in two steps: first to the pre-split 4.3 commit named in the upgrade guide, then to the latest 4.3.
3. Fix component references in your scripts. Fields typed `SkeletonRenderer` that pointed at a `SkeletonAnimation` become null after the upgrade, because `SkeletonAnimation` is no longer a subclass. Either retype them and use `[FormerlySerializedAs]`, or use the guide's pattern: keep a hidden `Component` field with `[FormerlySerializedAs("oldName")]` and resolve it to the new type in an `[ExecuteAlways]` `Awake`.
4. Edit > Preferences > Spine > Upgrade Scenes & Prefabs > **Upgrade All**, or open and save every scene and prefab. Components only split when an asset is loaded and saved; building before that ships old components.
5. Verify, then disable Split Component Upgrade in the same preferences to surface anything missed and stop the per-load checks.
6. Scripts that enable/disable a skeleton must toggle both components.

## spine-csharp 4.3 renames

| 4.2 | 4.3 |
|---|---|
| `skeleton.SetToSetupPose()` / `SetBonesToSetupPose()` / `SetSlotsToSetupPose()` | `SetupPose()` / `SetupPoseBones()` / `SetupPoseSlots()` |
| `state.GetCurrent(i)` | `state.GetTrack(i)` |
| `bone.X`, `bone.Rotation`, `bone.ScaleX`... | `bone.Pose.X`, `bone.Pose.Rotation`... |
| `bone.WorldX`, `bone.AX`... | `bone.AppliedPose.WorldX`, `bone.AppliedPose.X`... |
| `slot.Attachment`, `slot.R/G/B/A` | `slot.AppliedPose.Attachment`, `slot.AppliedPose.GetColor()/SetColor()` |
| `skeleton.R/G/B/A` | `skeleton.GetColor()` / `SetColor(...)` (`Color32F`) |
| `ikConstraint.Mix` etc. | `ikConstraint.Pose.Mix` etc. |
| `skeletonData.FindIkConstraint(name)` | `skeletonData.FindConstraint<IkConstraintData>(name)` |
| `eventData.Int/Float/String` | `eventData.SetupPose.Int/...` (the fired `Spine.Event` still has `Int`, `Float`, `String`) |
| `TrackEntry.HoldPrevious`, `MixBlend`, `InterruptAlpha` | Removed; holds are automatic; `TrackEntry.Additive` |
| `Skeleton.Physics.Update` | `Spine.Physics.Update` (clashes with `UnityEngine.Physics`; qualify) |
| `skeleton.DrawOrder` (`ExposedList<Slot>`) | `DrawOrder` class: `.Pose` to change, `.AppliedPose` to read rendered order |
| `MeshAttachment.ParentMesh` | `SourceMesh` |
| `Skin.SkinEntry.Name` | `Skin.SkinEntry.Placeholder` |

Assume more renames exist than this table lists; the full list is in the CHANGELOG's "C#" and "Unity" sections under 4.3.
