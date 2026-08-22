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
        public List<FlipbookEventData> Events = new List<FlipbookEventData>();
    }
}
