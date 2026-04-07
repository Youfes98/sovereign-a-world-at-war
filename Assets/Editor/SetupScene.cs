// SetupScene.cs
// Editor utility: adds Bootstrap to the current scene.
// Menu: WarStrategy > Setup Current Scene

using UnityEditor;
using UnityEngine;

namespace WarStrategy.Editor
{
    public static class SetupScene
    {
        [MenuItem("WarStrategy/Setup Current Scene")]
        public static void AddBootstrapToScene()
        {
            // Check if Bootstrap already exists
            var existing = Object.FindAnyObjectByType<Core.Bootstrap>();
            if (existing != null)
            {
                Debug.Log("[SetupScene] Bootstrap already exists in scene.");
                return;
            }

            // Create GameManager with Bootstrap
            var manager = new GameObject("GameManager");
            manager.AddComponent<Core.Bootstrap>();
            Undo.RegisterCreatedObjectUndo(manager, "Add Bootstrap");

            // Mark scene dirty so user can save
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("[SetupScene] GameManager + Bootstrap added to scene." +
                      "\nHit Play — Bootstrap creates all services, SceneSetup creates camera/map/borders/labels." +
                      "\nThe existing Main Camera will be reused by MapCamera.");
        }
    }
}
