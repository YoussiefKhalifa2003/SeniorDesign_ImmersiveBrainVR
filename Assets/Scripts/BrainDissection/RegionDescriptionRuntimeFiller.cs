using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime safety net: fills every BrainRegion's RegionData with a curated,
/// undergraduate-friendly description (function + clinical relevance) so the
/// region info panel always reads like the Angular Gyrus reference layout:
///   - Title (displayName)
///   - Subtitle "Brain region: <displayName>" (built by RegionUIController)
///   - Body paragraph (this field — multi-sentence, includes risks if damaged)
///
/// We write into <see cref="RegionData.detailedDescription"/> so the body shows
/// up in the wider/longer panel slot. The short subtitle is generated from the
/// display name in RegionUIController, so we keep <c>shortDescription</c> empty.
/// </summary>
public class RegionDescriptionRuntimeFiller : MonoBehaviour
{
    static bool _applied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyOnce()
    {
        if (_applied) return;
        _applied = true;

        var lookup = BuildLookup();
        var regions = FindObjectsByType<BrainRegion>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int patched = 0;

        foreach (var region in regions)
        {
            if (region == null || region.regionData == null) continue;
            var data = region.regionData;

            string baseKey = StripPrefixAndHemisphere(data.regionId, out string hemisphereTag);
            if (string.IsNullOrEmpty(baseKey)) continue;
            if (!lookup.TryGetValue(baseKey, out string body)) continue;

            string filled = body.Replace("{HEMI}", hemisphereTag);

            // Decide whether to overwrite the body paragraph. We replace any
            // empty value or any leftover placeholder ("Add a detailed
            // description for this region..."). We do NOT clobber long custom
            // text that already looks curated.
            bool placeholder = string.IsNullOrWhiteSpace(data.detailedDescription) ||
                               data.detailedDescription.StartsWith("Add a detailed description");
            if (placeholder)
            {
                data.detailedDescription = filled;
                patched++;
            }

            // Always wipe the legacy "Brain region: ..." text in shortDescription.
            // The panel rebuilds the subtitle from displayName.
            if (!string.IsNullOrEmpty(data.shortDescription) &&
                data.shortDescription.StartsWith("Brain region:"))
            {
                data.shortDescription = "";
            }
        }

        if (patched > 0)
            Debug.Log($"[RegionDescriptionRuntimeFiller] Patched {patched} region descriptions at runtime.");
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

    static Dictionary<string, string> BuildLookup()
    {
        // {HEMI} => "left"|"right" at write time. Each entry is a 2-3 sentence
        // student-facing paragraph: location + role + clinical relevance.
        return new Dictionary<string, string>
        {
            // ===== Cerebrum surface (gyri / opercula) =====
            { "superior_frontal_gyrus",
                "The superior frontal gyrus runs along the upper {HEMI} frontal lobe and contributes to motor planning, working memory, and self-awareness. " +
                "Damage can disrupt initiation of voluntary movement, planning, and may produce apathy or dysexecutive symptoms." },
            { "middle_frontal_gyrus",
                "The middle frontal gyrus on the {HEMI} hemisphere supports attention, working memory, and goal-directed cognition. " +
                "Lesions can impair sustained attention, planning multi-step tasks, and flexible thinking." },
            { "inferior_frontal_gyrus_opercular_part",
                "The pars opercularis sits in the {HEMI} inferior frontal gyrus and, in the dominant hemisphere, forms part of Broca's area for speech production. " +
                "Damage can cause expressive (Broca's) aphasia where speech is effortful and non-fluent but comprehension is preserved." },
            { "inferior_frontal_gyrus_triangular_part",
                "The pars triangularis lies in the {HEMI} inferior frontal gyrus and works with the opercular part for language production and inhibitory control. " +
                "Lesions in the dominant hemisphere can contribute to Broca's aphasia and difficulty inhibiting inappropriate responses." },
            { "precentral_gyrus",
                "The precentral gyrus on the {HEMI} side is the primary motor cortex. It sends commands down the corticospinal tract to muscles on the opposite side of the body. " +
                "Damage causes contralateral weakness or paralysis (hemiparesis or hemiplegia)." },
            { "postcentral_gyrus",
                "The postcentral gyrus on the {HEMI} side is the primary somatosensory cortex, receiving touch, pressure, temperature, and proprioception from the opposite side of the body. " +
                "Damage produces contralateral loss of sensation and impaired stereognosis." },
            { "frontal_pole",
                "The frontal pole is the most anterior tip of the {HEMI} frontal lobe and supports high-level reasoning, planning, and integration of long-term goals. " +
                "Damage can blunt insight, foresight, and complex decision-making, often without affecting basic motor or language abilities." },
            { "frontomarginal_gyrus",
                "The frontomarginal gyrus is a small {HEMI} prefrontal gyrus along the frontal pole that contributes to anterior prefrontal cognitive functions. " +
                "Lesions are usually subtle but may add to broader prefrontal deficits in planning and abstract thought." },
            { "frontal_operculum",
                "The frontal operculum on the {HEMI} side covers the insula and supports speech, taste, and motor control of the face and mouth. " +
                "Damage can impair articulation, swallowing coordination, and contribute to aphasia in the dominant hemisphere." },
            { "paracentral_lobule_rostral_part",
                "The rostral paracentral lobule on the medial {HEMI} hemisphere provides motor control for the lower limb and pelvic floor. " +
                "Bilateral damage can cause weakness of the legs and loss of voluntary bladder control." },
            { "paracentral_lobule_caudal_part",
                "The caudal paracentral lobule on the medial {HEMI} hemisphere receives somatosensory input from the lower limb and pelvic floor. " +
                "Damage can cause loss of sensation in the leg and impaired awareness of bladder fullness." },
            { "gyrus_rectus_straight_gyrus",
                "The gyrus rectus is the medial-most {HEMI} orbitofrontal gyrus and contributes to emotion regulation, smell, and reward processing. " +
                "Lesions can impair social behavior, emotional control, and olfaction." },
            { "rostral_gyrus",
                "The rostral gyrus is a small {HEMI} orbitofrontal region that contributes to evaluation of stimuli and reward. " +
                "Damage may subtly affect decision-making and emotional appraisal." },
            { "anterior_intermediate_orbital_gyrus",
                "The anterior intermediate orbital gyrus on the {HEMI} hemisphere supports reward valuation and flexible decision-making. " +
                "Orbitofrontal damage can produce poor judgment, impulsivity, and inappropriate social behavior." },
            { "posterior_intermediate_orbital_gyrus",
                "The posterior intermediate orbital gyrus on the {HEMI} hemisphere integrates sensory and emotional input for decision-making. " +
                "Lesions can impair the ability to update choices when rewards or punishments change." },
            { "lateral_orbital_gyrus",
                "The lateral orbital gyrus on the {HEMI} hemisphere is part of the orbitofrontal cortex and supports reward evaluation, emotion regulation, and flexible decision-making. " +
                "Damage to orbitofrontal regions is linked to disinhibition, impulsivity, and impaired social judgment." },
            { "medial_orbital_gyrus",
                "The medial orbital gyrus on the {HEMI} hemisphere is involved in value-based decision-making and emotion. " +
                "Lesions can affect mood regulation and the evaluation of reward outcomes." },
            { "supramarginal_gyrus",
                "The supramarginal gyrus on the {HEMI} side is part of the inferior parietal lobule and supports language, phonological processing, and tool use. " +
                "Damage in the dominant hemisphere can contribute to conduction aphasia and difficulty repeating words." },
            { "angular_gyrus",
                "The angular gyrus lies in the {HEMI} inferior parietal lobule and integrates language, mathematics, memory retrieval, and spatial cognition. " +
                "Damage in the dominant hemisphere can produce Gerstmann's syndrome (agraphia, acalculia, finger agnosia, left-right confusion) and word-finding difficulty." },
            { "precuneus",
                "The precuneus is a medial {HEMI} parietal area involved in self-referential thought, episodic memory retrieval, and visuospatial imagery. " +
                "Damage can disrupt autobiographical memory and the sense of self." },
            { "supraparietal_lobule",
                "The superior parietal lobule on the {HEMI} side supports spatial attention, visuomotor coordination, and mental rotation. " +
                "Lesions can impair reaching toward objects and produce contralateral neglect when right-sided." },
            { "parietal_operculum",
                "The {HEMI} parietal operculum overhangs the insula and processes secondary somatosensory and pain information. " +
                "Damage can produce abnormal pain perception and altered touch awareness." },
            { "superior_temporal_gyrus",
                "The superior temporal gyrus on the {HEMI} side contains primary auditory cortex and, in the dominant hemisphere, Wernicke's area for language comprehension. " +
                "Damage can cause receptive (Wernicke's) aphasia, where speech is fluent but meaningless, and comprehension is impaired." },
            { "middle_temporal_gyrus",
                "The middle temporal gyrus on the {HEMI} side is involved in language semantics and recognition of meaningful objects, motion, and faces. " +
                "Lesions can produce semantic memory loss and impaired motion perception." },
            { "inferior_temporal_gyrus",
                "The inferior temporal gyrus on the {HEMI} side is part of the ventral visual stream for object and face recognition. " +
                "Damage can cause visual agnosia (difficulty recognizing objects) and contribute to prosopagnosia (face blindness)." },
            { "temporal_pole",
                "The temporal pole on the {HEMI} side integrates emotional and social meaning with sensory input. " +
                "Damage is associated with semantic dementia, with progressive loss of word meaning and personal-event memory." },
            { "transverse_temporal_gyrus_Heschls_gyrus",
                "Heschl's gyrus on the {HEMI} side is the primary auditory cortex and processes basic features of sound such as frequency and timing. " +
                "Bilateral damage can produce cortical deafness, while unilateral damage usually causes only subtle auditory deficits." },
            { "planum_temporale",
                "The planum temporale lies behind Heschl's gyrus on the {HEMI} side and contributes to language and complex sound processing. " +
                "Asymmetry of this region is linked to language lateralization, and damage can affect speech perception." },
            { "planum_polare",
                "The planum polare lies in front of Heschl's gyrus on the {HEMI} side and supports auditory processing including music and speech. " +
                "Lesions can disrupt fine perception of complex sounds and music." },
            { "occipitotemporal_fusiform_gyrus_temporal_part",
                "The temporal part of the {HEMI} fusiform gyrus is part of the ventral visual stream for face and object recognition. " +
                "Damage can cause prosopagnosia and difficulty recognizing familiar visual categories." },
            { "lateral_occipitotemporal_fusiform_gyrus_occipital_part",
                "The occipital part of the {HEMI} lateral occipitotemporal/fusiform gyrus processes high-level visual features. " +
                "Lesions can impair recognition of words, faces, and objects depending on their exact location." },
            { "perirhinal_gyrus_rostral_part_of_FuGt",
                "The rostral perirhinal cortex on the {HEMI} temporal lobe supports recognition memory for objects. " +
                "Damage can produce impaired familiarity judgments and contribute to early memory loss in Alzheimer's disease." },
            { "cuneus",
                "The cuneus is a medial {HEMI} occipital gyrus that processes visual input from the lower visual field. " +
                "Damage can produce a contralateral lower visual field defect." },
            { "lingual_gyrus_medial_occipitotemporal_gyrus",
                "The lingual gyrus on the medial {HEMI} occipitotemporal surface is involved in visual processing of word forms and faces. " +
                "Damage can cause pure alexia (difficulty reading despite preserved writing) and color recognition deficits." },
            { "occipital_pole",
                "The occipital pole is the most posterior tip of the {HEMI} occipital lobe and contains the central representation of primary visual cortex. " +
                "Damage causes a contralateral homonymous hemianopia, often with macular sparing." },
            { "superior_occipital_gyrus",
                "The superior occipital gyrus on the {HEMI} side is part of the dorsal visual stream for spatial vision and motion. " +
                "Lesions can impair spatial localization and visually guided reaching." },
            { "inferior_occipital_gyrus",
                "The inferior occipital gyrus on the {HEMI} side contributes to object recognition along the ventral visual stream. " +
                "Damage can produce visual object agnosia and contribute to prosopagnosia." },

            // ===== Cingulate / limbic =====
            { "cingulate_gyrus_rostral_anterior_part",
                "The anterior cingulate cortex on the {HEMI} side supports attention, conflict monitoring, motivation, and emotion regulation. " +
                "Damage can cause apathy, akinetic mutism, and reduced ability to detect errors or conflicts." },
            { "cingulate_gyrus_caudal_posterior_part",
                "The posterior cingulate cortex on the {HEMI} side is a central node of the default mode network and supports self-referential thought and memory retrieval. " +
                "Damage can disrupt autobiographical memory and contribute to Alzheimer's-related changes." },
            { "paracingulate_gyrus",
                "The {HEMI} paracingulate gyrus runs parallel to the cingulate and contributes to cognitive control and social cognition. " +
                "Variability in this gyrus has been linked to differences in error monitoring and theory-of-mind ability." },
            { "subcallosal_gyrus_parolfactory_gyrus",
                "The subcallosal/parolfactory gyrus on the {HEMI} side sits below the rostrum of the corpus callosum and is involved in mood regulation and autonomic control. " +
                "This region is implicated in major depressive disorder and is a target for some neuromodulation therapies." },
            { "anterior_parahippocampal_gyrus",
                "The anterior parahippocampal gyrus on the {HEMI} side acts as a gateway to the hippocampus for memory and contextual processing. " +
                "Damage impairs the formation of new declarative memories and recognition of familiar contexts." },
            { "posterior_parahippocampal_gyrus",
                "The posterior parahippocampal gyrus on the {HEMI} side supports scene recognition and spatial memory. " +
                "Damage can cause topographical disorientation and difficulty recognizing familiar places." },
            { "ingulo_parahippocampal_isthmus",
                "The {HEMI} cinguloparahippocampal isthmus is a narrow strip linking the cingulate and parahippocampal gyri that relays limbic information. " +
                "Lesions can interrupt limbic connectivity and contribute to memory and emotional disturbances." },
            { "gyrus_ambiens",
                "The gyrus ambiens is a small {HEMI} gyrus near the uncus and is part of olfactory and limbic cortex. " +
                "Damage can subtly affect smell identification and emotional processing." },

            // ===== Insula and olfactory =====
            { "short_insular_gyri",
                "The anterior {HEMI} insula contains the short insular gyri, which support interoception, taste, and emotion. " +
                "Damage can disrupt awareness of internal body states and emotional decision-making." },
            { "long_insular_gyri",
                "The posterior {HEMI} insula contains the long insular gyri, which process interoception, pain, and somatosensory integration. " +
                "Lesions can produce abnormal pain perception and altered awareness of bodily signals." },
            { "limen_insula",
                "The limen insula is the threshold between {HEMI} insular and frontal/temporal cortex; it participates in olfactory and limbic networks. " +
                "Damage can subtly affect smell and emotional processing." },
            { "frontal_agranular_insular_cortex_area_FI",
                "The frontal agranular insular cortex on the {HEMI} side processes interoceptive and emotional signals at the front of the insula. " +
                "Lesions can impair awareness of bodily states and emotional regulation." },
            { "temporal_agranular_insular_cortex_area_TI",
                "The temporal agranular insular cortex on the {HEMI} side is involved in olfaction and emotional processing. " +
                "Damage can produce subtle smell and emotional appraisal deficits." },
            { "anterior_olfactory_nucleus",
                "The {HEMI} anterior olfactory nucleus relays olfactory information from the bulb to higher cortical areas. " +
                "Damage contributes to anosmia (loss of smell) and reduced odor memory." },
            { "olfactory_bulb",
                "The {HEMI} olfactory bulb is the first relay for smell signals from the nasal epithelium. " +
                "Damage causes anosmia on that side; bilateral damage produces complete loss of smell, which can also affect taste." },
            { "olfactory_tract",
                "The {HEMI} olfactory tract carries signals from the olfactory bulb to the olfactory cortex. " +
                "Lesions, including from olfactory groove meningiomas, can cause progressive anosmia." },
            { "lateral_olfactory_gyrus",
                "The lateral olfactory gyrus on the {HEMI} side is part of the primary olfactory cortex. " +
                "Damage impairs identification and discrimination of odors." },
            { "piriform_region",
                "The {HEMI} piriform cortex is the primary olfactory cortex and is essential for identifying smells. " +
                "It is highly susceptible to seizure activity in temporal lobe epilepsy and damage causes anosmia and odor identification deficits." },

            // ===== Visual tracts =====
            { "optic_tract",
                "The {HEMI} optic tract carries visual information from the optic chiasm to the lateral geniculate nucleus of the thalamus. " +
                "Damage produces a contralateral homonymous hemianopia, since the tract carries fibers from both retinas." },
            { "optic_radiation",
                "The {HEMI} optic radiation projects visual signals from the lateral geniculate nucleus to the primary visual cortex. " +
                "Damage causes contralateral visual field defects, including quadrantanopia depending on which fibers are affected." },

            // ===== Hemisphere wrapper =====
            { "brain_Hemisphere",
                "The {HEMI} cerebral hemisphere contains the cortex and underlying subcortical structures of one side of the brain. " +
                "Large unilateral damage can cause hemiplegia, hemisensory loss, and lateralized cognitive deficits like aphasia or neglect." },

            // ===== Basal ganglia =====
            { "putamen",
                "The {HEMI} putamen is the outer shell of the lentiform nucleus and supports motor planning, learning, and habit formation. " +
                "Damage contributes to movement disorders and is affected in Parkinson's and Huntington's diseases." },
            { "posteroventral_putamen",
                "The posteroventral region of the {HEMI} putamen participates in motor and sensorimotor circuits. " +
                "Lesions contribute to abnormalities of voluntary movement and motor learning." },
            { "external_segment_of_globus_pallidus",
                "The external globus pallidus (GPe) on the {HEMI} side is part of the indirect basal ganglia pathway that suppresses unwanted movement. " +
                "Dysfunction is implicated in Parkinson's disease and dystonias." },
            { "internal_segment_of_globus_pallidus",
                "The internal globus pallidus (GPi) on the {HEMI} side is the main basal ganglia output controlling movement via the thalamus. " +
                "It is a common target for deep brain stimulation in Parkinson's disease and dystonia." },
            { "head_of_caudate",
                "The head of the {HEMI} caudate participates in cognition, learning, and goal-directed behavior. " +
                "Atrophy here is a hallmark of Huntington's disease and can produce chorea and cognitive decline." },
            { "body_of_caudate",
                "The body of the {HEMI} caudate continues the caudate's role in motor and cognitive control. " +
                "Damage contributes to motor disorders and impaired learning of new habits." },
            { "tail_of_caudate",
                "The tail of the {HEMI} caudate wraps toward the temporal lobe and is involved in associative learning and visual category learning. " +
                "It is also affected in Huntington's disease as the disorder progresses." },
            { "nucleus_accumbens",
                "The {HEMI} nucleus accumbens is part of the ventral striatum and is central to reward, motivation, and reinforcement learning. " +
                "It is a key node in addiction and a target of deep brain stimulation for treatment-resistant depression and OCD." },
            { "subthalamic_nucleus",
                "The {HEMI} subthalamic nucleus (STN) is part of the indirect basal ganglia pathway and helps regulate movement intensity. " +
                "Lesions can cause hemiballismus (large flinging movements) on the opposite side; the STN is a major DBS target for Parkinson's disease." },
            { "substantia_nigra",
                "The {HEMI} substantia nigra contains dopamine neurons that modulate basal ganglia activity. " +
                "Loss of these neurons causes Parkinson's disease, with bradykinesia, rigidity, and resting tremor." },
            { "claustrum",
                "The {HEMI} claustrum is a thin sheet of grey matter beside the insula thought to coordinate widespread cortical activity. " +
                "Damage is rare but has been linked to disturbances of consciousness and attention." },

            // ===== Thalamus & related =====
            { "thalamus",
                "The {HEMI} thalamus is the major relay for sensory and motor information traveling to the cerebral cortex. " +
                "Strokes here can cause complex deficits including hemisensory loss, neglect, language disturbance, and central post-stroke pain." },
            { "anterior_nuclear_complex_of_thalamus",
                "The anterior nuclei of the {HEMI} thalamus are part of the limbic Papez circuit and support memory. " +
                "Damage, as in Korsakoff's syndrome from thiamine deficiency, produces severe anterograde amnesia." },
            { "centromedian_nucleus_of_thalamus",
                "The centromedian intralaminar nucleus of the {HEMI} thalamus modulates arousal and basal ganglia output. " +
                "Damage can blunt arousal and contribute to movement disorders." },
            { "dorsal_lateral_geniculate_nucleus",
                "The {HEMI} lateral geniculate nucleus relays visual information from the optic tract to primary visual cortex. " +
                "Damage produces contralateral homonymous visual field defects." },
            { "habenular_nuclei",
                "The {HEMI} habenular nuclei in the epithalamus influence dopamine and serotonin systems and reward processing. " +
                "Habenular dysfunction has been implicated in depression and altered response to negative outcomes." },
            { "lateral_dorsal_nucleus_of_thalamus",
                "The lateral dorsal nucleus of the {HEMI} thalamus connects with limbic cortex to support memory and emotion. " +
                "Damage may contribute to memory deficits when paired with other limbic injury." },
            { "lateral_posterior_nucleus_of_thalamus",
                "The lateral posterior nucleus of the {HEMI} thalamus is involved in spatial attention and visual integration. " +
                "Lesions can contribute to neglect and attentional deficits." },
            { "medial_geniculate_nuclei",
                "The {HEMI} medial geniculate nucleus relays auditory information from the inferior colliculus to the auditory cortex. " +
                "Damage can produce subtle auditory perception deficits since most pathways are bilateral." },
            { "mediodorsal_nucleus_of_thalamus",
                "The mediodorsal nucleus of the {HEMI} thalamus is the main thalamic partner of the prefrontal cortex for memory and cognition. " +
                "Damage contributes to amnesia in Korsakoff's syndrome and to executive dysfunction." },
            { "midline_nuclear_complex",
                "The midline {HEMI} thalamic nuclei modulate arousal, emotion, and limbic activity. " +
                "Damage can produce reduced alertness and emotional changes." },
            { "parafascicular_nucleus_of_thalamus",
                "The parafascicular intralaminar nucleus of the {HEMI} thalamus modulates striatal activity and arousal. " +
                "Lesions can contribute to attention and movement disturbances." },
            { "pulvinar_of_thalamus",
                "The {HEMI} pulvinar is a large thalamic nucleus involved in visual attention and integration across sensory areas. " +
                "Damage can produce contralateral neglect and impaired visual attention." },
            { "reuniens_nucleus_medioventral_nucleus_of_thalamus",
                "The reuniens nucleus of the {HEMI} thalamus coordinates communication between hippocampus and prefrontal cortex. " +
                "Disruption is implicated in working memory and decision-making deficits." },
            { "ventral_anterior_nucleus_of_thalamus",
                "The ventral anterior thalamic nucleus on the {HEMI} side relays basal ganglia signals to motor cortex. " +
                "Damage contributes to movement initiation problems seen in basal ganglia disorders." },
            { "ventral_lateral_nucleus_of_thalamus",
                "The ventral lateral thalamic nucleus on the {HEMI} side relays cerebellar input to motor cortex for movement coordination. " +
                "It is a common DBS target for essential tremor; lesions can cause incoordination." },
            { "ventral_posterior_lateral_nucleus",
                "The ventral posterolateral thalamic nucleus on the {HEMI} side relays body somatosensation to the cortex. " +
                "Damage causes contralateral hemisensory loss and can produce thalamic pain syndrome." },
            { "ventral_posterior_medial_nucleus",
                "The ventral posteromedial thalamic nucleus on the {HEMI} side relays facial somatosensation and taste to the cortex. " +
                "Damage produces contralateral facial sensory loss and can affect taste perception." },

            // ===== Amygdala / hippocampus =====
            { "amygdaloid_complex",
                "The {HEMI} amygdala processes emotion, especially fear and salience, and shapes emotional memory. " +
                "Damage reduces fear conditioning and the recognition of emotional facial expressions." },
            { "amygdalohippocampal_area",
                "The {HEMI} amygdalohippocampal area is the transition zone between amygdala and hippocampus, integrating emotion and memory. " +
                "Damage can blunt emotional memory formation and affect contextual fear learning." },
            { "anterior_amygdaloid_area",
                "The anterior {HEMI} amygdaloid area links olfactory input with emotional processing. " +
                "Lesions can impair odor-driven emotional responses and salience evaluation." },
            { "anterior_cortical_nucleus",
                "The anterior cortical nucleus of the {HEMI} amygdala receives olfactory input and contributes to emotion. " +
                "Damage can subtly disrupt smell-based emotional and social cues." },
            { "basolateral_nucleus_basal_nucleus",
                "The basolateral nucleus of the {HEMI} amygdala is central to fear learning and emotional memory. " +
                "Damage impairs the ability to learn that a stimulus predicts a threatening or rewarding outcome." },
            { "basomedial_nucleus_accessory_basal_nucleus",
                "The basomedial (accessory basal) nucleus of the {HEMI} amygdala integrates emotional and contextual information. " +
                "Lesions can affect context-dependent emotional responses." },
            { "central_nuclear_group",
                "The central nuclear group of the {HEMI} amygdala drives autonomic and behavioral responses to threat. " +
                "Damage reduces fear-related changes in heart rate, freezing, and startle responses." },
            { "lateral_nucleus",
                "The lateral nucleus of the {HEMI} amygdala is the primary input region for sensory information during fear learning. " +
                "Damage impairs the formation of new fear associations." },
            { "medial_nucleus",
                "The medial nucleus of the {HEMI} amygdala processes social and pheromonal cues. " +
                "Lesions can disrupt social and reproductive behavior in animal models, with subtler effects in humans." },
            { "posterior_cortical_nucleus",
                "The posterior cortical nucleus of the {HEMI} amygdala is involved in olfactory-emotional processing. " +
                "Damage can subtly alter the emotional response to smells." },
            { "head_of_hippocampus",
                "The head of the {HEMI} hippocampus is the anterior part involved in emotional and novelty-related memory. " +
                "Damage causes anterograde amnesia, especially for emotional and personal events; this region is among the first affected in Alzheimer's disease." },
            { "body_of_hippocampus",
                "The body of the {HEMI} hippocampus is the central region for spatial and episodic memory formation. " +
                "Damage produces severe anterograde amnesia and impaired navigation." },
            { "tail_of_hippocampus",
                "The tail of the {HEMI} hippocampus contributes to spatial memory and scene recognition. " +
                "Damage can affect navigation and contextual memory." },

            // ===== Hypothalamus regions =====
            { "hypothalamus",
                "The {HEMI} hypothalamus coordinates autonomic, endocrine, and homeostatic functions, including temperature, hunger, thirst, sleep, and stress responses. " +
                "Damage can disrupt body temperature regulation, sleep cycles, appetite, and hormonal balance." },
            { "mammillary_region_of_HTH",
                "The mammillary region of the {HEMI} hypothalamus is part of the limbic Papez circuit for memory. " +
                "Damage from thiamine deficiency produces Wernicke-Korsakoff syndrome with severe amnesia and confabulation." },
            { "preoptic_region_of_HTH",
                "The preoptic region of the {HEMI} hypothalamus regulates body temperature, sleep, and reproductive behavior. " +
                "Damage can cause disturbed thermoregulation and altered sleep-wake cycles." },
            { "supraoptic_region_of_HTH",
                "The supraoptic region of the {HEMI} hypothalamus produces hormones including vasopressin and oxytocin for fluid balance and lactation. " +
                "Damage can cause central diabetes insipidus with excessive urination and thirst." },
            { "tuberal_region_of_HTH",
                "The tuberal region of the {HEMI} hypothalamus controls pituitary hormone release and feeding behaviors. " +
                "Damage can produce endocrine disturbances, abnormal weight changes, and altered reproductive function." },

            // ===== Basal forebrain / commissures / tracts =====
            { "basal_forebrain",
                "The {HEMI} basal forebrain is a major source of cholinergic input to the cortex, important for arousal, attention, and memory. " +
                "Loss of these cholinergic neurons is a hallmark of Alzheimer's disease and underlies many memory symptoms." },
            { "bed_nucleus_of_stria_terminalis",
                "The {HEMI} bed nucleus of the stria terminalis is part of the extended amygdala and mediates sustained anxiety and stress responses. " +
                "It is implicated in anxiety disorders and PTSD." },
            { "septal_nuclei",
                "The {HEMI} septal nuclei modulate hippocampal activity and contribute to reward and emotion. " +
                "Damage can cause disinhibited rage in animal models and affect memory in humans." },
            { "zona_incerta",
                "The {HEMI} zona incerta sits below the thalamus and integrates motor, sensory, and limbic signals. " +
                "It is being explored as a deep brain stimulation target for tremor and Parkinson's disease." },
            { "fornix",
                "The {HEMI} fornix is the main output tract of the hippocampus, carrying memory-related signals to the mammillary bodies. " +
                "Damage causes severe anterograde amnesia, similar to direct hippocampal injury." },
            { "corpus_callosum",
                "The {HEMI} half of the corpus callosum is the major commissural tract connecting the two cerebral hemispheres. " +
                "Damage or surgical division (corpus callosotomy) can produce split-brain phenomena and disconnection syndromes." },
            { "anterior_commissure",
                "The anterior commissure connects parts of the temporal lobes and olfactory regions across the midline. " +
                "Damage can mildly affect interhemispheric transfer of olfactory and emotional information." },
            { "white_matter_of_forebrain",
                "The {HEMI} forebrain white matter consists of myelinated axon bundles connecting cortical and subcortical regions. " +
                "Diffuse white matter damage, as in multiple sclerosis or vascular disease, slows information transfer and impairs cognition." },

            // ===== Midbrain =====
            { "red_nucleus",
                "The {HEMI} red nucleus in the midbrain is involved in motor coordination and works closely with the cerebellum. " +
                "Damage can produce contralateral tremor and ataxia (rubral or Holmes' tremor)." },
            { "pretectal_region",
                "The {HEMI} pretectal region in the midbrain controls pupillary light reflexes and certain eye movements. " +
                "Damage can produce light-near dissociation of the pupils, as in Parinaud's syndrome from pineal-region tumors." },
            { "pineal_body",
                "The pineal body produces melatonin to regulate circadian and seasonal rhythms. " +
                "Pineal-region tumors can cause Parinaud's syndrome and disturb sleep and hormonal cycles." },
            { "midbrain_tegmentum",
                "The {HEMI} midbrain tegmentum contains motor, autonomic, and arousal nuclei. " +
                "Damage can impair eye movements, consciousness, and produce contralateral motor deficits." },
            { "superior_colliculus",
                "The {HEMI} superior colliculus coordinates reflexive eye movements and orienting responses to visual stimuli. " +
                "Damage can impair saccadic eye movements and visual orienting." },
            { "inferior_colliculus",
                "The {HEMI} inferior colliculus is a key auditory midbrain relay between brainstem and the medial geniculate nucleus. " +
                "Damage can subtly impair sound localization and processing of complex auditory input." },
            { "cerebral_peduncle_crus_cerebri",
                "The {HEMI} cerebral peduncle (crus cerebri) carries motor fibers from cortex to brainstem and spinal cord. " +
                "Damage causes contralateral hemiparesis affecting the body and lower face." },

            // ===== Pons / medulla =====
            { "basilar_part_of_pons",
                "The {HEMI} basilar pons relays cortical motor signals to the cerebellum via pontine nuclei. " +
                "Damage can produce ataxia and disrupt coordinated movement." },
            { "pontine_tegmentum",
                "The {HEMI} pontine tegmentum contains nuclei for arousal, eye movement, and autonomic control. " +
                "Damage can cause coma, gaze palsies, and locked-in syndrome with extensive bilateral injury." },
            { "tegmentum_of_medulla_oblongata",
                "The {HEMI} medullary tegmentum contains autonomic, sensory, and reticular nuclei controlling heart rate, breathing, and blood pressure. " +
                "Damage can be life-threatening due to disturbances of cardiovascular and respiratory function." },
            { "pyramidal_part_of_medulla_oblongata",
                "The {HEMI} medullary pyramid carries the corticospinal tract toward the spinal cord. " +
                "Damage above the pyramidal decussation produces contralateral weakness; below it, ipsilateral weakness." },
            { "inferior_olive",
                "The {HEMI} inferior olivary nucleus in the medulla provides climbing-fiber input to the cerebellum, important for motor learning. " +
                "Lesions can cause palatal myoclonus and impaired motor learning." },

            // ===== Cerebellum =====
            { "lateral_hemisphere_of_cerebellum",
                "The {HEMI} lateral cerebellar hemisphere is involved in motor planning and cognitive timing. " +
                "Damage causes ipsilateral incoordination (ataxia) and may contribute to cognitive-affective cerebellar syndrome." },
            { "paravermis_of_cerebellum",
                "The {HEMI} cerebellar paravermis coordinates ongoing limb movement with cortical commands. " +
                "Damage produces ipsilateral limb ataxia." },
            { "cerebellar_vermis",
                "The cerebellar vermis controls posture, gait, and trunk movements. " +
                "Damage causes truncal ataxia, with wide-based, unsteady walking even when limbs work normally." },
            { "cerebellar_deep_nuclei",
                "The {HEMI} deep cerebellar nuclei are the main output of the cerebellum to motor and premotor systems. " +
                "Damage produces severe ataxia and intention tremor." },
            { "superior_cerebellar_peduncle_brachium_conjunctivum",
                "The {HEMI} superior cerebellar peduncle is the main cerebellar output bundle to the thalamus and red nucleus. " +
                "Damage causes ipsilateral ataxia and intention tremor." },
            { "middle_cerebellar_peduncle",
                "The {HEMI} middle cerebellar peduncle carries pontine input into the cerebellum. " +
                "Damage produces ataxia by interrupting cortico-ponto-cerebellar signals." },
            { "inferior_cerebellar_peduncle",
                "The {HEMI} inferior cerebellar peduncle carries spinocerebellar and vestibular input into the cerebellum. " +
                "Damage produces ataxia and impaired balance and proprioception integration." },
            { "white_matter_of_hindbrain",
                "The {HEMI} hindbrain white matter contains tracts running through pons, medulla, and cerebellum. " +
                "Damage interrupts ascending and descending pathways and contributes to ataxia and weakness." },

            // ===== Tracts =====
            { "mammillothalamic_tract",
                "The {HEMI} mammillothalamic tract connects mammillary bodies to anterior thalamic nuclei in the limbic memory circuit. " +
                "Damage contributes to severe amnesia in Korsakoff's syndrome." },

            // ===== Ventricles =====
            { "anterior_horn_of_lateral_ventricle",
                "The {HEMI} anterior (frontal) horn of the lateral ventricle is a CSF-filled cavity within the frontal lobe. " +
                "Enlargement here suggests hydrocephalus or atrophy of surrounding tissue." },
            { "body_of_lateral_ventricle",
                "The {HEMI} body of the lateral ventricle is the central CSF cavity beneath the corpus callosum. " +
                "Asymmetric enlargement can indicate volume loss or obstructed CSF flow." },
            { "atrium_of_lateral_ventricle",
                "The {HEMI} atrium (trigone) of the lateral ventricle is where the body, posterior, and inferior horns meet. " +
                "Tumors in this region can obstruct CSF flow and cause hydrocephalus." },
            { "posterior_horn_of_lateral_ventricle",
                "The {HEMI} posterior (occipital) horn of the lateral ventricle extends into the occipital lobe. " +
                "Asymmetric enlargement can be a normal variant or reflect occipital tissue loss." },
            { "inferior_horn_of_lateral_ventricle",
                "The {HEMI} inferior (temporal) horn of the lateral ventricle extends into the temporal lobe alongside the hippocampus. " +
                "Enlargement here is an early sign of medial temporal atrophy in Alzheimer's disease." },
            { "third_ventricle",
                "The third ventricle is a midline CSF-filled cavity between the two thalami (shown on the {HEMI} side). " +
                "Tumors or cysts here, such as colloid cysts, can cause acute hydrocephalus and headache." },
            { "cerebral_aqueduct",
                "The cerebral aqueduct (shown on the {HEMI} side) links the third and fourth ventricles within the midbrain. " +
                "Obstruction causes obstructive hydrocephalus, a common cause of headache and altered mental state." },
            { "fourth_ventricle",
                "The fourth ventricle (shown on the {HEMI} side) lies between the brainstem and cerebellum and carries CSF to the central canal and subarachnoid spaces. " +
                "Tumors here, especially in children, can obstruct CSF flow and produce hydrocephalus." },
            { "central_canal_of_medulla_oblongata",
                "The central canal continues the CSF system from the fourth ventricle into the spinal cord (shown on the {HEMI} side). " +
                "Pathological dilation, as in syringomyelia, can compress surrounding fibers and cause sensory loss and weakness." },

            // ===== Midline =====
            { "optic_chiasm",
                "The optic chiasm sits at the midline where fibers from the nasal halves of each retina cross before entering the optic tracts. " +
                "Compression by pituitary tumors typically causes bitemporal hemianopia, a loss of peripheral vision in both eyes." },
        };
    }
}
