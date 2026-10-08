# Terrain API

Namespace `UnityEngine` (`Terrain`, `TerrainData`, `TerrainLayer`, `TreePrototype`, `TreeInstance`, `DetailPrototype`). Editor code under an `Editor` folder.

## Create a terrain with saved data

```csharp
using UnityEditor;
using UnityEngine;

public static class TerrainBuilder
{
    [MenuItem("Tools/Terrain/Create 500m Terrain")]
    public static void Create()
    {
        var data = new TerrainData
        {
            heightmapResolution = 513,
            alphamapResolution = 512,
            baseMapResolution = 1024
        };
        data.size = new Vector3(500f, 120f, 500f);
        data.SetDetailResolution(1024, 32);

        AssetDatabase.CreateAsset(data, "Assets/Terrain/MainTerrain.asset");

        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.name = "MainTerrain";
        Undo.RegisterCreatedObjectUndo(go, "Create terrain");
    }
}
```

`CreateTerrainGameObject` adds `Terrain` and `TerrainCollider` and assigns the data to both. Object initializers apply in order, so `heightmapResolution` is set before `size`.

## Coordinates

| Space | Range | Conversion |
|---|---|---|
| World | metres | `world = terrain.GetPosition() + new Vector3(nx * size.x, h * size.y, nz * size.z)` |
| Normalized | 0..1 on X/Z | `nx = (world.x - pos.x) / size.x` |
| Heightmap sample | `0..heightmapResolution-1` | `hx = Mathf.RoundToInt(nx * (res - 1))` |
| Alphamap sample | `0..alphamapWidth-1` | `ax = Mathf.RoundToInt(nx * (alphamapWidth - 1))` |
| Height value | 0..1 | metres / `size.y` |

Arrays are `[y, x]` where y follows world Z.

## Sculpt a smooth hill

```csharp
public static void AddHill(Terrain terrain, Vector3 worldCenter, float radius, float heightMeters)
{
    TerrainData data = terrain.terrainData;
    Undo.RegisterCompleteObjectUndo(data, "Add hill");

    int res = data.heightmapResolution;
    Vector3 size = data.size;
    Vector3 local = worldCenter - terrain.GetPosition();

    int cx = Mathf.RoundToInt(local.x / size.x * (res - 1));
    int cz = Mathf.RoundToInt(local.z / size.z * (res - 1));
    int r = Mathf.CeilToInt(radius / size.x * (res - 1));

    int x0 = Mathf.Clamp(cx - r, 0, res - 1);
    int z0 = Mathf.Clamp(cz - r, 0, res - 1);
    int w = Mathf.Clamp(cx + r, 0, res - 1) - x0 + 1;
    int h = Mathf.Clamp(cz + r, 0, res - 1) - z0 + 1;

    float[,] heights = data.GetHeights(x0, z0, w, h);
    float add = heightMeters / size.y;

    for (int y = 0; y < h; y++)
    for (int x = 0; x < w; x++)
    {
        float dx = (x0 + x - cx) / (float)r;
        float dz = (z0 + y - cz) / (float)r;
        float d = Mathf.Sqrt(dx * dx + dz * dz);
        if (d >= 1f) continue;
        float falloff = 0.5f + 0.5f * Mathf.Cos(d * Mathf.PI);
        heights[y, x] = Mathf.Clamp01(heights[y, x] + add * falloff);
    }

    data.SetHeights(x0, z0, heights);
}
```

Flatten under a spline or building: sample the target height once, then lerp cells within the footprint toward it with a falloff band.

## Terrain layers and slope painting

```csharp
public static TerrainLayer CreateLayer(string path, Texture2D albedo, Texture2D normal, float tile)
{
    var layer = new TerrainLayer { diffuseTexture = albedo, normalMapTexture = normal, tileSize = new Vector2(tile, tile) };
    AssetDatabase.CreateAsset(layer, path);
    return layer;
}

public static void PaintBySlope(TerrainData data, TerrainLayer grass, TerrainLayer rock, float rockFromDeg, float rockFullDeg)
{
    Undo.RegisterCompleteObjectUndo(data, "Paint by slope");
    data.terrainLayers = new[] { grass, rock };

    int w = data.alphamapWidth;
    int h = data.alphamapHeight;
    var maps = new float[h, w, 2];

    for (int y = 0; y < h; y++)
    for (int x = 0; x < w; x++)
    {
        float nx = x / (float)(w - 1);
        float ny = y / (float)(h - 1);
        float steep = data.GetSteepness(nx, ny);
        float rockW = Mathf.InverseLerp(rockFromDeg, rockFullDeg, steep);
        maps[y, x, 0] = 1f - rockW;
        maps[y, x, 1] = rockW;
    }

    data.SetAlphamaps(0, 0, maps);
}
```

