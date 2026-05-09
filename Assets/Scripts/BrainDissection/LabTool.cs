using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections;

/// <summary>
/// Attach to each lab tool (gloves, knife, tweezers).
///
/// All tools use XRSimpleInteractable (point + click).
///
/// Gloves:   Click to equip permanently. Object disappears.
/// Knife:    Click to pick up. Follows controller. Picking up tweezers drops it.
/// Tweezers: Click to pick up. Follows controller. Picking up knife drops it.
///
/// Only ONE tool (knife or tweezers) can be held at a time.
/// Reset Lab drops all tools back to their starting positions.
/// </summary>
public class LabTool : MonoBehaviour
{
    public enum ToolType { Gloves, Knife, Tweezers }

    [Header("Tool Configuration")]
    public ToolType toolType = ToolType.Gloves;

    [Header("Animation")]
    public float pickupAnimDuration = 0.35f;
    public float dropAnimDuration = 0.5f;

    private XRBaseInteractable _interactable;
    private bool _equipped;
    private ToolFollower _follower;
    private Coroutine _animCoroutine;

    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private bool _originalSaved;

    private void Awake()
    {
        _interactable = GetComponent<XRBaseInteractable>();
    }

    private void Start()
    {
        // Save original table position
        _originalPosition = transform.position;
        _originalRotation = transform.rotation;
        _originalSaved = true;
    }

    private void OnEnable()
    {
        if (_interactable == null) return;
        _interactable.selectEntered.AddListener(OnSelected);
    }

    private void OnDisable()
    {
        if (_interactable == null) return;
        _interactable.selectEntered.RemoveListener(OnSelected);
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        var mgr = LabToolManager.Instance;
        if (mgr == null) { Debug.LogWarning("[LabTool] No LabToolManager in scene!"); return; }

        switch (toolType)
        {
            case ToolType.Gloves:
                if (!mgr.handsWashed)
                {
                    Debug.Log("[LabTool] Cannot equip gloves -- hands not washed.");
                    if (mgr.regionUIController != null)
                        mgr.regionUIController.ShowHoverName("Wash your hands at the sink first.");
                    return;
                }
                mgr.EquipGloves();
                HideForEquip();
                break;

            case ToolType.Knife:
                if (!_equipped)
                {
                    // Gate: gloves must be equipped first
                    if (!mgr.glovesEquipped)
                    {
                        Debug.Log("[LabTool] Cannot pick up knife -- gloves not equipped.");
                        if (mgr.regionUIController != null)
                            mgr.regionUIController.ShowHoverName("Equip your gloves first!");
                        return;
                    }
                    // Drop any other held tool first
                    mgr.DropAllHeldTools(this);
                    _equipped = true;
                    mgr.SetKnifeHeld(true);
                    AttachToController(args);
                    Debug.Log("[LabTool] Knife picked up.");
                }
                break;

            case ToolType.Tweezers:
                if (!_equipped)
                {
                    // Gate: brain must be split before tweezers can be used
                    if (!mgr.brainIsSplit)
                    {
                        Debug.Log("[LabTool] Cannot pick up tweezers -- brain not yet split.");
                        if (mgr.regionUIController != null)
                            mgr.regionUIController.ShowHoverName("Split the brain first before using tweezers.");
                        return;
                    }
                    if (SessionData.IsTutorialMode && mgr.brainManager != null && !mgr.brainManager.IsHemisphereSelected)
                    {
                        Debug.Log("[LabTool] Cannot pick up tweezers in Tutorial -- hemisphere not yet selected.");
                        if (mgr.regionUIController != null)
                            mgr.regionUIController.ShowHoverName("Select the requested hemisphere first, then pick up the tweezers.");
                        return;
                    }
                    // Drop any other held tool first
                    mgr.DropAllHeldTools(this);
                    _equipped = true;
                    mgr.SetTweezersHeld(true);
                    AttachToController(args);
                    Debug.Log("[LabTool] Tweezers picked up.");
                }
                break;
        }
    }

