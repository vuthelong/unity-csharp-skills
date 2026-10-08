# Rig and animation import

Read when setting up the Rig and Animation tabs for Blender FBX: Generic vs Humanoid, Avatars, retargeting, Optimize Game Objects, clip splitting, looping, root motion and events. Runtime Animator work (controllers, blend trees, `OnAnimatorMove`) belongs to `animation-authoring`.

## Rig tab

| Inspector | API | Notes |
|---|---|---|
| Animation Type | `animationType` (`ModelImporterAnimationType.None/Legacy/Generic/Human`) | None for props. Legacy only for the old `Animation` component |
| Avatar Definition | `avatarSetup` (`ModelImporterAvatarSetup.NoAvatar/CreateFromThisModel/CopyFromOther`) | |
| Source | `sourceAvatar` | The Avatar sub-asset of the character's model FBX, for `CopyFromOther` |
| Root node (Generic) | `motionNodeName` | Transform path of the bone that drives root motion |
| Skin Weights | `skinWeights`, `maxBonesPerVertex`, `minBoneWeight` | Standard (4 bones) for most platforms |
| Optimize Bones | `optimizeBones` | Removes bones that have no skin weights |
| Optimize Game Objects | `optimizeGameObjects` | Needs an Avatar. See below |
| Extra Transforms to Expose | `extraExposedTransformPaths` (string[] of transform paths) | Bones that stay as GameObjects under Optimize Game Objects |

### Generic or Humanoid

| | Generic | Humanoid |
|---|---|---|
| Use for | Creatures, props with bones, characters that only play their own clips | Bipeds that share clips across different rigs, mocap, store animations |
| Retargeting | Only between identical hierarchies and bone names | Any Humanoid Avatar to any Humanoid Avatar |
| Extra bones (tail, cape, weapon) | Animated normally | Animated only if included in the clip's Avatar Mask transform list; otherwise ignored |
| Foot IK, Mirror, Look At | No | Yes (`OnAnimatorIK`, clip Mirror) |
| CPU cost | Lower | Higher (retargeting solve every frame) |
| Root motion source | `motionNodeName` | Body center of mass |

Default to Generic for non-humanoid creatures and for performance-critical crowds. Choose Humanoid when you need retargeting or Animator IK.

### Humanoid Avatar setup

1. Rig tab: Animation Type Humanoid, Avatar Definition Create From This Model, Apply.
2. Configure: every required bone (Hips, Spine, Chest, Neck, Head, upper/lower arms and legs, hands, feet) must be green. Map the Blender spine chain explicitly if auto-mapping skipped one.
3. Pose > Enforce T-Pose, Apply. A rest pose far from T-pose produces twisted limbs on retargeted clips.
4. Muscles & Settings: preview the ranges; fix flipped knees/elbows by correcting the bone roll in Blender rather than muscle ranges.
5. Save the mapping (Mapping > Save) as `.ht` for rigs that share bone names, and load it for the next character.

### Copy From Other Avatar (animation-only FBX)

- Animation-only files: Animation Type same as the character, Avatar Definition `CopyFromOther`, Source = the character's Avatar sub-asset.
- Bone names and hierarchy must match the source model. A mismatch imports silently with clips that do nothing for the missing bones.
- Put Copy From Other + Source in the `Animations/` folder's `ModelImport.preset` (one preset per character folder) rather than loading the Avatar inside an import callback. From a menu item outside import, `AssetDatabase.LoadAssetAtPath<Avatar>(characterFbxPath)` returns the Avatar sub-asset to assign to `sourceAvatar`.

### Optimize Game Objects

- Removes the bone GameObjects from the instantiated hierarchy; skinning and animation run natively with no Transform sync. Large CPU win on characters.
- Bones you need at runtime (weapon socket, IK target, VFX attach point) go into Extra Transforms to Expose. They appear as direct children of the root, not at their original depth.
- Scripts that `Find("Armature/Hips/...")` break. Expose the bone instead, or toggle at runtime with `AnimatorUtility.DeoptimizeTransformHierarchy` / `OptimizeTransformHierarchy`.
- Ragdolls and cloth need the full hierarchy; leave it off for those characters.

## Animation tab

| Inspector | API | Notes |
|---|---|---|
| Import Animation | `importAnimation` | Off for props and for the mesh-only character FBX if clips live elsewhere |
| Import Constraints | `importConstraints` | Off unless you want Unity constraint components from the FBX |
| Anim. Compression | `animationCompression` (`ModelImporterAnimationCompression.Off/KeyframeReduction/Optimal`) | Optimal for shipping |
| Rotation / Position / Scale Error | `animationRotationError`, `animationPositionError`, `animationScaleError` | Raise carefully; fingers and feet show errors first |
| Resample Curves | `resampleCurves` | On (Quaternion resampling avoids Euler flips) |
| Remove Constant Scale Curves | `removeConstantScaleCurves` | On |
| Clips | `clipAnimations` (`ModelImporterClipAnimation[]`) | Empty means "use `defaultClipAnimations`", one per take |

