using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-accessible neuroanatomical cross-reference table. Each entry
/// maps a base region key (e.g. <c>superior_temporal_gyrus</c>) to two
/// arrays of base keys: regions that physically border it (<i>adjacent</i>)
/// and regions linked by a shared circuit, tract, or clinical pairing
/// (<i>related</i>).
///
/// At runtime, <see cref="Resolve"/> takes a <see cref="RegionData"/> and
/// returns the resolved <see cref="RegionData"/>[] arrays for the same
/// hemisphere. Both this class and the editor menu
/// <c>BulkRegionCrossReferences</c> read from the same lookup so the
/// data is authored in exactly one place — runtime fallback is identical
/// to what the menu writes onto the asset arrays.
///
/// The runtime resolver finds target regions by scanning all
/// <see cref="BrainRegion"/> components in the active scene (cached on
/// first use). This means cross-refs work out of the box in a fresh
/// build without anyone needing to run the editor menu first.
/// </summary>
public static class RegionCrossReferenceData
{
    public struct Entry
    {
        public string[] adjacent;
        public string[] related;
    }

    public struct Resolved
    {
        public RegionData[] adjacent;
        public RegionData[] related;
    }

    static Dictionary<string, Entry> _lookup;
    static Dictionary<string, RegionData> _registry;

    /// <summary>
    /// Resolve cross-references for <paramref name="data"/>. Returns
    /// empty arrays if the region has no curated entry or if the runtime
    /// registry can't find a target region in the active scene.
    /// </summary>
    public static Resolved Resolve(RegionData data)
    {
        var empty = new Resolved { adjacent = Array.Empty<RegionData>(), related = Array.Empty<RegionData>() };
        if (data == null || string.IsNullOrEmpty(data.regionId)) return empty;

        EnsureLookup();
        EnsureRegistry();

        string baseKey = StripPrefixAndHemisphere(data.regionId, out string hemiSuffix);
        if (string.IsNullOrEmpty(baseKey) || string.IsNullOrEmpty(hemiSuffix)) return empty;

        if (!_lookup.TryGetValue(baseKey, out var entry)) return empty;

        return new Resolved
        {
            adjacent = ResolveKeys(entry.adjacent, hemiSuffix, data.regionId),
            related = ResolveKeys(entry.related, hemiSuffix, data.regionId),
        };
    }

    public static Entry? GetEntry(string baseKey)
    {
        EnsureLookup();
        return _lookup.TryGetValue(baseKey, out var e) ? (Entry?)e : null;
    }

    /// <summary>Drop the cached BrainRegion registry. Call after a scene
    /// load if needed; we rebuild lazily on next Resolve.</summary>
    public static void InvalidateRegistry() => _registry = null;

    /// <summary>
    /// Strip the <c>Allen_</c> prefix (case-insensitive) and the
    /// trailing <c>_L</c> / <c>_R</c> hemisphere suffix from a region id.
    /// Returns the base key and the hemisphere letter ("L"/"R"/"").
    /// </summary>
    public static string StripPrefixAndHemisphere(string regionId, out string hemiSuffix)
    {
        hemiSuffix = "";
        if (string.IsNullOrEmpty(regionId)) return "";

        string id = regionId;
        if (id.StartsWith("Allen_", StringComparison.OrdinalIgnoreCase))
            id = id.Substring("Allen_".Length);

        if (id.EndsWith("_L")) { hemiSuffix = "L"; id = id.Substring(0, id.Length - 2); }
        else if (id.EndsWith("_R")) { hemiSuffix = "R"; id = id.Substring(0, id.Length - 2); }
        return id;
    }

