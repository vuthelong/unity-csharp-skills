using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace ParticleVfx.Atlas
{
    public enum ParticleAtlasBlend
    {
        Additive = 1,
        AlphaClamped = 2,
        Alpha = 3
    }

    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ParticleAtlasBatchRenderer : ParticleAtlasBatchRendererBase
    {
        [Serializable]
        public sealed class Source
        {
            public ParticleSystemRenderer renderer;
            public bool trails;
            public Vector4 mainRect;
            public Vector4 mainST = new Vector4(1, 1, 0, 0);
            public float blend;
            public float sampling;
            public Color tint = Color.white;
            [NonSerialized] internal ParticleSystem particles;
            [NonSerialized] internal Mesh baked;
            [NonSerialized] internal NativeState native;
        }

        internal sealed class NativeState
        {
            public readonly List<ParticleSystemVertexStream> streams = new List<ParticleSystemVertexStream>(8);
            public Material material;
            public Sprite[] sprites;
            public Sprite[] proxies;
            public bool customDataEnabled;
            public ParticleSystemCustomDataMode custom2Mode;
            public int custom2Components;
            public readonly ParticleSystem.MinMaxCurve[] custom2 = new ParticleSystem.MinMaxCurve[4];
            public int row;
        }

        const string UIParticleTypeName = "Coffee.UIExtensions.UIParticle";
        static readonly ProfilerMarker Marker = new ProfilerMarker("ParticleAtlasBatchRenderer.Build");
        static readonly List<ParticleSystemVertexStream> NativeStreams = new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.Custom2X
        };
        static readonly List<ParticleAtlasBatchRenderer> enabledInstances = new List<ParticleAtlasBatchRenderer>(256);
        static readonly List<Vector4> liveCustom2 = new List<Vector4>(64);
        static bool nativeBatching = true;
        static bool uiParticleTypeResolved;
        static Type uiParticleType;

        public Source[] sources = Array.Empty<Source>();
        public bool additiveBeforeAlphaAtSameDepth;

        protected override ProfilerMarker BuildMarker => Marker;
        protected override int UvChannelCount => 3;
        protected override bool RebuildSuppressed => nativeActive;

        readonly List<Source> ordered = new List<Source>(16);
        Camera sortingCamera;
        Comparison<Source> sourceComparison;
        bool nativeActive;
        bool? nativeCompatible;
        bool standDown;

        public static bool NativeBatching
        {
            get => nativeBatching;
            set
            {
                if (nativeBatching == value) return;
                nativeBatching = value;
                foreach (var instance in enabledInstances) instance.ApplyRenderPath();
            }
        }

        public static Type UIParticleType
        {
            get
            {
                if (uiParticleTypeResolved) return uiParticleType;
                uiParticleTypeResolved = true;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    uiParticleType = assembly.GetType(UIParticleTypeName, false);
                    if (uiParticleType != null) break;
                }
                return uiParticleType;
            }
        }

        protected override void OnEnable()
        {
            var type = UIParticleType;
            standDown = type != null && GetComponentInParent(type, true) != null;
            if (standDown)
            {
                GetComponent<MeshRenderer>().enabled = false;
                return;
            }
            base.OnEnable();
            enabledInstances.Add(this);
            ApplyRenderPath();
        }

        protected override void OnDisable()
        {
            if (standDown) return;
            enabledInstances.Remove(this);
            base.OnDisable();
        }

        void ApplyRenderPath()
        {
            bool native = nativeBatching && Application.isPlaying && IsNativeCompatible();
            if (native == nativeActive) return;
            if (native) EnterNative(); else ExitNative();
        }

        bool IsNativeCompatible()
        {
            if (nativeCompatible.HasValue) return nativeCompatible.Value;
            var output = GetComponent<MeshRenderer>();
            bool compatible = ParticleAtlasSourceTable.Supported && output != null && output.sharedMaterial != null && sources.Length > 0;
            foreach (var source in sources)
            {
                if (!compatible) break;
                compatible = source != null && source.renderer != null && !source.trails;
                if (!compatible) break;
                var customData = source.renderer.GetComponent<ParticleSystem>().customData;
                compatible = !customData.enabled || customData.GetMode(ParticleSystemCustomData.Custom2) != ParticleSystemCustomDataMode.Color;
            }
            nativeCompatible = compatible;
            return compatible;
        }

        void EnterNative()
        {
            var output = GetComponent<MeshRenderer>();
            if (sources[0].native == null) CaptureNativeStates();
            foreach (var source in sources)
            {
                var renderer = source.renderer;
                var state = source.native;
                var customData = source.particles.customData;
                customData.enabled = true;
                customData.SetMode(ParticleSystemCustomData.Custom2, ParticleSystemCustomDataMode.Vector);
                customData.SetVectorComponentCount(ParticleSystemCustomData.Custom2, 1);
                customData.SetVector(ParticleSystemCustomData.Custom2, 0, new ParticleSystem.MinMaxCurve(state.row));
                WriteLiveParticleRows(source.particles, state);
                if (state.proxies != null)
                {
                    var sheet = source.particles.textureSheetAnimation;
                    for (int i = 0; i < state.proxies.Length; i++) sheet.SetSprite(i, state.proxies[i]);
                }
                renderer.SetActiveVertexStreams(NativeStreams);
                renderer.sharedMaterial = ParticleAtlasSourceTable.NativeMaterial(output.sharedMaterial, state.material.renderQueue);
                renderer.enabled = true;
            }
            output.enabled = false;
            nativeActive = true;
        }

        static void WriteLiveParticleRows(ParticleSystem particles, NativeState state)
        {
            if (particles.particleCount == 0) return;
            int count = particles.GetCustomParticleData(liveCustom2, ParticleSystemCustomData.Custom2);
            for (int i = 0; i < count; i++) liveCustom2[i] = new Vector4(state.row, 0, 0, 0);
            particles.SetCustomParticleData(liveCustom2, ParticleSystemCustomData.Custom2);
        }

        void CaptureNativeStates()
        {
            foreach (var source in sources)
            {
                var renderer = source.renderer;
                if (source.particles == null) source.particles = renderer.GetComponent<ParticleSystem>();
                var customData = source.particles.customData;
                var state = source.native = new NativeState { material = renderer.sharedMaterial, customDataEnabled = customData.enabled };
                renderer.GetActiveVertexStreams(state.streams);
                state.custom2Mode = customData.GetMode(ParticleSystemCustomData.Custom2);
                state.custom2Components = customData.GetVectorComponentCount(ParticleSystemCustomData.Custom2);
                for (int i = 0; i < 4; i++) state.custom2[i] = customData.GetVector(ParticleSystemCustomData.Custom2, i);
                var sheet = source.particles.textureSheetAnimation;
                var proxyMapping = new Vector4(0, 0, 1, 1);
                if (sheet.enabled && sheet.mode == ParticleSystemAnimationMode.Sprites && sheet.spriteCount > 0)
                {
                    state.sprites = new Sprite[sheet.spriteCount];
                    for (int i = 0; i < state.sprites.Length; i++) state.sprites[i] = sheet.GetSprite(i);
                    state.proxies = new Sprite[state.sprites.Length];
                    if (!ParticleAtlasSourceTable.TryCreateProxies(state.sprites, state.proxies, out proxyMapping)) state.proxies = null;
                }
                state.row = ParticleAtlasSourceTable.Register(source, proxyMapping);
            }
        }

        void ExitNative()
        {
            foreach (var source in sources)
            {
                var state = source?.native;
                if (state == null) continue;
                var customData = source.particles.customData;
                customData.SetMode(ParticleSystemCustomData.Custom2, state.custom2Mode);
                customData.SetVectorComponentCount(ParticleSystemCustomData.Custom2, state.custom2Components);
                for (int i = 0; i < 4; i++) customData.SetVector(ParticleSystemCustomData.Custom2, i, state.custom2[i]);
                customData.enabled = state.customDataEnabled;
                if (state.sprites != null)
                {
                    var sheet = source.particles.textureSheetAnimation;
                    for (int i = 0; i < state.sprites.Length; i++) sheet.SetSprite(i, state.sprites[i]);
                }
                source.renderer.SetActiveVertexStreams(state.streams);
                source.renderer.sharedMaterial = state.material;
                source.renderer.enabled = false;
            }
            nativeActive = false;
        }

        int CompareSources(Source a, Source b)
        {
            int order = a.renderer.sortingOrder.CompareTo(b.renderer.sortingOrder);
            if (order != 0) return order;
            var am = a.trails ? a.renderer.trailMaterial : a.renderer.sharedMaterial;
            var bm = b.trails ? b.renderer.trailMaterial : b.renderer.sharedMaterial;
            order = am.renderQueue.CompareTo(bm.renderQueue);
            if (order != 0) return order;
            Vector3 av = a.renderer.bounds.center - sortingCamera.transform.position;
            Vector3 bv = b.renderer.bounds.center - sortingCamera.transform.position;
            var mode = sortingCamera.transparencySortMode;
            Vector3 axis = sortingCamera.transparencySortAxis;
            if (mode == TransparencySortMode.Default)
            {
                mode = GraphicsSettings.transparencySortMode;
                axis = GraphicsSettings.transparencySortAxis;
            }
            bool orthographic = mode == TransparencySortMode.Orthographic
                || (mode == TransparencySortMode.Default && sortingCamera.orthographic);
            if (mode != TransparencySortMode.CustomAxis) axis = sortingCamera.transform.forward;
            bool axial = orthographic || mode == TransparencySortMode.CustomAxis;
            float ad = (axial ? Vector3.Dot(av, axis) : av.magnitude) + a.renderer.sortingFudge;
            float bd = (axial ? Vector3.Dot(bv, axis) : bv.magnitude) + b.renderer.sortingFudge;
            order = bd.CompareTo(ad);
            if (order != 0) return order;
            order = (IsAdditive(a.blend) ? 1 : 0).CompareTo(IsAdditive(b.blend) ? 1 : 0);
            if (additiveBeforeAlphaAtSameDepth) order = -order;
            return order != 0 ? order : Array.IndexOf(sources, a).CompareTo(Array.IndexOf(sources, b));
        }

        protected override void EnsureSources()
        {
            foreach (var source in sources)
                if (source != null) EnsureSource(source.renderer, ref source.particles, ref source.baked);
        }

        protected override void ReleaseSources()
        {
            foreach (var source in sources) if (source != null) Release(source.baked);
        }

        static bool IsAdditive(float blend) => Mathf.RoundToInt(blend) == (int)ParticleAtlasBlend.Additive;

        protected override void AppendGeometry(Camera camera, Matrix4x4 worldToLocal)
        {
            sortingCamera = camera;
            if (sourceComparison == null) sourceComparison = CompareSources;
            ordered.Clear();
            foreach (var source in sources) if (source != null && source.renderer != null) ordered.Add(source);
            ordered.Sort(sourceComparison);
            foreach (var source in ordered)
            {
                if (!BakeSource(source.renderer, source.particles, source.baked, camera, source.trails)) continue;
                int start = positions.Count;
                Vector4 tint = source.tint;
                for (int i = 0; i < sourcePositions.Count; i++)
                {
                    positions.Add(worldToLocal.MultiplyPoint3x4(sourcePositions[i]));
                    colors.Add(sourceColors.Count > i ? sourceColors[i] : Color.white);
                    Vector4 texcoord = sourceUv0.Count > i ? sourceUv0[i] : Vector4.zero;
                    uv[0].Add(new Vector4(texcoord.x * source.mainST.x + source.mainST.z, texcoord.y * source.mainST.y + source.mainST.w,
                        source.blend, source.sampling));
                    uv[1].Add(source.mainRect);
                    uv[2].Add(tint);
                }
                foreach (int index in sourceIndices) indices.Add(start + index);
            }
        }
    }
}
