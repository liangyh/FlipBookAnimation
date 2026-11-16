using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace KingdomTD.Flipbook.Editor
{
    /// <summary>
    /// 方向表的结构性修改（套用模板、增删方向、修改帧来源）。修改后自动按方向身份重建各动画的
    /// DirectionStartFrames，使用者无需关心其索引。
    /// </summary>
    internal static class FlipbookDirectionEditing
    {
        private const int SingleDirectionId = -100;

        public static List<FlipbookDirectionEditModel> Read(SerializedObject serializedAsset)
        {
            SerializedProperty directions = serializedAsset.FindProperty("_directions");
            List<FlipbookDirectionEditModel> result = new List<FlipbookDirectionEditModel>(directions.arraySize);
            for (int i = 0; i < directions.arraySize; i++)
            {
                SerializedProperty direction = directions.GetArrayElementAtIndex(i);
                result.Add(new FlipbookDirectionEditModel
                {
                    Id = i,
                    Name = direction.FindPropertyRelative("Name").stringValue,
                    Angle = direction.FindPropertyRelative("Angle").floatValue,
                    SourceDirection = direction.FindPropertyRelative("SourceDirection").intValue,
                    FlipX = direction.FindPropertyRelative("FlipX").boolValue
                });
            }

            return result;
        }

        public static void Modify(SerializedObject serializedAsset,
            Action<List<FlipbookDirectionEditModel>> modification)
        {
            List<FlipbookDirectionEditModel> before = Read(serializedAsset);
            List<List<KeyValuePair<int, int>>> clipFrames = CaptureClipFrames(serializedAsset, before);
            List<FlipbookDirectionEditModel> after = new List<FlipbookDirectionEditModel>(before.Count);
            for (int i = 0; i < before.Count; i++)
            {
                after.Add(before[i].Clone());
            }

            modification(after);
            Write(serializedAsset, after, clipFrames);
        }

        public static void ApplyPreset(SerializedObject serializedAsset, int presetIndex)
        {
            Modify(serializedAsset, directions =>
            {
                List<FlipbookDirectionEditModel> preset = FlipbookDirectionPresets.Create(presetIndex);
                for (int i = 0; i < preset.Count; i++)
                {
                    FlipbookDirectionEditModel existing = directions.Find(direction =>
                        string.Equals(direction.Name, preset[i].Name, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        continue;
                    }

                    preset[i].Id = existing.Id;
                    if (!existing.HasOwnFrames && existing.SourceDirection < directions.Count)
                    {
                        preset[i].FallbackId = directions[existing.SourceDirection].Id;
                    }
                }

                directions.Clear();
                directions.AddRange(preset);
            });
        }

        public static void AddDirection(List<FlipbookDirectionEditModel> directions)
        {
            directions.Add(new FlipbookDirectionEditModel
            {
                Name = CreateUniqueName(directions),
                Angle = FindLargestGapAngle(directions)
            });
        }

        public static void RemoveDirection(List<FlipbookDirectionEditModel> directions, int index)
        {
            const int PendingSource = int.MinValue;
            FlipbookDirectionEditModel removed = directions[index];
            directions.RemoveAt(index);
            int removedSource = removed.SourceDirection > index ? removed.SourceDirection - 1 : removed.SourceDirection;
            for (int i = 0; i < directions.Count; i++)
            {
                FlipbookDirectionEditModel direction = directions[i];
                if (direction.SourceDirection == index)
                {
                    direction.SourceDirection = PendingSource;
                }
                else if (direction.SourceDirection > index)
                {
                    direction.SourceDirection--;
                }
            }

            // 借用被删方向的项：被删方向使用自己的帧时，第一个借用者接管这些帧（保留它的翻转，画面不变），
            // 其余借用者改为借用接管者；被删方向本身是借用时，改为借用它的来源。
            int heir = -1;
            for (int i = 0; i < directions.Count; i++)
            {
                FlipbookDirectionEditModel direction = directions[i];
                if (direction.SourceDirection != PendingSource)
                {
                    continue;
                }

                if (!removed.HasOwnFrames)
                {
                    direction.SourceDirection = removedSource;
                }
                else if (heir < 0)
                {
                    heir = i;
                    direction.SourceDirection = -1;
                    direction.FallbackId = removed.Id;
                }
                else
                {
                    direction.SourceDirection = heir;
                }
            }
        }

        public static void SetSource(List<FlipbookDirectionEditModel> directions, int index, int sourceDirection,
            bool flipX)
        {
            FlipbookDirectionEditModel direction = directions[index];
            if (sourceDirection >= 0)
            {
                for (int i = 0; i < directions.Count; i++)
                {
                    if (i != index && directions[i].SourceDirection == index)
                    {
                        directions[i].SourceDirection = sourceDirection;
                    }
                }
            }
            else if (!direction.HasOwnFrames && direction.SourceDirection < directions.Count)
            {
                direction.FallbackId = directions[direction.SourceDirection].Id;
            }

            direction.SourceDirection = sourceDirection;
            direction.FlipX = flipX;
        }

        private static List<List<KeyValuePair<int, int>>> CaptureClipFrames(SerializedObject serializedAsset,
            List<FlipbookDirectionEditModel> directions)
        {
            SerializedProperty clips = serializedAsset.FindProperty("_clips");
            List<List<KeyValuePair<int, int>>> result = new List<List<KeyValuePair<int, int>>>(clips.arraySize);
            for (int clipIndex = 0; clipIndex < clips.arraySize; clipIndex++)
            {
                SerializedProperty clip = clips.GetArrayElementAtIndex(clipIndex);
                int startFrame = clip.FindPropertyRelative("StartFrame").intValue;
                SerializedProperty startFrames = clip.FindPropertyRelative("DirectionStartFrames");
                List<KeyValuePair<int, int>> frames = new List<KeyValuePair<int, int>>();
                if (directions.Count == 0)
                {
                    frames.Add(new KeyValuePair<int, int>(SingleDirectionId, startFrame));
                }

                int authoredIndex = 0;
                for (int i = 0; i < directions.Count; i++)
                {
                    if (!directions[i].HasOwnFrames)
                    {
                        continue;
                    }

                    int frame = authoredIndex < startFrames.arraySize
                        ? startFrames.GetArrayElementAtIndex(authoredIndex).intValue
                        : startFrame;
                    frames.Add(new KeyValuePair<int, int>(directions[i].Id, frame));
                    authoredIndex++;
                }

                result.Add(frames);
            }

            return result;
        }

        private static void Write(SerializedObject serializedAsset, List<FlipbookDirectionEditModel> directions,
            List<List<KeyValuePair<int, int>>> clipFrames)
        {
            SerializedProperty directionsProperty = serializedAsset.FindProperty("_directions");
            directionsProperty.arraySize = directions.Count;
            List<FlipbookDirectionEditModel> authored = new List<FlipbookDirectionEditModel>();
            for (int i = 0; i < directions.Count; i++)
            {
                FlipbookDirectionEditModel direction = directions[i];
                SerializedProperty element = directionsProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Name").stringValue = direction.Name;
                element.FindPropertyRelative("Angle").floatValue = direction.Angle;
                element.FindPropertyRelative("SourceDirection").intValue = direction.SourceDirection;
                element.FindPropertyRelative("FlipX").boolValue = direction.FlipX;
                if (direction.HasOwnFrames)
                {
                    authored.Add(direction);
                }
            }

            SerializedProperty clips = serializedAsset.FindProperty("_clips");
            for (int clipIndex = 0; clipIndex < clips.arraySize && clipIndex < clipFrames.Count; clipIndex++)
            {
                SerializedProperty clip = clips.GetArrayElementAtIndex(clipIndex);
                SerializedProperty startFrame = clip.FindPropertyRelative("StartFrame");
                SerializedProperty startFrames = clip.FindPropertyRelative("DirectionStartFrames");
                int frameCount = Mathf.Max(1, clip.FindPropertyRelative("FrameCount").intValue);
                List<KeyValuePair<int, int>> frames = clipFrames[clipIndex];
                int baseFrame = frames.Count > 0 ? frames[0].Value : startFrame.intValue;

                if (directions.Count == 0)
                {
                    startFrame.intValue = baseFrame;
                    startFrames.arraySize = 0;
                    continue;
                }

                startFrames.arraySize = authored.Count;
                for (int i = 0; i < authored.Count; i++)
                {
                    int frame = TryFind(frames, authored[i].Id, out int value) ||
                                TryFind(frames, authored[i].FallbackId, out value) ||
                                (i == 0 && TryFind(frames, SingleDirectionId, out value))
                        ? value
                        : baseFrame + i * frameCount;
                    startFrames.GetArrayElementAtIndex(i).intValue = frame;
                }

                if (authored.Count > 0)
                {
                    startFrame.intValue = startFrames.GetArrayElementAtIndex(0).intValue;
                }
            }
        }

        private static bool TryFind(List<KeyValuePair<int, int>> frames, int id, out int frame)
        {
            frame = 0;
            if (id == FlipbookDirectionEditModel.NewDirectionId)
            {
                return false;
            }

            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i].Key == id)
                {
                    frame = frames[i].Value;
                    return true;
                }
            }

            return false;
        }

        private static string CreateUniqueName(List<FlipbookDirectionEditModel> directions)
        {
            for (int index = directions.Count + 1; ; index++)
            {
                string name = $"D{index}";
                if (!directions.Exists(direction => direction.Name == name))
                {
                    return name;
                }
            }
        }

        private static float FindLargestGapAngle(List<FlipbookDirectionEditModel> directions)
        {
            if (directions.Count == 0)
            {
                return 0f;
            }

            List<float> angles = new List<float>(directions.Count);
            for (int i = 0; i < directions.Count; i++)
            {
                angles.Add(Mathf.Repeat(directions[i].Angle, 360f));
            }

            angles.Sort();
            float bestAngle = angles[0] + 180f;
            float bestGap = -1f;
            for (int i = 0; i < angles.Count; i++)
            {
                float next = i + 1 < angles.Count ? angles[i + 1] : angles[0] + 360f;
                float gap = next - angles[i];
                if (gap > bestGap)
                {
                    bestGap = gap;
                    bestAngle = angles[i] + gap * 0.5f;
                }
            }

            return Mathf.Repeat(Mathf.Round(bestAngle), 360f);
        }
    }
}
