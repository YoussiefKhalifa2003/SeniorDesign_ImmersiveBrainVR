using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Depth preset for anatomy layering.
/// Mentor should approve final scientific labels shown in UI.
/// </summary>
public enum AnatomyDepthPreset
{
    /// <summary>Frontal lobe gyri, parietal lobe gyri, paracentral lobule, orbital gyri.</summary>
    FrontalParietal = 0,
    /// <summary>Temporal lobe gyri, occipital lobe gyri, fusiform, poles.</summary>
    TemporalOccipital = 1,
    /// <summary>Cingulate, parahippocampal, insular cortices, olfactory, limbic surface.</summary>
    InsularLimbic = 2,
    /// <summary>Basal ganglia, thalamic nuclei, amygdala/hippocampal complex, hypothalamus, white matter tracts.</summary>
    DeepNuclei = 3,
    /// <summary>Brainstem, cerebellum, ventricles, commissural tracts, peduncles.</summary>
    BrainstemCore = 4
}

/// <summary>
/// Single-asset catalog that maps every region key to a depth preset.
/// Avoids editing hundreds of RegionData SOs by hand — maintained as one
/// authoritative list (can be rebuilt from CSV with the editor utility).
/// </summary>
[CreateAssetMenu(fileName = "AnatomyLayerCatalog", menuName = "Brain Dissection/Anatomy Layer Catalog")]
public class AnatomyLayerCatalog : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public string regionKey;
        public AnatomyDepthPreset preset;
        public bool tutorialEligible;
    }

    [Tooltip("Master region → preset mapping. regionKey should match RegionData.regionId or GameObject.name.")]
    public List<Entry> entries = new List<Entry>();

    // Runtime lookup built on first query
    Dictionary<string, Entry> _lookup;

    void BuildLookup()
    {
        _lookup = new Dictionary<string, Entry>(entries.Count, System.StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            if (string.IsNullOrEmpty(e.regionKey)) continue;
            _lookup[e.regionKey] = e;
        }
    }

    public bool TryGetEntry(string regionKey, out Entry entry)
    {
        if (_lookup == null) BuildLookup();
        if (!string.IsNullOrEmpty(regionKey) && _lookup.TryGetValue(regionKey, out entry))
            return true;
        entry = default;
        return false;
    }

    public AnatomyDepthPreset GetPreset(string regionKey)
    {
        if (TryGetEntry(regionKey, out var e)) return e.preset;
        return AnatomyDepthPreset.FrontalParietal;
    }

    public bool IsTutorialEligible(string regionKey)
    {
        if (TryGetEntry(regionKey, out var e)) return e.tutorialEligible;
        return false;
    }

    /// <summary>Invalidate runtime cache (call after editor import).</summary>
    public void InvalidateCache() => _lookup = null;

    /// <summary>
    /// Populate the catalog with rule-based defaults for all known regions.
    /// Called from editor utility or at runtime as a bootstrap fallback.
    /// </summary>
    public void PopulateDefaults()
    {
        entries.Clear();
        foreach (var kv in DefaultRegionAssignments.All)
        {
            entries.Add(new Entry
            {
                regionKey = kv.Key,
                preset = kv.Value.preset,
                tutorialEligible = kv.Value.tutorialEligible
            });
        }
        InvalidateCache();
    }
}

/// <summary>
/// Static table of every region → preset assignment.
/// Rule-based first pass; mentor can override via the SO inspector.
/// </summary>
public static class DefaultRegionAssignments
{
    public struct Info
    {
        public AnatomyDepthPreset preset;
        public bool tutorialEligible;
        public Info(AnatomyDepthPreset p, bool tut = false) { preset = p; tutorialEligible = tut; }
    }

    static Dictionary<string, Info> _all;

    public static Dictionary<string, Info> All
    {
        get
        {
            if (_all == null) Build();
            return _all;
        }
    }

    static void Add(string key, AnatomyDepthPreset p, bool tut = false)
    {
        _all[key] = new Info(p, tut);
    }

    static void AddBoth(string baseName, AnatomyDepthPreset p, bool tut = false)
    {
        Add(baseName + "_L", p, tut);
        Add(baseName + "_R", p, tut);
    }

