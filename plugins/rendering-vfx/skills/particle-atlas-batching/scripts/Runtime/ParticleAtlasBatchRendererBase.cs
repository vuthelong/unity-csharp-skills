using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ParticleVfx.Atlas
{
    public abstract class ParticleAtlasBatchRendererBase : MonoBehaviour
    {
        static readonly int ClampSourceId = Shader.PropertyToID("_ClampSource");
        static readonly List<ParticleAtlasBatchRendererBase> enabledRenderers = new List<ParticleAtlasBatchRendererBase>(256);
        static bool camerasHooked;

        protected abstract int UvChannelCount { get; }
        protected abstract ProfilerMarker BuildMarker { get; }
        protected abstract void EnsureSources();
        protected abstract void ReleaseSources();
        protected abstract void AppendGeometry(Camera camera, Matrix4x4 worldToLocal);
        protected virtual void ConfigureProperties(MaterialPropertyBlock block) { }
        protected virtual bool RebuildSuppressed => false;

        protected readonly List<Vector3> positions = new List<Vector3>(128);
        protected readonly List<Color> colors = new List<Color>(128);
        protected readonly List<int> indices = new List<int>(192);
        protected List<Vector4>[] uv;
        protected readonly List<Vector3> sourcePositions = new List<Vector3>(64);
        protected readonly List<Color> sourceColors = new List<Color>(64);
        protected readonly List<int> sourceIndices = new List<int>(96);
        protected readonly List<Vector4> sourceUv0 = new List<Vector4>(64);

        Mesh mesh;
        MeshRenderer output;
        MaterialPropertyBlock properties;
        int enabledIndex = -1;

        protected virtual void OnEnable()
        {
            HookCameras();
            enabledIndex = enabledRenderers.Count;
            enabledRenderers.Add(this);
        }

        protected virtual void OnDisable()
        {
            UnregisterEnabled();
            if (output != null) output.enabled = false;
            if (mesh != null) mesh.Clear();
        }

        protected virtual void OnDestroy()
        {
            Release(mesh);
            ReleaseSources();
        }

        protected static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        void EnsureResources()
        {
            if (output == null) output = GetComponent<MeshRenderer>();
            if (properties == null) properties = new MaterialPropertyBlock();
            if (mesh == null)
            {
                mesh = new Mesh { name = "Particle atlas combined", hideFlags = HideFlags.DontSave };
                mesh.MarkDynamic();
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }
            if (uv == null)
            {
                uv = new List<Vector4>[UvChannelCount];
                for (int i = 0; i < uv.Length; i++) uv[i] = new List<Vector4>(128);
            }
            EnsureSources();
        }

        protected static void EnsureSource(ParticleSystemRenderer renderer, ref ParticleSystem particles, ref Mesh baked)
        {
            if (renderer == null) return;
            renderer.enabled = false;
            if (particles == null) particles = renderer.GetComponent<ParticleSystem>();
            if (baked == null)
            {
                baked = new Mesh { name = "Particle atlas source", hideFlags = HideFlags.DontSave };
                baked.MarkDynamic();
            }
        }

        protected bool BakeSource(ParticleSystemRenderer renderer, ParticleSystem particles, Mesh baked, Camera camera, bool trails = false)
        {
            if (renderer == null || !renderer.gameObject.activeInHierarchy || particles == null
                || (!trails && particles.particleCount == 0)) return false;
            const ParticleSystemBakeMeshOptions options = ParticleSystemBakeMeshOptions.BakeRotationAndScale | ParticleSystemBakeMeshOptions.BakePosition;
            if (trails) renderer.BakeTrailsMesh(baked, camera, options);
            else renderer.BakeMesh(baked, camera, options);
            if (baked.vertexCount == 0) return false;
            baked.GetVertices(sourcePositions);
            baked.GetColors(sourceColors);
            baked.GetUVs(0, sourceUv0);
            baked.GetIndices(sourceIndices, 0);
            return true;
        }

        void UnregisterEnabled()
        {
            if (enabledIndex < 0) return;
            int last = enabledRenderers.Count - 1;
            var moved = enabledRenderers[last];
            enabledRenderers[enabledIndex] = moved;
            moved.enabledIndex = enabledIndex;
            enabledRenderers.RemoveAt(last);
            enabledIndex = -1;
        }

        static void HookCameras()
        {
            if (camerasHooked) return;
            camerasHooked = true;
            RenderPipelineManager.beginCameraRendering += BeforeSrpCamera;
            Camera.onPreCull += BeforeBuiltinCamera;
        }

        static void BeforeSrpCamera(ScriptableRenderContext context, Camera camera) => RebuildAll(camera);

        static void BeforeBuiltinCamera(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline == null) RebuildAll(camera);
        }

        static void RebuildAll(Camera camera)
        {
            for (int i = enabledRenderers.Count - 1; i >= 0; i--) enabledRenderers[i].Rebuild(camera);
        }

        public void Rebuild(Camera camera)
        {
            if (RebuildSuppressed || !isActiveAndEnabled || camera == null || (camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            using (BuildMarker.Auto())
            {
                EnsureResources();
                positions.Clear(); colors.Clear(); indices.Clear();
                foreach (var list in uv) list.Clear();
                AppendGeometry(camera, transform.worldToLocalMatrix);
                mesh.Clear();
                if (positions.Count == 0) { output.enabled = false; return; }
                mesh.indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(positions);
                mesh.SetColors(colors);
                for (int i = 0; i < uv.Length; i++) mesh.SetUVs(i, uv[i]);
                mesh.SetTriangles(indices, 0, true);
                bool hdr = camera.allowHDR;
                if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline) hdr &= pipeline.supportsHDR;
                else if (camera.targetTexture != null)
                {
                    var format = camera.targetTexture.format;
                    hdr = format == RenderTextureFormat.ARGBHalf || format == RenderTextureFormat.ARGBFloat
                        || format == RenderTextureFormat.RGB111110Float || format == RenderTextureFormat.DefaultHDR;
                }
                output.GetPropertyBlock(properties);
                properties.SetFloat(ClampSourceId, hdr ? 0f : 1f);
                ConfigureProperties(properties);
                output.SetPropertyBlock(properties);
                output.enabled = true;
            }
        }
    }
}
