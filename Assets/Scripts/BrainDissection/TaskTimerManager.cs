using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Professional task-based timing system. Each major lab interaction
/// triggers a new timer segment. Results are sent to SessionLogger.
///
/// Task flow: Equip Gloves -> Equip Knife -> Brain Dissection ->
///            Equip Tweezers -> Region Extraction
///
/// Exposes current task info so the HUD can display it.
/// </summary>
public class TaskTimerManager : MonoBehaviour
{
    public static TaskTimerManager Instance { get; private set; }

    public string CurrentTaskId => _currentTask;
    public string CurrentTaskDisplayName => _currentTask != null && _taskNames.ContainsKey(_currentTask) ? _taskNames[_currentTask] : "";
    public int CurrentStep => _currentStep;
    public int TotalSteps => TOTAL_STEPS;
    public bool IsTimingActive => _timing;
    public float CurrentTaskElapsed => _timing ? Time.time - _taskStartTime : 0f;

    private const int TOTAL_STEPS = 5;

    private static readonly Dictionary<string, string> _taskNames = new Dictionary<string, string>
    {
        { "equipGloves",     "Equip Gloves" },
        { "equipKnife",      "Equip Knife" },
        { "brainDissection", "Brain Dissection" },
        { "equipTweezers",   "Equip Tweezers" },
        { "regionExtraction","Region Extraction" }
    };

    private string _currentTask;
    private float _taskStartTime;
    private bool _timing;
    private int _currentStep;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        var mgr = LabToolManager.Instance;
        if (mgr != null)
        {
            mgr.OnGlovesEquipped += OnGlovesEquipped;
            mgr.OnBrainSplit += OnBrainSplit;
            mgr.OnLabReset += OnLabReset;
        }
    }

    private void OnDestroy()
    {
        var mgr = LabToolManager.Instance;
        if (mgr != null)
        {
            mgr.OnGlovesEquipped -= OnGlovesEquipped;
            mgr.OnBrainSplit -= OnBrainSplit;
            mgr.OnLabReset -= OnLabReset;
        }
    }

    public void BeginSession()
    {
        _currentStep = 0;
        StartTask("equipGloves");
        Debug.Log("[TaskTimerManager] Session timing started. Waiting for gloves...");
    }

    private void OnGlovesEquipped()
    {
        FinishTask("equipGloves");
        StartTask("equipKnife");
    }

    public void OnKnifeEquipped()
    {
        FinishTask("equipKnife");
        StartTask("brainDissection");
    }

    private void OnBrainSplit()
    {
        FinishTask("brainDissection");
        StartTask("equipTweezers");
    }

    public void OnTweezersEquipped()
    {
        FinishTask("equipTweezers");
        StartTask("regionExtraction");
    }

    public void OnFirstRegionExtracted()
    {
        FinishTask("regionExtraction");
    }

    public void EndSession()
    {
        _timing = false;
        _currentTask = null;
        _currentStep = 0;
        Debug.Log("[TaskTimerManager] Session ended.");
    }

    private void OnLabReset()
    {
        _timing = false;
        _currentTask = null;
        _currentStep = 0;
    }

    private void StartTask(string taskName)
    {
        _currentTask = taskName;
        _taskStartTime = Time.time;
        _timing = true;
        _currentStep++;
    }

    private void FinishTask(string taskName)
    {
        if (!_timing || _currentTask != taskName) return;
        float duration = Time.time - _taskStartTime;
        _timing = false;

        if (SessionLogger.Instance != null)
            SessionLogger.Instance.RecordTask(taskName, duration);

        Debug.Log($"[TaskTimerManager] Task '{taskName}' completed in {duration:F1}s");
    }
}
