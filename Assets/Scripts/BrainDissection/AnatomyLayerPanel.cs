using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space UI panel that lets the user pick a depth preset
/// after hemisphere focus. Created dynamically and positioned near
/// the existing hemisphere/control panels.
/// </summary>
public class AnatomyLayerPanel : MonoBehaviour
{
    public static AnatomyLayerPanel Instance { get; private set; }

    GameObject _panelRoot;
    GameObject _expandedRoot;
    Button[] _presetButtons;
    Text _titleText;
    Button _collapseButton;
    Text _collapseButtonLabel;
    bool _visible;
    bool _collapsed;

    static readonly Color PanelBg = new Color(0.08f, 0.08f, 0.12f, 0.92f);
    static readonly Color BtnNormal = new Color(0.18f, 0.22f, 0.35f, 1f);
    static readonly Color BtnActive = new Color(0.25f, 0.55f, 0.75f, 1f);
    static readonly Color TextWhite = new Color(0.95f, 0.95f, 0.97f);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        BuildPanel();
        Hide();
    }

    void BuildPanel()
    {
        var svc = AnatomyLayerService.Instance;
        if (svc == null) return;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        _panelRoot = new GameObject("AnatomyLayerPanel");
        _panelRoot.transform.SetParent(transform, false);

        var canvas = _panelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _panelRoot.AddComponent<CanvasScaler>();
        _panelRoot.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

        var canvasRt = _panelRoot.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(420, 400);
        canvasRt.localScale = Vector3.one * 0.00065f;

        var bg = new GameObject("BG");
        bg.transform.SetParent(_panelRoot.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;
        bg.AddComponent<Image>().color = PanelBg;

        // Title
        var titleGo = MakeText("Title", _panelRoot.transform, new Vector2(0, 165), new Vector2(380, 30),
            "Select Anatomy Layer", 18, FontStyle.Bold, TextWhite, font);
        _titleText = titleGo.GetComponent<Text>();

        var collapseGo = MakeButton("Collapse", _panelRoot.transform, new Vector2(165, 165),
            new Vector2(42, 30), "-", BtnNormal, font);
        _collapseButton = collapseGo.GetComponent<Button>();
        _collapseButton.onClick.AddListener(ToggleCollapsed);
        _collapseButtonLabel = collapseGo.transform.Find("Label").GetComponent<Text>();

        _expandedRoot = new GameObject("ExpandedRoot");
        _expandedRoot.transform.SetParent(_panelRoot.transform, false);
        var expandedRt = _expandedRoot.AddComponent<RectTransform>();
        expandedRt.anchorMin = Vector2.zero;
        expandedRt.anchorMax = Vector2.one;
        expandedRt.offsetMin = Vector2.zero;
        expandedRt.offsetMax = Vector2.zero;

        // Buttons
        string[] labels = AnatomyLayerService.PresetLabels;
        _presetButtons = new Button[labels.Length];

        float startY = 105f;
        float spacing = 50f;

        for (int i = 0; i < labels.Length; i++)
        {
            float y = startY - i * spacing;
            var btnGo = MakeButton(labels[i], _expandedRoot.transform, new Vector2(0, y),
                new Vector2(360, 44), labels[i], BtnNormal, font);
            _presetButtons[i] = btnGo.GetComponent<Button>();

            int presetIndex = i;
            _presetButtons[i].onClick.AddListener(() => OnPresetSelected(presetIndex));
        }

        // "Show All" button at bottom
        var allBtn = MakeButton("ShowAll", _expandedRoot.transform, new Vector2(0, startY - labels.Length * spacing),
            new Vector2(360, 36), "Show All Layers", new Color(0.4f, 0.4f, 0.4f), font);
        allBtn.GetComponent<Button>().onClick.AddListener(OnShowAll);

        SetCollapsed(false);
    }

    void OnPresetSelected(int index)
    {
        var svc = AnatomyLayerService.Instance;
        if (svc == null) return;

        var preset = (AnatomyDepthPreset)index;
        svc.ApplyPreset(preset);
        UpdateButtonHighlights(index);
    }

    void OnShowAll()
    {
        var svc = AnatomyLayerService.Instance;
        if (svc == null) return;

        svc.RestoreAll();
        UpdateButtonHighlights(-1);
    }

    void UpdateButtonHighlights(int activeIndex)
    {
        for (int i = 0; i < _presetButtons.Length; i++)
        {
            if (_presetButtons[i] == null) continue;
            var colors = _presetButtons[i].colors;
            colors.normalColor = (i == activeIndex) ? BtnActive : BtnNormal;
            colors.highlightedColor = (i == activeIndex) ? BtnActive : new Color(0.25f, 0.30f, 0.45f);
            _presetButtons[i].colors = colors;

            var img = _presetButtons[i].GetComponent<Image>();
            if (img != null) img.color = (i == activeIndex) ? BtnActive : BtnNormal;
        }
    }

    /// <summary>Show the layer panel, positioned near the brain.</summary>
    public void Show()
    {
        if (_panelRoot == null) return;
        _visible = true;
        _panelRoot.SetActive(true);
        PositionNearBrain();
        UpdateButtonHighlights(-1);
    }

    /// <summary>Hide the layer panel.</summary>
    public void Hide()
    {
        if (_panelRoot == null) return;
        _visible = false;
        _panelRoot.SetActive(false);
    }

    public bool IsVisible => _visible;

    void ToggleCollapsed()
    {
        SetCollapsed(!_collapsed);
    }

    void SetCollapsed(bool collapsed)
    {
        _collapsed = collapsed;
        if (_expandedRoot != null)
            _expandedRoot.SetActive(!collapsed);

        var rt = _panelRoot != null ? _panelRoot.GetComponent<RectTransform>() : null;
        if (rt != null)
            rt.sizeDelta = collapsed ? new Vector2(420, 90) : new Vector2(420, 400);

        if (_collapseButtonLabel != null)
            _collapseButtonLabel.text = collapsed ? "+" : "-";
    }

    void PositionNearBrain()
    {
        var cam = Camera.main;
        if (cam == null || _panelRoot == null) return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0;
        forward.Normalize();

        _panelRoot.transform.position = cam.transform.position
            + forward * 0.7f
            + cam.transform.right * -0.35f
            + Vector3.up * 0.15f;

        _panelRoot.transform.rotation = Quaternion.LookRotation(
            _panelRoot.transform.position - cam.transform.position);
    }

    public void RefreshFromService()
    {
        if (_panelRoot == null) return;

        var svc = AnatomyLayerService.Instance;
        if (svc == null || !svc.IsPresetActive)
        {
            UpdateButtonHighlights(-1);
            return;
        }

        UpdateButtonHighlights((int)svc.ActivePreset);
    }

    // ========================= UI HELPERS =========================

    static GameObject MakeText(string name, Transform parent, Vector2 pos, Vector2 size,
        string content, int fontSize, FontStyle style, Color color, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = go.AddComponent<Text>();
        t.text = content;
        t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return go;
    }

    static GameObject MakeButton(string name, Transform parent, Vector2 pos, Vector2 size,
        string label, Color bgColor, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.2f;
        colors.pressedColor = bgColor * 0.8f;
        btn.colors = colors;

        var txtGo = new GameObject("Label");
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;
        var t = txtGo.AddComponent<Text>();
        t.text = label;
        t.font = font;
        t.fontSize = 16;
        t.fontStyle = FontStyle.Normal;
        t.color = TextWhite;
        t.alignment = TextAnchor.MiddleCenter;

        return go;
    }
}
