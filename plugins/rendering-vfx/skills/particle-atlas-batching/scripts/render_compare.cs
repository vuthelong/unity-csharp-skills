var dir = "Temp/particle-atlas";
var lines = System.IO.File.ReadAllLines(dir + "/cmp_list.txt");
var times = new[] { 0.15f, 0.4f, 0.9f };
const int C = 220;
const int Seed = 1234;
var log = new System.Text.StringBuilder("playing=" + UnityEngine.Application.isPlaying + " native=" + ParticleVfx.Atlas.ParticleAtlasBatchRenderer.NativeBatching + "\n");
var byKey = new System.Collections.Generic.SortedDictionary<string, System.Collections.Generic.List<string[]>>();
foreach (var l in lines)
{
    if (string.IsNullOrWhiteSpace(l)) continue;
    var c = l.Split('\t');
    if (!byKey.ContainsKey(c[0])) byKey[c[0]] = new System.Collections.Generic.List<string[]>();
    byKey[c[0]].Add(c);
}
foreach (var kv in byKey)
{
    var img = new UnityEngine.Texture2D(C * times.Length * 2, C * kv.Value.Count, UnityEngine.TextureFormat.RGBA32, false);
    for (int row = 0; row < kv.Value.Count; row++)
    {
        var c = kv.Value[row];
        var far = new UnityEngine.Vector3(5000, 5000, 0);
        var gos = new UnityEngine.GameObject[2];
        gos[0] = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(c[2]), far, UnityEngine.Quaternion.identity);
        gos[1] = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(c[1]), far, UnityEngine.Quaternion.identity);
        foreach (var go in gos)
            foreach (var ps in go.GetComponentsInChildren<UnityEngine.ParticleSystem>(true))
            { ps.Stop(true, UnityEngine.ParticleSystemStopBehavior.StopEmittingAndClear); ps.useAutoRandomSeed = false; ps.randomSeed = Seed; }
        var b = new UnityEngine.Bounds(gos[0].transform.position, UnityEngine.Vector3.one);
        foreach (var t in times)
        {
            gos[0].GetComponentInChildren<UnityEngine.ParticleSystem>(true).Simulate(t, true, true, true);
            foreach (var r in gos[0].GetComponentsInChildren<UnityEngine.Renderer>(false))
                if (r.enabled && r.bounds.size.sqrMagnitude > 0 && r.bounds.size.magnitude < 50) b.Encapsulate(r.bounds);
        }
        float size = UnityEngine.Mathf.Max(b.extents.x, b.extents.y) * 1.1f;
        var camGo = new UnityEngine.GameObject("render_compare_camera");
        var cam = camGo.AddComponent<UnityEngine.Camera>();
        cam.allowHDR = false; cam.orthographic = true; cam.orthographicSize = size;
        cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor; cam.backgroundColor = new UnityEngine.Color(0.3f, 0.3f, 0.3f, 1f);
        var rt = new UnityEngine.RenderTexture(C, C, 24, UnityEngine.RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        for (int ti = 0; ti < times.Length; ti++)
            for (int k = 0; k < 2; k++)
            {
                gos[k].SetActive(true); gos[1 - k].SetActive(false);
                foreach (var ps in gos[k].GetComponentsInChildren<UnityEngine.ParticleSystem>(true)) { ps.useAutoRandomSeed = false; ps.randomSeed = Seed; }
                gos[k].GetComponentInChildren<UnityEngine.ParticleSystem>(true).Simulate(times[ti], true, true, true);
                cam.transform.position = new UnityEngine.Vector3(b.center.x, b.center.y, far.z - 20);
                cam.Render();
                UnityEngine.RenderTexture.active = rt;
                img.ReadPixels(new UnityEngine.Rect(0, 0, C, C), (ti * 2 + k) * C, C * (kv.Value.Count - 1 - row));
                UnityEngine.RenderTexture.active = null;
            }
        cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(camGo); UnityEngine.Object.DestroyImmediate(gos[0]); UnityEngine.Object.DestroyImmediate(gos[1]);
        log.AppendLine(kv.Key + " row " + row + ": " + c[1].Substring(c[1].LastIndexOf('/') + 1) + " orthoSize=" + size.ToString("F2"));
    }
    var opaque = img.GetPixels32();
    for (int i = 0; i < opaque.Length; i++) opaque[i].a = 255;
    img.SetPixels32(opaque); img.Apply();
    System.IO.File.WriteAllBytes(dir + "/compare_" + kv.Key + ".png", UnityEngine.ImageConversion.EncodeToPNG(img));
    UnityEngine.Object.DestroyImmediate(img);
}
System.IO.File.WriteAllText(dir + "/out.txt", log.ToString());
return "done";
