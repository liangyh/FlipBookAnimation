using UnityEditor;
using UnityEngine;
using UnityEditor.UI;

namespace KingdomTD.Flipbook.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(FlipbookGraphic))]
    public sealed class FlipbookGraphicEditor : GraphicEditor
    {
        private SerializedProperty _animationAsset;
        private SerializedProperty _defaultAnimationName;
        private SerializedProperty _timeMode;
        private SerializedProperty _playOnEnable;
        private SerializedProperty _loop;
        private SerializedProperty _randomizeLoopStartFrame;
        private SerializedProperty _pauseWhenCulled;
        private SerializedProperty _preserveAspect;
        private SerializedProperty _speed;
        private SerializedProperty _frameEvent;
        private SerializedProperty _animationCompleted;

        protected override void OnEnable()
        {
            base.OnEnable();
            _animationAsset = serializedObject.FindProperty("_animationAsset");
            _defaultAnimationName = serializedObject.FindProperty("_defaultAnimationName");
            _timeMode = serializedObject.FindProperty("_timeMode");
            _playOnEnable = serializedObject.FindProperty("_playOnEnable");
            _loop = serializedObject.FindProperty("_loop");
            _randomizeLoopStartFrame = serializedObject.FindProperty("_randomizeLoopStartFrame");
            _pauseWhenCulled = serializedObject.FindProperty("_pauseWhenCulled");
            _preserveAspect = serializedObject.FindProperty("_preserveAspect");
            _speed = serializedObject.FindProperty("_speed");
            _frameEvent = serializedObject.FindProperty("_frameEvent");
            _animationCompleted = serializedObject.FindProperty("_animationCompleted");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(m_Script);
            EditorGUILayout.PropertyField(_animationAsset);
            bool runtimeSelection = Application.isPlaying;
            bool animationChanged;
            string selectedAnimationName;
            if (runtimeSelection)
            {
                string currentAnimationName = GetCurrentAnimationName(out bool hasMultipleAnimationNames);
                animationChanged = FlipbookAnimationInspectorUtility.DrawRuntimeAnimationPopup(
                    _animationAsset, currentAnimationName, hasMultipleAnimationNames, out selectedAnimationName);
            }
            else
            {
                animationChanged = FlipbookAnimationInspectorUtility.DrawSerializedAnimationPopup(_animationAsset,
                    _defaultAnimationName);
                selectedAnimationName = _defaultAnimationName.stringValue;
            }

            EditorGUILayout.PropertyField(_timeMode);
            EditorGUILayout.PropertyField(_playOnEnable);
            EditorGUILayout.PropertyField(_loop);
            EditorGUILayout.PropertyField(_randomizeLoopStartFrame);
            EditorGUILayout.PropertyField(_pauseWhenCulled);
            EditorGUILayout.PropertyField(_preserveAspect);
            EditorGUILayout.PropertyField(_speed);
            AppearanceControlsGUI();
            RaycastControlsGUI();
            MaskableControlsGUI();
            EditorGUILayout.PropertyField(_frameEvent);
            EditorGUILayout.PropertyField(_animationCompleted);
            serializedObject.ApplyModifiedProperties();

            if (animationChanged)
            {
                ApplySelectedAnimation(selectedAnimationName, runtimeSelection);
            }

            SetShowNativeSize(_animationAsset.objectReferenceValue != null, false);
            NativeSizeButtonGUI();
        }

        private string GetCurrentAnimationName(out bool hasMultipleAnimationNames)
        {
            string animationName = ((FlipbookGraphic)targets[0]).CurrentAnimationName;
            hasMultipleAnimationNames = false;
            for (int i = 1; i < targets.Length; i++)
            {
                if (((FlipbookGraphic)targets[i]).CurrentAnimationName == animationName)
                {
                    continue;
                }

                hasMultipleAnimationNames = true;
                break;
            }

            return animationName;
        }

        private void ApplySelectedAnimation(string animationName, bool runtimeSelection)
        {
            string resolvedAnimationName = string.IsNullOrEmpty(animationName) ? null : animationName;
            foreach (Object targetObject in targets)
            {
                FlipbookGraphic flipbookGraphic = (FlipbookGraphic)targetObject;
                if ((!runtimeSelection && !flipbookGraphic.Initialize()) ||
                    !flipbookGraphic.Play(resolvedAnimationName, _loop.boolValue, _speed.floatValue))
                {
                    continue;
                }

                if (!runtimeSelection)
                {
                    flipbookGraphic.SetNormalizedTime(0f);
                }
            }

            if (!runtimeSelection)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
        }
    }
}
