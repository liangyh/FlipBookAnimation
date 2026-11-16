using System.Collections.Generic;
using UnityEngine;

namespace KingdomTD.Flipbook
{
    public static class FlipbookDirectionResolver
    {
        private const float MinDirectionSqrMagnitude = 1e-8f;

        public static int GetAuthoredDirectionCount(IReadOnlyList<FlipbookDirectionData> directions)
        {
            if (directions == null || directions.Count == 0)
            {
                return 1;
            }

            int count = 0;
            for (int i = 0; i < directions.Count; i++)
            {
                if (IsAuthored(directions[i]))
                {
                    count++;
                }
            }

            return Mathf.Max(1, count);
        }

        public static int ResolveIndex(IReadOnlyList<FlipbookDirectionData> directions, float angle,
            int currentIndex = -1, float hysteresis = 0f)
        {
            if (directions == null || directions.Count == 0)
            {
                return 0;
            }

            int bestIndex = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < directions.Count; i++)
            {
                FlipbookDirectionData direction = directions[i];
                if (direction == null)
                {
                    continue;
                }

                float distance = Mathf.Abs(Mathf.DeltaAngle(angle, direction.Angle));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                return 0;
            }

            if (hysteresis > 0f && currentIndex >= 0 && currentIndex < directions.Count &&
                currentIndex != bestIndex && directions[currentIndex] != null)
            {
                float currentDistance = Mathf.Abs(Mathf.DeltaAngle(angle, directions[currentIndex].Angle));
                if (currentDistance - bestDistance < hysteresis)
                {
                    return currentIndex;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// 解析方向实际使用的帧来源。authoredDirection 是 <see cref="FlipbookClipData.DirectionStartFrames"/>
        /// 的索引（只统计使用自己帧的方向）。配置无效时返回 false 并回退到本方向。
        /// </summary>
        public static bool ResolveSource(IReadOnlyList<FlipbookDirectionData> directions, int directionIndex,
            out int authoredDirection, out bool flipX)
        {
            authoredDirection = 0;
            flipX = false;
            if (directions == null || directions.Count == 0)
            {
                return true;
            }

            if (directionIndex < 0 || directionIndex >= directions.Count)
            {
                return false;
            }

            FlipbookDirectionData direction = directions[directionIndex];
            if (direction == null)
            {
                return false;
            }

            flipX = direction.FlipX;
            int frameDirection = direction.HasOwnFrames ? directionIndex : direction.SourceDirection;
            bool valid = frameDirection < directions.Count && IsAuthored(directions[frameDirection]);
            if (!valid)
            {
                frameDirection = directionIndex;
            }

            authoredDirection = CountAuthoredBefore(directions, frameDirection);
            return valid;
        }

        public static bool TryGetAngle(Vector2 direction, out float angle)
        {
            if (direction.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                angle = 0f;
                return false;
            }

            angle = Mathf.Repeat(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, 360f);
            return true;
        }

        /// <summary>
        /// 将地面（法线为 groundNormal）上的世界方向转换为相对相机的屏幕朝向角度：
        /// 相机右侧为 0°，远离相机为 90°。相机水平旋转后结果依然正确。
        /// </summary>
        public static bool TryGetScreenAngle(Vector3 worldDirection, Transform view, Vector3 groundNormal,
            out float angle)
        {
            if (view == null)
            {
                return TryGetAngle(new Vector2(worldDirection.x, worldDirection.z), out angle);
            }

            Vector3 right = Vector3.ProjectOnPlane(view.right, groundNormal);
            if (right.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                right = Vector3.ProjectOnPlane(-Vector3.Cross(view.forward, groundNormal), groundNormal);
            }

            if (right.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                angle = 0f;
                return false;
            }

            right.Normalize();
            Vector3 forward = Vector3.Cross(right, groundNormal.normalized);
            return TryGetAngle(new Vector2(Vector3.Dot(worldDirection, right), Vector3.Dot(worldDirection, forward)),
                out angle);
        }

        private static bool IsAuthored(FlipbookDirectionData direction)
        {
            return direction != null && direction.HasOwnFrames;
        }

        private static int CountAuthoredBefore(IReadOnlyList<FlipbookDirectionData> directions, int index)
        {
            int count = 0;
            for (int i = 0; i < index && i < directions.Count; i++)
            {
                if (IsAuthored(directions[i]))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
