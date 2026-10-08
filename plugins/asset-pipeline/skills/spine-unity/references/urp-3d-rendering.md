# Spine in a 3D URP scene

Read when placing Spine skeletons in a 3D world rendered by URP's Universal (Forward / Forward+ / Deferred) renderer: shader choice, material settings, sorting against 3D geometry and transparent effects, billboarding, lighting, normals, shadows and z-spacing.

## Shader choice (Spine URP Shaders package)

| Shader | Lighting | Shadows | Normal maps | Use |
|---|---|---|---|---|
| `Universal Render Pipeline/Spine/Skeleton` | Unlit | Casts (ShadowCaster pass, `_Cutoff`) | No | Stylized/unlit characters, UI-like world elements, best performance |
| `Universal Render Pipeline/Spine/Skeleton Lit` | Lambert, main + additional lights | Casts; receives with `Receive Shadows` toggle (`_RECEIVE_SHADOWS`) | No | Characters that should react to scene lights cheaply. `Double-Sided Lighting` toggle for flipped skeletons |
| `Universal Render Pipeline/Spine/Sprite` | Per-pixel, optional rim, emission, fixed normals | Casts (`_ShadowAlphaCutoff`) and receives | Yes (`_NORMALMAP`) | Hero characters lit like 3D objects, normal-mapped Spine art |
| `Universal Render Pipeline/Spine/Outline/...` | as base | | | Outline variants; needs its own material |
| `Universal Render Pipeline/2D/Spine/*` | 2D Renderer lights | | | **Only** with URP's 2D Renderer. Wrong in a 3D project |

`SkeletonGraphic` (Canvas) uses the `Spine/SkeletonGraphic*` materials, never the URP 3D shaders.

Create materials per atlas page: duplicate the generated `_Material`, switch the shader, keep the page texture, and assign them through the atlas asset (so every instance uses them), or per instance with `SkeletonRendererCustomMaterials`. Changing the shader on the generated `_Material` directly also works but is overwritten if you re-generate the atlas asset.

## Material and renderer settings

| Setting | Where | Value for 3D |
|---|---|---|
| `Straight Alpha Texture` | Material | On with the straight-alpha workflow (4.3 default); must match export and texture import |
| `Depth Write` (`_ZWrite`) | Material | Off for classic transparent sorting; On to depth-test against the world (then set Render Queue to `AlphaTest` (2450) where needed, and use Z Spacing) |
| `Shadow alpha cutoff` (`_Cutoff`) | Material | 0.1-0.5; below it fragments cast no shadow |
| `Receive Shadows` | Material (Skeleton Lit) | On if the skeleton stands in shadowed areas |
| Fixed Normals | Material (Sprite) | Model-space or world-space fixed normals avoid needing mesh normals |
| `Advanced > Add Normals` | SkeletonRenderer (`MeshSettings.addNormals`) | On for Skeleton Lit and for Sprite without fixed normals |
| `Advanced > Solve Tangents` | SkeletonRenderer (`MeshSettings.calculateTangents`) | On when using normal maps |
| `Advanced > Z Spacing` | SkeletonRenderer (`MeshSettings.zSpacing`) | Non-zero (for example -0.0001 to -0.001) when Depth Write is on, to stop z-fighting between attachments |
| `Advanced > PMA Vertex Colors` | SkeletonRenderer (`MeshSettings.pmaVertexColors`) | On (needed for tinting and single-pass additive) |
| `Advanced > Use Clipping` | SkeletonRenderer (`MeshSettings.useClipping`) | Off if the skeleton has clipping attachments you can do without |
| Cast Shadows | MeshRenderer | `Two Sided` for a flat skeleton, otherwise the back face casts nothing |
| Receive Shadows | MeshRenderer | Match the material |
| Sorting Layer / Order in Layer | SkeletonRenderer Inspector (stored on the MeshRenderer) | Only meaningful among transparents; see below |

4.3 moved these into `MeshSettings`: from code use `skeletonAnimation.Renderer.MeshSettings.zSpacing`. The 4.2 field names (`skeletonRenderer.zSpacing` ...) remain as forwarding properties on `SkeletonRenderer` only.

