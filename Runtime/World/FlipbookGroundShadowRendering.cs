using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KingdomTD.Flipbook
{
    /// <summary>
    /// 没有添加 <see cref="FlipbookGroundShadowRendererFeature"/> 的 URP Renderer 也能画出地面阴影：
    /// 只要有角色开着地面阴影，每个相机开始渲染时就加入默认的影子 Pass。
    /// 添加了 Feature 的 Renderer 改用 Feature 的设置，默认 Pass 在同一次渲染中让位。
    /// </summary>
    internal static class FlipbookGroundShadowRendering
    {
        private static readonly FlipbookGroundShadowRenderPass DefaultPass =
            new FlipbookGroundShadowRenderPass(yieldToFeature: true);
        private static ScriptableRenderer _featureRenderer;
        private static int _users;

        /// <summary>当前开着地面阴影的角色数；为 0 时不订阅相机渲染事件。</summary>
        internal static int UserCount => _users;

        /// <summary>角色开始绘制地面阴影时调用，与 <see cref="RemoveUser"/> 成对出现。</summary>
        public static void AddUser()
        {
            if (_users++ == 0)
            {
                RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            }
        }

        /// <summary>最后一个角色停止绘制地面阴影后注销，不再为任何相机提交默认 Pass。</summary>
        public static void RemoveUser()
        {
            if (_users > 0 && --_users == 0)
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
                _featureRenderer = null;
            }
        }

        /// <summary>
        /// Feature 在 <c>AddRenderPasses</c> 中调用。URP 对同一次相机渲染先触发 <c>beginCameraRendering</c>，
        /// 再调用 Feature，最后才录制 Pass，所以默认 Pass 录制时就知道本次是否由 Feature 接管。
        /// </summary>
        public static void ClaimByFeature(ScriptableRenderer renderer)
        {
            _featureRenderer = renderer;
        }

        public static bool IsClaimedByFeature(ScriptableRenderer renderer)
        {
            return renderer != null && ReferenceEquals(_featureRenderer, renderer);
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            ScriptableRenderer renderer = GetRenderer(camera);
            if (renderer == null)
            {
                return;
            }

            // Cameras render one at a time, so only the current camera's claim matters.
            _featureRenderer = null;
            renderer.EnqueuePass(DefaultPass);
        }

        private static ScriptableRenderer GetRenderer(Camera camera)
        {
            if (camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
            {
                return cameraData.scriptableRenderer;
            }

            // Cameras without URP data use the pipeline's default renderer.
            UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            return pipeline != null ? pipeline.scriptableRenderer : null;
        }
    }
}
