using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Writes session data (student name, timestamps, task timers) to a local JSON file.
/// Each session is appended as a new entry without overwriting previous sessions.
/// File location: Application.persistentDataPath/session_log.json
/// </summary>
public class SessionLogger : MonoBehaviour
{
    public static SessionLogger Instance { get; private set; }

    private SessionEntry _current;
    private string _filePath;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _filePath = Path.Combine(Application.persistentDataPath, "session_log.json");
        Debug.Log($"[SessionLogger] Log file will be saved to:\n{_filePath}");
    }

    public void BeginSession(string studentName)
    {
        _current = new SessionEntry
        {
            name = studentName,
            sessionStartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            tasks = new TaskTimings()
        };
        Debug.Log($"[SessionLogger] Session started for {studentName}. Log file: {_filePath}");
    }

    public void RecordTask(string taskKey, float durationSeconds)
    {
        if (_current == null) return;
        string formatted = $"{durationSeconds:F1}s";

        switch (taskKey)
        {
            case "equipGloves":     _current.tasks.equipGloves = formatted; break;
            case "equipKnife":      _current.tasks.equipKnife = formatted; break;
            case "brainDissection": _current.tasks.brainDissection = formatted; break;
            case "equipTweezers":   _current.tasks.equipTweezers = formatted; break;
            case "regionExtraction": _current.tasks.regionExtraction = formatted; break;
        }
        SaveToDisk();
    }

    public void EndSession()
    {
        if (_current == null) return;
        _current.sessionEndTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        SaveToDisk();
        Debug.Log("[SessionLogger] Session ended and saved.");
    }

    private void SaveToDisk()
    {
        if (_current == null) return;

        SessionLog log;
        if (File.Exists(_filePath))
        {
            try
            {
                string existing = File.ReadAllText(_filePath);
                log = JsonUtility.FromJson<SessionLog>(existing);
                if (log == null || log.sessions == null)
                    log = new SessionLog { sessions = new List<SessionEntry>() };
            }
            catch
            {
                log = new SessionLog { sessions = new List<SessionEntry>() };
            }
        }
        else
        {
            log = new SessionLog { sessions = new List<SessionEntry>() };
        }

        int idx = log.sessions.FindIndex(s =>
            s.name == _current.name && s.sessionStartTime == _current.sessionStartTime);
        if (idx >= 0)
            log.sessions[idx] = _current;
        else
            log.sessions.Add(_current);

        string json = JsonUtility.ToJson(log, true);
        File.WriteAllText(_filePath, json);
    }

    [Serializable]
    private class SessionLog
    {
        public List<SessionEntry> sessions = new List<SessionEntry>();
    }

    [Serializable]
    private class SessionEntry
    {
        public string name;
        public string sessionStartTime;
        public string sessionEndTime;
        public TaskTimings tasks;
    }

    [Serializable]
    private class TaskTimings
    {
        public string equipGloves = "";
        public string equipKnife = "";
        public string brainDissection = "";
        public string equipTweezers = "";
        public string regionExtraction = "";
    }
}
