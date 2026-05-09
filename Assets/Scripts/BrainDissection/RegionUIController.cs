using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls all world-space UI elements: tool status, status message,
/// hover label, region details panel, hemisphere buttons, control buttons, opacity slider.
///
/// New fields for the lab tool flow are set by the editor setup script
/// and updated at runtime by LabToolManager.
/// </summary>
public class RegionUIController : MonoBehaviour
{
    // ========================= HOVER =========================
    [Header("Hover Label")]
    public Text hoverNameTextLegacy;
    [Tooltip("The parent panel of the hover text (the background)")]
    public GameObject hoverPanel;

    // ========================= DETAILS =========================
    [Header("Region Details Panel")]
    public GameObject detailsPanel;
    public Text regionTitleTextLegacy;
    public Text regionShortDescriptionTextLegacy;
    public Text regionDetailedDescriptionTextLegacy;

    // ========================= SLIDER =========================
    [Header("Opacity Slider")]
    public Slider opacitySlider;

    // ========================= MAIN PANEL =========================
    [Header("Main Button Panel")]
    public GameObject mainButtonPanel;

    // ========================= LAB TOOL UI =========================
    [Header("Lab Tool UI (set by editor setup)")]
    [Tooltip("Text showing tool equip status (Gloves/Knife/Tweezers)")]
    public Text toolStatusText;

    [Tooltip("Text showing current instruction / status message")]
    public Text statusMessageText;

    [Tooltip("Panel containing View Left / View Right / Show Whole buttons")]
    public GameObject hemispherePanel;

    [Tooltip("Panel containing Rotate, Zoom, Reset, Opacity controls")]
    public GameObject controlPanel;

    // ========================= COMPARISON PANEL =========================
    // Built lazily as a SIBLING of detailsPanel (not a child) the first time
    // ShowComparison runs. This makes the side-by-side compare panel a fully
    // separate world-space panel from the single-region details panel — only
    // one is active at a time so they can never overlap or fight for clicks.
    GameObject _comparisonPanel;
    Text _comparisonTitleA;
    Text _comparisonSubA;
    Text _comparisonBodyA;
    Text _comparisonTitleB;
    Text _comparisonSubB;
    Text _comparisonBodyB;

    public bool IsComparisonShown => _comparisonPanel != null && _comparisonPanel.activeSelf;

    void Start()
    {
        ApplyReadableFontSizes();
    }

    void ApplyReadableFontSizes()
    {
        SetFontSize(hoverNameTextLegacy, 32);
        SetFontSize(regionTitleTextLegacy, 30);
        // Subtitle is intentionally small in the new details layout
        // ("Brain region: ..."), so we only enforce a sane minimum.
        SetFontSize(regionShortDescriptionTextLegacy, 16);
        SetFontSize(regionDetailedDescriptionTextLegacy, 22);
        SetFontSize(toolStatusText, 18);
        SetFontSize(statusMessageText, 20);

        if (mainButtonPanel != null)
        {
            foreach (var text in mainButtonPanel.GetComponentsInChildren<Text>(true))
                text.fontSize = Mathf.Max(text.fontSize, 18);
        }

        if (detailsPanel != null)
        {
            foreach (var text in detailsPanel.GetComponentsInChildren<Text>(true))
                text.fontSize = Mathf.Max(text.fontSize, 18);
        }

        if (hemispherePanel != null)
        {
            foreach (var text in hemispherePanel.GetComponentsInChildren<Text>(true))
                text.fontSize = Mathf.Max(text.fontSize, 18);
        }

        if (controlPanel != null)
        {
            foreach (var text in controlPanel.GetComponentsInChildren<Text>(true))
                text.fontSize = Mathf.Max(text.fontSize, 18);
        }
    }

    static void SetFontSize(Text text, int size)
    {
        if (text != null)
            text.fontSize = Mathf.Max(text.fontSize, size);
    }

    // ========================= HOVER METHODS =========================

