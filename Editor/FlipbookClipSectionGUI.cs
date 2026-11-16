using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace KingdomTD.Flipbook.Editor
{
    /// <summary>
    /// 资产 Inspector 的“动画”区域：默认动画、各动画参数，以及按方向名称显示的起始帧。
    /// </summary>
    internal sealed class FlipbookClipSectionGUI
    {
        private const float ThumbnailSize = 36f;
        private const float HeaderButtonWidth = 22f;
        private const float DefaultFrameRate = 12f;

        private static readonly GUIContent DefaultAnimationLabel = new GUIContent("默认动画",
            "Play() 未传入动画名称时播放的动画。");
        private static readonly GUIContent NameLabel = new GUIContent("名称");
        private static readonly GUIContent FrameCountLabel = new GUIContent("帧数");
        private static readonly GUIContent FrameRateLabel = new GUIContent("帧率 (fps)");
        private static readonly GUIContent SpeedLabel = new GUIContent("速度倍率");
        private static readonly GUIContent StartFrameLabel = new GUIContent("起始帧", "纹理网格中的帧序号，从左到右、从上到下，从 0 开始。");
        private static readonly GUIContent EventsLabel = new GUIContent("帧事件",
            "帧号相对于本动画，从 0 开始；所有方向共享。");
        private static readonly GUIContent PackLabel = new GUIContent("紧密排列", "每个方向紧接上一个方向的最后一帧。");
        private static readonly GUIContent RowLabel = new GUIContent("每方向一行", "每个方向从纹理的新一行开始。");
        private static readonly GUIContent MoveUpLabel = new GUIContent("▲", "上移");
        private static readonly GUIContent MoveDownLabel = new GUIContent("▼", "下移");
        private static GUIContent _duplicateLabel;
        private static readonly GUIContent RemoveLabel = new GUIContent("✕", "删除");
        private static readonly GUIContent AddClipLabel = new GUIContent("添加动画");

        private static GUIContent DuplicateLabel => _duplicateLabel ??=
            new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Duplicate").image, "复制");

        private readonly List<int> _authoredDirections = new List<int>();
        private readonly List<string> _borrowerNames = new List<string>();

        public void Draw(SerializedObject serializedAsset, FlipbookAnimationAsset animationAsset,
            List<FlipbookDirectionEditModel> directions, List<Action> pendingActions)
        {
            FlipbookInspectorStyles.SectionHeader("动画");
            SerializedProperty clips = serializedAsset.FindProperty("_clips");
            DrawDefaultAnimationPopup(serializedAsset.FindProperty("_defaultAnimationName"), clips);
            CollectAuthoredDirections(directions);

            for (int i = 0; i < clips.arraySize; i++)
            {
                DrawClip(clips, i, animationAsset, directions, pendingActions);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(AddClipLabel, EditorStyles.miniButton, GUILayout.Width(80f)))
                {
                    int columns = Mathf.Max(1, serializedAsset.FindProperty("_columns").intValue);
                    int authoredCount = directions.Count == 0 ? 0 : _authoredDirections.Count;
                    pendingActions.Add(() => AddClip(clips, columns, authoredCount));
                }
            }
        }

        private static void DrawDefaultAnimationPopup(SerializedProperty defaultAnimationName,
            SerializedProperty clips)
        {
            List<string> names = new List<string>(clips.arraySize);
            for (int i = 0; i < clips.arraySize; i++)
            {
                string name = clips.GetArrayElementAtIndex(i).FindPropertyRelative("AnimationName").stringValue;
                if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                {
                    names.Add(name);
                }
            }

            if (names.Count == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Popup(DefaultAnimationLabel, 0, new[] { new GUIContent("（暂无动画）") });
                }

                return;
            }

            List<GUIContent> options = new List<GUIContent>(names.Count + 1);
            for (int i = 0; i < names.Count; i++)
            {
                options.Add(new GUIContent(names[i]));
            }

            string current = defaultAnimationName.stringValue;
            int index = names.IndexOf(current);
            if (index < 0)
            {
                index = options.Count;
                options.Add(new GUIContent(string.IsNullOrEmpty(current) ? "（未设置）" : $"缺失：{current}"));
            }

            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup(DefaultAnimationLabel, index, options.ToArray());
            if (EditorGUI.EndChangeCheck() && selected < names.Count)
            {
                defaultAnimationName.stringValue = names[selected];
            }
        }

        private void DrawClip(SerializedProperty clips, int index, FlipbookAnimationAsset animationAsset,
            List<FlipbookDirectionEditModel> directions, List<Action> pendingActions)
        {
            SerializedProperty clip = clips.GetArrayElementAtIndex(index);
            SerializedProperty animationName = clip.FindPropertyRelative("AnimationName");
            SerializedProperty frameCount = clip.FindPropertyRelative("FrameCount");
            SerializedProperty frameRate = clip.FindPropertyRelative("FrameRate");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Rect header = EditorGUILayout.GetControlRect();
                Rect buttons = new Rect(header.xMax - HeaderButtonWidth * 4f, header.y, HeaderButtonWidth * 4f,
                    header.height);
                Rect foldoutRect = new Rect(header.x + 12f, header.y, buttons.x - header.x - 16f, header.height);
                string title = string.IsNullOrEmpty(animationName.stringValue) ? "（未命名）" : animationName.stringValue;
                clip.isExpanded = EditorGUI.Foldout(foldoutRect, clip.isExpanded,
                    $"{title}    {frameCount.intValue} 帧 · {frameRate.floatValue:0.##} fps", true,
                    EditorStyles.foldout);
                DrawHeaderButtons(buttons, clips, index, pendingActions);

                if (!clip.isExpanded)
                {
                    return;
                }

                EditorGUILayout.PropertyField(animationName, NameLabel);
                EditorGUILayout.PropertyField(frameCount, FrameCountLabel);
                EditorGUILayout.PropertyField(frameRate, FrameRateLabel);
                EditorGUILayout.PropertyField(clip.FindPropertyRelative("Speed"), SpeedLabel);
                if (frameCount.intValue < 1)
                {
                    frameCount.intValue = 1;
                }

                if (directions.Count == 0)
                {
                    DrawSingleStartFrame(clip, animationAsset);
                }
                else
                {
                    DrawDirectionStartFrames(clip, animationAsset, directions);
                }

                EditorGUILayout.PropertyField(clip.FindPropertyRelative("Events"), EventsLabel, true);
            }
        }

        private static void DrawHeaderButtons(Rect area, SerializedProperty clips, int index,
            List<Action> pendingActions)
        {
            Rect button = new Rect(area.x, area.y, HeaderButtonWidth, area.height);
            using (new EditorGUI.DisabledScope(index == 0))
            {
                if (GUI.Button(button, MoveUpLabel, EditorStyles.miniButtonLeft))
                {
                    pendingActions.Add(() => clips.MoveArrayElement(index, index - 1));
                }
            }

            button.x += HeaderButtonWidth;
            using (new EditorGUI.DisabledScope(index == clips.arraySize - 1))
            {
                if (GUI.Button(button, MoveDownLabel, EditorStyles.miniButtonMid))
                {
                    pendingActions.Add(() => clips.MoveArrayElement(index, index + 1));
                }
            }

            button.x += HeaderButtonWidth;
            if (GUI.Button(button, DuplicateLabel, EditorStyles.miniButtonMid))
            {
                pendingActions.Add(() => DuplicateClip(clips, index));
            }

            button.x += HeaderButtonWidth;
            if (GUI.Button(button, RemoveLabel, EditorStyles.miniButtonRight))
            {
                pendingActions.Add(() => clips.DeleteArrayElementAtIndex(index));
            }
        }

        private static void DrawSingleStartFrame(SerializedProperty clip, FlipbookAnimationAsset animationAsset)
        {
            SerializedProperty startFrame = clip.FindPropertyRelative("StartFrame");
            int frameCount = clip.FindPropertyRelative("FrameCount").intValue;
            Rect row = EditorGUILayout.GetControlRect(false, ThumbnailSize);
            Rect fieldRow = new Rect(row.x, row.y + (row.height - EditorGUIUtility.singleLineHeight) * 0.5f,
                row.width - ThumbnailSize - 6f, EditorGUIUtility.singleLineHeight);
            Rect thumbnail = new Rect(row.xMax - ThumbnailSize, row.y, ThumbnailSize, ThumbnailSize);
            Rect fieldRect = EditorGUI.PrefixLabel(fieldRow, StartFrameLabel);
            Rect valueRect = new Rect(fieldRect.x, fieldRect.y, 60f, fieldRect.height);
            DrawFrameField(valueRect, startFrame);
            DrawRangeLabel(new Rect(valueRect.xMax + 6f, fieldRect.y, fieldRect.xMax - valueRect.xMax - 6f,
                fieldRect.height), startFrame.intValue, frameCount, animationAsset);
            FlipbookInspectorStyles.DrawFrameThumbnail(thumbnail, animationAsset, startFrame.intValue);
        }

        private void DrawDirectionStartFrames(SerializedProperty clip, FlipbookAnimationAsset animationAsset,
            List<FlipbookDirectionEditModel> directions)
        {
            SerializedProperty startFrame = clip.FindPropertyRelative("StartFrame");
            SerializedProperty startFrames = clip.FindPropertyRelative("DirectionStartFrames");
            int frameCount = clip.FindPropertyRelative("FrameCount").intValue;
            if (startFrames.arraySize != _authoredDirections.Count)
            {
                int previousCount = startFrames.arraySize;
                startFrames.arraySize = _authoredDirections.Count;
                for (int i = previousCount; i < startFrames.arraySize; i++)
                {
                    startFrames.GetArrayElementAtIndex(i).intValue = startFrame.intValue + i * frameCount;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent("各方向起始帧", "所有方向共享帧数、帧率与帧事件。"),
                    EditorStyles.miniBoldLabel);
                if (GUILayout.Button(PackLabel, EditorStyles.miniButtonLeft, GUILayout.Width(70f)))
                {
                    Arrange(startFrames, frameCount);
                }

                if (GUILayout.Button(RowLabel, EditorStyles.miniButtonRight, GUILayout.Width(70f)))
                {
                    int columns = animationAsset != null ? animationAsset.Columns : 1;
                    Arrange(startFrames, Mathf.CeilToInt((float)frameCount / columns) * columns);
                }
            }

            for (int i = 0; i < _authoredDirections.Count; i++)
            {
                int directionIndex = _authoredDirections[i];
                SerializedProperty element = startFrames.GetArrayElementAtIndex(i);
                Rect row = EditorGUILayout.GetControlRect(false, ThumbnailSize);
                Rect thumbnail = new Rect(row.x, row.y, ThumbnailSize, ThumbnailSize);
                float lineY = row.y + (row.height - EditorGUIUtility.singleLineHeight) * 0.5f;
                float lineHeight = EditorGUIUtility.singleLineHeight;
                Rect nameRect = new Rect(thumbnail.xMax + 6f, lineY, 56f, lineHeight);
                Rect valueRect = new Rect(nameRect.xMax + 2f, lineY, 56f, lineHeight);
                Rect infoRect = new Rect(valueRect.xMax + 6f, lineY, row.xMax - valueRect.xMax - 6f, lineHeight);

                FlipbookInspectorStyles.DrawFrameThumbnail(thumbnail, animationAsset, element.intValue);
                EditorGUI.LabelField(nameRect,
                    FlipbookDirectionSectionGUI.GetDisplayName(directions[directionIndex], directionIndex),
                    EditorStyles.boldLabel);
                DrawFrameField(valueRect, element);
                DrawRangeLabel(infoRect, element.intValue, frameCount, animationAsset,
                    BuildBorrowerText(directions, directionIndex));
            }

            if (startFrames.arraySize > 0 && startFrame.intValue != startFrames.GetArrayElementAtIndex(0).intValue)
            {
                startFrame.intValue = startFrames.GetArrayElementAtIndex(0).intValue;
            }
        }

        private static void DrawFrameField(Rect rect, SerializedProperty property)
        {
            EditorGUI.BeginChangeCheck();
            int value = EditorGUI.IntField(rect, property.intValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = Mathf.Max(0, value);
            }
        }

        private static void Arrange(SerializedProperty startFrames, int stride)
        {
            if (startFrames.arraySize == 0)
            {
                return;
            }

            int first = startFrames.GetArrayElementAtIndex(0).intValue;
            for (int i = 1; i < startFrames.arraySize; i++)
            {
                startFrames.GetArrayElementAtIndex(i).intValue = first + i * Mathf.Max(1, stride);
            }
        }

        private static void DrawRangeLabel(Rect rect, int startFrame, int frameCount,
            FlipbookAnimationAsset animationAsset, string suffix = null)
        {
            int lastFrame = startFrame + frameCount - 1;
            int totalFrameCount = animationAsset != null ? animationAsset.TotalFrameCount : int.MaxValue;
            bool valid = startFrame >= 0 && lastFrame < totalFrameCount;
            string text = valid
                ? $"第 {startFrame} – {lastFrame} 帧"
                : $"第 {startFrame} – {lastFrame} 帧，超出纹理（共 {totalFrameCount} 帧）";
            if (!string.IsNullOrEmpty(suffix))
            {
                text += $"   {suffix}";
            }

            GUI.Label(rect, text, valid ? FlipbookInspectorStyles.Hint : FlipbookInspectorStyles.ErrorMiniLabel);
        }

        private string BuildBorrowerText(List<FlipbookDirectionEditModel> directions, int sourceIndex)
        {
            _borrowerNames.Clear();
            for (int i = 0; i < directions.Count; i++)
            {
                if (directions[i].SourceDirection == sourceIndex)
                {
                    string name = FlipbookDirectionSectionGUI.GetDisplayName(directions[i], i);
                    _borrowerNames.Add(directions[i].FlipX ? $"{name} 镜像" : $"{name} 复用");
                }
            }

            if (_borrowerNames.Count == 0)
            {
                return null;
            }

            StringBuilder builder = new StringBuilder("（");
            for (int i = 0; i < _borrowerNames.Count; i++)
            {
                builder.Append(i == 0 ? string.Empty : "，").Append(_borrowerNames[i]);
            }

            return builder.Append("）").ToString();
        }

        private void CollectAuthoredDirections(List<FlipbookDirectionEditModel> directions)
        {
            _authoredDirections.Clear();
            for (int i = 0; i < directions.Count; i++)
            {
                if (directions[i].HasOwnFrames)
                {
                    _authoredDirections.Add(i);
                }
            }
        }

        private static void AddClip(SerializedProperty clips, int columns, int authoredDirectionCount)
        {
            int index = clips.arraySize;
            clips.arraySize++;
            SerializedProperty clip = clips.GetArrayElementAtIndex(index);
            clip.FindPropertyRelative("AnimationName").stringValue = CreateUniqueName(clips, "NewClip");
            clip.FindPropertyRelative("StartFrame").intValue = 0;
            clip.FindPropertyRelative("FrameCount").intValue = columns;
            clip.FindPropertyRelative("FrameRate").floatValue = DefaultFrameRate;
            clip.FindPropertyRelative("Speed").floatValue = 1f;
            clip.FindPropertyRelative("Events").arraySize = 0;
            SerializedProperty startFrames = clip.FindPropertyRelative("DirectionStartFrames");
            startFrames.arraySize = authoredDirectionCount;
            for (int i = 0; i < authoredDirectionCount; i++)
            {
                startFrames.GetArrayElementAtIndex(i).intValue = i * columns;
            }

            clip.isExpanded = true;
        }

        private static void DuplicateClip(SerializedProperty clips, int index)
        {
            clips.InsertArrayElementAtIndex(index);
            SerializedProperty copy = clips.GetArrayElementAtIndex(index + 1);
            SerializedProperty name = copy.FindPropertyRelative("AnimationName");
            name.stringValue = CreateUniqueName(clips, $"{name.stringValue}_Copy");
            copy.isExpanded = true;
        }

        private static string CreateUniqueName(SerializedProperty clips, string baseName)
        {
            for (int suffix = 0; ; suffix++)
            {
                string candidate = suffix == 0 ? baseName : $"{baseName}{suffix}";
                bool exists = false;
                for (int i = 0; i < clips.arraySize && !exists; i++)
                {
                    exists = clips.GetArrayElementAtIndex(i).FindPropertyRelative("AnimationName").stringValue ==
                             candidate;
                }

                if (!exists)
                {
                    return candidate;
                }
            }
        }
    }
}
