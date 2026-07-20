using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace OblastZero.Rendering
{
    /// <summary>
    /// Custom ScriptableRenderPass using Unity 6 Render Graph API to share the camera depth buffer
    /// between the 3D opaque pass and a 2D sprite-lit pass. This is the architectural foundation
    /// for Oblast Zero's 2.5D hybrid rendering — 2D UI/sprite elements correctly Z-occlude against
    /// 3D scavenge geometry without resorting to camera stacking.
    ///
    /// Pipeline order:
    ///   1. URP renders 3D opaque geometry (writes color + depth)
    ///   2. THIS PASS loads the depth buffer with RenderBufferLoadAction.Load
    ///   3. 2D sprite shaders sample the loaded depth, producing correct occlusion
    ///   4. Final composite to camera target
    /// </summary>
    public class HybridDepthRenderPass : ScriptableRenderPass
    {
        private const string PASS_NAME = "OblastZero.HybridDepthPass";
        private readonly HybridDepthSettings _settings;
        private readonly ProfilingSampler _profilingSampler;

        // Shader tag IDs the 2D sprite pass will match against.
        // Sprites in the bunker UI should use shaders that declare these LightModes.
        private static readonly ShaderTagId[] kShaderTags = new[]
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("Universal2D"),
            new ShaderTagId("SRPDefaultUnlit"),
        };

        public HybridDepthRenderPass(HybridDepthSettings settings)
        {
            _settings = settings;
            renderPassEvent = settings.renderPassEvent;
            _profilingSampler = new ProfilingSampler(PASS_NAME);

            if (_settings.enableDebugLogging)
            {
                Debug.Log($"[{PASS_NAME}] Constructed at renderPassEvent={settings.renderPassEvent}");
            }
        }

        /// <summary>
        /// Data passed into the Render Graph execution closure.
        /// All resources the pass touches are declared here so the graph can schedule dependencies.
        /// </summary>
        private class PassData
        {
            public TextureHandle cameraColor;
            public TextureHandle cameraDepth;
            public RendererListHandle rendererList;
            public bool debugLog;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Pull camera resources from the frame's UniversalResourceData.
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();
            var renderingData = frameData.Get<UniversalRenderingData>();

            if (!resourceData.activeColorTexture.IsValid() || !resourceData.activeDepthTexture.IsValid())
            {
                if (_settings.enableDebugLogging)
                {
                    Debug.LogWarning($"[{PASS_NAME}] Active color/depth textures invalid this frame. Skipping pass.");
                }
                return;
            }

            // Build the renderer list that produces the 2D sprite draws.
            // SortingCriteria.CommonTransparent ensures back-to-front draw order needed for blending,
            // while depth-testing the shared buffer enforces correct Z occlusion against 3D geometry.
            var sortFlags = SortingCriteria.CommonTransparent;
            var drawingSettings = RenderingUtils.CreateDrawingSettings(
                new System.Collections.Generic.List<ShaderTagId>(kShaderTags),
                renderingData,
                cameraData,
                lightData,
                sortFlags);

            var filterSettings = new FilteringSettings(
                _settings.renderQueueType == RenderQueueType.Transparent
                    ? RenderQueueRange.transparent
                    : RenderQueueRange.opaque,
                _settings.spriteLayerMask);

            var rendererListParams = new RendererListParams(
                renderingData.cullResults,
                drawingSettings,
                filterSettings);

            var rendererListHandle = renderGraph.CreateRendererList(rendererListParams);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(PASS_NAME, out var passData, _profilingSampler))
            {
                passData.cameraColor = resourceData.activeColorTexture;
                passData.cameraDepth = resourceData.activeDepthTexture;
                passData.rendererList = rendererListHandle;
                passData.debugLog = _settings.enableDebugLogging;

                // CRITICAL: Load the existing depth buffer. This is the line that makes hybrid rendering work.
                // Without it, the 2D pass writes to a fresh depth buffer and 3D occlusion is lost.
                builder.SetRenderAttachment(passData.cameraColor, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(passData.cameraDepth, AccessFlags.ReadWrite);

                builder.UseRendererList(passData.rendererList);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    if (data.debugLog)
                    {
                        Debug.Log($"[{PASS_NAME}] Executing — drawing hybrid sprites against shared depth buffer.");
                    }

                    ctx.cmd.DrawRendererList(data.rendererList);
                });
            }
        }
    }
}
