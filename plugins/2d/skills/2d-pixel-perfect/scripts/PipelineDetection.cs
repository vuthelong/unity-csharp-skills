using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PixelPerfect
{
    public enum PipelineType { BuiltIn, URP, HDRP, Custom }

    public static class PipelineDetection
    {
        public const string UrpCameraTypeName =
            "UnityEngine.Rendering.Universal.PixelPerfectCamera, Unity.RenderPipelines.Universal.2D.Runtime";

        public const string BuiltInCameraTypeName =
            "UnityEngine.U2D.PixelPerfectCamera, Unity.2D.PixelPerfect";

        public static PipelineType DetectPipeline()
        {
            var rpa = GraphicsSettings.currentRenderPipeline;
            if (rpa == null) return PipelineType.BuiltIn;

            var fullName = rpa.GetType().FullName ?? string.Empty;
            if (fullName.Contains("Universal")) return PipelineType.URP;
            if (fullName.Contains("HighDefinition")) return PipelineType.HDRP;
            return PipelineType.Custom;
        }

        public static Type GetPixelPerfectCameraType(PipelineType pipeline)
        {
            if (pipeline == PipelineType.URP)
            {
                var urpType = Type.GetType(UrpCameraTypeName);
                if (urpType != null) return urpType;
                Debug.LogWarning("URP detected but its PixelPerfectCamera was not found. Is the 2D Renderer set up?");
                return null;
            }

            if (pipeline == PipelineType.BuiltIn)
                return Type.GetType(BuiltInCameraTypeName);

            return null;
        }

        public static bool HasPipelineMismatch(Camera camera)
        {
            if (DetectPipeline() != PipelineType.URP) return false;

            var builtInType = Type.GetType(BuiltInCameraTypeName);
            return builtInType != null && camera.TryGetComponent(builtInType, out _);
        }
    }
}
