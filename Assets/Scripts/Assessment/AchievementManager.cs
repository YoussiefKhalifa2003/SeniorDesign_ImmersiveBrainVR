using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Tracks and awards achievements across all modes.
/// Persisted per-user via PlayerPrefs.
/// </summary>
public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }

    [System.Serializable]
    public class Achievement
    {
        public string id;
        public string title;
        public string description;
        public bool unlocked;
    }

    static readonly Achievement[] Definitions = new Achievement[]
    {
        new Achievement { id = "TutorialComplete", title = "First Steps",
            description = "Complete the tutorial." },
        new Achievement { id = "PlayComplete", title = "Explorer",
            description = "Complete Play mode." },
        new Achievement { id = "MCQPerfect", title = "Perfect Score",
            description = "Get 100% on an MCQ quiz." },
        new Achievement { id = "LDPerfect", title = "Surgeon's Precision",
            description = "Get a perfect score in Live Dissection." },
        new Achievement { id = "Streak5", title = "On Fire",
            description = "Achieve a 5x streak in any mode." },
        new Achievement { id = "Streak10", title = "Unstoppable",
            description = "Achieve a 10x streak in any mode." },
        new Achievement { id = "MCQ10", title = "Quiz Veteran",
            description = "Complete 10 MCQ quiz sessions." },
        new Achievement { id = "LD5", title = "Dissection Expert",
            description = "Complete 5 Live Dissection sessions." },
        new Achievement { id = "AllModes", title = "Well-Rounded",
            description = "Complete all four modes at least once." },
        new Achievement { id = "SpeedDemon", title = "Speed Demon",
            description = "Complete a Live Dissection with 0 wrong answers." },
    };

    List<Achievement> _achievements = new List<Achievement>();
    GameObject _popupGO;
    Coroutine _popupRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        LoadAchievements();
    }

    string PrefsKey => $"Achievements_{SessionData.UserName ?? "default"}";

    void LoadAchievements()
    {
        _achievements.Clear();
        string saved = PlayerPrefs.GetString(PrefsKey, "");
        HashSet<string> unlocked = new HashSet<string>();
        if (!string.IsNullOrEmpty(saved))
            foreach (var id in saved.Split(','))
                unlocked.Add(id);

        foreach (var def in Definitions)
        {
            _achievements.Add(new Achievement
            {
                id = def.id,
                title = def.title,
                description = def.description,
                unlocked = unlocked.Contains(def.id)
            });
        }
    }

    void SaveAchievements()
    {
        var ids = new List<string>();
        foreach (var a in _achievements)
            if (a.unlocked) ids.Add(a.id);
        PlayerPrefs.SetString(PrefsKey, string.Join(",", ids));
        PlayerPrefs.Save();
    }

    public void ReloadForUser()
    {
        LoadAchievements();
    }

    public bool TryUnlock(string achievementId)
    {
        foreach (var a in _achievements)
        {
            if (a.id == achievementId && !a.unlocked)
            {
                a.unlocked = true;
                SaveAchievements();
                ShowPopup(a.title);
                if (SoundManager.Instance != null) SoundManager.Instance.PlayAchievement();
                Debug.Log($"[Achievement] Unlocked: {a.title}");
                return true;
            }
        }
        return false;
    }

    public static int GetUnlockedCount()
    {
        if (Instance == null) return 0;
        int c = 0;
        foreach (var a in Instance._achievements) if (a.unlocked) c++;
        return c;
    }

    public static int GetTotalCount()
    {
        return Definitions.Length;
    }

    public List<Achievement> GetAllAchievements()
    {
        return new List<Achievement>(_achievements);
    }

    // Check common conditions
    public void CheckTutorialComplete()
    {
        if (ProgressTracker.TutorialCompleted) TryUnlock("TutorialComplete");
        CheckAllModes();
    }

    public void CheckPlayComplete()
    {
        if (ProgressTracker.PlayCompleted) TryUnlock("PlayComplete");
        CheckAllModes();
    }

    public void CheckMCQScore(int score, int total)
    {
        if (total > 0 && score == total) TryUnlock("MCQPerfect");

        var entries = LeaderboardManager.GetEntries();
        int mcqCount = 0;
        string user = SessionData.UserName ?? "";
        foreach (var e in entries)
            if (e.studentName == user && e.mode == "MCQ") mcqCount++;
        if (mcqCount >= 10) TryUnlock("MCQ10");
    }

    public void CheckLDScore(int score, int total, int totalWrong)
    {
        if (total > 0 && score == total) TryUnlock("LDPerfect");
        if (totalWrong == 0) TryUnlock("SpeedDemon");

        var entries = LeaderboardManager.GetEntries();
        int ldCount = 0;
        string user = SessionData.UserName ?? "";
        foreach (var e in entries)
            if (e.studentName == user && e.mode == "LiveDissection") ldCount++;
        if (ldCount >= 5) TryUnlock("LD5");
    }

    public void CheckStreak(int streak)
    {
        if (streak >= 5) TryUnlock("Streak5");
        if (streak >= 10) TryUnlock("Streak10");
    }

    void CheckAllModes()
    {
        var entries = LeaderboardManager.GetEntries();
        string user = SessionData.UserName ?? "";
        bool hasMCQ = false, hasLD = false;
        foreach (var e in entries)
        {
            if (e.studentName != user) continue;
            if (e.mode == "MCQ") hasMCQ = true;
            if (e.mode == "LiveDissection") hasLD = true;
        }
        if (ProgressTracker.TutorialCompleted && ProgressTracker.PlayCompleted && hasMCQ && hasLD)
            TryUnlock("AllModes");
    }

    // Popup
    void ShowPopup(string title)
    {
        if (_popupRoutine != null) StopCoroutine(_popupRoutine);
        if (_popupGO != null) Destroy(_popupGO);

        var cam = Camera.main;
        if (cam == null) return;

        _popupGO = new GameObject("AchievementPopup");
        var canvas = _popupGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _popupGO.AddComponent<CanvasScaler>();

        var rt = _popupGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400, 80);
        rt.localScale = Vector3.one * 0.0006f;
        _popupGO.transform.position = cam.transform.position + cam.transform.forward * 0.6f + Vector3.up * 0.15f;
        _popupGO.transform.rotation = Quaternion.LookRotation(
            _popupGO.transform.position - cam.transform.position);

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var bg = new GameObject("Bg");
        bg.transform.SetParent(_popupGO.transform, false);
        var bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one; bgRT.sizeDelta = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.15f, 0.4f, 0.2f, 0.92f);

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(bg.transform, false);
        var txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one; txtRT.sizeDelta = Vector2.zero;
        var txt = txtGO.AddComponent<Text>();
        txt.text = $"\u2b50 Achievement Unlocked!\n{title}";
        txt.font = font;
        txt.fontSize = 16;
        txt.color = new Color(1f, 0.95f, 0.7f);
        txt.alignment = TextAnchor.MiddleCenter;

        _popupRoutine = StartCoroutine(DismissPopup(3.5f));
    }

    IEnumerator DismissPopup(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_popupGO != null) Destroy(_popupGO);
        _popupGO = null;
    }
}