    public void ShowHoverName(string regionName)
    {
        if (hoverNameTextLegacy != null)
            hoverNameTextLegacy.text = regionName;

        if (hoverPanel != null)
            hoverPanel.SetActive(true);
        else if (hoverNameTextLegacy != null)
            hoverNameTextLegacy.transform.parent.gameObject.SetActive(true);
    }

    public void ClearHoverName()
    {
        if (hoverPanel != null)
            hoverPanel.SetActive(false);
        else if (hoverNameTextLegacy != null)
            hoverNameTextLegacy.transform.parent.gameObject.SetActive(false);
    }

    // ========================= REGION DETAILS =========================

    public void ShowRegionDetails(RegionData data, bool hideMainPanel = true)
    {
        if (data == null) return;
        // The compare panel is a separate sibling — hide it so the single-
        // region details panel can take over the same world-space slot.
        if (_comparisonPanel != null && _comparisonPanel.activeSelf)
            _comparisonPanel.SetActive(false);
        if (detailsPanel != null) detailsPanel.SetActive(true);
        if (mainButtonPanel != null) mainButtonPanel.SetActive(!hideMainPanel);

        if (regionTitleTextLegacy != null) regionTitleTextLegacy.text = data.displayName;

        // Subtitle (small italic gray): "Brain region: <displayName>".
        if (regionShortDescriptionTextLegacy != null)
        {
            regionShortDescriptionTextLegacy.text = $"Brain region: {data.displayName}";
            regionShortDescriptionTextLegacy.fontStyle = FontStyle.Italic;
            regionShortDescriptionTextLegacy.alignment = TextAnchor.MiddleCenter;
            regionShortDescriptionTextLegacy.lineSpacing = 1.0f;
            regionShortDescriptionTextLegacy.horizontalOverflow = HorizontalWrapMode.Wrap;
            regionShortDescriptionTextLegacy.verticalOverflow = VerticalWrapMode.Overflow;
            regionShortDescriptionTextLegacy.gameObject.SetActive(true);
        }

        // Body paragraph: prefer detailedDescription (curated, two-sentence text).
        // We split the curated "<role>. <clinical>." pattern into a visible
        // paragraph break so the panel reads as two short paragraphs (matching
        // the Angular Gyrus reference) instead of one wall of text.
        if (regionDetailedDescriptionTextLegacy != null)
        {
            string body = !string.IsNullOrWhiteSpace(data.detailedDescription)
                ? data.detailedDescription
                : data.shortDescription;

            regionDetailedDescriptionTextLegacy.text = FormatBodyParagraphs(body);
            regionDetailedDescriptionTextLegacy.fontStyle = FontStyle.Italic;
            regionDetailedDescriptionTextLegacy.alignment = TextAnchor.MiddleCenter;
            regionDetailedDescriptionTextLegacy.lineSpacing = 1.30f;
            regionDetailedDescriptionTextLegacy.horizontalOverflow = HorizontalWrapMode.Wrap;
            regionDetailedDescriptionTextLegacy.verticalOverflow = VerticalWrapMode.Overflow;
            regionDetailedDescriptionTextLegacy.gameObject.SetActive(!string.IsNullOrWhiteSpace(body));
        }
    }

    /// <summary>
    /// Split a curated description into up to two short paragraphs at the
    /// first sentence boundary. Inserts a blank line so the body reads with
    /// breathing room instead of as one block of italics.
    /// </summary>
    static string FormatBodyParagraphs(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "";
        body = body.Trim();
        if (body.Contains("\n\n")) return body;

        int splitAt = -1;
        for (int i = 0; i < body.Length - 1; i++)
        {
            if (body[i] == '.' && body[i + 1] == ' ')
            {
                splitAt = i + 1;
                break;
            }
        }
        if (splitAt < 0 || splitAt > body.Length - 8) return body;
        return body.Substring(0, splitAt).TrimEnd() + "\n\n" + body.Substring(splitAt).TrimStart();
    }

    public void HideRegionDetails()
    {
        if (_comparisonPanel != null) _comparisonPanel.SetActive(false);
        if (detailsPanel != null) detailsPanel.SetActive(false);
        if (mainButtonPanel != null) mainButtonPanel.SetActive(true);
    }

