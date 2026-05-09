using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Stores and retrieves quiz scores from a local JSON file.
/// Provides CSV export for instructors.
/// </summary>
public static class LeaderboardManager
{
    static string FilePath => Path.Combine(Application.persistentDataPath, "leaderboard.json");

    [Serializable]
    public class Entry
    {
        public string studentName;
        public int score;
        public int totalQuestions;
        public string date;
        public string mode;
        // Time taken to complete the assessment (seconds). 0 for legacy entries
        // recorded before assessment timing existed.
        public int elapsedSeconds;
    }

    [Serializable]
    class Board
    {
        public List<Entry> entries = new List<Entry>();
    }

    public static void RecordScore(string name, int score, int totalQuestions,
        string mode = "MCQ", int elapsedSeconds = 0)
    {
        var board = Load();
        board.entries.Add(new Entry
        {
            studentName = string.IsNullOrEmpty(name) ? "Anonymous" : name,
            score = score,
            totalQuestions = totalQuestions,
            date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            mode = mode,
            elapsedSeconds = Mathf.Max(0, elapsedSeconds)
        });
        Save(board);
        Debug.Log($"[Leaderboard] Recorded: {name} = {score}/{totalQuestions} in {elapsedSeconds}s ({mode})");
    }

    public static List<Entry> GetEntries()
    {
        var board = Load();
        board.entries.Sort((a, b) =>
        {
            int cmp = b.score.CompareTo(a.score);
            if (cmp != 0) return cmp;
            // Lower elapsed time wins on tie. Legacy entries with 0 are
            // treated as "unknown" and pushed below entries that have a time.
            int aTime = a.elapsedSeconds > 0 ? a.elapsedSeconds : int.MaxValue;
            int bTime = b.elapsedSeconds > 0 ? b.elapsedSeconds : int.MaxValue;
            cmp = aTime.CompareTo(bTime);
            if (cmp != 0) return cmp;
            return string.Compare(a.date, b.date, StringComparison.Ordinal);
        });
        return board.entries;
    }

    public static string FormatElapsed(int totalSeconds)
    {
        if (totalSeconds <= 0) return "—";
        int m = totalSeconds / 60;
        int s = totalSeconds % 60;
        return $"{m:0}:{s:00}";
    }

    public static string FormatMode(string mode)
    {
        if (string.IsNullOrEmpty(mode)) return "MCQ";
        if (mode == "LiveDissection") return "Live Dissection";
        return mode;
    }

    public static string ExportToCSV()
    {
        var entries = GetEntries();
        var csvPath = Path.Combine(Application.persistentDataPath, "leaderboard_export.csv");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Rank,Student,Mode,Score,Total,Percentage,Time,Date");
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            float pct = e.totalQuestions > 0 ? (float)e.score / e.totalQuestions * 100f : 0;
            sb.AppendLine($"{i + 1},{e.studentName},{FormatMode(e.mode)},{e.score},{e.totalQuestions},{pct:F0}%,{FormatElapsed(e.elapsedSeconds)},{e.date}");
        }
        File.WriteAllText(csvPath, sb.ToString());
        Debug.Log($"[Leaderboard] Exported to: {csvPath}");
        return csvPath;
    }

    static Board Load()
    {
        if (!File.Exists(FilePath))
            return new Board();
        try
        {
            return JsonUtility.FromJson<Board>(File.ReadAllText(FilePath)) ?? new Board();
        }
        catch
        {
            return new Board();
        }
    }

    static void Save(Board board)
    {
        File.WriteAllText(FilePath, JsonUtility.ToJson(board, true));
    }
}
