# Culling and LOD

Read when triangle counts, culling time or shadow caster counts are high, or when distant objects cost too much.

## LODGroup

- Authoring and scripting `LODGroup` (SetLODs, transition heights) is covered in `level-geometry-authoring`.
- Budget: each LOD step should cut triangles by roughly 50%. LOD0 only within a few metres of the camera on mobile.
- Last LOD's transition height is the cull threshold; set it (e.g. 1-2% of screen height) so small props disappear entirely at distance.
- **LOD Bias** (Quality settings or URP-specific quality override): > 1 keeps higher LODs longer, < 1 switches earlier. Lower it on low tiers instead of authoring separate LODs. `QualitySettings.lodBias` at runtime.
- **Maximum LOD Level** (Quality settings) skips the highest LODs entirely on low tiers and excludes them from the build for that tier's platforms.
- Shadow casters: use a lower LOD or a simplified mesh for shadows (`ShadowCastingMode.ShadowsOnly` proxy) on dense props.
- Mesh LOD (automatic per-mesh LOD generation at import, no LODGroup) is available from Unity 6.2 per the `level-geometry-authoring` notes; verify in your editor version.

### LOD crossfade

- LODGroup **Fade Mode: Cross Fade**, plus URP Asset > Quality > **LOD Cross Fade** enabled, plus a shader that supports the `LOD_FADE_CROSSFADE` keyword (URP Lit, Simple Lit, Shader Graph with the LOD crossfade option).
- **Dither type**: Bayer or Blue Noise (URP Asset). Dithered crossfade avoids sorting issues but looks noisy without TAA/STP.
- During the transition both LODs render: doubled draw and vertex cost for objects mid-fade. Keep `fadeTransitionWidth` small and avoid crossfade on very common props on mobile.
- Crossfade creates extra shader variants; disable it on the URP Asset for tiers that do not use it so they get stripped.
- **Animate Cross-fading** (time-based) vs transition width (distance-based): time-based hides popping better when the camera stops in the transition zone.

## Baked occlusion culling (Umbra)

- Window > Rendering > Occlusion Culling. Mark large static walls/buildings **Occluder Static**, everything that can be hidden **Occludee Static** (dynamic objects are occludees automatically if `Renderer.allowOcclusionWhenDynamic` is on).
- Bake parameters:
  - **Smallest Occluder**: the smallest object that should hide others (metres). Larger value = smaller data, faster runtime, less precise.
  - **Smallest Hole**: the smallest gap to see through (doorways, windows). Too large and objects pop behind fences.
  - **Backface Threshold**: removes data for areas under the terrain/inside geometry; lower values shrink data but can break if the camera enters those areas.
- **Occlusion Areas** limit where camera cells are computed (smaller data). **Occlusion Portals** toggle occlusion for doors at runtime (`OcclusionPortal.open`).
- Camera: `useOcclusionCulling` true (default).
- Best for: interiors, dense cities, corridors. Little or negative value for open terrain where almost everything is visible: the per-camera query cost remains.
- Visualize: Occlusion Culling window > Visualization tab with the camera selected.
- Unity 6 GPU occlusion culling is the dynamic alternative for GRD renderers; see `batching.md`.

## Camera far plane

- Lower `Camera.farClipPlane` to the distance the game needs. It reduces renderers passing frustum culling and improves depth precision (fewer z-fighting artifacts). Hide the cutoff with fog or a skybox.
- Shadow distance is separate (URP Asset > Shadows > Max Distance); lowering the far plane does not reduce shadow cost by itself.
- Many-camera setups: every camera culls and renders separately. Overlay cameras for UI or weapons still cull; restrict their culling masks.

## Per-layer cull distances

```csharp
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class LayerCullDistances : MonoBehaviour
{
    #region Fields

    [SerializeField] private string[] layerNames = { "SmallProps", "Foliage", "Decals" };
    [SerializeField] private float[] distances = { 40f, 80f, 30f };
    [SerializeField] private bool spherical = true;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        var targetCamera = GetComponent<Camera>();
        var cullDistances = new float[32];

        var count = Mathf.Min(this.layerNames.Length, this.distances.Length);
        for (var i = 0; i < count; i++)
        {
            var layer = LayerMask.NameToLayer(this.layerNames[i]);
            if (layer < 0) continue;
            cullDistances[layer] = this.distances[i];
        }

        targetCamera.layerCullDistances = cullDistances;
        targetCamera.layerCullSpherical = this.spherical;
    }

    #endregion
}
```

- A value of 0 means "use the far plane".
- Assign the whole array; modifying elements of the returned array has no effect.
- `layerCullSpherical` avoids objects popping in and out when the camera rotates (planar distance changes with view direction).
- Cull distances do not apply to shadow casters drawn by other cameras or lights the same way; small props on a culled layer still cast shadows if inside the shadow distance. Combine with `ShadowCastingMode.Off` for small props.

## Other culling levers

- **Shadow casting off** for small props, decals, grass and interior clutter: shadow passes multiply draws per cascade.
- **Terrain**: Pixel Error, Basemap Distance, Detail Distance and Detail Density, Tree Distance and Billboard Start. Terrain detail is often the largest triangle source on mobile.
- **Renderer bounds**: oversized bounds (skinned meshes with `updateWhenOffscreen`, procedural meshes with manual bounds) defeat frustum culling.
- **Skinned meshes**: `SkinnedMeshRenderer.updateWhenOffscreen` off; Animator Culling Mode `Cull Update Transforms` or `Cull Completely` for non-player characters.
- **Particle systems**: culling mode Automatic needs no world-space-dependent modules; otherwise they simulate and render off-screen.
