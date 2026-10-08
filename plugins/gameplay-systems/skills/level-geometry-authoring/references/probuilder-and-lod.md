# ProBuilder and LODGroup API

## ProBuilder (com.unity.probuilder)

Namespaces: `UnityEngine.ProBuilder` (`ProBuilderMesh`, `Face`, `ShapeGenerator`, `PivotLocation`), `UnityEngine.ProBuilder.MeshOperations` (extension methods `Extrude`, `DeleteFaces`, `Subdivide`, `Bridge`, `ConnectEdges`), `UnityEditor.ProBuilder` (`EditorMeshUtility`).

### Create a blockout platform with a painted top and a raised tier

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

public static class BlockoutBuilder
{
    [MenuItem("Tools/Blockout/Platform")]
    public static void CreatePlatform()
    {
        ProBuilderMesh mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(4f, 0.5f, 4f));
        mesh.gameObject.name = "Platform";
        Undo.RegisterCreatedObjectUndo(mesh.gameObject, "Create platform");

        var grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Grass.mat");
        List<Face> top = FacesFacing(mesh, Vector3.up);
        mesh.SetMaterial(top, grass);

        mesh.Extrude(top, ExtrudeMethod.FaceNormal, 1f);

        mesh.ToMesh();
        mesh.Refresh();
        EditorMeshUtility.Optimize(mesh);

        if (mesh.TryGetComponent(out MeshCollider col))
            col.sharedMesh = mesh.GetComponent<MeshFilter>().sharedMesh;
    }

    public static List<Face> FacesFacing(ProBuilderMesh mesh, Vector3 direction, float minDot = 0.7f)
    {
        Vector3 dir = mesh.transform.InverseTransformDirection(direction).normalized;
        return mesh.faces.Where(f => Vector3.Dot(UnityEngine.ProBuilder.Math.Normal(mesh, f), dir) >= minDot).ToList();
    }
}
```

`minDot = 0.7` is roughly a 45 degree tolerance. `Extrude` modifies the passed faces in place, so `top` refers to the new top after extrusion; extrude again for a second tier.

### Other operations

| Need | API |
|---|---|
| Shapes | `ShapeGenerator.GenerateCube`, `GenerateCylinder`, `GenerateStair`, `GenerateArch`, `GeneratePlane`, `GeneratePipe`, `GenerateCone`, `GenerateTorus`, `GenerateDoor`, `GeneratePrism`, `GenerateIcosahedron` |
| Mesh from points | `ProBuilderMesh.Create(positions, faces)`; extruded floor plan: `mesh.CreateShapeFromPolygon(points, height, flipNormals: false)` |
| Delete faces | `mesh.DeleteFaces(faces)` (never all of them) |
| Flip normals | `foreach (var f in faces) f.Reverse();` |
| Subdivide faces | `ConnectElements.Connect(mesh, faces)` |
| Merge objects | `CombineMeshes.Combine(meshes, target)` |
| UVs | `face.uv` (`AutoUnwrapSettings`), `face.manualUV` |
| Smoothing groups | `face.smoothingGroup` |
| Face count | `mesh.faceCount`, `mesh.vertexCount` |

Always finish with `ToMesh()` + `Refresh()` (and `EditorMeshUtility.Optimize` in the Editor, which also builds lightmap UVs when the object is Contribute GI static).

### Pitfalls

- Edits without `ToMesh()`/`Refresh()` stay invisible.
- Face indices change after extrude/delete; re-query by normal rather than caching indices.
- Non-uniform object scale makes extrusion distances and UV scaling look wrong; author at scale 1 and change geometry instead.
- ProBuilder runtime editing works in builds only for operations in the runtime assembly; `EditorMeshUtility` is Editor-only.

## LODGroup

```csharp
public static LODGroup SetupLods(GameObject root, Renderer[] lod0, Renderer[] lod1, Renderer[] lod2)
{
    if (!root.TryGetComponent(out LODGroup group))
        group = root.AddComponent<LODGroup>();

    group.SetLODs(new[]
    {
        new LOD(0.60f, lod0),
        new LOD(0.25f, lod1),
        new LOD(0.02f, lod2)
    });
    group.fadeMode = LODFadeMode.CrossFade;
    group.animateCrossFading = true;
    group.RecalculateBounds();
    return group;
}
```

- Heights are fractions of screen height, strictly descending. Below the last value the object is culled.
- `LOD.fadeTransitionWidth` sets the cross-fade band when `animateCrossFading` is false.
- `group.ForceLOD(index)` (or `-1` to release) for previews and debugging.
- Renderers in a LODGroup should be children of the group's GameObject so bounds and culling stay correct.
- Static batching and LODGroups work together; GPU Resident Drawer in Unity 6 also supports LODGroups.
- For trees on Terrain, give the tree prefab a LODGroup; Terrain uses it for tree LOD and billboards.
