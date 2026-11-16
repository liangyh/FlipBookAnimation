using System.Collections.Generic;

namespace KingdomTD.Flipbook.Tests
{
    internal static class FlipbookDirectionTestData
    {
        public static List<FlipbookDirectionData> CreateEightDirections()
        {
            return new List<FlipbookDirectionData>
            {
                Direction("E", 0f, -1, false),
                Direction("NE", 45f, -1, false),
                Direction("N", 90f, -1, false),
                Direction("NW", 135f, 1, true),
                Direction("W", 180f, 0, true),
                Direction("SW", 225f, 7, true),
                Direction("S", 270f, -1, false),
                Direction("SE", 315f, -1, false)
            };
        }

        public static FlipbookDirectionData Direction(string name, float angle, int source, bool flipX)
        {
            return new FlipbookDirectionData
            {
                Name = name,
                Angle = angle,
                SourceDirection = source,
                FlipX = flipX
            };
        }
    }
}
