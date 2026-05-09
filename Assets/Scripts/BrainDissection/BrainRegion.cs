using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Attach to each brain region GameObject. Holds RegionData and forwards hover/select/activate
/// to BrainManager.
///
/// Keybind mapping (while holding tweezers or not):
///   - Grip (Activate):  rotate the whole brain (only after brain is sliced)
///   - Trigger (Select): pick region for inspection (only when tweezers held)
/// </summary>
public class BrainRegion : MonoBehaviour
{
    [Tooltip("Display name and description for this region")]
    public RegionData regionData;

    [Tooltip("Optional: assign BrainManager. If not set, will be found in scene.")]
    public BrainManager brainManager;

    private XRBaseInteractable _interactable;
    private Renderer _renderer;
    private Color _originalColor;
    private string _colorProperty;
    private bool _startedBrainRotate;

    // Study tint: a persistent compare/study color applied independently of the
    // transient hover yellow. While a study tint is active, hover state is
    // skipped so the compare colors stay readable.
    private bool _studyTintActive;
    private Color _studyTintColor;

    // Saved original transform so PutBackRegion can restore it after RegionInspector rotates it
    [HideInInspector] public Vector3 originalLocalPosition;
    [HideInInspector] public Quaternion originalLocalRotation;
    [HideInInspector] public Vector3 originalLocalScale;
    private bool _transformSaved;

    private void Awake()
    {
        _interactable = GetComponent<XRBaseInteractable>();
        if (_interactable == null)
            _interactable = GetComponentInChildren<XRBaseInteractable>();

        if (brainManager == null)
            brainManager = FindFirstObjectByType<BrainManager>();

        _renderer = GetComponent<Renderer>();
        if (_renderer != null && _renderer.material != null)
        {
            var mat = _renderer.material;
            if (mat.HasProperty("_BaseColor"))
                _colorProperty = "_BaseColor";
            else if (mat.HasProperty("_Color"))
                _colorProperty = "_Color";

            if (_colorProperty != null)
                _originalColor = mat.GetColor(_colorProperty);
        }
    }

    private void Start()
    {
        // Save the original local transform at startup so we can restore after inspection
        if (!_transformSaved)
        {
            originalLocalPosition = transform.localPosition;
            originalLocalRotation = transform.localRotation;
            originalLocalScale = transform.localScale;
            _transformSaved = true;
        }
    }

    /// <summary>Restore this region to its original local transform (undoes RegionInspector rotation).</summary>
    public void RestoreOriginalTransform()
    {
        if (!_transformSaved) return;
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
        transform.localScale = originalLocalScale;
    }

    private void OnEnable()
    {
        if (_interactable == null) return;
        _interactable.hoverEntered.AddListener(OnHoverEntered);
        _interactable.hoverExited.AddListener(OnHoverExited);
        _interactable.selectEntered.AddListener(OnSelectEntered);
        _interactable.selectExited.AddListener(OnSelectExited);
        _interactable.activated.AddListener(OnActivated);
        _interactable.deactivated.AddListener(OnDeactivated);
    }

    private void OnDisable()
    {
        if (_interactable == null) return;
        _interactable.hoverEntered.RemoveListener(OnHoverEntered);
        _interactable.hoverExited.RemoveListener(OnHoverExited);
        _interactable.selectEntered.RemoveListener(OnSelectEntered);
        _interactable.selectExited.RemoveListener(OnSelectExited);
        _interactable.activated.RemoveListener(OnActivated);
        _interactable.deactivated.RemoveListener(OnDeactivated);
        if (_startedBrainRotate && brainManager != null)
        {
            brainManager.EndUserRotate();
            _startedBrainRotate = false;
        }
    }

    // ========================= EVENT HANDLERS =========================

    public static event System.Action<BrainRegion> OnAnyHoverEntered;
    public static event System.Action<BrainRegion> OnAnyHoverExited;

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (!AreGlovesEquipped()) return;
        if (brainManager != null && brainManager.IsInspectingRegion) return;

