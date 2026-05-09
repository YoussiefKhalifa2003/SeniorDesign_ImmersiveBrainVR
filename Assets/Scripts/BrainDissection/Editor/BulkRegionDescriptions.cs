using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot editor menu that fills every RegionData asset with a curated,
/// undergraduate-friendly description. The text is written into
/// <see cref="RegionData.detailedDescription"/> so it shows up as the body
/// paragraph in the region info panel; <c>shortDescription</c> is cleared
/// because <c>RegionUIController</c> now builds the subtitle from displayName.
///
/// Run via: Tools > Brain Dissection > Bulk Fill Region Descriptions.
/// Re-runnable; existing curated descriptions are overwritten so the panel
/// stays consistent across the whole dataset.
/// </summary>
public static class BulkRegionDescriptions
{
    [MenuItem("Tools/Brain Dissection/Bulk Fill Region Descriptions")]
    public static void Run()
    {
        var lookup = BuildLookup();
        var guids = AssetDatabase.FindAssets("t:RegionData");
        int updated = 0;
        int missed = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<RegionData>(path);
            if (data == null) continue;

            string baseKey = StripPrefixAndHemisphere(data.regionId, out string hemisphereTag);
            if (string.IsNullOrEmpty(baseKey)) continue;

            if (!lookup.TryGetValue(baseKey, out string sentence))
            {
                missed++;
                Debug.LogWarning($"[BulkRegionDescriptions] No entry for '{data.regionId}' (key '{baseKey}')");
                continue;
            }

            data.detailedDescription = ApplyHemisphere(sentence, hemisphereTag);
            data.shortDescription = ""; // panel auto-builds the subtitle from displayName
            EditorUtility.SetDirty(data);
            updated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[BulkRegionDescriptions] Updated {updated} regions, missed {missed}.");
    }

    static string StripPrefixAndHemisphere(string regionId, out string hemisphereTag)
    {
        hemisphereTag = "";
        if (string.IsNullOrEmpty(regionId)) return "";

        string id = regionId;
        if (id.StartsWith("Allen_")) id = id.Substring("Allen_".Length);
        else if (id.StartsWith("VH_M_")) id = id.Substring("VH_M_".Length);

        if (id.EndsWith("_L")) { hemisphereTag = "left"; id = id.Substring(0, id.Length - 2); }
        else if (id.EndsWith("_R")) { hemisphereTag = "right"; id = id.Substring(0, id.Length - 2); }
        return id;
    }

    static string ApplyHemisphere(string sentence, string hemisphereTag)
    {
        if (string.IsNullOrEmpty(hemisphereTag)) return sentence;
        return sentence.Replace("{HEMI}", hemisphereTag);
    }

