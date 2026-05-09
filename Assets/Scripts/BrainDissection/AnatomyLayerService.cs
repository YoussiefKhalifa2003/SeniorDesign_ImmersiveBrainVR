using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Runtime service that filters visible/interactable brain regions by depth preset.
/// Attach to the same GameObject as BrainManager or create as a singleton.
/// </summary>
public class AnatomyLayerService : MonoBehaviour
{
    enum HemisphereScope
    {
        Both,
        LeftOnly,
        RightOnly
    }

    public static AnatomyLayerService Instance { get; private set; }

    [Tooltip("Assign the catalog asset (or leave null to auto-populate defaults at runtime).")]
    public AnatomyLayerCatalog catalog;

    AnatomyDepthPreset _activePreset = AnatomyDepthPreset.FrontalParietal;
    bool _presetActive;
    BrainRegion[] _allRegions;

    /// <summary>Currently applied depth preset (only meaningful when IsPresetActive).</summary>
    public AnatomyDepthPreset ActivePreset => _activePreset;

    /// <summary>True when a layer filter is actively applied.</summary>
    public bool IsPresetActive => _presetActive;

    /// <summary>Preset display names for UI buttons. Mentor can update these.</summary>
    public static readonly string[] PresetLabels = new string[]
    {
        "Frontal & Parietal Cortex",
        "Temporal & Occipital Cortex",
        "Insular & Limbic Cortex",
        "Deep Nuclei & Tracts",
        "Brainstem & Ventricles"
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<AnatomyLayerCatalog>();
            catalog.PopulateDefaults();
            Debug.Log("[AnatomyLayerService] No catalog assigned — using built-in defaults.");
        }
    }

    /// <summary>
    /// Apply a depth preset: show only regions tagged to that preset,
    /// hide/disable everything else.
    /// </summary>
    public void ApplyPreset(AnatomyDepthPreset preset)
    {
        _activePreset = preset;
        _presetActive = true;
        RefreshRegionCache();
        HemisphereScope scope = GetHemisphereScope();

        int shown = 0, hidden = 0;

        foreach (var region in _allRegions)
        {
            if (region == null) continue;

            if (!IsRegionInScope(region, scope))
                continue;

            string key = GetRegionKey(region);
            AnatomyDepthPreset regionPreset = catalog.GetPreset(key);
            bool isShell = IsShellMesh(key);

            bool shouldShow = (regionPreset == preset) && !isShell;

            SetRegionVisible(region, shouldShow);
            if (shouldShow) shown++; else hidden++;
        }

        Debug.Log($"[AnatomyLayerService] Applied preset '{PresetLabels[(int)preset]}': {shown} shown, {hidden} hidden.");
    }

    /// <summary>Restore all regions to visible/interactable (clear the layer filter).</summary>
    public void RestoreAll()
    {
        _presetActive = false;
        RefreshRegionCache();

        foreach (var region in _allRegions)
        {
            if (region == null) continue;
            SetRegionVisible(region, true);
        }

        Debug.Log("[AnatomyLayerService] Restored all regions.");
    }

    /// <summary>Check whether a specific region belongs to the active preset.</summary>
    public bool IsRegionInActivePreset(BrainRegion region)
    {
        if (!_presetActive || region == null) return true;
        if (!IsRegionInScope(region, GetHemisphereScope())) return false;
        string key = GetRegionKey(region);
        return catalog.GetPreset(key) == _activePreset && !IsShellMesh(key);
    }

    /// <summary>Check tutorial eligibility for a region.</summary>
    public bool IsRegionTutorialEligible(BrainRegion region)
    {
        if (region == null) return false;
        string key = GetRegionKey(region);
        return catalog.IsTutorialEligible(key);
    }

    /// <summary>Get the preset for a region key.</summary>
    public AnatomyDepthPreset GetPresetForRegion(BrainRegion region)
    {
        if (region == null) return AnatomyDepthPreset.FrontalParietal;
        return catalog.GetPreset(GetRegionKey(region));
    }

    // ========================= INTERNALS =========================

    void RefreshRegionCache()
    {
        _allRegions = FindObjectsByType<BrainRegion>(FindObjectsSortMode.None);
    }

    static string GetRegionKey(BrainRegion region)
    {
        string raw;
        if (region.regionData != null && !string.IsNullOrEmpty(region.regionData.regionId))
            raw = region.regionData.regionId;
        else
            raw = region.gameObject.name;
        return NormalizeKey(raw);
    }

    /// <summary>Strip common prefixes (Allen_) so catalog keys match scene names.</summary>
    static string NormalizeKey(string raw)
    {
        if (raw.StartsWith("Allen_", System.StringComparison.OrdinalIgnoreCase))
            return raw.Substring(6);
        return raw;
    }

    static bool IsShellMesh(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        string lower = key.ToLowerInvariant();
        return lower.StartsWith("brain_hemisphere");
    }

    static void SetRegionVisible(BrainRegion region, bool visible)
    {
        foreach (var rend in region.GetComponentsInChildren<Renderer>(true))
        {
            if (rend != null) rend.enabled = visible;
        }

        foreach (var col in region.GetComponentsInChildren<Collider>(true))
        {
            if (col != null) col.enabled = visible;
        }

        var interactable = region.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        if (interactable != null) interactable.enabled = visible;

        var childInteractable = region.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(true);
        if (childInteractable != null) childInteractable.enabled = visible;
    }

    HemisphereScope GetHemisphereScope()
    {
        var brainManager = FindFirstObjectByType<BrainManager>();
        if (brainManager == null) return HemisphereScope.Both;
        if (brainManager.IsLeftHemisphereFocused) return HemisphereScope.LeftOnly;
        if (brainManager.IsRightHemisphereFocused) return HemisphereScope.RightOnly;
        return HemisphereScope.Both;
    }

    static bool IsRegionInScope(BrainRegion region, HemisphereScope scope)
    {
        if (scope == HemisphereScope.Both || region == null)
            return true;

        var brainManager = FindFirstObjectByType<BrainManager>();
        if (brainManager == null)
            return true;

        if (scope == HemisphereScope.LeftOnly)
            return brainManager.IsRegionInLeftHemisphere(region);

        return brainManager.IsRegionInRightHemisphere(region);
    }
}
