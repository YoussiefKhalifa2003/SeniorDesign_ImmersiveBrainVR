using UnityEngine;

/// <summary>
/// Hides DummyPatient and Dissection_Brain on scene start.
/// They are only activated by LiveDissectionManager when the user
/// selects Live Dissection from the Assessment menu.
/// Searches ALL objects including inactive ones, and matches case-insensitively.
/// </summary>
public class AssessmentObjectHider : MonoBehaviour
{
    static readonly string[] ObjectsToHide = {
        "DummyPatient", "Dissection_Brain", "dissection_brain"
    };

    void Awake()
    {
        foreach (var name in ObjectsToHide)
            HideByName(name);
    }

    void HideByName(string objectName)
    {
        var go = GameObject.Find(objectName);
        if (go != null)
        {
            go.SetActive(false);
            Debug.Log($"[AssessmentObjectHider] '{objectName}' hidden.");
            return;
        }

        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name == objectName)
            {
                t.gameObject.SetActive(false);
                Debug.Log($"[AssessmentObjectHider] '{objectName}' found via search and hidden.");
                return;
            }
        }
    }
}
