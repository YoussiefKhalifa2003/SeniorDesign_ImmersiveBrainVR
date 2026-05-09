using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class CreateSampleRegionData
{
    const string FolderPath = "Assets/Data/BrainRegions";

    [MenuItem("Tools/Brain Dissection/Create Sample Region Data")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(FolderPath))
            AssetDatabase.CreateFolder("Assets/Data", "BrainRegions");

        CreateRegion("Allen_angular_gyrus_L", "Angular Gyrus (L)", RegionData.Hemisphere.Left,
            "Involved in language and number processing.",
            "The angular gyrus is in the parietal lobe and is involved in semantic processing, number processing, and attention. It connects visual and language areas.");
        CreateRegion("Allen_body_of_hippocampus_L", "Body of Hippocampus (L)", RegionData.Hemisphere.Left,
            "Key structure for memory formation.",
            "The body of the hippocampus is part of the hippocampal formation and is essential for forming new declarative memories and spatial memory.");
        CreateRegion("Allen_amygdaloid_complex_L", "Amygdaloid Complex (L)", RegionData.Hemisphere.Left,
            "Involved in emotion processing, especially fear.",
            "The amygdaloid complex is a set of nuclei involved in emotional processing, particularly fear and aggression. It plays a role in emotional memory.");
        CreateRegion("Allen_lateral_hemisphere_of_cerebellum_L", "Lateral Hemisphere of Cerebellum (L)", RegionData.Hemisphere.Left,
            "Coordinates voluntary movement and balance.",
            "The cerebellar lateral hemispheres help coordinate voluntary movements and fine motor control.");
        CreateRegion("Allen_thalamus_R", "Thalamus (R)", RegionData.Hemisphere.Right,
            "Relay station for sensory and motor signals.",
            "The thalamus relays sensory and motor signals to the cerebral cortex and regulates consciousness and sleep.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Sample Region Data assets created in " + FolderPath);
    }

    static void CreateRegion(string id, string displayName, RegionData.Hemisphere hemisphere, string shortDesc, string detailedDesc)
    {
        var safeName = id.Replace(" ", "_").Replace("(", "").Replace(")", "");
        var path = FolderPath + "/" + safeName + ".asset";
        if (AssetDatabase.LoadAssetAtPath<RegionData>(path) != null) return;
        var data = ScriptableObject.CreateInstance<RegionData>();
        data.regionId = id;
        data.displayName = displayName;
        data.hemisphere = hemisphere;
        data.shortDescription = shortDesc;
        data.detailedDescription = detailedDesc;
        AssetDatabase.CreateAsset(data, path);
    }

    [MenuItem("Tools/Brain Dissection/Add Region Components To Brain")]
    public static void AddRegionComponentsToBrain()
    {
        var brainRoot = GameObject.Find("BrainRoot");
        if (brainRoot == null)
        {
            Debug.LogError("Add Region Components: No GameObject named 'BrainRoot' in scene.");
            return;
        }
        var regionAssets = AssetDatabase.FindAssets("t:RegionData", new[] { "Assets/Data/BrainRegions" });
        int added = 0;
        foreach (var guid in regionAssets)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<RegionData>(path);
            if (data == null) continue;
            var go = FindChildRecursive(brainRoot.transform, data.regionId);
            if (go == null) continue;
            // 1. Add MeshCollider first (needed for ray interaction)
            var collider = go.GetComponent<Collider>();
            if (collider == null)
            {
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
                else
                {
                    // Fallback: box collider around the renderer bounds
                    go.AddComponent<BoxCollider>();
                }
            }
            // 2. Add XRSimpleInteractable (for hover/select events)
            var interactable = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (interactable == null) go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            // 3. Remove or freeze any Rigidbody that got auto-added (prevents regions from falling)
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            // 4. Add BrainRegion and link data
            var region = go.GetComponent<BrainRegion>();
            if (region == null) region = go.AddComponent<BrainRegion>();
            region.regionData = data;
            added++;
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Add Region Components: configured " + added + " regions with BrainRegion and XRSimpleInteractable.");
    }

    static GameObject FindChildRecursive(Transform root, string name)
    {
        if (root.name == name) return root.gameObject;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildRecursive(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    static readonly string[] AllRegionIds = new string[]
    {
        "Allen_amygdalohippocampal_area_L","Allen_amygdaloid_complex_L","Allen_angular_gyrus_L","Allen_anterior_amygdaloid_area_L","Allen_anterior_commissure_L","Allen_anterior_cortical_nucleus_L","Allen_anterior_horn_of_lateral_ventricle_L","Allen_anterior_intermediate_orbital_gyrus_L","Allen_anterior_nuclear_complex_of_thalamus_L","Allen_anterior_olfactory_nucleus_L","Allen_anterior_parahippocampal_gyrus_L","Allen_atrium_of_lateral_ventricle_L","Allen_basal_forebrain_L","Allen_basilar_part_of_pons_L","Allen_basolateral_nucleus_basal_nucleus_L","Allen_basomedial_nucleus_accessory_basal_nucleus_L","Allen_bed_nucleus_of_stria_terminalis_L","Allen_body_of_caudate_L","Allen_body_of_hippocampus_L","Allen_body_of_lateral_ventricle_L","Allen_central_canal_of_medulla_oblongata_L","Allen_central_nuclear_group_L","Allen_centromedian_nucleus_of_thalamus_L","Allen_cerebellar_deep_nuclei_L","Allen_cerebellar_vermis_L","Allen_cerebral_aqueduct_L","Allen_cerebral_peduncle_crus_cerebri_L","Allen_cingulate_gyrus_caudal_posterior_part_L","Allen_cingulate_gyrus_rostral_anterior_part_L","Allen_claustrum_L","Allen_corpus_callosum_L","Allen_cuneus_L","Allen_dorsal_lateral_geniculate_nucleus_L","Allen_external_segment_of_globus_pallidus_L","Allen_fornix_L","Allen_fourth_ventricle_L","Allen_frontal_agranular_insular_cortex_area_Fl_L","Allen_frontal_operculum_L","Allen_frontal_pole_L","Allen_frontomarginal_gyrus_L","Allen_gyrus_ambiens_L","Allen_gyrus_rectus_straight_gyrus_L","Allen_habenular_nuclei_L","Allen_head_of_caudate_L","Allen_head_of_hippocampus_L","Allen_inferior_cerebellar_peduncle_L","Allen_inferior_frontal_gyrus_opercular_part_L","Allen_inferior_frontal_gyrus_triangular_part_L","Allen_inferior_horn_of_lateral_ventricle_L","Allen_inferior_occipital_gyrus_L","Allen_inferior_olive_L","Allen_inferior_temporal_gyrus_L","Allen_ingulo_parahippocampal_isthmus_L","Allen_internal_segment_of_globus_pallidus_L","Allen_lateral_dorsal_nucleus_of_thalamus_L","Allen_lateral_hemisphere_of_cerebellum_L","Allen_lateral_nucleus_L","Allen_lateral_occipitotemporal_fusiform_gyrus_occipital_part_L","Allen_lateral_olfactory_gyrus_L","Allen_lateral_orbital_gyrus_L","Allen_lateral_posterior_nucleus_of_thalamus_L","Allen_limen_insula_L","Allen_lingual_gyrus_medial_occipitotemporal_gyrus_L","Allen_long_insular_gyri_L","Allen_mammillothalamic_tract_L","Allen_medial_geniculate_nuclei_L","Allen_medial_nucleus_L","Allen_medial_orbital_gyrus_L","Allen_mediodorsal_nucleus_of_thalamus_L","Allen_middle_cerebellar_peduncle_L","Allen_middle_frontal_gyrus_L","Allen_middle_temporal_gyrus_L","Allen_midline_nuclear_complex_L","Allen_nucleus_accumbens_L","Allen_occipital_pole_L","Allen_occipitotemporal_fusiform_gyrus_temporal_part_L","Allen_olfactory_bulb_L","Allen_olfactory_tract_L","Allen_optic_radiation_L","Allen_optic_tract_L","Allen_paracentral_lobule_caudal_part_L","Allen_paracentral_lobule_rostral_part_L","Allen_paracingulate_gyrus_L","Allen_parafascicular_nucleus_of_thalamus_L","Allen_paravermis_of_cerebellum_L","Allen_parietal_operculum_L","Allen_perirhinal_gyrus_rostral_part_of_FuGt_L","Allen_pineal_body_L","Allen_piriform_region_L","Allen_planum_polare_L","Allen_planum_temporale_L","Allen_pontine_tegmentum_L","Allen_postcentral_gyrus_L","Allen_posterior_cortical_nucleus_L","Allen_posterior_horn_of_lateral_ventricle_L","Allen_posterior_intermediate_orbital_gyrus_L","Allen_posterior_parahippocampal_gyrus_L","Allen_posteroventral_putamen_L","Allen_precentral_gyrus_L","Allen_precuneus_L","Allen_preoptic_region_of_HTH_L","Allen_pretectal_region_L","Allen_pulvinar_of_thalamus_L","Allen_putamen_L","Allen_pyramidal_part_of_medulla_oblongata_L","Allen_reuniens_nucleus_medioventral_nucleus_of_thalamus_L","Allen_rostral_gyrus_L","Allen_septal_nuclei_L","Allen_short_insular_gyri_L","Allen_subcallosal_gyrus_parolfactory_gyrus_L","Allen_substantia_nigra_L","Allen_superior_cerebellar_peduncle_brachium_conjunctivum_L","Allen_superior_colliculus_L","Allen_superior_frontal_gyrus_L","Allen_superior_occipital_gyrus_L","Allen_superior_temporal_gyrus_L","Allen_supramarginal_gyrus_L","Allen_supraoptic_region_of_HTH_L","Allen_supraparietal_lobule_L","Allen_tail_of_caudate_L","Allen_tail_of_hippocampus_L","Allen_tegmentum_of_medulla_oblongata_L","Allen_temporal_agranular_insular_cortex_area_Tl_L","Allen_temporal_pole_L","Allen_thalamus_L","Allen_third_ventricle_L","Allen_transverse_temporal_gyrus_Heschls_gyrus_L","Allen_tuberal_region_of_HTH_L","Allen_ventral_anterior_nucleus_of_thalamus_L","Allen_ventral_lateral_nucleus_of_thalamus_L","Allen_ventral_posterior_lateral_nucleus_L","Allen_ventral_posterior_medial_nucleus_L","Allen_white_matter_of_forebrain_L","Allen_white_matter_of_hindbrain_L","Allen_zona_incerta_L",
        "Allen_amygdalohippocampal_area_R","Allen_amygdaloid_complex_R","Allen_angular_gyrus_R","Allen_anterior_amygdaloid_area_R","Allen_anterior_commissure_R","Allen_anterior_cortical_nucleus_R","Allen_anterior_horn_of_lateral_ventricle_R","Allen_anterior_intermediate_orbital_gyrus_R","Allen_anterior_nuclear_complex_of_thalamus_R","Allen_anterior_olfactory_nucleus_R","Allen_anterior_parahippocampal_gyrus_R","Allen_atrium_of_lateral_ventricle_R","Allen_basal_forebrain_R","Allen_basilar_part_of_pons_R","Allen_basolateral_nucleus_basal_nucleus_R","Allen_basomedial_nucleus_accessory_basal_nucleus_R","Allen_bed_nucleus_of_stria_terminalis_R","Allen_body_of_caudate_R","Allen_body_of_hippocampus_R","Allen_body_of_lateral_ventricle_R","Allen_central_canal_of_medulla_oblongata_R","Allen_central_nuclear_group_R","Allen_centromedian_nucleus_of_thalamus_R","Allen_cerebellar_deep_nuclei_R","Allen_cerebellar_vermis_R","Allen_cerebral_aqueduct_R","Allen_cerebral_peduncle_crus_cerebri_R","Allen_cingulate_gyrus_caudal_posterior_part_R","Allen_cingulate_gyrus_rostral_anterior_part_R","Allen_claustrum_R","Allen_corpus_callosum_R","Allen_cuneus_R","Allen_dorsal_lateral_geniculate_nucleus_R","Allen_external_segment_of_globus_pallidus_R","Allen_fornix_R","Allen_fourth_ventricle_R","Allen_frontal_agranular_insular_cortex_area_Fl_R","Allen_frontal_operculum_R","Allen_frontal_pole_R","Allen_frontomarginal_gyrus_R","Allen_gyrus_ambiens_R","Allen_gyrus_rectus_straight_gyrus_R","Allen_habenular_nuclei_R","Allen_head_of_caudate_R","Allen_head_of_hippocampus_R","Allen_inferior_cerebellar_peduncle_R","Allen_inferior_frontal_gyrus_opercular_part_R","Allen_inferior_frontal_gyrus_triangular_part_R","Allen_inferior_horn_of_lateral_ventricle_R","Allen_inferior_occipital_gyrus_R","Allen_inferior_olive_R","Allen_inferior_temporal_gyrus_R","Allen_ingulo_parahippocampal_isthmus_R","Allen_internal_segment_of_globus_pallidus_R","Allen_lateral_dorsal_nucleus_of_thalamus_R","Allen_lateral_hemisphere_of_cerebellum_R","Allen_lateral_nucleus_R","Allen_lateral_occipitotemporal_fusiform_gyrus_occipital_part_R","Allen_lateral_olfactory_gyrus_R","Allen_lateral_orbital_gyrus_R","Allen_lateral_posterior_nucleus_of_thalamus_R","Allen_limen_insula_R","Allen_lingual_gyrus_medial_occipitotemporal_gyrus_R","Allen_long_insular_gyri_R","Allen_mammillothalamic_tract_R","Allen_medial_geniculate_nuclei_R","Allen_medial_nucleus_R","Allen_medial_orbital_gyrus_R","Allen_mediodorsal_nucleus_of_thalamus_R","Allen_middle_cerebellar_peduncle_R","Allen_middle_frontal_gyrus_R","Allen_middle_temporal_gyrus_R","Allen_midline_nuclear_complex_R","Allen_nucleus_accumbens_R","Allen_occipital_pole_R","Allen_occipitotemporal_fusiform_gyrus_temporal_part_R","Allen_olfactory_bulb_R","Allen_olfactory_tract_R","Allen_optic_radiation_R","Allen_optic_tract_R","Allen_paracentral_lobule_caudal_part_R","Allen_paracentral_lobule_rostral_part_R","Allen_paracingulate_gyrus_R","Allen_parafascicular_nucleus_of_thalamus_R","Allen_paravermis_of_cerebellum_R","Allen_parietal_operculum_R","Allen_perirhinal_gyrus_rostral_part_of_FuGt_R","Allen_pineal_body_R","Allen_piriform_region_R","Allen_planum_polare_R","Allen_planum_temporale_R","Allen_pontine_tegmentum_R","Allen_postcentral_gyrus_R","Allen_posterior_cortical_nucleus_R","Allen_posterior_horn_of_lateral_ventricle_R","Allen_posterior_intermediate_orbital_gyrus_R","Allen_posterior_parahippocampal_gyrus_R","Allen_posteroventral_putamen_R","Allen_precentral_gyrus_R","Allen_precuneus_R","Allen_preoptic_region_of_HTH_R","Allen_pretectal_region_R","Allen_pulvinar_of_thalamus_R","Allen_putamen_R","Allen_pyramidal_part_of_medulla_oblongata_R","Allen_reuniens_nucleus_medioventral_nucleus_of_thalamus_R","Allen_rostral_gyrus_R","Allen_septal_nuclei_R","Allen_short_insular_gyri_R","Allen_subcallosal_gyrus_parolfactory_gyrus_R","Allen_substantia_nigra_R","Allen_superior_cerebellar_peduncle_brachium_conjunctivum_R","Allen_superior_colliculus_R","Allen_superior_frontal_gyrus_R","Allen_superior_occipital_gyrus_R","Allen_superior_temporal_gyrus_R","Allen_supramarginal_gyrus_R","Allen_supraoptic_region_of_HTH_R","Allen_supraparietal_lobule_R","Allen_tail_of_caudate_R","Allen_tail_of_hippocampus_R","Allen_tegmentum_of_medulla_oblongata_R","Allen_temporal_agranular_insular_cortex_area_Tl_R","Allen_temporal_pole_R","Allen_thalamus_R","Allen_third_ventricle_R","Allen_transverse_temporal_gyrus_Heschls_gyrus_R","Allen_tuberal_region_of_HTH_R","Allen_ventral_anterior_nucleus_of_thalamus_R","Allen_ventral_lateral_nucleus_of_thalamus_R","Allen_ventral_posterior_lateral_nucleus_R","Allen_ventral_posterior_medial_nucleus_R","Allen_white_matter_of_forebrain_R","Allen_white_matter_of_hindbrain_R","Allen_zona_incerta_R"
    };

    static string IdToDisplayName(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        var s = id.Replace("Allen_", "").Replace("_L", " (L)").Replace("_R", " (R)");
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.Replace("_", " ").ToLowerInvariant());
    }

    [MenuItem("Tools/Brain Dissection/Create All 132 Region Data Assets")]
    public static void CreateAll132Regions()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(FolderPath))
            AssetDatabase.CreateFolder("Assets/Data", "BrainRegions");
        int created = 0;
        foreach (var id in AllRegionIds)
        {
            var path = FolderPath + "/" + id + ".asset";
            if (AssetDatabase.LoadAssetAtPath<RegionData>(path) != null) continue;
            var data = ScriptableObject.CreateInstance<RegionData>();
            data.regionId = id;
            data.displayName = IdToDisplayName(id);
            data.hemisphere = id.EndsWith("_L") ? RegionData.Hemisphere.Left : RegionData.Hemisphere.Right;
            data.shortDescription = "Brain region: " + data.displayName;
            data.detailedDescription = "Add a detailed description for this region for your psychology students.";
            AssetDatabase.CreateAsset(data, path);
            created++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created " + created + " Region Data assets. Run 'Add Region Components To Brain' with BrainDissectionScene open to wire them up.");
    }
}