    // ========================= SIDE-BY-SIDE COMPARISON =========================

    /// <summary>
    /// Show a dedicated side-by-side comparison panel. This is its OWN
    /// sibling panel to detailsPanel — when comparison is on, the single-
    /// region details panel and main panel are hidden so they can never
    /// visually overlap or compete for raycasts.
    /// </summary>
    public void ShowComparison(RegionData a, RegionData b)
    {
        if (a == null || b == null || detailsPanel == null) return;

        EnsureComparisonPanel();
        if (_comparisonPanel == null) return;

        if (detailsPanel != null) detailsPanel.SetActive(false);
        if (mainButtonPanel != null) mainButtonPanel.SetActive(false);

        _comparisonPanel.SetActive(true);

        if (_comparisonTitleA != null) _comparisonTitleA.text = a.displayName;
        if (_comparisonSubA != null) _comparisonSubA.text = "Region A";
        if (_comparisonBodyA != null) _comparisonBodyA.text = FormatBodyParagraphs(SelectBody(a));

        if (_comparisonTitleB != null) _comparisonTitleB.text = b.displayName;
        if (_comparisonSubB != null) _comparisonSubB.text = "Region B";
        if (_comparisonBodyB != null) _comparisonBodyB.text = FormatBodyParagraphs(SelectBody(b));
    }

    public void HideComparison()
    {
        if (_comparisonPanel != null) _comparisonPanel.SetActive(false);
        if (detailsPanel != null) detailsPanel.SetActive(false);
        if (mainButtonPanel != null) mainButtonPanel.SetActive(true);
    }

    static string SelectBody(RegionData data)
    {
        if (data == null) return "";
        return !string.IsNullOrWhiteSpace(data.detailedDescription)
            ? data.detailedDescription
            : (data.shortDescription ?? "");
    }

