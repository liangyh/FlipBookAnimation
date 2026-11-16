using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace KingdomTD.Flipbook.Editor
{
    [CustomEditor(typeof(FlipbookAnimationAsset))]
    public sealed class FlipbookAnimationAssetInspector : UnityEditor.Editor
    {
        private static readonly GUIContent MainTextureLabel = new GUIContent("主纹理", "序列帧网格纹理。");
        private static readonly GUIContent EffectTextureLabel = new GUIContent("效果纹理（可选）",
            "场景渲染时可通过 SetEffectChangeRate 混合的扩展纹理。");
        private static readonly GUIContent WorldMaterialLabel = new GUIContent("场景材质",
            "使用 KingdomTD/Flipbook/World Shader 的共享材质，纹理与网格会自动同步。");
        private static readonly GUIContent GridLabel = new GUIContent("网格（列 × 行）",
            "帧顺序为从左到右、从上到下，从 0 开始编号。");

        private readonly List<string> _validationErrors = new List<string>();
        private readonly List<Action> _pendingActions = new List<Action>();
        private readonly FlipbookDirectionSectionGUI _directionSection = new FlipbookDirectionSectionGUI();
        private readonly FlipbookClipSectionGUI _clipSection = new FlipbookClipSectionGUI();
        private int _previewClipIndex;
        private bool _previewPlaying = true;
        private double _previewStartTime;

        private void OnEnable()
        {
            _previewStartTime = EditorApplication.timeSinceStartup;
            _directionSection.SelectionChanged += RestartPreview;
            SynchronizeWorldMaterial((FlipbookAnimationAsset)target, false);
        }

        private void OnDisable()
        {
            _directionSection.SelectionChanged -= RestartPreview;
        }

        public override void OnInspectorGUI()
        {
            FlipbookAnimationAsset animationAsset = (FlipbookAnimationAsset)target;
            serializedObject.Update();
            _pendingActions.Clear();

            DrawValidationErrors(animationAsset);

            EditorGUI.BeginChangeCheck();
            DrawTextureSection(animationAsset);
            bool textureChanged = EditorGUI.EndChangeCheck();

            List<FlipbookDirectionEditModel> directions = FlipbookDirectionEditing.Read(serializedObject);
            _directionSection.Draw(serializedObject, directions, _pendingActions);
            _clipSection.Draw(serializedObject, animationAsset, directions, _pendingActions);

            bool hasPendingActions = _pendingActions.Count > 0;
            for (int i = 0; i < _pendingActions.Count; i++)
            {
                _pendingActions[i]();
            }

            _pendingActions.Clear();
            if (textureChanged)
            {
                Material pendingMaterial = serializedObject.FindProperty("_worldMaterial").objectReferenceValue
                    as Material;
                if (pendingMaterial != null)
                {
                    Undo.RecordObject(pendingMaterial, "Synchronize Flipbook Material");
                }
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                SynchronizeWorldMaterial(animationAsset, textureChanged);
            }

            if (hasPendingActions)
            {
                GUIUtility.ExitGUI();
            }
        }

        private void DrawValidationErrors(FlipbookAnimationAsset animationAsset)
        {
            animationAsset.GetValidationErrors(_validationErrors);
            if (_validationErrors.Count == 0)
            {
                return;
            }

            StringBuilder message = new StringBuilder($"有 {_validationErrors.Count} 个问题需要处理：");
            for (int i = 0; i < _validationErrors.Count; i++)
            {
                message.Append("\n• ").Append(_validationErrors[i]);
            }

            EditorGUILayout.HelpBox(message.ToString(), MessageType.Error);
        }

        private void DrawTextureSection(FlipbookAnimationAsset animationAsset)
        {
            FlipbookInspectorStyles.SectionHeader("纹理与网格");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_mainTexture"), MainTextureLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_effectTexture"), EffectTextureLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_worldMaterial"), WorldMaterialLabel);

            SerializedProperty columns = serializedObject.FindProperty("_columns");
            SerializedProperty rows = serializedObject.FindProperty("_rows");
            Rect gridRect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), GridLabel);
            float fieldWidth = (gridRect.width - 20f) * 0.5f;
            Rect columnsRect = new Rect(gridRect.x, gridRect.y, fieldWidth, gridRect.height);
            Rect separatorRect = new Rect(columnsRect.xMax, gridRect.y, 20f, gridRect.height);
            Rect rowsRect = new Rect(separatorRect.xMax, gridRect.y, fieldWidth, gridRect.height);
            int indentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            DrawPositiveIntField(columnsRect, columns);
            GUI.Label(separatorRect, "×", FlipbookInspectorStyles.CenteredMiniLabel);
            DrawPositiveIntField(rowsRect, rows);
            EditorGUI.indentLevel = indentLevel;

            if (animationAsset.MainTexture != null)
            {
                Vector2 frameSize = animationAsset.FramePixelSize;
                EditorGUILayout.LabelField(
                    $"共 {animationAsset.TotalFrameCount} 帧 · 单帧 {frameSize.x:0.##} × {frameSize.y:0.##} px",
                    FlipbookInspectorStyles.Hint);
            }
        }

        private static void DrawPositiveIntField(Rect rect, SerializedProperty property)
        {
            EditorGUI.BeginChangeCheck();
            int value = EditorGUI.IntField(rect, property.intValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = Mathf.Max(1, value);
            }
        }

        private void RestartPreview()
        {
            _previewStartTime = EditorApplication.timeSinceStartup;
            Repaint();
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

            IReadOnlyList<FlipbookDirectionData> directions = animationAsset.Directions;
            if (directions.Count > 0)
            {
                string[] directionNames = new string[directions.Count];
                for (int i = 0; i < directionNames.Length; i++)
                {
                    FlipbookDirectionData direction = directions[i];
                    directionNames[i] = direction == null || string.IsNullOrEmpty(direction.Name)
                        ? $"#{i}"
                        : direction.Name;
                }

                int selectedDirection = Mathf.Clamp(_directionSection.SelectedDirection, 0, directionNames.Length - 1);
                int newDirection = EditorGUILayout.Popup(selectedDirection, directionNames, GUILayout.Width(60f));
                if (newDirection != _directionSection.SelectedDirection)
                {
                    _directionSection.SelectedDirection = newDirection;
                    Repaint();
                }
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

            int directionIndex = Mathf.Clamp(_directionSection.SelectedDirection, 0,
                animationAsset.DirectionCount - 1);
            FlipbookDirectionResolver.ResolveSource(animationAsset.Directions, directionIndex,
                out int authoredDirection, out bool flipX);
            Rect uvRect = animationAsset.GetFrameUvRect(clip.GetStartFrame(authoredDirection) + localFrame);
            if (flipX)
            {
                uvRect = new Rect(uvRect.xMax, uvRect.y, -uvRect.width, uvRect.height);
            }

            Rect drawArea = FlipbookInspectorStyles.FitAspect(previewArea, animationAsset.FramePixelSize);
            EditorGUI.DrawRect(previewArea, new Color(0.12f, 0.12f, 0.12f, 1f));
            GUI.DrawTextureWithTexCoords(drawArea, animationAsset.MainTexture, uvRect, true);
        }

        public override bool RequiresConstantRepaint()
        {
            return _previewPlaying;
        }

        public override Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height)
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
    }
}
