namespace KingdomTD.Flipbook.Editor
{
    /// <summary>
    /// 编辑方向表时使用的临时模型。Id 用于在增删、改来源后找回各动画原有的起始帧。
    /// </summary>
    internal sealed class FlipbookDirectionEditModel
    {
        public const int NewDirectionId = -1;

        public int Id = NewDirectionId;
        public int FallbackId = NewDirectionId;
        public string Name;
        public float Angle;
        public int SourceDirection = -1;
        public bool FlipX;

        public bool HasOwnFrames => SourceDirection < 0;

        public FlipbookDirectionEditModel Clone()
        {
            return (FlipbookDirectionEditModel)MemberwiseClone();
        }
    }
}
