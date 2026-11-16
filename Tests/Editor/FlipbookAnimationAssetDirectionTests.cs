using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KingdomTD.Flipbook.Tests
{
    public sealed class FlipbookAnimationAssetDirectionTests
    {
        private FlipbookAnimationAsset _asset;
        private SerializedObject _serializedAsset;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<FlipbookAnimationAsset>();
            _serializedAsset = new SerializedObject(_asset);
            _serializedAsset.FindProperty("_columns").intValue = 4;
            _serializedAsset.FindProperty("_rows").intValue = 2;
            SerializedProperty clips = _serializedAsset.FindProperty("_clips");
            clips.arraySize = 1;
            SerializedProperty clip = clips.GetArrayElementAtIndex(0);
            clip.FindPropertyRelative("AnimationName").stringValue = "Idle";
            clip.FindPropertyRelative("StartFrame").intValue = 0;
            clip.FindPropertyRelative("FrameCount").intValue = 4;
            clip.FindPropertyRelative("FrameRate").floatValue = 10f;
            clip.FindPropertyRelative("Speed").floatValue = 1f;
            _serializedAsset.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_asset);
        }

        [Test]
        public void GetValidationErrors_WhenSourceDirectionChained_ReportsError()
        {
            SetDirections((0f, -1, false), (90f, 2, true), (180f, 0, true));
            SetDirectionStartFrames(0);

            List<string> errors = _asset.GetValidationErrors();

            Assert.IsTrue(errors.Exists(error => error.Contains("不支持链式借用")));
        }

        [Test]
        public void GetValidationErrors_WhenDirectionFramesOutOfRange_ReportsError()
        {
            SetDirections((0f, -1, false), (180f, -1, false));
            SetDirectionStartFrames(0, 6);

            List<string> errors = _asset.GetValidationErrors();

            Assert.IsTrue(errors.Exists(error => error.Contains("DirectionStartFrames[1]")));
        }

        [Test]
        public void GetValidationErrors_WhenDirectionsValid_ReportsNoDirectionErrors()
        {
            SetDirections((0f, -1, false), (180f, 0, true));
            SetDirectionStartFrames(0);

            List<string> errors = _asset.GetValidationErrors();

            Assert.IsFalse(errors.Exists(error => error.Contains("方向") || error.Contains("Direction")));
            Assert.AreEqual(2, _asset.DirectionCount);
            Assert.AreEqual(1, _asset.AuthoredDirectionCount);
        }

        private void SetDirections(params (float Angle, int Source, bool FlipX)[] directions)
        {
            SerializedProperty property = _serializedAsset.FindProperty("_directions");
            property.arraySize = directions.Length;
            for (int i = 0; i < directions.Length; i++)
            {
                SerializedProperty direction = property.GetArrayElementAtIndex(i);
                direction.FindPropertyRelative("Name").stringValue = $"D{i}";
                direction.FindPropertyRelative("Angle").floatValue = directions[i].Angle;
                direction.FindPropertyRelative("SourceDirection").intValue = directions[i].Source;
                direction.FindPropertyRelative("FlipX").boolValue = directions[i].FlipX;
            }

            _serializedAsset.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetDirectionStartFrames(params int[] startFrames)
        {
            SerializedProperty property = _serializedAsset.FindProperty("_clips").GetArrayElementAtIndex(0)
                .FindPropertyRelative("DirectionStartFrames");
            property.arraySize = startFrames.Length;
            for (int i = 0; i < startFrames.Length; i++)
            {
                property.GetArrayElementAtIndex(i).intValue = startFrames[i];
            }

            _serializedAsset.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
