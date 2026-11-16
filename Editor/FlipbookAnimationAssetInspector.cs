using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace KingdomTD.Flipbook.Editor
{
    [CustomEditor(typeof(FlipbookAnimationAsset))]
    public sealed class FlipbookAnimationAssetInspector : UnityEditor.Editor
    {
        private static readonly (string Name, float Angle, int Source, bool FlipX)[] TwoDirectionPreset =
        {
            ("Right", 0f, -1, false),
            ("Left", 180f, 0, true)
        };

        private static readonly (string Name, float Angle, int Source, bool FlipX)[] FourDirectionPreset =
        {
            ("E", 0f, -1, false),
            ("N", 90f, -1, false),
            ("W", 180f, 0, true),
            ("S", 270f, -1, false)
        };

        private static readonly (string Name, float Angle, int Source, bool FlipX)[] EightDirectionPreset =
        {
            ("E", 0f, -1, false),
            ("NE", 45f, -1, false),
            ("N", 90f, -1, false),
            ("NW", 135f, 1, true),
            ("W", 180f, 0, true),
            ("SW", 225f, 7, true),
            ("S", 270f, -1, false),
            ("SE", 315f, -1, false)
        };

        private readonly List<string> _validationErrors = new List<string>();
        private int _previewClipIndex;
        private int _previewDirectionIndex;
        private int _directionFrameStride;
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

            DrawDirectionTools(animationAsset);
        }

        private void DrawDirectionTools(FlipbookAnimationAsset animationAsset)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("方向工具", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("方向预设");
                if (GUILayout.Button("单方向", EditorStyles.miniButtonLeft))
                {
                    ApplyDirectionPreset(null);
                }

                if (GUILayout.Button("左右", EditorStyles.miniButtonMid))
                {
                    ApplyDirectionPreset(TwoDirectionPreset);
                }

                if (GUILayout.Button("4 方向", EditorStyles.miniButtonMid))
                {
                    ApplyDirectionPreset(FourDirectionPreset);
                }

                if (GUILayout.Button("8 方向", EditorStyles.miniButtonRight))
                {
                    ApplyDirectionPreset(EightDirectionPreset);
                }
            }

            IReadOnlyList<FlipbookDirectionData> directions = animationAsset.Directions;
            if (directions.Count == 0)
            {
                EditorGUILayout.HelpBox("当前为单方向资产，所有动画使用 StartFrame。", MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox(BuildDirectionMappingText(directions), MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                _directionFrameStride = Mathf.Max(0, EditorGUILayout.IntField(
                    new GUIContent("方向帧步长", "相邻方向起始帧的间隔；0 表示使用各动画自己的 FrameCount。"),
                    _directionFrameStride));
                if (GUILayout.Button("填充所有动画", GUILayout.Width(100f)))
                {
                    FillDirectionStartFrames(animationAsset.AuthoredDirectionCount);
                }
            }
        }

        private void ApplyDirectionPreset((string Name, float Angle, int Source, bool FlipX)[] preset)
        {
            serializedObject.Update();
            SerializedProperty directions = serializedObject.FindProperty("_directions");
            directions.arraySize = preset?.Length ?? 0;
            for (int i = 0; i < directions.arraySize; i++)
            {
                SerializedProperty direction = directions.GetArrayElementAtIndex(i);
                direction.FindPropertyRelative("Name").stringValue = preset[i].Name;
                direction.FindPropertyRelative("Angle").floatValue = preset[i].Angle;
                direction.FindPropertyRelative("SourceDirection").intValue = preset[i].Source;
                direction.FindPropertyRelative("FlipX").boolValue = preset[i].FlipX;
            }

            serializedObject.ApplyModifiedProperties();
            _previewDirectionIndex = 0;
        }

        private void FillDirectionStartFrames(int authoredDirectionCount)
        {
            serializedObject.Update();
            SerializedProperty clips = serializedObject.FindProperty("_clips");
            for (int clipIndex = 0; clipIndex < clips.arraySize; clipIndex++)
            {
                SerializedProperty clip = clips.GetArrayElementAtIndex(clipIndex);
                int startFrame = clip.FindPropertyRelative("StartFrame").intValue;
                int stride = _directionFrameStride > 0
                    ? _directionFrameStride
                    : Mathf.Max(1, clip.FindPropertyRelative("FrameCount").intValue);
                SerializedProperty startFrames = clip.FindPropertyRelative("DirectionStartFrames");
                startFrames.arraySize = authoredDirectionCount;
                for (int direction = 0; direction < authoredDirectionCount; direction++)
                {
                    startFrames.GetArrayElementAtIndex(direction).intValue = startFrame + direction * stride;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static string BuildDirectionMappingText(IReadOnlyList<FlipbookDirectionData> directions)
        {
            StringBuilder authored = new StringBuilder("DirectionStartFrames 索引：");
            StringBuilder borrowed = new StringBuilder();
            int authoredIndex = 0;
            for (int i = 0; i < directions.Count; i++)
            {
                FlipbookDirectionData direction = directions[i];
                if (direction == null)
                {
                    continue;
                }

                string name = GetDirectionName(direction, i);
                if (direction.HasOwnFrames)
                {
                    authored.Append(authoredIndex == 0 ? " " : "，").Append($"[{authoredIndex}] {name}");
                    authoredIndex++;
                    continue;
                }

                string sourceName = direction.SourceDirection < directions.Count
                    ? GetDirectionName(directions[direction.SourceDirection], direction.SourceDirection)
                    : $"#{direction.SourceDirection}";
                borrowed.Append(borrowed.Length == 0 ? "\n复用：" : "，")
                    .Append($"{name} ← {sourceName}{(direction.FlipX ? "（翻转）" : string.Empty)}");
            }

            return authored.Append(borrowed).ToString();
        }

        private static string GetDirectionName(FlipbookDirectionData direction, int index)
        {
            return direction == null || string.IsNullOrEmpty(direction.Name) ? $"#{index}" : direction.Name;
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
                    directionNames[i] = GetDirectionName(directions[i], i);
                }

                _previewDirectionIndex = Mathf.Clamp(_previewDirectionIndex, 0, directionNames.Length - 1);
                _previewDirectionIndex = EditorGUILayout.Popup(_previewDirectionIndex, directionNames,
                    GUILayout.Width(60f));
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

            int directionIndex = Mathf.Clamp(_previewDirectionIndex, 0, animationAsset.DirectionCount - 1);
            FlipbookDirectionResolver.ResolveSource(animationAsset.Directions, directionIndex,
                out int authoredDirection, out bool flipX);
            Rect uvRect = animationAsset.GetFrameUvRect(clip.GetStartFrame(authoredDirection) + localFrame);
            if (flipX)
            {
                uvRect = new Rect(uvRect.xMax, uvRect.y, -uvRect.width, uvRect.height);
            }

            Rect drawArea = GetAspectFittedRect(previewArea, animationAsset.FramePixelSize);
            EditorGUI.DrawRect(previewArea, new Color(0.12f, 0.12f, 0.12f, 1f));
            GUI.DrawTextureWithTexCoords(drawArea, animationAsset.MainTexture, uvRect, true);
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
