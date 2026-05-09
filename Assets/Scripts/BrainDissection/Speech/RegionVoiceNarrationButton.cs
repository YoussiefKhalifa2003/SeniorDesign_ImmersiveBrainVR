using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Head-tracked floating "Ask Aloud" HUD for the Play-mode voice narration
/// system. The HUD lives on a world-space canvas parented to the active
/// head camera so the panel travels with the player's gaze, and it only
/// appears while a region is currently extracted/inspected.
///
/// Visibility rules (all must hold for the HUD to show):
///   - <see cref="SessionData.IsPlayMode"/> is true and Tutorial / Assessment
///     / Live Dissection contexts are inactive.
///   - <see cref="BrainManager.IsInspectingRegion"/> is true and the
///     inspected region exposes <see cref="RegionData"/>.
///
/// One tap toggles narration:
///   Idle  → starts listening (mic captures up to N seconds).
///   Listening → stops recording early.
///   Speaking  → cancels playback so the student can ask something else.
///
/// The HUD also surfaces a hint line ("Try: 'What is this region?'") so
/// first-time students know how to phrase a question without having to read
/// any of the description text.
/// </summary>
public class RegionVoiceNarrationButton : MonoBehaviour
{
    static readonly Color ColorIdle = new Color(0.20f, 0.60f, 0.45f, 1f);
    static readonly Color ColorListening = new Color(0.85f, 0.30f, 0.30f, 1f);
    static readonly Color ColorTranscribing = new Color(0.55f, 0.45f, 0.85f, 1f);
    static readonly Color ColorSpeaking = new Color(0.30f, 0.55f, 0.85f, 1f);

    static readonly string[] Hints = new[]
    {
        "Tip: tap and ask \"What is this region?\"",
        "Tip: try \"Describe this region\"",
        "Tip: try \"What does this region do?\"",
    };

    // Position relative to the head camera (metres). Sits to the upper-right
    // of the eye view: clearly visible, but well outside the central area
    // where the brain itself is being held. Mirrors the placement convention
    // used by FpsCounterOverlay so HUD elements feel consistent.
    static readonly Vector3 LocalOffset = new Vector3(0.30f, 0.20f, 0.7f);

    static RegionVoiceNarrationButton _instance;

    Camera _attachedCamera;
    GameObject _hudRoot;
    GameObject _panel;
    Button _button;
    Image _bg;
    Text _label;
    Text _hint;

    PlayModeRegionVoiceNarration _service;
    int _hintIndex;
    float _nextHintRotateTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject(nameof(RegionVoiceNarrationButton));
        _instance = go.AddComponent<RegionVoiceNarrationButton>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    void OnEnable()
    {
        if (PlayModeRegionVoiceNarration.Instance != null)
            BindToService(PlayModeRegionVoiceNarration.Instance);
    }

    void OnDisable()
    {
        if (_service != null) _service.OnStateChanged -= OnStateChanged;
        _service = null;
    }

    void Update()
    {
        if (_service == null && PlayModeRegionVoiceNarration.Instance != null)
            BindToService(PlayModeRegionVoiceNarration.Instance);

        EnsureHud();
        AttachToCamera();

        bool show = ShouldShowHud();
        if (_hudRoot != null && _hudRoot.activeSelf != show)
            _hudRoot.SetActive(show);

        if (!show) return;

        UpdateButtonAppearance();
        RotateHintIfNeeded();
    }

    bool ShouldShowHud()
    {
        if (!SessionData.IsPlayMode) return false;
        if (SessionData.IsTutorialMode) return false;
        if (SessionData.IsAssessmentMode) return false;

        var live = LiveDissectionManager.Instance;
        if (live != null && live.IsLiveDissectionActive) return false;
        var tut = TutorialManager.Instance;
        if (tut != null && tut.IsTutorialActive) return false;

        var bm = FindFirstObjectByType<BrainManager>();
        if (bm == null) return false;
        if (!bm.IsInspectingRegion) return false;
        var region = bm.InspectedRegion;
        return region != null && region.regionData != null;
    }

    void BindToService(PlayModeRegionVoiceNarration service)
    {
        if (_service == service) return;
        if (_service != null) _service.OnStateChanged -= OnStateChanged;
        _service = service;
        _service.OnStateChanged += OnStateChanged;
    }

    void OnStateChanged(PlayModeRegionVoiceNarration.State state) => UpdateButtonAppearance();

