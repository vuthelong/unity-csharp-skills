using UnityEngine;
using UnityEngine.U2D;

namespace PixelPerfect
{
    public static class CameraSetupBuiltIn
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
            pp.pixelSnapping = true;
            pp.upscaleRT = false;
            pp.cropFrameX = true;
            pp.cropFrameY = true;
            pp.stretchFill = false;

            return pp;
        }
    }
}
