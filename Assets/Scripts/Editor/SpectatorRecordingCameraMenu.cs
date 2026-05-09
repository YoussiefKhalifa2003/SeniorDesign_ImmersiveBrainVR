#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-click setup for <see cref="SpectatorRecordingCamera"/> in open scenes (Editor toolbar).
/// </summary>
internal static class SpectatorRecordingCameraMenu
{
    public const string RootName = "SpectatorRecording";
    private const string MenuPath = "Tools/Immersive Brain/Add OBS Recording Camera";
    private const string PreviewPath = "Tools/Immersive Brain/Open OBS Camera Preview";

    [MenuItem(MenuPath)]
    static void AddObsRecordingCameraToScene()
    {
        GameObject root = FindRootInActiveScene();
        SpectatorRecordingCamera existingComp = root != null ? root.GetComponent<SpectatorRecordingCamera>() : null;

        if (existingComp != null)
        {
            SelectAndPing(root);
            SpectatorRecordingCameraPreviewWindow.Open();
            Debug.Log($"[SpectatorRecordingCameraMenu] Already present: `{RootName}`. Opened OBS Camera Preview.");
            return;
        }

        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Add OBS Recording Camera");
            Undo.AddComponent<SpectatorRecordingCamera>(root);
        }
        else
        {
            Undo.AddComponent<SpectatorRecordingCamera>(root);
        }

        MarkActiveSceneDirty();
        SelectAndPing(root);
        SpectatorRecordingCameraPreviewWindow.Open();

        Debug.Log(
            $"[SpectatorRecordingCameraMenu] Added `{RootName}` with SpectatorRecordingCamera. Press Play → capture the OBS Camera Preview window.",
            root);
    }

    [MenuItem(PreviewPath)]
    static void OpenObsPreviewWindow()
    {
        SpectatorRecordingCameraPreviewWindow.Open();
    }

    static GameObject FindRootInActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return null;

        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == RootName)
                return go;
        }

        return null;
    }

    static void SelectAndPing(Object obj)
    {
        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);
    }

    static void MarkActiveSceneDirty()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);
    }
}

internal sealed class SpectatorRecordingCameraPreviewWindow : EditorWindow
{
    public static void Open()
    {
        SpectatorRecordingCameraPreviewWindow window = GetWindow<SpectatorRecordingCameraPreviewWindow>("OBS Camera Preview");
        window.minSize = new Vector2(640f, 360f);
        window.Show();
    }

    void OnGUI()
    {
        RenderTexture preview = SpectatorRecordingCamera.PreviewTexture;
        if (preview == null)
        {
            EditorGUILayout.HelpBox(
                "Press Play after adding Tools > Immersive Brain > Add OBS Recording Camera.\n\n" +
                "This window will show the RecordingCamera feed for OBS.",
                MessageType.Info);
            return;
        }

        Rect rect = GUILayoutUtility.GetRect(position.width, position.height, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit, false);
    }

    void Update()
    {
        Repaint();
    }
}
#endif
