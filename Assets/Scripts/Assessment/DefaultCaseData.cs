using System.Collections.Generic;

/// <summary>
/// Default clinical cases for Live Dissection when no CasePrompt asset is assigned.
/// Each case has a scenario, one correct region keyword, and wrong keywords
/// matched against BrainRegion.regionData.displayName at runtime.
/// Shortfall in keyword-matched regions is filled with random regions.
/// Wrong keywords are varied across cases to avoid repetitive highlights.
/// </summary>
public static class DefaultCaseData
{
    public static List<DissectionCase> GetCases()
    {
        return new List<DissectionCase>
        {
            new DissectionCase {
                scenarioText = "A 65-year-old patient presents with progressive memory loss. They cannot form new memories and frequently forget recent conversations. Family reports significant personality changes over the past year.",
                correctRegionKeyword = "Hippocamp",
                wrongRegionKeywords = new[] { "Frontal", "Cuneus", "Cingulate", "Insula", "Putamen", "Precuneus" },
                explanation = "The hippocampus is essential for converting short-term memories into long-term storage. Damage here causes anterograde amnesia.",
                voiceoverText = "Patient presenting with progressive memory loss. Identify the brain region most likely responsible.",
                difficulty = 1
            },
            new DissectionCase {
                scenarioText = "A 45-year-old patient suffered a stroke and now cannot produce coherent speech. They understand what others say but struggle to form words and sentences.",
                correctRegionKeyword = "Frontal",
                wrongRegionKeywords = new[] { "Temporal", "Angular", "Supramarginal", "Caudate", "Calcarine", "Lingual" },
                explanation = "Broca's area in the inferior frontal gyrus controls speech production. Damage causes expressive (Broca's) aphasia.",
                voiceoverText = "Patient cannot produce coherent speech after a stroke. Which region is most likely affected?",
                difficulty = 1
            },
            new DissectionCase {
                scenarioText = "A 30-year-old patient reports sudden loss of vision in both eyes after a car accident with head trauma to the back of the skull. Pupils react normally to light.",
                correctRegionKeyword = "Occipital",
                wrongRegionKeywords = new[] { "Precentral", "Postcentral", "Fusiform", "Thalamus", "Amygdala", "Parahippo" },
                explanation = "The occipital lobe contains the primary visual cortex. Bilateral damage causes cortical blindness with preserved pupillary reflexes.",
                voiceoverText = "Patient has cortical blindness after trauma to the back of the head. Identify the affected region.",
                difficulty = 2
            },
            new DissectionCase {
                scenarioText = "A patient exhibits severe coordination problems. They cannot walk in a straight line, have difficulty with fine motor tasks, and their speech is slurred.",
                correctRegionKeyword = "Cerebellum",
                wrongRegionKeywords = new[] { "Superior Frontal", "Middle Temporal", "Parietal Operc", "Rolandic", "Heschl", "Pallidum" },
                explanation = "The cerebellum coordinates voluntary movement, balance, and motor speech. Damage causes ataxia and dysarthria.",
                voiceoverText = "Patient shows severe motor coordination issues. Which brain structure is responsible?",
                difficulty = 1
            },
            new DissectionCase {
                scenarioText = "A 55-year-old patient can hear sounds but cannot understand spoken language. They speak fluently but their sentences are nonsensical and filled with made-up words.",
                correctRegionKeyword = "Temporal",
                wrongRegionKeywords = new[] { "Orbital", "Rectus", "Paracentral", "Precuneus", "Olfactory", "Supplementary" },
                explanation = "Wernicke's area in the superior temporal gyrus handles language comprehension. Damage causes receptive (Wernicke's) aphasia with fluent but meaningless speech.",
                voiceoverText = "Patient speaks fluently but cannot understand language. Identify the region involved.",
                difficulty = 2
            },
            new DissectionCase {
                scenarioText = "A patient cannot feel touch, temperature, or pain on the right side of their body. They also have difficulty determining the position of their right arm without looking at it.",
                correctRegionKeyword = "Parietal",
                wrongRegionKeywords = new[] { "Inferior Frontal", "Hippocampus", "Cerebellum", "Fusiform", "Cuneus", "Insula" },
                explanation = "The parietal lobe's postcentral gyrus is the primary somatosensory cortex. The left parietal lobe processes right-side body sensations.",
                voiceoverText = "Patient has lost sensation on one side of the body. Which lobe processes somatosensory information?",
                difficulty = 2
            },
            new DissectionCase {
                scenarioText = "A 40-year-old patient has uncontrollable emotional outbursts, especially intense fear responses to non-threatening situations. Brain imaging shows a lesion in the medial temporal lobe.",
                correctRegionKeyword = "Temporal",
                wrongRegionKeywords = new[] { "Calcarine", "Lingual", "Angular", "Supramarginal", "Caudate", "Putamen" },
                explanation = "The amygdala in the medial temporal lobe processes emotional responses, especially fear. Lesions cause inappropriate fear responses or emotional dysregulation.",
                voiceoverText = "Patient has uncontrollable fear responses. A medial temporal lobe lesion is suspected. Identify the region.",
                difficulty = 3
            },
            new DissectionCase {
                scenarioText = "A 60-year-old patient cannot recognize familiar faces, including their spouse and children, despite having normal vision. They can identify people by voice.",
                correctRegionKeyword = "Fusiform",
                wrongRegionKeywords = new[] { "Precentral", "Cingulate", "Thalamus", "Rolandic", "Heschl", "Pallidum" },
                explanation = "The fusiform face area in the inferior temporal lobe specializes in facial recognition. Damage causes prosopagnosia (face blindness).",
                voiceoverText = "Patient cannot recognize familiar faces despite normal vision. Which region is most likely affected?",
                difficulty = 3
            },
            new DissectionCase {
                scenarioText = "A patient has difficulty planning, organizing tasks, and controlling impulsive behavior. They were previously a successful manager but now cannot complete simple multi-step tasks.",
                correctRegionKeyword = "Frontal",
                wrongRegionKeywords = new[] { "Occipital", "Hippocampus", "Cerebellum", "Amygdala", "Postcentral", "Parahippo" },
                explanation = "The prefrontal cortex handles executive functions including planning, decision-making, and impulse control. Damage causes dysexecutive syndrome.",
                voiceoverText = "Patient shows severe executive dysfunction. Which brain region controls planning and organization?",
                difficulty = 2
            },
            new DissectionCase {
                scenarioText = "A 50-year-old patient reports chronic visceral discomfort, altered taste perception, and difficulty recognizing their own internal body states such as hunger and heart rate.",
                correctRegionKeyword = "Insula",
                wrongRegionKeywords = new[] { "Frontal", "Temporal", "Parietal", "Cuneus", "Rectus", "Supplementary" },
                explanation = "The insular cortex processes interoception — awareness of internal body states including pain, temperature, hunger, and autonomic regulation.",
                voiceoverText = "Patient cannot perceive internal body signals properly. Which deep cortical region is responsible for interoception?",
                difficulty = 3
            }
        };
    }
}
