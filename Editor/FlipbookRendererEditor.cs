using UnityEditor;
using UnityEngine;

namespace KingdomTD.Flipbook.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(FlipbookRenderer))]
    public sealed class FlipbookRendererEditor : UnityEditor.Editor
    {
        private SerializedProperty _script;
        private SerializedProperty _animationAsset;
        private SerializedProperty _defaultAnimationName;
        private SerializedProperty _timeMode;
        private SerializedProperty _playOnEnable;
        private SerializedProperty _loop;
        private SerializedProperty _randomizeLoopStartFrame;
        private SerializedProperty _speed;
        private SerializedProperty _rotateParentForBillboard;
        private SerializedProperty _faceCamera;
        private SerializedProperty _useCameraForward;
        private SerializedProperty _frameEvent;
        private SerializedProperty _animationCompleted;

        private void OnEnable()
        {
            _script = serializedObject.FindProperty("m_Script");
            _animationAsset = serializedObject.FindProperty("_animationAsset");
            _defaultAnimationName = serializedObject.FindProperty("_defaultAnimationName");
            _timeMode = serializedObject.FindProperty("_timeMode");
            _playOnEnable = serializedObject.FindProperty("_playOnEnable");
            _loop = serializedObject.FindProperty("_loop");
            _randomizeLoopStartFrame = serializedObject.FindProperty("_randomizeLoopStartFrame");
            _speed = serializedObject.FindProperty("_speed");
            _rotateParentForBillboard = serializedObject.FindProperty("_rotateParentForBillboard");
            _faceCamera = serializedObject.FindProperty("_faceCamera");
            _useCameraForward = serializedObject.FindProperty("_useCameraForward");
            _frameEvent = serializedObject.FindProperty("_frameEvent");
            _animationCompleted = serializedObject.FindProperty("_animationCompleted");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(_script);
            }

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
            EditorGUILayout.PropertyField(_speed);
            EditorGUILayout.PropertyField(_faceCamera);
            if (_faceCamera.boolValue || _faceCamera.hasMultipleDifferentValues)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(_useCameraForward);
                    EditorGUILayout.PropertyField(_rotateParentForBillboard);
                }
            }
            EditorGUILayout.PropertyField(_frameEvent);
            EditorGUILayout.PropertyField(_animationCompleted);
            serializedObject.ApplyModifiedProperties();

            if (animationChanged)
            {
                ApplySelectedAnimation(selectedAnimationName, runtimeSelection);
            }
        }

        private string GetCurrentAnimationName(out bool hasMultipleAnimationNames)
        {
            string animationName = ((FlipbookRenderer)targets[0]).CurrentAnimationName;
            hasMultipleAnimationNames = false;
            for (int i = 1; i < targets.Length; i++)
            {
                if (((FlipbookRenderer)targets[i]).CurrentAnimationName == animationName)
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
                FlipbookRenderer flipbookRenderer = (FlipbookRenderer)targetObject;
                if ((!runtimeSelection && !flipbookRenderer.Initialize()) ||
                    !flipbookRenderer.Play(resolvedAnimationName, _loop.boolValue, _speed.floatValue))
                {
                    continue;
                }

                if (!runtimeSelection)
                {
                    flipbookRenderer.SetNormalizedTime(0f);
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
