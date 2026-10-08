using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PixelPerfect
{
    public static class CameraSetupURP
    {
        public static PixelPerfectCamera ConfigureCamera(Camera camera, int refResX, int refResY, int targetPPU)
        {
            if (!camera.TryGetComponent(out PixelPerfectCamera pp))
                pp = camera.gameObject.AddComponent<PixelPerfectCamera>();

            camera.orthographic = true;
            camera.orthographicSize = refResY * 0.5f / targetPPU;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = false;
            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

            pp.assetsPPU = targetPPU;
            pp.refResolutionX = refResX;
            pp.refResolutionY = refResY;
            pp.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            pp.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;

            return pp;
        }

        public static void SetManualPixelSnapSpacing(Camera camera)
        {
            UnityEngine.U2D.PixelPerfectRendering.pixelSnapSpacing = camera.orthographicSize * 2f / camera.pixelHeight;
        }
    }
}
