using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KingdomTD.Flipbook.Editor
{
    internal static class FlipbookAssetFactory
    {
        internal const string WorldShaderName = "KingdomTD/Flipbook/World";

        private const string DefaultAnimationName = "Idle";
        private const float DefaultFrameRate = 30f;

        internal static bool TryCreate(Texture2D texture, string destinationFolder,
            out FlipbookAnimationAsset animationAsset, out Material worldMaterial, out string error)
        {
            animationAsset = null;
            worldMaterial = null;
            error = string.Empty;

            if (texture == null)
            {
                error = "请选择一张 Texture2D 图片。";
                return false;
            }

            if (string.IsNullOrEmpty(destinationFolder) || !AssetDatabase.IsValidFolder(destinationFolder))
            {
                error = $"输出目录无效：{destinationFolder}";
                return false;
            }

            Shader worldShader = Shader.Find(WorldShaderName);
            if (worldShader == null)
            {
                error = $"找不到 Flipbook Shader：{WorldShaderName}";
                return false;
            }

            string sourcePath = AssetDatabase.GetAssetPath(texture);
            string sourceName = string.IsNullOrEmpty(sourcePath)
                ? texture.name
                : Path.GetFileNameWithoutExtension(sourcePath);
            string basePath = FindAvailableBasePath(destinationFolder, $"{sourceName}_Flipbook");
            string materialPath = $"{basePath}.mat";
            string animationAssetPath = $"{basePath}.asset";
            InferGrid(texture, sourcePath, out int columns, out int rows);

            worldMaterial = new Material(worldShader)
            {
                name = Path.GetFileName(basePath),
                enableInstancing = true
            };
            if (worldMaterial.HasProperty("_MainTex"))
            {
                worldMaterial.SetTexture("_MainTex", texture);
            }

            if (worldMaterial.HasProperty("_Columns"))
            {
                worldMaterial.SetFloat("_Columns", columns);
            }

            if (worldMaterial.HasProperty("_Rows"))
            {
                worldMaterial.SetFloat("_Rows", rows);
            }

            animationAsset = ScriptableObject.CreateInstance<FlipbookAnimationAsset>();
            animationAsset.name = Path.GetFileName(basePath);
            ConfigureAnimationAsset(animationAsset, texture, worldMaterial, columns, rows);

            bool materialCreated = false;
            bool animationAssetCreated = false;
            try
            {
                AssetDatabase.CreateAsset(worldMaterial, materialPath);
                materialCreated = true;
                AssetDatabase.CreateAsset(animationAsset, animationAssetPath);
                animationAssetCreated = true;
                AssetDatabase.SaveAssets();
                return true;
            }
            catch (Exception exception)
            {
                error = $"创建 Flipbook 资产失败：{exception.Message}";
                if (animationAssetCreated)
                {
                    AssetDatabase.DeleteAsset(animationAssetPath);
                }
                else
                {
                    Object.DestroyImmediate(animationAsset);
                }

                if (materialCreated)
                {
                    AssetDatabase.DeleteAsset(materialPath);
                }
                else
                {
                    Object.DestroyImmediate(worldMaterial);
                }

                animationAsset = null;
                worldMaterial = null;
                return false;
            }
        }

        private static void ConfigureAnimationAsset(FlipbookAnimationAsset animationAsset, Texture2D texture,
            Material worldMaterial, int columns, int rows)
        {
            SerializedObject serializedAsset = new SerializedObject(animationAsset);
            serializedAsset.FindProperty("_mainTexture").objectReferenceValue = texture;
            serializedAsset.FindProperty("_worldMaterial").objectReferenceValue = worldMaterial;
            serializedAsset.FindProperty("_columns").intValue = columns;
            serializedAsset.FindProperty("_rows").intValue = rows;
            serializedAsset.FindProperty("_defaultAnimationName").stringValue = DefaultAnimationName;

            SerializedProperty clips = serializedAsset.FindProperty("_clips");
            clips.arraySize = 1;
            SerializedProperty clip = clips.GetArrayElementAtIndex(0);
            clip.FindPropertyRelative("AnimationName").stringValue = DefaultAnimationName;
            clip.FindPropertyRelative("StartFrame").intValue = 0;
            clip.FindPropertyRelative("FrameCount").intValue = columns * rows;
            clip.FindPropertyRelative("FrameRate").floatValue = DefaultFrameRate;
            clip.FindPropertyRelative("Speed").floatValue = 1f;
            SerializedProperty events = clip.FindPropertyRelative("Events");
            if (events != null)
            {
                events.arraySize = 0;
            }

            serializedAsset.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string FindAvailableBasePath(string destinationFolder, string baseName)
        {
            string normalizedFolder = destinationFolder.TrimEnd('/');
            for (int index = 0; ; index++)
            {
                string suffix = index == 0 ? string.Empty : $"_{index}";
                string candidate = $"{normalizedFolder}/{baseName}{suffix}";
                if (AssetDatabase.LoadMainAssetAtPath($"{candidate}.asset") == null &&
                    AssetDatabase.LoadMainAssetAtPath($"{candidate}.mat") == null)
                {
                    return candidate;
                }
            }
        }

        private static void InferGrid(Texture2D texture, string sourcePath, out int columns, out int rows)
        {
            columns = 1;
            rows = 1;
            if (string.IsNullOrEmpty(sourcePath))
            {
                return;
            }

            Object[] representations = AssetDatabase.LoadAllAssetRepresentationsAtPath(sourcePath);
            List<Sprite> sprites = new List<Sprite>();
            for (int index = 0; index < representations.Length; index++)
            {
                if (representations[index] is Sprite sprite)
                {
                    sprites.Add(sprite);
                }
            }

            if (sprites.Count <= 1)
            {
                return;
            }

            float frameWidth = sprites[0].rect.width;
            float frameHeight = sprites[0].rect.height;
            if (frameWidth <= 0f || frameHeight <= 0f)
            {
                return;
            }

            int inferredColumns = Mathf.RoundToInt(texture.width / frameWidth);
            int inferredRows = Mathf.RoundToInt(texture.height / frameHeight);
            if (inferredColumns <= 0 || inferredRows <= 0 || inferredColumns * inferredRows != sprites.Count ||
                !Approximately(frameWidth * inferredColumns, texture.width) ||
                !Approximately(frameHeight * inferredRows, texture.height))
            {
                return;
            }

            bool[] occupiedCells = new bool[sprites.Count];
            for (int index = 0; index < sprites.Count; index++)
            {
                Rect rect = sprites[index].rect;
                if (!Approximately(rect.width, frameWidth) || !Approximately(rect.height, frameHeight))
                {
                    return;
                }

                int column = Mathf.RoundToInt(rect.x / frameWidth);
                int row = Mathf.RoundToInt(rect.y / frameHeight);
                if (column < 0 || column >= inferredColumns || row < 0 || row >= inferredRows ||
                    !Approximately(rect.x, column * frameWidth) ||
                    !Approximately(rect.y, row * frameHeight))
                {
                    return;
                }

                int cellIndex = row * inferredColumns + column;
                if (occupiedCells[cellIndex])
                {
                    return;
                }

                occupiedCells[cellIndex] = true;
            }

            columns = inferredColumns;
            rows = inferredRows;
        }

        private static bool Approximately(float left, float right)
        {
            return Mathf.Abs(left - right) <= 0.01f;
        }
    }
}
