using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DissectionCase
{
    [TextArea(3, 6)]
    public string scenarioText;
    public string correctRegionKeyword;
    public string[] wrongRegionKeywords;
    [TextArea(2, 4)]
    public string explanation;
    [TextArea(2, 4)]
    public string voiceoverText;
    [Tooltip("1 = Easy, 2 = Medium, 3 = Hard")]
    public int difficulty = 2;
}

[CreateAssetMenu(fileName = "CasePromptBank", menuName = "Brain Dissection/Case Prompt Bank")]
public class CasePrompt : ScriptableObject
{
    public List<DissectionCase> cases = new List<DissectionCase>();
}
