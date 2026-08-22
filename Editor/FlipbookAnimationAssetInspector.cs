using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace KingdomTD.Flipbook.Editor
{
    [CustomEditor(typeof(FlipbookAnimationAsset))]
    public sealed class FlipbookAnimationAssetInspector : UnityEditor.Editor
    {
        private readonly List<string> _validationErrors = new List<string>();
        private int _previewClipIndex;
        private bool _previewPlaying = true;
        private double _previewStartTime;

        private void OnEnable()
        {
            _previewStartTime = EditorApplication.timeSinceStartup;
            SynchronizeWorldMaterial((FlipbookAnimationAsset)target, false);
        }

        public override void OnInspectorGUI()
        {
            FlipbookAnimationAsset animationAsset = (FlipbookAnimationAsset)target;
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            bool inspectorChanged = EditorGUI.EndChangeCheck();
            if (inspectorChanged)
            {
                Material pendingMaterial = serializedObject.FindProperty("_worldMaterial").objectReferenceValue
                    as Material;
                if (pendingMaterial != null)
                {
                    Undo.RecordObject(pendingMaterial, "Synchronize Flipbook Material");
                }
            }

            bool propertiesChanged = serializedObject.ApplyModifiedProperties();
            if (propertiesChanged)
            {
                SynchronizeWorldMaterial(animationAsset, true);
            }

            animationAsset.GetValidationErrors(_validationErrors);
            for (int i = 0; i < _validationErrors.Count; i++)
            {
                EditorGUILayout.HelpBox(_validationErrors[i], MessageType.Error);
            }

            if (animationAsset.MainTexture != null)
            {
                Vector2 frameSize = animationAsset.FramePixelSize;
                EditorGUILayout.HelpBox(
                    $"总帧数：{animationAsset.TotalFrameCount}，单帧尺寸：{frameSize.x:0.##} × {frameSize.y:0.##}",
                    MessageType.Info);
            }
        }

        public override bool HasPreviewGUI()
        {
            FlipbookAnimationAsset animationAsset = (FlipbookAnimationAsset)target;
            return animationAsset.MainTexture != null && animationAsset.Clips.Count > 0;
        }

        public override void OnPreviewSettings()
        {
            FlipbookAnimationAsset animationAsset = (FlipbookAnimationAsset)target;
            if (animationAsset.Clips.Count == 0)
            {
                return;
            }

            string[] clipNames = new string[animationAsset.Clips.Count];
            for (int i = 0; i < clipNames.Length; i++)
            {
                clipNames[i] = animationAsset.Clips[i]?.AnimationName ?? $"Clip {i}";
            }

            _previewClipIndex = Mathf.Clamp(_previewClipIndex, 0, clipNames.Length - 1);
            int selectedIndex = EditorGUILayout.Popup(_previewClipIndex, clipNames, GUILayout.Width(120f));
            if (selectedIndex != _previewClipIndex)
            {
                _previewClipIndex = selectedIndex;
                _previewStartTime = EditorApplication.timeSinceStartup;
            }

            bool previewPlaying = GUILayout.Toggle(_previewPlaying, _previewPlaying ? "暂停" : "播放",
                EditorStyles.toolbarButton);
            if (previewPlaying != _previewPlaying)
            {
                _previewPlaying = previewPlaying;
                _previewStartTime = EditorApplication.timeSinceStartup;
            }
        }

        public override void OnPreviewGUI(Rect previewArea, GUIStyle background)
        {
            FlipbookAnimationAsset animationAsset = (FlipbookAnimationAsset)target;
            if (animationAsset.MainTexture == null || animationAsset.Clips.Count == 0)
            {
                return;
            }

            _previewClipIndex = Mathf.Clamp(_previewClipIndex, 0, animationAsset.Clips.Count - 1);
            FlipbookClipData clip = animationAsset.Clips[_previewClipIndex];
            if (clip == null || clip.FrameCount <= 0)
            {
                return;
            }

            int localFrame = 0;
            if (_previewPlaying)
            {
                double elapsed = EditorApplication.timeSinceStartup - _previewStartTime;
                localFrame = (int)(elapsed * clip.FrameRate * Mathf.Max(0.0001f, clip.Speed)) % clip.FrameCount;
            }

            Rect drawArea = GetAspectFittedRect(previewArea, animationAsset.FramePixelSize);
            EditorGUI.DrawRect(previewArea, new Color(0.12f, 0.12f, 0.12f, 1f));
            GUI.DrawTextureWithTexCoords(drawArea, animationAsset.MainTexture,
                animationAsset.GetFrameUvRect(clip.StartFrame + localFrame), true);
        }

        public override bool RequiresConstantRepaint()
        {
            return _previewPlaying;
        }

        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            Texture2D editorIcon = FlipbookEditorIcons.AnimationAssetIcon;
            if (editorIcon == null || width <= 0 || height <= 0)
            {
                return base.RenderStaticPreview(assetPath, subAssets, width, height);
            }

            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previousRenderTexture = RenderTexture.active;
            try
            {
                Graphics.Blit(editorIcon, renderTexture);
                RenderTexture.active = renderTexture;
                Texture2D staticPreview = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                staticPreview.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                staticPreview.Apply();
                return staticPreview;
            }
            finally
            {
                RenderTexture.active = previousRenderTexture;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private static void SynchronizeWorldMaterial(FlipbookAnimationAsset animationAsset, bool forceDirty)
        {
            if (animationAsset == null || animationAsset.WorldMaterial == null)
            {
                return;
            }

            bool materialChanged = animationAsset.SynchronizeWorldMaterial();
            if (materialChanged || forceDirty)
            {
                EditorUtility.SetDirty(animationAsset.WorldMaterial);
                SceneView.RepaintAll();
            }
        }

        private static Rect GetAspectFittedRect(Rect area, Vector2 frameSize)
        {
            if (frameSize.x <= 0f || frameSize.y <= 0f)
            {
                return area;
            }

            float frameAspect = frameSize.x / frameSize.y;
            float areaAspect = area.width / area.height;
            if (areaAspect > frameAspect)
            {
                float width = area.height * frameAspect;
                return new Rect(area.x + (area.width - width) * 0.5f, area.y, width, area.height);
            }

            float height = area.width / frameAspect;
            return new Rect(area.x, area.y + (area.height - height) * 0.5f, area.width, height);
        }
    }
}
