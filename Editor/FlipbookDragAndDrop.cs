using UnityEditor;
using UnityEngine;

namespace KingdomTD.Flipbook.Editor
{
    [InitializeOnLoad]
    internal static class FlipbookDragAndDrop
    {
        private const string WorldMenuLabel = "Flipbook Renderer";
        private const string UiMenuLabel = "Flipbook Graphic (UI)";

        static FlipbookDragAndDrop()
        {
            DragAndDrop.RemoveDropHandler(HandleHierarchyDrop);
            DragAndDrop.AddDropHandler(HandleHierarchyDrop);
            SceneView.duringSceneGui -= HandleSceneViewDragAndDrop;
            SceneView.duringSceneGui += HandleSceneViewDragAndDrop;
        }

        private static DragAndDropVisualMode HandleHierarchyDrop(int dropTargetInstanceId,
            HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            FlipbookAnimationAsset animationAsset = GetDraggedAnimationAsset();
            if (animationAsset == null)
            {
                return DragAndDropVisualMode.None;
            }

            if (!perform)
            {
                return DragAndDropVisualMode.Copy;
            }

            GameObject dropTargetObject = EditorUtility.InstanceIDToObject(dropTargetInstanceId) as GameObject;
            Transform dropTarget = dropTargetObject != null ? dropTargetObject.transform : null;
            Transform parent = dropTarget != null ? dropTarget : parentForDraggedObjects;
            int siblingIndex = -1;
            if (dropTarget != null)
            {
                if (dropMode == HierarchyDropFlags.DropBetween)
                {
                    parent = dropTarget.parent;
                    siblingIndex = dropTarget.GetSiblingIndex() + 1;
                }
                else if (dropMode == HierarchyDropFlags.DropAbove)
                {
                    parent = dropTarget.parent;
                    siblingIndex = dropTarget.GetSiblingIndex();
                }
            }

            ShowCreateMenu(animationAsset, parent, parent, siblingIndex, Vector3.zero, Vector3.zero, false);
            return DragAndDropVisualMode.Copy;
        }

        private static void HandleSceneViewDragAndDrop(SceneView sceneView)
        {
            Event currentEvent = Event.current;
            if (currentEvent == null ||
                (currentEvent.type != EventType.DragUpdated && currentEvent.type != EventType.DragPerform))
            {
                return;
            }

            FlipbookAnimationAsset animationAsset = GetDraggedAnimationAsset();
            if (animationAsset == null)
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (currentEvent.type == EventType.DragUpdated)
            {
                currentEvent.Use();
                return;
            }

            Vector2 mousePosition = currentEvent.mousePosition;
            Vector3 worldPosition = GetWorldSpawnPosition(sceneView, mousePosition);
            RectTransform uiParent = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<RectTransform>()
                : null;
            Vector3 uiPosition = uiParent != null
                ? GetPointOnPlane(mousePosition, new Plane(-uiParent.forward, uiParent.position), worldPosition)
                : worldPosition;

            ShowCreateMenu(animationAsset, null, uiParent, -1, worldPosition, uiPosition, true);
            DragAndDrop.AcceptDrag();
            currentEvent.Use();
        }

        private static FlipbookAnimationAsset GetDraggedAnimationAsset()
        {
            Object[] references = DragAndDrop.objectReferences;
            return references.Length == 1 ? references[0] as FlipbookAnimationAsset : null;
        }

        private static void ShowCreateMenu(FlipbookAnimationAsset animationAsset, Transform worldParent,
            Transform uiParent, int siblingIndex, Vector3 worldPosition, Vector3 uiPosition,
            bool useWorldPosition)
        {
            GenericMenu menu = new GenericMenu();
            if (FlipbookEditorObjectFactory.CanCreateWorldObject(animationAsset))
            {
                menu.AddItem(new GUIContent(WorldMenuLabel), false,
                    () => FlipbookEditorObjectFactory.CreateWorldObject(animationAsset, worldParent, siblingIndex,
                        worldPosition, useWorldPosition));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent($"{WorldMenuLabel} (需要纹理、动画和 World Material)"));
            }

            if (FlipbookEditorObjectFactory.CanCreateUiObject(animationAsset))
            {
                menu.AddItem(new GUIContent(UiMenuLabel), false,
                    () => FlipbookEditorObjectFactory.CreateUiObject(animationAsset, uiParent, siblingIndex,
                        uiPosition, useWorldPosition));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent($"{UiMenuLabel} (需要纹理和动画)"));
            }

            menu.ShowAsContext();
        }

        private static Vector3 GetWorldSpawnPosition(SceneView sceneView, Vector2 mousePosition)
        {
            if (HandleUtility.PlaceObject(mousePosition, out Vector3 position, out _))
            {
                return position;
            }

            Plane fallbackPlane = new Plane(-sceneView.camera.transform.forward, sceneView.pivot);
            return GetPointOnPlane(mousePosition, fallbackPlane, sceneView.pivot);
        }

        private static Vector3 GetPointOnPlane(Vector2 mousePosition, Plane plane, Vector3 fallback)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : fallback;
        }
    }
}
