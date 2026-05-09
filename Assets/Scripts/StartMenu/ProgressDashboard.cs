using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using System.Collections.Generic;

/// <summary>
/// Unified progress dashboard showing stats from all modes:
/// Tutorial, Play, MCQ, and Live Dissection.
/// </summary>
public class ProgressDashboard : MonoBehaviour
{
    public static ProgressDashboard Instance { get; private set; }

    GameObject _dashCanvas;
    bool _visible;

    static readonly Color PanelBg = new Color(0.06f, 0.06f, 0.10f, 0.95f);
    static readonly Color HeaderColor = new Color(0.3f, 0.6f, 0.9f);
    static readonly Color TextWhite = new Color(0.95f, 0.95f, 0.97f);
    static readonly Color TextDim = new Color(0.70f, 0.70f, 0.75f);
    static readonly Color SuccessColor = new Color(0.3f, 0.85f, 0.4f);
    static readonly Color PendingColor = new Color(0.8f, 0.6f, 0.2f);
    static readonly Color BtnOrange = new Color(0.70f, 0.45f, 0.10f);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public void Toggle()
    {
        if (_visible) Hide();
        else Show();
    }

    public void Show()
    {
        if (_dashCanvas != null) Destroy(_dashCanvas);
        BuildDashboard();
        _visible = true;
    }

    public void Hide()
    {
        if (_dashCanvas != null) Destroy(_dashCanvas);
        _dashCanvas = null;
        _visible = false;
    }