    void EnsureHud()
    {
        if (_hudRoot != null) return;

        _hudRoot = new GameObject("VoiceNarrationHUD");
        _hudRoot.transform.SetParent(transform, false);

        var canvas = _hudRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 4900;
        _hudRoot.AddComponent<CanvasScaler>();
        _hudRoot.AddComponent<GraphicRaycaster>();
        _hudRoot.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

        var canvasRT = _hudRoot.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(360f, 130f);
        canvasRT.localScale = Vector3.one * 0.001f;

        // ===== Panel (button + hint stacked vertically) =====
        _panel = new GameObject("Panel");
        _panel.transform.SetParent(_hudRoot.transform, false);
        var panelRT = _panel.AddComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(360f, 130f);
        panelRT.anchoredPosition = Vector2.zero;
        var panelBg = _panel.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.06f, 0.12f, 0.85f);

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // ===== Button =====
        var btnGO = new GameObject("AskAloudButton");
        btnGO.transform.SetParent(_panel.transform, false);
        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin = btnRT.anchorMax = new Vector2(0.5f, 1f);
        btnRT.pivot = new Vector2(0.5f, 1f);
        btnRT.sizeDelta = new Vector2(320f, 60f);
        btnRT.anchoredPosition = new Vector2(0f, -10f);

        _bg = btnGO.AddComponent<Image>();
        _bg.color = ColorIdle;
        _button = btnGO.AddComponent<Button>();
        _button.targetGraphic = _bg;
        _button.onClick.AddListener(OnButtonClicked);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        var lrt = labelGO.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        _label = labelGO.AddComponent<Text>();
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = new Color(0.95f, 0.95f, 0.97f, 1f);
        _label.fontStyle = FontStyle.Bold;
        _label.fontSize = 22;
        _label.font = font;
        _label.text = "Ask Aloud";

        // ===== Hint subtitle =====
        var hintGO = new GameObject("Hint");
        hintGO.transform.SetParent(_panel.transform, false);
        var hrt = hintGO.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0.5f, 0f);
        hrt.anchorMax = new Vector2(0.5f, 0f);
        hrt.pivot = new Vector2(0.5f, 0f);
        hrt.sizeDelta = new Vector2(340f, 44f);
        hrt.anchoredPosition = new Vector2(0f, 8f);
        _hint = hintGO.AddComponent<Text>();
        _hint.alignment = TextAnchor.MiddleCenter;
        _hint.fontStyle = FontStyle.Italic;
        _hint.color = new Color(0.78f, 0.82f, 0.92f, 1f);
        _hint.fontSize = 14;
        _hint.font = font;
        _hint.horizontalOverflow = HorizontalWrapMode.Wrap;
        _hint.verticalOverflow = VerticalWrapMode.Overflow;
        _hint.text = Hints[0];

        _hudRoot.SetActive(false);
    }

    /// <summary>
    /// Re-parent the HUD canvas to the current head camera. Runs every
    /// frame because the active camera can change across scene loads
    /// (start menu → play scene) and <see cref="Camera.main"/> may be null
    /// for the first few frames after loading.
    /// </summary>
    void AttachToCamera()
    {
        if (_hudRoot == null) return;
        var cam = Camera.main;
        if (cam == null || _attachedCamera == cam) return;

        _attachedCamera = cam;
        _hudRoot.transform.SetParent(cam.transform, false);
        _hudRoot.transform.localPosition = LocalOffset;
        _hudRoot.transform.localRotation = Quaternion.identity;
    }

    void OnButtonClicked()
    {
        if (_service == null) return;

        switch (_service.CurrentState)
        {
            case PlayModeRegionVoiceNarration.State.Idle:
                _service.BeginListening();
                break;
            case PlayModeRegionVoiceNarration.State.Listening:
                _service.EndListening();
                break;
            case PlayModeRegionVoiceNarration.State.Transcribing:
                break;
            case PlayModeRegionVoiceNarration.State.Speaking:
                _service.Cancel();
                break;
        }
    }

    void UpdateButtonAppearance()
    {
        if (_panel == null || _service == null) return;

        switch (_service.CurrentState)
        {
            case PlayModeRegionVoiceNarration.State.Idle:
                _label.text = "Ask Aloud";
                _bg.color = ColorIdle;
                _button.interactable = _service.IsAvailable;
                break;
            case PlayModeRegionVoiceNarration.State.Listening:
                _label.text = "Listening… tap to stop";
                _bg.color = ColorListening;
                _button.interactable = true;
                break;
            case PlayModeRegionVoiceNarration.State.Transcribing:
                _label.text = "Transcribing…";
                _bg.color = ColorTranscribing;
                _button.interactable = false;
                break;
            case PlayModeRegionVoiceNarration.State.Speaking:
                _label.text = "Reading… tap to stop";
                _bg.color = ColorSpeaking;
                _button.interactable = true;
                break;
        }
    }

    /// <summary>
    /// Cycle through the example hints every few seconds so first-time
    /// students see more than one suggested phrasing without having to
    /// read a wall of text. Hint rotation pauses while narration is
    /// actively running so the student can focus on the response.
    /// </summary>
    void RotateHintIfNeeded()
    {
        if (_hint == null || _service == null) return;
        if (_service.CurrentState != PlayModeRegionVoiceNarration.State.Idle) return;
        if (Time.unscaledTime < _nextHintRotateTime) return;

        _hintIndex = (_hintIndex + 1) % Hints.Length;
        _hint.text = Hints[_hintIndex];
        _nextHintRotateTime = Time.unscaledTime + 4f;
    }
}