    /// <summary>
    /// Makes the tool follow the controller that selected it.
    /// Does NOT parent to the XR rig.
    /// </summary>
    private void AttachToController(SelectEnterEventArgs args)
    {
        Transform ctrlTransform = null;
        if (args.interactorObject != null)
        {
            var interactorMB = args.interactorObject as MonoBehaviour;
            if (interactorMB != null)
            {
                // Walk up to find the controller root (under Camera Offset)
                Transform current = interactorMB.transform;
                while (current.parent != null)
                {
                    string n = current.parent.name.ToLower();
                    if (n.Contains("camera offset") || n.Contains("xr origin"))
                        break;
                    current = current.parent;
                }
                ctrlTransform = current;
            }
        }

        if (ctrlTransform == null)
        {
            Debug.LogWarning("[LabTool] Could not find controller transform.");
            return;
        }

        // Add ToolFollower
        _follower = gameObject.AddComponent<ToolFollower>();
        _follower.targetController = ctrlTransform;

        if (toolType == ToolType.Knife)
        {
            _follower.positionOffset = new Vector3(0f, -0.02f, 0.10f);
            _follower.rotationOffset = new Vector3(-30f, 0f, 0f);
        }
        else
        {
            _follower.positionOffset = new Vector3(0f, -0.03f, 0.08f);
            _follower.rotationOffset = new Vector3(-45f, 0f, 0f);
        }

        // For knife: keep collider as trigger for cut zone detection
        // For tweezers: disable collider entirely
        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            if (toolType == ToolType.Knife)
                col.isTrigger = true;
            else
                col.enabled = false;
        }

        // Disable interactable so it can't be re-clicked while held
        if (_interactable != null)
            _interactable.enabled = false;

        // Remove Rigidbody
        var rb = GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);
    }

    /// <summary>
    /// Drops the tool back to its original table position.
    /// Called by LabToolManager when switching tools or resetting.
    /// </summary>
    public void DropTool()
    {
        if (!_equipped) return;
        _equipped = false;

        if (_follower != null)
        {
            Destroy(_follower);
            _follower = null;
        }

        var mgr = LabToolManager.Instance;
        if (mgr != null)
        {
            if (toolType == ToolType.Knife) mgr.SetKnifeHeld(false);
            if (toolType == ToolType.Tweezers) mgr.SetTweezersHeld(false);
        }

        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimateDropToTable());

        Debug.Log($"[LabTool] {toolType} dropping back to table.");
    }

    private IEnumerator AnimateDropToTable()
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 endPos = _originalSaved ? _originalPosition : startPos;
        Quaternion endRot = _originalSaved ? _originalRotation : startRot;

        float elapsed = 0f;
        while (elapsed < dropAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dropAnimDuration);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        transform.position = endPos;
        transform.rotation = endRot;

        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            col.enabled = true;
            col.isTrigger = false;
        }

        if (_interactable != null)
            _interactable.enabled = true;
    }

    /// <summary>
    /// Full reset for lab reset button.
    /// </summary>
    public void ResetTool()
    {
        if (toolType == ToolType.Gloves)
        {
            ShowForReequip();
            return;
        }
        DropTool();
    }

    /// <summary>Hides the gloves visually but keeps them in the scene so
    /// they can reappear on lab reset for a second equip cycle.</summary>
    private void HideForEquip()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = false;
        foreach (var col in GetComponentsInChildren<Collider>(true))
            col.enabled = false;
        if (_interactable != null)
            _interactable.enabled = false;
    }

    /// <summary>Reverses HideForEquip so the gloves can be picked up again.</summary>
    private void ShowForReequip()
    {
        if (_originalSaved)
        {
            transform.position = _originalPosition;
            transform.rotation = _originalRotation;
        }

        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = true;
        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            col.enabled = true;
            col.isTrigger = false;
        }
        if (_interactable != null)
            _interactable.enabled = true;
    }
}
