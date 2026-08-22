using UnityEditor;
using UnityEngine;

namespace KingdomTD.Flipbook.Editor
{
    internal static class FlipbookEditorIcons
    {
        internal const string AnimationAssetIconPath =
            "Packages/com.kingdomtd.flipbook/Editor/Icons/FlipbookAnimationAssetIcon.png";

        private static Texture2D _animationAssetIcon;

        internal static Texture2D AnimationAssetIcon
        {
            get
            {
                _animationAssetIcon ??=
                    AssetDatabase.LoadAssetAtPath<Texture2D>(AnimationAssetIconPath);
                return _animationAssetIcon;
            }
        }
    }
}