    static RegionData[] ResolveKeys(string[] keys, string hemiSuffix, string sourceId)
    {
        if (keys == null || keys.Length == 0) return Array.Empty<RegionData>();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<RegionData>();

        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(key)) continue;
            string targetId = "Allen_" + key + "_" + hemiSuffix;
            if (string.Equals(targetId, sourceId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!seen.Add(targetId)) continue;

            var rd = FindRegionData(targetId);
            if (rd != null) result.Add(rd);
        }
        return result.ToArray();
    }

    static RegionData FindRegionData(string regionId)
    {
        if (_registry == null) return null;
        return _registry.TryGetValue(regionId, out var rd) ? rd : null;
    }

    static void EnsureRegistry()
    {
        if (_registry != null) return;
        _registry = new Dictionary<string, RegionData>(StringComparer.OrdinalIgnoreCase);

        var regions = UnityEngine.Object.FindObjectsByType<BrainRegion>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var br in regions)
        {
            if (br == null || br.regionData == null) continue;
            string id = br.regionData.regionId;
            if (string.IsNullOrEmpty(id)) continue;
            if (!_registry.ContainsKey(id)) _registry[id] = br.regionData;
        }
    }

    static void EnsureLookup()
    {
        if (_lookup != null) return;
        _lookup = BuildLookup();
    }

    static Dictionary<string, Entry> BuildLookup()
    {
        var d = new Dictionary<string, Entry>(StringComparer.Ordinal);
        void Add(string key, string[] adj, string[] rel) =>
            d[key] = new Entry { adjacent = adj ?? Array.Empty<string>(), related = rel ?? Array.Empty<string>() };

        // ===================== Cerebrum surface =====================

        Add("superior_frontal_gyrus",
            new[] { "middle_frontal_gyrus", "precentral_gyrus", "frontal_pole", "cingulate_gyrus_rostral_anterior_part", "paracingulate_gyrus", "paracentral_lobule_rostral_part" },
            new[] { "middle_frontal_gyrus", "paracingulate_gyrus", "precentral_gyrus", "mediodorsal_nucleus_of_thalamus" });

        Add("middle_frontal_gyrus",
            new[] { "superior_frontal_gyrus", "inferior_frontal_gyrus_opercular_part", "inferior_frontal_gyrus_triangular_part", "precentral_gyrus", "frontal_pole" },
            new[] { "superior_frontal_gyrus", "inferior_frontal_gyrus_triangular_part", "mediodorsal_nucleus_of_thalamus" });

        Add("inferior_frontal_gyrus_opercular_part",
            new[] { "inferior_frontal_gyrus_triangular_part", "middle_frontal_gyrus", "precentral_gyrus", "frontal_operculum", "short_insular_gyri" },
            new[] { "inferior_frontal_gyrus_triangular_part", "superior_temporal_gyrus", "supramarginal_gyrus", "angular_gyrus" });

        Add("inferior_frontal_gyrus_triangular_part",
            new[] { "inferior_frontal_gyrus_opercular_part", "middle_frontal_gyrus", "frontal_operculum", "lateral_orbital_gyrus" },
            new[] { "inferior_frontal_gyrus_opercular_part", "superior_temporal_gyrus", "angular_gyrus" });

        Add("precentral_gyrus",
            new[] { "postcentral_gyrus", "superior_frontal_gyrus", "middle_frontal_gyrus", "inferior_frontal_gyrus_opercular_part", "frontal_operculum", "paracentral_lobule_rostral_part" },
            new[] { "postcentral_gyrus", "ventral_lateral_nucleus_of_thalamus", "internal_segment_of_globus_pallidus", "putamen", "cerebral_peduncle_crus_cerebri", "pyramidal_part_of_medulla_oblongata" });

        Add("postcentral_gyrus",
            new[] { "precentral_gyrus", "supramarginal_gyrus", "supraparietal_lobule", "parietal_operculum", "paracentral_lobule_caudal_part" },
            new[] { "precentral_gyrus", "ventral_posterior_lateral_nucleus", "ventral_posterior_medial_nucleus", "supraparietal_lobule" });

        Add("frontal_pole",
            new[] { "superior_frontal_gyrus", "middle_frontal_gyrus", "frontomarginal_gyrus", "gyrus_rectus_straight_gyrus", "rostral_gyrus" },
            new[] { "superior_frontal_gyrus", "frontomarginal_gyrus", "anterior_intermediate_orbital_gyrus" });

        Add("frontomarginal_gyrus",
            new[] { "frontal_pole", "superior_frontal_gyrus", "gyrus_rectus_straight_gyrus", "anterior_intermediate_orbital_gyrus" },
            new[] { "frontal_pole", "gyrus_rectus_straight_gyrus" });

        Add("frontal_operculum",
            new[] { "inferior_frontal_gyrus_opercular_part", "inferior_frontal_gyrus_triangular_part", "precentral_gyrus", "short_insular_gyri", "frontal_agranular_insular_cortex_area_Fl" },
            new[] { "inferior_frontal_gyrus_opercular_part", "short_insular_gyri", "superior_temporal_gyrus" });

        Add("paracentral_lobule_rostral_part",
            new[] { "paracentral_lobule_caudal_part", "superior_frontal_gyrus", "cingulate_gyrus_rostral_anterior_part", "precentral_gyrus" },
            new[] { "precentral_gyrus", "paracentral_lobule_caudal_part" });

        Add("paracentral_lobule_caudal_part",
            new[] { "paracentral_lobule_rostral_part", "precuneus", "cingulate_gyrus_caudal_posterior_part", "postcentral_gyrus" },
            new[] { "postcentral_gyrus", "paracentral_lobule_rostral_part", "precuneus" });

        Add("gyrus_rectus_straight_gyrus",
            new[] { "medial_orbital_gyrus", "frontal_pole", "frontomarginal_gyrus", "anterior_intermediate_orbital_gyrus", "subcallosal_gyrus_parolfactory_gyrus", "olfactory_bulb" },
            new[] { "medial_orbital_gyrus", "lateral_orbital_gyrus", "anterior_intermediate_orbital_gyrus" });

        Add("rostral_gyrus",
            new[] { "gyrus_rectus_straight_gyrus", "anterior_intermediate_orbital_gyrus", "frontal_pole" },
            new[] { "anterior_intermediate_orbital_gyrus", "posterior_intermediate_orbital_gyrus", "medial_orbital_gyrus" });

        Add("anterior_intermediate_orbital_gyrus",
            new[] { "posterior_intermediate_orbital_gyrus", "lateral_orbital_gyrus", "medial_orbital_gyrus", "gyrus_rectus_straight_gyrus", "rostral_gyrus", "frontomarginal_gyrus" },
            new[] { "posterior_intermediate_orbital_gyrus", "lateral_orbital_gyrus", "medial_orbital_gyrus" });

        Add("posterior_intermediate_orbital_gyrus",
            new[] { "anterior_intermediate_orbital_gyrus", "lateral_orbital_gyrus", "medial_orbital_gyrus" },
            new[] { "anterior_intermediate_orbital_gyrus", "lateral_orbital_gyrus", "medial_orbital_gyrus" });

        Add("lateral_orbital_gyrus",
            new[] { "anterior_intermediate_orbital_gyrus", "posterior_intermediate_orbital_gyrus", "medial_orbital_gyrus", "frontal_operculum", "inferior_frontal_gyrus_triangular_part" },
            new[] { "medial_orbital_gyrus", "anterior_intermediate_orbital_gyrus", "posterior_intermediate_orbital_gyrus" });

        Add("medial_orbital_gyrus",
            new[] { "gyrus_rectus_straight_gyrus", "anterior_intermediate_orbital_gyrus", "posterior_intermediate_orbital_gyrus", "lateral_orbital_gyrus", "subcallosal_gyrus_parolfactory_gyrus" },
            new[] { "gyrus_rectus_straight_gyrus", "lateral_orbital_gyrus", "anterior_intermediate_orbital_gyrus" });

        Add("supramarginal_gyrus",
            new[] { "angular_gyrus", "postcentral_gyrus", "superior_temporal_gyrus", "parietal_operculum", "supraparietal_lobule" },
            new[] { "angular_gyrus", "inferior_frontal_gyrus_opercular_part", "superior_temporal_gyrus" });

        Add("angular_gyrus",
            new[] { "supramarginal_gyrus", "supraparietal_lobule", "middle_temporal_gyrus", "superior_temporal_gyrus", "superior_occipital_gyrus" },
            new[] { "supramarginal_gyrus", "middle_temporal_gyrus", "inferior_frontal_gyrus_triangular_part" });

        Add("precuneus",
            new[] { "paracentral_lobule_caudal_part", "supraparietal_lobule", "cingulate_gyrus_caudal_posterior_part", "cuneus" },
            new[] { "cingulate_gyrus_caudal_posterior_part", "supraparietal_lobule" });

        Add("supraparietal_lobule",
            new[] { "postcentral_gyrus", "angular_gyrus", "supramarginal_gyrus", "precuneus", "superior_occipital_gyrus" },
            new[] { "postcentral_gyrus", "precuneus", "pulvinar_of_thalamus" });

        Add("parietal_operculum",
            new[] { "supramarginal_gyrus", "postcentral_gyrus", "long_insular_gyri", "superior_temporal_gyrus" },
            new[] { "postcentral_gyrus", "long_insular_gyri" });

        Add("superior_temporal_gyrus",
            new[] { "middle_temporal_gyrus", "transverse_temporal_gyrus_Heschls_gyrus", "planum_temporale", "planum_polare", "supramarginal_gyrus", "parietal_operculum", "temporal_pole" },
            new[] { "middle_temporal_gyrus", "transverse_temporal_gyrus_Heschls_gyrus", "inferior_frontal_gyrus_opercular_part", "supramarginal_gyrus" });

        Add("middle_temporal_gyrus",
            new[] { "superior_temporal_gyrus", "inferior_temporal_gyrus", "angular_gyrus", "temporal_pole" },
            new[] { "superior_temporal_gyrus", "inferior_temporal_gyrus", "angular_gyrus" });

        Add("inferior_temporal_gyrus",
            new[] { "middle_temporal_gyrus", "occipitotemporal_fusiform_gyrus_temporal_part", "temporal_pole", "perirhinal_gyrus_rostral_part_of_FuGt" },
            new[] { "occipitotemporal_fusiform_gyrus_temporal_part", "middle_temporal_gyrus" });

        Add("temporal_pole",
            new[] { "superior_temporal_gyrus", "middle_temporal_gyrus", "inferior_temporal_gyrus", "anterior_parahippocampal_gyrus", "gyrus_ambiens", "amygdaloid_complex" },
            new[] { "amygdaloid_complex", "anterior_parahippocampal_gyrus", "superior_temporal_gyrus" });

        Add("transverse_temporal_gyrus_Heschls_gyrus",
            new[] { "superior_temporal_gyrus", "planum_temporale", "planum_polare", "long_insular_gyri" },
            new[] { "medial_geniculate_nuclei", "superior_temporal_gyrus", "planum_temporale" });

        Add("planum_temporale",
            new[] { "transverse_temporal_gyrus_Heschls_gyrus", "superior_temporal_gyrus", "planum_polare" },
            new[] { "transverse_temporal_gyrus_Heschls_gyrus", "superior_temporal_gyrus", "supramarginal_gyrus" });

        Add("planum_polare",
            new[] { "transverse_temporal_gyrus_Heschls_gyrus", "planum_temporale", "superior_temporal_gyrus", "temporal_pole" },
            new[] { "transverse_temporal_gyrus_Heschls_gyrus", "planum_temporale" });

        Add("occipitotemporal_fusiform_gyrus_temporal_part",
            new[] { "lateral_occipitotemporal_fusiform_gyrus_occipital_part", "inferior_temporal_gyrus", "perirhinal_gyrus_rostral_part_of_FuGt", "posterior_parahippocampal_gyrus", "lingual_gyrus_medial_occipitotemporal_gyrus" },
            new[] { "lateral_occipitotemporal_fusiform_gyrus_occipital_part", "inferior_temporal_gyrus", "lingual_gyrus_medial_occipitotemporal_gyrus" });

        Add("lateral_occipitotemporal_fusiform_gyrus_occipital_part",
            new[] { "occipitotemporal_fusiform_gyrus_temporal_part", "inferior_occipital_gyrus", "lingual_gyrus_medial_occipitotemporal_gyrus" },
            new[] { "occipitotemporal_fusiform_gyrus_temporal_part", "lingual_gyrus_medial_occipitotemporal_gyrus", "inferior_occipital_gyrus" });

        Add("perirhinal_gyrus_rostral_part_of_FuGt",
            new[] { "occipitotemporal_fusiform_gyrus_temporal_part", "anterior_parahippocampal_gyrus", "inferior_temporal_gyrus" },
            new[] { "anterior_parahippocampal_gyrus", "head_of_hippocampus", "occipitotemporal_fusiform_gyrus_temporal_part" });

        Add("cuneus",
            new[] { "precuneus", "occipital_pole", "superior_occipital_gyrus", "lingual_gyrus_medial_occipitotemporal_gyrus" },
            new[] { "occipital_pole", "lingual_gyrus_medial_occipitotemporal_gyrus", "dorsal_lateral_geniculate_nucleus", "optic_radiation" });

        Add("lingual_gyrus_medial_occipitotemporal_gyrus",
            new[] { "cuneus", "occipital_pole", "lateral_occipitotemporal_fusiform_gyrus_occipital_part", "posterior_parahippocampal_gyrus" },
            new[] { "cuneus", "lateral_occipitotemporal_fusiform_gyrus_occipital_part", "occipital_pole" });

        Add("occipital_pole",
            new[] { "cuneus", "superior_occipital_gyrus", "inferior_occipital_gyrus", "lingual_gyrus_medial_occipitotemporal_gyrus" },
            new[] { "dorsal_lateral_geniculate_nucleus", "cuneus", "optic_radiation" });

        Add("superior_occipital_gyrus",
            new[] { "cuneus", "occipital_pole", "inferior_occipital_gyrus", "supraparietal_lobule", "angular_gyrus" },
            new[] { "cuneus", "occipital_pole", "inferior_occipital_gyrus" });

        Add("inferior_occipital_gyrus",
            new[] { "superior_occipital_gyrus", "occipital_pole", "lateral_occipitotemporal_fusiform_gyrus_occipital_part", "lingual_gyrus_medial_occipitotemporal_gyrus" },
            new[] { "lateral_occipitotemporal_fusiform_gyrus_occipital_part", "occipital_pole", "occipitotemporal_fusiform_gyrus_temporal_part" });

        // ===================== Cingulate / limbic =====================

        Add("cingulate_gyrus_rostral_anterior_part",
            new[] { "superior_frontal_gyrus", "paracingulate_gyrus", "subcallosal_gyrus_parolfactory_gyrus", "paracentral_lobule_rostral_part", "corpus_callosum" },
            new[] { "paracingulate_gyrus", "cingulate_gyrus_caudal_posterior_part", "subcallosal_gyrus_parolfactory_gyrus", "mediodorsal_nucleus_of_thalamus", "anterior_nuclear_complex_of_thalamus" });

        Add("cingulate_gyrus_caudal_posterior_part",
            new[] { "cingulate_gyrus_rostral_anterior_part", "paracentral_lobule_caudal_part", "precuneus", "ingulo_parahippocampal_isthmus", "corpus_callosum" },
            new[] { "precuneus", "cingulate_gyrus_rostral_anterior_part", "ingulo_parahippocampal_isthmus" });

        Add("paracingulate_gyrus",
            new[] { "cingulate_gyrus_rostral_anterior_part", "superior_frontal_gyrus" },
            new[] { "cingulate_gyrus_rostral_anterior_part", "superior_frontal_gyrus" });

        Add("subcallosal_gyrus_parolfactory_gyrus",
            new[] { "cingulate_gyrus_rostral_anterior_part", "gyrus_rectus_straight_gyrus", "medial_orbital_gyrus", "septal_nuclei", "corpus_callosum" },
            new[] { "cingulate_gyrus_rostral_anterior_part", "septal_nuclei", "nucleus_accumbens" });

        Add("anterior_parahippocampal_gyrus",
            new[] { "posterior_parahippocampal_gyrus", "head_of_hippocampus", "gyrus_ambiens", "perirhinal_gyrus_rostral_part_of_FuGt", "temporal_pole" },
            new[] { "head_of_hippocampus", "body_of_hippocampus", "posterior_parahippocampal_gyrus", "amygdaloid_complex" });

        Add("posterior_parahippocampal_gyrus",
            new[] { "anterior_parahippocampal_gyrus", "body_of_hippocampus", "tail_of_hippocampus", "ingulo_parahippocampal_isthmus", "lingual_gyrus_medial_occipitotemporal_gyrus" },
            new[] { "body_of_hippocampus", "tail_of_hippocampus", "anterior_parahippocampal_gyrus" });

        Add("ingulo_parahippocampal_isthmus",
            new[] { "cingulate_gyrus_caudal_posterior_part", "posterior_parahippocampal_gyrus", "tail_of_hippocampus" },
            new[] { "cingulate_gyrus_caudal_posterior_part", "posterior_parahippocampal_gyrus" });

        Add("gyrus_ambiens",
            new[] { "anterior_parahippocampal_gyrus", "amygdaloid_complex", "piriform_region", "temporal_pole" },
            new[] { "amygdaloid_complex", "piriform_region", "anterior_olfactory_nucleus" });

        // ===================== Insula and olfactory =====================

        Add("short_insular_gyri",
            new[] { "long_insular_gyri", "frontal_operculum", "parietal_operculum", "limen_insula", "claustrum" },
            new[] { "long_insular_gyri", "frontal_agranular_insular_cortex_area_Fl", "claustrum" });

        Add("long_insular_gyri",
            new[] { "short_insular_gyri", "parietal_operculum", "transverse_temporal_gyrus_Heschls_gyrus" },
            new[] { "short_insular_gyri", "parietal_operculum", "claustrum" });

        Add("limen_insula",
            new[] { "short_insular_gyri", "frontal_agranular_insular_cortex_area_Fl", "temporal_agranular_insular_cortex_area_Tl", "piriform_region", "anterior_olfactory_nucleus" },
            new[] { "piriform_region", "anterior_olfactory_nucleus", "short_insular_gyri" });

        Add("frontal_agranular_insular_cortex_area_Fl",
            new[] { "short_insular_gyri", "limen_insula", "frontal_operculum" },
            new[] { "short_insular_gyri", "temporal_agranular_insular_cortex_area_Tl", "limen_insula" });

        Add("temporal_agranular_insular_cortex_area_Tl",
            new[] { "limen_insula", "gyrus_ambiens", "piriform_region", "anterior_olfactory_nucleus" },
            new[] { "piriform_region", "frontal_agranular_insular_cortex_area_Fl", "anterior_olfactory_nucleus" });

        Add("anterior_olfactory_nucleus",
            new[] { "olfactory_bulb", "olfactory_tract", "lateral_olfactory_gyrus", "piriform_region", "gyrus_ambiens" },
            new[] { "olfactory_bulb", "olfactory_tract", "piriform_region" });

        Add("olfactory_bulb",
            new[] { "olfactory_tract", "anterior_olfactory_nucleus", "gyrus_rectus_straight_gyrus" },
            new[] { "olfactory_tract", "anterior_olfactory_nucleus", "piriform_region" });

        Add("olfactory_tract",
            new[] { "olfactory_bulb", "anterior_olfactory_nucleus", "lateral_olfactory_gyrus", "piriform_region" },
            new[] { "olfactory_bulb", "piriform_region", "anterior_olfactory_nucleus" });

        Add("lateral_olfactory_gyrus",
            new[] { "olfactory_tract", "anterior_olfactory_nucleus", "piriform_region", "gyrus_ambiens" },
            new[] { "piriform_region", "anterior_olfactory_nucleus", "olfactory_tract" });

        Add("piriform_region",
            new[] { "anterior_olfactory_nucleus", "olfactory_tract", "lateral_olfactory_gyrus", "gyrus_ambiens", "limen_insula", "amygdaloid_complex" },
            new[] { "olfactory_bulb", "anterior_olfactory_nucleus", "gyrus_ambiens" });

        // ===================== Visual tracts =====================

        Add("optic_tract",
            new[] { "dorsal_lateral_geniculate_nucleus", "cerebral_peduncle_crus_cerebri", "supraoptic_region_of_HTH" },
            new[] { "optic_radiation", "dorsal_lateral_geniculate_nucleus", "superior_colliculus", "pretectal_region" });

        Add("optic_radiation",
            new[] { "dorsal_lateral_geniculate_nucleus", "white_matter_of_forebrain", "occipital_pole", "lingual_gyrus_medial_occipitotemporal_gyrus", "cuneus" },
            new[] { "dorsal_lateral_geniculate_nucleus", "optic_tract", "occipital_pole", "cuneus" });

        // ===================== Basal ganglia =====================

        Add("putamen",
            new[] { "external_segment_of_globus_pallidus", "internal_segment_of_globus_pallidus", "posteroventral_putamen", "head_of_caudate", "body_of_caudate", "claustrum" },
            new[] { "external_segment_of_globus_pallidus", "internal_segment_of_globus_pallidus", "head_of_caudate", "substantia_nigra" });

        Add("posteroventral_putamen",
            new[] { "putamen", "external_segment_of_globus_pallidus", "body_of_caudate", "internal_segment_of_globus_pallidus" },
            new[] { "putamen", "external_segment_of_globus_pallidus", "internal_segment_of_globus_pallidus" });

        Add("external_segment_of_globus_pallidus",
            new[] { "putamen", "internal_segment_of_globus_pallidus", "posteroventral_putamen" },
            new[] { "internal_segment_of_globus_pallidus", "putamen", "substantia_nigra" });

        Add("internal_segment_of_globus_pallidus",
            new[] { "external_segment_of_globus_pallidus", "putamen" },
            new[] { "external_segment_of_globus_pallidus", "ventral_anterior_nucleus_of_thalamus", "ventral_lateral_nucleus_of_thalamus", "substantia_nigra" });

        Add("head_of_caudate",
            new[] { "body_of_caudate", "putamen", "anterior_horn_of_lateral_ventricle", "nucleus_accumbens", "internal_segment_of_globus_pallidus" },
            new[] { "body_of_caudate", "tail_of_caudate", "putamen", "substantia_nigra" });

        Add("body_of_caudate",
            new[] { "head_of_caudate", "tail_of_caudate", "body_of_lateral_ventricle", "putamen", "thalamus" },
            new[] { "head_of_caudate", "tail_of_caudate", "putamen" });

        Add("tail_of_caudate",
            new[] { "body_of_caudate", "inferior_horn_of_lateral_ventricle", "amygdaloid_complex", "atrium_of_lateral_ventricle" },
            new[] { "head_of_caudate", "body_of_caudate", "putamen", "amygdaloid_complex" });

        Add("nucleus_accumbens",
            new[] { "head_of_caudate", "putamen", "septal_nuclei", "basal_forebrain", "bed_nucleus_of_stria_terminalis" },
            new[] { "substantia_nigra", "ventral_anterior_nucleus_of_thalamus", "amygdaloid_complex", "septal_nuclei" });

        Add("substantia_nigra",
            new[] { "cerebral_peduncle_crus_cerebri" },
            new[] { "putamen", "head_of_caudate", "internal_segment_of_globus_pallidus", "external_segment_of_globus_pallidus" });

        Add("claustrum",
            new[] { "putamen", "short_insular_gyri", "long_insular_gyri", "white_matter_of_forebrain" },
            new[] { "short_insular_gyri", "long_insular_gyri", "putamen" });

        // ===================== Thalamus and related =====================

        Add("thalamus",
            new[] { "third_ventricle", "body_of_lateral_ventricle", "internal_segment_of_globus_pallidus", "white_matter_of_forebrain", "body_of_caudate", "pulvinar_of_thalamus", "tuberal_region_of_HTH" },
            new[] { "pulvinar_of_thalamus", "mediodorsal_nucleus_of_thalamus", "ventral_lateral_nucleus_of_thalamus", "ventral_posterior_lateral_nucleus", "anterior_nuclear_complex_of_thalamus", "tuberal_region_of_HTH" });

        Add("anterior_nuclear_complex_of_thalamus",
            new[] { "mediodorsal_nucleus_of_thalamus", "lateral_dorsal_nucleus_of_thalamus", "midline_nuclear_complex", "fornix" },
            new[] { "mammillothalamic_tract", "fornix", "cingulate_gyrus_rostral_anterior_part" });

        Add("centromedian_nucleus_of_thalamus",
            new[] { "mediodorsal_nucleus_of_thalamus", "parafascicular_nucleus_of_thalamus", "ventral_posterior_lateral_nucleus", "pulvinar_of_thalamus" },
            new[] { "parafascicular_nucleus_of_thalamus", "putamen", "internal_segment_of_globus_pallidus" });

        Add("dorsal_lateral_geniculate_nucleus",
            new[] { "pulvinar_of_thalamus", "ventral_posterior_lateral_nucleus", "medial_geniculate_nuclei" },
            new[] { "optic_tract", "optic_radiation", "occipital_pole", "cuneus", "superior_colliculus" });

        Add("habenular_nuclei",
            new[] { "pineal_body", "third_ventricle", "mediodorsal_nucleus_of_thalamus" },
            new[] { "pineal_body", "substantia_nigra" });

        Add("lateral_dorsal_nucleus_of_thalamus",
            new[] { "anterior_nuclear_complex_of_thalamus", "lateral_posterior_nucleus_of_thalamus", "mediodorsal_nucleus_of_thalamus" },
            new[] { "anterior_nuclear_complex_of_thalamus", "lateral_posterior_nucleus_of_thalamus", "cingulate_gyrus_caudal_posterior_part" });

        Add("lateral_posterior_nucleus_of_thalamus",
            new[] { "lateral_dorsal_nucleus_of_thalamus", "pulvinar_of_thalamus", "mediodorsal_nucleus_of_thalamus" },
            new[] { "pulvinar_of_thalamus", "supraparietal_lobule", "lateral_dorsal_nucleus_of_thalamus" });

        Add("medial_geniculate_nuclei",
            new[] { "pulvinar_of_thalamus", "dorsal_lateral_geniculate_nucleus" },
            new[] { "transverse_temporal_gyrus_Heschls_gyrus", "superior_temporal_gyrus", "planum_temporale" });

        Add("mediodorsal_nucleus_of_thalamus",
            new[] { "anterior_nuclear_complex_of_thalamus", "centromedian_nucleus_of_thalamus", "midline_nuclear_complex", "lateral_dorsal_nucleus_of_thalamus" },
            new[] { "superior_frontal_gyrus", "middle_frontal_gyrus", "anterior_intermediate_orbital_gyrus", "cingulate_gyrus_rostral_anterior_part" });

        Add("midline_nuclear_complex",
            new[] { "mediodorsal_nucleus_of_thalamus", "anterior_nuclear_complex_of_thalamus", "third_ventricle", "reuniens_nucleus_medioventral_nucleus_of_thalamus" },
            new[] { "reuniens_nucleus_medioventral_nucleus_of_thalamus", "parafascicular_nucleus_of_thalamus", "supraoptic_region_of_HTH" });

        Add("parafascicular_nucleus_of_thalamus",
            new[] { "centromedian_nucleus_of_thalamus", "mediodorsal_nucleus_of_thalamus", "midline_nuclear_complex" },
            new[] { "centromedian_nucleus_of_thalamus", "head_of_caudate", "putamen" });

        Add("pulvinar_of_thalamus",
            new[] { "lateral_posterior_nucleus_of_thalamus", "dorsal_lateral_geniculate_nucleus", "medial_geniculate_nuclei", "mediodorsal_nucleus_of_thalamus", "centromedian_nucleus_of_thalamus" },
            new[] { "lateral_posterior_nucleus_of_thalamus", "supraparietal_lobule", "occipital_pole", "superior_colliculus" });

        Add("reuniens_nucleus_medioventral_nucleus_of_thalamus",
            new[] { "midline_nuclear_complex", "mediodorsal_nucleus_of_thalamus", "third_ventricle" },
            new[] { "midline_nuclear_complex", "head_of_hippocampus", "body_of_hippocampus", "mediodorsal_nucleus_of_thalamus" });

        Add("ventral_anterior_nucleus_of_thalamus",
            new[] { "ventral_lateral_nucleus_of_thalamus", "mediodorsal_nucleus_of_thalamus", "anterior_nuclear_complex_of_thalamus" },
            new[] { "internal_segment_of_globus_pallidus", "ventral_lateral_nucleus_of_thalamus", "superior_frontal_gyrus", "precentral_gyrus" });

        Add("ventral_lateral_nucleus_of_thalamus",
            new[] { "ventral_anterior_nucleus_of_thalamus", "ventral_posterior_lateral_nucleus", "mediodorsal_nucleus_of_thalamus" },
            new[] { "ventral_anterior_nucleus_of_thalamus", "precentral_gyrus", "internal_segment_of_globus_pallidus", "superior_cerebellar_peduncle_brachium_conjunctivum", "cerebellar_deep_nuclei" });

        Add("ventral_posterior_lateral_nucleus",
            new[] { "ventral_lateral_nucleus_of_thalamus", "ventral_posterior_medial_nucleus", "pulvinar_of_thalamus" },
            new[] { "postcentral_gyrus", "ventral_posterior_medial_nucleus" });

        Add("ventral_posterior_medial_nucleus",
            new[] { "ventral_posterior_lateral_nucleus", "ventral_lateral_nucleus_of_thalamus", "centromedian_nucleus_of_thalamus" },
            new[] { "postcentral_gyrus", "ventral_posterior_lateral_nucleus" });

        // ===================== Amygdala / hippocampus =====================

        Add("amygdaloid_complex",
            new[] { "head_of_hippocampus", "anterior_parahippocampal_gyrus", "gyrus_ambiens", "piriform_region", "temporal_pole", "tail_of_caudate", "inferior_horn_of_lateral_ventricle" },
            new[] { "head_of_hippocampus", "anterior_parahippocampal_gyrus", "bed_nucleus_of_stria_terminalis", "supraoptic_region_of_HTH", "central_nuclear_group" });

        Add("amygdalohippocampal_area",
            new[] { "amygdaloid_complex", "head_of_hippocampus", "anterior_parahippocampal_gyrus", "basolateral_nucleus_basal_nucleus" },
            new[] { "amygdaloid_complex", "head_of_hippocampus", "anterior_parahippocampal_gyrus" });

        Add("anterior_amygdaloid_area",
            new[] { "amygdaloid_complex", "basolateral_nucleus_basal_nucleus", "central_nuclear_group", "piriform_region" },
            new[] { "amygdaloid_complex", "central_nuclear_group", "basolateral_nucleus_basal_nucleus" });

        Add("anterior_cortical_nucleus",
            new[] { "amygdaloid_complex", "posterior_cortical_nucleus", "medial_nucleus", "gyrus_ambiens", "piriform_region" },
            new[] { "olfactory_tract", "piriform_region", "posterior_cortical_nucleus", "anterior_olfactory_nucleus" });

        Add("basolateral_nucleus_basal_nucleus",
            new[] { "amygdaloid_complex", "basomedial_nucleus_accessory_basal_nucleus", "lateral_nucleus", "central_nuclear_group", "amygdalohippocampal_area" },
            new[] { "lateral_nucleus", "central_nuclear_group", "basomedial_nucleus_accessory_basal_nucleus", "head_of_hippocampus" });

        Add("basomedial_nucleus_accessory_basal_nucleus",
            new[] { "basolateral_nucleus_basal_nucleus", "medial_nucleus", "central_nuclear_group", "amygdaloid_complex" },
            new[] { "basolateral_nucleus_basal_nucleus", "medial_nucleus", "central_nuclear_group" });

        Add("central_nuclear_group",
            new[] { "basolateral_nucleus_basal_nucleus", "basomedial_nucleus_accessory_basal_nucleus", "medial_nucleus", "anterior_amygdaloid_area", "bed_nucleus_of_stria_terminalis" },
            new[] { "bed_nucleus_of_stria_terminalis", "supraoptic_region_of_HTH", "basolateral_nucleus_basal_nucleus" });

        Add("lateral_nucleus",
            new[] { "basolateral_nucleus_basal_nucleus", "amygdaloid_complex", "basomedial_nucleus_accessory_basal_nucleus", "tail_of_caudate" },
            new[] { "basolateral_nucleus_basal_nucleus", "central_nuclear_group", "amygdaloid_complex" });

        Add("medial_nucleus",
            new[] { "basomedial_nucleus_accessory_basal_nucleus", "anterior_cortical_nucleus", "central_nuclear_group", "posterior_cortical_nucleus" },
            new[] { "anterior_cortical_nucleus", "posterior_cortical_nucleus", "supraoptic_region_of_HTH" });

        Add("posterior_cortical_nucleus",
            new[] { "amygdaloid_complex", "anterior_cortical_nucleus", "medial_nucleus" },
            new[] { "anterior_cortical_nucleus", "piriform_region", "medial_nucleus" });

        Add("head_of_hippocampus",
            new[] { "body_of_hippocampus", "amygdaloid_complex", "amygdalohippocampal_area", "anterior_parahippocampal_gyrus", "inferior_horn_of_lateral_ventricle" },
            new[] { "body_of_hippocampus", "tail_of_hippocampus", "anterior_parahippocampal_gyrus", "fornix", "amygdaloid_complex" });

        Add("body_of_hippocampus",
            new[] { "head_of_hippocampus", "tail_of_hippocampus", "posterior_parahippocampal_gyrus", "inferior_horn_of_lateral_ventricle", "fornix" },
            new[] { "head_of_hippocampus", "tail_of_hippocampus", "fornix", "anterior_parahippocampal_gyrus", "mammillothalamic_tract" });

        Add("tail_of_hippocampus",
            new[] { "body_of_hippocampus", "posterior_parahippocampal_gyrus", "ingulo_parahippocampal_isthmus", "atrium_of_lateral_ventricle" },
            new[] { "body_of_hippocampus", "head_of_hippocampus", "posterior_parahippocampal_gyrus" });

        // ===================== Hypothalamus regions =====================

        Add("preoptic_region_of_HTH",
            new[] { "supraoptic_region_of_HTH", "anterior_commissure", "septal_nuclei" },
            new[] { "supraoptic_region_of_HTH", "tuberal_region_of_HTH", "septal_nuclei" });

        Add("supraoptic_region_of_HTH",
            new[] { "preoptic_region_of_HTH", "tuberal_region_of_HTH", "optic_tract" },
            new[] { "preoptic_region_of_HTH", "tuberal_region_of_HTH", "amygdaloid_complex" });

        Add("tuberal_region_of_HTH",
            new[] { "supraoptic_region_of_HTH", "third_ventricle" },
            new[] { "supraoptic_region_of_HTH", "preoptic_region_of_HTH", "mammillothalamic_tract" });

        // ===================== Basal forebrain & related =====================

        Add("basal_forebrain",
            new[] { "nucleus_accumbens", "septal_nuclei", "anterior_commissure", "bed_nucleus_of_stria_terminalis", "white_matter_of_forebrain" },
            new[] { "septal_nuclei", "nucleus_accumbens", "supraoptic_region_of_HTH", "amygdaloid_complex" });

        Add("bed_nucleus_of_stria_terminalis",
            new[] { "septal_nuclei", "anterior_commissure", "nucleus_accumbens", "tuberal_region_of_HTH", "basal_forebrain" },
            new[] { "amygdaloid_complex", "central_nuclear_group", "supraoptic_region_of_HTH", "septal_nuclei" });

        Add("septal_nuclei",
            new[] { "subcallosal_gyrus_parolfactory_gyrus", "anterior_commissure", "fornix", "basal_forebrain", "bed_nucleus_of_stria_terminalis", "third_ventricle" },
            new[] { "preoptic_region_of_HTH", "fornix", "head_of_hippocampus", "body_of_hippocampus" });

        Add("zona_incerta",
            new[] { "tuberal_region_of_HTH", "thalamus", "substantia_nigra" },
            new[] { "substantia_nigra", "supraoptic_region_of_HTH", "thalamus" });

        Add("fornix",
            new[] { "body_of_hippocampus", "head_of_hippocampus", "septal_nuclei", "anterior_commissure", "third_ventricle", "corpus_callosum" },
            new[] { "body_of_hippocampus", "anterior_nuclear_complex_of_thalamus", "mammillothalamic_tract" });

        Add("corpus_callosum",
            new[] { "cingulate_gyrus_rostral_anterior_part", "cingulate_gyrus_caudal_posterior_part", "white_matter_of_forebrain", "body_of_lateral_ventricle", "fornix", "septal_nuclei" },
            new[] { "white_matter_of_forebrain", "anterior_commissure" });

        Add("anterior_commissure",
            new[] { "bed_nucleus_of_stria_terminalis", "septal_nuclei", "basal_forebrain", "fornix", "third_ventricle" },
            new[] { "corpus_callosum", "fornix", "basal_forebrain" });

        Add("white_matter_of_forebrain",
            new[] { "corpus_callosum", "putamen", "head_of_caudate", "body_of_caudate", "claustrum", "thalamus", "optic_radiation" },
            new[] { "corpus_callosum", "optic_radiation", "white_matter_of_hindbrain", "cerebral_peduncle_crus_cerebri" });

        // ===================== Midbrain =====================

        Add("pretectal_region",
            new[] { "superior_colliculus", "third_ventricle", "pulvinar_of_thalamus" },
            new[] { "superior_colliculus", "optic_tract" });

        Add("pineal_body",
            new[] { "habenular_nuclei", "third_ventricle", "superior_colliculus" },
            new[] { "habenular_nuclei", "tuberal_region_of_HTH" });

        Add("superior_colliculus",
            new[] { "pretectal_region", "pulvinar_of_thalamus", "pineal_body" },
            new[] { "optic_tract", "dorsal_lateral_geniculate_nucleus", "pulvinar_of_thalamus", "pretectal_region" });

        Add("cerebral_peduncle_crus_cerebri",
            new[] { "substantia_nigra", "basilar_part_of_pons", "optic_tract" },
            new[] { "precentral_gyrus", "basilar_part_of_pons", "pyramidal_part_of_medulla_oblongata", "white_matter_of_forebrain" });

        // ===================== Pons / medulla =====================

        Add("basilar_part_of_pons",
            new[] { "pontine_tegmentum", "cerebral_peduncle_crus_cerebri", "middle_cerebellar_peduncle", "pyramidal_part_of_medulla_oblongata" },
            new[] { "middle_cerebellar_peduncle", "lateral_hemisphere_of_cerebellum", "cerebral_peduncle_crus_cerebri", "pyramidal_part_of_medulla_oblongata" });

        Add("pontine_tegmentum",
            new[] { "basilar_part_of_pons", "fourth_ventricle", "tegmentum_of_medulla_oblongata", "superior_cerebellar_peduncle_brachium_conjunctivum" },
            new[] { "tegmentum_of_medulla_oblongata", "superior_cerebellar_peduncle_brachium_conjunctivum" });

        Add("tegmentum_of_medulla_oblongata",
            new[] { "pyramidal_part_of_medulla_oblongata", "inferior_olive", "fourth_ventricle", "central_canal_of_medulla_oblongata", "pontine_tegmentum", "inferior_cerebellar_peduncle" },
            new[] { "pontine_tegmentum", "inferior_cerebellar_peduncle", "inferior_olive" });

        Add("pyramidal_part_of_medulla_oblongata",
            new[] { "tegmentum_of_medulla_oblongata", "inferior_olive", "basilar_part_of_pons" },
            new[] { "precentral_gyrus", "cerebral_peduncle_crus_cerebri", "basilar_part_of_pons" });

        Add("inferior_olive",
            new[] { "tegmentum_of_medulla_oblongata", "pyramidal_part_of_medulla_oblongata", "inferior_cerebellar_peduncle" },
            new[] { "inferior_cerebellar_peduncle", "cerebellar_vermis", "lateral_hemisphere_of_cerebellum" });

        // ===================== Cerebellum =====================

        Add("lateral_hemisphere_of_cerebellum",
            new[] { "paravermis_of_cerebellum", "cerebellar_deep_nuclei", "middle_cerebellar_peduncle", "superior_cerebellar_peduncle_brachium_conjunctivum", "white_matter_of_hindbrain" },
            new[] { "paravermis_of_cerebellum", "cerebellar_vermis", "cerebellar_deep_nuclei", "basilar_part_of_pons" });

        Add("paravermis_of_cerebellum",
            new[] { "lateral_hemisphere_of_cerebellum", "cerebellar_vermis", "cerebellar_deep_nuclei", "white_matter_of_hindbrain" },
            new[] { "lateral_hemisphere_of_cerebellum", "cerebellar_vermis", "cerebellar_deep_nuclei" });

        Add("cerebellar_vermis",
            new[] { "paravermis_of_cerebellum", "cerebellar_deep_nuclei", "fourth_ventricle", "inferior_cerebellar_peduncle", "white_matter_of_hindbrain" },
            new[] { "paravermis_of_cerebellum", "lateral_hemisphere_of_cerebellum", "cerebellar_deep_nuclei" });

        Add("cerebellar_deep_nuclei",
            new[] { "lateral_hemisphere_of_cerebellum", "paravermis_of_cerebellum", "cerebellar_vermis", "superior_cerebellar_peduncle_brachium_conjunctivum", "white_matter_of_hindbrain" },
            new[] { "superior_cerebellar_peduncle_brachium_conjunctivum", "ventral_lateral_nucleus_of_thalamus", "lateral_hemisphere_of_cerebellum" });

        Add("superior_cerebellar_peduncle_brachium_conjunctivum",
            new[] { "cerebellar_deep_nuclei", "pontine_tegmentum", "lateral_hemisphere_of_cerebellum", "fourth_ventricle" },
            new[] { "cerebellar_deep_nuclei", "ventral_lateral_nucleus_of_thalamus", "middle_cerebellar_peduncle" });

        Add("middle_cerebellar_peduncle",
            new[] { "basilar_part_of_pons", "lateral_hemisphere_of_cerebellum", "fourth_ventricle", "white_matter_of_hindbrain", "inferior_cerebellar_peduncle" },
            new[] { "basilar_part_of_pons", "lateral_hemisphere_of_cerebellum", "superior_cerebellar_peduncle_brachium_conjunctivum", "inferior_cerebellar_peduncle" });

        Add("inferior_cerebellar_peduncle",
            new[] { "tegmentum_of_medulla_oblongata", "middle_cerebellar_peduncle", "cerebellar_vermis", "fourth_ventricle", "white_matter_of_hindbrain" },
            new[] { "inferior_olive", "cerebellar_vermis", "middle_cerebellar_peduncle", "tegmentum_of_medulla_oblongata" });

        Add("white_matter_of_hindbrain",
            new[] { "lateral_hemisphere_of_cerebellum", "paravermis_of_cerebellum", "cerebellar_vermis", "middle_cerebellar_peduncle", "inferior_cerebellar_peduncle", "superior_cerebellar_peduncle_brachium_conjunctivum" },
            new[] { "white_matter_of_forebrain", "middle_cerebellar_peduncle", "inferior_cerebellar_peduncle", "superior_cerebellar_peduncle_brachium_conjunctivum" });

        // ===================== Tracts =====================

        Add("mammillothalamic_tract",
            new[] { "anterior_nuclear_complex_of_thalamus", "fornix", "third_ventricle" },
            new[] { "anterior_nuclear_complex_of_thalamus", "fornix", "body_of_hippocampus" });

        // ===================== Ventricles / CSF =====================

        Add("anterior_horn_of_lateral_ventricle",
            new[] { "body_of_lateral_ventricle", "head_of_caudate", "corpus_callosum", "septal_nuclei", "third_ventricle" },
            new[] { "body_of_lateral_ventricle", "third_ventricle", "atrium_of_lateral_ventricle" });

        Add("body_of_lateral_ventricle",
            new[] { "anterior_horn_of_lateral_ventricle", "atrium_of_lateral_ventricle", "body_of_caudate", "thalamus", "corpus_callosum", "fornix" },
            new[] { "anterior_horn_of_lateral_ventricle", "atrium_of_lateral_ventricle", "third_ventricle" });

        Add("atrium_of_lateral_ventricle",
            new[] { "body_of_lateral_ventricle", "posterior_horn_of_lateral_ventricle", "inferior_horn_of_lateral_ventricle", "tail_of_caudate", "tail_of_hippocampus" },
            new[] { "body_of_lateral_ventricle", "posterior_horn_of_lateral_ventricle", "inferior_horn_of_lateral_ventricle" });

        Add("posterior_horn_of_lateral_ventricle",
            new[] { "atrium_of_lateral_ventricle", "occipital_pole", "lingual_gyrus_medial_occipitotemporal_gyrus" },
            new[] { "atrium_of_lateral_ventricle", "body_of_lateral_ventricle" });

        Add("inferior_horn_of_lateral_ventricle",
            new[] { "atrium_of_lateral_ventricle", "head_of_hippocampus", "body_of_hippocampus", "amygdaloid_complex", "tail_of_caudate" },
            new[] { "head_of_hippocampus", "body_of_hippocampus", "atrium_of_lateral_ventricle" });

        Add("third_ventricle",
            new[] { "thalamus", "tuberal_region_of_HTH", "anterior_horn_of_lateral_ventricle", "cerebral_aqueduct", "fornix", "midline_nuclear_complex" },
            new[] { "cerebral_aqueduct", "body_of_lateral_ventricle", "fourth_ventricle" });

        Add("cerebral_aqueduct",
            new[] { "third_ventricle", "fourth_ventricle", "pretectal_region" },
            new[] { "third_ventricle", "fourth_ventricle" });

        Add("fourth_ventricle",
            new[] { "cerebral_aqueduct", "central_canal_of_medulla_oblongata", "pontine_tegmentum", "tegmentum_of_medulla_oblongata", "cerebellar_vermis", "middle_cerebellar_peduncle", "inferior_cerebellar_peduncle", "superior_cerebellar_peduncle_brachium_conjunctivum" },
            new[] { "cerebral_aqueduct", "central_canal_of_medulla_oblongata", "third_ventricle" });

        Add("central_canal_of_medulla_oblongata",
            new[] { "fourth_ventricle", "tegmentum_of_medulla_oblongata" },
            new[] { "fourth_ventricle", "tegmentum_of_medulla_oblongata" });

        return d;
    }
}
