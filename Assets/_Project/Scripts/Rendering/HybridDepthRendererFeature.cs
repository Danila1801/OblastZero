using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OblastZero.Rendering
{
    /// <summary>
    /// URP RendererFeature wrapper for the HybridDepthRenderPass.
    /// Add this to your URP Renderer asset (Settings > Renderer > Add Renderer Feature)
    /// to enable depth-shared 2D/3D rendering project-wide.
    /// </summary>
    [DisallowMultipleRendererFeature("Oblast Zero - Hybrid Depth Pass")]
    public class HybridDepthRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private HybridDepthSettings settings = new HybridDepthSettings();

        private HybridDepthRenderPass _pass;

        public override void Create()
        {
            _pass = new HybridDepthRenderPass(settings);

            if (settings.enableDebugLogging)
            {
                Debug.Log("[HybridDepthRendererFeature] Created and registered.");
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null)
            {
                Debug.LogError("[HybridDepthRendererFeature] Pass is null — Create() failed. Skipping enqueue.");
                return;
            }

            // Don't run the pass on preview cameras (Inspector previews, scene thumbnails) — wasted work.
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
            {
                return;
            }

            renderer.EnqueuePass(_pass);
        }
    }
}
