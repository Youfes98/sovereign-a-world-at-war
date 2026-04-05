#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class CreatePanelSettingsOnLoad
{
    static CreatePanelSettingsOnLoad()
    {
        // Run once on next editor update (after domain reload)
        EditorApplication.delayCall += () =>
        {
            string path = "Assets/Resources/UI/GamePanelSettings.asset";
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(path) != null)
                return; // Already exists

            // Ensure directory
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
                AssetDatabase.CreateFolder("Assets/Resources", "UI");

            // Create PanelSettings
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;

            // Find default theme
            string[] guids = AssetDatabase.FindAssets("t:ThemeStyleSheet");
            foreach (var guid in guids)
            {
                var themePath = AssetDatabase.GUIDToAssetPath(guid);
                var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
                if (theme != null)
                {
                    ps.themeStyleSheet = theme;
                    Debug.Log($"[AutoSetup] Assigned theme: {themePath}");
                    break;
                }
            }

            // If no theme found, create one
            if (ps.themeStyleSheet == null)
            {
                // Create a TSS that imports Unity's default theme
                string tssPath = "Assets/Resources/UI/DefaultRuntimeTheme.tss";
                System.IO.File.WriteAllText(tssPath, "@import url(\"unity-theme://default\");\nVisualElement {}");
                AssetDatabase.ImportAsset(tssPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                
                var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(tssPath);
                if (theme != null)
                {
                    ps.themeStyleSheet = theme;
                    Debug.Log("[AutoSetup] Created and assigned DefaultRuntimeTheme.tss");
                }
            }

            AssetDatabase.CreateAsset(ps, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AutoSetup] Created PanelSettings at {path}");
        };
    }
}
#endif
