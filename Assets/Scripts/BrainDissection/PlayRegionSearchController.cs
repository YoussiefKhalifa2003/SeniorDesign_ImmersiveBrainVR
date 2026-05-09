using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Play-only world-space study panel. Students must split the brain, choose a
/// study layer first, then search or pick regions within that active layer
/// without entering extraction mode.
/// </summary>
public class PlayRegionSearchController : MonoBehaviour
{
    public static PlayRegionSearchController Instance { get; private set; }

    enum StudyPanelMode
    {
        ChooseLayer,
        ChooseRegion
    }

    static readonly Color PanelBg = new Color(0.08f, 0.08f, 0.12f, 0.94f);
    static readonly Color TextWhite = new Color(0.95f, 0.95f, 0.97f, 1f);
    static readonly Color TextDim = new Color(0.72f, 0.75f, 0.82f, 1f);
    static readonly Color ButtonBg = new Color(0.18f, 0.24f, 0.36f, 1f);
    static readonly Color ButtonHover = new Color(0.24f, 0.32f, 0.48f, 1f);
    static readonly Color ButtonActive = new Color(0.24f, 0.53f, 0.76f, 1f);
    static readonly Color ButtonMuted = new Color(0.18f, 0.24f, 0.36f, 0.45f);
    static readonly Color InputBg = new Color(0.12f, 0.12f, 0.17f, 1f);
    static readonly Color InputMuted = new Color(0.08f, 0.08f, 0.12f, 0.75f);

    // Compare-mode tints. A is cyan, B is magenta — high contrast against
    // the brain's pinks/whites and against each other.
    static readonly Color CompareTintA = new Color(0.18f, 0.82f, 1.00f, 1f);
    static readonly Color CompareTintB = new Color(1.00f, 0.42f, 0.86f, 1f);
    static readonly Color CompareSlotEmpty = new Color(0.16f, 0.20f, 0.30f, 1f);

    public BrainManager brainManager;

    RegionUIController _regionUIController;
    GameObject _panelRoot;
    RectTransform _panelRect;
    GameObject _expandedRoot;
    GameObject _layerSectionRoot;
    GameObject _regionSectionRoot;
    InputField _searchInput;
    Image _searchInputImage;
    RectTransform _listContent;
    Text _titleText;
    Text _subtitleText;
    Text _emptyStateText;
    Text _collapsedHintText;
    Text _layerIntroText;
    Button _collapseButton;
    Text _collapseButtonLabel;
    Button _backToLayersButton;
    Button[] _layerButtons;
    Font _font;
    bool _isVisible;
    bool _isCollapsed;
    bool _lastCanStudy;
    bool _studyStateInitialized;
    StudyPanelMode _mode = StudyPanelMode.ChooseLayer;
    int _selectedPresetIndex = -1;

    readonly List<BrainRegion> _allRegions = new List<BrainRegion>();
    readonly List<BrainRegion> _filteredRegions = new List<BrainRegion>();
    readonly List<Button> _regionButtons = new List<Button>();

    Coroutine _spotlightRoutine;
    BrainRegion _activeSpotlightRegion;
    float _restoreOpacity = 1f;
    bool _opacityWasForced;

    // Compare mode (Play only). When enabled, list clicks fill slot A then B
    // and apply persistent study tints instead of running the single-region
    // spotlight. Cross-layer lets students contrast e.g. cortex vs adjacent
    // white matter without changing the active anatomy preset.
    bool _compareEnabled;
    bool _compareCrossLayer;
    int _comparePickingSlot = -1;
    BrainRegion _compareA;
    BrainRegion _compareB;
    bool _compareForcedRestoreAll;

    GameObject _compareBarRoot;
    Button _compareToggleButton;
    Text _compareToggleLabel;
    Button _compareSlotAButton;
    Image _compareSlotAImage;
    Text _compareSlotALabel;
    Button _compareSlotBButton;
    Image _compareSlotBImage;
    Text _compareSlotBLabel;
    Button _compareClearButton;
    Button _compareCrossLayerButton;
    Image _compareCrossLayerImage;
    Text _compareCrossLayerLabel;
    Button _compareViewButton;
    Image _compareViewImage;
    Text _compareViewLabel;

    void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (brainManager == null)
            brainManager = FindFirstObjectByType<BrainManager>();

