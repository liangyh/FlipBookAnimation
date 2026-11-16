using System;
using UnityEngine;

namespace KingdomTD.Flipbook
{
    [Serializable]
    public sealed class FlipbookDirectionData
    {
        [Tooltip("仅用于显示与调试，例如 E、NE、N。")]
        public string Name = "E";

        [Tooltip("屏幕空间朝向角度（度）：0 = 右，90 = 上（远离相机），逆时针递增。")]
        public float Angle;

        [Tooltip("-1：使用本方向自己的帧；>= 0：复用该索引方向的帧（目标方向必须使用自己的帧）。")]
        public int SourceDirection = -1;

        [Tooltip("采样时水平翻转。通常与 SourceDirection 配合，用一侧的帧表现另一侧。")]
        public bool FlipX;

        public bool HasOwnFrames => SourceDirection < 0;
    }
}