        var ld = LiveDissectionManager.Instance;
        if (ld != null && ld.IsLiveDissectionActive)
        {
            OnAnyHoverEntered?.Invoke(this);
            Transform interactorT = (args.interactorObject as MonoBehaviour)?.transform;
            HapticFeedback.LightPulse(interactorT);
            return;
        }

        // Tutorial: priority focus mode. When a target region is highlighted,
        // hovering on anything else is silently ignored — no label, no
        // highlight — so the player only ever sees the red target. If the
        // ray happens to hit the highlighted region directly, fall through
        // and run the normal hover behavior.
        var tut = TutorialManager.Instance;
        if (tut != null && tut.IsTutorialActive && tut.AllowedRegion != null && tut.AllowedRegion != this)
        {
            return;
        }

        SetHighlight(true);
        OnAnyHoverEntered?.Invoke(this);
        brainManager?.OnRegionHoverEnter(this);

        Transform interactorTN = (args.interactorObject as MonoBehaviour)?.transform;
        HapticFeedback.LightPulse(interactorTN);

        if (regionData != null && WorldSpaceHoverLabel.Instance != null)
            WorldSpaceHoverLabel.Instance.Show(regionData.displayName, transform);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        if (brainManager != null && brainManager.IsInspectingRegion) return;

        var ld = LiveDissectionManager.Instance;
        if (ld != null && ld.IsLiveDissectionActive)
        {
            OnAnyHoverExited?.Invoke(this);
            return;
        }

        // See OnHoverEntered: non-highlighted regions are inert during the
        // tutorial select step, so we never started a hover effect for them.
        var tut = TutorialManager.Instance;
        if (tut != null && tut.IsTutorialActive && tut.AllowedRegion != null && tut.AllowedRegion != this)
        {
            return;
        }

        SetHighlight(false);
        OnAnyHoverExited?.Invoke(this);
        brainManager?.OnRegionHoverExit(this);

