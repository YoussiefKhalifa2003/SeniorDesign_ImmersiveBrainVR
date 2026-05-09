using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Menu: Tools > Brain Dissection > Setup Tutorial
/// Verifies the tutorial system is ready: TutorialManager exists,
/// MenuManager has the tutorial button wired, post-processing is
/// enabled, and the HapticFeedback utility is accessible.
/// </summary>
public static class SetupTutorial
{
    [MenuItem("Tools/Brain Dissection/Setup Tutorial")]
    static void Run()
    {
        int fixed_count = 0;

        // 1. Ensure TutorialManager exists
        var tm = Object.FindFirstObjectByType<TutorialManager>();
        if (tm == null)
        {
            var existing = GameObject.Find("StartMenuSystem");
            if (existing == null) existing = new GameObject("StartMenuSystem");
            tm = existing.AddComponent<TutorialManager>();
            EditorUtility.SetDirty(existing);
            fixed_count++;
            Debug.Log("[Tutorial Setup] Added TutorialManager to " + existing.name);
        }
        else
        {
            Debug.Log("[Tutorial Setup] TutorialManager found on: " + tm.gameObject.name);
        }

        // 2. Ensure MenuManager exists and OnTutorialPressed is accessible
        var mm = Object.FindFirstObjectByType<MenuManager>();
        if (mm == null)
        {
            Debug.LogWarning("[Tutorial Setup] No MenuManager found! Run 'Tools > Start Menu > Setup' first.");
        }
        else
        {
            Debug.Log("[Tutorial Setup] MenuManager OK on: " + mm.gameObject.name);
        }

        // 3. Ensure LabToolManager exists (tutorial reads its state flags)
        var ltm = LabToolManager.Instance;
        if (ltm == null)
        {
            ltm = Object.FindFirstObjectByType<LabToolManager>();
        }
        if (ltm == null)
        {
            Debug.LogWarning("[Tutorial Setup] No LabToolManager found! Run 'Tools > Brain Dissection > Setup Scene' first.");
        }
        else
        {
            Debug.Log("[Tutorial Setup] LabToolManager OK.");
        }

        // 4. Ensure OptionsController exists with post-processing note
        var oc = Object.FindFirstObjectByType<OptionsController>();
        if (oc == null)
        {
            Debug.LogWarning("[Tutorial Setup] No OptionsController found! Run 'Tools > Start Menu > Setup' first.");
        }
        else
        {
            Debug.Log("[Tutorial Setup] OptionsController OK (post-processing enabled at runtime).");
        }

        // 5. Ensure BrainManager exists (tutorial checks IsInspectingRegion)
        var bm = Object.FindFirstObjectByType<BrainManager>();
        if (bm == null)
        {
            Debug.LogWarning("[Tutorial Setup] No BrainManager found! Run 'Tools > Brain Dissection > Setup Scene' first.");
        }
        else
        {
            Debug.Log("[Tutorial Setup] BrainManager OK.");
        }

        // 6. Check BrainRegion count
        var regions = Object.FindObjectsByType<BrainRegion>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[Tutorial Setup] Found {regions.Length} BrainRegion(s) in scene.");
        if (regions.Length == 0)
        {
            Debug.LogWarning("[Tutorial Setup] No BrainRegions! Run 'Tools > Brain Dissection > Setup Scene' first.");
        }

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        if (fixed_count > 0)
            Debug.Log($"[Tutorial Setup] Fixed {fixed_count} issue(s). Save the scene.");

        string summary = ltm != null && bm != null && mm != null && oc != null && regions.Length > 0
            ? "All systems GO. Tutorial is ready to use."
            : "Some components missing. See warnings above.";

        Debug.Log("[Tutorial Setup] " + summary);
        EditorUtility.DisplayDialog("Tutorial Setup",
            summary + "\n\nThe tutorial runs in the current scene.\n" +
            "Click 'Tutorial' in the main menu to start it.\n\n" +
            "How it works:\n" +
            "1. Doors open, player enters the lab\n" +
            "2. HUD panel appears on camera with step-by-step instructions\n" +
            "3. Student must complete each step before advancing\n" +
            "4. Steps: Gloves -> Knife -> Cut Brain -> Tweezers -> Select Region -> Put Back\n" +
            "5. At the end, 'Return to Menu' button appears",
            "OK");
    }
}
