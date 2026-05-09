using System.Collections.Generic;

/// <summary>
/// Default quiz questions used when no QuizQuestionBank asset is assigned.
/// </summary>
public static class DefaultQuizData
{
    public static List<QuizQuestion> GetQuestions()
    {
        return new List<QuizQuestion>
        {
            new QuizQuestion {
                questionText = "Which brain region is primarily responsible for memory formation?",
                correctAnswer = "Hippocampus",
                wrongAnswers = new[] { "Frontal Lobe", "Cerebellum", "Occipital Lobe" },
                explanation = "The hippocampus, located in the medial temporal lobe, is critical for converting short-term memories into long-term memories.",
                difficulty = QuizDifficulty.Easy,
                category = "Function",
                lobe = "Limbic"
            },
            new QuizQuestion {
                questionText = "Which part of the brain controls voluntary movement?",
                correctAnswer = "Motor Cortex",
                wrongAnswers = new[] { "Hippocampus", "Broca's Area", "Temporal Lobe" },
                explanation = "The motor cortex, located in the frontal lobe's precentral gyrus, initiates and controls voluntary muscle movements.",
                difficulty = QuizDifficulty.Easy,
                category = "Function",
                lobe = "Frontal"
            },
            new QuizQuestion {
                questionText = "Where is the primary visual cortex located?",
                correctAnswer = "Occipital Lobe",
                wrongAnswers = new[] { "Parietal Lobe", "Frontal Lobe", "Temporal Lobe" },
                explanation = "The primary visual cortex (V1) is located in the occipital lobe at the back of the brain and processes visual information from the eyes.",
                difficulty = QuizDifficulty.Easy,
                category = "Anatomy",
                lobe = "Occipital"
            },
            new QuizQuestion {
                questionText = "Which structure connects the two cerebral hemispheres?",
                correctAnswer = "Corpus Callosum",
                wrongAnswers = new[] { "Thalamus", "Pons", "Medulla Oblongata" },
                explanation = "The corpus callosum is a thick bundle of nerve fibers that allows communication between the left and right cerebral hemispheres.",
                difficulty = QuizDifficulty.Medium,
                category = "Anatomy",
                lobe = "All"
            },
            new QuizQuestion {
                questionText = "Damage to Broca's area most likely results in difficulty with:",
                correctAnswer = "Speech production",
                wrongAnswers = new[] { "Vision", "Memory", "Balance", "Hearing" },
                explanation = "Broca's area in the left frontal lobe is responsible for speech production. Damage causes expressive aphasia — difficulty forming words despite understanding language.",
                difficulty = QuizDifficulty.Medium,
                category = "Function",
                lobe = "Frontal"
            },
            new QuizQuestion {
                questionText = "Which brain region regulates body temperature, hunger, and thirst?",
                correctAnswer = "Hypothalamus",
                wrongAnswers = new[] { "Cerebellum", "Amygdala", "Parietal Lobe" },
                explanation = "The hypothalamus is a small region at the base of the brain that maintains homeostasis by regulating temperature, hunger, thirst, sleep, and hormone release.",
                difficulty = QuizDifficulty.Medium,
                category = "Function",
                lobe = "Limbic"
            },
            new QuizQuestion {
                questionText = "The cerebellum is primarily responsible for:",
                correctAnswer = "Coordination and balance",
                wrongAnswers = new[] { "Language processing", "Emotional regulation", "Memory storage" },
                explanation = "The cerebellum coordinates voluntary movements, maintains balance and posture, and enables motor learning.",
                difficulty = QuizDifficulty.Easy,
                category = "Function",
                lobe = "Cerebellum"
            },
            new QuizQuestion {
                questionText = "Which lobe of the brain is most associated with decision-making and personality?",
                correctAnswer = "Frontal Lobe",
                wrongAnswers = new[] { "Temporal Lobe", "Parietal Lobe", "Occipital Lobe" },
                explanation = "The frontal lobe, especially the prefrontal cortex, is crucial for executive functions including planning, decision-making, and personality expression.",
                difficulty = QuizDifficulty.Easy,
                category = "Function",
                lobe = "Frontal"
            },
            new QuizQuestion {
                questionText = "Wernicke's area is primarily involved in:",
                correctAnswer = "Language comprehension",
                wrongAnswers = new[] { "Motor control", "Visual processing", "Spatial awareness", "Pain sensation" },
                explanation = "Wernicke's area in the posterior temporal lobe is essential for understanding spoken and written language. Damage causes receptive aphasia.",
                difficulty = QuizDifficulty.Hard,
                category = "Function",
                lobe = "Temporal"
            },
            new QuizQuestion {
                questionText = "Which structure acts as a relay station for sensory information going to the cerebral cortex?",
                correctAnswer = "Thalamus",
                wrongAnswers = new[] { "Hypothalamus", "Hippocampus", "Amygdala", "Basal Ganglia" },
                explanation = "The thalamus relays almost all sensory information (except smell) to the appropriate cortical area for processing.",
                difficulty = QuizDifficulty.Hard,
                category = "Function",
                lobe = "Limbic"
            },
            new QuizQuestion {
                questionText = "The amygdala is most closely associated with processing which type of information?",
                correctAnswer = "Emotions, especially fear",
                wrongAnswers = new[] { "Spatial navigation", "Language syntax", "Motor planning", "Taste perception" },
                explanation = "The amygdala is an almond-shaped structure in the temporal lobe that plays a key role in processing emotions, particularly fear and threat detection.",
                difficulty = QuizDifficulty.Medium,
                category = "Function",
                lobe = "Limbic"
            },
            new QuizQuestion {
                questionText = "A patient has difficulty recognizing faces after brain damage. Which condition is this?",
                correctAnswer = "Prosopagnosia",
                wrongAnswers = new[] { "Aphasia", "Apraxia", "Agnosia", "Ataxia" },
                explanation = "Prosopagnosia (face blindness) results from damage to the fusiform face area in the temporal lobe, impairing the ability to recognize familiar faces.",
                difficulty = QuizDifficulty.Hard,
                category = "Clinical",
                lobe = "Temporal"
            },
            new QuizQuestion {
                questionText = "Which part of the brainstem controls heart rate and breathing?",
                correctAnswer = "Medulla Oblongata",
                wrongAnswers = new[] { "Pons", "Midbrain", "Thalamus" },
                explanation = "The medulla oblongata at the base of the brainstem controls autonomic functions including heart rate, breathing, and blood pressure.",
                difficulty = QuizDifficulty.Medium,
                category = "Function",
                lobe = "Brainstem"
            },
            new QuizQuestion {
                questionText = "The parietal lobe is primarily responsible for processing:",
                correctAnswer = "Somatosensory information (touch, pressure, temperature)",
                wrongAnswers = new[] { "Auditory information", "Visual information", "Olfactory information", "Gustatory information" },
                explanation = "The parietal lobe processes somatosensory input including touch, pressure, temperature, and spatial awareness through the postcentral gyrus.",
                difficulty = QuizDifficulty.Hard,
                category = "Function",
                lobe = "Parietal"
            },
            new QuizQuestion {
                questionText = "Which neurotransmitter is primarily associated with the reward system?",
                correctAnswer = "Dopamine",
                wrongAnswers = new[] { "Serotonin", "GABA", "Acetylcholine" },
                explanation = "Dopamine is the primary neurotransmitter in the brain's reward pathway (mesolimbic system), driving motivation, pleasure, and reinforcement learning.",
                difficulty = QuizDifficulty.Medium,
                category = "Neuroscience",
                lobe = "Limbic"
            },
            new QuizQuestion {
                questionText = "A region on the brain is highlighted. Identify it by clicking on it.",
                correctAnswer = "Hippocampus",
                wrongAnswers = new string[0],
                explanation = "The hippocampus is located in the medial temporal lobe and is critical for memory formation.",
                difficulty = QuizDifficulty.Medium,
                category = "Identify",
                lobe = "Limbic",
                questionType = QuizQuestionType.IdentifyRegion,
                targetRegionKeyword = "Hippocamp"
            },
            new QuizQuestion {
                questionText = "A region on the brain is highlighted. Click on it to identify it.",
                correctAnswer = "Frontal Lobe",
                wrongAnswers = new string[0],
                explanation = "The frontal lobe controls executive functions, motor activity, and speech production.",
                difficulty = QuizDifficulty.Easy,
                category = "Identify",
                lobe = "Frontal",
                questionType = QuizQuestionType.IdentifyRegion,
                targetRegionKeyword = "Frontal"
            },
            new QuizQuestion {
                questionText = "Which highlighted region is this? Click on it.",
                correctAnswer = "Temporal Lobe",
                wrongAnswers = new string[0],
                explanation = "The temporal lobe processes auditory information and is involved in memory and language comprehension.",
                difficulty = QuizDifficulty.Easy,
                category = "Identify",
                lobe = "Temporal",
                questionType = QuizQuestionType.IdentifyRegion,
                targetRegionKeyword = "Temporal"
            }
        };
    }
}
