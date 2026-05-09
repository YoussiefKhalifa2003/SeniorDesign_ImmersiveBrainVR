using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu: Tools &gt; Brain Dissection &gt; Clear All User Save Data...
/// Wipes PlayerPrefs for this Unity project / editor identity (every username)
/// plus known JSON/csv files in Application.persistentDataPath.
/// </summary>
public static class ClearAllUserSaveData
{
    const string MenuPath = "Tools/Brain Dissection/Clear All User Save Data...";

    [MenuItem(MenuPath, false, 300)]
    static void Execute()
    {
        if (!EditorUtility.DisplayDialog(
            "Clear all user save data?",
            "This removes all saved preferences for every username in this project: " +
            "tutorial/play progress, achievements, bookmarks, brightness, and FPS toggle.\n\n" +
            "It also deletes leaderboard.json, leaderboard_export.csv, and session_log.json " +
            "(if present) next to persistent data.\n\n" +
            "This cannot be undone.",
            "Clear everything",
            "Cancel"))
            return;

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        string dir = Application.persistentDataPath;
        TryDelete(Path.Combine(dir, "leaderboard.json"));
        TryDelete(Path.Combine(dir, "leaderboard_export.csv"));
        TryDelete(Path.Combine(dir, "session_log.json"));

        Debug.Log($"[ClearAllUserSaveData] PlayerPrefs cleared. Removed known JSON/CSV in:\n{dir}");
        EditorUtility.DisplayDialog("Done", $"All user save data cleared.\n\nPersistent folder:\n{dir}", "OK");
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[ClearAllUserSaveData] Could not delete {path}: {e.Message}");
        }
    }
}
