using UnityEngine;
using System;

/// <summary>
/// Singleton manager tracking lab tool state.
/// Gates brain interactions behind tool requirements:
///   - Gloves:   required for ALL brain interaction
///   - Knife:    required to split brain into hemispheres
///   - Tweezers: required to select individual brain regions
/// </summary>
public class LabToolManager : MonoBehaviour
{
    // ========================= SINGLETON =========================
    public static LabToolManager Instance { get; private set; }

    // ========================= REFERENCES =========================
    [Header("References")]
    public BrainManager brainManager;
    public RegionUIController regionUIController;
    public GloveEquipper gloveEquipper;
    public BrainCutZone cutZone;

    // ========================= STATE (visible in Inspector at runtime for debugging/screenshots) =========================
    [Header("Runtime State")]
    public bool handsWashed;
    public bool glovesEquipped;
    public bool isHoldingKnife;
    public bool isHoldingTweezers;
    public bool brainIsSplit;

    // ========================= EVENTS =========================
    public event Action OnHandsWashed;
    public event Action OnGlovesEquipped;
    public event Action OnGlovesUnequipped;
    public event Action OnBrainSplit;
    public event Action OnLabReset;

    // ========================= LIFECYCLE =========================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        UpdateUI();
    }

    // ========================= TOOL ACTIONS =========================

    /// <summary>Called by HandWashStation when the player completes the wash interaction.</summary>
    public void NotifyHandsWashed()
    {
        if (handsWashed) return;
        handsWashed = true;
        OnHandsWashed?.Invoke();
        UpdateUI();
        Debug.Log("[LabToolManager] Hands washed.");
    }

    /// <summary>Called by LabTool when gloves are grabbed. One-time permanent equip.</summary>
    public void EquipGloves()
    {
        if (glovesEquipped) return;
        if (!handsWashed)
        {
            if (regionUIController != null)
                regionUIController.SetStatusMessage("Wash your hands at the sink before putting on gloves.");
            return;
        }
        glovesEquipped = true;
        gloveEquipper?.EquipGloves();
        OnGlovesEquipped?.Invoke();
        UpdateUI();
        Debug.Log("[LabToolManager] Gloves equipped!");
    }

    /// <summary>Called by LabTool when knife is picked up / dropped.</summary>
    public void SetKnifeHeld(bool held)
    {
        isHoldingKnife = held;
        if (held && TaskTimerManager.Instance != null)
            TaskTimerManager.Instance.OnKnifeEquipped();
        UpdateUI();
    }

    /// <summary>Called by LabTool when tweezers are picked up / dropped.</summary>
    public void SetTweezersHeld(bool held)
    {
        isHoldingTweezers = held;
        if (held && TaskTimerManager.Instance != null)
            TaskTimerManager.Instance.OnTweezersEquipped();
        UpdateUI();
    }

    /// <summary>
    /// Drops all currently held tools EXCEPT the one being picked up.
    /// Called by LabTool before equipping a new tool, so only one is held at a time.
    /// </summary>
    public void DropAllHeldTools(LabTool except)
    {
        foreach (var tool in FindObjectsByType<LabTool>(FindObjectsSortMode.None))
        {
            if (tool == except) continue;
            if (tool.toolType == LabTool.ToolType.Gloves) continue;
            tool.DropTool();
        }
    }

    /// <summary>Called by BrainCutZone when the knife successfully cuts the brain.</summary>
    public void NotifyBrainSplit()
    {
        if (!glovesEquipped || brainIsSplit) return;
        brainIsSplit = true;
        brainManager?.PerformBrainSplit();
        OnBrainSplit?.Invoke();
        UpdateUI();
        Debug.Log("[LabToolManager] Brain has been split!");
    }

    /// <summary>Called by UI Reset button. Restores brain to original unsplit state.</summary>
    public void ResetLab()
    {
        bool wasGloved = glovesEquipped;

        brainIsSplit = false;
        isHoldingKnife = false;
        isHoldingTweezers = false;
        handsWashed = false;
        glovesEquipped = false;
        gloveEquipper?.ResetEquipped();

        brainManager?.ResetBrain();
        cutZone?.ResetCutZone();

        // Reset any equipped tools: gloves reappear on the table, others drop back.
        foreach (var tool in FindObjectsByType<LabTool>(FindObjectsSortMode.None))
        {
            tool.ResetTool();
        }

        if (wasGloved)
            OnGlovesUnequipped?.Invoke();

        OnLabReset?.Invoke();
        UpdateUI();
        Debug.Log("[LabToolManager] Lab reset.");
    }

    // ========================= UI STATE MACHINE =========================

    private void UpdateUI()
    {
        if (regionUIController == null) return;

        // ----- Tool status bar -----
        string g = glovesEquipped ? "ON" : "--";
        string k = isHoldingKnife ? "HELD" : "--";
        string t = isHoldingTweezers ? "HELD" : "--";
        regionUIController.SetToolStatus(
            $"Gloves: {g}     Knife: {k}     Tweezers: {t}");

        // ----- Status message + panel visibility -----
        if (!handsWashed)
        {
            regionUIController.SetStatusMessage(
                "Step 1: Wash your hands at the sink to begin.");
            regionUIController.ShowHemisphereButtons(false);
            regionUIController.ShowControlButtons(false);
        }
        else if (!glovesEquipped)
        {
            regionUIController.SetStatusMessage(
                "Hands clean. Now equip your gloves to begin the lab.");
            regionUIController.ShowHemisphereButtons(false);
            regionUIController.ShowControlButtons(false);
        }
        else if (!brainIsSplit)
        {
            regionUIController.SetStatusMessage(
                "Use the knife through the red line to split the brain. Rotate after splitting.");
            regionUIController.ShowHemisphereButtons(false);
            regionUIController.ShowControlButtons(true);
        }
        else
        {
            string msg = isHoldingTweezers
                ? "Grip = rotate brain. Trigger on a region = inspect it."
                : "Select a hemisphere to view. Grip on brain to rotate. Grab tweezers to pick regions.";
            regionUIController.SetStatusMessage(msg);
            regionUIController.ShowHemisphereButtons(true);
            regionUIController.ShowControlButtons(true);
        }
    }
}
