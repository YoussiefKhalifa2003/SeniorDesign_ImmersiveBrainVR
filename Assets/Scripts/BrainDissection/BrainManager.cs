using UnityEngine;
using UnityEngine.XR;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Central brain dissection controller.
///
/// THE #1 RULE: Everything stays DIRECTLY IN FRONT of the user.
///   - Brain never moves from its visual center position
///   - Rotation: compute visual center, rotate root, snap center back
///   - Split: tiny separation scaled to actual brain size
///   - Hemisphere select: just show/hide, zero position changes
///   - Zoom: move XR rig toward whatever is being viewed
/// </summary>
public class BrainManager : MonoBehaviour
{
    [Header("Hemisphere References")]
    public GameObject leftHemisphere;
    public GameObject rightHemisphere;

    [Header("Brain Root")]
    public GameObject brainRoot;

    [Header("Kidney Tray (hemispheres go here after split)")]
    public Transform kidneyTray;

    [Header("UI")]
    public RegionUIController regionUIController;

    // ---- State ----
    public enum ViewState { WholeBrain, LeftFocused, RightFocused, RegionSelected }
    private ViewState _currentState = ViewState.WholeBrain;
    public bool IsInspectingRegion => _currentState == ViewState.RegionSelected;
    public bool IsHemisphereSelected => _currentState == ViewState.LeftFocused || _currentState == ViewState.RightFocused;
    public bool IsLeftHemisphereFocused => _currentState == ViewState.LeftFocused;
    public bool IsRightHemisphereFocused => _currentState == ViewState.RightFocused;

    // ---- Selected region ----
    private BrainRegion _selectedRegion;
    private RegionInspector _activeInspector;
    private ViewState _stateBeforeRegionSelect;
    private bool _firstRegionExtracted;
    private BrainRegion _crossReferenceBlinkTarget;
    private Coroutine _crossReferenceBlinkCoroutine;

    /// <summary>
    /// The region currently being inspected (extracted), or null. Read-only
    /// accessor used by Play-mode features (e.g. voice narration) to bind
    /// their context to the active region without depending on UI state.
    /// </summary>
    public BrainRegion InspectedRegion => _selectedRegion;

    /// <summary>
    /// Raised after the inspected region is cleared (put-back, reset, etc.).
    /// Fires once per inspection end, after _selectedRegion has been cleared,
    /// so listeners (e.g. voice narration) can stop in-flight TTS playback.
    /// </summary>
    public static event System.Action OnInspectionEnded;

    // ---- ORIGINAL state (set once at Start, NEVER modified) ----
    private Vector3 _originalRootPosition;
    private Quaternion _originalRootRotation;

    // ---- Current locked root position (updated after rotation to keep center fixed) ----
    private Vector3 _lockedRootPosition;
    private bool _initialized;

    // ---- Cached renderers for ComputeVisualCenter (avoid per-frame GetComponentsInChildren) ----
    private Renderer[] _cachedRenderers;

    // ---- Grab-to-rotate: user holds Grip on brain, release stops immediately ----
    private Transform _rotateInteractor;
    private Quaternion _lastInteractorRotation;
    private bool _hasLastInteractorRotation;
    private bool _rotateIsLeftHand;

    // (Hemisphere positions are never modified - brain stays in place)

    // ---- Opacity ----
    private float _brainOpacity = 1f;
    private List<MaterialData> _materialCache = new List<MaterialData>();
    private bool _materialsCached;

    private struct MaterialData
    {
        public Renderer renderer;
        public Material material;
        public Color originalColor;
        public int originalRenderQueue;
    }

    private void Start()
    {
        if (brainRoot == null) return;

        _originalRootPosition = brainRoot.transform.position;
        _originalRootRotation = brainRoot.transform.rotation;
        _lockedRootPosition = _originalRootPosition;

        _initialized = true;
        CacheMaterials();

        Debug.Log($"[BrainManager] Init. Root={_originalRootPosition}, " +
                  $"VisualCenter={ComputeVisualCenter()}");
    }

    // ===================== POSITION ENFORCEMENT =====================

    // Poll-based rotation fallback: when XRI events can't reach the brain (e.g. tweezers held),
    // we poll grip input directly and apply controller rotation to the brain.
    private bool _pollRotating;
    private XRNode _pollRotateHand;
    private Quaternion _pollLastControllerRot;
    private bool _pollHasLastRot;

    private void Update()
    {
        // XRI-driven rotation: stop when grip released
        if (_rotateInteractor != null && !IsRotateButtonStillHeld())
        {
            EndUserRotate();
        }

        // Poll-based fallback for Tutorial/Play (skipped during Live Dissection which has its own poll)
        if (_rotateInteractor == null)
            PollGripRotationFallback();
    }

