using UnityEditor;
using UnityEngine;

namespace KingdomTD.Flipbook.Editor
{
    internal static class FlipbookInspectorStyles
    {
        public static readonly Color OwnFramesColor = new Color(0.33f, 0.65f, 1f, 1f);
        public static readonly Color BorrowedFramesColor = new Color(1f, 0.67f, 0.25f, 1f);
        public static readonly Color SelectionColor = new Color(1f, 1f, 1f, 0.9f);
        public static readonly Color ThumbnailBackground = new Color(0.12f, 0.12f, 0.12f, 1f);
        public static readonly Color ErrorTextColor = new Color(1f, 0.42f, 0.38f, 1f);

        private static GUIStyle _sectionTitle;
        private static GUIStyle _hint;
        private static GUIStyle _centeredMiniLabel;
        private static GUIStyle _errorMiniLabel;

        public static GUIStyle Hint => _hint ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            normal = { textColor = EditorStyles.centeredGreyMiniLabel.normal.textColor }
        };

        public static GUIStyle CenteredMiniLabel => _centeredMiniLabel ??=
            new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };

        public static GUIStyle ErrorMiniLabel => _errorMiniLabel ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = ErrorTextColor }
        };

        private static GUIStyle SectionTitle => _sectionTitle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13
        };

        public static void SectionHeader(string title, string hint = null)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(title, SectionTitle);
            Rect line = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, new Color(0.5f, 0.5f, 0.5f, 0.4f));
            if (!string.IsNullOrEmpty(hint))
            {
                EditorGUILayout.LabelField(hint, Hint);
            }

            EditorGUILayout.Space(2f);
        }

        public static void DrawFrameThumbnail(Rect rect, FlipbookAnimationAsset animationAsset, int frame,
            bool flipX = false)
        {
            EditorGUI.DrawRect(rect, ThumbnailBackground);
            if (animationAsset == null || animationAsset.MainTexture == null || frame < 0 ||
                frame >= animationAsset.TotalFrameCount)
            {
                return;
            }

            Rect uvRect = animationAsset.GetFrameUvRect(frame);
            if (flipX)
            {
                uvRect = new Rect(uvRect.xMax, uvRect.y, -uvRect.width, uvRect.height);
            }

            GUI.DrawTextureWithTexCoords(FitAspect(rect, animationAsset.FramePixelSize),
                animationAsset.MainTexture, uvRect, true);
        }

        public static Rect FitAspect(Rect area, Vector2 frameSize)
        {
            if (frameSize.x <= 0f || frameSize.y <= 0f || area.width <= 0f || area.height <= 0f)
            {
                return area;
            }

            float frameAspect = frameSize.x / frameSize.y;
            if (area.width / area.height > frameAspect)
            {
                float width = area.height * frameAspect;
                return new Rect(area.x + (area.width - width) * 0.5f, area.y, width, area.height);
            }

            float height = area.width / frameAspect;
            return new Rect(area.x, area.y + (area.height - height) * 0.5f, area.width, height);
        }
    }
}
