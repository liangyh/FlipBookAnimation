using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace KingdomTD.Flipbook.Tests
{
    public sealed class FlipbookDirectionResolverTests
    {
        private List<FlipbookDirectionData> _directions;

        [SetUp]
        public void SetUp()
        {
            _directions = FlipbookDirectionTestData.CreateEightDirections();
        }

        [TestCase(0f, 0)]
        [TestCase(20f, 0)]
        [TestCase(30f, 1)]
        [TestCase(350f, 0)]
        [TestCase(-10f, 0)]
        [TestCase(200f, 4)]
        [TestCase(260f, 6)]
        [TestCase(330f, 7)]
        public void ResolveIndex_WhenAngleNearest_ReturnsClosestDirection(float angle, int expectedIndex)
        {
            Assert.AreEqual(expectedIndex, FlipbookDirectionResolver.ResolveIndex(_directions, angle));
        }

        [Test]
        public void ResolveIndex_WithinHysteresis_KeepsCurrentDirection()
        {
            Assert.AreEqual(0, FlipbookDirectionResolver.ResolveIndex(_directions, 24f, 0, 5f));
            Assert.AreEqual(1, FlipbookDirectionResolver.ResolveIndex(_directions, 30f, 0, 5f));
        }

        [Test]
        public void ResolveIndex_WhenNoDirections_ReturnsZero()
        {
            Assert.AreEqual(0, FlipbookDirectionResolver.ResolveIndex(null, 123f));
            Assert.AreEqual(0, FlipbookDirectionResolver.ResolveIndex(new List<FlipbookDirectionData>(), 123f));
        }

        [TestCase(0, 0, false)]
        [TestCase(1, 1, false)]
        [TestCase(2, 2, false)]
        [TestCase(3, 1, true)]
        [TestCase(4, 0, true)]
        [TestCase(5, 4, true)]
        [TestCase(6, 3, false)]
        [TestCase(7, 4, false)]
        public void ResolveSource_WhenMirrored_ReturnsSourceAndFlip(int directionIndex, int expectedAuthored,
            bool expectedFlip)
        {
            Assert.IsTrue(FlipbookDirectionResolver.ResolveSource(_directions, directionIndex,
                out int authoredDirection, out bool flipX));
            Assert.AreEqual(expectedAuthored, authoredDirection);
            Assert.AreEqual(expectedFlip, flipX);
        }

        [Test]
        public void ResolveSource_WhenSourceChained_ReturnsFalse()
        {
            _directions[3].SourceDirection = 4;
            Assert.IsFalse(FlipbookDirectionResolver.ResolveSource(_directions, 3, out _, out _));
        }

        [Test]
        public void GetAuthoredDirectionCount_CountsOnlyOwnFrameDirections()
        {
            Assert.AreEqual(5, FlipbookDirectionResolver.GetAuthoredDirectionCount(_directions));
            Assert.AreEqual(1, FlipbookDirectionResolver.GetAuthoredDirectionCount(null));
        }

        [Test]
        public void TryGetAngle_WhenZeroVector_ReturnsFalse()
        {
            Assert.IsFalse(FlipbookDirectionResolver.TryGetAngle(Vector2.zero, out _));
            Assert.IsTrue(FlipbookDirectionResolver.TryGetAngle(Vector2.down, out float angle));
            Assert.AreEqual(270f, angle, 0.001f);
        }

        [Test]
        public void TryGetScreenAngle_WhenCameraYawed_UsesCameraRelativeAngle()
        {
            GameObject view = new GameObject("View");
            try
            {
                view.transform.rotation = Quaternion.Euler(45f, 90f, 0f);

                Assert.IsTrue(FlipbookDirectionResolver.TryGetScreenAngle(Vector3.right, view.transform,
                    Vector3.up, out float awayAngle));
                Assert.AreEqual(90f, awayAngle, 0.01f);

                Assert.IsTrue(FlipbookDirectionResolver.TryGetScreenAngle(Vector3.back, view.transform,
                    Vector3.up, out float rightAngle));
                Assert.AreEqual(0f, Mathf.DeltaAngle(0f, rightAngle), 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(view);
            }
        }
    }
}
