#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// One-time editor utility: creates a PanelSettings asset with the default Unity theme.
/// Run via menu: WarStrategy > Create Panel Settings
/// </summary>
public static class CreatePanelSettings
{
    [MenuItem("WarStrategy/Create Panel Settings")]
    public static void Create()
    {
        // Create PanelSettings asset
        var ps = ScriptableObject.CreateInstance<PanelSettings>();
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution = new Vector2Int(1920, 1080);
        ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        ps.match = 0.5f;

        // Try to find or create a default theme
        string[] themeGuids = AssetDatabase.FindAssets("t:ThemeStyleSheet");
        if (themeGuids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(themeGuids[0]);
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
            if (theme != null)
            {
                ps.themeStyleSheet = theme;
                Debug.Log($"[CreatePanelSettings] Using existing theme: {path}");
            }
        }

        // Save as asset
        string dir = "Assets/Resources/UI";
        if (!AssetDatabase.IsValidFolder(dir))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "UI");
        }

        AssetDatabase.CreateAsset(ps, $"{dir}/GamePanelSettings.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CreatePanelSettings] Created PanelSettings at {dir}/GamePanelSettings.asset");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = ps;
    }
}
#endif
