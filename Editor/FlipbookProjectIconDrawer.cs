using System;
using UnityEditor;
using UnityEngine;

namespace KingdomTD.Flipbook.Editor
{
    [InitializeOnLoad]
    internal static class FlipbookProjectIconDrawer
    {
        private const float ListIconSize = 16f;
        private const float ListViewHeight = 20f;

        static FlipbookProjectIconDrawer()
        {
            EditorApplication.projectWindowItemOnGUI += DrawAnimationAssetIcon;
        }

        private static void DrawAnimationAssetIcon(string guid, Rect selectionRect)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                AssetDatabase.GetMainAssetTypeAtPath(assetPath) != typeof(FlipbookAnimationAsset))
            {
                return;
            }

            Texture2D icon = FlipbookEditorIcons.AnimationAssetIcon;
            if (icon == null)
            {
                return;
            }

            GUI.DrawTexture(GetIconRect(selectionRect), icon, ScaleMode.ScaleToFit, true);
        }

        internal static Rect GetIconRect(Rect selectionRect)
        {
            if (selectionRect.height <= ListViewHeight)
            {
                float size = Mathf.Min(ListIconSize, selectionRect.height);
                return new Rect(selectionRect.x, selectionRect.y + (selectionRect.height - size) * 0.5f,
                    size, size);
            }

            return new Rect(selectionRect.x, selectionRect.y, selectionRect.width, selectionRect.width);
        }
    }
}