### Clip settings (`ModelImporterClipAnimation`)

| Inspector | Property |
|---|---|
| Clip name | `name` (`takeName` is the source take, for example `Armature|Run`) |
| Start / End | `firstFrame`, `lastFrame` (frames, float) |
| Loop Time | `loopTime` |
| Loop Pose | `loopPose` (blends end to start; use when Loop Match is not green) |
| Cycle Offset | `cycleOffset` |
| Root Transform Rotation: Bake Into Pose / Based Upon Original / Offset | `lockRootRotation`, `keepOriginalOrientation`, `rotationOffset` |
| Root Transform Position (Y): Bake Into Pose / Based Upon Original or Feet / Offset | `lockRootHeightY`, `keepOriginalPositionY`, `heightFromFeet`, `heightOffset` |
| Root Transform Position (XZ): Bake Into Pose / Based Upon Original | `lockRootPositionXZ`, `keepOriginalPositionXZ` |
| Mirror (Humanoid) | `mirror` |
| Mask | `maskType`, `maskSource` |
| Curves | `curves` (`ClipAnimationInfoCurve[]`) |
| Events | `events` (`AnimationEvent[]`, `time` is normalized 0..1 of the clip) |
| Additive Reference Pose | `hasAdditiveReferencePose`, `additiveReferencePoseFrame` |

### Root motion decisions

"Bake Into Pose" keeps that component of motion in the body (the GameObject does not move from it). Not baked means it becomes root motion and moves the GameObject when Animator Apply Root Motion is on.

| Clip | Rotation | Y | XZ |
|---|---|---|---|
| Idle, emote, attack in place | Bake | Bake (Based Upon Original or Feet) | Bake |
| Walk/run cycle, motion-driven locomotion | Bake (unless turning in place) | Bake | **Not baked** |
| Turn-in-place | **Not baked** | Bake | Bake |
| Jump, climb, vault | Bake | **Not baked** if the arc should move the object | Not baked |

Check the Root Transform indicators: green "loop match" means the start and end poses agree for that component. Red on XZ for a forward run is expected when XZ is not baked. In-place locomotion driven by a CharacterController: bake all three and turn Apply Root Motion off.

### Splitting and configuring clips from code

`OnPreprocessAnimation` in [../scripts/Editor/ModelImportRules.cs](../scripts/Editor/ModelImportRules.cs) sets loop flags by take name. For explicit frame ranges (one long take from an older pipeline), run from a menu item:

```csharp
using UnityEditor;
using UnityEngine;

public static class ClipSplitter
{
    #region Public Methods
    public static void Split(string modelPath, string takeName, string[] names, int[] firstFrames, int[] lastFrames, bool[] loops)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        if (importer == null) return;

        var clips = new ModelImporterClipAnimation[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            clips[i] = new ModelImporterClipAnimation
            {
                takeName = takeName,
                name = names[i],
                firstFrame = firstFrames[i],
                lastFrame = lastFrames[i],
                loopTime = loops[i],
                lockRootRotation = true,
                lockRootHeightY = true,
                lockRootPositionXZ = !loops[i],
                keepOriginalOrientation = true,
                keepOriginalPositionY = true,
                keepOriginalPositionXZ = true,
            };
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }
    #endregion
}
```

Adding an event to an existing clip definition:

```csharp
var clips = importer.clipAnimations;
var events = new AnimationEvent[1];
events[0] = new AnimationEvent { functionName = "OnFootstep", time = 0.25f };
clips[0].events = events;
importer.clipAnimations = clips;
importer.SaveAndReimport();
```

`clipAnimations` returns a copy; always assign the modified array back. Receivers for events need a method of that name on a component next to the Animator (see `animation-authoring`).

### Animation pitfalls

- Clips imported from FBX are read-only. Duplicate (Ctrl+D) to edit curves, or better, fix in Blender or via `clipAnimations` so reimports keep working.
- Renaming a take in Blender changes `takeName`; existing `clipAnimations` entries that point to the old take import empty. Keep action names stable.
- A clip that moves the character in the Scene view but not in play: Apply Root Motion is off, or XZ is baked.
- Character sinks or floats: Root Transform Position (Y) Based Upon should be Feet (Humanoid) or Original, with Bake Into Pose on.
- Foot sliding on retargeted clips: enable Foot IK on the Animator state and check the source rig's hips height.
