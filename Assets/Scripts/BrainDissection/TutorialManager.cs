using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Step-based VR tutorial with locked progression, TTS narration,
/// hemisphere-aware random region selection, and a fixed-size red
/// sphere highlight on the target region.
///
/// Slim banner at the TOP of the camera view. The main Brain
/// Dissection panel stays fully visible below so students learn
/// the actual interface while being guided.
///
/// TUTORIAL-ONLY: nothing here modifies the normal Play flow.
///
/// Interaction restriction: during step 6 (select region), only the
/// highlighted region can be extracted. All other regions allow
/// hover/label but block extraction. BrainRegion.OnSelectEntered
/// checks TutorialManager.AllowedRegion to enforce this.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Settings")]
    public float typingSpeed = 0.02f;
    public int speechRate = 1;

    public bool IsTutorialActive => _active;
    public int CurrentStep => _step;

    /// <summary>
    /// During tutorial, only this region may be extracted.
    /// Returns null when tutorial is inactive or not on the region step.
    /// BrainRegion.OnSelectEntered checks this to block non-target extraction.
    /// </summary>
    public BrainRegion AllowedRegion => (_active && _highlighted != null) ? _highlighted : null;

    // Banner UI
    GameObject _banner;
    Text _stepLabel;
    Text _instrText;
    Text _hintText;
    Text _progressText;
    GameObject _returnBtn;

    Coroutine _pulseRoutine;
    GameObject _overlayGO;
    GameObject _outlineGO;
    SphereCollider _easyHitCollider;

    bool _active;
    int _step;
    const int TOTAL = 9;
    // Step indices (1-based) so the rest of the file reads naturally.
    const int STEP_WELCOME    = 1;
    const int STEP_WASH       = 2;
    const int STEP_GLOVES     = 3;
    const int STEP_KNIFE      = 4;
    const int STEP_CUT        = 5;
    const int STEP_HEMI_TWZ   = 6;
    const int STEP_SELECT     = 7;
    const int STEP_PUT_BACK   = 8;
    const int STEP_COMPLETE   = 9;
    bool _waiting;
    bool _narrationComplete;
    bool _tutorialTriggerHeld;
    Coroutine _typeRoutine;
    BrainRegion _highlighted;
    AnatomyDepthPreset _highlightedPreset;
    bool _hasHighlightedPreset;
    string _highlightedHemisphere;
    List<BrainRegion> _allRegions = new List<BrainRegion>();
    BrainManager _cachedBrainManager;

    // Multi-region loop
    const int REGIONS_PER_RUN = 3;
    int _regionIndex;
    List<BrainRegion> _recentlyUsedRegions = new List<BrainRegion>();
    List<AnatomyDepthPreset> _recentlyUsedPresets = new List<AnatomyDepthPreset>();

    // Auto-opacity for deep targets (tutorial only)
    FloatingInfoPanel _floatingHud;
    bool _floatingHudTweaked;
    float _savedHudFollowDistance;
    float _savedHudVerticalAngle;

    // Skip narration button
    GameObject _skipBtn;

    // Visual guide arrow
    GameObject _guideArrow;
    Transform _guideArrowTarget;
    static readonly string[] ToolSearchNames = new[]
    {
        "LabGloves", "MedicalGloves",
        "LabKnife", "SurgicalKnife",
        "LabTweezers", "tweezers"
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        TextToSpeech.Stop();
        ClearHighlight();
        ClearArrow();
    }

    // ========================= PUBLIC API =========================

    public void BeginTutorial()
    {
        _active = true;
        _step = 0;
        _waiting = false;
        _regionIndex = 0;
        _tutorialTriggerHeld = false;
        _recentlyUsedRegions.Clear();
        _recentlyUsedPresets.Clear();
        _hasHighlightedPreset = false;

        _allRegions.Clear();
        _allRegions.AddRange(FindObjectsByType<BrainRegion>(
            FindObjectsInactive.Include, FindObjectsSortMode.None));

        if (LabToolManager.Instance != null)
            LabToolManager.Instance.ResetLab();

        ApplyTutorialHudSpacing();

        BuildBanner();
        _banner.SetActive(true);
        if (_returnBtn != null) _returnBtn.SetActive(false);

        UpdateProgress();
        AdvanceStep();
        Debug.Log("[Tutorial] Started.");
    }

    public void EndTutorial()
    {
        _active = false;
        _waiting = false;
        _tutorialTriggerHeld = false;
        TextToSpeech.Stop();
        ClearArrow();

        ClearHighlight();
        _highlighted = null;
        _hasHighlightedPreset = false;

        EnableAllColliders();
        RestoreFloatingHudSpacing();

        if (_banner != null) _banner.SetActive(false);
        Debug.Log("[Tutorial] Ended.");
    }

    // ========================= STEP MONITORING =========================

    void Update()
    {
        if (_guideArrow != null && _guideArrowTarget != null)
        {
            PositionArrow();
            float bob = Mathf.Sin(Time.time * 3f) * 0.04f;
            _guideArrow.transform.position += Vector3.up * bob;
        }

        if (!_active || !_waiting) return;

        if (TryCatchUpTutorialState())
            return;

        if (!_narrationComplete)
        {
            if (_hintText != null && !_hintText.text.StartsWith("\u266A"))
                _hintText.text = "\u266A  Listening...";
            return;
        }

        var ltm = LabToolManager.Instance;
        if (_cachedBrainManager == null) _cachedBrainManager = FindFirstObjectByType<BrainManager>();
        var bm = _cachedBrainManager;

        if (_step == STEP_SELECT)
            PollTutorialRegionSelectionFallback(bm);

        switch (_step)
        {
            case STEP_WASH:
                if (ltm != null && ltm.handsWashed)
                    CompleteAndAdvance();
                break;
            case STEP_GLOVES:
                if (ltm != null && ltm.glovesEquipped)
                    CompleteAndAdvance();
                break;
            case STEP_KNIFE:
                if (ltm != null && ltm.isHoldingKnife)
                    CompleteAndAdvance();
                break;
            case STEP_CUT:
                if (ltm != null && ltm.brainIsSplit)
                    CompleteAndAdvance();
                break;
            case STEP_HEMI_TWZ:
                if (ltm != null && ltm.isHoldingTweezers &&
                    bm != null && bm.IsHemisphereSelected)
                {
                    PickAndHighlightRegion(bm);
                    CompleteAndAdvance();
                }
                break;
            case STEP_SELECT:
                if (bm != null && bm.IsInspectingRegion)
                    CompleteAndAdvance();
                break;
            case STEP_PUT_BACK:
                if (bm != null && !bm.IsInspectingRegion)
                    CompleteAndAdvance();
                break;
        }
    }

    bool TryCatchUpTutorialState()
    {
        var ltm = LabToolManager.Instance;
        if (_cachedBrainManager == null) _cachedBrainManager = FindFirstObjectByType<BrainManager>();
        var bm = _cachedBrainManager;

        int targetStep = _step;

        if (ltm != null && ltm.handsWashed)
            targetStep = Mathf.Max(targetStep, STEP_GLOVES);
        if (ltm != null && ltm.glovesEquipped)
            targetStep = Mathf.Max(targetStep, STEP_KNIFE);
        if (ltm != null && ltm.isHoldingKnife)
            targetStep = Mathf.Max(targetStep, STEP_CUT);
        if (ltm != null && ltm.brainIsSplit)
            targetStep = Mathf.Max(targetStep, STEP_HEMI_TWZ);
        if (ltm != null && ltm.isHoldingTweezers && bm != null && bm.IsHemisphereSelected)
            targetStep = Mathf.Max(targetStep, STEP_SELECT);
        if (bm != null && bm.IsInspectingRegion)
            targetStep = Mathf.Max(targetStep, STEP_PUT_BACK);
        if (bm != null && !bm.IsInspectingRegion && _step == STEP_PUT_BACK)
            targetStep = Mathf.Max(targetStep, STEP_COMPLETE);

        if (targetStep <= _step)
            return false;

        _step = targetStep - 1;
        TextToSpeech.Stop();
        if (_typeRoutine != null)
        {
            StopCoroutine(_typeRoutine);
            _typeRoutine = null;
        }
        AdvanceStep();
        return true;
    }

    void CompleteAndAdvance()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlayStepComplete();
        StartCoroutine(FlashCompletion());
        AdvanceStep();
    }

    IEnumerator FlashCompletion()
    {
        if (_hintText == null) yield break;
        var orig = _hintText.color;
        _hintText.color = new Color(0.2f, 1f, 0.3f);
        _hintText.text = "\u2713  Step complete!";
        yield return new WaitForSeconds(0.6f);
        _hintText.color = orig;
    }

    // ========================= STEP LOGIC =========================

    void AdvanceStep()
    {
        // After put-back, loop back to select-region for the next target if more remain
        if (_step == STEP_PUT_BACK && _regionIndex < REGIONS_PER_RUN - 1)
        {
            _regionIndex++;
            _step = STEP_HEMI_TWZ; // will be incremented to STEP_SELECT below
        }

        _step++;
        _waiting = false;
        ClearArrow();

        if (_step > TOTAL) { EndTutorial(); return; }

        string stepDisplay = _step < STEP_SELECT
            ? $"TUTORIAL  \u2014  Step {_step} of {TOTAL}"
            : _step < STEP_COMPLETE
                ? $"TUTORIAL  \u2014  Step {_step} of {TOTAL}  |  Region {_regionIndex + 1} of {REGIONS_PER_RUN}"
                : $"TUTORIAL  \u2014  Complete!";
        if (_stepLabel != null)
            _stepLabel.text = stepDisplay;

        UpdateProgress();

        switch (_step)
        {
            case STEP_WELCOME:  StepWelcome(); break;
            case STEP_WASH:     StepWashHands(); break;
            case STEP_GLOVES:   StepGloves(); break;
            case STEP_KNIFE:    StepKnife(); break;
            case STEP_CUT:      StepCutBrain(); break;
            case STEP_HEMI_TWZ: StepHemisphereTweezers(); break;
            case STEP_SELECT:   StepSelectRegion(); break;
            case STEP_PUT_BACK: StepPutBack(); break;
            case STEP_COMPLETE: StepComplete(); break;
        }
    }

    // ========================= GUIDE ARROW =========================

    Transform FindToolByName(params string[] names)
    {
        foreach (var name in names)
        {
            var go = GameObject.Find(name);
            if (go != null) return go.transform;
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t != null && t.name == name) return t;
        }
        return null;
    }

    void SpawnArrowTo(Transform target)
    {
        ClearArrow();
        if (target == null) return;
        _guideArrowTarget = target;

        _guideArrow = new GameObject("TutorialGuideArrow");

        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.transform.SetParent(_guideArrow.transform, false);
        body.transform.localScale = new Vector3(0.03f, 0.12f, 0.03f);
        body.transform.localPosition = Vector3.zero;
        var bodyCol = body.GetComponent<Collider>();
        if (bodyCol != null) Destroy(bodyCol);

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.transform.SetParent(_guideArrow.transform, false);
        head.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
        head.transform.localPosition = new Vector3(0, -0.14f, 0);
        var headCol = head.GetComponent<Collider>();
        if (headCol != null) Destroy(headCol);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.1f, 0.9f, 0.3f);
        mat.SetColor("_EmissionColor", new Color(0.1f, 0.9f, 0.3f) * 1.5f);
        mat.EnableKeyword("_EMISSION");
        body.GetComponent<Renderer>().material = mat;
        head.GetComponent<Renderer>().material = mat;

        PositionArrow();
    }

    void PositionArrow()
    {
        if (_guideArrow == null || _guideArrowTarget == null) return;
        Vector3 targetPos = _guideArrowTarget.position;
        _guideArrow.transform.position = targetPos + Vector3.up * 0.35f;
        _guideArrow.transform.LookAt(targetPos);
        _guideArrow.transform.Rotate(90f, 0f, 0f);
    }

    void ClearArrow()
    {
        if (_guideArrow != null) Destroy(_guideArrow);
        _guideArrow = null;
        _guideArrowTarget = null;
    }

    // ========================= STEPS =========================

    void StepWelcome()
    {
        Show(
            "Welcome to the Brain Dissection Lab. " +
            "We will wash up, equip tools, split the brain, and examine a few regions. " +
            "Tip: hold A or B on the right controller (or X or Y on the left) to open the floating panel.",
            "Listen, then we will begin");
        _waiting = true;
    }

    void StepWashHands()
    {
        HapticPulse(0.2f);
        var target = FindToolByName("sm_handsoap.001", "sm_handsoap", "sm_sink.001", "sm_sink");
        SpawnArrowTo(target);
        Show(
            "First, wash your hands at the sink. Stand near the soap and hold the trigger until the meter fills.",
            "Walk to the sink and HOLD the trigger");
        _waiting = true;
    }

    void StepGloves()
    {
        HapticPulse(0.2f);
        var target = FindToolByName("LabGloves", "MedicalGloves");
        SpawnArrowTo(target);
        Show(
            "Hands are clean. Now equip the lab gloves from the tool table.",
            "Point at the gloves and press TRIGGER");
        _waiting = true;
    }

    void StepKnife()
    {
        HapticPulse(0.3f);
        var target = FindToolByName("LabKnife", "SurgicalKnife");
        SpawnArrowTo(target);
        Show(
            "Good. Pick up the dissection knife to split the brain.",
            "Pick up the knife from the tool table");
        _waiting = true;
    }

    void StepCutBrain()
    {
        HapticPulse(0.3f);
        Show(
            "Drag the knife along the red guide line on the brain to split it into two hemispheres.",
            "Drag knife through the red line on the brain");
        _waiting = true;
    }

    void StepHemisphereTweezers()
    {
        HapticPulse(0.5f);
        var target = FindToolByName("LabTweezers", "tweezers");
        SpawnArrowTo(target);
        Show(
            "Pick a hemisphere on the main panel, then pick up the tweezers. " +
            "Lower the opacity to see deep regions if needed.",
            "Choose a hemisphere, then take the tweezers");
        _waiting = true;
    }

    void StepSelectRegion()
    {
        HapticPulse(0.3f);

        if (_highlighted == null)
        {
            if (_cachedBrainManager == null) _cachedBrainManager = FindFirstObjectByType<BrainManager>();
            PickAndHighlightRegion(_cachedBrainManager);
        }

        DisableCollidersExceptHighlighted();
        AddEasyHitCollider();

        string regionName = _highlighted != null && _highlighted.regionData != null
            ? _highlighted.regionData.displayName
            : "the highlighted region";

        string hemiHint = "";
        if (_highlightedHemisphere != null)
            hemiHint = $" It is located in the {_highlightedHemisphere} hemisphere.";

        string regionCounter = _regionIndex > 0
            ? $"Region {_regionIndex + 1} of {REGIONS_PER_RUN}. "
            : $"We will practice with {REGIONS_PER_RUN} regions total. This is region 1. ";

        string layerHint = "";
        if (_hasHighlightedPreset)
        {
            string presetName = AnatomyLayerService.PresetLabels[(int)_highlightedPreset];
            layerHint = $" The target is in the {presetName} layer, so choose that layer yourself from the layer panel.";
        }

        string deepHint = "";
        if (_highlighted != null)
        {
            var rend = _highlighted.GetComponent<Renderer>();
            if (rend != null && rend.bounds.size.magnitude < 0.04f)
                deepHint = " This region is quite small. If you cannot see it clearly, " +
                    "hold B to open the panel and lower the opacity slider to make the brain transparent.";
        }

        Show(
            regionCounter +
            "The red highlight is " + regionName + "." + hemiHint + layerHint + deepHint +
            " Aim at it and press the trigger to extract it.",
            $"Select '{regionName}' with TRIGGER");
        _waiting = true;
    }

    void StepPutBack()
    {
        HapticPulse(0.3f);
        EnableAllColliders();
        ClearHighlight();
        _highlighted = null;
        _hasHighlightedPreset = false;
        _highlightedHemisphere = null;

        bool moreRegions = _regionIndex < REGIONS_PER_RUN - 1;
        string nextHint = moreRegions ? " Then we will try another region." : " This is the last one.";

        Show(
            "Read the details on the panel. When ready, click 'Put Back Into Brain'." + nextHint,
            "Click 'Put Back Into Brain' on the panel");
        _waiting = true;
    }

    void StepComplete()
    {
        HapticPulse(0.6f);
        ClearHighlight();
        _highlighted = null;
        _hasHighlightedPreset = false;
        _highlightedHemisphere = null;
        EnableAllColliders();
        Show(
            "Tutorial complete. You can now use Play and Assessment freely.",
            "Tutorial complete! Click Return to Menu.");
        if (_returnBtn != null) _returnBtn.SetActive(true);
    }

    // ========================= DISPLAY + TTS =========================

    string _pendingHint;

    void Show(string instruction, string hint)
    {
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _typeRoutine = StartCoroutine(TypeText(instruction));

        _pendingHint = hint;
        _narrationComplete = false;
        SetSkipButtonVisible(true);

        if (_hintText != null)
            _hintText.text = "\u266A  Listening...";

        TextToSpeech.Speak(instruction, speechRate, OnNarrationFinished);
    }

    void OnNarrationFinished()
    {
        _narrationComplete = true;
        SetSkipButtonVisible(false);
        if (_hintText != null && _pendingHint != null)
            _hintText.text = $">> {_pendingHint}";

        // The welcome step has no interactive completion -- advance once
        // narration finishes (this also triggers when the user presses Skip).
        if (_active && _step == STEP_WELCOME && _waiting)
        {
            _waiting = false;
            CompleteAndAdvance();
        }
    }

    void OnSkipNarration()
    {
        TextToSpeech.Stop();
        OnNarrationFinished();
    }

    void SetSkipButtonVisible(bool visible)
    {
        if (_skipBtn != null) _skipBtn.SetActive(visible);
    }

    IEnumerator TypeText(string full)
    {
        if (_instrText == null) yield break;
        _instrText.text = full;
    }

    // Angular raycast selection — same logic as Live Dissection.
    // Uses the XRRayInteractor transform for a proper world-space ray,
    // then checks the angle between the ray and the highlighted region's center.
    // No collider dependency: works reliably even for tiny/deep regions.

    Transform _cachedRayOrigin;
    int _raySearchCooldown;

    void PollTutorialRegionSelectionFallback(BrainManager bm)
    {
        if (bm == null || _highlighted == null || bm.IsInspectingRegion) return;
        if (LabToolManager.Instance == null || !LabToolManager.Instance.isHoldingTweezers) return;

        Ray ray = GetControllerRay();

        var rend = _highlighted.GetComponent<Renderer>();
        if (rend == null) return;

        Vector3 center = rend.bounds.center;
        float regionRadius = rend.bounds.extents.magnitude;

        Vector3 toCenter = center - ray.origin;
        float dist = toCenter.magnitude;
        if (dist < 0.01f) return;

        float angle = Vector3.Angle(ray.direction, toCenter.normalized);

        // Priority focus: while the tutorial is asking for a specific region,
        // any aim within the surrounding brain volume should resolve to the
        // target. We size the acceptance cone by the apparent angle of the
        // whole hemisphere from the player's viewpoint, then add a generous
        // margin so the user can be approximate.
        Bounds hemiBounds = ComputeHemisphereBoundsForHighlighted();
        float hemiRadius = hemiBounds.size.sqrMagnitude > 0.0001f
            ? hemiBounds.extents.magnitude
            : regionRadius * 6f;

        Vector3 toHemi = hemiBounds.size.sqrMagnitude > 0.0001f
            ? (hemiBounds.center - ray.origin)
            : toCenter;
        float hemiDist = Mathf.Max(toHemi.magnitude, 0.01f);
        float apparentHemi = Mathf.Atan2(hemiRadius, hemiDist) * Mathf.Rad2Deg;
        float maxAngle = Mathf.Clamp(apparentHemi + 10f, 12f, 45f);

        if (angle > maxAngle)
        {
            _tutorialTriggerHeld = false;
            return;
        }

        bool triggerDown = IsTriggerDown();
        if (triggerDown && !_tutorialTriggerHeld)
        {
            _tutorialTriggerHeld = true;
            _highlighted.SetHighlight(false);
            if (WorldSpaceHoverLabel.Instance != null)
                WorldSpaceHoverLabel.Instance.Hide();
            HapticFeedback.PulseBoth(0.35f, 0.15f);
            bm.OnRegionSelected(_highlighted);
        }
        else if (!triggerDown)
        {
            _tutorialTriggerHeld = false;
        }
    }

    Ray GetControllerRay()
    {
        if (_cachedRayOrigin == null && _raySearchCooldown <= 0)
        {
            _raySearchCooldown = 30;
            foreach (var ri in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(
                         FindObjectsSortMode.None))
            {
                if (ri != null && ri.enabled && ri.gameObject.activeInHierarchy)
                {
                    _cachedRayOrigin = ri.transform;
                    break;
                }
            }
        }
        else if (_cachedRayOrigin == null)
        {
            _raySearchCooldown--;
        }

        if (_cachedRayOrigin != null)
            return new Ray(_cachedRayOrigin.position, _cachedRayOrigin.forward);

        var mainCam = Camera.main;
        if (mainCam != null)
            return mainCam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));

        return new Ray(Vector3.zero, Vector3.forward);
    }

    bool IsTriggerDown()
    {
        var deviceBuffer = new List<InputDevice>();
        XRNode[] handNodes = { XRNode.RightHand, XRNode.LeftHand };
        foreach (var node in handNodes)
        {
            deviceBuffer.Clear();
            InputDevices.GetDevicesAtXRNode(node, deviceBuffer);
            foreach (var d in deviceBuffer)
            {
                if (!d.isValid) continue;
                if (d.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed)
                    return true;
            }
        }
        return false;
    }

    // ========================= RED HIGHLIGHT =========================
    // XRI's ColorMaterialPropertyAffordanceReceiver + MaterialPropertyBlockHelper
    // overrides ALL material and PropertyBlock changes on the region every frame.
    // The ONLY way to guarantee a visible red highlight is to create a completely
    // separate GameObject with its own MeshRenderer that XRI cannot touch.

    void PickAndHighlightRegion(BrainManager bm)
    {
        ClearHighlight();
        _highlighted = null;
        _highlightedHemisphere = null;

        GameObject selectedHemi = null;
        if (bm != null)
        {
            if (bm.leftHemisphere != null && bm.leftHemisphere.activeInHierarchy)
                selectedHemi = bm.leftHemisphere;
            if (selectedHemi == null && bm.rightHemisphere != null && bm.rightHemisphere.activeInHierarchy)
                selectedHemi = bm.rightHemisphere;
            if (selectedHemi == null)
                selectedHemi = bm.leftHemisphere ?? bm.rightHemisphere;
        }

        var layerSvc = AnatomyLayerService.Instance;
        bool hasLayer = layerSvc != null && layerSvc.IsPresetActive;
        AnatomyDepthPreset? chosenPreset = null;

        if (layerSvc != null)
        {
            var presetPool = new List<AnatomyDepthPreset>();
            var fallbackPresetPool = new List<AnatomyDepthPreset>();

            foreach (var r in _allRegions)
            {
                if (r == null || r.regionData == null) continue;
                if (selectedHemi != null && !r.transform.IsChildOf(selectedHemi.transform))
                    continue;

                var rend = r.GetComponent<Renderer>();
                if (rend == null) continue;

                var preset = layerSvc.GetPresetForRegion(r);
                if (!fallbackPresetPool.Contains(preset))
                    fallbackPresetPool.Add(preset);

                if (_recentlyUsedRegions.Contains(r)) continue;
                if (!layerSvc.IsRegionTutorialEligible(r)) continue;
                if (!presetPool.Contains(preset))
                    presetPool.Add(preset);
            }

            var availablePresets = new List<AnatomyDepthPreset>();
            foreach (var preset in presetPool)
            {
                if (!_recentlyUsedPresets.Contains(preset))
                    availablePresets.Add(preset);
            }

            if (availablePresets.Count == 0 && presetPool.Count > 0)
            {
                _recentlyUsedPresets.Clear();
                availablePresets.AddRange(presetPool);
            }

            if (availablePresets.Count == 0)
            {
                foreach (var preset in fallbackPresetPool)
                {
                    if (!_recentlyUsedPresets.Contains(preset))
                        availablePresets.Add(preset);
                }

                if (availablePresets.Count == 0)
                {
                    _recentlyUsedPresets.Clear();
                    availablePresets.AddRange(fallbackPresetPool);
                }
            }

            if (availablePresets.Count > 0)
            {
                chosenPreset = availablePresets[Random.Range(0, availablePresets.Count)];
                if (AnatomyLayerPanel.Instance != null)
                {
                    AnatomyLayerPanel.Instance.Show();
                    AnatomyLayerPanel.Instance.RefreshFromService();
                }
            }
        }

        var pool = new List<BrainRegion>();
        foreach (var r in _allRegions)
        {
            if (r == null || r.regionData == null) continue;
            if (_recentlyUsedRegions.Contains(r)) continue;
            if (selectedHemi != null && !r.transform.IsChildOf(selectedHemi.transform))
                continue;

            if (hasLayer)
            {
                if (chosenPreset.HasValue && layerSvc.GetPresetForRegion(r) != chosenPreset.Value) continue;
                if (!layerSvc.IsRegionTutorialEligible(r)) continue;
            }

            var rend = r.GetComponent<Renderer>();
            if (rend == null) continue;
            if (hasLayer || (r.gameObject.activeInHierarchy && rend.enabled))
                pool.Add(r);
        }

        if (pool.Count == 0 && hasLayer)
        {
            foreach (var r in _allRegions)
            {
                if (r == null || r.regionData == null) continue;
                if (_recentlyUsedRegions.Contains(r)) continue;
                if (selectedHemi != null && !r.transform.IsChildOf(selectedHemi.transform))
                    continue;
                if (chosenPreset.HasValue && layerSvc.GetPresetForRegion(r) != chosenPreset.Value) continue;
                pool.Add(r);
            }
        }

        if (pool.Count == 0)
        {
            foreach (var r in _allRegions)
            {
                if (r != null && r.regionData != null && r.gameObject.activeInHierarchy)
                {
                    if (_recentlyUsedRegions.Contains(r)) continue;
                    var rend = r.GetComponent<Renderer>();
                    if (rend != null && rend.enabled)
                        pool.Add(r);
                }
            }
        }

        // Last resort: allow repeats
        if (pool.Count == 0)
        {
            foreach (var r in _allRegions)
            {
                if (r != null && r.regionData != null && r.gameObject.activeInHierarchy)
                {
                    var rend = r.GetComponent<Renderer>();
                    if (rend != null && rend.enabled)
                        pool.Add(r);
                }
            }
        }

        if (pool.Count == 0)
        {
            Debug.LogError("[Tutorial] No regions available.");
            return;
        }

        _highlighted = pool[Random.Range(0, pool.Count)];
        _recentlyUsedRegions.Add(_highlighted);

        var actualPreset = layerSvc != null
            ? layerSvc.GetPresetForRegion(_highlighted)
            : AnatomyDepthPreset.FrontalParietal;

        if (!_recentlyUsedPresets.Contains(actualPreset))
            _recentlyUsedPresets.Add(actualPreset);

        _highlightedPreset = actualPreset;
        _hasHighlightedPreset = true;

        if (layerSvc != null)
        {
            var targetPreset = actualPreset;
            if (AnatomyLayerPanel.Instance != null)
            {
                AnatomyLayerPanel.Instance.Show();
                AnatomyLayerPanel.Instance.RefreshFromService();
            }
            Debug.Log($"[Tutorial] Target preset is '{AnatomyLayerService.PresetLabels[(int)targetPreset]}'. Student must choose it manually.");
        }

        CreateRedOverlay(_highlighted);
        BrainRegion.OnAnyHoverEntered += OnRegionHovered;
        BrainRegion.OnAnyHoverExited += OnRegionUnhovered;

        // Determine hemisphere
        if (bm != null)
        {
            if (bm.leftHemisphere != null && _highlighted.transform.IsChildOf(bm.leftHemisphere.transform))
                _highlightedHemisphere = "left";
            else if (bm.rightHemisphere != null && _highlighted.transform.IsChildOf(bm.rightHemisphere.transform))
                _highlightedHemisphere = "right";
        }
        if (_highlightedHemisphere == null && _highlighted.regionData != null)
        {
            _highlightedHemisphere = _highlighted.regionData.hemisphere == RegionData.Hemisphere.Left ? "left" : "right";
        }

        Debug.Log($"[Tutorial] Highlighted '{_highlighted.regionData.displayName}' " +
                  $"(hemisphere: {_highlightedHemisphere ?? "unknown"}) with overlay mesh.");
    }

    void CreateRedOverlay(BrainRegion region)
    {
        if (_overlayGO != null) Destroy(_overlayGO);

        if (!TryGetOverlaySource(region, out Transform meshTransform, out Mesh mesh))
        {
            Debug.LogError("[Tutorial] Region has no mesh -- cannot create overlay.");
            return;
        }

        // Attach the overlay to the transform that actually owns the mesh.
        // Some regions use a child mesh, and parenting to the BrainRegion root
        // causes the red highlight to appear offset from the real anatomy.
        _overlayGO = new GameObject("TutorialRedOverlay");
        _overlayGO.transform.SetParent(meshTransform, false);
        _overlayGO.transform.localPosition = Vector3.zero;
        _overlayGO.transform.localRotation = Quaternion.identity;
        _overlayGO.transform.localScale = Vector3.one * 1.015f;

        var overlayMF = _overlayGO.AddComponent<MeshFilter>();
        overlayMF.sharedMesh = mesh;
        var overlayRend = _overlayGO.AddComponent<MeshRenderer>();
        overlayRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        overlayRend.receiveShadows = false;

        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("UI/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");

        var mat = new Material(sh);
        mat.color = new Color(0.9f, 0.1f, 0.1f, 0.45f);
        mat.renderQueue = 3100;
        overlayRend.material = mat;

        _outlineGO = new GameObject("TutorialWhiteOutline");
        _outlineGO.transform.SetParent(meshTransform, false);
        _outlineGO.transform.localPosition = Vector3.zero;
        _outlineGO.transform.localRotation = Quaternion.identity;
        _outlineGO.transform.localScale = Vector3.one * 1.035f;

        var outlineMF = _outlineGO.AddComponent<MeshFilter>();
        outlineMF.sharedMesh = mesh;
        var outlineRend = _outlineGO.AddComponent<MeshRenderer>();
        outlineRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRend.receiveShadows = false;

        var outlineMat = new Material(sh);
        outlineMat.color = new Color(1f, 1f, 1f, 0.6f);
        outlineMat.renderQueue = 3200;
        outlineRend.material = outlineMat;

        _outlineGO.SetActive(false);

        _pulseRoutine = StartCoroutine(PulseGlow(overlayRend));

        Debug.Log($"[Tutorial] Overlay created on '{region.name}' using mesh source '{meshTransform.name}'");
    }

    bool TryGetOverlaySource(BrainRegion region, out Transform meshTransform, out Mesh mesh)
    {
        meshTransform = null;
        mesh = null;
        if (region == null) return false;

        var mf = region.GetComponent<MeshFilter>();
        var mr = region.GetComponent<MeshRenderer>();
        if (mf != null && mr != null && mf.sharedMesh != null)
        {
            meshTransform = mf.transform;
            mesh = mf.sharedMesh;
            return true;
        }

        foreach (var childMf in region.GetComponentsInChildren<MeshFilter>(true))
        {
            if (childMf == null || childMf.sharedMesh == null) continue;
            if (childMf.GetComponent<MeshRenderer>() == null) continue;
            meshTransform = childMf.transform;
            mesh = childMf.sharedMesh;
            return true;
        }

        var smr = region.GetComponent<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null)
        {
            meshTransform = smr.transform;
            mesh = smr.sharedMesh;
            return true;
        }

        foreach (var childSmr in region.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (childSmr == null || childSmr.sharedMesh == null) continue;
            meshTransform = childSmr.transform;
            mesh = childSmr.sharedMesh;
            return true;
        }

        return false;
    }

    IEnumerator PulseGlow(Renderer rend)
    {
        while (_overlayGO != null && rend != null)
        {
            float t = (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(0.25f, 0.55f, t);
            rend.material.color = new Color(0.9f, 0.1f, 0.1f, alpha);
            yield return null;
        }
    }

    void SetOutlineVisible(bool visible)
    {
        if (_outlineGO != null) _outlineGO.SetActive(visible);
    }

    void OnRegionHovered(BrainRegion region)
    {
        if (_highlighted != null && region == _highlighted)
        {
            region.SetHighlight(false);
            SetOutlineVisible(true);
        }
    }

    void OnRegionUnhovered(BrainRegion region)
    {
        if (_highlighted != null && region == _highlighted)
            SetOutlineVisible(false);
    }

    void ClearHighlight()
    {
        BrainRegion.OnAnyHoverEntered -= OnRegionHovered;
        BrainRegion.OnAnyHoverExited -= OnRegionUnhovered;
        RemoveEasyHitCollider();
        if (_pulseRoutine != null) { StopCoroutine(_pulseRoutine); _pulseRoutine = null; }
        if (_overlayGO != null) { Destroy(_overlayGO); _overlayGO = null; }
        if (_outlineGO != null) { Destroy(_outlineGO); _outlineGO = null; }
    }

    // ========================= COLLIDER MANAGEMENT =========================

    /// <summary>Only the highlighted region keeps its colliders.
    /// All other regions become non-interactive so the ray passes through them.</summary>
    void DisableCollidersExceptHighlighted()
    {
        foreach (var r in _allRegions)
        {
            if (r == null) continue;
            bool enable = (r == _highlighted);
            foreach (var col in r.GetComponentsInChildren<Collider>(true))
            {
                if (col != null) col.enabled = enable;
            }
        }

        if (_cachedBrainManager != null)
        {
            DisableNonRegionColliders(_cachedBrainManager.leftHemisphere);
            DisableNonRegionColliders(_cachedBrainManager.rightHemisphere);
        }
    }

    void DisableNonRegionColliders(GameObject hemi)
    {
        if (hemi == null) return;
        foreach (var col in hemi.GetComponentsInChildren<Collider>(true))
        {
            if (col == null) continue;
            if (col.GetComponentInParent<BrainRegion>() != null) continue;
            col.enabled = false;
        }
    }

    /// <summary>Adds a very large sphere collider to the highlighted region so
    /// it's effectively the only thing the XR ray can hit while the tutorial
    /// is asking for that specific target. The collider is sized to wrap the
    /// surrounding brain hemisphere so the player can aim almost anywhere on
    /// the brain and still trigger the highlighted target — this is what
    /// makes deeply-embedded small regions reliably selectable.</summary>
    void AddEasyHitCollider()
    {
        RemoveEasyHitCollider();
        if (_highlighted == null) return;

        _easyHitCollider = _highlighted.gameObject.AddComponent<SphereCollider>();

        // Compute the brain hemisphere bounds so the easy-hit collider can
        // span the whole surrounding region. We prefer the parent hemisphere
        // (matching the side the highlighted target sits on) so we don't
        // overlap into the opposite hemisphere.
        Bounds hemisphereBounds = ComputeHemisphereBoundsForHighlighted();

        var rend = _highlighted.GetComponent<Renderer>();
        Vector3 worldCenter;
        float radius;

        if (rend != null)
        {
            // Center on the highlighted region so any ray angle that arcs
            // through the brain volume still resolves to it.
            worldCenter = rend.bounds.center;

            // Take the largest distance from the highlighted region's center
            // to any corner of the hemisphere bounds — this guarantees the
            // collider wraps the whole hemisphere.
            float reach = ComputeReachToBounds(worldCenter, hemisphereBounds);
            float regionRadius = rend.bounds.extents.magnitude;

            // Floors keep the collider huge even if hemisphere bounds couldn't
            // be measured (e.g. inactive renderers).
            radius = Mathf.Max(reach, regionRadius * 4f, 0.35f);
        }
        else if (hemisphereBounds.size.sqrMagnitude > 0.0001f)
        {
            worldCenter = hemisphereBounds.center;
            radius = hemisphereBounds.extents.magnitude;
        }
        else
        {
            worldCenter = _highlighted.transform.position;
            radius = 0.4f;
        }

        _easyHitCollider.center = _highlighted.transform.InverseTransformPoint(worldCenter);
        _easyHitCollider.radius = radius;
        _easyHitCollider.isTrigger = false;
    }

    /// <summary>Returns the world-space bounds of the hemisphere the highlighted
    /// region belongs to, falling back to whichever hemisphere has data.</summary>
    Bounds ComputeHemisphereBoundsForHighlighted()
    {
        var bm = _cachedBrainManager;
        if (bm == null) bm = FindFirstObjectByType<BrainManager>();
        if (bm == null || _highlighted == null)
            return new Bounds(Vector3.zero, Vector3.zero);

        GameObject hemi = null;
        if (bm.leftHemisphere != null && _highlighted.transform.IsChildOf(bm.leftHemisphere.transform))
            hemi = bm.leftHemisphere;
        else if (bm.rightHemisphere != null && _highlighted.transform.IsChildOf(bm.rightHemisphere.transform))
            hemi = bm.rightHemisphere;
        if (hemi == null) hemi = bm.leftHemisphere != null ? bm.leftHemisphere : bm.rightHemisphere;
        if (hemi == null) return new Bounds(_highlighted.transform.position, Vector3.zero);

        var renderers = hemi.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(_highlighted.transform.position, Vector3.zero);

        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    static float ComputeReachToBounds(Vector3 center, Bounds b)
    {
        if (b.size.sqrMagnitude < 0.0001f) return 0f;
        Vector3 min = b.min, max = b.max;
        float dx = Mathf.Max(Mathf.Abs(center.x - min.x), Mathf.Abs(center.x - max.x));
        float dy = Mathf.Max(Mathf.Abs(center.y - min.y), Mathf.Abs(center.y - max.y));
        float dz = Mathf.Max(Mathf.Abs(center.z - min.z), Mathf.Abs(center.z - max.z));
        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    float GetTutorialOpacityAssistMultiplier()
    {
        var ui = _cachedBrainManager != null ? _cachedBrainManager.regionUIController : null;
        var slider = ui != null ? ui.opacitySlider : null;
        if (slider == null) return 1f;

        float opacity = slider.value;
        if (opacity <= 0.10f) return 1.9f;
        if (opacity <= 0.25f) return 1.6f;
        if (opacity <= 0.40f) return 1.3f;
        return 1f;
    }

    void RemoveEasyHitCollider()
    {
        if (_easyHitCollider != null)
        {
            Destroy(_easyHitCollider);
            _easyHitCollider = null;
        }
    }

    void EnableAllColliders()
    {
        RemoveEasyHitCollider();
        foreach (var r in _allRegions)
        {
            if (r == null) continue;
            foreach (var col in r.GetComponentsInChildren<Collider>(true))
            {
                if (col != null) col.enabled = true;
            }
        }

        if (_cachedBrainManager != null)
        {
            EnableNonRegionColliders(_cachedBrainManager.leftHemisphere);
            EnableNonRegionColliders(_cachedBrainManager.rightHemisphere);
        }
    }

    void EnableNonRegionColliders(GameObject hemi)
    {
        if (hemi == null) return;
        foreach (var col in hemi.GetComponentsInChildren<Collider>(true))
        {
            if (col != null) col.enabled = true;
        }
    }

    // ========================= RETURN TO MENU =========================

    public void OnReturnToMenuClicked()
    {
        EndTutorial();

        ProgressTracker.MarkTutorialComplete();
        if (AchievementManager.Instance != null)
            AchievementManager.Instance.CheckTutorialComplete();

        SessionData.IsTutorialMode = false;

        if (LabToolManager.Instance != null)
            LabToolManager.Instance.ResetLab();

        var mm = FindFirstObjectByType<MenuManager>();
        if (mm != null)
        {
            // Disable movement
            if (mm.movementGate != null)
                mm.movementGate.DisableMovement();

            // Close doors
            if (mm.doorController != null)
                mm.doorController.CloseDoors();

            // Hide floating brain dissection panel
            var floatingPanel = FindFirstObjectByType<FloatingInfoPanel>();
            if (floatingPanel != null)
            {
                var cg = floatingPanel.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 0f;
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                }
            }

            // Teleport player back to start
            mm.TeleportToStart();

            // Fade menu back in
            if (mm.startMenuCanvas != null)
                mm.startMenuCanvas.SetActive(true);
            if (mm.menuCanvasGroup != null)
            {
                mm.menuCanvasGroup.alpha = 1f;
                mm.menuCanvasGroup.interactable = true;
                mm.menuCanvasGroup.blocksRaycasts = true;
            }
            mm.ShowMainMenu();
        }
    }

    void HapticPulse(float amp) => HapticFeedback.PulseBoth(amp, 0.2f);

    void ApplyTutorialHudSpacing()
    {
        _floatingHud = FindFirstObjectByType<FloatingInfoPanel>();
        if (_floatingHud == null || _floatingHudTweaked) return;

        _savedHudFollowDistance = _floatingHud.followDistance;
        _savedHudVerticalAngle = _floatingHud.verticalAngle;

        _floatingHud.followDistance = 0.88f;
        _floatingHud.verticalAngle = -4.5f;
        _floatingHudTweaked = true;
        _floatingHud.SnapToView();
    }

    void RestoreFloatingHudSpacing()
    {
        if (_floatingHud == null || !_floatingHudTweaked) return;

        _floatingHud.followDistance = _savedHudFollowDistance;
        _floatingHud.verticalAngle = _savedHudVerticalAngle;
        _floatingHudTweaked = false;
        _floatingHud.SnapToView();
    }

    // ========================= PROGRESS DOTS =========================

    void UpdateProgress()
    {
        if (_progressText == null) return;
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= TOTAL; i++)
        {
            sb.Append(i < _step ? "\u25CF" : i == _step ? "\u25C9" : "\u25CB");
            if (i < TOTAL) sb.Append("  ");
        }
        _progressText.text = sb.ToString();
    }

    // ========================= BANNER CREATION =========================

    void BuildBanner()
    {
        if (_banner != null) return;

        var cam = Camera.main;
        if (cam == null) { Debug.LogWarning("[Tutorial] No camera."); return; }

        _banner = new GameObject("TutorialBanner");
        _banner.transform.SetParent(cam.transform, false);
        _banner.transform.localPosition = new Vector3(0f, 0.12f, 0.65f);
        _banner.transform.localRotation = Quaternion.identity;

        var canvas = _banner.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _banner.AddComponent<CanvasScaler>();
        _banner.AddComponent<TrackedDeviceGraphicRaycaster>();

        var rt = _banner.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(940, 220);
        rt.localScale = Vector3.one * 0.0008f;

        var bg = new GameObject("Bg");
        bg.transform.SetParent(_banner.transform, false);
        var bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.02f, 0.04f, 0.10f, 0.92f);

        var border = new GameObject("Border");
        border.transform.SetParent(bg.transform, false);
        var bRT = border.AddComponent<RectTransform>();
        bRT.anchorMin = Vector2.zero; bRT.anchorMax = Vector2.one;
        bRT.offsetMin = new Vector2(-1, -1); bRT.offsetMax = new Vector2(1, 1);
        border.AddComponent<Image>().color = new Color(0.2f, 0.5f, 1f, 0.3f);
        border.transform.SetAsFirstSibling();

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        _stepLabel = MkText("Step", bg.transform,
            new Vector2(0, 84), new Vector2(860, 26),
            "TUTORIAL  \u2014  Step 1 of 8", 18, FontStyle.Bold,
            new Color(0.4f, 0.85f, 1f), TextAnchor.MiddleCenter, font);

        _progressText = MkText("Progress", bg.transform,
            new Vector2(0, 60), new Vector2(860, 22),
            "", 16, FontStyle.Normal,
            new Color(0.5f, 0.7f, 1f), TextAnchor.MiddleCenter, font);

        var line = new GameObject("Line");
        line.transform.SetParent(bg.transform, false);
        var lineRT = line.AddComponent<RectTransform>();
        lineRT.anchorMin = lineRT.anchorMax = new Vector2(0.5f, 0.5f);
        lineRT.sizeDelta = new Vector2(860, 2);
        lineRT.anchoredPosition = new Vector2(0, 40);
        line.AddComponent<Image>().color = new Color(0.3f, 0.6f, 1f, 0.4f);

        _instrText = MkText("Instr", bg.transform,
            new Vector2(0, -5), new Vector2(870, 100),
            "", 18, FontStyle.Normal,
            new Color(0.93f, 0.93f, 0.96f), TextAnchor.UpperLeft, font);

        _hintText = MkText("Hint", bg.transform,
            new Vector2(0, -78), new Vector2(860, 30),
            "", 16, FontStyle.Bold,
            new Color(0.3f, 1f, 0.5f), TextAnchor.MiddleCenter, font);

        _returnBtn = new GameObject("ReturnBtn");
        _returnBtn.transform.SetParent(bg.transform, false);
        var btnRT = _returnBtn.AddComponent<RectTransform>();
        btnRT.anchorMin = btnRT.anchorMax = new Vector2(0.5f, 0.5f);
        btnRT.sizeDelta = new Vector2(220, 40);
        btnRT.anchoredPosition = new Vector2(0, -78);
        _returnBtn.AddComponent<Image>().color = new Color(0.12f, 0.50f, 0.22f);
        var btn = _returnBtn.AddComponent<Button>();
        btn.onClick.AddListener(OnReturnToMenuClicked);

        var lblGO = new GameObject("Lbl");
        lblGO.transform.SetParent(_returnBtn.transform, false);
        var lblRT = lblGO.AddComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;
        var lblT = lblGO.AddComponent<Text>();
        lblT.text = "Return to Menu"; lblT.fontSize = 16;
        lblT.fontStyle = FontStyle.Bold; lblT.color = Color.white;
        lblT.alignment = TextAnchor.MiddleCenter; lblT.font = font;
        _returnBtn.SetActive(false);

        // Skip narration button (top-right of banner)
        _skipBtn = new GameObject("SkipBtn");
        _skipBtn.transform.SetParent(bg.transform, false);
        var skipRT = _skipBtn.AddComponent<RectTransform>();
        skipRT.anchorMin = skipRT.anchorMax = new Vector2(1f, 1f);
        skipRT.pivot = new Vector2(1f, 1f);
        skipRT.sizeDelta = new Vector2(128, 32);
        skipRT.anchoredPosition = new Vector2(-12, -10);
        _skipBtn.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.4f, 0.7f);
        var skipButton = _skipBtn.AddComponent<Button>();
        skipButton.onClick.AddListener(OnSkipNarration);

        var skipLbl = new GameObject("Lbl");
        skipLbl.transform.SetParent(_skipBtn.transform, false);
        var skipLblRT = skipLbl.AddComponent<RectTransform>();
        skipLblRT.anchorMin = Vector2.zero; skipLblRT.anchorMax = Vector2.one;
        skipLblRT.offsetMin = skipLblRT.offsetMax = Vector2.zero;
        var skipT = skipLbl.AddComponent<Text>();
        skipT.text = "Skip \u25B6"; skipT.fontSize = 14;
        skipT.fontStyle = FontStyle.Bold; skipT.color = Color.white;
        skipT.alignment = TextAnchor.MiddleCenter; skipT.font = font;
        _skipBtn.SetActive(false);
    }

    Text MkText(string name, Transform parent, Vector2 pos, Vector2 size,
        string text, int fontSize, FontStyle style, Color color,
        TextAnchor align, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        var t = go.AddComponent<Text>();
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
}
