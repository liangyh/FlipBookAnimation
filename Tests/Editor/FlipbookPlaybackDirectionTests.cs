using System.Collections.Generic;
using NUnit.Framework;

namespace KingdomTD.Flipbook.Tests
{
    public sealed class FlipbookPlaybackDirectionTests
    {
        private const float FrameTime = 0.1f;

        private static FlipbookClipData CreateWalkClip()
        {
            return new FlipbookClipData
            {
                AnimationName = "Walk",
                StartFrame = 0,
                FrameCount = 4,
                FrameRate = 10f,
                Speed = 1f,
                DirectionStartFrames = new List<int> { 0, 4, 8, 12, 16 },
                Events = new List<FlipbookEventData> { new FlipbookEventData { Name = "Step", Frame = 2 } }
            };
        }

        private static FlipbookPlayback CreatePlayback()
        {
            FlipbookPlayback playback = new FlipbookPlayback();
            playback.SetSource(new List<FlipbookClipData> { CreateWalkClip() }, "Walk", 20,
                FlipbookDirectionTestData.CreateEightDirections());
            return playback;
        }

        [Test]
        public void SetDirection_WhilePlaying_KeepsLocalFrameAndDoesNotRaiseEvents()
        {
            FlipbookPlayback playback = CreatePlayback();
            List<string> events = new List<string>();
            int completedCount = 0;
            playback.FrameEvent += events.Add;
            playback.Completed += _ => completedCount++;

            Assert.IsTrue(playback.Play("Walk"));
            playback.Tick(FrameTime * 1.5f);
            Assert.AreEqual(1, playback.CurrentFrame);

            Assert.IsTrue(playback.SetDirection(6));
            Assert.AreEqual(13, playback.CurrentFrame);
            Assert.IsFalse(playback.FlipX);
            Assert.IsTrue(playback.IsPlaying);
            CollectionAssert.IsEmpty(events);

            playback.Tick(FrameTime);
            Assert.AreEqual(14, playback.CurrentFrame);
            CollectionAssert.AreEqual(new[] { "Step" }, events);
            Assert.AreEqual(0, completedCount);
        }

        [Test]
        public void SetDirection_WhenMirrored_UsesSourceFramesAndRaisesFlip()
        {
            FlipbookPlayback playback = CreatePlayback();
            List<bool> flips = new List<bool>();
            playback.FlipXChanged += flips.Add;
            playback.Play("Walk");
            playback.SetDirection(1);
            Assert.AreEqual(4, playback.CurrentFrame);

            playback.SetDirection(3);

            Assert.AreEqual(4, playback.CurrentFrame);
            Assert.IsTrue(playback.FlipX);
            CollectionAssert.AreEqual(new[] { true }, flips);
        }

        [Test]
        public void SetFacingAngle_SelectsNearestDirection()
        {
            FlipbookPlayback playback = CreatePlayback();
            playback.Play("Walk");

            Assert.AreEqual(4, playback.SetFacingAngle(185f));
            Assert.AreEqual(0, playback.CurrentFrame);
            Assert.IsTrue(playback.FlipX);

            Assert.AreEqual(6, playback.SetFacingAngle(-80f));
            Assert.AreEqual(12, playback.CurrentFrame);
            Assert.IsFalse(playback.FlipX);
        }

        [Test]
        public void Play_AfterDirectionSet_StartsFromDirectionStartFrame()
        {
            FlipbookPlayback playback = CreatePlayback();
            playback.SetDirection(2);

            playback.Play("Walk");

            Assert.AreEqual(8, playback.CurrentFrame);
        }

        [Test]
        public void Play_WhenAssetHasNoDirections_UsesStartFrame()
        {
            FlipbookClipData clip = CreateWalkClip();
            clip.StartFrame = 3;
            clip.DirectionStartFrames.Clear();
            FlipbookPlayback playback = new FlipbookPlayback();
            playback.SetSource(new List<FlipbookClipData> { clip }, "Walk", 20);

            Assert.IsTrue(playback.Play("Walk"));

            Assert.AreEqual(3, playback.CurrentFrame);
            Assert.AreEqual(1, playback.DirectionCount);
            Assert.IsFalse(playback.SetDirection(1));
            Assert.AreEqual(0, playback.SetFacingAngle(180f));
            Assert.IsFalse(playback.FlipX);
        }

        [Test]
        public void SetDirection_WhenOutOfRange_ReturnsFalse()
        {
            FlipbookPlayback playback = CreatePlayback();

            Assert.IsFalse(playback.SetDirection(-1));
            Assert.IsFalse(playback.SetDirection(8));
            Assert.AreEqual(0, playback.CurrentDirectionIndex);
        }
    }
}
