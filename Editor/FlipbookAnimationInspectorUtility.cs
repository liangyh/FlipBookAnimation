using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace KingdomTD.Flipbook.Editor
{
    internal static class FlipbookAnimationInspectorUtility
    {
        private static readonly GUIContent AnimationLabel = new GUIContent("Animation");
        private static readonly GUIContent[] NoAssetOptions = { new GUIContent("No Animation Asset") };
        private static readonly GUIContent[] NoClipOptions = { new GUIContent("No Animation Clips") };
        private static readonly Dictionary<int, int> OptionHashes = new Dictionary<int, int>();
        private static readonly Dictionary<int, string[]> AnimationNamesByAsset =
            new Dictionary<int, string[]>();
        private static readonly Dictionary<int, GUIContent[]> DisplayNamesByAsset =
            new Dictionary<int, GUIContent[]>();
        private static readonly Dictionary<int, string[]> RuntimeAnimationNamesByAsset =
            new Dictionary<int, string[]>();
        private static readonly Dictionary<int, GUIContent[]> RuntimeDisplayNamesByAsset =
            new Dictionary<int, GUIContent[]>();

        internal static bool DrawSerializedAnimationPopup(SerializedProperty animationAssetProperty,
            SerializedProperty animationNameProperty)
        {
            if (animationAssetProperty.hasMultipleDifferentValues ||
                animationAssetProperty.objectReferenceValue is not FlipbookAnimationAsset animationAsset)
            {
                EditorGUILayout.PropertyField(animationNameProperty, AnimationLabel);
                return false;
            }

            GetAnimationOptions(animationAsset, out string[] animationNames, out GUIContent[] displayNames);
            bool changed = DrawPopup(animationNames, displayNames, animationNameProperty.stringValue,
                animationNameProperty.hasMultipleDifferentValues, out string selectedAnimationName);
            if (changed)
            {
                animationNameProperty.stringValue = selectedAnimationName;
            }

            return changed;
        }

        internal static bool DrawRuntimeAnimationPopup(SerializedProperty animationAssetProperty,
            string currentAnimationName, bool hasMultipleDifferentValues, out string selectedAnimationName)
        {
            currentAnimationName ??= string.Empty;
            selectedAnimationName = currentAnimationName;
            if (animationAssetProperty.hasMultipleDifferentValues ||
                animationAssetProperty.objectReferenceValue is not FlipbookAnimationAsset animationAsset)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Popup(AnimationLabel, 0, NoAssetOptions);
                }

                return false;
            }

            GetRuntimeAnimationOptions(animationAsset, out string[] animationNames,
                out GUIContent[] displayNames);
            if (animationNames.Length == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Popup(AnimationLabel, 0, NoClipOptions);
                }

                return false;
            }

            return DrawPopup(animationNames, displayNames, currentAnimationName, hasMultipleDifferentValues,
                out selectedAnimationName);
        }

        private static bool DrawPopup(string[] animationNames, GUIContent[] displayNames,
            string currentAnimationName, bool hasMultipleDifferentValues, out string selectedAnimationName)
        {
            selectedAnimationName = currentAnimationName;
            int selectedIndex = FindAnimationIndex(animationNames, currentAnimationName);
            if (selectedIndex < 0)
            {
                string[] namesWithMissing = new string[animationNames.Length + 1];
                GUIContent[] displayNamesWithMissing = new GUIContent[displayNames.Length + 1];
                animationNames.CopyTo(namesWithMissing, 0);
                displayNames.CopyTo(displayNamesWithMissing, 0);
                selectedIndex = animationNames.Length;
                namesWithMissing[selectedIndex] = currentAnimationName;
                displayNamesWithMissing[selectedIndex] = new GUIContent($"Missing ({currentAnimationName})");
                animationNames = namesWithMissing;
                displayNames = displayNamesWithMissing;
            }

            EditorGUI.showMixedValue = hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int newSelectedIndex = EditorGUILayout.Popup(AnimationLabel, selectedIndex, displayNames);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;
            if (changed)
            {
                selectedAnimationName = animationNames[newSelectedIndex];
            }

            return changed;
        }

        private static void GetAnimationOptions(FlipbookAnimationAsset animationAsset,
            out string[] animationNames, out GUIContent[] displayNames)
        {
            int instanceId = animationAsset.GetInstanceID();
            int optionHash = CalculateOptionHash(animationAsset);
            if (!OptionHashes.TryGetValue(instanceId, out int cachedHash) || cachedHash != optionHash ||
                !RuntimeAnimationNamesByAsset.ContainsKey(instanceId))
            {
                List<string> animationNameList = new List<string>(animationAsset.Clips.Count + 1)
                {
                    string.Empty
                };
                List<GUIContent> displayNameList = new List<GUIContent>(animationAsset.Clips.Count + 1)
                {
                    new GUIContent($"Asset Default ({animationAsset.DefaultAnimationName})")
                };
                List<string> runtimeAnimationNameList = new List<string>(animationAsset.Clips.Count);
                List<GUIContent> runtimeDisplayNameList = new List<GUIContent>(animationAsset.Clips.Count);

                for (int i = 0; i < animationAsset.Clips.Count; i++)
                {
                    FlipbookClipData clip = animationAsset.Clips[i];
                    if (clip == null || string.IsNullOrEmpty(clip.AnimationName))
                    {
                        continue;
                    }

                    animationNameList.Add(clip.AnimationName);
                    displayNameList.Add(new GUIContent(clip.AnimationName));
                    runtimeAnimationNameList.Add(clip.AnimationName);
                    runtimeDisplayNameList.Add(new GUIContent(clip.AnimationName));
                }

                OptionHashes[instanceId] = optionHash;
                AnimationNamesByAsset[instanceId] = animationNameList.ToArray();
                DisplayNamesByAsset[instanceId] = displayNameList.ToArray();
                RuntimeAnimationNamesByAsset[instanceId] = runtimeAnimationNameList.ToArray();
                RuntimeDisplayNamesByAsset[instanceId] = runtimeDisplayNameList.ToArray();
            }

            animationNames = AnimationNamesByAsset[instanceId];
            displayNames = DisplayNamesByAsset[instanceId];
        }

        private static void GetRuntimeAnimationOptions(FlipbookAnimationAsset animationAsset,
            out string[] animationNames, out GUIContent[] displayNames)
        {
            GetAnimationOptions(animationAsset, out _, out _);
            int instanceId = animationAsset.GetInstanceID();
            animationNames = RuntimeAnimationNamesByAsset[instanceId];
            displayNames = RuntimeDisplayNamesByAsset[instanceId];
        }

        private static int CalculateOptionHash(FlipbookAnimationAsset animationAsset)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + animationAsset.Clips.Count;
                hash = hash * 31 + (animationAsset.DefaultAnimationName?.GetHashCode() ?? 0);
                for (int i = 0; i < animationAsset.Clips.Count; i++)
                {
                    hash = hash * 31 + (animationAsset.Clips[i]?.AnimationName?.GetHashCode() ?? 0);
                }

                return hash;
            }
        }

        private static int FindAnimationIndex(string[] animationNames, string animationName)
        {
            for (int i = 0; i < animationNames.Length; i++)
            {
                if (animationNames[i] == animationName)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
