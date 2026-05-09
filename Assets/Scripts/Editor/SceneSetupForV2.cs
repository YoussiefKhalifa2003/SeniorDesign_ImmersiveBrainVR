using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Brain Dissection > Create V2 Scenes
/// Creates PlayScene and AssessmentScene for the v2 spec.
/// Adds them to Build Settings.
/// </summary>
public static class SceneSetupForV2
{
    const string ScenesPath = "Assets/Scenes";
    const string SampleScene = "SampleScene";
    const string PlayScene = "PlayScene";
    const string AssessmentScene = "AssessmentScene";

    [MenuItem("Tools/Brain Dissection/Create V2 Scenes (Play + Assessment)")]
    public static void CreateV2Scenes()
    {
        string samplePath = $"{ScenesPath}/{SampleScene}.unity";
        string playPath = $"{ScenesPath}/{PlayScene}.unity";
        string assessmentPath = $"{ScenesPath}/{AssessmentScene}.unity";

        if (!System.IO.File.Exists(samplePath))
        {
            Debug.LogError($"[SceneSetup] {samplePath} not found.");
            return;
        }

        if (!System.IO.File.Exists(playPath))
        {
            AssetDatabase.CopyAsset(samplePath, playPath);
            AssetDatabase.Refresh();
            Debug.Log($"[SceneSetup] Created {PlayScene} from {SampleScene}");
        }
        else
        {
            Debug.Log($"[SceneSetup] {PlayScene} already exists.");
        }

        if (!System.IO.File.Exists(assessmentPath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, assessmentPath);
            Debug.Log($"[SceneSetup] Created empty {AssessmentScene}");
        }
        else
        {
            Debug.Log($"[SceneSetup] {AssessmentScene} already exists.");
        }

        AddScenesToBuildSettings();
    }

    static void AddScenesToBuildSettings()
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        bool AddIfMissing(string path)
        {
            int idx = scenes.FindIndex(s => s.path.EndsWith(path));
            if (idx < 0)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
                return true;
            }
            return false;
        }

        bool changed = false;
        changed |= AddIfMissing($"{ScenesPath}/{PlayScene}.unity");
        changed |= AddIfMissing($"{ScenesPath}/{AssessmentScene}.unity");

        if (changed)
        {
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[SceneSetup] Added PlayScene and AssessmentScene to Build Settings.");
        }
    }
}
