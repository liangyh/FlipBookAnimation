using System.Collections.Generic;
using KingdomTD.Flipbook.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KingdomTD.Flipbook.Tests
{
    public sealed class FlipbookDirectionEditingTests
    {
        private const int EightDirectionPreset = 3;
        private const int FourDirectionPreset = 2;
        private const int TwoDirectionPreset = 1;

        private FlipbookAnimationAsset _asset;
        private SerializedObject _serializedAsset;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<FlipbookAnimationAsset>();
            _serializedAsset = new SerializedObject(_asset);
            _serializedAsset.FindProperty("_columns").intValue = 8;
            _serializedAsset.FindProperty("_rows").intValue = 4;
            SerializedProperty clips = _serializedAsset.FindProperty("_clips");
            clips.arraySize = 1;
            SerializedProperty clip = clips.GetArrayElementAtIndex(0);
            clip.FindPropertyRelative("AnimationName").stringValue = "Walk";
            clip.FindPropertyRelative("StartFrame").intValue = 2;
            clip.FindPropertyRelative("FrameCount").intValue = 4;
            clip.FindPropertyRelative("FrameRate").floatValue = 10f;
            clip.FindPropertyRelative("Speed").floatValue = 1f;
            clip.FindPropertyRelative("DirectionStartFrames").arraySize = 0;
            _serializedAsset.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_asset);
        }

        [Test]
        public void ApplyPreset_FromSingleDirection_PacksStartFramesAfterOriginalStart()
        {
            FlipbookDirectionEditing.ApplyPreset(_serializedAsset, EightDirectionPreset);
            Apply();

            CollectionAssert.AreEqual(new[] { 2, 6, 10, 14, 18 }, _asset.Clips[0].DirectionStartFrames);
            Assert.AreEqual(2, _asset.Clips[0].StartFrame);
            Assert.AreEqual(8, _asset.DirectionCount);
        }

        [Test]
        public void ApplyPreset_KeepsStartFramesOfSameNamedDirections()
        {
            FlipbookDirectionEditing.ApplyPreset(_serializedAsset, FourDirectionPreset);
            SetStartFrames(0, 10, 20);
            Apply();

            FlipbookDirectionEditing.ApplyPreset(_serializedAsset, EightDirectionPreset);
            Apply();

            List<int> frames = _asset.Clips[0].DirectionStartFrames;
            Assert.AreEqual(5, frames.Count);
            Assert.AreEqual(0, frames[0]);
            Assert.AreEqual(10, frames[2]);
            Assert.AreEqual(20, frames[3]);
        }

        [Test]
        public void ApplyPreset_ToSingleDirection_KeepsFirstDirectionFramesAsStartFrame()
        {
            FlipbookDirectionEditing.ApplyPreset(_serializedAsset, FourDirectionPreset);
            SetStartFrames(12, 16, 20);
            Apply();

            FlipbookDirectionEditing.ApplyPreset(_serializedAsset, 0);
            Apply();

            Assert.AreEqual(0, _asset.Directions.Count);
            Assert.AreEqual(12, _asset.Clips[0].StartFrame);
            CollectionAssert.IsEmpty(_asset.Clips[0].DirectionStartFrames);
        }

        [Test]
        public void RemoveDirection_WhenMirroredDirectionRemains_InheritsFramesAndFlip()
        {
            FlipbookDirectionEditing.ApplyPreset(_serializedAsset, TwoDirectionPreset);
            SetStartFrames(5);
            Apply();

            FlipbookDirectionEditing.Modify(_serializedAsset,
                directions => FlipbookDirectionEditing.RemoveDirection(directions, 0));
            Apply();

            Assert.AreEqual(1, _asset.Directions.Count);
            Assert.AreEqual("Left", _asset.Directions[0].Name);
            Assert.IsTrue(_asset.Directions[0].HasOwnFrames);
            Assert.IsTrue(_asset.Directions[0].FlipX);
            CollectionAssert.AreEqual(new[] { 5 }, _asset.Clips[0].DirectionStartFrames);
        }

        [Test]
        public void RemoveDirection_RemapsLaterSourceIndices()
        {
            FlipbookDirectionEditing.ApplyPreset(_serializedAsset, EightDirectionPreset);
            Apply();

            FlipbookDirectionEditing.Modify(_serializedAsset,
                directions => FlipbookDirectionEditing.RemoveDirection(directions, 2));
            Apply();

            Assert.AreEqual(7, _asset.Directions.Count);
            Assert.AreEqual("SW", _asset.Directions[4].Name);
            Assert.AreEqual(6, _asset.Directions[4].SourceDirection);
            Assert.AreEqual("SE", _asset.Directions[6].Name);
            Assert.IsEmpty(_asset.GetValidationErrors().FindAll(error => error.Contains("方向")));
        }

        [Test]
        public void SetSource_FromMirrorToOwnFrames_StartsFromPreviousSourceFrames()
        {
            FlipbookDirectionEditing.ApplyPreset(_serializedAsset, EightDirectionPreset);
            SetStartFrames(0, 4, 8, 12, 16);
            Apply();

            FlipbookDirectionEditing.Modify(_serializedAsset,
                directions => FlipbookDirectionEditing.SetSource(directions, 4, -1, false));
            Apply();

            // 自有帧方向顺序：E、NE、N、W、S、SE；W 从原来的来源 E 的帧开始。
            CollectionAssert.AreEqual(new[] { 0, 4, 8, 0, 12, 16 }, _asset.Clips[0].DirectionStartFrames);
        }

        [Test]
        public void SetSource_WhenDirectionStartsBorrowing_RedirectsItsBorrowers()
        {
            FlipbookDirectionEditing.ApplyPreset(_serializedAsset, EightDirectionPreset);
            SetStartFrames(0, 4, 8, 12, 16);
            Apply();

            FlipbookDirectionEditing.Modify(_serializedAsset,
                directions => FlipbookDirectionEditing.SetSource(directions, 1, 0, false));
            Apply();

            Assert.AreEqual(0, _asset.Directions[3].SourceDirection);
            CollectionAssert.AreEqual(new[] { 0, 8, 12, 16 }, _asset.Clips[0].DirectionStartFrames);
        }

        [Test]
        public void AddDirection_PlacesNewDirectionInLargestGap()
        {
            FlipbookDirectionEditing.ApplyPreset(_serializedAsset, TwoDirectionPreset);
            Apply();

            FlipbookDirectionEditing.Modify(_serializedAsset, FlipbookDirectionEditing.AddDirection);
            Apply();

            Assert.AreEqual(3, _asset.Directions.Count);
            Assert.AreEqual(90f, _asset.Directions[2].Angle, 0.01f);
            Assert.AreEqual(2, _asset.Clips[0].DirectionStartFrames.Count);
        }

        private void SetStartFrames(params int[] frames)
        {
            SerializedProperty startFrames = _serializedAsset.FindProperty("_clips").GetArrayElementAtIndex(0)
                .FindPropertyRelative("DirectionStartFrames");
            startFrames.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                startFrames.GetArrayElementAtIndex(i).intValue = frames[i];
            }
        }

        private void Apply()
        {
            _serializedAsset.ApplyModifiedPropertiesWithoutUndo();
            _serializedAsset.Update();
        }
    }
}
