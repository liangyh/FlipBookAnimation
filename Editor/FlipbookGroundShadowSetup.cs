using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KingdomTD.Flipbook.Editor
{
    /// <summary>
    /// 检查项目各画质等级使用的 URP 资产是否都添加了 <see cref="FlipbookGroundShadowRendererFeature"/>。
    /// </summary>
    internal static class FlipbookGroundShadowSetup
    {
        /// <summary>
        /// 返回没有任何 Renderer 启用该 Feature 的 URP 资产名称，用顿号连接；全部配置好或项目不用 URP 时返回 null。
        /// </summary>
        public static string FindPipelinesMissingFeature()
        {
            HashSet<UniversalRenderPipelineAsset> visited = new HashSet<UniversalRenderPipelineAsset>();
            List<string> missing = null;
            Check(GraphicsSettings.defaultRenderPipeline, visited, ref missing);
            for (int i = 0; i < QualitySettings.count; i++)
            {
                Check(QualitySettings.GetRenderPipelineAssetAt(i), visited, ref missing);
            }

            return missing == null ? null : string.Join("、", missing);
        }

        private static void Check(RenderPipelineAsset asset, HashSet<UniversalRenderPipelineAsset> visited,
            ref List<string> missing)
        {
            if (!(asset is UniversalRenderPipelineAsset pipeline) || !visited.Add(pipeline) || HasFeature(pipeline))
            {
                return;
            }

            missing ??= new List<string>();
            missing.Add(pipeline.name);
        }

        private static bool HasFeature(UniversalRenderPipelineAsset pipeline)
        {
            foreach (ScriptableRendererData data in pipeline.rendererDataList)
            {
                if (data == null)
                {
                    continue;
                }

                foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                {
                    if (feature is FlipbookGroundShadowRendererFeature && feature.isActive)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