    void BuildDashboard()
    {
        _dashCanvas = new GameObject("ProgressDashboard");
        var canvas = _dashCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _dashCanvas.AddComponent<CanvasScaler>();
        _dashCanvas.AddComponent<TrackedDeviceGraphicRaycaster>();

        var rt = _dashCanvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(800, 500);
        rt.localScale = Vector3.one * 0.0008f;

        var cam = Camera.main;
        if (cam != null)
        {
            _dashCanvas.transform.position = cam.transform.position + cam.transform.forward * 0.9f;
            _dashCanvas.transform.rotation = Quaternion.LookRotation(
                _dashCanvas.transform.position - cam.transform.position);
        }

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var bg = MakeRect("Bg", _dashCanvas.transform, Vector2.zero, rt.sizeDelta);
        bg.gameObject.AddComponent<Image>().color = PanelBg;

        string user = string.IsNullOrEmpty(SessionData.UserName) ? "Student" : SessionData.UserName;
        MakeText("Title", bg, new Vector2(0, 220), new Vector2(750, 36),
            $"Progress Dashboard — {user}", 20, FontStyle.Bold, HeaderColor, TextAnchor.MiddleCenter, font);

        float y = 170f;

        // Tutorial
        bool tutDone = ProgressTracker.TutorialCompleted;
        MakeText("TutHeader", bg, new Vector2(-200, y), new Vector2(350, 26),
            "TUTORIAL", 16, FontStyle.Bold, HeaderColor, TextAnchor.MiddleLeft, font);
        MakeText("TutStatus", bg, new Vector2(150, y), new Vector2(200, 26),
            tutDone ? "\u2713 Completed" : "\u25cb Not completed",
            14, FontStyle.Normal, tutDone ? SuccessColor : PendingColor, TextAnchor.MiddleLeft, font);
        y -= 40f;

        // Play
        bool playDone = ProgressTracker.PlayCompleted;
        MakeText("PlayHeader", bg, new Vector2(-200, y), new Vector2(350, 26),
            "PLAY MODE", 16, FontStyle.Bold, HeaderColor, TextAnchor.MiddleLeft, font);
        MakeText("PlayStatus", bg, new Vector2(150, y), new Vector2(200, 26),
            playDone ? "\u2713 Completed" : "\u25cb Not completed",
            14, FontStyle.Normal, playDone ? SuccessColor : PendingColor, TextAnchor.MiddleLeft, font);
        y -= 40f;

        // MCQ
        var entries = LeaderboardManager.GetEntries();
        string userName = SessionData.UserName ?? "";
        int mcqBest = 0, mcqTotal = 0, mcqAttempts = 0;
        int ldBest = 0, ldTotal = 0, ldAttempts = 0;

        foreach (var e in entries)
        {
            if (e.studentName != userName) continue;
            if (e.mode == "MCQ")
            {
                mcqAttempts++;
                if (e.score > mcqBest) { mcqBest = e.score; mcqTotal = e.totalQuestions; }
            }
            else if (e.mode == "LiveDissection")
            {
                ldAttempts++;
                if (e.score > ldBest) { ldBest = e.score; ldTotal = e.totalQuestions; }
            }
        }

        MakeText("MCQHeader", bg, new Vector2(-200, y), new Vector2(350, 26),
            "MCQ QUIZ", 16, FontStyle.Bold, HeaderColor, TextAnchor.MiddleLeft, font);
        string mcqStr = mcqAttempts > 0
            ? $"Best: {mcqBest}/{mcqTotal}  |  Attempts: {mcqAttempts}"
            : "No attempts yet";
        MakeText("MCQStats", bg, new Vector2(150, y), new Vector2(300, 26),
            mcqStr, 13, FontStyle.Normal, TextWhite, TextAnchor.MiddleLeft, font);
        y -= 40f;

        // Live Dissection
        MakeText("LDHeader", bg, new Vector2(-200, y), new Vector2(350, 26),
            "LIVE DISSECTION", 16, FontStyle.Bold, HeaderColor, TextAnchor.MiddleLeft, font);
        string ldStr = ldAttempts > 0
            ? $"Best: {ldBest}/{ldTotal}  |  Attempts: {ldAttempts}"
            : "No attempts yet";
        MakeText("LDStats", bg, new Vector2(150, y), new Vector2(300, 26),
            ldStr, 13, FontStyle.Normal, TextWhite, TextAnchor.MiddleLeft, font);
        y -= 50f;

        // Overall summary
        int totalModes = 4;
        int completed = (tutDone ? 1 : 0) + (playDone ? 1 : 0) + (mcqAttempts > 0 ? 1 : 0) + (ldAttempts > 0 ? 1 : 0);
        MakeText("SummaryLine", bg, new Vector2(0, y), new Vector2(700, 26),
            $"Overall: {completed}/{totalModes} modes attempted",
            15, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);
        y -= 50f;

        // Achievements count
        int unlocked = AchievementManager.GetUnlockedCount();
        int total = AchievementManager.GetTotalCount();
        MakeText("AchLine", bg, new Vector2(0, y), new Vector2(700, 26),
            $"Achievements: {unlocked}/{total} unlocked",
            14, FontStyle.Normal, unlocked > 0 ? SuccessColor : TextDim, TextAnchor.MiddleCenter, font);

        // Close button
        var closeBtn = MakeRect("CloseBtn", bg, new Vector2(0, -220), new Vector2(200, 40));
        closeBtn.gameObject.AddComponent<Image>().color = BtnOrange;
        var btn = closeBtn.gameObject.AddComponent<Button>();
        btn.targetGraphic = closeBtn.GetComponent<Image>();
        btn.onClick.AddListener(Hide);
        MakeText("CloseLbl", closeBtn, Vector2.zero, new Vector2(200, 40),
            "Close", 15, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);
    }

    static RectTransform MakeRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Text MakeText(string name, Transform parent, Vector2 pos, Vector2 size,
        string text, int fontSize, FontStyle style, Color color, TextAnchor align, Font font)
    {
        var rt = MakeRect(name, parent, pos, size);
        var t = rt.gameObject.AddComponent<Text>();
        t.text = text; t.fontSize = fontSize; t.fontStyle = style;
        t.color = color; t.alignment = align; t.font = font;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
}