        if (WorldSpaceHoverLabel.Instance != null)
            WorldSpaceHoverLabel.Instance.Hide();
    }

    // Trigger (Select): pick region for inspection — only when tweezers held
    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (!AreGlovesEquipped())
        {
            ShowToolMessage("Please equip your gloves first.");
            return;
        }
        if (!AreTweezersHeld())
        {
            ShowToolMessage("Hold the tweezers to select a brain region.");
            return;
        }

        // Tutorial: priority focus mode. If the player aims and pulls the
        // trigger anywhere on the brain, auto-redirect the extraction to the
        // currently highlighted target region. This makes deeply-embedded
        // small regions reliably selectable even when a larger structure
        // happens to be in front of the ray.
        var tut = TutorialManager.Instance;
        if (tut != null && tut.IsTutorialActive && tut.AllowedRegion != null && tut.AllowedRegion != this)
        {
            var redirectTarget = tut.AllowedRegion;
            Transform redirectT = (args.interactorObject as MonoBehaviour)?.transform;
            HapticFeedback.StrongPulse(redirectT);
            if (WorldSpaceHoverLabel.Instance != null)
                WorldSpaceHoverLabel.Instance.Hide();
            redirectTarget.SetHighlight(false);
            if (brainManager != null)
                brainManager.OnRegionSelected(redirectTarget);
            else if (redirectTarget.brainManager != null)
                redirectTarget.brainManager.OnRegionSelected(redirectTarget);
            return;
        }

        // Live Dissection gate: forward selection to LiveDissectionManager
        var ld = LiveDissectionManager.Instance;
        if (ld != null && ld.IsLiveDissectionActive)
        {
            ld.OnRegionSelected(this);
            return;
        }

        SetHighlight(false);
        if (WorldSpaceHoverLabel.Instance != null)
            WorldSpaceHoverLabel.Instance.Hide();

        Transform selectT = (args.interactorObject as MonoBehaviour)?.transform;
        HapticFeedback.StrongPulse(selectT);

        brainManager?.OnRegionSelected(this);
    }

    private void OnSelectExited(SelectExitEventArgs args) { }

    // Grip (Activate): rotate the whole brain — only after brain is sliced; works with or without tweezers
    private void OnActivated(ActivateEventArgs args)
    {
        if (!AreGlovesEquipped()) return;
        if (LabToolManager.Instance != null && !LabToolManager.Instance.brainIsSplit) return;


        Transform interactorTransform = (args.interactorObject as MonoBehaviour)?.transform;
        if (interactorTransform == null) return;

        var ld = LiveDissectionManager.Instance;
        if (ld != null && ld.IsLiveDissectionActive)
            return;

        if (brainManager != null)
        {
            brainManager.StartUserRotate(interactorTransform);
            _startedBrainRotate = true;
        }
    }

    private void OnDeactivated(DeactivateEventArgs args)
    {
        if (_startedBrainRotate)
        {
            brainManager?.EndUserRotate();
            _startedBrainRotate = false;
        }
    }

    // ========================= TOOL CHECKS =========================

    private bool AreGlovesEquipped()
    {
        return LabToolManager.Instance != null && LabToolManager.Instance.glovesEquipped;
    }

    private bool AreTweezersHeld()
    {
        return LabToolManager.Instance != null && LabToolManager.Instance.isHoldingTweezers;
    }

    private void ShowToolMessage(string msg)
    {
        if (brainManager != null && brainManager.regionUIController != null)
            brainManager.regionUIController.ShowHoverName(msg);
    }

    // ========================= VISUALS =========================

    /// <summary>Highlight region with yellow tint and emissive glow (hover).</summary>
    public void SetHighlight(bool on)
    {
        if (_renderer == null || _colorProperty == null) return;
        // While a study tint is active (compare mode), the hover yellow is
        // suppressed so the compare colors remain readable. Turning hover off
        // simply restores the active study tint instead of the original color.
        if (_studyTintActive)
        {
            ApplyStudyTintToMaterial();
            return;
        }
        var mat = _renderer.material;
        if (on)
        {
            mat.SetColor(_colorProperty, new Color(1f, 0.9f, 0.3f, 1f));
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.8f, 0.65f, 0.15f, 1f) * 0.5f);
            }
        }
        else
        {
            mat.SetColor(_colorProperty, _originalColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", Color.black);
                mat.DisableKeyword("_EMISSION");
            }
        }
    }

    /// <summary>
    /// Apply a persistent study tint (used by compare mode). Independent from
    /// hover highlight: stays on through hover events until cleared explicitly.
    /// Pass <paramref name="on"/> = false to restore the original color.
    /// </summary>
    public void SetStudyTint(bool on, Color tint = default)
    {
        if (_renderer == null || _colorProperty == null) return;
        if (on)
        {
            _studyTintActive = true;
            _studyTintColor = tint;
            ApplyStudyTintToMaterial();
        }
        else
        {
            _studyTintActive = false;
            var mat = _renderer.material;
            mat.SetColor(_colorProperty, _originalColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", Color.black);
                mat.DisableKeyword("_EMISSION");
            }
        }
    }

    private void ApplyStudyTintToMaterial()
    {
        if (_renderer == null || _colorProperty == null) return;
        var mat = _renderer.material;
        mat.SetColor(_colorProperty, _studyTintColor);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            // Use the same hue as the tint at moderate intensity for a clean glow.
            mat.SetColor("_EmissionColor", _studyTintColor * 0.55f);
        }
    }


    /// <summary>Re-cache the original color (e.g. after materials are restored).</summary>
    public void RefreshOriginalColor()
    {
        if (_renderer != null && _colorProperty != null)
            _originalColor = _renderer.material.GetColor(_colorProperty);
    }
}
