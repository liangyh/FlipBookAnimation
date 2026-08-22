using UnityEditor;
using UnityEngine;

namespace KingdomTD.Flipbook.Editor
{
    internal static class FlipbookEditorObjectFactory
    {
        private const string UndoName = "Create Flipbook GameObject";

        internal static bool CanCreateWorldObject(FlipbookAnimationAsset animationAsset)
        {
            return CanCreateUiObject(animationAsset) && animationAsset.WorldMaterial != null;
        }

        internal static bool CanCreateUiObject(FlipbookAnimationAsset animationAsset)
        {
            return animationAsset != null && animationAsset.MainTexture != null && animationAsset.Clips.Count > 0;
        }

        internal static GameObject CreateWorldObject(FlipbookAnimationAsset animationAsset, Transform parent,
            int siblingIndex, Vector3 position, bool useWorldPosition)
        {
            GameObject targetObject = new GameObject(animationAsset.name);
            Undo.RegisterCreatedObjectUndo(targetObject, UndoName);
            ConfigureTransform(targetObject.transform, parent, siblingIndex, position, useWorldPosition);

            FlipbookRenderer flipbookRenderer = Undo.AddComponent<FlipbookRenderer>(targetObject);
            Undo.RecordObject(flipbookRenderer, UndoName);
            flipbookRenderer.SetAsset(animationAsset);
            EditorUtility.SetDirty(flipbookRenderer);
            Selection.activeGameObject = targetObject;
            return targetObject;
        }

        internal static GameObject CreateUiObject(FlipbookAnimationAsset animationAsset, Transform parent,
            int siblingIndex, Vector3 position, bool useWorldPosition)
        {
            GameObject targetObject = new GameObject(animationAsset.name, typeof(RectTransform),
                typeof(CanvasRenderer));
            Undo.RegisterCreatedObjectUndo(targetObject, UndoName);

            RectTransform rectTransform = targetObject.GetComponent<RectTransform>();
            ConfigureTransform(rectTransform, parent, siblingIndex, position, useWorldPosition);

            FlipbookGraphic flipbookGraphic = Undo.AddComponent<FlipbookGraphic>(targetObject);
            Undo.RecordObject(flipbookGraphic, UndoName);
            flipbookGraphic.SetAsset(animationAsset);
            flipbookGraphic.SetNativeSize();
            EditorUtility.SetDirty(flipbookGraphic);

            if (parent == null || parent.GetComponentInParent<Canvas>() == null)
            {
                Debug.LogWarning($"{targetObject.name}: 创建的 FlipbookGraphic 不在 Canvas 下，加入 Canvas 后才会显示。",
                    targetObject);
            }

            Selection.activeGameObject = targetObject;
            return targetObject;
        }

        private static void ConfigureTransform(Transform target, Transform parent, int siblingIndex,
            Vector3 position, bool useWorldPosition)
        {
            if (parent != null)
            {
                Undo.SetTransformParent(target, parent, UndoName);
            }

            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
            if (useWorldPosition)
            {
                target.position = position;
            }
            else if (target is RectTransform rectTransform)
            {
                rectTransform.anchoredPosition3D = Vector3.zero;
            }
            else
            {
                target.localPosition = Vector3.zero;
            }

            if (parent != null && siblingIndex >= 0)
            {
                target.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, parent.childCount - 1));
            }
        }
    }
}