    private void PollGripRotationFallback()
    {
        var ld = LiveDissectionManager.Instance;
        if (ld != null && ld.IsLiveDissectionActive) return;

        if (LabToolManager.Instance == null || !LabToolManager.Instance.glovesEquipped) return;
        if (LabToolManager.Instance != null && !LabToolManager.Instance.brainIsSplit) return;
        if (_currentState == ViewState.RegionSelected) return;
        if (brainRoot == null) return;

        bool leftGrip = false, rightGrip = false;
        Quaternion leftRot = Quaternion.identity, rightRot = Quaternion.identity;

        var leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftDevice.isValid)
        {
            leftDevice.TryGetFeatureValue(CommonUsages.gripButton, out leftGrip);
            leftDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out leftRot);
        }
        var rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightDevice.isValid)
        {
            rightDevice.TryGetFeatureValue(CommonUsages.gripButton, out rightGrip);
            rightDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out rightRot);
        }

        bool anyGrip = leftGrip || rightGrip;

        if (anyGrip && !_pollRotating)
        {
            _pollRotating = true;
            _pollRotateHand = leftGrip ? XRNode.LeftHand : XRNode.RightHand;
            _pollLastControllerRot = _pollRotateHand == XRNode.LeftHand ? leftRot : rightRot;
            _pollHasLastRot = false;
            return;
        }

        if (!anyGrip && _pollRotating)
        {
            _pollRotating = false;
            _pollHasLastRot = false;
            return;
        }

        if (_pollRotating)
        {
            Quaternion currentRot = _pollRotateHand == XRNode.LeftHand ? leftRot : rightRot;
            if (_pollHasLastRot)
            {
                Quaternion delta = currentRot * Quaternion.Inverse(_pollLastControllerRot);
                ApplyRotationKeepingCenterFixed(delta);
            }
            _pollLastControllerRot = currentRot;
            _pollHasLastRot = true;
        }
    }

    private void LateUpdate()
    {
        if (!_initialized || brainRoot == null) return;

        // Apply grab-to-rotate: controller rotation delta drives brain rotation, center stays fixed
        if (_rotateInteractor != null)
        {
            Quaternion currentRot = _rotateInteractor.rotation;
            if (_hasLastInteractorRotation)
            {
                Quaternion delta = currentRot * Quaternion.Inverse(_lastInteractorRotation);
                ApplyRotationKeepingCenterFixed(delta);
            }
            _lastInteractorRotation = currentRot;
            _hasLastInteractorRotation = true;
        }
        else
        {
            _hasLastInteractorRotation = false;
        }

        brainRoot.transform.position = _lockedRootPosition;
    }

    /// <summary>True if the hand that started rotate is still holding Grip (activate).</summary>
    private bool IsRotateButtonStillHeld()
    {
        var device = InputDevices.GetDeviceAtXRNode(_rotateIsLeftHand ? XRNode.LeftHand : XRNode.RightHand);
        if (!device.isValid) return false;
        // Grip is typically the "activate" binding in XR Controller
        if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool grip) && grip) return true;
        // Fallback: some setups bind activate to trigger when not selecting
        if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger) && trigger) return true;
        return false;
    }

    // ===================== VISUAL CENTER =====================

    /// <summary>World-space center of all visible brain renderers.</summary>
    private Vector3 ComputeVisualCenter()
    {
        if (brainRoot == null) return Vector3.zero;
        if (_cachedRenderers == null)
            _cachedRenderers = brainRoot.GetComponentsInChildren<Renderer>(true);
        Bounds b = new Bounds();
        bool first = true;
        foreach (var r in _cachedRenderers)
        {
            if (r == null || !r.enabled) continue;
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        return first ? brainRoot.transform.position : b.center;
    }

    /// <summary>Invalidate cached renderers (call when brain hierarchy changes).</summary>
    public void InvalidateRendererCache() { _cachedRenderers = null; }

    // ===================== MATERIAL CACHE =====================

    private void CacheMaterials()
    {
        _materialCache.Clear();
        if (brainRoot == null) return;
        foreach (var rend in brainRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (rend == null) continue;
            foreach (var mat in rend.materials)
            {
                if (mat == null) continue;
                _materialCache.Add(new MaterialData
                {
                    renderer = rend,
                    material = mat,
                    originalColor = mat.color,
                    originalRenderQueue = mat.renderQueue
                });
            }
        }
        _materialsCached = true;
    }

    // ===================== BRAIN SPLIT =====================

    // Saved original hierarchy state so we can undo the split
    private Transform _leftHemiOrigParent;
    private Transform _rightHemiOrigParent;
    private Vector3 _leftHemiOrigLocal;
    private Vector3 _rightHemiOrigLocal;
    private Quaternion _leftHemiOrigRotation;
    private Quaternion _rightHemiOrigRotation;
    private Vector3 _leftHemiOrigScale;
    private Vector3 _rightHemiOrigScale;
    private bool _hemiPositionsSaved;

    // Saved kidney tray positions (world space) so we can send hemispheres back
    private Vector3 _leftKidneyPos;
    private Vector3 _rightKidneyPos;
    private Quaternion _leftKidneyRot;
    private Quaternion _rightKidneyRot;

    [Header("Animation")]
    public float splitAnimDuration = 1.5f;
    public float hemiMoveAnimDuration = 1.0f;
    private bool _animating;

    /// <summary>
    /// On split: both hemispheres first separate slightly, then smoothly
    /// animate into the KidneyTray with an S-curve ease.
    /// </summary>
    public void PerformBrainSplit()
    {
        if (!_hemiPositionsSaved)
        {
            if (leftHemisphere != null)
            {
                _leftHemiOrigParent = leftHemisphere.transform.parent;
                _leftHemiOrigLocal = leftHemisphere.transform.localPosition;
                _leftHemiOrigRotation = leftHemisphere.transform.localRotation;
                _leftHemiOrigScale = leftHemisphere.transform.localScale;
            }
            if (rightHemisphere != null)
            {
                _rightHemiOrigParent = rightHemisphere.transform.parent;
                _rightHemiOrigLocal = rightHemisphere.transform.localPosition;
                _rightHemiOrigRotation = rightHemisphere.transform.localRotation;
                _rightHemiOrigScale = rightHemisphere.transform.localScale;
            }
            _hemiPositionsSaved = true;
        }

        if (kidneyTray != null)
            StartCoroutine(AnimateBrainSplit());
        else
        {
            float localGap = 0.01f;
            if (leftHemisphere != null)
                leftHemisphere.transform.localPosition = _leftHemiOrigLocal + new Vector3(-localGap, 0, 0);
            if (rightHemisphere != null)
                rightHemisphere.transform.localPosition = _rightHemiOrigLocal + new Vector3(localGap, 0, 0);
        }
    }

    private IEnumerator AnimateBrainSplit()
    {
        _animating = true;

        SetHemisphereVisible(leftHemisphere, true);
        SetHemisphereVisible(rightHemisphere, true);

        Vector3 leftStartPos = leftHemisphere != null ? leftHemisphere.transform.position : Vector3.zero;
        Quaternion leftStartRot = leftHemisphere != null ? leftHemisphere.transform.rotation : Quaternion.identity;
        Vector3 rightStartPos = rightHemisphere != null ? rightHemisphere.transform.position : Vector3.zero;
        Quaternion rightStartRot = rightHemisphere != null ? rightHemisphere.transform.rotation : Quaternion.identity;

        Bounds trayBounds = ComputeWorldBounds(kidneyTray.gameObject);
        Vector3 trayCenter = trayBounds.center;
        float trayTop = trayBounds.max.y;
        bool spreadAlongZ = trayBounds.size.z > trayBounds.size.x;

        float hemiSize = 0f;
        if (leftHemisphere != null)
        {
            Bounds lb = ComputeWorldBounds(leftHemisphere);
            hemiSize = spreadAlongZ ? lb.size.z : lb.size.x;
        }
        float spacing = Mathf.Max(0.05f, hemiSize * 0.6f);

        Vector3 leftTargetPos = leftStartPos;
        Vector3 rightTargetPos = rightStartPos;

        if (leftHemisphere != null)
        {
            leftHemisphere.transform.SetParent(kidneyTray, true);
            Bounds lb = ComputeWorldBounds(leftHemisphere);
            if (spreadAlongZ)
                leftTargetPos = leftHemisphere.transform.position + (new Vector3(trayCenter.x, trayTop, trayCenter.z - spacing) - lb.center);
            else
                leftTargetPos = leftHemisphere.transform.position + (new Vector3(trayCenter.x - spacing, trayTop, trayCenter.z) - lb.center);
        }

        if (rightHemisphere != null)
        {
            rightHemisphere.transform.SetParent(kidneyTray, true);
            Bounds rb = ComputeWorldBounds(rightHemisphere);
            if (spreadAlongZ)
                rightTargetPos = rightHemisphere.transform.position + (new Vector3(trayCenter.x, trayTop, trayCenter.z + spacing) - rb.center);
            else
                rightTargetPos = rightHemisphere.transform.position + (new Vector3(trayCenter.x + spacing, trayTop, trayCenter.z) - rb.center);
        }

        float elapsed = 0f;
        while (elapsed < splitAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / splitAnimDuration);

            if (leftHemisphere != null)
                leftHemisphere.transform.position = Vector3.Lerp(leftStartPos, leftTargetPos, t);
            if (rightHemisphere != null)
                rightHemisphere.transform.position = Vector3.Lerp(rightStartPos, rightTargetPos, t);

            yield return null;
        }

        if (leftHemisphere != null)
        {
            leftHemisphere.transform.position = leftTargetPos;
            _leftKidneyPos = leftHemisphere.transform.position;
            _leftKidneyRot = leftHemisphere.transform.rotation;
        }
        if (rightHemisphere != null)
        {
            rightHemisphere.transform.position = rightTargetPos;
            _rightKidneyPos = rightHemisphere.transform.position;
            _rightKidneyRot = rightHemisphere.transform.rotation;
        }

        _animating = false;
        Debug.Log("[BrainManager] Brain split animation complete.");
    }

    /// <summary>Restore hemispheres to their original parent and local positions (BrainRoot).</summary>
    private void UndoBrainSplit()
    {
        if (!_hemiPositionsSaved) return;
        ReturnToSurgicalTray(leftHemisphere, _leftHemiOrigParent, _leftHemiOrigLocal, _leftHemiOrigRotation, _leftHemiOrigScale);
        ReturnToSurgicalTray(rightHemisphere, _rightHemiOrigParent, _rightHemiOrigLocal, _rightHemiOrigRotation, _rightHemiOrigScale);
    }

    /// <summary>Send a hemisphere back to the kidney tray at its saved position.</summary>
    private void SendToKidneyTray(GameObject hemi, Vector3 savedPos, Quaternion savedRot)
    {
        if (hemi == null || kidneyTray == null) return;
        hemi.transform.SetParent(kidneyTray, true);
        hemi.transform.position = savedPos;
        hemi.transform.rotation = savedRot;
        SetHemisphereVisible(hemi, true);
    }

    /// <summary>Return a hemisphere to surgical tray (BrainRoot) at its original local transform.</summary>
    private void ReturnToSurgicalTray(GameObject hemi, Transform origParent,
        Vector3 origLocalPos, Quaternion origLocalRot, Vector3 origLocalScale)
    {
        if (hemi == null || !_hemiPositionsSaved) return;
        hemi.transform.SetParent(origParent, false);
        hemi.transform.localPosition = origLocalPos;
        hemi.transform.localRotation = origLocalRot;
        hemi.transform.localScale = origLocalScale;
        SetHemisphereVisible(hemi, true);
    }

    /// <summary>World-space bounds of any GameObject (from all child renderers).</summary>
    private Bounds ComputeWorldBounds(GameObject go)
    {
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        bool first = true;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // ===================== HEMISPHERE SELECTION =====================
    // Selected hemisphere returns to surgical tray. Other stays VISIBLE in kidney tray.

    public void ShowLeftHemisphere()
    {
        if (_currentState == ViewState.RegionSelected || _animating) return;
        if (LabToolManager.Instance != null && !LabToolManager.Instance.brainIsSplit) return;

        StartCoroutine(AnimateHemisphereSwitch(
            leftHemisphere, _leftHemiOrigParent, _leftHemiOrigLocal, _leftHemiOrigRotation, _leftHemiOrigScale,
            rightHemisphere, _rightKidneyPos, _rightKidneyRot,
            ViewState.LeftFocused));
    }

    public void ShowRightHemisphere()
    {
        if (_currentState == ViewState.RegionSelected || _animating) return;
        if (LabToolManager.Instance != null && !LabToolManager.Instance.brainIsSplit) return;

        StartCoroutine(AnimateHemisphereSwitch(
            rightHemisphere, _rightHemiOrigParent, _rightHemiOrigLocal, _rightHemiOrigRotation, _rightHemiOrigScale,
            leftHemisphere, _leftKidneyPos, _leftKidneyRot,
            ViewState.RightFocused));
    }

    private IEnumerator AnimateHemisphereSwitch(
        GameObject toSurgical, Transform origParent, Vector3 origLocal, Quaternion origRot, Vector3 origScale,
        GameObject toKidney, Vector3 kidneyPos, Quaternion kidneyRot,
        ViewState targetState)
    {
        _animating = true;

        Vector3 kidStartWorld = toKidney != null ? toKidney.transform.position : Vector3.zero;
        Quaternion kidStartRot = toKidney != null ? toKidney.transform.rotation : Quaternion.identity;

        if (toSurgical != null)
            toSurgical.transform.SetParent(origParent, true);
        if (toKidney != null)
            toKidney.transform.SetParent(kidneyTray, true);

        Vector3 surgLocalStart = toSurgical != null ? toSurgical.transform.localPosition : Vector3.zero;
        Quaternion surgLocalStartRot = toSurgical != null ? toSurgical.transform.localRotation : Quaternion.identity;

        float elapsed = 0f;
        while (elapsed < hemiMoveAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / hemiMoveAnimDuration);

            if (toSurgical != null)
            {
                toSurgical.transform.localPosition = Vector3.Lerp(surgLocalStart, origLocal, t);
                toSurgical.transform.localRotation = Quaternion.Slerp(surgLocalStartRot, origRot, t);
            }
            if (toKidney != null)
            {
                toKidney.transform.position = Vector3.Lerp(kidStartWorld, kidneyPos, t);
                toKidney.transform.rotation = Quaternion.Slerp(kidStartRot, kidneyRot, t);
            }

            yield return null;
        }

        if (toSurgical != null)
        {
            toSurgical.transform.localPosition = origLocal;
            toSurgical.transform.localRotation = origRot;
            toSurgical.transform.localScale = origScale;
        }
        if (toKidney != null)
        {
            toKidney.transform.position = kidneyPos;
            toKidney.transform.rotation = kidneyRot;
        }

        SetHemisphereVisible(toSurgical, true);
        SetHemisphereVisible(toKidney, true);

        _currentState = targetState;
        _animating = false;

        bool allowStandaloneLayerPanel = !(SessionData.IsPlayMode && PlayRegionSearchController.Instance != null && PlayRegionSearchController.Instance.ShouldOwnLayerUI);
        if (AnatomyLayerPanel.Instance != null && allowStandaloneLayerPanel)
            AnatomyLayerPanel.Instance.Show();
        bool allowExplodedPanel = !(SessionData.IsPlayMode && !SessionData.IsTutorialMode);
        if (ExplodedViewController.Instance != null && allowExplodedPanel)
            ExplodedViewController.Instance.ShowPanel();

        Debug.Log($"[BrainManager] Hemisphere switch to {targetState} complete.");
    }

    public void ShowWholeBrain()
    {
        if (_currentState == ViewState.RegionSelected) return;

        // Both hemispheres back to surgical tray, fully visible
        ReturnToSurgicalTray(leftHemisphere, _leftHemiOrigParent, _leftHemiOrigLocal, _leftHemiOrigRotation, _leftHemiOrigScale);
        ReturnToSurgicalTray(rightHemisphere, _rightHemiOrigParent, _rightHemiOrigLocal, _rightHemiOrigRotation, _rightHemiOrigScale);

        _currentState = ViewState.WholeBrain;

        if (PlayRegionSearchController.Instance != null && PlayRegionSearchController.Instance.ShouldOwnLayerUI)
            PlayRegionSearchController.Instance.ClearStudySelection();
        else if (AnatomyLayerService.Instance != null)
            AnatomyLayerService.Instance.RestoreAll();
        if (AnatomyLayerPanel.Instance != null)
            AnatomyLayerPanel.Instance.Hide();
        if (ExplodedViewController.Instance != null)
        {
            ExplodedViewController.Instance.Collapse();
            ExplodedViewController.Instance.HidePanel();
        }
    }

    private void SetHemisphereVisible(GameObject hemi, bool visible)
    {
        if (hemi == null) return;
        foreach (var r in hemi.GetComponentsInChildren<Renderer>(true))
            if (r != null) r.enabled = visible;
    }

    // ===================== ROTATE =====================

    /// <summary>
    /// Rotate the currently focused anatomy around its own stable visual center.
    /// This avoids the "hemisphere flying away" problem that happens when a
    /// focused hemisphere is rotated around the full-brain root pivot.
    /// </summary>
    private void ApplyRotationKeepingCenterFixed(Quaternion deltaRotation)
    {
        Transform target = GetRotationTarget();
        if (target == null) return;

        Vector3 localCenter = ComputeStableLocalVisualCenter(target);
        Vector3 worldCenterBefore = target.TransformPoint(localCenter);

        target.rotation = deltaRotation * target.rotation;

        Vector3 worldCenterAfter = target.TransformPoint(localCenter);
        target.position += (worldCenterBefore - worldCenterAfter);

        if (target == brainRoot.transform)
            _lockedRootPosition = target.position;
    }

    Transform GetRotationTarget()
    {
        if (_currentState == ViewState.LeftFocused && leftHemisphere != null)
            return leftHemisphere.transform;
        if (_currentState == ViewState.RightFocused && rightHemisphere != null)
            return rightHemisphere.transform;
        return brainRoot != null ? brainRoot.transform : null;
    }

    Vector3 ComputeStableLocalVisualCenter(Transform target)
    {
        Bounds b = default;
        bool found = false;

        foreach (var r in target.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || !r.enabled) continue;
            if (!TryGetRendererLocalBounds(r, out Bounds rendererLocalBounds)) continue;

            Matrix4x4 toTarget = target.worldToLocalMatrix * r.transform.localToWorldMatrix;
            Vector3 min = rendererLocalBounds.min;
            Vector3 max = rendererLocalBounds.max;

            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            foreach (var corner in corners)
            {
                Vector3 p = toTarget.MultiplyPoint3x4(corner);
                if (!found)
                {
                    b = new Bounds(p, Vector3.zero);
                    found = true;
                }
                else
                {
                    b.Encapsulate(p);
                }
            }
        }

        return found ? b.center : Vector3.zero;
    }

    bool TryGetRendererLocalBounds(Renderer r, out Bounds bounds)
    {
        if (r is SkinnedMeshRenderer smr)
        {
            bounds = smr.localBounds;
            return true;
        }

        var mf = r.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            bounds = mf.sharedMesh.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    /// <summary>
    /// Start grab-to-rotate: user holds Grip (activate) on the brain. Only allowed after brain is sliced.
    /// Works with or without tweezers. Trigger = pick region when tweezers held.
    /// </summary>
    public void StartUserRotate(Transform interactorTransform)
    {
        if (interactorTransform == null) return;
        if (LabToolManager.Instance != null && !LabToolManager.Instance.glovesEquipped) return;
        if (LabToolManager.Instance != null && !LabToolManager.Instance.brainIsSplit) return;
        if (_currentState == ViewState.RegionSelected) return;

        _rotateInteractor = interactorTransform;
        _lastInteractorRotation = interactorTransform.rotation;
        _hasLastInteractorRotation = false;
        // Determine which hand so we can poll grip release (deactivate may not fire if ray left the brain)
        string name = interactorTransform.name ?? "";
        _rotateIsLeftHand = name.IndexOf("left", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// End grab-to-rotate when user releases trigger.
    /// </summary>
    public void EndUserRotate()
    {
        _rotateInteractor = null;
        _hasLastInteractorRotation = false;
    }

    /// <summary>
    /// Rotates the brain 15 degrees (e.g. from UI button). Kept for accessibility.
    /// </summary>
    public void RotateBrain()
    {
        if (brainRoot == null) return;
        if (LabToolManager.Instance != null && !LabToolManager.Instance.glovesEquipped) return;

        ApplyRotationKeepingCenterFixed(Quaternion.Euler(0f, 15f, 0f));
    }

    // ===================== ZOOM =====================

    public void ZoomIn()
    {
        if (LabToolManager.Instance != null && !LabToolManager.Instance.glovesEquipped) return;
        MoveXRRigToward(0.10f);
    }

    public void ZoomOut()
    {
        if (LabToolManager.Instance != null && !LabToolManager.Instance.glovesEquipped) return;
        MoveXRRigToward(-0.10f);
    }

    private void MoveXRRigToward(float distance)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Transform xrRig = cam.transform;
        while (xrRig.parent != null && xrRig.parent.name != "DontDestroyOnLoad")
            xrRig = xrRig.parent;

        // Zoom toward whatever the user is currently looking at
        Vector3 target;
        if (_currentState == ViewState.RegionSelected && _selectedRegion != null)
        {
            // Zoom toward selected region
            var rend = _selectedRegion.GetComponent<Renderer>();
            target = rend != null ? rend.bounds.center : _selectedRegion.transform.position;
        }
        else
        {
            // Zoom toward brain visual center
            target = ComputeVisualCenter();
        }

        Vector3 dir = (target - cam.transform.position).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = cam.transform.forward;
        xrRig.position += dir * distance;
    }

    // ===================== HOVER =====================

    public void OnRegionHoverEnter(BrainRegion region)
    {
        if (region == null || region.regionData == null) return;
        if (_currentState == ViewState.RegionSelected) return;
        regionUIController?.ShowHoverName(region.regionData.displayName);
    }

    public void OnRegionHoverExit(BrainRegion region)
    {
        if (_currentState == ViewState.RegionSelected) return;
        regionUIController?.ClearHoverName();
    }

    // ===================== SELECT REGION =====================

    public void OnRegionSelected(BrainRegion region)
    {
        if (region == null || region.regionData == null) return;
        StopCrossReferenceTargetBlink(region);
        if (_currentState == ViewState.RegionSelected) return;

        // Remember which state we were in so PutBack can restore it
        _stateBeforeRegionSelect = _currentState;

        _selectedRegion = region;
        _currentState = ViewState.RegionSelected;

        // Determine which hemisphere this region belongs to
        bool isLeftRegion = IsChildOf(region.gameObject, leftHemisphere);
        bool isRightRegion = IsChildOf(region.gameObject, rightHemisphere);

        // Hide the hemisphere the region came from (the region itself stays visible)
        // Keep the OTHER hemisphere visible in the kidney tray
        if (isLeftRegion)
        {
            SetVisExcept(leftHemisphere, region.gameObject);
            DisableInteractionExcept(leftHemisphere, region.gameObject);
        }
        else if (isRightRegion)
        {
            SetVisExcept(rightHemisphere, region.gameObject);
            DisableInteractionExcept(rightHemisphere, region.gameObject);
        }
        else
        {
            HideAllExcept(region.gameObject);
            DisableInteractionExcept(leftHemisphere, region.gameObject);
            DisableInteractionExcept(rightHemisphere, region.gameObject);
        }

        // Freeze physics
        var rb = region.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

        // Add slow in-place auto-rotation
        _activeInspector = region.gameObject.GetComponent<RegionInspector>();
        if (_activeInspector == null)
            _activeInspector = region.gameObject.AddComponent<RegionInspector>();
        _activeInspector.StartInspecting();

        // Show details panel and comparison
        regionUIController?.ClearHoverName();
        regionUIController?.ShowRegionDetails(region.regionData);



        if (!_firstRegionExtracted)
        {
            _firstRegionExtracted = true;
            if (TaskTimerManager.Instance != null)
                TaskTimerManager.Instance.OnFirstRegionExtracted();
        }

        Debug.Log($"[BrainManager] Selected region: {region.regionData.displayName} " +
                  $"(from {(isLeftRegion ? "Left" : isRightRegion ? "Right" : "Unknown")} hemisphere)");
    }

    /// <summary>Check if a GameObject is a child of another.</summary>
    private bool IsChildOf(GameObject child, GameObject parent)
    {
        if (child == null || parent == null) return false;
        return child.transform.IsChildOf(parent.transform);
    }

    public bool IsRegionInLeftHemisphere(BrainRegion region)
    {
        return region != null && IsChildOf(region.gameObject, leftHemisphere);
    }

    public bool IsRegionInRightHemisphere(BrainRegion region)
    {
        return region != null && IsChildOf(region.gameObject, rightHemisphere);
    }

    // ===================== CROSS-REFERENCE NAVIGATION HELPERS =====================
    // Used by RegionCrossReferenceNavigation. Additive only — no behaviour
    // change to existing code paths.

    /// <summary>Read-only view of the current dissection state.</summary>
    public ViewState BrainViewState => _currentState;

    /// <summary>
    /// If <paramref name="target"/> belongs to a hemisphere that is currently
    /// hidden by a focused-view (the *other* hemisphere is on the surgical
    /// tray), trigger the existing hemisphere-switch animation so the target
    /// becomes visible. Returns true when an animation was started so the
    /// caller can wait <see cref="hemiMoveAnimDuration"/> before highlighting.
    /// </summary>
    public bool BeginHemisphereSwitchForCrossReference(BrainRegion target)
    {
        if (target == null) return false;
        bool isLeft = IsRegionInLeftHemisphere(target);
        bool isRight = IsRegionInRightHemisphere(target);

        if (_currentState == ViewState.LeftFocused && isRight)
        {
            ShowRightHemisphere();
            return true;
        }
        if (_currentState == ViewState.RightFocused && isLeft)
        {
            ShowLeftHemisphere();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Briefly highlight a region — used by cross-reference navigation to
    /// point a learner at "the other place" they just clicked. Does not
    /// touch global opacity or study-mode state, so it never fights the
    /// PlayRegionSearchController spotlight or the layer service.
    /// </summary>
    public void PulseRegionHighlight(BrainRegion region, float holdSeconds = 1.5f)
    {
        if (region == null) return;
        StartCoroutine(PulseRegionHighlightCoroutine(region, holdSeconds));
    }

    /// <summary>
    /// Blink a cross-reference target so the learner can find it after
    /// clicking a pill. The blink stops when this exact region is selected,
    /// when a new cross-reference target starts blinking, or when the max
    /// duration expires.
    /// </summary>
    public void StartCrossReferenceTargetBlink(BrainRegion region, float maxDurationSeconds = 5f)
    {
        if (region == null) return;

        StopCrossReferenceTargetBlink();
        _crossReferenceBlinkTarget = region;
        _crossReferenceBlinkCoroutine = StartCoroutine(CrossReferenceTargetBlinkCoroutine(region, Mathf.Max(0.5f, maxDurationSeconds)));
    }

    void StopCrossReferenceTargetBlink(BrainRegion selectedRegion = null)
    {
        if (selectedRegion != null && _crossReferenceBlinkTarget != selectedRegion) return;

        if (_crossReferenceBlinkCoroutine != null)
        {
            StopCoroutine(_crossReferenceBlinkCoroutine);
            _crossReferenceBlinkCoroutine = null;
        }

        if (_crossReferenceBlinkTarget != null)
            _crossReferenceBlinkTarget.SetHighlight(false);
        _crossReferenceBlinkTarget = null;
    }

    IEnumerator CrossReferenceTargetBlinkCoroutine(BrainRegion region, float maxDurationSeconds)
    {
        const float onSeconds = 0.35f;
        const float offSeconds = 0.25f;

        float elapsed = 0f;
        bool on = false;

        while (region != null && elapsed < maxDurationSeconds && _selectedRegion != region)
        {
            on = !on;
            region.SetHighlight(on);

            float wait = on ? onSeconds : offSeconds;
            float end = Time.unscaledTime + wait;
            while (Time.unscaledTime < end)
            {
                if (region == null || _selectedRegion == region) break;
                yield return null;
            }

            elapsed += wait;
        }

        if (region != null) region.SetHighlight(false);
        if (_crossReferenceBlinkTarget == region)
        {
            _crossReferenceBlinkTarget = null;
            _crossReferenceBlinkCoroutine = null;
        }
    }

    private IEnumerator PulseRegionHighlightCoroutine(BrainRegion region, float holdSeconds)
    {
        if (region == null) yield break;
        region.SetHighlight(true);
        float elapsed = 0f;
        while (elapsed < holdSeconds)
        {
            if (region == null) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (region != null) region.SetHighlight(false);
    }

    // ===================== OPACITY =====================

    public void SetBrainOpacity(float opacity, bool forceForStudy = false)
    {
        if (!forceForStudy && LabToolManager.Instance != null && !LabToolManager.Instance.glovesEquipped) return;

        _brainOpacity = Mathf.Clamp01(opacity);
        if (!_materialsCached) CacheMaterials();

        foreach (var md in _materialCache)
        {
            if (md.material == null || md.renderer == null) continue;

            if (_currentState == ViewState.RegionSelected && _selectedRegion != null)
            {
                if (md.renderer.gameObject == _selectedRegion.gameObject) continue;
                if (md.renderer.transform.IsChildOf(_selectedRegion.transform)) continue;
            }

            Color c = md.originalColor;
            c.a = _brainOpacity;
            md.material.color = c;

            if (_brainOpacity < 0.99f)
            {
                if (md.material.HasProperty("_Surface")) md.material.SetFloat("_Surface", 1);
                md.material.SetOverrideTag("RenderType", "Transparent");
                if (md.material.HasProperty("_SrcBlend"))
                    md.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (md.material.HasProperty("_DstBlend"))
                    md.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (md.material.HasProperty("_ZWrite"))
                    md.material.SetInt("_ZWrite", 0);
                md.material.renderQueue = 3000;
                md.material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                md.material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            }
            else
            {
                if (md.material.HasProperty("_Surface")) md.material.SetFloat("_Surface", 0);
                md.material.SetOverrideTag("RenderType", "Opaque");
                if (md.material.HasProperty("_SrcBlend"))
                    md.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                if (md.material.HasProperty("_DstBlend"))
                    md.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                if (md.material.HasProperty("_ZWrite"))
                    md.material.SetInt("_ZWrite", 1);
                md.material.renderQueue = md.originalRenderQueue;
                md.material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                md.material.EnableKeyword("_SURFACE_TYPE_OPAQUE");
            }
        }

        if (_currentState == ViewState.RegionSelected && _selectedRegion != null && _brainOpacity > 0.01f)
        {
            ShowAllRegions();
            EnsureRegionFullyVisible(_selectedRegion);
        }

        if (_currentState == ViewState.RegionSelected && _selectedRegion != null)
            EnsureRegionFullyVisible(_selectedRegion);
    }

    private void EnsureRegionFullyVisible(BrainRegion region)
    {
        var rend = region.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.enabled = true;
            foreach (var mat in rend.materials)
            {
                if (mat == null) continue;
                Color c = mat.color; c.a = 1f; mat.color = c;
            }
        }
    }

    // ===================== PUT BACK REGION =====================

    /// <summary>
    /// Returns the selected region to its hemisphere. Does NOT reset the brain.
    /// After put-back, both hemispheres are visible in the kidney tray.
    /// The user can continue selecting other regions.
    /// </summary>
    public void PutBackRegion()
    {
        if (_currentState != ViewState.RegionSelected || _selectedRegion == null)
            return;

        Debug.Log("[BrainManager] PutBackRegion called");

        // Stop inspector rotation
        if (_activeInspector != null)
        {
            Destroy(_activeInspector);
            _activeInspector = null;
        }

        // Remove highlight
        _selectedRegion.SetHighlight(false);

        // Restore the region's original local transform (undoes RegionInspector auto-rotation)
        _selectedRegion.RestoreOriginalTransform();

        // Determine which hemisphere the region belongs to
        bool isLeftRegion = IsChildOf(_selectedRegion.gameObject, leftHemisphere);

        // Clear selection
        _selectedRegion = null;

        // Re-enable colliders on all regions so they become interactive again
        EnableAllInteraction();

        // Restore visibility of the hemisphere the region came from
        if (isLeftRegion)
            SetHemisphereVisible(leftHemisphere, true);
        else
            SetHemisphereVisible(rightHemisphere, true);

        // The OTHER hemisphere is already visible in the kidney tray (we never touched it)

        // Return to the state we were in before selecting the region
        // (both hemispheres in kidney tray, or one on surgical tray)
        _currentState = _stateBeforeRegionSelect;

        // If we were in a hemisphere-focused view, make sure that hemisphere is on surgical tray
        // and the other is in kidney tray
        if (_currentState == ViewState.LeftFocused)
        {
            ReturnToSurgicalTray(leftHemisphere, _leftHemiOrigParent, _leftHemiOrigLocal, _leftHemiOrigRotation, _leftHemiOrigScale);
            SendToKidneyTray(rightHemisphere, _rightKidneyPos, _rightKidneyRot);
        }
        else if (_currentState == ViewState.RightFocused)
        {
            ReturnToSurgicalTray(rightHemisphere, _rightHemiOrigParent, _rightHemiOrigLocal, _rightHemiOrigRotation, _rightHemiOrigScale);
            SendToKidneyTray(leftHemisphere, _leftKidneyPos, _leftKidneyRot);
        }
        else
        {
            // Both in kidney tray (came from WholeBrain state or directly from split)
            SendToKidneyTray(leftHemisphere, _leftKidneyPos, _leftKidneyRot);
            SendToKidneyTray(rightHemisphere, _rightKidneyPos, _rightKidneyRot);
        }

        // Reset opacity
        _brainOpacity = 1f;
        SetBrainOpacity(1f);
        if (regionUIController != null && regionUIController.opacitySlider != null)
            regionUIController.opacitySlider.value = 1f;

        regionUIController?.ClearHoverName();
        regionUIController?.HideRegionDetails();

        if (PlayRegionSearchController.Instance != null && PlayRegionSearchController.Instance.ShouldOwnLayerUI)
            PlayRegionSearchController.Instance.OnBrainViewStateReset();



        Debug.Log("[BrainManager] Region put back. Both hemispheres visible, dissection continues.");

        OnInspectionEnded?.Invoke();
    }

    // ===================== RESET (FULL) =====================

    public void ResetBrain()
    {
        Debug.Log("[BrainManager] ResetBrain called");

        bool hadInspectedRegion = _selectedRegion != null;
        if (_selectedRegion != null)
        {
            if (_activeInspector != null)
            {
                Destroy(_activeInspector);
                _activeInspector = null;
            }
            _selectedRegion.SetHighlight(false);
            _selectedRegion = null;
        }

        // Reset hemispheres to original local positions
        UndoBrainSplit();

        // Reset root to ORIGINAL position and rotation
        if (brainRoot != null)
        {
            brainRoot.transform.position = _originalRootPosition;
            brainRoot.transform.rotation = _originalRootRotation;
            _lockedRootPosition = _originalRootPosition;
        }

        EnableAllInteraction();
        ShowAllRegions();
        _currentState = ViewState.WholeBrain;

        // Reset opacity
        _brainOpacity = 1f;
        SetBrainOpacity(1f);
        if (regionUIController != null && regionUIController.opacitySlider != null)
            regionUIController.opacitySlider.value = 1f;

        regionUIController?.ClearHoverName();
        regionUIController?.HideRegionDetails();

        if (PlayRegionSearchController.Instance != null && PlayRegionSearchController.Instance.ShouldOwnLayerUI)
            PlayRegionSearchController.Instance.ClearStudySelection();
        else if (AnatomyLayerService.Instance != null)
            AnatomyLayerService.Instance.RestoreAll();
        if (AnatomyLayerPanel.Instance != null)
            AnatomyLayerPanel.Instance.Hide();
        if (ExplodedViewController.Instance != null)
        {
            ExplodedViewController.Instance.Collapse();
            ExplodedViewController.Instance.HidePanel();
        }

        if (hadInspectedRegion)
            OnInspectionEnded?.Invoke();
    }

    // ===================== VISIBILITY =====================

    private void HideAllExcept(GameObject keepVisible)
    {
        if (leftHemisphere != null) SetVisExcept(leftHemisphere, keepVisible);
        if (rightHemisphere != null) SetVisExcept(rightHemisphere, keepVisible);
    }

    private void SetVisExcept(GameObject hemi, GameObject keep)
    {
        foreach (var r in hemi.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            r.enabled = (r.gameObject == keep || r.transform.IsChildOf(keep.transform));
        }
    }

    /// <summary>
    /// Disable colliders on every child of a hemisphere except the kept
    /// region. This fully removes hidden regions from XR interaction so
    /// they cannot be hovered, selected, or display labels.
    /// </summary>
    private void DisableInteractionExcept(GameObject hemi, GameObject keep)
    {
        if (hemi == null) return;
        foreach (var col in hemi.GetComponentsInChildren<Collider>(true))
        {
            if (col == null) continue;
            bool isKept = col.gameObject == keep || col.transform.IsChildOf(keep.transform);
            if (!isKept) col.enabled = false;
        }
    }

    /// <summary>Re-enable every collider in both hemispheres.</summary>
    private void EnableAllInteraction()
    {
        EnableCollidersIn(leftHemisphere);
        EnableCollidersIn(rightHemisphere);
    }

    private void EnableCollidersIn(GameObject hemi)
    {
        if (hemi == null) return;
        foreach (var col in hemi.GetComponentsInChildren<Collider>(true))
            if (col != null) col.enabled = true;
    }

    private void ShowAllRegions()
    {
        SetHemisphereVisible(leftHemisphere, true);
        SetHemisphereVisible(rightHemisphere, true);
        EnableAllInteraction();
    }
}
