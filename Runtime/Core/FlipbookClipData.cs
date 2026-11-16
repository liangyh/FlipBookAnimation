using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace KingdomTD.Flipbook
{
    [Serializable]
    public sealed class FlipbookClipData
    {
        [FormerlySerializedAs("Animation")]
        public string AnimationName = "Idle";
        [Min(0)] public int StartFrame;
        [Min(1)] public int FrameCount = 1;
        [Min(1f)] public float FrameRate = 30f;
        [Min(0.0001f)] public float Speed = 1f;

        [Tooltip("每个使用自己帧的方向的起始帧，按方向表中 SourceDirection = -1 的方向依次排列。" +
                 "为空或缺项时使用 StartFrame。所有方向共享 FrameCount、FrameRate 与 Events。")]
        public List<int> DirectionStartFrames = new List<int>();

        public List<FlipbookEventData> Events = new List<FlipbookEventData>();

        public int GetStartFrame(int authoredDirection)
        {
            return DirectionStartFrames != null && authoredDirection >= 0 &&
                   authoredDirection < DirectionStartFrames.Count
                ? DirectionStartFrames[authoredDirection]
                : StartFrame;
        }
    }
}
