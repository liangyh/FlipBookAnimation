using UnityEditor;
using UnityEditor.SceneManagement;

namespace KingdomTD.Flipbook.Editor
{
    [InitializeOnLoad]
    internal static class FlipbookWorldEditor
    {
        static FlipbookWorldEditor()
        {
            FlipbookGroundShadowSettings.IsPreviewScene = EditorSceneManager.IsPreviewScene;
            EditorApplication.update += FlipbookRenderer.ApplyPendingValidations;
        }
    }
}
