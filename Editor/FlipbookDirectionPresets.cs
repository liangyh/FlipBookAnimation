using System.Collections.Generic;
using UnityEngine;

namespace KingdomTD.Flipbook.Editor
{
    internal static class FlipbookDirectionPresets
    {
        public const int SingleIndex = 0;

        public static readonly GUIContent[] Options =
        {
            new GUIContent("单方向", "不区分朝向，所有动画只用一段帧。"),
            new GUIContent("左右（画右侧，左侧镜像）"),
            new GUIContent("4 方向（画 3 个，W 镜像 E）"),
            new GUIContent("8 方向（画 5 个，左侧 3 个镜像）"),
            new GUIContent("自定义")
        };

        public static int CustomIndex => Options.Length - 1;

        private static readonly (string Name, float Angle, int Source, bool FlipX)[][] Presets =
        {
            new (string, float, int, bool)[0],
            new[]
            {
                ("Right", 0f, -1, false),
                ("Left", 180f, 0, true)
            },
            new[]
            {
                ("E", 0f, -1, false),
                ("N", 90f, -1, false),
                ("W", 180f, 0, true),
                ("S", 270f, -1, false)
            },
            new[]
            {
                ("E", 0f, -1, false),
                ("NE", 45f, -1, false),
                ("N", 90f, -1, false),
                ("NW", 135f, 1, true),
                ("W", 180f, 0, true),
                ("SW", 225f, 7, true),
                ("S", 270f, -1, false),
                ("SE", 315f, -1, false)
            }
        };

        public static List<FlipbookDirectionEditModel> Create(int presetIndex)
        {
            var preset = Presets[presetIndex];
            List<FlipbookDirectionEditModel> result = new List<FlipbookDirectionEditModel>(preset.Length);
            for (int i = 0; i < preset.Length; i++)
            {
                result.Add(new FlipbookDirectionEditModel
                {
                    Name = preset[i].Name,
                    Angle = preset[i].Angle,
                    SourceDirection = preset[i].Source,
                    FlipX = preset[i].FlipX
                });
            }

            return result;
        }

        public static int FindMatchingPreset(IReadOnlyList<FlipbookDirectionEditModel> directions)
        {
            for (int presetIndex = 0; presetIndex < Presets.Length; presetIndex++)
            {
                var preset = Presets[presetIndex];
                if (preset.Length != directions.Count)
                {
                    continue;
                }

                bool matches = true;
                for (int i = 0; i < preset.Length && matches; i++)
                {
                    FlipbookDirectionEditModel direction = directions[i];
                    matches = direction.Name == preset[i].Name &&
                              Mathf.Approximately(direction.Angle, preset[i].Angle) &&
                              direction.SourceDirection == preset[i].Source &&
                              direction.FlipX == preset[i].FlipX;
                }

                if (matches)
                {
                    return presetIndex;
                }
            }

            return CustomIndex;
        }
    }
}