        _regionUIController = brainManager != null
            ? brainManager.regionUIController
            : FindFirstObjectByType<RegionUIController>();

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null)
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        BuildPanel();
        RefreshRegionCache();
        UpdateModeUI();
        SyncVisibility(forceUpdate: true);
    }

    void Update()
    {
        SyncVisibility();

        if (!_isVisible)
            return;

        if (AnatomyLayerPanel.Instance != null && AnatomyLayerPanel.Instance.IsVisible)
            AnatomyLayerPanel.Instance.Hide();

        EnsurePresetStateMatchesScene();
        UpdateStudyState();
        PositionNearBrain();
    }

    void OnDisable()
    {
        StopSpotlight();
        if (_compareEnabled)
            SetCompareEnabled(false);
    }

    void OnDestroy()
    {
        if (_compareEnabled)
            SetCompareEnabled(false);
        if (Instance == this)
            Instance = null;
    }

    void BuildPanel()
    {
        _panelRoot = new GameObject("PlayRegionSearchPanel");
        _panelRoot.transform.SetParent(transform, false);

        var canvas = _panelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _panelRoot.AddComponent<CanvasScaler>();
        _panelRoot.AddComponent<TrackedDeviceGraphicRaycaster>();

        _panelRect = _panelRoot.GetComponent<RectTransform>();
        _panelRect.sizeDelta = new Vector2(430f, 540f);
        _panelRect.localScale = Vector3.one * 0.0006f;

        var bg = CreateImage("Background", _panelRoot.transform, Vector2.zero, new Vector2(430f, 540f), PanelBg);
        StretchToParent(bg.GetComponent<RectTransform>());

        _titleText = CreateText("Title", _panelRoot.transform, new Vector2(-10f, 230f), new Vector2(300f, 34f),
            "Study Layers", 20, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter);
        _subtitleText = CreateText("Subtitle", _panelRoot.transform, new Vector2(-10f, 198f), new Vector2(340f, 24f),
            "Choose a layer to reveal its regions", 14, FontStyle.Normal, TextDim, TextAnchor.MiddleCenter);

        CreateCollapseButton();

        _expandedRoot = new GameObject("ExpandedRoot");
        _expandedRoot.transform.SetParent(_panelRoot.transform, false);
        var expandedRt = _expandedRoot.AddComponent<RectTransform>();
        StretchToParent(expandedRt);

        CreateLayerSection();
        CreateRegionSection();

        _collapsedHintText = CreateText("CollapsedHint", _panelRoot.transform, new Vector2(0f, -6f), new Vector2(300f, 24f),
            "Expand to choose a layer", 14, FontStyle.Italic, TextDim, TextAnchor.MiddleCenter);
        _collapsedHintText.gameObject.SetActive(false);

        SetCollapsed(false);
        _panelRoot.SetActive(false);
    }

    void CreateCollapseButton()
    {
        var collapseGo = CreateImage("CollapseButton", _panelRoot.transform, new Vector2(170f, 230f), new Vector2(48f, 34f), ButtonBg);
        _collapseButton = collapseGo.AddComponent<Button>();
        var colors = _collapseButton.colors;
        colors.normalColor = ButtonBg;
        colors.highlightedColor = ButtonHover;
        colors.pressedColor = ButtonBg * 0.8f;
        _collapseButton.colors = colors;
        _collapseButton.onClick.AddListener(ToggleCollapsed);

        _collapseButtonLabel = CreateText("Label", collapseGo.transform, Vector2.zero, new Vector2(40f, 24f),
            "-", 18, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter);
    }

    void CreateLayerSection()
    {
        _layerSectionRoot = new GameObject("LayerSection");
        _layerSectionRoot.transform.SetParent(_expandedRoot.transform, false);
        var rt = _layerSectionRoot.AddComponent<RectTransform>();
        StretchToParent(rt);

        _layerIntroText = CreateText("LayerIntro", _layerSectionRoot.transform, new Vector2(0f, 155f), new Vector2(350f, 42f),
            "Pick a layer. Play mode will only show the regions inside that layer.", 15, FontStyle.Normal, TextDim, TextAnchor.MiddleCenter);

        string[] labels = AnatomyLayerService.PresetLabels;
        _layerButtons = new Button[labels.Length];

        float startY = 90f;
        float spacing = 55f;
        for (int i = 0; i < labels.Length; i++)
        {
            var buttonRoot = CreateButton("Layer_" + i, _layerSectionRoot.transform, new Vector2(0f, startY - i * spacing),
                new Vector2(360f, 44f), labels[i], ButtonBg);
            int presetIndex = i;
            _layerButtons[i] = buttonRoot.GetComponent<Button>();
            _layerButtons[i].onClick.AddListener(() => OnLayerSelected(presetIndex));
        }
    }

    void CreateRegionSection()
    {
        _regionSectionRoot = new GameObject("RegionSection");
        _regionSectionRoot.transform.SetParent(_expandedRoot.transform, false);
        var rt = _regionSectionRoot.AddComponent<RectTransform>();
        StretchToParent(rt);

        var backRoot = CreateButton("BackToLayers", _regionSectionRoot.transform, new Vector2(0f, 155f),
            new Vector2(240f, 38f), "Choose Different Layer", new Color(0.38f, 0.38f, 0.44f, 1f));
        _backToLayersButton = backRoot.GetComponent<Button>();
        _backToLayersButton.onClick.AddListener(() => ClearActivePreset(true));

        CreateInputField();
        CreateScrollList();
        CreateCompareBar();
    }

    void CreateCompareBar()
    {
        _compareBarRoot = CreateImage("CompareBar", _regionSectionRoot.transform,
            new Vector2(0f, -215f), new Vector2(390f, 90f),
            new Color(0.10f, 0.12f, 0.18f, 0.96f));

        // Row 1 (y=30): Compare toggle + View Side-by-Side
        var toggleRoot = CreateButton("CompareToggle", _compareBarRoot.transform,
            new Vector2(-95f, 30f), new Vector2(180f, 26f), "Compare: OFF", ButtonBg);
        _compareToggleButton = toggleRoot.GetComponent<Button>();
        _compareToggleLabel = toggleRoot.transform.Find("Label").GetComponent<Text>();
        _compareToggleLabel.fontSize = 12;
        _compareToggleButton.onClick.AddListener(OnCompareTogglePressed);

        var viewRoot = CreateButton("CompareView", _compareBarRoot.transform,
            new Vector2(95f, 30f), new Vector2(180f, 26f),
            "View Side-by-Side", new Color(0.20f, 0.40f, 0.65f, 1f));
        _compareViewButton = viewRoot.GetComponent<Button>();
        _compareViewImage = viewRoot.GetComponent<Image>();
        _compareViewLabel = viewRoot.transform.Find("Label").GetComponent<Text>();
        _compareViewLabel.fontSize = 12;
        _compareViewButton.onClick.AddListener(OnCompareViewPressed);

        // Row 2 (y=0): Slot A + Slot B
        var slotARoot = CreateButton("CompareSlotA", _compareBarRoot.transform,
            new Vector2(-95f, 0f), new Vector2(180f, 28f), "A: pick a region", CompareSlotEmpty);
        _compareSlotAButton = slotARoot.GetComponent<Button>();
        _compareSlotAImage = slotARoot.GetComponent<Image>();
        _compareSlotALabel = slotARoot.transform.Find("Label").GetComponent<Text>();
        _compareSlotALabel.fontSize = 12;
        _compareSlotAButton.onClick.AddListener(() => OnCompareSlotPressed(0));

        var slotBRoot = CreateButton("CompareSlotB", _compareBarRoot.transform,
            new Vector2(95f, 0f), new Vector2(180f, 28f), "B: pick a region", CompareSlotEmpty);
        _compareSlotBButton = slotBRoot.GetComponent<Button>();
        _compareSlotBImage = slotBRoot.GetComponent<Image>();
        _compareSlotBLabel = slotBRoot.transform.Find("Label").GetComponent<Text>();
        _compareSlotBLabel.fontSize = 12;
        _compareSlotBButton.onClick.AddListener(() => OnCompareSlotPressed(1));

        // Row 3 (y=-32): Clear + Same/Any Layer toggle
        var clearRoot = CreateButton("CompareClear", _compareBarRoot.transform,
            new Vector2(-95f, -32f), new Vector2(180f, 26f), "Clear", new Color(0.40f, 0.20f, 0.20f, 1f));
        _compareClearButton = clearRoot.GetComponent<Button>();
        var clearLabel = clearRoot.transform.Find("Label").GetComponent<Text>();
        clearLabel.fontSize = 12;
        _compareClearButton.onClick.AddListener(OnCompareClearPressed);

        var crossLayerRoot = CreateButton("CompareCrossLayer", _compareBarRoot.transform,
            new Vector2(95f, -32f), new Vector2(180f, 26f), "Same Layer", ButtonBg);
        _compareCrossLayerButton = crossLayerRoot.GetComponent<Button>();
        _compareCrossLayerImage = crossLayerRoot.GetComponent<Image>();
        _compareCrossLayerLabel = crossLayerRoot.transform.Find("Label").GetComponent<Text>();
        _compareCrossLayerLabel.fontSize = 12;
        _compareCrossLayerButton.onClick.AddListener(OnCompareCrossLayerPressed);

        ApplyCompareUI();
    }

    void CreateInputField()
    {
        var inputRoot = CreateImage("SearchInput", _regionSectionRoot.transform, new Vector2(0f, 108f), new Vector2(360f, 42f), InputBg);
        var inputRt = inputRoot.GetComponent<RectTransform>();
        _searchInputImage = inputRoot.GetComponent<Image>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(inputRoot.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(12f, 6f);
        textRt.offsetMax = new Vector2(-12f, -6f);

        var text = textGo.AddComponent<Text>();
        text.font = _font;
        text.fontSize = 16;
        text.color = TextWhite;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;

        var placeholderGo = new GameObject("Placeholder");
        placeholderGo.transform.SetParent(inputRoot.transform, false);
        var placeholderRt = placeholderGo.AddComponent<RectTransform>();
        placeholderRt.anchorMin = Vector2.zero;
        placeholderRt.anchorMax = Vector2.one;
        placeholderRt.offsetMin = new Vector2(12f, 6f);
        placeholderRt.offsetMax = new Vector2(-12f, -6f);

        var placeholder = placeholderGo.AddComponent<Text>();
        placeholder.font = _font;
        placeholder.fontSize = 16;
        placeholder.color = new Color(TextDim.r, TextDim.g, TextDim.b, 0.75f);
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.text = "Search within the selected layer...";

        _searchInput = inputRoot.AddComponent<InputField>();
        _searchInput.textComponent = text;
        _searchInput.placeholder = placeholder;
        _searchInput.lineType = InputField.LineType.SingleLine;
        _searchInput.onValueChanged.AddListener(OnSearchChanged);

        inputRt.SetAsLastSibling();
    }

    void CreateScrollList()
    {
        var viewport = CreateImage("Viewport", _regionSectionRoot.transform, new Vector2(0f, -25f), new Vector2(370f, 240f),
            new Color(0.10f, 0.10f, 0.15f, 0.95f));
        var viewportRt = viewport.GetComponent<RectTransform>();
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        var scrollRect = viewport.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 20f;
        scrollRect.viewport = viewportRt;

        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(viewport.transform, false);
        _listContent = contentGo.AddComponent<RectTransform>();
        _listContent.anchorMin = new Vector2(0f, 1f);
        _listContent.anchorMax = new Vector2(1f, 1f);
        _listContent.pivot = new Vector2(0.5f, 1f);
        _listContent.anchoredPosition = Vector2.zero;
        _listContent.sizeDelta = new Vector2(0f, 0f);

        var layout = contentGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.content = _listContent;

        _emptyStateText = CreateText("EmptyState", viewport.transform, Vector2.zero, new Vector2(320f, 48f),
            "Choose a layer first.", 16, FontStyle.Italic, TextDim, TextAnchor.MiddleCenter);
        var emptyRt = _emptyStateText.GetComponent<RectTransform>();
        emptyRt.anchorMin = new Vector2(0.5f, 0.5f);
        emptyRt.anchorMax = new Vector2(0.5f, 0.5f);
        emptyRt.anchoredPosition = Vector2.zero;
    }

    void SyncVisibility(bool forceUpdate = false)
    {
        bool shouldShow = CanShowPanel();
        if (!forceUpdate && shouldShow == _isVisible)
            return;

        _isVisible = shouldShow;
        if (_panelRoot != null)
            _panelRoot.SetActive(shouldShow);

        if (!shouldShow)
        {
            StopSpotlight();
            if (_compareEnabled)
                SetCompareEnabled(false);
            if (_searchInput != null && !string.IsNullOrEmpty(_searchInput.text))
                _searchInput.text = string.Empty;
            return;
        }

        if (AnatomyLayerPanel.Instance != null)
            AnatomyLayerPanel.Instance.Hide();

        RefreshRegionCache();
        EnsurePresetStateMatchesScene();
        ApplyFilter(_searchInput != null ? _searchInput.text : string.Empty);
        UpdateStudyState();
        UpdateModeUI();
    }

    bool CanShowPanel()
    {
        if (!SessionData.IsPlayMode || SessionData.IsTutorialMode || SessionData.IsAssessmentMode)
            return false;

        if (brainManager == null)
            return false;

        if (brainManager.IsInspectingRegion)
            return false;

        var tutorial = TutorialManager.Instance;
        if (tutorial != null && tutorial.IsTutorialActive)
            return false;

        var liveDissection = LiveDissectionManager.Instance;
        if (liveDissection != null && liveDissection.IsLiveDissectionActive)
            return false;

        return true;
    }

    void RefreshRegionCache()
    {
        _allRegions.Clear();

        var regions = FindObjectsByType<BrainRegion>(FindObjectsSortMode.None);
        for (int i = 0; i < regions.Length; i++)
        {
            var region = regions[i];
            if (region == null || region.regionData == null)
                continue;

            _allRegions.Add(region);
        }

        _allRegions.Sort((a, b) =>
        {
            string left = a.regionData != null ? a.regionData.displayName : string.Empty;
            string right = b.regionData != null ? b.regionData.displayName : string.Empty;
            return string.Compare(left, right, System.StringComparison.OrdinalIgnoreCase);
        });
    }

    void OnLayerSelected(int presetIndex)
    {
        if (!CanStudyRegions())
        {
            if (_regionUIController != null)
                _regionUIController.SetStatusMessage("Split the brain first, then choose a layer to study.");
            return;
        }

        var service = AnatomyLayerService.Instance;
        if (service == null)
            return;

        StopSpotlight();
        // Switching layers invalidates the compare slots; clear them so
        // students aren't comparing a now-hidden region.
        if (_compareEnabled)
            ClearCompareSelection();
        _compareForcedRestoreAll = false;

        _selectedPresetIndex = presetIndex;
        _mode = StudyPanelMode.ChooseRegion;
        service.ApplyPreset((AnatomyDepthPreset)presetIndex);

        if (_compareEnabled && _compareCrossLayer)
        {
            _compareForcedRestoreAll = true;
            service.RestoreAll();
        }

        if (_searchInput != null)
            _searchInput.text = string.Empty;

        if (_regionUIController != null && !brainManager.IsInspectingRegion)
            _regionUIController.HideRegionDetails();

        ApplyFilter(string.Empty);
        UpdateModeUI();
        UpdateStudyState();
    }

    void ClearActivePreset(bool restoreAll)
    {
        StopSpotlight();

        // Layer is going away — compare slots referenced regions inside that
        // layer, so clear them rather than leaving stale tints.
        if (_compareEnabled)
            SetCompareEnabled(false);

        _selectedPresetIndex = -1;
        _mode = StudyPanelMode.ChooseLayer;

        if (_searchInput != null && !string.IsNullOrEmpty(_searchInput.text))
            _searchInput.text = string.Empty;

        if (restoreAll && AnatomyLayerService.Instance != null)
            AnatomyLayerService.Instance.RestoreAll();

        if (_regionUIController != null && !brainManager.IsInspectingRegion)
            _regionUIController.HideRegionDetails();

        ApplyFilter(string.Empty);
        UpdateModeUI();
        UpdateStudyState();
    }

    public bool ShouldOwnLayerUI => SessionData.IsPlayMode && !SessionData.IsTutorialMode && !SessionData.IsAssessmentMode;

    public void ClearStudySelection()
    {
        ClearActivePreset(true);
    }

    public void OnBrainViewStateReset()
    {
        if (_mode != StudyPanelMode.ChooseRegion || _selectedPresetIndex < 0)
            return;

        var service = AnatomyLayerService.Instance;
        if (service == null)
            return;

        service.ApplyPreset((AnatomyDepthPreset)_selectedPresetIndex);
        ApplyFilter(_searchInput != null ? _searchInput.text : string.Empty);
        UpdateModeUI();
    }

    void OnSearchChanged(string value)
    {
        ApplyFilter(value);
    }

    void ApplyFilter(string query)
    {
        if (_listContent == null)
            return;

        _filteredRegions.Clear();

        if (_mode != StudyPanelMode.ChooseRegion || _selectedPresetIndex < 0)
        {
            RebuildButtons();
            return;
        }

        string normalizedQuery = Normalize(query);
        for (int i = 0; i < _allRegions.Count; i++)
        {
            var region = _allRegions[i];
            if (RegionMatches(region, normalizedQuery))
                _filteredRegions.Add(region);
        }

        RebuildButtons();
    }

    bool RegionMatches(BrainRegion region, string normalizedQuery)
    {
        if (region == null || region.regionData == null)
            return false;

        var service = AnatomyLayerService.Instance;
        // Compare's "Any Layer" mode lets students contrast a cortex region
        // with adjacent white matter, etc. We still respect the active
        // hemisphere scope (left/right focus) by deferring to RestoreAll
        // visibility — see SetCompareCrossLayer.
        bool requireLayerMembership = !(_compareEnabled && _compareCrossLayer);
        if (requireLayerMembership)
        {
            if (service == null || !service.IsPresetActive || !service.IsRegionInActivePreset(region))
                return false;
        }
        else if (brainManager != null)
        {
            if (brainManager.IsLeftHemisphereFocused && !brainManager.IsRegionInLeftHemisphere(region))
                return false;
            if (brainManager.IsRightHemisphereFocused && !brainManager.IsRegionInRightHemisphere(region))
                return false;
        }

        if (string.IsNullOrEmpty(normalizedQuery))
            return true;

        string displayName = Normalize(region.regionData.displayName);
        string regionId = Normalize(region.regionData.regionId);
        return displayName.Contains(normalizedQuery) || regionId.Contains(normalizedQuery);
    }

    static string Normalize(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    void RebuildButtons()
    {
        _regionButtons.Clear();

        for (int i = _listContent.childCount - 1; i >= 0; i--)
            Destroy(_listContent.GetChild(i).gameObject);

        for (int i = 0; i < _filteredRegions.Count; i++)
        {
            var region = _filteredRegions[i];
            var button = CreateRegionButton(region);
            button.transform.SetParent(_listContent, false);
        }

        if (_emptyStateText != null)
            _emptyStateText.gameObject.SetActive(_filteredRegions.Count == 0);

        ApplyStudyInteractableState();
    }

    GameObject CreateRegionButton(BrainRegion region)
    {
        string label = region.regionData != null ? region.regionData.displayName : "Unknown Region";
        var buttonRoot = CreateButton("RegionButton_" + label, null, Vector2.zero, new Vector2(340f, 42f), label, ButtonBg);
        buttonRoot.AddComponent<LayoutElement>().preferredHeight = 42f;

        var button = buttonRoot.GetComponent<Button>();
        button.onClick.AddListener(() => OnRegionPicked(region));
        _regionButtons.Add(button);
        return buttonRoot;
    }

    void OnRegionPicked(BrainRegion region)
    {
        if (region == null || region.regionData == null || brainManager == null)
            return;

        if (!CanStudyRegions())
        {
            if (_regionUIController != null)
                _regionUIController.SetStatusMessage("Split the brain first, then choose a layer to study.");
            return;
        }

        if (_mode != StudyPanelMode.ChooseRegion || _selectedPresetIndex < 0)
        {
            if (_regionUIController != null)
                _regionUIController.SetStatusMessage("Choose a layer before selecting a region.");
            return;
        }

        if (brainManager.IsInspectingRegion)
        {
            if (_regionUIController != null)
                _regionUIController.SetStatusMessage("Finish putting back the current region before studying another one.");
            return;
        }

        // Compare mode steals the click: assign to the active slot, tint, and
        // skip the single-region spotlight so both picks stay readable.
        if (_compareEnabled)
        {
            AssignCompareRegion(region);
            return;
        }

        if (_regionUIController != null)
        {
            _regionUIController.SetStatusMessage("Study focus: " + region.regionData.displayName);
            _regionUIController.HideRegionDetails();
        }

        SetCollapsed(true);
        StopSpotlight();
        _spotlightRoutine = StartCoroutine(SpotlightRegion(region));
    }

    // ========================= COMPARE MODE =========================

    void OnCompareTogglePressed()
    {
        SetCompareEnabled(!_compareEnabled);
    }

    void OnCompareSlotPressed(int slot)
    {
        if (!_compareEnabled)
            SetCompareEnabled(true);

        _comparePickingSlot = slot;
        if (_regionUIController != null)
        {
            string slotLabel = slot == 0 ? "A" : "B";
            _regionUIController.SetStatusMessage($"Compare: tap a region to set slot {slotLabel}.");
        }
        ApplyCompareUI();
    }

    void OnCompareClearPressed()
    {
        ClearCompareSelection();
        ApplyCompareUI();
        if (_regionUIController != null)
            _regionUIController.SetStatusMessage(_compareEnabled
                ? "Compare cleared. Pick A and B again."
                : "Compare cleared.");
    }

    void OnCompareCrossLayerPressed()
    {
        SetCompareCrossLayer(!_compareCrossLayer);
    }

    void OnCompareViewPressed()
    {
        if (_regionUIController == null)
            return;

        if (!_compareEnabled || _compareA == null || _compareB == null ||
            _compareA.regionData == null || _compareB.regionData == null)
        {
            _regionUIController.SetStatusMessage("Pick both A and B first to open the side-by-side panel.");
            return;
        }

        _regionUIController.ShowComparison(_compareA.regionData, _compareB.regionData);
        _regionUIController.SetStatusMessage(
            $"Side-by-side:  A: {_compareA.regionData.displayName}   vs   B: {_compareB.regionData.displayName}");
    }

    void SetCompareEnabled(bool enabled)
    {
        if (_compareEnabled == enabled)
            return;

        _compareEnabled = enabled;
        _comparePickingSlot = -1;

        if (!enabled)
        {
            ClearCompareSelection();
            SetCompareCrossLayer(false);
        }
        else
        {
            // First pick goes to slot A by default for a clear flow.
            _comparePickingSlot = 0;
            StopSpotlight();
        }

        ApplyCompareUI();
        ApplyFilter(_searchInput != null ? _searchInput.text : string.Empty);

        if (_regionUIController != null)
            _regionUIController.SetStatusMessage(enabled
                ? "Compare mode on. Pick a region for slot A."
                : "Compare mode off.");
    }

    void SetCompareCrossLayer(bool on)
    {
        if (_compareCrossLayer == on)
            return;

        _compareCrossLayer = on;

        var service = AnatomyLayerService.Instance;
        if (service != null)
        {
            if (on)
            {
                // Make every region visible so cross-layer picks render.
                _compareForcedRestoreAll = true;
                service.RestoreAll();
            }
            else if (_compareForcedRestoreAll)
            {
                _compareForcedRestoreAll = false;
                if (_selectedPresetIndex >= 0)
                    service.ApplyPreset((AnatomyDepthPreset)_selectedPresetIndex);
            }
        }

        ApplyCompareUI();
        ApplyFilter(_searchInput != null ? _searchInput.text : string.Empty);
    }

    void AssignCompareRegion(BrainRegion region)
    {
        // Decide which slot to fill: explicit pick > first empty slot > replace A.
        int slot = _comparePickingSlot;
        if (slot < 0)
        {
            if (_compareA == null) slot = 0;
            else if (_compareB == null) slot = 1;
            else slot = 0;
        }

        if (slot == 0)
        {
            ClearTint(_compareA);
            _compareA = region;
            region.SetStudyTint(true, CompareTintA);
        }
        else
        {
            ClearTint(_compareB);
            _compareB = region;
            region.SetStudyTint(true, CompareTintB);
        }

        // Auto-advance: if the other slot is empty, prime it for the next click.
        if (_compareA != null && _compareB == null) _comparePickingSlot = 1;
        else if (_compareB != null && _compareA == null) _comparePickingSlot = 0;
        else _comparePickingSlot = -1;

        bool bothFilled = _compareA != null && _compareB != null;

        if (_regionUIController != null)
        {
            string a = _compareA != null && _compareA.regionData != null ? _compareA.regionData.displayName : "—";
            string b = _compareB != null && _compareB.regionData != null ? _compareB.regionData.displayName : "—";
            _regionUIController.SetStatusMessage($"Comparing  A: {a}   vs   B: {b}");

            // First time both slots are filled → open the side-by-side panel
            // automatically. If the user then closes it, they can reopen via
            // the "View Side-by-Side" button without re-picking the slots.
            if (bothFilled)
            {
                _regionUIController.ShowComparison(_compareA.regionData, _compareB.regionData);
            }
            else
            {
                _regionUIController.HideRegionDetails();
            }
        }

        ApplyCompareUI();
    }

    void ClearCompareSelection()
    {
        ClearTint(_compareA);
        ClearTint(_compareB);
        _compareA = null;
        _compareB = null;
        _comparePickingSlot = _compareEnabled ? 0 : -1;

        if (_regionUIController != null && _regionUIController.IsComparisonShown)
            _regionUIController.HideComparison();
    }

    static void ClearTint(BrainRegion region)
    {
        if (region != null) region.SetStudyTint(false);
    }

    void ApplyCompareUI()
    {
        if (_compareBarRoot == null) return;

        if (_compareToggleLabel != null)
            _compareToggleLabel.text = _compareEnabled ? "Compare: ON" : "Compare: OFF";

        if (_compareToggleButton != null)
        {
            var img = _compareToggleButton.GetComponent<Image>();
            if (img != null) img.color = _compareEnabled ? ButtonActive : ButtonBg;
        }

        UpdateSlotVisual(_compareSlotAImage, _compareSlotALabel, _compareA, "A", CompareTintA, 0);
        UpdateSlotVisual(_compareSlotBImage, _compareSlotBLabel, _compareB, "B", CompareTintB, 1);

        if (_compareClearButton != null)
        {
            bool anyFilled = _compareA != null || _compareB != null;
            _compareClearButton.interactable = _compareEnabled && anyFilled;
        }

        if (_compareCrossLayerLabel != null)
            _compareCrossLayerLabel.text = _compareCrossLayer ? "Any Layer" : "Same Layer";
        if (_compareCrossLayerImage != null)
            _compareCrossLayerImage.color = _compareCrossLayer ? ButtonActive : ButtonBg;
        if (_compareCrossLayerButton != null)
            _compareCrossLayerButton.interactable = _compareEnabled;

        // View Side-by-Side: enabled only when compare is on and both slots
        // are filled. Mute the color when disabled so it reads as inert.
        bool canView = _compareEnabled && _compareA != null && _compareB != null;
        if (_compareViewButton != null)
            _compareViewButton.interactable = canView;
        if (_compareViewImage != null)
            _compareViewImage.color = canView
                ? new Color(0.20f, 0.55f, 0.85f, 1f)
                : new Color(0.20f, 0.40f, 0.65f, 0.55f);

        if (_compareSlotAButton != null) _compareSlotAButton.interactable = _compareEnabled;
        if (_compareSlotBButton != null) _compareSlotBButton.interactable = _compareEnabled;
    }

    void UpdateSlotVisual(Image img, Text label, BrainRegion region, string slotLetter,
        Color tint, int slotIndex)
    {
        if (label != null)
        {
            string name = region != null && region.regionData != null
                ? Shorten(region.regionData.displayName, 22)
                : "pick a region";
            string prefix = _comparePickingSlot == slotIndex ? "\u25cf " : "";
            label.text = $"{prefix}{slotLetter}: {name}";
        }
        if (img != null)
        {
            if (region != null)
                img.color = new Color(tint.r * 0.55f, tint.g * 0.55f, tint.b * 0.55f, 1f);
            else
                img.color = _comparePickingSlot == slotIndex ? ButtonActive : CompareSlotEmpty;
        }
    }

    static string Shorten(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
        return s.Substring(0, max - 1).TrimEnd() + "\u2026";
    }

    IEnumerator SpotlightRegion(BrainRegion region)
    {
        _activeSpotlightRegion = region;

        _restoreOpacity = 1f;
        if (_regionUIController != null && _regionUIController.opacitySlider != null)
            _restoreOpacity = _regionUIController.opacitySlider.value;

        brainManager.SetBrainOpacity(0.16f, forceForStudy: true);
        _opacityWasForced = true;

        for (int i = 0; i < 2; i++)
        {
            if (region == null)
                yield break;

            region.SetHighlight(true);
            yield return new WaitForSeconds(0.18f);
            region.SetHighlight(false);
            yield return new WaitForSeconds(0.10f);
        }

        if (region != null)
            region.SetHighlight(true);

        yield return new WaitForSeconds(0.55f);

        if (region != null)
            region.SetHighlight(false);

        RestoreOpacity();
        _spotlightRoutine = null;
        _activeSpotlightRegion = null;
    }

    void StopSpotlight()
    {
        if (_spotlightRoutine != null)
        {
            StopCoroutine(_spotlightRoutine);
            _spotlightRoutine = null;
        }

        if (_activeSpotlightRegion != null)
            _activeSpotlightRegion.SetHighlight(false);

        _activeSpotlightRegion = null;
        RestoreOpacity();
    }

    void RestoreOpacity()
    {
        if (!_opacityWasForced || brainManager == null)
            return;

        brainManager.SetBrainOpacity(_restoreOpacity, forceForStudy: true);
        _opacityWasForced = false;
    }

    void PositionNearBrain()
    {
        if (_panelRoot == null)
            return;

        var cam = Camera.main;
        if (cam == null)
            return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        bool detailsVisible = _regionUIController != null &&
            _regionUIController.detailsPanel != null &&
            _regionUIController.detailsPanel.activeSelf;

        float sideOffset = detailsVisible ? 0.22f : 0.34f;
        float verticalOffset = _isCollapsed ? 0.19f : 0.14f;

        Vector3 position = cam.transform.position
            + forward * 0.72f
            + cam.transform.right * sideOffset
            + Vector3.up * verticalOffset;

        _panelRoot.transform.position = position;
        _panelRoot.transform.rotation = Quaternion.LookRotation(position - cam.transform.position);
    }

    bool CanStudyRegions()
    {
        return LabToolManager.Instance != null && LabToolManager.Instance.brainIsSplit;
    }

    void EnsurePresetStateMatchesScene()
    {
        var service = AnatomyLayerService.Instance;

        if (!CanStudyRegions())
        {
            if (_selectedPresetIndex >= 0 || _mode != StudyPanelMode.ChooseLayer)
                ClearActivePreset(true);
            return;
        }

        if (_selectedPresetIndex >= 0 && (service == null || !service.IsPresetActive))
            ClearActivePreset(false);
    }

    void UpdateStudyState()
    {
        bool canStudy = CanStudyRegions();
        if (_studyStateInitialized && canStudy == _lastCanStudy)
            return;

        _studyStateInitialized = true;
        _lastCanStudy = canStudy;
        ApplyStudyInteractableState();
    }

    void ApplyStudyInteractableState()
    {
        bool canStudy = CanStudyRegions();
        bool canSearchLayer = canStudy && _mode == StudyPanelMode.ChooseRegion && _selectedPresetIndex >= 0;

        if (_searchInput != null)
            _searchInput.interactable = canSearchLayer;

        if (_searchInputImage != null)
            _searchInputImage.color = canSearchLayer ? InputBg : InputMuted;

        if (_backToLayersButton != null)
            _backToLayersButton.interactable = canSearchLayer;

        for (int i = 0; i < _regionButtons.Count; i++)
        {
            if (_regionButtons[i] == null) continue;
            _regionButtons[i].interactable = canSearchLayer;

            var img = _regionButtons[i].GetComponent<Image>();
            if (img != null)
                img.color = canSearchLayer ? ButtonBg : ButtonMuted;
        }

        if (_layerButtons != null)
        {
            for (int i = 0; i < _layerButtons.Length; i++)
            {
                if (_layerButtons[i] == null) continue;

                _layerButtons[i].interactable = canStudy;
                var img = _layerButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    bool selected = i == _selectedPresetIndex && _mode == StudyPanelMode.ChooseRegion;
                    img.color = !canStudy ? ButtonMuted : (selected ? ButtonActive : ButtonBg);
                }
            }
        }

        if (_layerIntroText != null)
        {
            _layerIntroText.text = canStudy
                ? "Pick a layer. Play mode will only show the regions inside that layer."
                : "Split the brain first to unlock layer study mode.";
        }

        if (_subtitleText != null)
        {
            if (!canStudy)
                _subtitleText.text = "Split the brain first to unlock study mode";
            else if (_mode == StudyPanelMode.ChooseLayer)
                _subtitleText.text = "Choose a layer to reveal its regions";
            else
                _subtitleText.text = "Search or pick a region inside the active layer";
        }

        if (_titleText != null)
            _titleText.text = _mode == StudyPanelMode.ChooseLayer ? "Study Layers" : "Study Regions";

        if (_emptyStateText != null)
        {
            if (!canStudy)
                _emptyStateText.text = "Split the brain to enable study mode.";
            else if (_mode == StudyPanelMode.ChooseLayer)
                _emptyStateText.text = "Choose a layer first.";
            else
                _emptyStateText.text = "No matching regions in this layer.";
        }

        if (_collapsedHintText != null)
        {
            _collapsedHintText.text = _mode == StudyPanelMode.ChooseLayer
                ? "Expand to choose a layer"
                : "Expand to search within the active layer";
        }
    }

    void UpdateModeUI()
    {
        if (_layerSectionRoot != null)
            _layerSectionRoot.SetActive(_mode == StudyPanelMode.ChooseLayer);

        if (_regionSectionRoot != null)
            _regionSectionRoot.SetActive(_mode == StudyPanelMode.ChooseRegion);

        // The compare bar lives inside the region section, so it follows the
        // section's active state, but we still want it hidden if the user
        // collapses the panel.
        if (_compareBarRoot != null && _mode != StudyPanelMode.ChooseRegion)
        {
            // Defensive: tints are already cleared via SetCompareEnabled(false)
            // calls in flow transitions; this just keeps UI in sync.
            ApplyCompareUI();
        }

        ApplyStudyInteractableState();
    }

    void ToggleCollapsed()
    {
        SetCollapsed(!_isCollapsed);
    }

    void SetCollapsed(bool collapsed)
    {
        _isCollapsed = collapsed;

        if (_expandedRoot != null)
            _expandedRoot.SetActive(!collapsed);

        if (_collapsedHintText != null)
            _collapsedHintText.gameObject.SetActive(collapsed);

        if (_panelRect != null)
            _panelRect.sizeDelta = collapsed ? new Vector2(320f, 92f) : new Vector2(430f, 540f);

        if (_collapseButtonLabel != null)
            _collapseButtonLabel.text = collapsed ? "+" : "-";
    }

    GameObject CreateImage(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        if (parent != null)
            go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;

        var image = go.AddComponent<Image>();
        image.color = color;
        return go;
    }

    GameObject CreateButton(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, string label, Color color)
    {
        var go = CreateImage(name, parent, anchoredPosition, size, color);
        var button = go.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = ButtonHover;
        colors.selectedColor = ButtonHover;
        colors.pressedColor = color * 0.8f;
        button.colors = colors;

        var text = CreateText("Label", go.transform, Vector2.zero,
            new Vector2(Mathf.Max(size.x - 20f, 220f), size.y - 8f),
            label, 15, FontStyle.Normal, TextWhite, TextAnchor.MiddleCenter);
        var textRt = text.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10f, 4f);
        textRt.offsetMax = new Vector2(-10f, -4f);
        return go;
    }

    Text CreateText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size,
        string content, int fontSize, FontStyle style, Color color, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;

        var text = go.AddComponent<Text>();
        text.font = _font;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    static void StretchToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
