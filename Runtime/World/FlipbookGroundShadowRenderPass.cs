using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace KingdomTD.Flipbook
{
    /// <summary>
    /// 在一次独立的绘制中画出所有 Flipbook 地面阴影（Shader Pass 的 LightMode 为 <c>FlipbookGroundShadow</c>）。
    /// 角色本体随 URP 的不透明绘制合批，影子在这里集中合批，两者不会交替提交。
    /// </summary>
    public sealed class FlipbookGroundShadowRenderPass : ScriptableRenderPass
    {
        public const string LightModeTag = "FlipbookGroundShadow";
        // Shadows must land on the opaque ground and stay under transparent effects.
        internal const RenderPassEvent EarliestEvent = RenderPassEvent.AfterRenderingOpaques;
        internal const RenderPassEvent LatestEvent = RenderPassEvent.BeforeRenderingTransparents;
        private const string PassName = "Flipbook Ground Shadows";
        private static readonly ShaderTagId ShaderTag = new ShaderTagId(LightModeTag);
        private static readonly HashSet<int> CamerasWarnedWithoutStencil = new HashSet<int>();

        private FilteringSettings _filtering = new FilteringSettings(RenderQueueRange.opaque);

        public FlipbookGroundShadowRenderPass(RenderPassEvent passEvent = EarliestEvent)
        {
            renderPassEvent = (RenderPassEvent)Mathf.Clamp((int)passEvent, (int)EarliestEvent, (int)LatestEvent);
            profilingSampler = new ProfilingSampler(PassName);
        }

        public LayerMask LayerMask
        {
            get => _filtering.layerMask;
            set => _filtering.layerMask = value;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData camera = frameData.Get<UniversalCameraData>();
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            UniversalRenderingData rendering = frameData.Get<UniversalRenderingData>();
            UniversalLightData light = frameData.Get<UniversalLightData>();
            WarnIfDepthHasNoStencil(renderGraph.GetRenderTargetInfo(resources.activeDepthTexture).format, camera.camera);
            DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(ShaderTag, rendering, camera, light,
                SortingCriteria.OptimizeStateChanges);
            RendererListParams list = new RendererListParams(rendering.cullResults, drawing, _filtering);

            using (IRasterRenderGraphBuilder builder =
                   renderGraph.AddRasterRenderPass(PassName, out PassData data, profilingSampler))
            {
                data.Shadows = renderGraph.CreateRendererList(list);
                builder.UseRendererList(data.Shadows);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                // Shadows test depth and write the stencil bit that keeps overlaps from darkening twice.
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                    context.cmd.DrawRendererList(pass.Shadows));
            }
        }

        /// <summary>
        /// 深度附件没有模板位时 GPU 跳过模板测试，重叠的半透明影子会再次变深。每台相机只提示一次。
        /// </summary>
        private static void WarnIfDepthHasNoStencil(GraphicsFormat depthFormat, Camera camera)
        {
            if (depthFormat == GraphicsFormat.None || GraphicsFormatUtility.IsStencilFormat(depthFormat) ||
                camera == null || !CamerasWarnedWithoutStencil.Add(camera.GetInstanceID()))
            {
                return;
            }

            Debug.LogWarning($"{camera.name}: 深度附件 {depthFormat} 没有模板位，重叠的半透明地面阴影会叠深。" +
                             "相机直接输出到 RenderTexture 时，请使用带 S8 的深度格式，例如 D24_UNorm_S8_UInt 或 D32_SFloat_S8_UInt。",
                camera);
        }

        private sealed class PassData
        {
            public RendererListHandle Shadows;
        }
    }
}
