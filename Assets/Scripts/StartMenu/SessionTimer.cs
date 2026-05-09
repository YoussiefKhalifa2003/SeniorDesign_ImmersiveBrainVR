using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays session info on the HUD:
///   Line 1:  UserName  |  Session: MM:SS
///   Line 2:  Step X/5: Task Name — 00:15  (pulsing dot while active)
///
/// StartTimer() is called by MenuManager after Play is pressed.
/// </summary>
public class SessionTimer : MonoBehaviour
{
    [Header("UI References")]
    public Text timerText;
    public Text taskText;
    public GameObject hudPanel;

    private float _startTime;
    private bool _running;
    private float _pulseTimer;

    private void Start()
    {
        if (hudPanel != null)
            hudPanel.SetActive(false);
    }

    public void StartTimer()
    {
        _startTime = Time.time;
        _running = true;

        if (hudPanel != null)
            hudPanel.SetActive(true);

        Debug.Log("[SessionTimer] Timer started.");
    }

    public void StopTimer()
    {
        _running = false;
    }

    private void Update()
    {
        if (!_running) return;

        float elapsed = Time.time - _startTime;
        int minutes = Mathf.FloorToInt(elapsed / 60f);
        int seconds = Mathf.FloorToInt(elapsed % 60f);
        string name = string.IsNullOrEmpty(SessionData.UserName) ? "User" : SessionData.UserName;

        if (timerText != null)
            timerText.text = $"{name}  |  Session: {minutes:00}:{seconds:00}";

        UpdateTaskDisplay();
    }

    private void UpdateTaskDisplay()
    {
        if (taskText == null) return;

        var ttm = TaskTimerManager.Instance;
        if (ttm == null || string.IsNullOrEmpty(ttm.CurrentTaskDisplayName))
        {
            if (ttm != null && ttm.CurrentStep >= ttm.TotalSteps && !ttm.IsTimingActive)
                taskText.text = "All tasks complete";
            else
                taskText.text = "";
            return;
        }

        float taskElapsed = ttm.CurrentTaskElapsed;
        int tMin = Mathf.FloorToInt(taskElapsed / 60f);
        int tSec = Mathf.FloorToInt(taskElapsed % 60f);

        _pulseTimer += Time.deltaTime;
        string dot = (_pulseTimer % 1.2f) < 0.6f ? "\u25CF" : "\u25CB";

        taskText.text = $"{dot}  Step {ttm.CurrentStep}/{ttm.TotalSteps}: {ttm.CurrentTaskDisplayName}  —  {tMin:00}:{tSec:00}";
    }
}