    static void Build()
    {
        _all = new Dictionary<string, Info>(320, System.StringComparer.OrdinalIgnoreCase);

        // ============ FRONTAL & PARIETAL CORTEX ============

        AddBoth("superior_frontal_gyrus", AnatomyDepthPreset.FrontalParietal, true);
        AddBoth("middle_frontal_gyrus", AnatomyDepthPreset.FrontalParietal, true);
        AddBoth("inferior_frontal_gyrus_opercular_part", AnatomyDepthPreset.FrontalParietal, true);
        AddBoth("inferior_frontal_gyrus_triangular_part", AnatomyDepthPreset.FrontalParietal, true);
        AddBoth("precentral_gyrus", AnatomyDepthPreset.FrontalParietal, true);
        AddBoth("postcentral_gyrus", AnatomyDepthPreset.FrontalParietal, true);
        AddBoth("frontal_pole", AnatomyDepthPreset.FrontalParietal, true);
        AddBoth("frontomarginal_gyrus", AnatomyDepthPreset.FrontalParietal);
        AddBoth("frontal_operculum", AnatomyDepthPreset.FrontalParietal);
        AddBoth("paracentral_lobule_rostral_part", AnatomyDepthPreset.FrontalParietal, true);
        AddBoth("paracentral_lobule_caudal_part", AnatomyDepthPreset.FrontalParietal, true);
        AddBoth("gyrus_rectus_straight_gyrus", AnatomyDepthPreset.FrontalParietal);
        AddBoth("rostral_gyrus", AnatomyDepthPreset.FrontalParietal);
        AddBoth("anterior_intermediate_orbital_gyrus", AnatomyDepthPreset.FrontalParietal);
        AddBoth("posterior_intermediate_orbital_gyrus", AnatomyDepthPreset.FrontalParietal);
        AddBoth("lateral_orbital_gyrus", AnatomyDepthPreset.FrontalParietal);
        AddBoth("medial_orbital_gyrus", AnatomyDepthPreset.FrontalParietal);
        AddBoth("supramarginal_gyrus", AnatomyDepthPreset.FrontalParietal, true);
        AddBoth("angular_gyrus", AnatomyDepthPreset.FrontalParietal, true);
        AddBoth("precuneus", AnatomyDepthPreset.FrontalParietal, true);
        AddBoth("supraparietal_lobule", AnatomyDepthPreset.FrontalParietal, true);

        // ============ TEMPORAL & OCCIPITAL CORTEX ============

        AddBoth("superior_temporal_gyrus", AnatomyDepthPreset.TemporalOccipital, true);
        AddBoth("middle_temporal_gyrus", AnatomyDepthPreset.TemporalOccipital, true);
        AddBoth("inferior_temporal_gyrus", AnatomyDepthPreset.TemporalOccipital, true);
        AddBoth("temporal_pole", AnatomyDepthPreset.TemporalOccipital, true);
        AddBoth("transverse_temporal_gyrus_Heschls_gyrus", AnatomyDepthPreset.TemporalOccipital, true);
        AddBoth("planum_temporale", AnatomyDepthPreset.TemporalOccipital);
        AddBoth("planum_polare", AnatomyDepthPreset.TemporalOccipital);
        AddBoth("occipitotemporal_fusiform_gyrus_temporal_part", AnatomyDepthPreset.TemporalOccipital);
        AddBoth("lateral_occipitotemporal_fusiform_gyrus_occipital_part", AnatomyDepthPreset.TemporalOccipital);
        AddBoth("perirhinal_gyrus_rostral_part_of_FuGt", AnatomyDepthPreset.TemporalOccipital);
        AddBoth("cuneus", AnatomyDepthPreset.TemporalOccipital, true);
        AddBoth("lingual_gyrus_medial_occipitotemporal_gyrus", AnatomyDepthPreset.TemporalOccipital, true);
        AddBoth("occipital_pole", AnatomyDepthPreset.TemporalOccipital, true);
        AddBoth("superior_occipital_gyrus", AnatomyDepthPreset.TemporalOccipital, true);
        AddBoth("inferior_occipital_gyrus", AnatomyDepthPreset.TemporalOccipital, true);

        // ============ INSULAR & LIMBIC CORTEX ============

        AddBoth("cingulate_gyrus_rostral_anterior_part", AnatomyDepthPreset.InsularLimbic, true);
        AddBoth("cingulate_gyrus_caudal_posterior_part", AnatomyDepthPreset.InsularLimbic, true);
        AddBoth("paracingulate_gyrus", AnatomyDepthPreset.InsularLimbic);
        AddBoth("subcallosal_gyrus_parolfactory_gyrus", AnatomyDepthPreset.InsularLimbic);
        AddBoth("anterior_parahippocampal_gyrus", AnatomyDepthPreset.InsularLimbic, true);
        AddBoth("posterior_parahippocampal_gyrus", AnatomyDepthPreset.InsularLimbic, true);
        AddBoth("ingulo_parahippocampal_isthmus", AnatomyDepthPreset.InsularLimbic);
        AddBoth("gyrus_ambiens", AnatomyDepthPreset.InsularLimbic);
        AddBoth("short_insular_gyri", AnatomyDepthPreset.InsularLimbic);
        AddBoth("long_insular_gyri", AnatomyDepthPreset.InsularLimbic);
        AddBoth("limen_insula", AnatomyDepthPreset.InsularLimbic);
        AddBoth("frontal_agranular_insular_cortex_area_FI", AnatomyDepthPreset.InsularLimbic);
        AddBoth("temporal_agranular_insular_cortex_area_TI", AnatomyDepthPreset.InsularLimbic);
        AddBoth("anterior_olfactory_nucleus", AnatomyDepthPreset.InsularLimbic);
        AddBoth("olfactory_bulb", AnatomyDepthPreset.InsularLimbic);
        AddBoth("olfactory_tract", AnatomyDepthPreset.InsularLimbic);
        AddBoth("lateral_olfactory_gyrus", AnatomyDepthPreset.InsularLimbic);
        AddBoth("piriform_region", AnatomyDepthPreset.InsularLimbic);
        AddBoth("optic_radiation", AnatomyDepthPreset.InsularLimbic);
        AddBoth("optic_tract", AnatomyDepthPreset.InsularLimbic);

        // Hemisphere shell — context only, never a tutorial target
        AddBoth("brain_Hemisphere", AnatomyDepthPreset.FrontalParietal, false);

        // ============ DEEP NUCLEI & TRACTS ============

        // Basal ganglia
        AddBoth("putamen", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("posteroventral_putamen", AnatomyDepthPreset.DeepNuclei);
        AddBoth("external_segment_of_globus_pallidus", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("internal_segment_of_globus_pallidus", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("head_of_caudate", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("body_of_caudate", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("tail_of_caudate", AnatomyDepthPreset.DeepNuclei);
        AddBoth("nucleus_accumbens", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("subthalamic_nucleus", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("substantia_nigra", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("claustrum", AnatomyDepthPreset.DeepNuclei);

        // Thalamic nuclei
        AddBoth("thalamus", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("anterior_nuclear_complex_of_thalamus", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("centromedian_nucleus_of_thalamus", AnatomyDepthPreset.DeepNuclei);
        AddBoth("dorsal_lateral_geniculate_nucleus", AnatomyDepthPreset.DeepNuclei);
        AddBoth("habenular_nuclei", AnatomyDepthPreset.DeepNuclei);
        AddBoth("lateral_dorsal_nucleus_of_thalamus", AnatomyDepthPreset.DeepNuclei);
        AddBoth("lateral_posterior_nucleus_of_thalamus", AnatomyDepthPreset.DeepNuclei);
        AddBoth("medial_geniculate_nuclei", AnatomyDepthPreset.DeepNuclei);
        AddBoth("mediodorsal_nucleus_of_thalamus", AnatomyDepthPreset.DeepNuclei);
        AddBoth("midline_nuclear_complex", AnatomyDepthPreset.DeepNuclei);
        AddBoth("parafascicular_nucleus_of_thalamus", AnatomyDepthPreset.DeepNuclei);
        AddBoth("pulvinar_of_thalamus", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("reuniens_nucleus_medioventral_nucleus_of_thalamus", AnatomyDepthPreset.DeepNuclei);
        AddBoth("ventral_anterior_nucleus_of_thalamus", AnatomyDepthPreset.DeepNuclei);
        AddBoth("ventral_lateral_nucleus_of_thalamus", AnatomyDepthPreset.DeepNuclei);
        AddBoth("ventral_posterior_lateral_nucleus", AnatomyDepthPreset.DeepNuclei);
        AddBoth("ventral_posterior_medial_nucleus", AnatomyDepthPreset.DeepNuclei);

        // Amygdala complex
        AddBoth("amygdaloid_complex", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("amygdalohippocampal_area", AnatomyDepthPreset.DeepNuclei);
        AddBoth("anterior_amygdaloid_area", AnatomyDepthPreset.DeepNuclei);
        AddBoth("anterior_cortical_nucleus", AnatomyDepthPreset.DeepNuclei);
        AddBoth("basolateral_nucleus_basal_nucleus", AnatomyDepthPreset.DeepNuclei);
        AddBoth("basomedial_nucleus_accessory_basal_nucleus", AnatomyDepthPreset.DeepNuclei);
        AddBoth("central_nuclear_group", AnatomyDepthPreset.DeepNuclei);
        AddBoth("lateral_nucleus", AnatomyDepthPreset.DeepNuclei);
        AddBoth("medial_nucleus", AnatomyDepthPreset.DeepNuclei);
        AddBoth("posterior_cortical_nucleus", AnatomyDepthPreset.DeepNuclei);

        // Hippocampal formation
        AddBoth("head_of_hippocampus", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("body_of_hippocampus", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("tail_of_hippocampus", AnatomyDepthPreset.DeepNuclei);

        // Hypothalamus and related
        AddBoth("hypothalamus", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("mammillary_region_of_HTH", AnatomyDepthPreset.DeepNuclei);
        AddBoth("preoptic_region_of_HTH", AnatomyDepthPreset.DeepNuclei);
        AddBoth("supraoptic_region_of_HTH", AnatomyDepthPreset.DeepNuclei);
        AddBoth("tuberal_region_of_HTH", AnatomyDepthPreset.DeepNuclei);

        // Other deep telencephalic / diencephalic
        AddBoth("basal_forebrain", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("bed_nucleus_of_stria_terminalis", AnatomyDepthPreset.DeepNuclei);
        AddBoth("fornix", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("corpus_callosum", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("anterior_commissure", AnatomyDepthPreset.DeepNuclei);
        AddBoth("septal_nuclei", AnatomyDepthPreset.DeepNuclei);
        AddBoth("zona_incerta", AnatomyDepthPreset.DeepNuclei);
        AddBoth("red_nucleus", AnatomyDepthPreset.DeepNuclei, true);
        AddBoth("pretectal_region", AnatomyDepthPreset.DeepNuclei);
        AddBoth("pineal_body", AnatomyDepthPreset.DeepNuclei);
        AddBoth("white_matter_of_forebrain", AnatomyDepthPreset.DeepNuclei);

        // ============ BRAINSTEM & CORE ============

        // Brainstem
        AddBoth("basilar_part_of_pons", AnatomyDepthPreset.BrainstemCore, true);
        AddBoth("cerebral_peduncle_crus_cerebri", AnatomyDepthPreset.BrainstemCore, true);
        AddBoth("inferior_colliculus", AnatomyDepthPreset.BrainstemCore, true);
        AddBoth("inferior_olive", AnatomyDepthPreset.BrainstemCore);
        AddBoth("midbrain_tegmentum", AnatomyDepthPreset.BrainstemCore, true);
        AddBoth("pontine_tegmentum", AnatomyDepthPreset.BrainstemCore);
        AddBoth("pyramidal_part_of_medulla_oblongata", AnatomyDepthPreset.BrainstemCore);
        AddBoth("superior_colliculus", AnatomyDepthPreset.BrainstemCore, true);
        AddBoth("tegmentum_of_medulla_oblongata", AnatomyDepthPreset.BrainstemCore);

        // Cerebellum
        AddBoth("cerebellar_deep_nuclei", AnatomyDepthPreset.BrainstemCore, true);
        AddBoth("cerebellar_vermis", AnatomyDepthPreset.BrainstemCore, true);
        AddBoth("lateral_hemisphere_of_cerebellum", AnatomyDepthPreset.BrainstemCore, true);
        AddBoth("paravermis_of_cerebellum", AnatomyDepthPreset.BrainstemCore);
        AddBoth("inferior_cerebellar_peduncle", AnatomyDepthPreset.BrainstemCore);
        AddBoth("middle_cerebellar_peduncle", AnatomyDepthPreset.BrainstemCore);
        AddBoth("superior_cerebellar_peduncle_brachium_conjunctivum", AnatomyDepthPreset.BrainstemCore);

        // Ventricles and CSF spaces
        AddBoth("anterior_horn_of_lateral_ventricle", AnatomyDepthPreset.BrainstemCore);
        AddBoth("atrium_of_lateral_ventricle", AnatomyDepthPreset.BrainstemCore);
        AddBoth("body_of_lateral_ventricle", AnatomyDepthPreset.BrainstemCore);
        AddBoth("cerebral_aqueduct", AnatomyDepthPreset.BrainstemCore);
        AddBoth("central_canal_of_medulla_oblongata", AnatomyDepthPreset.BrainstemCore);
        AddBoth("fourth_ventricle", AnatomyDepthPreset.BrainstemCore);
        AddBoth("inferior_horn_of_lateral_ventricle", AnatomyDepthPreset.BrainstemCore);
        AddBoth("posterior_horn_of_lateral_ventricle", AnatomyDepthPreset.BrainstemCore);
        AddBoth("third_ventricle", AnatomyDepthPreset.BrainstemCore);

        // White matter of hindbrain
        AddBoth("white_matter_of_hindbrain", AnatomyDepthPreset.BrainstemCore);

        // Midline
        Add("VH_M_optic_chiasm", AnatomyDepthPreset.DeepNuclei);

        // Right-only entry
        Add("mammillothalamic_tract_R", AnatomyDepthPreset.DeepNuclei);
    }
}
