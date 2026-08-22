using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KingdomTD.Flipbook.Editor
{
    internal static class FlipbookAssetCreationMenu
    {
        private const string MenuPath =
            "Assets/Create/KingdomTD/Flipbook Animation Asset + Material";

        [MenuItem(MenuPath, false, 210)]
        private static void CreateFromSelectedTexture()
        {
            if (!TryGetSelectedTexture(out Texture2D texture, out string texturePath))
            {
                return;
            }

            string destinationFolder = Path.GetDirectoryName(texturePath)?.Replace('\\', '/');
            if (!FlipbookAssetFactory.TryCreate(texture, destinationFolder,
                    out FlipbookAnimationAsset animationAsset, out _, out string error))
            {
                EditorUtility.DisplayDialog("创建 Flipbook 资产失败", error, "确定");
                return;
            }

            Selection.activeObject = animationAsset;
            EditorGUIUtility.PingObject(animationAsset);
            EditorApplication.RepaintProjectWindow();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateCreateFromSelectedTexture()
        {
            return TryGetSelectedTexture(out _, out string texturePath) &&
                   texturePath.StartsWith("Assets/", StringComparison.Ordinal);
        }

        internal static bool TryGetSelectedTexture(out Texture2D texture, out string texturePath)
        {
            texture = Selection.activeObject as Texture2D;
            texturePath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (texture == null && Selection.activeObject is Sprite)
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }

            return texture != null && !string.IsNullOrEmpty(texturePath);
        }
    }
}
