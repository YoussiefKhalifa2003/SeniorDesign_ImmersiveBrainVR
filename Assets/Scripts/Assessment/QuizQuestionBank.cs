using UnityEngine;
using System.Collections.Generic;

public enum QuizQuestionType { MultipleChoice, IdentifyRegion }

[System.Serializable]
public class QuizQuestion
{
    [TextArea(2, 4)]
    public string questionText;
    public string correctAnswer;
    public string[] wrongAnswers;
    [TextArea(2, 4)]
    public string explanation;
    public QuizDifficulty difficulty;
    public string category;
    public string lobe;
    public QuizQuestionType questionType;
    [Tooltip("For IdentifyRegion: keyword to match against BrainRegion.regionData.displayName")]
    public string targetRegionKeyword;
}

public enum QuizDifficulty { Easy, Medium, Hard }

[CreateAssetMenu(fileName = "QuizQuestionBank", menuName = "Brain Dissection/Quiz Question Bank")]
public class QuizQuestionBank : ScriptableObject
{
    public List<QuizQuestion> questions = new List<QuizQuestion>();
}