## Sorting against 3D content

URP draws opaques front to back, then transparents back to front by distance to the object's bounds center, unless Sorting Layer, Order in Layer or a `SortingGroup` say otherwise. Sorting Layer and Order in Layer apply to transparents in the 3D renderer too.

Options, simplest first:

1. **Transparent + SortingGroup** (default shaders, Depth Write off): add a `SortingGroup` on the skeleton root so all its submeshes (several atlas pages, blend-mode materials) sort as one unit at the root's distance. Works with particles and other transparents. Fails when part of the skeleton should be behind a 3D object and part in front.
2. **Depth Write on, AlphaTest queue, Z Spacing**: the skeleton depth-tests like an opaque cutout. It intersects ground and walls correctly, works with SSAO, depth of field and soft particles (writes to the depth texture through the DepthOnly/DepthNormals passes of the URP Spine shaders), at the cost of hard alpha edges.
3. **SkeletonRenderSeparator**: splits the skeleton at chosen slots into several renderers, so props can sit between parts (a leg behind a pillar, the other in front).

Transparent shadows: only the alpha-cutoff shape casts, regardless of the sorting option.

## Billboarding

The skeleton mesh lies in the local XY plane, facing -Z. For a perspective 3D camera, rotate a parent (not the skeleton object itself, so `Skeleton.ScaleX` flipping stays independent) around world Y to face the camera:

```csharp
using UnityEngine;

public sealed class SpineBillboard : MonoBehaviour
{
    #region Fields
    private const float MinSqrMagnitude = 0.0001f;

    [SerializeField] private Camera targetCamera;

    private Transform _cameraTransform;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        if (this.targetCamera == null) this.targetCamera = Camera.main;
        if (this.targetCamera != null) this._cameraTransform = this.targetCamera.transform;
    }

    private void LateUpdate()
    {
        if (this._cameraTransform == null) return;

        var forward = this._cameraTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < MinSqrMagnitude) return;

        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
    #endregion
}
```

- Cylindrical (Y-axis only) keeps feet planted and avoids leaning. Use the full camera rotation only for flying or top-down views.
- Facing the camera plane (camera forward) rather than the camera position avoids skew at screen edges.
- Facing left/right gameplay direction: flip with `Skeleton.ScaleX`, never with a negative Transform scale.
- With many characters, run billboarding from one manager loop instead of one `LateUpdate` per character.
- Billboards change their shadow silhouette as the camera turns. If that is distracting, cast shadows from a fixed-orientation child copy, or use a blob shadow decal instead.

## Lighting

- URP lights affect Spine only with Skeleton Lit or Sprite shaders. Forward+ removes the per-object light limit; Forward caps additional lights per object (see `unity-lighting`).
- Light probes and baked GI: set the MeshRenderer's Light Probes to `Blend Probes` so skeletons pick up baked ambient light. The URP Spine shaders sample SH ambient.
- Normals: generated normals point toward -Z of the skeleton; with billboarding they face the camera, so lighting looks flat but consistent. Normal maps (Sprite shader) give depth.
- Rim lighting (Sprite shader) helps separate characters from dark backgrounds.

## Z Spacing and draw order

Spine draw order is slot order. With Depth Write off, attachments are drawn in that order within a submesh and Z Spacing is unnecessary. With Depth Write on, each later slot must be slightly closer to the camera or it fails the depth test against earlier ones: Z Spacing offsets each attachment along local Z. Keep the value tiny; large values make the skeleton visibly thick when seen at an angle.

`BoneFollower.followAttachmentZSpacing` (and `followZPosition`) keeps attached objects at the correct depth when Z Spacing is used.

## Performance in 3D

- Each atlas page and each blend-mode change is a submesh with its own material, so a separate draw call (and shadow-caster draw). SRP Batcher batches compatible draws, but submesh count still matters.
- Shadows double the draws. Disable Cast Shadows on background characters.
- Every skeleton has its own dynamic mesh, so GPU instancing does not help. Reduce vertex counts and draw calls instead.
