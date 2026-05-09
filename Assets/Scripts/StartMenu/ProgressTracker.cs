using UnityEngine;

/// <summary>
/// Tracks which modes the student has completed.
/// Assessment is gated behind both Tutorial and Play completion.
/// Persisted via PlayerPrefs with per-user keys (BD_{username}_TutDone).
/// A new username gets fresh defaults automatically.
/// </summary>
public static class ProgressTracker
{
    static string UserPrefix => string.IsNullOrEmpty(SessionData.UserName) ? "" : SessionData.UserName;
    static string TutorialKey => $"BD_{UserPrefix}_TutDone";
    static string PlayKey => $"BD_{UserPrefix}_PlayDone";

    public static bool TutorialCompleted
    {
        get => PlayerPrefs.GetInt(TutorialKey, 0) == 1;
        set { PlayerPrefs.SetInt(TutorialKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool PlayCompleted
    {
        get => PlayerPrefs.GetInt(PlayKey, 0) == 1;
        set { PlayerPrefs.SetInt(PlayKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool CanAccessPlay => TutorialCompleted;
    public static bool CanAccessAssessment => TutorialCompleted && PlayCompleted;

    public static void MarkTutorialComplete()
    {
        TutorialCompleted = true;
        Debug.Log($"[ProgressTracker] Tutorial marked complete for '{UserPrefix}'.");
    }

    public static void MarkPlayComplete()
    {
        PlayCompleted = true;
        Debug.Log($"[ProgressTracker] Play mode marked complete for '{UserPrefix}'.");
    }

    public static void ResetProgress()
    {
        TutorialCompleted = false;
        PlayCompleted = false;
        Debug.Log($"[ProgressTracker] Progress reset for '{UserPrefix}'.");
    }
}
