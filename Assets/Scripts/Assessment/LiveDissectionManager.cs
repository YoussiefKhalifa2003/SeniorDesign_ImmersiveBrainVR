using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Live Dissection assessment mode with holographic brain scanner.
///
/// The Dissection_Brain transforms into a floating, rotatable hologram above the patient.
/// Grip (hold) on either controller rotates the hologram -- polled directly via XR InputDevice
/// so rotation is smooth and doesn't depend on hovering a specific region.
/// Trigger-select on a red candidate region to answer the clinical case.
/// </summary>
public class LiveDissectionManager : MonoBehaviour
{
    public static LiveDissectionManager Instance { get; private set; }

    [Header("Optional: assign a CasePrompt asset. If null, uses defaults.")]
    public CasePrompt caseBank;

    // ========================= GAME STATE =========================

    List<DissectionCase> _cases;
    int _caseIndex;
    int _totalScore;
    int _maxPossibleScore;
    int _totalAttempts;
    int _caseAttempts;
    bool _hintUsed;
    bool _active;
    bool _waitingForSelection;

    BrainRegion _correctRegion;
    string _lastWrongSelectionName;
    List<BrainRegion> _candidateRegions = new List<BrainRegion>();
    List<BrainRegion> _allRegions = new List<BrainRegion>();
    HashSet<BrainRegion> _recentlyUsedRegions = new HashSet<BrainRegion>();
    List<GameObject> _overlays = new List<GameObject>();
    Dictionary<BrainRegion, GameObject> _regionOverlayMap = new Dictionary<BrainRegion, GameObject>();
    Dictionary<Renderer, Color> _savedRegionColors = new Dictionary<Renderer, Color>();
    Dictionary<Renderer, Color> _savedRegionEmission = new Dictionary<Renderer, Color>();

    List<int> _pointsPerCase = new List<int>();
    List<int> _attemptsPerCase = new List<int>();
    List<Coroutine> _pulseCoroutines = new List<Coroutine>();

    // Streak system
    int _currentStreak;
    int _bestStreak;
    Text _streakText;

    // Difficulty tier: 1 = Normal (case difficulty <= 2), 2 = Difficult (all cases).
    int _selectedDifficulty;
    GameObject _difficultyPanel;
    const int NormalCandidateCount = 5;
    const int DifficultMinCandidates = 12;
    const int DifficultMaxCandidates = 15;

    // Case review data
    struct CaseReview
    {
        public string scenario;
        public string correctRegion;
        public string selectedWrongRegion;
        public string explanation;
        public int points;
        public int attempts;
    }
    List<CaseReview> _caseReviews = new List<CaseReview>();
    GameObject _reviewPanel;
    Text _reviewText;
    int _reviewPage;

    // ========================= SCENE OBJECTS =========================

    GameObject _patientDummy;
    GameObject _dissectionBrain;
    GameObject _brainRoot;
    GameObject _hiddenKnife;

    // ========================= HOLOGRAM =========================

    GameObject _hologramPivot;
    Vector3 _brainOrigPos;
    Quaternion _brainOrigRot;
    Transform _brainOrigParent;
    bool _hologramActive;

    bool _idleRotating;
    const float IdleRotSpeed = 3f;

    // Direct grip polling for rotation
    bool _isUserRotating;
    Quaternion _lastDeviceRot;
    List<InputDevice> _deviceBuffer = new List<InputDevice>();

    // Hover region + trigger edge tracking
    BrainRegion _rayHoveredRegion;
    bool _triggerWasDown;

    // Hologram visuals
    GameObject _statusLabelGO;
    GameObject _projectorBase;
    Coroutine _riseCoroutine;

    struct SavedMaterial
    {
        public Renderer renderer;
        public Material originalMaterial;
    }
    List<SavedMaterial> _savedHoloMats = new List<SavedMaterial>();

    // (easy-hit colliders removed — using angular raycast instead)

    // Lighting
    struct SavedLight
    {
        public Light light;
        public bool wasEnabled;
        public float intensity;
    }
    List<SavedLight> _savedLights = new List<SavedLight>();

    // Saved RenderSettings for full dark-room restore
    UnityEngine.Rendering.AmbientMode _savedAmbientMode;
    Color _savedAmbientLight;
    float _savedAmbientIntensity;
    float _savedReflectionIntensity;

    struct SavedEmissive { public Material mat; public string prop; public Color color; }
    List<SavedEmissive> _savedEmissives = new List<SavedEmissive>();

    struct SavedLightmapInfo { public Renderer rend; public int idx; public Vector4 so; }
    List<SavedLightmapInfo> _savedLightmaps = new List<SavedLightmapInfo>();

    List<GameObject> _dynamicSpotlights = new List<GameObject>();

    // URP pipeline: true originals captured once at Start(), never overwritten
    UniversalRenderPipelineAsset _urpAsset;
    int _savedURPLightMode = -1;
    int _savedURPLightLimit = -1;
    static int _trueOriginalURPMode = -1;
    static int _trueOriginalURPLimit = -1;
    static bool _trueOriginalsCaputred;

    // OptionsController: cached when entering Live Dissection
    OptionsController _optCtrl;

    // Golden baseline: captured on first successful dark-room setup and replayed on all
    // subsequent entries so that lighting is 100% identical regardless of user or session.
    struct GoldenLightState { public string name; public LightType type; public float intensity; public bool enabled; }
    bool _goldenCaptured;
    List<GoldenLightState> _goldenLights = new List<GoldenLightState>();

    // sm_lights bulb glow (we boost emission on the lamp heads)
    struct SavedBulbEmission { public Material mat; public Color original; }
    List<SavedBulbEmission> _savedBulbEmissions = new List<SavedBulbEmission>();

    // ========================= UI =========================

    GameObject _canvas;
    Text _scenarioText;
    Text _progressText;
    Text _feedbackText;
    Text _timerText;
    GameObject _feedbackPanel;
    GameObject _continueBtn;
    GameObject _hintBtn;
    GameObject _finishPanel;
    float _ldStartTime;
    int _ldElapsedFrozenSeconds;

    static readonly Color PanelBg = new Color(0.06f, 0.06f, 0.10f, 0.94f);
    static readonly Color BtnGreen = new Color(0.12f, 0.50f, 0.22f, 1f);
    static readonly Color BtnOrange = new Color(0.70f, 0.45f, 0.10f, 1f);
    static readonly Color BtnYellow = new Color(0.75f, 0.65f, 0.10f, 1f);
    static readonly Color TextWhite = new Color(0.95f, 0.95f, 0.97f, 1f);
    static readonly Color TextDim = new Color(0.70f, 0.70f, 0.75f, 1f);
    static readonly Color HoloCyan = new Color(0.2f, 0.85f, 1f, 0.75f);
    const float LiveDissectionDirectionalIntensity = 0.04f;
    const float LiveDissectionAmbientIntensity = 0.1f;
    const float LiveDissectionRuntimeSpotIntensity = 8f;
    const float LiveDissectionFillIntensity = 10f;
    const float LiveDissectionExtraSpotIntensity = 10f;
    const float LiveDissectionFillRange = 4.5f;
    const float LiveDissectionSpotRange = 8f;
    const float LiveDissectionSpotAngle = 70f;
    const float LiveDissectionInnerSpotAngle = 35f;
    const float LiveDissectionPostExposure = 0f;
    static readonly Color LiveDissectionAmbientColor = new Color(0.03f, 0.03f, 0.05f);

    static readonly Color OverlayNormal = new Color(0.95f, 0.12f, 0.12f, 0.08f);
    static readonly Color OverlayHover = new Color(1f, 0.85f, 0.1f, 0.90f);
    static readonly Color OverlayCorrect = new Color(0.1f, 0.95f, 0.2f, 0.6f);
    static readonly UnityEngine.XR.XRNode[] HandNodes = { UnityEngine.XR.XRNode.RightHand, UnityEngine.XR.XRNode.LeftHand };
    static readonly Color OverlayFlash = new Color(1f, 0f, 0f, 0.75f);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        _goldenCaptured = false;
        _goldenLights.Clear();