Paths end in `.terrainlayer`. The third array dimension must equal `terrainLayers.Length`. Unity 6 URP Terrain Lit blends 4 layers per pass; more layers add passes.

## Trees

```csharp
public static void ScatterTrees(Terrain terrain, GameObject treePrefab, int count, float maxSlopeDeg, int seed)
{
    TerrainData data = terrain.terrainData;
    Undo.RegisterCompleteObjectUndo(data, "Scatter trees");

    var protos = new System.Collections.Generic.List<TreePrototype>(data.treePrototypes);
    int protoIndex = protos.FindIndex(p => p.prefab == treePrefab);
    if (protoIndex < 0)
    {
        protos.Add(new TreePrototype { prefab = treePrefab });
        data.treePrototypes = protos.ToArray();
        protoIndex = protos.Count - 1;
    }

    var rng = new System.Random(seed);
    var instances = new System.Collections.Generic.List<TreeInstance>(data.treeInstances);
    for (int i = 0; i < count; i++)
    {
        float nx = (float)rng.NextDouble();
        float nz = (float)rng.NextDouble();
        if (data.GetSteepness(nx, nz) > maxSlopeDeg) continue;

        float s = Mathf.Lerp(0.8f, 1.2f, (float)rng.NextDouble());
        instances.Add(new TreeInstance
        {
            prototypeIndex = protoIndex,
            position = new Vector3(nx, 0f, nz),
            widthScale = s,
            heightScale = s,
            rotation = (float)(rng.NextDouble() * Mathf.PI * 2.0),
            color = Color.white,
            lightmapColor = Color.white
        });
    }

    data.SetTreeInstances(instances.ToArray(), snapToHeightmap: true);
}
```

`snapToHeightmap: true` fills the normalized Y. Call `data.RefreshPrototypes()` after editing prototype prefabs.

## Details (grass)

```csharp
data.detailPrototypes = new[]
{
    new DetailPrototype
    {
        prototype = grassMeshPrefab,
        usePrototypeMesh = true,
        useInstancing = true,
        renderMode = DetailRenderMode.VertexLit,
        minWidth = 0.8f, maxWidth = 1.2f, minHeight = 0.8f, maxHeight = 1.2f
    }
};
int[,] density = new int[data.detailHeight, data.detailWidth];
data.SetDetailLayer(0, 0, 0, density);
```

Detail arrays are `[y, x]` at `detailResolution`. In URP prefer instanced mesh details; the legacy Grass billboard modes are Built-in oriented.

## Holes

`data.SetHoles(x, y, bool[,] holes)` at `holesResolution` (= heightmap resolution - 1). `true` = solid surface, `false` = hole. Holes affect rendering and the collider.

## Multi-tile worlds

```csharp
tileCenter.SetNeighbors(left: west, top: north, right: east, bottom: south);
```

Every tile must set its own neighbors (the relation is not mutual automatically). Alternatively set `allowAutoConnect = true` and the same `groupingID` on all tiles and call `Terrain.SetConnectivityDirty()`. Edge rows/columns of adjacent heightmaps must hold identical values to avoid cracks.

## Runtime and performance knobs

| Property | Effect |
|---|---|
| `terrain.drawInstanced` | GPU instanced terrain rendering; keep on |
| `terrain.heightmapPixelError` | Higher = fewer triangles at distance |
| `terrain.basemapDistance` | Beyond this, a baked base map replaces splat blending |
| `terrain.treeDistance`, `treeBillboardDistance` | Tree draw / billboard distances |
| `terrain.detailObjectDistance`, `detailObjectDensity` | Grass cost |
| `TerrainCollider` `enableTreeColliders` | Tree capsule colliders on/off |

Gameplay queries: `terrain.SampleHeight(worldPos) + terrain.GetPosition().y`, `data.GetInterpolatedNormal(nx, nz)`, `data.GetSteepness(nx, nz)`, or `Physics.Raycast` against the TerrainCollider.
