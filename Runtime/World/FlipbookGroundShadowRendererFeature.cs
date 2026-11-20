using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KingdomTD.Flipbook
{
    /// <summary>
    /// 可选：为某个 URP Renderer 自定义地面阴影的绘制时机和层。没有添加时，地面阴影会自动以默认设置绘制。
    /// </summary>
    [DisallowMultipleRendererFeature]
    public sealed class FlipbookGroundShadowRendererFeature : ScriptableRendererFeature
    {
        [Tooltip("只能在 AfterRenderingOpaques 到 BeforeRenderingTransparents 之间：影子要画在不透明地面之后、透明物体之前，超出范围会被夹回。")]
        [SerializeField] private RenderPassEvent _renderPassEvent = FlipbookGroundShadowRenderPass.EarliestEvent;
        [Tooltip("只绘制这些层上角色的地面阴影。")]
        [SerializeField] private LayerMask _layerMask = ~0;

        private FlipbookGroundShadowRenderPass _pass;

        public override void Create()
        {
            _pass = new FlipbookGroundShadowRenderPass(_renderPassEvent) { LayerMask = _layerMask };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            FlipbookGroundShadowRendering.ClaimByFeature(renderer);
            renderer.EnqueuePass(_pass);
        }
    }
}
