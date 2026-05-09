using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles scene loading and mode switching for Brain Dissection VR.
/// Tutorial, Play, and Assessment each have their own scene.
/// SessionData.UserName persists across scene loads (static).
/// </summary>
public static class SceneFlowManager
{
    public const string MainMenuScene = "SampleScene";
    public const string TutorialScene = "TutorialScene";
    public const string PlayScene = "PlayScene";
    public const string AssessmentScene = "AssessmentScene";
    public const string LiveDissectionScene = "LiveDissectionScene";

    public static bool IsLoading { get; private set; }

    public static void LoadMainMenu()
    {
        LoadScene(MainMenuScene);
    }

    public static void LoadTutorialScene()
    {
        LoadScene(TutorialScene);
    }

    public static void LoadPlayScene()
    {
        LoadScene(PlayScene);
    }

    public static void LoadAssessmentScene()
    {
        LoadScene(AssessmentScene);
    }

    public static void LoadLiveDissectionScene()
    {
        LoadScene(LiveDissectionScene);
    }

    static void LoadScene(string sceneName)
    {
        if (IsLoading) return;

        var active = SceneManager.GetActiveScene();
        if (active.name == sceneName)
        {
            Debug.Log($"[SceneFlowManager] Already in {sceneName}");
            return;
        }

        IsLoading = true;
        SceneManager.LoadScene(sceneName);
        IsLoading = false;
        Debug.Log($"[SceneFlowManager] Loaded {sceneName}");
    }

    public static IEnumerator LoadSceneAsync(string sceneName, System.Action onComplete = null)
    {
        if (IsLoading) yield break;

        IsLoading = true;
        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op != null)
        {
            while (!op.isDone)
                yield return null;
        }
        IsLoading = false;
        onComplete?.Invoke();
        Debug.Log($"[SceneFlowManager] Loaded {sceneName} (async)");
    }
}
