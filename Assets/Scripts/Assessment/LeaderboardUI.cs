using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Displays leaderboard entries in a world-space UI panel.
/// Columns: rank, student, mode (MCQ / Live Dissection), score, time, date.
/// Shows up to 15 entries by default and lets the user scroll for more.
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    public static LeaderboardUI Instance { get; private set; }

    GameObject _canvas;

    const int VisibleRowCount = 12;

    static readonly Color PanelBg = new Color(0.06f, 0.07f, 0.12f, 0.96f);
    static readonly Color RowEven = new Color(0.10f, 0.11f, 0.17f, 0.92f);
    static readonly Color RowOdd = new Color(0.07f, 0.08f, 0.13f, 0.92f);
    static readonly Color RowDivider = new Color(0.20f, 0.22f, 0.30f, 0.55f);
    static readonly Color HeaderBg = new Color(0.14f, 0.18f, 0.30f, 1f);
    static readonly Color Gold = new Color(1f, 0.84f, 0.10f);
    static readonly Color Silver = new Color(0.78f, 0.80f, 0.86f);
    static readonly Color Bronze = new Color(0.85f, 0.55f, 0.25f);
    static readonly Color TextWhite = new Color(0.95f, 0.95f, 0.97f, 1f);
    static readonly Color TextDim = new Color(0.66f, 0.70f, 0.78f, 1f);
    static readonly Color BtnOrange = new Color(0.70f, 0.45f, 0.10f, 1f);
    static readonly Color BtnBlue = new Color(0.18f, 0.35f, 0.62f, 1f);
    static readonly Color McqAccent = new Color(0.45f, 0.85f, 1f);
    static readonly Color LdAccent = new Color(1f, 0.55f, 0.40f);

    // Column geometry — center X, width — laid out so cells never overlap.
    // Row width = 880, cells span [-440, 440].
    //   Rank        x=-390  w= 70 → [-425, -355]
    //   Student     x=-260  w=240 → [-380, -140]   (left-aligned, padded)
    //   Mode        x= -30  w=160 → [-110,   30]
    //   Score       x= 130  w=140 → [  60,  200]
    //   Time        x= 260  w=100 → [ 210,  310]
    //   Date        x= 380  w=140 → [ 310,  450]   (right-aligned)
    const float ColRankX = -390f;
    const float ColStudentX = -260f;
    const float ColModeX = -30f;
    const float ColScoreX = 130f;
    const float ColTimeX = 260f;
    const float ColDateX = 380f;
    const float ColRankW = 70f;
    const float ColStudentW = 240f;
    const float ColModeW = 160f;
    const float ColScoreW = 140f;
    const float ColTimeW = 100f;
    const float ColDateW = 140f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public void Show()
    {
        if (_canvas != null) Destroy(_canvas);
        BuildUI();
    }

    public void Hide()
    {
        if (_canvas != null) { Destroy(_canvas); _canvas = null; }
        var mm = FindFirstObjectByType<MenuManager>();
        if (mm != null) mm.ShowAssessment();
    }

    void BuildUI()
    {
        var cam = Camera.main;
        _canvas = new GameObject("LeaderboardCanvas");
        if (cam != null)
        {
            _canvas.transform.SetParent(cam.transform, false);
            _canvas.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            _canvas.transform.localRotation = Quaternion.identity;
        }

        var canvas = _canvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _canvas.AddComponent<CanvasScaler>();
        _canvas.AddComponent<GraphicRaycaster>();
        _canvas.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

        var rt = _canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(960, 720);
        rt.localScale = Vector3.one * 0.001f;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var bg = MakeRect("Bg", _canvas.transform, Vector2.zero, rt.sizeDelta);
        bg.gameObject.AddComponent<Image>().color = PanelBg;

        MakeText("Title", bg.transform, new Vector2(0, 320), new Vector2(900, 50),
            "LEADERBOARD", 30, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);

        MakeText("Subtitle", bg.transform, new Vector2(0, 285), new Vector2(900, 24),
            "Top scores — higher scores rank first; ties broken by faster time. Scroll for more.",
            14, FontStyle.Italic, TextDim, TextAnchor.MiddleCenter, font);

        BuildHeaderRow(bg.transform, font);
        BuildScrollableRows(bg.transform, font);
        BuildButtons(bg.transform, font);
    }

    void BuildHeaderRow(Transform parent, Font font)
    {
        var hdr = MakeRect("Header", parent, new Vector2(0, 235), new Vector2(880, 40));
        hdr.gameObject.AddComponent<Image>().color = HeaderBg;
        AddCell(hdr, "RANK", ColRankX, ColRankW, 14, FontStyle.Bold, TextDim, TextAnchor.MiddleCenter, font);
        AddCell(hdr, "STUDENT", ColStudentX, ColStudentW, 14, FontStyle.Bold, TextDim, TextAnchor.MiddleLeft, font);
        AddCell(hdr, "MODE", ColModeX, ColModeW, 14, FontStyle.Bold, TextDim, TextAnchor.MiddleCenter, font);
        AddCell(hdr, "SCORE", ColScoreX, ColScoreW, 14, FontStyle.Bold, TextDim, TextAnchor.MiddleCenter, font);
        AddCell(hdr, "TIME", ColTimeX, ColTimeW, 14, FontStyle.Bold, TextDim, TextAnchor.MiddleCenter, font);
        AddCell(hdr, "DATE", ColDateX, ColDateW, 14, FontStyle.Bold, TextDim, TextAnchor.MiddleRight, font);
    }

    void BuildScrollableRows(Transform parent, Font font)
    {
        // Viewport: shows ~12 rows (each 38 px) for a scrollable area of ~456 px.
        // Slightly taller rows + fewer-per-screen makes the leaderboard read
        // less like a dense table and more like a scoreboard.
        const float rowHeight = 38f;
        const float visibleHeight = rowHeight * VisibleRowCount;

        var viewport = MakeRect("Viewport", parent, new Vector2(0, -40), new Vector2(880, visibleHeight));
        viewport.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.9f);
        var mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        var scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;
        scrollRect.viewport = viewport;

        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(viewport, false);
        var content = contentGo.AddComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        var layout = contentGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 0f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.content = content;

        var entries = LeaderboardManager.GetEntries();
        if (entries.Count == 0)
        {
            MakeText("Empty", parent, new Vector2(0, -50), new Vector2(800, 40),
                "No scores recorded yet. Complete a quiz or live dissection to see results!",
                18, FontStyle.Italic, TextDim, TextAnchor.MiddleCenter, font);
            return;
        }

        // Render every entry; the ScrollRect clips the content and lets the
        // user scroll past the first ~12 rows for the rest of the leaderboard.
        for (int i = 0; i < entries.Count; i++)
        {
            BuildRow(content, entries[i], i, font, rowHeight);
        }
    }

    void BuildRow(Transform content, LeaderboardManager.Entry e, int index, Font font, float rowHeight)
    {
        var row = MakeRect($"Row{index}", content, Vector2.zero, new Vector2(880f, rowHeight));
        var rowImg = row.gameObject.AddComponent<Image>();
        rowImg.color = index % 2 == 0 ? RowEven : RowOdd;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = rowHeight;

        // 1-px hairline at the bottom of each row separates entries cleanly.
        var hairline = MakeRect("Sep", row.transform, new Vector2(0f, -rowHeight * 0.5f + 0.5f),
            new Vector2(840f, 1f));
        hairline.gameObject.AddComponent<Image>().color = RowDivider;

        Color rankColor = index == 0 ? Gold : index == 1 ? Silver : index == 2 ? Bronze : TextDim;
        string rankText = index == 0 ? "1st" : index == 1 ? "2nd" : index == 2 ? "3rd" : $"#{index + 1}";
        AddCell(row, rankText, ColRankX, ColRankW, 16, FontStyle.Bold, rankColor, TextAnchor.MiddleCenter, font);

        AddCell(row, TruncateName(e.studentName, 18), ColStudentX, ColStudentW, 16,
            FontStyle.Normal, TextWhite, TextAnchor.MiddleLeft, font);

        Color modeColor = e.mode == "LiveDissection" ? LdAccent : McqAccent;
        AddCell(row, LeaderboardManager.FormatMode(e.mode), ColModeX, ColModeW, 14, FontStyle.Bold,
            modeColor, TextAnchor.MiddleCenter, font);

        float pct = e.totalQuestions > 0 ? (float)e.score / e.totalQuestions * 100f : 0;
        AddCell(row, $"{e.score} / {e.totalQuestions}  ({pct:F0}%)", ColScoreX, ColScoreW, 14,
            FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);

        string timeStr = LeaderboardManager.FormatElapsed(e.elapsedSeconds);
        AddCell(row, timeStr, ColTimeX, ColTimeW, 14,
            FontStyle.Normal, timeStr == "—" ? TextDim : TextWhite, TextAnchor.MiddleCenter, font);

        AddCell(row, FormatDateCompact(e.date), ColDateX, ColDateW, 12, FontStyle.Normal, TextDim,
            TextAnchor.MiddleRight, font);
    }

    static string TruncateName(string name, int max)
    {
        if (string.IsNullOrEmpty(name)) return "—";
        return name.Length <= max ? name : name.Substring(0, max - 1) + "…";
    }

    /// <summary>
    /// Compress a stored ISO date like "2026-04-28 11:08:32" to "Apr 28 · 11:08"
    /// so it fits in a narrow column without overflowing into the row edge.
    /// Falls back to the original string if parsing fails.
    /// </summary>
    static string FormatDateCompact(string date)
    {
        if (string.IsNullOrEmpty(date)) return "";
        if (System.DateTime.TryParse(date, out var dt))
            return dt.ToString("MMM d · HH:mm");
        return date;
    }

    void BuildButtons(Transform parent, Font font)
    {
        var exportGO = MakeRect("ExportBtn", parent, new Vector2(-160, -310), new Vector2(260, 52));
        exportGO.gameObject.AddComponent<Image>().color = BtnBlue;
        var exportBtn = exportGO.gameObject.AddComponent<Button>();
        exportBtn.targetGraphic = exportGO.GetComponent<Image>();
        MakeText("ExpTxt", exportGO, Vector2.zero, new Vector2(240, 48),
            "Export CSV", 17, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);
        exportBtn.onClick.AddListener(() =>
        {
            string path = LeaderboardManager.ExportToCSV();
            Debug.Log($"[Leaderboard] CSV exported to: {path}");
        });

        var backGO = MakeRect("BackBtn", parent, new Vector2(160, -310), new Vector2(260, 52));
        backGO.gameObject.AddComponent<Image>().color = BtnOrange;
        var backBtn = backGO.gameObject.AddComponent<Button>();
        backBtn.targetGraphic = backGO.GetComponent<Image>();
        MakeText("BackTxt", backGO, Vector2.zero, new Vector2(240, 48),
            "Back", 17, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);
        backBtn.onClick.AddListener(Hide);
    }

    static RectTransform MakeRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Text MakeText(string name, Transform parent, Vector2 pos, Vector2 size,
        string text, int fontSize, FontStyle style, Color color, TextAnchor align, Font font)
    {
        var rt = MakeRect(name, parent, pos, size);
        var t = rt.gameObject.AddComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.font = font;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static Text MakeText(string name, RectTransform parent, Vector2 pos, Vector2 size,
        string text, int fontSize, FontStyle style, Color color, TextAnchor align, Font font)
    {
        return MakeText(name, parent.transform, pos, size, text, fontSize, style, color, align, font);
    }

    static void AddCell(RectTransform parent, string text, float x, float width, int fontSize,
        FontStyle style, Color color, TextAnchor align, Font font)
    {
        // 8px inner gutter on left/right keeps text from kissing the cell edge,
        // which previously caused the rank "1" to look glued to the student name.
        var rt = MakeRect("Cell", parent.transform, new Vector2(x, 0),
            new Vector2(width - 16f, parent.sizeDelta.y - 4f));
        var t = rt.gameObject.AddComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.font = font;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
    }
}
