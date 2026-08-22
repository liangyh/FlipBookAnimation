using System;
using UnityEngine;

namespace KingdomTD.Flipbook
{
    [Serializable]
    public sealed class FlipbookEventData
    {
        [Min(0)] public int Frame;
        public string Name;
    }
}
