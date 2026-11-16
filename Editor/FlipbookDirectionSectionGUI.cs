using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace KingdomTD.Flipbook.Editor
{
    /// <summary>
    /// 资产 Inspector 的“朝向”区域：方向模板、罗盘示意和方向表。
    /// </summary>
    internal sealed class FlipbookDirectionSectionGUI
    {
        private const float CompassHeight = 164f;
        private const float DotRadius = 6f;
        private const float RowMarkerWidth = 16f;
        private const float AngleWidth = 56f;
        private const float RemoveButtonWidth = 22f;

        private static readonly GUIContent PresetLabel = new GUIContent("方向模板",
            "选择模板后会自动生成方向表；同名方向已配置的起始帧会保留。");
        private static readonly GUIContent AddDirectionLabel = new GUIContent("添加方向");
        private static readonly GUIContent RemoveDirectionLabel = new GUIContent("✕", "删除此方向");

        private readonly List<GUIContent> _sourceLabels = new List<GUIContent>();
        private readonly List<(int Source, bool FlipX)> _sourceValues = new List<(int, bool)>();

        public int SelectedDirection { get; set; }

        public event Action SelectionChanged;

        public void Draw(SerializedObject serializedAsset, List<FlipbookDirectionEditModel> directions,
            List<Action> pendingActions)
        {
            FlipbookInspectorStyles.SectionHeader("朝向",
                "游戏中调用 SetFacing / SetFacingWorld 设置角色朝向，组件会自动选择角度最接近的方向播放。");

            int presetIndex = FlipbookDirectionPresets.FindMatchingPreset(directions);
            EditorGUI.BeginChangeCheck();
            int selectedPreset = EditorGUILayout.Popup(PresetLabel, presetIndex, FlipbookDirectionPresets.Options);
            if (EditorGUI.EndChangeCheck() && selectedPreset != presetIndex &&
                selectedPreset != FlipbookDirectionPresets.CustomIndex)
            {
                bool confirm = presetIndex == FlipbookDirectionPresets.CustomIndex;
                pendingActions.Add(() =>
                {
                    if (confirm && !EditorUtility.DisplayDialog("替换方向表",
                            $"将使用「{FlipbookDirectionPresets.Options[selectedPreset].text}」替换当前自定义的方向表。\n" +
                            "同名方向已配置的起始帧会保留。", "替换", "取消"))
                    {
                        return;
                    }

                    FlipbookDirectionEditing.ApplyPreset(serializedAsset, selectedPreset);
                    SelectedDirection = 0;
                    SelectionChanged?.Invoke();
                });
            }

            if (directions.Count == 0)
            {
                EditorGUILayout.LabelField("当前所有动画只使用一段帧。角色需要按朝向显示不同画面时，请选择方向模板。",
                    FlipbookInspectorStyles.Hint);
                return;
            }

            SelectedDirection = Mathf.Clamp(SelectedDirection, 0, directions.Count - 1);
            DrawCompass(directions);
            EditorGUILayout.LabelField("● 自己绘制    ○ 镜像/复用（虚线指向帧来源）    点击圆点可在预览中查看",
                FlipbookInspectorStyles.CenteredMiniLabel);
            DrawTableHeader();
            for (int i = 0; i < directions.Count; i++)
            {
                DrawRow(serializedAsset, directions, i, pendingActions);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(AddDirectionLabel, EditorStyles.miniButton, GUILayout.Width(80f)))
                {
                    pendingActions.Add(() =>
                        FlipbookDirectionEditing.Modify(serializedAsset, FlipbookDirectionEditing.AddDirection));
                }
            }
        }

        private void DrawCompass(List<FlipbookDirectionEditModel> directions)
        {
            Rect area = GUILayoutUtility.GetRect(0f, CompassHeight, GUILayout.ExpandWidth(true));
            Vector2 center = area.center;
            float radius = Mathf.Min(area.height * 0.5f - 28f, 60f);
            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 &&
                area.Contains(currentEvent.mousePosition))
            {
                for (int i = 0; i < directions.Count; i++)
                {
                    if (Vector2.Distance(currentEvent.mousePosition, GetDotPosition(center, radius, directions[i])) <=
                        DotRadius * 2f)
                    {
                        Select(i);
                        currentEvent.Use();
                        break;
                    }
                }
            }

            if (currentEvent.type != EventType.Repaint)
            {
                return;
            }

            Color previousColor = Handles.color;
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            Handles.DrawWireDisc(center, Vector3.forward, radius);
            Handles.DrawLine(center + Vector2.left * 6f, center + Vector2.right * 6f);
            Handles.DrawLine(center + Vector2.up * 6f, center + Vector2.down * 6f);
            GUI.Label(new Rect(center.x - 40f, center.y + 8f, 80f, 14f), "角色", FlipbookInspectorStyles.CenteredMiniLabel);

            for (int i = 0; i < directions.Count; i++)
            {
                FlipbookDirectionEditModel direction = directions[i];
                Vector2 position = GetDotPosition(center, radius, direction);
                if (!direction.HasOwnFrames && direction.SourceDirection < directions.Count)
                {
                    Handles.color = new Color(FlipbookInspectorStyles.BorrowedFramesColor.r,
                        FlipbookInspectorStyles.BorrowedFramesColor.g, FlipbookInspectorStyles.BorrowedFramesColor.b,
                        0.6f);
                    Handles.DrawDottedLine(position,
                        GetDotPosition(center, radius, directions[direction.SourceDirection]), 3f);
                }
            }

            for (int i = 0; i < directions.Count; i++)
            {
                FlipbookDirectionEditModel direction = directions[i];
                Vector2 position = GetDotPosition(center, radius, direction);
                if (direction.HasOwnFrames)
                {
                    Handles.color = FlipbookInspectorStyles.OwnFramesColor;
                    Handles.DrawSolidDisc(position, Vector3.forward, DotRadius);
                }
                else
                {
                    Handles.color = FlipbookInspectorStyles.BorrowedFramesColor;
                    Handles.DrawWireDisc(position, Vector3.forward, DotRadius, 2f);
                }

                if (i == SelectedDirection)
                {
                    Handles.color = FlipbookInspectorStyles.SelectionColor;
                    Handles.DrawWireDisc(position, Vector3.forward, DotRadius + 3f, 1.5f);
                }

                Vector2 labelOffset = GetDirectionVector(direction.Angle) * (DotRadius + 14f);
                GUI.Label(new Rect(position.x + labelOffset.x - 30f, position.y + labelOffset.y - 8f, 60f, 16f),
                    GetDisplayName(direction, i), FlipbookInspectorStyles.CenteredMiniLabel);
            }

            Handles.color = previousColor;
        }

        private static void DrawTableHeader()
        {
            Rect row = EditorGUILayout.GetControlRect();
            GetRowRects(row, out _, out Rect nameRect, out Rect angleRect, out Rect sourceRect, out _);
            GUI.Label(nameRect, "名称", EditorStyles.miniBoldLabel);
            GUI.Label(angleRect, "角度 (°)", EditorStyles.miniBoldLabel);
            GUI.Label(sourceRect, new GUIContent("帧来源", "自己绘制：美术提供这个方向的帧。\n" +
                                                         "镜像：水平翻转另一个方向的帧。\n复用：直接使用另一个方向的帧。"),
                EditorStyles.miniBoldLabel);
        }

        private void DrawRow(SerializedObject serializedAsset, List<FlipbookDirectionEditModel> directions, int index,
            List<Action> pendingActions)
        {
            FlipbookDirectionEditModel direction = directions[index];
            SerializedProperty element = serializedAsset.FindProperty("_directions").GetArrayElementAtIndex(index);
            Rect row = EditorGUILayout.GetControlRect();
            GetRowRects(row, out Rect markerRect, out Rect nameRect, out Rect angleRect, out Rect sourceRect,
                out Rect removeRect);

            if (index == SelectedDirection)
            {
                EditorGUI.DrawRect(new Rect(row.x - 2f, row.y - 1f, row.width + 4f, row.height + 2f),
                    new Color(0.24f, 0.49f, 0.91f, 0.18f));
            }

            Color previousColor = GUI.contentColor;
            GUI.contentColor = direction.HasOwnFrames
                ? FlipbookInspectorStyles.OwnFramesColor
                : FlipbookInspectorStyles.BorrowedFramesColor;
            if (GUI.Button(markerRect, direction.HasOwnFrames ? "●" : "○", EditorStyles.label))
            {
                Select(index);
            }

            GUI.contentColor = previousColor;

            EditorGUI.PropertyField(nameRect, element.FindPropertyRelative("Name"), GUIContent.none);
            SerializedProperty angle = element.FindPropertyRelative("Angle");
            EditorGUI.BeginChangeCheck();
            float newAngle = EditorGUI.DelayedFloatField(angleRect, angle.floatValue);
            if (EditorGUI.EndChangeCheck())
            {
                angle.floatValue = Mathf.Repeat(newAngle, 360f);
            }

            BuildSourceOptions(directions, index);
            int current = _sourceValues.IndexOf((direction.SourceDirection, direction.FlipX));
            if (current < 0)
            {
                current = _sourceValues.Count;
                _sourceLabels.Add(new GUIContent($"无效来源 #{direction.SourceDirection}"));
                _sourceValues.Add((direction.SourceDirection, direction.FlipX));
            }

            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.Popup(sourceRect, current, _sourceLabels.ToArray());
            if (EditorGUI.EndChangeCheck() && selected != current)
            {
                (int source, bool flipX) = _sourceValues[selected];
                pendingActions.Add(() => FlipbookDirectionEditing.Modify(serializedAsset,
                    list => FlipbookDirectionEditing.SetSource(list, index, source, flipX)));
            }

            if (GUI.Button(removeRect, RemoveDirectionLabel, EditorStyles.miniButton))
            {
                pendingActions.Add(() =>
                {
                    FlipbookDirectionEditing.Modify(serializedAsset,
                        list => FlipbookDirectionEditing.RemoveDirection(list, index));
                    SelectionChanged?.Invoke();
                });
            }
        }

        private void BuildSourceOptions(List<FlipbookDirectionEditModel> directions, int index)
        {
            _sourceLabels.Clear();
            _sourceValues.Clear();
            _sourceLabels.Add(new GUIContent("自己绘制"));
            _sourceValues.Add((-1, false));
            if (directions[index].HasOwnFrames && directions[index].FlipX)
            {
                _sourceLabels.Add(new GUIContent("自己绘制（翻转）"));
                _sourceValues.Add((-1, true));
            }

            for (int pass = 0; pass < 2; pass++)
            {
                bool flipX = pass == 0;
                for (int i = 0; i < directions.Count; i++)
                {
                    if (i == index || !directions[i].HasOwnFrames)
                    {
                        continue;
                    }

                    string name = GetDisplayName(directions[i], i);
                    _sourceLabels.Add(new GUIContent(flipX ? $"镜像 {name}" : $"复用 {name}（不翻转）"));
                    _sourceValues.Add((i, flipX));
                }
            }
        }

        private void Select(int index)
        {
            SelectedDirection = index;
            SelectionChanged?.Invoke();
            GUI.changed = true;
        }

        private static void GetRowRects(Rect row, out Rect markerRect, out Rect nameRect, out Rect angleRect,
            out Rect sourceRect, out Rect removeRect)
        {
            const float spacing = 4f;
            markerRect = new Rect(row.x, row.y, RowMarkerWidth, row.height);
            removeRect = new Rect(row.xMax - RemoveButtonWidth, row.y, RemoveButtonWidth, row.height);
            float remaining = removeRect.x - markerRect.xMax - spacing * 3f - AngleWidth;
            float nameWidth = Mathf.Clamp(remaining * 0.35f, 40f, 120f);
            nameRect = new Rect(markerRect.xMax + spacing, row.y, nameWidth, row.height);
            angleRect = new Rect(nameRect.xMax + spacing, row.y, AngleWidth, row.height);
            sourceRect = new Rect(angleRect.xMax + spacing, row.y, removeRect.x - spacing - angleRect.xMax - spacing,
                row.height);
        }

        private static Vector2 GetDotPosition(Vector2 center, float radius, FlipbookDirectionEditModel direction)
        {
            return center + GetDirectionVector(direction.Angle) * radius;
        }

        private static Vector2 GetDirectionVector(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), -Mathf.Sin(radians));
        }

        internal static string GetDisplayName(FlipbookDirectionEditModel direction, int index)
        {
            return string.IsNullOrEmpty(direction.Name) ? $"#{index}" : direction.Name;
        }
    }
}
