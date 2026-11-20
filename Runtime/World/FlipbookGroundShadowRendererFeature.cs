using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KingdomTD.Flipbook
{
    /// <summary>
    /// 把 Flipbook 地面阴影加入 URP Renderer。使用地面阴影的项目需要在每个用到的 Renderer 资产中添加一次。
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
            renderer.EnqueuePass(_pass);
        }
    }
}