    static Dictionary<string, string> BuildLookup()
    {
        // Keys are stripped IDs (no Allen_/VH_M_ prefix, no _L/_R suffix).
        // {HEMI} is replaced with "left" or "right" at write time.
        return new Dictionary<string, string>
        {
            // Cerebrum surface (lobes, gyri, opercula)
            { "superior_frontal_gyrus", "Surface gyrus of the {HEMI} frontal lobe; contributes to motor planning, working memory, and executive control." },
            { "middle_frontal_gyrus", "Lateral {HEMI} frontal gyrus involved in attention, working memory, and goal-directed cognition." },
            { "inferior_frontal_gyrus_opercular_part", "Pars opercularis of the {HEMI} inferior frontal gyrus; in the dominant hemisphere it forms part of Broca's area for speech production." },
            { "inferior_frontal_gyrus_triangular_part", "Pars triangularis of the {HEMI} inferior frontal gyrus; works with the opercular part to support language production and inhibitory control." },
            { "precentral_gyrus", "Primary motor cortex of the {HEMI} hemisphere; sends commands to muscles on the opposite side of the body." },
            { "postcentral_gyrus", "Primary somatosensory cortex of the {HEMI} hemisphere; receives touch, pressure, temperature, and proprioception from the opposite side of the body." },
            { "frontal_pole", "Most anterior tip of the {HEMI} frontal lobe; involved in high-level reasoning and integrating future-oriented goals." },
            { "frontomarginal_gyrus", "Small {HEMI} prefrontal gyrus along the frontal pole; contributes to anterior prefrontal cognitive functions." },
            { "frontal_operculum", "Cortex of the {HEMI} frontal lobe overhanging the insula; supports speech, taste, and motor control of the face and mouth." },
            { "paracentral_lobule_rostral_part", "Anterior part of the {HEMI} paracentral lobule on the medial surface; motor control for the lower limb and pelvic floor." },
            { "paracentral_lobule_caudal_part", "Posterior part of the {HEMI} paracentral lobule on the medial surface; somatosensory input from the lower limb and pelvic floor." },
            { "gyrus_rectus_straight_gyrus", "Medial-most {HEMI} orbitofrontal gyrus; part of the orbitofrontal cortex involved in emotion regulation and reward processing." },
            { "rostral_gyrus", "Small rostral {HEMI} orbitofrontal gyrus; contributes to orbitofrontal evaluation of stimuli and reward." },
            { "anterior_intermediate_orbital_gyrus", "Anterior orbitofrontal gyrus of the {HEMI} hemisphere; supports reward valuation and flexible decision-making." },
            { "posterior_intermediate_orbital_gyrus", "Posterior orbitofrontal gyrus of the {HEMI} hemisphere; integrates sensory and emotional input for decision-making." },
            { "lateral_orbital_gyrus", "Lateral orbitofrontal gyrus of the {HEMI} hemisphere; supports reward evaluation, emotion regulation, and flexible decision-making." },
            { "medial_orbital_gyrus", "Medial orbitofrontal gyrus of the {HEMI} hemisphere; involved in value-based decision-making and emotion." },
            { "supramarginal_gyrus", "Inferior parietal gyrus of the {HEMI} hemisphere; involved in language, phonological processing, and tool use." },
            { "angular_gyrus", "{HEMI} inferior parietal gyrus; integrates language, mathematics, and spatial cognition." },
            { "precuneus", "Medial {HEMI} parietal cortex; involved in self-referential thought, episodic memory, and spatial imagery." },
            { "supraparietal_lobule", "Superior parietal lobule of the {HEMI} hemisphere; supports spatial attention and visuomotor coordination." },
            { "parietal_operculum", "{HEMI} parietal cortex overhanging the insula; processes secondary somatosensory and pain information." },
            { "superior_temporal_gyrus", "{HEMI} superior temporal gyrus; primary auditory cortex sits here; in the dominant hemisphere it includes Wernicke's area for language comprehension." },
            { "middle_temporal_gyrus", "Middle gyrus of the {HEMI} temporal lobe; involved in language semantics and recognition of meaningful objects and motion." },
            { "inferior_temporal_gyrus", "Inferior gyrus of the {HEMI} temporal lobe; ventral visual stream region for object and face recognition." },
            { "temporal_pole", "Most anterior tip of the {HEMI} temporal lobe; integrates emotional and social meaning with sensory input." },
            { "transverse_temporal_gyrus_Heschls_gyrus", "Heschl's gyrus of the {HEMI} hemisphere; primary auditory cortex that processes basic sound features." },
            { "planum_temporale", "Cortex behind Heschl's gyrus on the {HEMI} side; involved in language and complex sound processing." },
            { "planum_polare", "Cortex in front of Heschl's gyrus on the {HEMI} side; supports auditory processing including music and speech." },
            { "occipitotemporal_fusiform_gyrus_temporal_part", "Temporal portion of the {HEMI} fusiform gyrus; ventral visual stream involved in face and object recognition." },
            { "lateral_occipitotemporal_fusiform_gyrus_occipital_part", "Occipital portion of the {HEMI} lateral occipitotemporal/fusiform gyrus; processes high-level visual features." },
            { "perirhinal_gyrus_rostral_part_of_FuGt", "Rostral {HEMI} perirhinal cortex on the temporal lobe; supports recognition memory for objects." },
            { "cuneus", "Medial {HEMI} occipital lobe gyrus; processes visual input from the lower visual field." },
            { "lingual_gyrus_medial_occipitotemporal_gyrus", "Medial {HEMI} occipitotemporal gyrus; involved in visual processing including word forms and faces." },
            { "occipital_pole", "Most posterior tip of the {HEMI} occipital lobe; central representation of the primary visual cortex." },
            { "superior_occipital_gyrus", "Superior gyrus of the {HEMI} occipital lobe; involved in visual processing and dorsal-stream spatial vision." },
            { "inferior_occipital_gyrus", "Inferior gyrus of the {HEMI} occipital lobe; involved in object recognition along the ventral visual stream." },

            // Cingulate / limbic
            { "cingulate_gyrus_rostral_anterior_part", "Anterior cingulate cortex of the {HEMI} hemisphere; supports attention, conflict monitoring, and emotion regulation." },
            { "cingulate_gyrus_caudal_posterior_part", "Posterior cingulate cortex of the {HEMI} hemisphere; central node of the default mode network and self-referential thought." },
            { "paracingulate_gyrus", "{HEMI} paracingulate gyrus running parallel to the cingulate; involved in cognitive control and social cognition." },
            { "subcallosal_gyrus_parolfactory_gyrus", "{HEMI} subcallosal/parolfactory gyrus below the rostrum of the corpus callosum; involved in mood regulation and autonomic control." },
            { "anterior_parahippocampal_gyrus", "Anterior part of the {HEMI} parahippocampal gyrus; gateway to the hippocampus for memory and contextual processing." },
            { "posterior_parahippocampal_gyrus", "Posterior part of the {HEMI} parahippocampal gyrus; supports scene recognition and spatial memory." },
            { "ingulo_parahippocampal_isthmus", "Narrow {HEMI} isthmus connecting the cingulate gyrus to the parahippocampal gyrus; relays limbic information." },
            { "gyrus_ambiens", "Small {HEMI} gyrus near the uncus; part of the olfactory and limbic cortex." },

            // Insula and olfactory
            { "short_insular_gyri", "Anterior {HEMI} insular gyri; involved in interoception, taste, and emotion." },
            { "long_insular_gyri", "Posterior {HEMI} insular gyri; involved in interoception, pain, and somatosensory integration." },
            { "limen_insula", "Threshold region of the {HEMI} insula at its anterior boundary; part of olfactory and limbic networks." },
            { "frontal_agranular_insular_cortex_area_FI", "Frontal agranular {HEMI} insular cortex; processes interoceptive and emotional signals." },
            { "temporal_agranular_insular_cortex_area_TI", "Temporal agranular {HEMI} insular cortex; involved in olfaction and emotional processing." },
            { "anterior_olfactory_nucleus", "{HEMI} anterior olfactory nucleus; relays olfactory information from the bulb to higher cortical areas." },
            { "olfactory_bulb", "{HEMI} olfactory bulb; first relay for smell signals from the nasal epithelium." },
            { "olfactory_tract", "{HEMI} olfactory tract carrying signals from the olfactory bulb to the olfactory cortex." },
            { "lateral_olfactory_gyrus", "Lateral olfactory gyrus on the {HEMI} side; part of the primary olfactory cortex." },
            { "piriform_region", "{HEMI} piriform cortex; primary olfactory cortex involved in identifying smells." },

            // Visual tracts
            { "optic_tract", "{HEMI} optic tract carrying visual information from the optic chiasm to the lateral geniculate nucleus." },
            { "optic_radiation", "{HEMI} optic radiation projecting visual signals from the thalamus to the primary visual cortex." },

            // Hemisphere wrapper / catch-all
            { "brain_Hemisphere", "Whole {HEMI} cerebral hemisphere wrapper; container for cortical and subcortical structures on this side." },

            // Basal ganglia
            { "putamen", "{HEMI} putamen; outer shell of the lentiform nucleus; supports motor planning and procedural learning." },
            { "posteroventral_putamen", "Posterior-ventral region of the {HEMI} putamen; participates in motor and sensorimotor circuits." },
            { "external_segment_of_globus_pallidus", "External {HEMI} globus pallidus (GPe); part of the indirect basal ganglia pathway regulating movement." },
            { "internal_segment_of_globus_pallidus", "Internal {HEMI} globus pallidus (GPi); main basal ganglia output controlling movement." },
            { "head_of_caudate", "{HEMI} caudate head; participates in cognition, learning, and goal-directed behavior." },
            { "body_of_caudate", "{HEMI} caudate body; continues the caudate's role in motor and cognitive control." },
            { "tail_of_caudate", "{HEMI} caudate tail wrapping toward the temporal lobe; involved in associative learning." },
            { "nucleus_accumbens", "{HEMI} nucleus accumbens; part of the ventral striatum central to reward and motivation." },
            { "subthalamic_nucleus", "{HEMI} subthalamic nucleus; part of the indirect basal ganglia pathway and a target for Parkinson's disease therapy." },
            { "substantia_nigra", "{HEMI} substantia nigra; midbrain nucleus whose dopamine neurons modulate basal ganglia function." },
            { "claustrum", "Thin {HEMI} sheet of grey matter beside the insula; thought to coordinate widespread cortical activity." },

            // Thalamus and related
            { "thalamus", "{HEMI} thalamus; major relay for sensory and motor information traveling to the cerebral cortex." },
            { "anterior_nuclear_complex_of_thalamus", "Anterior nuclei of the {HEMI} thalamus; part of the limbic Papez circuit involved in memory." },
            { "centromedian_nucleus_of_thalamus", "Centromedian intralaminar nucleus of the {HEMI} thalamus; modulates arousal and basal ganglia output." },
            { "dorsal_lateral_geniculate_nucleus", "{HEMI} lateral geniculate nucleus; thalamic relay carrying visual information to primary visual cortex." },
            { "habenular_nuclei", "{HEMI} habenular nuclei in the epithalamus; influence dopamine and serotonin systems and reward processing." },
            { "lateral_dorsal_nucleus_of_thalamus", "Lateral dorsal nucleus of the {HEMI} thalamus; connects with limbic cortex for memory and emotion." },
            { "lateral_posterior_nucleus_of_thalamus", "Lateral posterior nucleus of the {HEMI} thalamus; involved in spatial attention and visual integration." },
            { "medial_geniculate_nuclei", "{HEMI} medial geniculate nucleus; thalamic relay for auditory information to the auditory cortex." },
            { "mediodorsal_nucleus_of_thalamus", "Mediodorsal nucleus of the {HEMI} thalamus; major thalamic partner of the prefrontal cortex for memory and cognition." },
            { "midline_nuclear_complex", "Midline {HEMI} thalamic nuclei; modulate arousal, emotion, and limbic activity." },
            { "parafascicular_nucleus_of_thalamus", "Parafascicular intralaminar nucleus of the {HEMI} thalamus; modulates striatal activity and arousal." },
            { "pulvinar_of_thalamus", "{HEMI} pulvinar; large thalamic nucleus involved in visual attention and integration." },
            { "reuniens_nucleus_medioventral_nucleus_of_thalamus", "Reuniens nucleus of the {HEMI} thalamus; coordinates communication between hippocampus and prefrontal cortex." },
            { "ventral_anterior_nucleus_of_thalamus", "Ventral anterior thalamic nucleus on the {HEMI} side; relays basal ganglia signals to motor cortex." },
            { "ventral_lateral_nucleus_of_thalamus", "Ventral lateral thalamic nucleus on the {HEMI} side; relays cerebellar input to motor cortex." },
            { "ventral_posterior_lateral_nucleus", "Ventral posterolateral thalamic nucleus on the {HEMI} side; relays body somatosensation to the cortex." },
            { "ventral_posterior_medial_nucleus", "Ventral posteromedial thalamic nucleus on the {HEMI} side; relays facial somatosensation and taste to the cortex." },

            // Amygdala / hippocampus
            { "amygdaloid_complex", "{HEMI} amygdala; processes emotion, especially fear and salience, and shapes emotional memory." },
            { "amygdalohippocampal_area", "Transition zone between {HEMI} amygdala and hippocampus; integrates emotion and memory." },
            { "anterior_amygdaloid_area", "Anterior {HEMI} amygdaloid area; bridges olfactory input with emotional processing." },
            { "anterior_cortical_nucleus", "Anterior cortical nucleus of the {HEMI} amygdala; receives olfactory input and contributes to emotion." },
            { "basolateral_nucleus_basal_nucleus", "Basolateral nucleus of the {HEMI} amygdala; central to fear learning and emotional memory." },
            { "basomedial_nucleus_accessory_basal_nucleus", "Basomedial (accessory basal) nucleus of the {HEMI} amygdala; integrates emotional and contextual information." },
            { "central_nuclear_group", "Central nuclear group of the {HEMI} amygdala; drives autonomic and behavioral responses to threat." },
            { "lateral_nucleus", "Lateral nucleus of the {HEMI} amygdala; primary input region for sensory information during fear learning." },
            { "medial_nucleus", "Medial nucleus of the {HEMI} amygdala; processes social and pheromonal cues." },
            { "posterior_cortical_nucleus", "Posterior cortical nucleus of the {HEMI} amygdala; involved in olfactory-emotional processing." },
            { "head_of_hippocampus", "Anterior head of the {HEMI} hippocampus; supports emotional and novelty-related memory." },
            { "body_of_hippocampus", "Body of the {HEMI} hippocampus; central region for spatial and episodic memory formation." },
            { "tail_of_hippocampus", "Posterior tail of the {HEMI} hippocampus; contributes to spatial memory and scene recognition." },

            // Hypothalamus regions
            { "hypothalamus", "{HEMI} hypothalamus; coordinates autonomic, endocrine, and homeostatic functions like temperature and hunger." },
            { "mammillary_region_of_HTH", "Mammillary region of the {HEMI} hypothalamus; part of the limbic memory circuit (Papez circuit)." },
            { "preoptic_region_of_HTH", "Preoptic region of the {HEMI} hypothalamus; regulates body temperature, sleep, and reproduction." },
            { "supraoptic_region_of_HTH", "Supraoptic region of the {HEMI} hypothalamus; produces hormones for fluid balance and lactation." },
            { "tuberal_region_of_HTH", "Tuberal region of the {HEMI} hypothalamus; controls pituitary hormone release and feeding behaviors." },

            // Basal forebrain & related
            { "basal_forebrain", "{HEMI} basal forebrain; major source of cholinergic input to the cortex, important for arousal and memory." },
            { "bed_nucleus_of_stria_terminalis", "{HEMI} bed nucleus of the stria terminalis; extended amygdala region involved in stress and anxiety." },
            { "septal_nuclei", "{HEMI} septal nuclei; modulate hippocampal activity and contribute to reward and emotion." },
            { "zona_incerta", "{HEMI} zona incerta below the thalamus; integrates motor, sensory, and limbic signals." },
            { "fornix", "{HEMI} fornix; main output tract of the hippocampus carrying memory-related signals to the mammillary bodies." },
            { "corpus_callosum", "{HEMI} corpus callosum; major commissural tract connecting the two cerebral hemispheres." },
            { "anterior_commissure", "{HEMI} anterior commissure; smaller commissure connecting parts of the temporal lobes and olfactory regions." },
            { "white_matter_of_forebrain", "{HEMI} forebrain white matter; bundles of myelinated axons connecting cortical and subcortical regions." },

            // Midbrain
            { "red_nucleus", "{HEMI} red nucleus in the midbrain; involved in motor coordination and cerebellar pathways." },
            { "pretectal_region", "{HEMI} pretectal region in the midbrain; controls pupillary light reflexes and eye movements." },
            { "pineal_body", "{HEMI} pineal body; produces melatonin to regulate circadian and seasonal rhythms." },
            { "midbrain_tegmentum", "{HEMI} midbrain tegmentum; contains motor, autonomic, and arousal nuclei." },
            { "superior_colliculus", "{HEMI} superior colliculus; coordinates eye movements and orienting responses to visual stimuli." },
            { "inferior_colliculus", "{HEMI} inferior colliculus; key auditory midbrain relay between brainstem and thalamus." },
            { "cerebral_peduncle_crus_cerebri", "{HEMI} cerebral peduncle (crus cerebri); carries motor fibers from cortex to brainstem and spinal cord." },

            // Pons / medulla
            { "basilar_part_of_pons", "{HEMI} basilar pons; relays cortical motor signals to the cerebellum via pontine nuclei." },
            { "pontine_tegmentum", "{HEMI} pontine tegmentum; contains nuclei for arousal, eye movement, and autonomic control." },
            { "tegmentum_of_medulla_oblongata", "{HEMI} medullary tegmentum; contains autonomic, sensory, and reticular nuclei." },
            { "pyramidal_part_of_medulla_oblongata", "{HEMI} medullary pyramid; carries the corticospinal tract to the spinal cord." },
            { "inferior_olive", "{HEMI} inferior olivary nucleus in the medulla; provides climbing-fiber input to the cerebellum for motor learning." },

            // Cerebellum
            { "lateral_hemisphere_of_cerebellum", "{HEMI} lateral cerebellar hemisphere; involved in motor planning and cognitive timing." },
            { "paravermis_of_cerebellum", "{HEMI} cerebellar paravermis; coordinates ongoing limb movement with cortical commands." },
            { "cerebellar_vermis", "{HEMI} cerebellar vermis; midline region controlling posture, gait, and trunk movements." },
            { "cerebellar_deep_nuclei", "{HEMI} deep cerebellar nuclei; main output of the cerebellum to motor and premotor systems." },
            { "superior_cerebellar_peduncle_brachium_conjunctivum", "{HEMI} superior cerebellar peduncle; main cerebellar output bundle to the thalamus and red nucleus." },
            { "middle_cerebellar_peduncle", "{HEMI} middle cerebellar peduncle; carries pontine input into the cerebellum." },
            { "inferior_cerebellar_peduncle", "{HEMI} inferior cerebellar peduncle; carries spinocerebellar and vestibular input into the cerebellum." },
            { "white_matter_of_hindbrain", "{HEMI} hindbrain white matter; tracts running through pons, medulla, and cerebellum." },

            // Tracts
            { "mammillothalamic_tract", "{HEMI} mammillothalamic tract; connects mammillary bodies to anterior thalamic nuclei in the limbic memory circuit." },

            // Ventricles / CSF spaces
            { "anterior_horn_of_lateral_ventricle", "{HEMI} anterior (frontal) horn of the lateral ventricle; CSF-filled cavity within the frontal lobe." },
            { "body_of_lateral_ventricle", "{HEMI} body of the lateral ventricle; central CSF cavity beneath the corpus callosum." },
            { "atrium_of_lateral_ventricle", "{HEMI} atrium (trigone) of the lateral ventricle; meeting point of body, posterior, and inferior horns." },
            { "posterior_horn_of_lateral_ventricle", "{HEMI} posterior (occipital) horn of the lateral ventricle; CSF cavity extending into the occipital lobe." },
            { "inferior_horn_of_lateral_ventricle", "{HEMI} inferior (temporal) horn of the lateral ventricle; CSF cavity extending into the temporal lobe." },
            { "third_ventricle", "Midline third ventricle (shown on the {HEMI} side); CSF-filled cavity between the two thalami." },
            { "cerebral_aqueduct", "Cerebral aqueduct in the midbrain (shown on the {HEMI} side); links the third and fourth ventricles." },
            { "fourth_ventricle", "Fourth ventricle between brainstem and cerebellum (shown on the {HEMI} side); CSF cavity continuous with the central canal." },
            { "central_canal_of_medulla_oblongata", "Central canal of the medulla on the {HEMI} side; continuation of CSF space into the spinal cord." },

            // Midline (no hemisphere)
            { "optic_chiasm", "Midline optic chiasm where fibers from the two optic nerves partially cross before entering the optic tracts." },
        };
    }
}