    void EnsureComparisonPanel()
    {
        if (_comparisonPanel != null || detailsPanel == null) return;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var panelBg = new Color(0.05f, 0.06f, 0.12f, 0.97f);
        var accent = new Color(0.30f, 0.55f, 0.85f, 1f);
        var divider = new Color(0.30f, 0.40f, 0.55f, 0.7f);
        var textWhite = new Color(0.95f, 0.95f, 0.97f, 1f);
        var textDim = new Color(0.72f, 0.75f, 0.82f, 1f);
        var tintCyan = new Color(0.32f, 0.85f, 1.00f, 1f);
        var tintMagenta = new Color(1.00f, 0.55f, 0.88f, 1f);

        // Build as a SIBLING of the details panel — same canvas, same world
        // location, but a separate GameObject. Only one is active at a time.
        var detailsRT = detailsPanel.GetComponent<RectTransform>();
        var parent = detailsPanel.transform.parent;
        Vector2 panelSize = detailsRT != null ? detailsRT.sizeDelta : new Vector2(820f, 480f);
        Vector2 panelPos = detailsRT != null ? detailsRT.anchoredPosition : Vector2.zero;

        _comparisonPanel = new GameObject("ComparisonPanel");
        _comparisonPanel.transform.SetParent(parent, false);

        var rt = _comparisonPanel.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = panelSize;
        rt.anchoredPosition = panelPos;

        var bg = _comparisonPanel.AddComponent<Image>();
        bg.color = panelBg;
        bg.raycastTarget = true;

        BuildComparisonText("CmpHeader", _comparisonPanel.transform, new Vector2(0, 200),
            new Vector2(740, 40), "Side-by-Side Comparison",
            24, FontStyle.Bold, textWhite, TextAnchor.MiddleCenter, font);

        BuildAccentLine(_comparisonPanel.transform, new Vector2(0, 168), new Vector2(620, 3), accent);
        BuildAccentLine(_comparisonPanel.transform, new Vector2(0, -10), new Vector2(2, 300), divider);

        // Column A (left)
        _comparisonTitleA = BuildComparisonText("CmpTitleA", _comparisonPanel.transform,
            new Vector2(-195, 130), new Vector2(360, 36),
            "", 22, FontStyle.Bold, tintCyan, TextAnchor.MiddleCenter, font);
        _comparisonSubA = BuildComparisonText("CmpSubA", _comparisonPanel.transform,
            new Vector2(-195, 100), new Vector2(360, 22),
            "Region A", 14, FontStyle.Italic, textDim, TextAnchor.MiddleCenter, font);
        _comparisonBodyA = BuildComparisonText("CmpBodyA", _comparisonPanel.transform,
            new Vector2(-195, -25), new Vector2(360, 240),
            "", 17, FontStyle.Italic, textWhite, TextAnchor.UpperCenter, font);
        _comparisonBodyA.lineSpacing = 1.25f;

        // Column B (right)
        _comparisonTitleB = BuildComparisonText("CmpTitleB", _comparisonPanel.transform,
            new Vector2(195, 130), new Vector2(360, 36),
            "", 22, FontStyle.Bold, tintMagenta, TextAnchor.MiddleCenter, font);
        _comparisonSubB = BuildComparisonText("CmpSubB", _comparisonPanel.transform,
            new Vector2(195, 100), new Vector2(360, 22),
            "Region B", 14, FontStyle.Italic, textDim, TextAnchor.MiddleCenter, font);
        _comparisonBodyB = BuildComparisonText("CmpBodyB", _comparisonPanel.transform,
            new Vector2(195, -25), new Vector2(360, 240),
            "", 17, FontStyle.Italic, textWhite, TextAnchor.UpperCenter, font);
        _comparisonBodyB.lineSpacing = 1.25f;

        var closeRoot = new GameObject("CmpCloseBtn");
        closeRoot.transform.SetParent(_comparisonPanel.transform, false);
        var closeRt = closeRoot.AddComponent<RectTransform>();
        closeRt.anchoredPosition = new Vector2(0, -195);
        closeRt.sizeDelta = new Vector2(260, 50);
        var closeImg = closeRoot.AddComponent<Image>();
        closeImg.color = new Color(0.70f, 0.45f, 0.10f, 1f);
        var closeBtn = closeRoot.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(HideComparison);

        var closeLabel = new GameObject("Label");
        closeLabel.transform.SetParent(closeRoot.transform, false);
        var closeLblRt = closeLabel.AddComponent<RectTransform>();
        closeLblRt.anchorMin = Vector2.zero;
        closeLblRt.anchorMax = Vector2.one;
        closeLblRt.offsetMin = Vector2.zero;
        closeLblRt.offsetMax = Vector2.zero;
        var closeLbl = closeLabel.AddComponent<Text>();
        closeLbl.font = font;
        closeLbl.text = "Close Comparison";
        closeLbl.fontSize = 18;
        closeLbl.fontStyle = FontStyle.Bold;
        closeLbl.color = textWhite;
        closeLbl.alignment = TextAnchor.MiddleCenter;

        _comparisonPanel.SetActive(false);
    }

    static Text BuildComparisonText(string name, Transform parent, Vector2 pos, Vector2 size,
        string text, int fontSize, FontStyle style, Color color, TextAnchor align, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static void BuildAccentLine(Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject("AccentLine");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.AddComponent<Image>().color = color;
    }

    // ========================= LAB TOOL STATUS =========================

    /// <summary>Update the tool status bar (called by LabToolManager).</summary>
    public void SetToolStatus(string status)
    {
        if (toolStatusText != null)
            toolStatusText.text = status;
    }

    /// <summary>Update the instruction / status message (called by LabToolManager).</summary>
    public void SetStatusMessage(string message)
    {
        if (statusMessageText != null)
            statusMessageText.text = message;
    }

    /// <summary>Show or hide the hemisphere view buttons panel.</summary>
    public void ShowHemisphereButtons(bool visible)
    {
        if (hemispherePanel != null)
            hemispherePanel.SetActive(visible);
    }

    /// <summary>Show or hide the control buttons panel (rotate, zoom, reset, opacity).</summary>
    public void ShowControlButtons(bool visible)
    {
        if (controlPanel != null)
            controlPanel.SetActive(visible);
    }
}
