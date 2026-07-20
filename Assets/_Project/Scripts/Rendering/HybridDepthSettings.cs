using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OblastZero.Rendering
{
    /// <summary>
    /// Inspector-exposed settings for the HybridDepthRendererFeature.
    /// Drives when and how the depth-sharing pass executes between 3D and 2D rendering.
    /// </summary>
    [Serializable]
    public class HybridDepthSettings
    {
        [Tooltip("When in the render pipeline the hybrid pass executes. AfterRenderingOpaques is correct for 99% of cases.")]
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        [Tooltip("Render queue range that the 2D sprite pass should consider. Transparent only by default.")]
        public RenderQueueType renderQueueType = RenderQueueType.Transparent;

        [Tooltip("Layer mask for 2D content that should participate in depth sharing. Set to a 'HybridSprite' layer in production.")]
        public LayerMask spriteLayerMask = -1;

        [Tooltip("Enable verbose debug logging for the hybrid pass. Disable in shipping builds.")]
        public bool enableDebugLogging = false;
    }
}