        // Capture the TRUE original URP values in Awake (before any coroutine can call StartLiveDissection)
        if (!_trueOriginalsCaputred)
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != null)
            {
                var assetType = asset.GetType();
                var mf = assetType.GetField("m_AdditionalLightsRenderingMode",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var lf = assetType.GetField("m_AdditionalLightsPerObjectLimit",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (mf != null) _trueOriginalURPMode = (int)mf.GetValue(asset);
                if (lf != null) _trueOriginalURPLimit = (int)lf.GetValue(asset);
                _trueOriginalsCaputred = true;
                Debug.Log($"[LiveDissection] Captured true URP originals: mode={_trueOriginalURPMode}, limit={_trueOriginalURPLimit}");
            }
        }
    }

    void Start()
    {
        _goldenCaptured = false;
        _goldenLights.Clear();
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t != null && t.name.StartsWith("LD_OperatingLight"))
                t.gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        UnsubscribeRegionHoverEvents();
        if (_savedURPLightMode >= 0 || _savedURPLightLimit >= 0)
            RestoreURPLights();
        // Safety net: if destroyed mid-session, restore user settings
        if (_optCtrl != null)
            _optCtrl.ReloadForCurrentUser();
    }

    // ========================= PUBLIC API =========================

    public bool IsLiveDissectionActive => _active;

    public void StartLiveDissection()
    {
        ActivateSceneObjects();
        ShowDifficultySelector();
    }

    void ShowDifficultySelector()
    {
        if (_difficultyPanel != null) Destroy(_difficultyPanel);
        if (_canvas != null) Destroy(_canvas);

        _canvas = new GameObject("LD_Canvas");
        var canvas = _canvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _canvas.AddComponent<CanvasScaler>();
        _canvas.AddComponent<TrackedDeviceGraphicRaycaster>();

        var crt = _canvas.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(600, 300);
        crt.localScale = Vector3.one * 0.0008f;

        var cam = Camera.main;
        if (cam != null)
        {
            _canvas.transform.position = cam.transform.position + cam.transform.forward * 0.8f;
            _canvas.transform.rotation = Quaternion.LookRotation(
                _canvas.transform.position - cam.transform.position);
        }

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        _difficultyPanel = MakeRect("DiffPanel", _canvas.transform, Vector2.zero, crt.sizeDelta).gameObject;
        _difficultyPanel.AddComponent<Image>().color = PanelBg;

        MakeText("Title", _difficultyPanel.transform, new Vector2(0, 100), new Vector2(500, 40),
            "Select Difficulty", 20, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);

        MakeText("Desc", _difficultyPanel.transform, new Vector2(0, 55), new Vector2(500, 30),
            "Normal: 5 highlighted regions per case.\nDifficult: 12\u201315 highlighted regions and a longer case set.",
            14, FontStyle.Normal, TextDim, TextAnchor.MiddleCenter, font);

        var normalBtn = MakeButton("NormalBtn", _difficultyPanel.transform, new Vector2(-90, -25), new Vector2(170, 60),
            "Normal", new Color(0.2f, 0.7f, 0.3f), font);
        normalBtn.GetComponent<Button>().onClick.AddListener(() => BeginWithDifficulty(1));

        var hardBtn = MakeButton("DifficultBtn", _difficultyPanel.transform, new Vector2(90, -25), new Vector2(170, 60),
            "Difficult", new Color(0.8f, 0.2f, 0.2f), font);
        hardBtn.GetComponent<Button>().onClick.AddListener(() => BeginWithDifficulty(2));
    }

    void BeginWithDifficulty(int tier)
    {
        _selectedDifficulty = tier;
        if (_difficultyPanel != null) Destroy(_difficultyPanel);
        _difficultyPanel = null;
        if (_canvas != null) { Destroy(_canvas); _canvas = null; }

        var allCases = caseBank != null && caseBank.cases.Count > 0
            ? new List<DissectionCase>(caseBank.cases)
            : DefaultCaseData.GetCases();

        // Normal = case difficulty 1 or 2 (the gentler half of the bank).
        // Difficult = the entire bank, which is strictly larger.
        if (tier == 1)
            _cases = allCases.FindAll(c => c.difficulty <= 2);
        else
            _cases = allCases;

        if (_cases.Count < 3)
            _cases = allCases;

        ShuffleCases();

        _caseIndex = 0;
        _totalScore = 0;
        _maxPossibleScore = _cases.Count * 3;
        _totalAttempts = 0;
        _active = true;
        _ldStartTime = Time.time;
        _ldElapsedFrozenSeconds = 0;
        SubscribeRegionHoverEvents();
        if (SoundManager.Instance != null) SoundManager.Instance.StartAmbient();

        _pointsPerCase.Clear();
        _attemptsPerCase.Clear();
        _currentStreak = 0;
        _bestStreak = 0;
        _caseReviews.Clear();

        CollectAllRegions();
        SetupToolsForDissection();
        CreateHologram();
        BuildUI();
        PresentCase();

        string tierName = tier == 1 ? "Normal" : "Difficult";
        Debug.Log($"[LiveDissection] Started ({tierName}) with {_cases.Count} cases, {_allRegions.Count} regions.");
    }

    public void EndLiveDissection()
    {
        UnsubscribeRegionHoverEvents();
        _active = false;
        _waitingForSelection = false;
        _isUserRotating = false;
        if (SoundManager.Instance != null) SoundManager.Instance.StopAmbient();

        ClearOverlays();
        DestroyHologram();
        RestoreTools();
        DeactivateSceneObjects();

        if (_canvas != null) Destroy(_canvas);

        var mm = FindFirstObjectByType<MenuManager>();
        if (mm != null)
        {
            if (mm.movementGate != null) mm.movementGate.DisableMovement();
            if (mm.doorController != null) mm.doorController.CloseDoors();

            var fp = FindFirstObjectByType<FloatingInfoPanel>();
            if (fp != null)
            {
                var cg = fp.GetComponent<CanvasGroup>();
                if (cg != null) { cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false; }
            }

            mm.TeleportToStart();
            if (mm.startMenuCanvas != null) mm.startMenuCanvas.SetActive(true);
            if (mm.menuCanvasGroup != null)
            {
                mm.menuCanvasGroup.alpha = 1f;
                mm.menuCanvasGroup.interactable = true;
                mm.menuCanvasGroup.blocksRaycasts = true;
            }
            mm.ShowAssessment();
        }
        SessionData.IsAssessmentMode = false;
    }

    public void OnRegionSelected(BrainRegion region)
    {
        if (!_active || !_waitingForSelection) return;
        if (_isUserRotating) return;
        if (!_candidateRegions.Contains(region)) return;

        _caseAttempts++;
        _totalAttempts++;

        if (region == _correctRegion) OnCorrectSelection();
        else OnWrongSelection(region);
    }

    // No longer needed -- rotation is polled directly via XR InputDevice
    public void StartHologramRotate(Transform interactor) { }
    public void EndHologramRotate() { }

    // ========================= UPDATE: GRIP ROTATION =========================

    void Update()
    {
        if (_active && _timerText != null)
        {
            int secs = Mathf.FloorToInt(Time.time - _ldStartTime);
            _timerText.text = $"Time: {LeaderboardManager.FormatElapsed(Mathf.Max(1, secs))}";
        }

        if (!_hologramActive || _hologramPivot == null) return;

        PollGripRotation();
        PollThumbstickZoom();
        PollRaycastInteraction();
    }

    void PollGripRotation()
    {
        if (TryGripHand(XRNode.RightHand)) return;
        if (TryGripHand(XRNode.LeftHand)) return;

        if (_isUserRotating)
            _isUserRotating = false;
    }

    bool TryGripHand(XRNode node)
    {
        _deviceBuffer.Clear();
        InputDevices.GetDevicesAtXRNode(node, _deviceBuffer);

        foreach (var device in _deviceBuffer)
        {
            if (!device.isValid) continue;
            if (!device.TryGetFeatureValue(CommonUsages.grip, out float gripVal)) continue;
            if (gripVal < 0.5f) continue;

            if (!device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
                continue;

            if (!_isUserRotating)
            {
                _isUserRotating = true;
                _idleRotating = false;
                _lastDeviceRot = rot;
            }
            else
            {
                Quaternion delta = rot * Quaternion.Inverse(_lastDeviceRot);
                _hologramPivot.transform.rotation = delta * _hologramPivot.transform.rotation;
                _lastDeviceRot = rot;
            }
            return true;
        }
        return false;
    }

    const float MinHoloScale = 0.6f;
    const float MaxHoloScale = 1.8f;
    const float ZoomSpeed    = 0.6f;
    float _targetHoloScale   = 1f;

    void PollThumbstickZoom()
    {
        // Zoom with A button (primaryButton) = zoom in, B button (secondaryButton) = zoom out
        // This avoids conflicting with thumbstick locomotion
        float zoomDir = 0f;
        foreach (var node in HandNodes)
        {
            _deviceBuffer.Clear();
            InputDevices.GetDevicesAtXRNode(node, _deviceBuffer);
            foreach (var d in _deviceBuffer)
            {
                if (!d.isValid) continue;
                if (d.TryGetFeatureValue(CommonUsages.primaryButton, out bool aBtn) && aBtn)
                    zoomDir += 1f;
                if (d.TryGetFeatureValue(CommonUsages.secondaryButton, out bool bBtn) && bBtn)
                    zoomDir -= 1f;
            }
        }

        if (Mathf.Abs(zoomDir) < 0.1f) return;

        _targetHoloScale = Mathf.Clamp(
            _targetHoloScale + zoomDir * ZoomSpeed * Time.deltaTime,
            MinHoloScale, MaxHoloScale);

        float current = _hologramPivot.transform.localScale.x;
        float smooth = Mathf.Lerp(current, _targetHoloScale, Time.deltaTime * 6f);
        _hologramPivot.transform.localScale = Vector3.one * smooth;
    }

    void SubscribeRegionHoverEvents()
    {
        BrainRegion.OnAnyHoverEntered -= HandleXRHover;
        BrainRegion.OnAnyHoverExited  -= HandleXRUnhover;
        BrainRegion.OnAnyHoverEntered += HandleXRHover;
        BrainRegion.OnAnyHoverExited  += HandleXRUnhover;
    }

    void UnsubscribeRegionHoverEvents()
    {
        BrainRegion.OnAnyHoverEntered -= HandleXRHover;
        BrainRegion.OnAnyHoverExited  -= HandleXRUnhover;
    }

    void HandleXRHover(BrainRegion region)
    {
        if (!_active || !_waitingForSelection || region == null) return;
        if (!_candidateRegions.Contains(region)) return;

        if (_rayHoveredRegion != null && _rayHoveredRegion != region)
            OnRegionUnhovered(_rayHoveredRegion);

        _rayHoveredRegion = region;
        OnRegionHovered(region);
    }

    void HandleXRUnhover(BrainRegion region)
    {
        if (region == null || _rayHoveredRegion != region) return;
        OnRegionUnhovered(region);
        _rayHoveredRegion = null;
    }

    // ========================= TRIGGER SELECTION =========================

    void PollRaycastInteraction()
    {
        if (!_waitingForSelection) return;

        bool triggerDown = IsTriggerDown();
        if (_rayHoveredRegion != null && triggerDown && !_triggerWasDown && !_isUserRotating)
            OnRegionSelected(_rayHoveredRegion);
        _triggerWasDown = triggerDown;
    }

    bool IsTriggerDown()
    {
        foreach (var node in HandNodes)
        {
            _deviceBuffer.Clear();
            InputDevices.GetDevicesAtXRNode(node, _deviceBuffer);
            foreach (var d in _deviceBuffer)
            {
                if (!d.isValid) continue;
                if (d.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed)
                    return true;
            }
        }
        return false;
    }

    void LateUpdate()
    {
        if (_statusLabelGO == null || !_hologramActive) return;
        if (_hologramPivot != null)
            _statusLabelGO.transform.position = _hologramPivot.transform.position + Vector3.up * 0.20f;

        var cam = Camera.main;
        if (cam != null)
        {
            var pos = _statusLabelGO.transform.position;
            _statusLabelGO.transform.rotation = Quaternion.LookRotation(pos - cam.transform.position);
        }
    }

    // ========================= SELECTION HANDLERS =========================

    void OnCorrectSelection()
    {
        _waitingForSelection = false;
        int basePts = CalculatePoints();

        _currentStreak++;
        if (_currentStreak > _bestStreak) _bestStreak = _currentStreak;

        float multiplier = Mathf.Min(1f + 0.1f * (_currentStreak - 1), 2f);
        int pts = Mathf.RoundToInt(basePts * multiplier);

        _totalScore += pts;
        _pointsPerCase.Add(pts);
        _attemptsPerCase.Add(_caseAttempts);

        HapticFeedback.PulseBoth(0.3f, 0.2f);
        if (SoundManager.Instance != null) SoundManager.Instance.PlayCorrect();

        if (_regionOverlayMap.TryGetValue(_correctRegion, out var overlay) && overlay != null)
        {
            var rend = overlay.GetComponent<Renderer>();
            if (rend != null) rend.material.color = OverlayCorrect;
            StartCoroutine(PulseOverlayBrightness(overlay));
        }

        string streakLabel = _currentStreak >= 2 ? $"  ({_currentStreak}x Streak!)" : "";
        var c = _cases[_caseIndex];
        _feedbackText.text = $"Correct! (+{pts} pts){streakLabel}\n{c.explanation}";
        _feedbackPanel.SetActive(true);
        _continueBtn.SetActive(true);
        if (_hintBtn != null) _hintBtn.SetActive(false);
        _progressText.text = $"Case {_caseIndex + 1} / {_cases.Count}  |  Score: {_totalScore}";
        UpdateStreakUI();

        _caseReviews.Add(new CaseReview
        {
            scenario = c.scenarioText,
            correctRegion = _correctRegion != null ? _correctRegion.regionData.displayName : c.correctRegionKeyword,
            selectedWrongRegion = _caseAttempts > 1 ? _lastWrongSelectionName : null,
            explanation = c.explanation,
            points = pts,
            attempts = _caseAttempts
        });

        TextToSpeech.Speak("Correct. " + c.explanation);
    }

    void OnWrongSelection(BrainRegion region)
    {
        _currentStreak = 0;
        UpdateStreakUI();
        _lastWrongSelectionName = region != null && region.regionData != null
            ? region.regionData.displayName : "Unknown";

        HapticFeedback.PulseBoth(0.8f, 0.4f);
        if (SoundManager.Instance != null) SoundManager.Instance.PlayWrong();

        if (_regionOverlayMap.TryGetValue(region, out var overlay) && overlay != null)
            StartCoroutine(FlashOverlayRed(overlay));

        _feedbackText.text = $"Incorrect (Attempt {_caseAttempts}). Try again.";
        _feedbackPanel.SetActive(true);
        StartCoroutine(HideFeedbackAfter(2f));

        if (_caseAttempts >= 2 && !_hintUsed && _hintBtn != null)
            _hintBtn.SetActive(true);
    }

    int CalculatePoints()
    {
        if (_caseAttempts <= 1) return _hintUsed ? 2 : 3;
        if (_caseAttempts == 2) return _hintUsed ? 1 : 2;
        if (_caseAttempts == 3) return 1;
        return 0;
    }

    void UpdateStreakUI()
    {
        if (_streakText == null) return;
        _streakText.text = _currentStreak >= 2
            ? $"\u2b50 {_currentStreak}x Streak (Best: {_bestStreak})"
            : "";
    }

    IEnumerator PulseOverlayBrightness(GameObject overlay)
    {
        var rend = overlay?.GetComponent<Renderer>();
        if (rend == null) yield break;
        float elapsed = 0f;
        while (elapsed < 0.8f)
        {
            elapsed += Time.deltaTime;
            float a = 0.6f + Mathf.Sin(elapsed * Mathf.PI * 4f) * 0.2f;
            Color c = OverlayCorrect;
            c.a = a;
            if (rend != null) rend.material.color = c;
            yield return null;
        }
        if (rend != null) rend.material.color = OverlayCorrect;
    }

    IEnumerator FlashOverlayRed(GameObject overlay)
    {
        var rend = overlay?.GetComponent<Renderer>();
        if (rend == null) yield break;
        rend.material.color = OverlayFlash;
        yield return new WaitForSeconds(0.4f);
        if (rend != null) rend.material.color = OverlayNormal;
    }

    IEnumerator HideFeedbackAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (_waitingForSelection && _feedbackPanel != null)
            _feedbackPanel.SetActive(false);
    }

    // ========================= HINT =========================

    public void OnHintPressed()
    {
        if (!_active || !_waitingForSelection || _hintUsed) return;
        _hintUsed = true;
        if (_hintBtn != null) _hintBtn.SetActive(false);

        var wrongOnes = new List<BrainRegion>();
        foreach (var r in _candidateRegions)
            if (r != _correctRegion) wrongOnes.Add(r);

        int toRemove = Mathf.Min(2, wrongOnes.Count);
        for (int i = 0; i < toRemove; i++)
        {
            int idx = Random.Range(0, wrongOnes.Count);
            var victim = wrongOnes[idx];
            wrongOnes.RemoveAt(idx);

            if (_regionOverlayMap.TryGetValue(victim, out var ov) && ov != null)
                StartCoroutine(DissolveOverlay(ov));
            _regionOverlayMap.Remove(victim);
            _candidateRegions.Remove(victim);

            foreach (var col in victim.GetComponentsInChildren<Collider>(true))
                if (col != null) col.enabled = false;
        }

        _feedbackText.text = "Hint used! 2 wrong regions removed. (-1 point penalty)";
        _feedbackPanel.SetActive(true);
        StartCoroutine(HideFeedbackAfter(2f));
    }

    IEnumerator DissolveOverlay(GameObject overlay)
    {
        var rend = overlay?.GetComponent<Renderer>();
        if (rend == null) { if (overlay != null) Destroy(overlay); yield break; }

        Color c = rend.material.color;
        float elapsed = 0f;
        while (elapsed < 0.5f)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(0.5f, 0f, elapsed / 0.5f);
            if (rend != null) rend.material.color = c;
            yield return null;
        }
        if (overlay != null) Destroy(overlay);
    }

    // ========================= CONTINUE =========================

    public void OnContinuePressed()
    {
        ClearOverlays();
        EnableAllColliders();
        _caseIndex++;
        if (_caseIndex >= _cases.Count) ShowResults();
        else PresentCase();
    }

    // ========================= CASE LOGIC =========================

    void PresentCase()
    {
        ClearOverlays();
        EnableAllColliders();

        _lastWrongSelectionName = null;
        _feedbackPanel.SetActive(false);
        _continueBtn.SetActive(false);
        if (_hintBtn != null) _hintBtn.SetActive(false);
        if (_finishPanel != null) _finishPanel.SetActive(false);
        if (_scenarioText != null) _scenarioText.gameObject.SetActive(true);

        _caseAttempts = 0;
        _hintUsed = false;
        _waitingForSelection = false;

        var c = _cases[_caseIndex];
        _scenarioText.text = c.scenarioText;
        _progressText.text = $"Case {_caseIndex + 1} / {_cases.Count}  |  Score: {_totalScore}";

        _correctRegion = FindRegionByKeyword(c.correctRegionKeyword);
        if (_correctRegion == null)
        {
            _pointsPerCase.Add(0);
            _attemptsPerCase.Add(0);
            _caseIndex++;
            if (_caseIndex < _cases.Count) PresentCase();
            else ShowResults();
            return;
        }

        // Per-difficulty candidate count: Normal = 5, Difficult = random 12..15.
        int targetCandidates = _selectedDifficulty == 2
            ? Random.Range(DifficultMinCandidates, DifficultMaxCandidates + 1)
            : NormalCandidateCount;

        _candidateRegions.Clear();
        _candidateRegions.Add(_correctRegion);

        // Add keyword-matched wrong regions (prefer ones not recently used)
        if (c.wrongRegionKeywords != null)
        {
            foreach (var kw in c.wrongRegionKeywords)
            {
                var r = FindRegionByKeyword(kw, _candidateRegions);
                if (r != null) _candidateRegions.Add(r);
                if (_candidateRegions.Count >= targetCandidates) break;
            }
        }

        // Fill remaining slots from random regions, avoiding recently used ones first
        if (_candidateRegions.Count < targetCandidates)
        {
            var freshPool = new List<BrainRegion>();
            var stalePool = new List<BrainRegion>();
            foreach (var r in _allRegions)
            {
                if (r == null || !r.gameObject.activeInHierarchy || _candidateRegions.Contains(r)) continue;
                var rend = r.GetComponent<Renderer>();
                if (rend == null || !rend.enabled) continue;

                if (_recentlyUsedRegions.Contains(r))
                    stalePool.Add(r);
                else
                    freshPool.Add(r);
            }
            Shuffle(freshPool);
            Shuffle(stalePool);

            foreach (var r in freshPool)
            {
                _candidateRegions.Add(r);
                if (_candidateRegions.Count >= targetCandidates) break;
            }
            foreach (var r in stalePool)
            {
                if (_candidateRegions.Count >= targetCandidates) break;
                _candidateRegions.Add(r);
            }
        }

        // Track these as recently used (keep last ~20 to cycle through regions)
        foreach (var r in _candidateRegions)
            _recentlyUsedRegions.Add(r);
        if (_recentlyUsedRegions.Count > 20)
            _recentlyUsedRegions.Clear();

        Shuffle(_candidateRegions);
        DisableNonCandidateColliders();

        foreach (var region in _candidateRegions)
            CreateOverlay(region);

        _waitingForSelection = true;

        if (!string.IsNullOrEmpty(c.voiceoverText))
            TextToSpeech.Speak(c.voiceoverText);

        Debug.Log($"[LiveDissection] Case {_caseIndex + 1}: correct='{_correctRegion.regionData.displayName}', candidates={_candidateRegions.Count}");
    }

    BrainRegion FindRegionByKeyword(string keyword, List<BrainRegion> exclude = null)
    {
        if (string.IsNullOrEmpty(keyword)) return null;
        string kw = keyword.ToLower();
        foreach (var r in _allRegions)
        {
            if (r == null || r.regionData == null) continue;
            if (!r.gameObject.activeInHierarchy) continue;
            if (exclude != null && exclude.Contains(r)) continue;
            if (r.regionData.displayName.ToLower().Contains(kw)) return r;
        }
        return null;
    }

    // ========================= HOLOGRAM =========================

    void CreateHologram()
    {
        if (_dissectionBrain == null) return;

        _brainOrigParent = _dissectionBrain.transform.parent;
        _brainOrigPos = _dissectionBrain.transform.position;
        _brainOrigRot = _dissectionBrain.transform.rotation;

        Bounds holoBounds = CalculateBrainBounds();

        _hologramPivot = new GameObject("HologramPivot");
        _hologramPivot.transform.position = holoBounds.center;
        _hologramPivot.transform.rotation = Quaternion.identity;
        _dissectionBrain.transform.SetParent(_hologramPivot.transform, true);

        ApplyHolographicMaterials();
        CreateProjectorBase(holoBounds);
        CreateStatusLabel();

        if (WorldSpaceHoverLabel.Instance != null)
            WorldSpaceHoverLabel.Instance.SetBrainRoot(_dissectionBrain.transform);

        SetOperatingRoomLighting();

        _hologramActive = true;
        _idleRotating = false;
        _riseCoroutine = StartCoroutine(AnimateHologramRise(0.5f, 1.5f));

        Debug.Log("[LiveDissection] Hologram created.");
    }

    void DestroyHologram()
    {
        _hologramActive = false;
        _isUserRotating = false;
        _idleRotating = false;
        _rayHoveredRegion = null;
        _targetHoloScale = 1f;

        if (_riseCoroutine != null) { StopCoroutine(_riseCoroutine); _riseCoroutine = null; }

        RestoreAllHolographicMaterials();
        EnableAllColliders();
        ClearOverlays();

        RestoreSceneLighting();

        if (_statusLabelGO != null) Destroy(_statusLabelGO);
        if (_projectorBase != null) Destroy(_projectorBase);

        if (_dissectionBrain != null)
        {
            _dissectionBrain.transform.SetParent(_brainOrigParent, true);
            _dissectionBrain.transform.position = _brainOrigPos;
            _dissectionBrain.transform.rotation = _brainOrigRot;
        }
        if (_hologramPivot != null) Destroy(_hologramPivot);

        if (WorldSpaceHoverLabel.Instance != null)
        {
            var bm = FindFirstObjectByType<BrainManager>();
            if (bm != null && bm.brainRoot != null)
                WorldSpaceHoverLabel.Instance.SetBrainRoot(bm.brainRoot.transform);
        }
    }

    Bounds CalculateBrainBounds()
    {
        Bounds b = new Bounds(_dissectionBrain.transform.position, Vector3.zero);
        bool any = false;
        foreach (var rend in _dissectionBrain.GetComponentsInChildren<Renderer>(true))
        {
            if (rend == null || !rend.enabled) continue;
            if (!any) { b = rend.bounds; any = true; }
            else b.Encapsulate(rend.bounds);
        }
        if (!any) b = new Bounds(_dissectionBrain.transform.position, Vector3.one * 0.2f);
        return b;
    }

    IEnumerator AnimateHologramRise(float riseHeight, float duration)
    {
        if (_hologramPivot == null) yield break;
        Vector3 startPos = _hologramPivot.transform.position;
        Vector3 endPos = startPos + Vector3.up * riseHeight;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            if (_hologramPivot != null)
                _hologramPivot.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        if (_hologramPivot != null) _hologramPivot.transform.position = endPos;
    }

    // ========================= HOLOGRAPHIC MATERIALS =========================

    void ApplyHolographicMaterials()
    {
        if (_dissectionBrain == null) return;
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");

        foreach (var rend in _dissectionBrain.GetComponentsInChildren<Renderer>(true))
        {
            if (rend == null) continue;
            _savedHoloMats.Add(new SavedMaterial { renderer = rend, originalMaterial = rend.sharedMaterial });

            var mat = new Material(sh);
            mat.color = new Color(0.10f, 0.45f, 0.78f, 0.15f);
            mat.renderQueue = 3000;
            rend.material = mat;
        }
    }

    void RestoreAllHolographicMaterials()
    {
        foreach (var sm in _savedHoloMats)
            if (sm.renderer != null && sm.originalMaterial != null)
                sm.renderer.sharedMaterial = sm.originalMaterial;
        _savedHoloMats.Clear();
    }

    // ========================= PROJECTOR BASE =========================

    void CreateProjectorBase(Bounds brainBounds)
    {
        _projectorBase = new GameObject("ProjectorBase");
        _projectorBase.transform.position = _hologramPivot.transform.position - Vector3.up * (brainBounds.extents.y + 0.04f);

        float baseRadius = Mathf.Max(brainBounds.extents.x, brainBounds.extents.z) * 1.6f;

        // Main disc
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "BaseDisc";
        disc.transform.SetParent(_projectorBase.transform, false);
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localScale = new Vector3(baseRadius * 2f, 0.003f, baseRadius * 2f);
        Destroy(disc.GetComponent<Collider>());

        var discRend = disc.GetComponent<Renderer>();
        discRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        var discMat = new Material(sh);
        discMat.color = new Color(0.08f, 0.35f, 0.55f, 0.5f);
        discMat.renderQueue = 2999;
        discRend.material = discMat;

        // Outer ring (slightly larger, brighter)
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "BaseRing";
        ring.transform.SetParent(_projectorBase.transform, false);
        ring.transform.localPosition = Vector3.zero;
        ring.transform.localScale = new Vector3(baseRadius * 2.3f, 0.002f, baseRadius * 2.3f);
        Destroy(ring.GetComponent<Collider>());

        var ringRend = ring.GetComponent<Renderer>();
        ringRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var ringMat = new Material(sh);
        ringMat.color = new Color(0.15f, 0.70f, 0.95f, 0.35f);
        ringMat.renderQueue = 2998;
        ringRend.material = ringMat;
    }

    // ========================= PARTICLES =========================

    // ========================= STATUS LABEL =========================

    void CreateStatusLabel()
    {
        _statusLabelGO = new GameObject("ScanStatusLabel");
        _statusLabelGO.transform.position = _hologramPivot.transform.position + Vector3.up * 0.20f;

        var canvas = _statusLabelGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 200;

        var rt = _statusLabelGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(500, 35);
        rt.localScale = Vector3.one * 0.00028f;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(_statusLabelGO.transform, false);
        var txt = textGO.AddComponent<Text>();
        txt.text = "[ NEURAL SCAN ACTIVE ]";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 22;
        txt.fontStyle = FontStyle.Bold;
        txt.color = HoloCyan;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;

        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = textRT.offsetMax = Vector2.zero;
    }

    // ========================= LIGHTING =========================

    void SetOperatingRoomLighting()
    {
        _savedLights.Clear();
        _savedEmissives.Clear();
        _savedLightmaps.Clear();
        _savedBulbEmissions.Clear();

        // ==== 1. Force URP to render additional lights (spot/point) per-pixel ====
        ForceURPPerPixelLights();

        // ==== 2. Remove per-user influence: normalize lights + force fixed post-processing ====
        // OptionsController scales lights by 2^userBrightness. Reset to base so every user
        // enters the dark room from the same absolute light intensity baseline.
        // Then directly set the ColorAdjustments to fixed values on the same object
        // OptionsController already uses — no new volumes, no layer/priority issues.
        _optCtrl = FindFirstObjectByType<OptionsController>();
        if (_optCtrl != null)
        {
            _optCtrl.ApplyNeutralLighting();
            _optCtrl.ForcePostProcessing(LiveDissectionPostExposure, 0f);
        }

        // ==== 3. Dark ambient ====
        _savedAmbientMode         = RenderSettings.ambientMode;
        _savedAmbientLight        = RenderSettings.ambientLight;
        _savedAmbientIntensity    = RenderSettings.ambientIntensity;
        _savedReflectionIntensity = RenderSettings.reflectionIntensity;

        RenderSettings.ambientMode      = AmbientMode.Flat;
        RenderSettings.ambientLight     = LiveDissectionAmbientColor;
        RenderSettings.ambientIntensity = LiveDissectionAmbientIntensity;
        RenderSettings.reflectionIntensity = 0f;

        // ==== 3. Safe-set (hologram + brain + sm_lights model) ====
        HashSet<Transform> safe = new HashSet<Transform>();
        if (_dissectionBrain != null)
            foreach (var t in _dissectionBrain.GetComponentsInChildren<Transform>(true))
                safe.Add(t);
        if (_hologramPivot != null)
            foreach (var t in _hologramPivot.GetComponentsInChildren<Transform>(true))
                safe.Add(t);
        GameObject smLightsGO = FindSmLights();
        if (smLightsGO != null)
            foreach (var t in smLightsGO.GetComponentsInChildren<Transform>(true))
                safe.Add(t);

        // ==== 4. Save and dim scene lights ====
        // On first entry: dim directionals to 8%, disable others, then capture results as golden baseline.
        // On subsequent entries: apply golden baseline values directly (guarantees identical look).
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light == null) continue;
            _savedLights.Add(new SavedLight
                { light = light, wasEnabled = light.enabled, intensity = light.intensity });
        }

        if (_goldenCaptured)
        {
            foreach (var sl in _savedLights)
            {
                if (sl.light == null) continue;
                var golden = _goldenLights.Find(g => g.name == sl.light.name && g.type == sl.light.type);
                if (golden.name != null)
                {
                    sl.light.intensity = golden.intensity;
                    sl.light.enabled = golden.enabled;
                }
                else
                {
                    if (sl.light.type == LightType.Directional)
                    {
                        sl.light.enabled = true;
                        sl.light.intensity = LiveDissectionDirectionalIntensity;
                    }
                    else
                    {
                        sl.light.enabled = false;
                    }
                }
            }
        }
        else
        {
            foreach (var sl in _savedLights)
            {
                if (sl.light == null) continue;
                if (sl.light.type == LightType.Directional)
                {
                    sl.light.enabled = true;
                    sl.light.intensity = LiveDissectionDirectionalIntensity;
                }
                else
                    sl.light.enabled = false;
            }
        }

        // ==== 5. Strip baked lightmaps + kill emissive glow ====
        foreach (var rend in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (rend == null || safe.Contains(rend.transform)) continue;

            if (rend.lightmapIndex >= 0)
            {
                _savedLightmaps.Add(new SavedLightmapInfo
                    { rend = rend, idx = rend.lightmapIndex, so = rend.lightmapScaleOffset });
                rend.lightmapIndex = -1;
            }

            bool hasEmission = false;
            foreach (var sm in rend.sharedMaterials)
                if (sm != null && sm.HasProperty("_EmissionColor") &&
                    sm.GetColor("_EmissionColor").maxColorComponent > 0.01f)
                    { hasEmission = true; break; }
            if (!hasEmission) continue;

            var mats = rend.materials;
            foreach (var mat in mats)
            {
                if (mat == null || !mat.HasProperty("_EmissionColor")) continue;
                Color orig = mat.GetColor("_EmissionColor");
                if (orig.maxColorComponent <= 0.01f) continue;
                _savedEmissives.Add(new SavedEmissive { mat = mat, prop = "_EmissionColor", color = orig });
                mat.SetColor("_EmissionColor", Color.black);
            }
            rend.materials = mats;
        }

        // ==== 6. Make the sm_lights lamp heads GLOW (visual emissive) ====
        MakeSmLightsBulbsGlow();

        // ==== 7. Activate LD spot/point lights ====
        EnableOperatingSpotlights();

        // ==== 8. Create the main operating spotlight (bright cone onto the bed) ====
        CreateOperatingSpotFromSmLights();

        DynamicGI.UpdateEnvironment();

        // ==== Golden baseline capture (first entry only) ====
        if (!_goldenCaptured)
        {
            _goldenLights.Clear();
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light == null) continue;
                _goldenLights.Add(new GoldenLightState
                {
                    name = light.name,
                    type = light.type,
                    intensity = light.intensity,
                    enabled = light.enabled
                });
            }
            _goldenCaptured = true;
            Debug.Log($"[LiveDissection] Golden baseline captured: {_goldenLights.Count} lights.");
        }

        Debug.Log($"[LiveDissection] Dark room ON — " +
                  $"lights dimmed: {_savedLights.Count}, " +
                  $"lightmaps stripped: {_savedLightmaps.Count}, " +
                  $"emissions killed: {_savedEmissives.Count}, " +
                  $"bulbs glowing: {_savedBulbEmissions.Count}, " +
                  $"LD lights: {_dynamicSpotlights.Count}.");
    }

    void ForceURPPerPixelLights()
    {
        _urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (_urpAsset == null) return;

        var assetType = _urpAsset.GetType();
        var modeField = assetType.GetField("m_AdditionalLightsRenderingMode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var limitField = assetType.GetField("m_AdditionalLightsPerObjectLimit",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (modeField != null)
        {
            // Always restore from the true originals, not the current (possibly stale) value
            _savedURPLightMode = _trueOriginalsCaputred ? _trueOriginalURPMode : (int)modeField.GetValue(_urpAsset);
            modeField.SetValue(_urpAsset, 2);  // 2 = PerPixel
            Debug.Log($"[LiveDissection] URP additional lights: {_savedURPLightMode} -> 2 (PerPixel)");
        }
        if (limitField != null)
        {
            _savedURPLightLimit = _trueOriginalsCaputred ? _trueOriginalURPLimit : (int)limitField.GetValue(_urpAsset);
            limitField.SetValue(_urpAsset, 8);
            Debug.Log($"[LiveDissection] URP light limit: {_savedURPLightLimit} -> 8");
        }
    }

    void RestoreURPLights()
    {
        if (_urpAsset == null) return;
        var assetType = _urpAsset.GetType();

        if (_savedURPLightMode >= 0)
        {
            var modeField = assetType.GetField("m_AdditionalLightsRenderingMode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (modeField != null)
                modeField.SetValue(_urpAsset, _savedURPLightMode);
            _savedURPLightMode = -1;
        }
        if (_savedURPLightLimit >= 0)
        {
            var limitField = assetType.GetField("m_AdditionalLightsPerObjectLimit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (limitField != null)
                limitField.SetValue(_urpAsset, _savedURPLightLimit);
            _savedURPLightLimit = -1;
        }
        _urpAsset = null;
        Debug.Log("[LiveDissection] URP restored to true originals.");
    }

    void CreateOperatingSpotFromSmLights()
    {
        GameObject smLightsGO = FindSmLights();
        if (smLightsGO == null) return;

        Vector3 lightPos = smLightsGO.transform.position;

        Vector3 targetPos;
        if (_patientDummy != null)
            targetPos = _patientDummy.transform.position;
        else
            targetPos = lightPos + Vector3.down * 3f;

        Vector3 dir = (targetPos - lightPos).normalized;

        var mainSpot = new GameObject("LD_RuntimeSpot_Main");
        mainSpot.transform.position = lightPos;
        mainSpot.transform.rotation = Quaternion.LookRotation(dir);
        var mainLight = mainSpot.AddComponent<Light>();
        mainLight.type           = LightType.Spot;
        mainLight.color          = new Color(0.93f, 0.95f, 1f);
        mainLight.intensity      = LiveDissectionRuntimeSpotIntensity;
        mainLight.range          = 8f;
        mainLight.spotAngle      = 70f;
        mainLight.innerSpotAngle = 35f;
        mainLight.shadows        = LightShadows.Soft;
        mainLight.shadowStrength = 0.5f;
        mainLight.renderMode     = LightRenderMode.ForcePixel;
        mainLight.enabled        = true;
        _dynamicSpotlights.Add(mainSpot);

        Debug.Log($"[LiveDissection] Runtime spot at {lightPos} aimed at {targetPos}, 150cd/70°.");
    }

    void MakeSmLightsBulbsGlow()
    {
        GameObject smLights = FindSmLights();
        if (smLights == null) return;

        Color glowColor = new Color(1.5f, 1.45f, 1.2f);  // mild HDR warm glow

        int boosted = 0;
        foreach (var rend in smLights.GetComponentsInChildren<Renderer>(true))
        {
            if (rend == null) continue;
            var mats = rend.materials;
            bool changed = false;
            foreach (var mat in mats)
            {
                if (mat == null || !mat.HasProperty("_EmissionColor")) continue;

                Color orig = mat.GetColor("_EmissionColor");

                // Only boost materials that ALREADY have emission (bulbs/lenses).
                // Frame/arm materials have zero emission and are left untouched.
                if (orig.maxColorComponent < 0.01f) continue;

                _savedBulbEmissions.Add(new SavedBulbEmission { mat = mat, original = orig });
                mat.SetColor("_EmissionColor", glowColor);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                changed = true;
                boosted++;
            }
            if (changed) rend.materials = mats;
        }
        Debug.Log($"[LiveDissection] sm_lights: {boosted} bulb materials glowing (skipped non-emissive frame parts).");
    }

    void EnableOperatingSpotlights()
    {
        _dynamicSpotlights.Clear();

        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null) continue;
            if (!t.name.StartsWith("LD_OperatingLight")) continue;

            // Ensure the entire parent chain is active (SetActive(true) on a child
            // has no visual effect if any ancestor is inactive).
            Transform walk = t.parent;
            while (walk != null)
            {
                if (!walk.gameObject.activeSelf)
                    walk.gameObject.SetActive(true);
                walk = walk.parent;
            }

            t.gameObject.SetActive(true);
            _dynamicSpotlights.Add(t.gameObject);

            var light = t.GetComponent<Light>();
            if (light != null)
            {
                if (light.name.IndexOf("Fill", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // The fill point light floods the whole room and is the main source of the
                    // unrealistic washed-out look. Live Dissection should rely on the focused
                    // operating spots only.
                    light.enabled = false;
                    continue;
                }
                else
                {
                    light.enabled = true;
                    if (light.intensity > LiveDissectionExtraSpotIntensity)
                        light.intensity = LiveDissectionExtraSpotIntensity;
                    if (light.type == LightType.Spot)
                    {
                        light.range = LiveDissectionSpotRange;
                        light.spotAngle = LiveDissectionSpotAngle;
                        light.innerSpotAngle = LiveDissectionInnerSpotAngle;
                    }
                }
                Debug.Log($"[LiveDissection] LD light ON: {t.name} " +
                          $"type={light.type} intensity={light.intensity} pos={t.position}");
            }
        }

        if (_dynamicSpotlights.Count == 0)
            Debug.LogWarning("[LiveDissection] No LD_OperatingLight objects found! " +
                             "Run Tools > Brain Dissection > Setup Operating Lights in the Editor.");
        else
            Debug.Log($"[LiveDissection] {_dynamicSpotlights.Count} LD lights activated.");
    }

    void DisableOperatingSpotlights()
    {
        foreach (var go in _dynamicSpotlights)
        {
            if (go == null) continue;
            // Runtime-created spots get destroyed; editor-placed ones just deactivate
            if (go.name.StartsWith("LD_Runtime"))
                Destroy(go);
            else
                go.SetActive(false);
        }
        _dynamicSpotlights.Clear();
    }

    GameObject FindSmLights()
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t != null && t.name.Equals("sm_lights", System.StringComparison.OrdinalIgnoreCase))
                return t.gameObject;
        }
        return null;
    }

    void RestoreSceneLighting()
    {
        DisableOperatingSpotlights();

        RestoreURPLights();

        foreach (var sl in _savedLights)
        {
            if (sl.light == null) continue;
            sl.light.enabled = sl.wasEnabled;
            sl.light.intensity = sl.intensity;
        }
        _savedLights.Clear();

        foreach (var se in _savedEmissives)
        {
            if (se.mat != null && !string.IsNullOrEmpty(se.prop))
                se.mat.SetColor(se.prop, se.color);
        }
        _savedEmissives.Clear();

        // Restore sm_lights bulb emission
        foreach (var be in _savedBulbEmissions)
        {
            if (be.mat != null)
                be.mat.SetColor("_EmissionColor", be.original);
        }
        _savedBulbEmissions.Clear();

        foreach (var lm in _savedLightmaps)
        {
            if (lm.rend == null) continue;
            lm.rend.lightmapIndex = lm.idx;
            lm.rend.lightmapScaleOffset = lm.so;
        }
        _savedLightmaps.Clear();

        RenderSettings.ambientMode = _savedAmbientMode;
        RenderSettings.ambientLight = _savedAmbientLight;
        RenderSettings.ambientIntensity = _savedAmbientIntensity;
        RenderSettings.reflectionIntensity = _savedReflectionIntensity;

        DynamicGI.UpdateEnvironment();

        // Restore the current user's brightness and contrast settings.
        if (_optCtrl != null)
            _optCtrl.ReloadForCurrentUser();

        Debug.Log("[LiveDissection] Scene lighting restored.");
    }

    // ========================= COLLIDERS =========================

    void DisableNonCandidateColliders()
    {
        foreach (var r in _allRegions)
        {
            if (r == null) continue;
            bool isCandidate = _candidateRegions.Contains(r);
            foreach (var col in r.GetComponentsInChildren<Collider>(true))
                if (col != null) col.enabled = isCandidate;
        }
    }

    void EnableAllColliders()
    {
        foreach (var r in _allRegions)
        {
            if (r == null) continue;
            foreach (var col in r.GetComponentsInChildren<Collider>(true))
                if (col != null) col.enabled = true;
        }
    }

    // ========================= OVERLAY =========================

    void CreateOverlay(BrainRegion region)
    {
        Mesh mesh = null;
        var mf = region.GetComponent<MeshFilter>();
        if (mf != null) mesh = mf.sharedMesh;
        if (mesh == null)
        {
            var smr = region.GetComponent<SkinnedMeshRenderer>();
            if (smr != null) mesh = smr.sharedMesh;
        }
        if (mesh == null) return;

        var go = new GameObject("HoloCandidateOverlay");
        go.transform.SetParent(region.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;   // flush with the region surface

        var overlayMF = go.AddComponent<MeshFilter>();
        overlayMF.sharedMesh = mesh;
        var rend = go.AddComponent<MeshRenderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var mat = new Material(sh);
        mat.SetFloat("_Surface", 1);  // transparent
        mat.SetFloat("_Blend", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3100;
        mat.color = OverlayNormal;

        // Emissive glow so the overlay is visible even in dark lighting
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.05f, 0.01f, 0.01f));
        }
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", OverlayNormal);

        rend.material = mat;

        _overlays.Add(go);
        _regionOverlayMap[region] = go;
        _pulseCoroutines.Add(StartCoroutine(PulseOverlayIdle(rend)));
    }

    IEnumerator PulseOverlayIdle(Renderer rend)
    {
        float offset = Random.Range(0f, Mathf.PI * 2f);
        while (rend != null)
        {
            float t = Mathf.Sin(Time.time * 1.8f + offset) * 0.5f + 0.5f;
            float a = Mathf.Lerp(0.35f, 0.6f, t);
            Color c = rend.material.color;
            c.a = a;
            rend.material.color = c;
            if (rend.material.HasProperty("_BaseColor"))
            {
                Color bc = rend.material.GetColor("_BaseColor");
                bc.a = a;
                rend.material.SetColor("_BaseColor", bc);
            }
            yield return null;
        }
    }

    void ClearOverlays()
    {
        if (_rayHoveredRegion != null)
            OnRegionUnhovered(_rayHoveredRegion);
        _rayHoveredRegion = null;
        foreach (var co in _pulseCoroutines)
            if (co != null) StopCoroutine(co);
        _pulseCoroutines.Clear();
        foreach (var o in _overlays)
            if (o != null) Destroy(o);
        _overlays.Clear();
        _regionOverlayMap.Clear();
        _savedRegionColors.Clear();
        _savedRegionEmission.Clear();
    }

    void OnRegionHovered(BrainRegion region)
    {
        if (!_active || !_candidateRegions.Contains(region)) return;

        foreach (var rend in region.GetComponentsInChildren<Renderer>(true))
        {
            if (rend == null || !rend.enabled) continue;
            if (rend.gameObject.name.StartsWith("HoloCandid")) continue;

            var mat = rend.material;
            if (!_savedRegionColors.ContainsKey(rend))
            {
                _savedRegionColors[rend] = mat.HasProperty("_BaseColor")
                    ? mat.GetColor("_BaseColor") : mat.color;
            }
            if (!_savedRegionEmission.ContainsKey(rend) && mat.HasProperty("_EmissionColor"))
            {
                _savedRegionEmission[rend] = mat.GetColor("_EmissionColor");
            }

            Color hoverTint = new Color(1f, 0.85f, 0.1f, 1f);
            mat.color = hoverTint;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", hoverTint);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1.5f, 1.1f, 0.15f));
            }
        }

        if (region.regionData != null && WorldSpaceHoverLabel.Instance != null)
            WorldSpaceHoverLabel.Instance.Show(region.regionData.displayName, region.transform);
    }

    void OnRegionUnhovered(BrainRegion region)
    {
        if (!_active) return;

        foreach (var rend in region.GetComponentsInChildren<Renderer>(true))
        {
            if (rend == null) continue;
            if (rend.gameObject.name.StartsWith("HoloCandid")) continue;

            var mat = rend.material;
            if (_savedRegionColors.TryGetValue(rend, out Color origColor))
            {
                mat.color = origColor;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", origColor);
            }
            if (_savedRegionEmission.TryGetValue(rend, out Color origEmission))
            {
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", origEmission);
            }
        }

        if (WorldSpaceHoverLabel.Instance != null)
            WorldSpaceHoverLabel.Instance.Hide();
    }

    // ========================= TOOL MANAGEMENT =========================

    void SetupToolsForDissection()
    {
        if (LabToolManager.Instance != null && !LabToolManager.Instance.glovesEquipped)
            LabToolManager.Instance.EquipGloves();

        var tools = FindObjectsByType<LabTool>(FindObjectsSortMode.None);
        foreach (var tool in tools)
        {
            if (tool.toolType == LabTool.ToolType.Knife)
            {
                _hiddenKnife = tool.gameObject;
                _hiddenKnife.SetActive(false);
                break;
            }
        }

        if (LabToolManager.Instance != null)
        {
            LabToolManager.Instance.isHoldingTweezers = true;
            LabToolManager.Instance.brainIsSplit = true;
        }
    }

    void RestoreTools()
    {
        if (_hiddenKnife != null) { _hiddenKnife.SetActive(true); _hiddenKnife = null; }
        if (LabToolManager.Instance != null)
        {
            LabToolManager.Instance.isHoldingTweezers = false;
            LabToolManager.Instance.brainIsSplit = false;
        }
    }

    // ========================= SCENE OBJECTS =========================

    void ActivateSceneObjects()
    {
        _brainRoot = GameObject.Find("BrainRoot");
        if (_brainRoot != null) { _brainRoot.SetActive(false); }

        _patientDummy = FindSceneObject("DummyPatient");
        if (_patientDummy != null) _patientDummy.SetActive(true);

        _dissectionBrain = FindSceneObject("Dissection_Brain");
        if (_dissectionBrain == null) _dissectionBrain = FindSceneObject("dissection_brain");
        if (_dissectionBrain != null) _dissectionBrain.SetActive(true);
        else Debug.LogWarning("[LiveDissection] Dissection_Brain not found.");
    }

    void DeactivateSceneObjects()
    {
        if (_patientDummy != null) _patientDummy.SetActive(false);
        if (_dissectionBrain != null) _dissectionBrain.SetActive(false);
        if (_brainRoot != null) _brainRoot.SetActive(true);
    }

    GameObject FindSceneObject(string objectName)
    {
        var go = GameObject.Find(objectName);
        if (go != null) return go;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.name == objectName) return t.gameObject;
        return null;
    }

    void CollectAllRegions()
    {
        _allRegions.Clear();
        if (_dissectionBrain != null)
            _allRegions.AddRange(_dissectionBrain.GetComponentsInChildren<BrainRegion>(true));
    }

    // ========================= RESULTS =========================

    void ShowResults()
    {
        _waitingForSelection = false;
        ClearOverlays();
        EnableAllColliders();

        // Freeze elapsed seconds before deactivating so the recorded time
        // reflects the run, not the time spent on the results screen.
        _ldElapsedFrozenSeconds = Mathf.Max(1, Mathf.FloorToInt(Time.time - _ldStartTime));
        _active = false;

        if (_scenarioText != null) _scenarioText.gameObject.SetActive(false);
        _feedbackPanel.SetActive(false);
        _continueBtn.SetActive(false);
        if (_hintBtn != null) _hintBtn.SetActive(false);
        _finishPanel.SetActive(true);

        float pct = _maxPossibleScore > 0 ? (float)_totalScore / _maxPossibleScore * 100f : 0;
        string grade = pct >= 90 ? "A" : pct >= 80 ? "B" : pct >= 70 ? "C" : pct >= 60 ? "D" : "F";
        string timeStr = LeaderboardManager.FormatElapsed(_ldElapsedFrozenSeconds);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("LIVE DISSECTION COMPLETE\n");
        sb.AppendLine($"Score: {_totalScore} / {_maxPossibleScore}  ({pct:F0}%)    Grade: {grade}");
        sb.AppendLine($"Time: {timeStr}\n");
        for (int i = 0; i < _pointsPerCase.Count && i < _cases.Count; i++)
        {
            string kw = _cases[i].correctRegionKeyword;
            sb.AppendLine($"  Case {i + 1} ({kw}): {_pointsPerCase[i]} pts, {_attemptsPerCase[i]} attempts");
        }
        sb.AppendLine($"\nTotal Attempts: {_totalAttempts}");
        if (_bestStreak >= 2)
            sb.AppendLine($"Best Streak: {_bestStreak}x");

        var txt = _finishPanel.GetComponentInChildren<Text>();
        if (txt != null) txt.text = sb.ToString();

        if (_timerText != null)
            _timerText.text = $"Time: {timeStr}";

        LeaderboardManager.RecordScore(SessionData.UserName, _totalScore, _maxPossibleScore,
            "LiveDissection", _ldElapsedFrozenSeconds);

        if (AchievementManager.Instance != null)
        {
            int totalWrong = _totalAttempts - _cases.Count;
            AchievementManager.Instance.CheckLDScore(_totalScore, _maxPossibleScore, totalWrong);
            AchievementManager.Instance.CheckStreak(_bestStreak);
        }
    }

    void ShowReviewPanel()
    {
        if (_caseReviews.Count == 0) return;
        _reviewPage = 0;
        _finishPanel.SetActive(false);
        _reviewPanel.SetActive(true);
        UpdateReviewPage();
    }

    void HideReviewPanel()
    {
        _reviewPanel.SetActive(false);
        _finishPanel.SetActive(true);
    }

    void ReviewPrev()
    {
        if (_caseReviews.Count == 0) return;
        _reviewPage = (_reviewPage - 1 + _caseReviews.Count) % _caseReviews.Count;
        UpdateReviewPage();
    }

    void ReviewNext()
    {
        if (_caseReviews.Count == 0) return;
        _reviewPage = (_reviewPage + 1) % _caseReviews.Count;
        UpdateReviewPage();
    }

    void UpdateReviewPage()
    {
        if (_reviewText == null || _reviewPage < 0 || _reviewPage >= _caseReviews.Count) return;
        var r = _caseReviews[_reviewPage];
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Case {_reviewPage + 1} / {_caseReviews.Count}\n");
        sb.AppendLine(r.scenario);
        sb.AppendLine();
        sb.AppendLine($"Correct Region:  {r.correctRegion}");
        if (!string.IsNullOrEmpty(r.selectedWrongRegion))
            sb.AppendLine($"You Selected:  {r.selectedWrongRegion}");
        sb.AppendLine($"Points: {r.points}  |  Attempts: {r.attempts}");
        sb.AppendLine();
        sb.AppendLine(r.explanation);
        _reviewText.text = sb.ToString();
    }

    // ========================= SHUFFLE =========================

    void ShuffleCases()
    {
        for (int i = _cases.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = _cases[i]; _cases[i] = _cases[j]; _cases[j] = tmp;
        }
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    // ========================= UI =========================

    void BuildUI()
    {
        if (_canvas != null) Destroy(_canvas);

        var cam = Camera.main;
        _canvas = new GameObject("LiveDissectionCanvas");
        if (cam != null)
        {
            _canvas.transform.SetParent(cam.transform, false);
            _canvas.transform.localPosition = new Vector3(0f, 0.22f, 1.0f);
            _canvas.transform.localRotation = Quaternion.identity;
        }

        var canvas = _canvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _canvas.AddComponent<CanvasScaler>();
        _canvas.AddComponent<TrackedDeviceGraphicRaycaster>();

        var rt = _canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(850, 280);
        rt.localScale = Vector3.one * 0.0008f;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var bg = MakeRect("Bg", _canvas.transform, Vector2.zero, rt.sizeDelta);
        bg.gameObject.AddComponent<Image>().color = PanelBg;

        _progressText = MakeText("Progress", bg.transform, new Vector2(-160, 118), new Vector2(420, 28),
            "", 19, FontStyle.Bold, TextDim, TextAnchor.MiddleLeft, font);

        _timerText = MakeText("Timer", bg.transform, new Vector2(280, 118), new Vector2(180, 28),
            "Time: 0:00", 18, FontStyle.Bold, new Color(0.45f, 0.85f, 1f), TextAnchor.MiddleRight, font);

        _streakText = MakeText("StreakText", bg.transform, new Vector2(0, 140), new Vector2(750, 24),
            "", 18, FontStyle.Bold, new Color(1f, 0.85f, 0.2f), TextAnchor.MiddleCenter, font);

        _scenarioText = MakeText("Scenario", bg.transform, new Vector2(0, 35), new Vector2(800, 110),
            "", 21, FontStyle.Normal, TextWhite, TextAnchor.MiddleCenter, font);

        _feedbackPanel = MakeRect("Feedback", bg.transform, new Vector2(0, -50), new Vector2(800, 50)).gameObject;
        _feedbackPanel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.14f, 0.92f);
        _feedbackText = MakeText("FBText", _feedbackPanel.transform, Vector2.zero, new Vector2(780, 45),
            "", 18, FontStyle.Italic, TextDim, TextAnchor.MiddleCenter, font);
        _feedbackPanel.SetActive(false);

        _continueBtn = MakeButton("ContinueBtn", bg.transform, new Vector2(-110, -105), new Vector2(200, 38),
            "Continue", BtnGreen, font);
        _continueBtn.GetComponent<Button>().onClick.AddListener(OnContinuePressed);
        _continueBtn.SetActive(false);

        _hintBtn = MakeButton("HintBtn", bg.transform, new Vector2(110, -105), new Vector2(200, 38),
            "Show Hint (-1pt)", BtnYellow, font);
        _hintBtn.GetComponent<Button>().onClick.AddListener(OnHintPressed);
        _hintBtn.SetActive(false);

        _finishPanel = MakeRect("FinishPanel", bg.transform, Vector2.zero, new Vector2(800, 260)).gameObject;
        _finishPanel.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.12f, 0.97f);
        MakeText("FinishText", _finishPanel.transform, new Vector2(0, 25), new Vector2(750, 200),
            "", 18, FontStyle.Normal, TextWhite, TextAnchor.MiddleCenter, font);
        var reviewBtnGO = MakeButton("ReviewBtn", _finishPanel.transform, new Vector2(-150, -110), new Vector2(220, 42),
            "Review Cases", new Color(0.2f, 0.45f, 0.8f), font);
        reviewBtnGO.GetComponent<Button>().onClick.AddListener(ShowReviewPanel);
        var doneGO = MakeButton("DoneBtn", _finishPanel.transform, new Vector2(150, -110), new Vector2(220, 42),
            "Return to Assessment", BtnOrange, font);
        doneGO.GetComponent<Button>().onClick.AddListener(EndLiveDissection);
        _finishPanel.SetActive(false);

        _reviewPanel = MakeRect("ReviewPanel", bg.transform, Vector2.zero, new Vector2(800, 260)).gameObject;
        _reviewPanel.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.12f, 0.97f);
        _reviewText = MakeText("ReviewText", _reviewPanel.transform, new Vector2(0, 25), new Vector2(750, 200),
            "", 17, FontStyle.Normal, TextWhite, TextAnchor.MiddleCenter, font);
        var prevBtnGO = MakeButton("PrevBtn", _reviewPanel.transform, new Vector2(-250, -110), new Vector2(140, 38),
            "< Prev", TextDim, font);
        prevBtnGO.GetComponent<Button>().onClick.AddListener(ReviewPrev);
        var nextBtnGO = MakeButton("NextBtn", _reviewPanel.transform, new Vector2(-100, -110), new Vector2(140, 38),
            "Next >", TextDim, font);
        nextBtnGO.GetComponent<Button>().onClick.AddListener(ReviewNext);
        var backBtnGO = MakeButton("BackToResults", _reviewPanel.transform, new Vector2(150, -110), new Vector2(220, 38),
            "Back to Results", BtnOrange, font);
        backBtnGO.GetComponent<Button>().onClick.AddListener(HideReviewPanel);
        _reviewPanel.SetActive(false);
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

    static GameObject MakeButton(string name, Transform parent, Vector2 pos, Vector2 size,
        string label, Color bg, Font font)
    {
        var rt = MakeRect(name, parent, pos, size);
        var go = rt.gameObject;
        go.AddComponent<Image>().color = bg;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        var colors = btn.colors;
        colors.normalColor = bg; colors.highlightedColor = bg * 1.2f; colors.pressedColor = bg * 0.8f;
        btn.colors = colors;

        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(go.transform, false);
        var lblRT = lblGO.AddComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;
        var txt = lblGO.AddComponent<Text>();
        txt.text = label; txt.fontSize = 18; txt.fontStyle = FontStyle.Bold;
        txt.color = TextWhite; txt.alignment = TextAnchor.MiddleCenter; txt.font = font;
        return go;
    }
}
