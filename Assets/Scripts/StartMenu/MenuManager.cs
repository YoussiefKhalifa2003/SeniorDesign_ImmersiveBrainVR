using System.Collections;
using UnityEngine;

/// <summary>
/// Controls the main menu panel transitions:
///   Login -> Main Menu -> (Play, Tutorial, Assessment, Options, Back to Login)
///
/// On Play: fades out the menu canvas, opens doors, then enables movement.
/// References are wired by StartMenuSetup editor script.
/// </summary>
public class MenuManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject loginPanel;
    public GameObject mainMenuPanel;
    public GameObject optionsPanel;
    public GameObject assessmentPanel;

    [Header("Play Gate")]
    public GameObject playButton;
    public UnityEngine.UI.Text playLockLabel;

    [Header("Assessment Gate")]
    public GameObject assessmentButton;
    public UnityEngine.UI.Text assessmentLockLabel;

    [Header("Canvas")]
    public CanvasGroup menuCanvasGroup;
    public GameObject startMenuCanvas;

    [Header("References")]
    public DoorController doorController;
    public MovementGate movementGate;
    public SessionTimer sessionTimer;
    public LabIntroduction labIntroduction;

    [Header("Settings")]
    public float fadeDuration = 1.0f;

    Vector3 _xrStartPos;
    Quaternion _xrStartRot;
    bool _startCached;

    private void Start()
    {
        CacheXROriginStartPose();
        ShowLoginPanel();
    }

    void CacheXROriginStartPose()
    {
        var xrOrigin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (xrOrigin != null)
        {
            _xrStartPos = xrOrigin.transform.position;
            _xrStartRot = xrOrigin.transform.rotation;
            _startCached = true;
            Debug.Log($"[MenuManager] Cached XR Origin start pose: pos={_xrStartPos}, rot={_xrStartRot.eulerAngles}");
        }
    }

    // ========================= PANEL TRANSITIONS =========================

    public void ShowLoginPanel()
    {
        SetPanel(loginPanel);
        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 1f;
            menuCanvasGroup.interactable = true;
            menuCanvasGroup.blocksRaycasts = true;
        }
        if (startMenuCanvas != null)
            startMenuCanvas.SetActive(true);
    }

    public void ShowMainMenu()
    {
        SetPanel(mainMenuPanel);
        EnsureGateReferences();
        RefreshPlayGate();
        RefreshAssessmentGate();
        ReloadUserSettings();
    }

    void ReloadUserSettings()
    {
        var optCtrl = FindFirstObjectByType<OptionsController>();
        if (optCtrl != null)
            optCtrl.ReloadForCurrentUser();
    }

    public void ShowOptions()
    {
        SetPanel(optionsPanel);
    }

    public void ShowAssessment()
    {
        SetPanel(assessmentPanel);
    }

    public void BackToLoginFromOptions()
    {
        ShowMainMenu();
    }

    public void BackToMenuFromAssessment()
    {
        ShowMainMenu();
    }

    public void ShowProgressDashboard()
    {
        var dash = FindFirstObjectByType<ProgressDashboard>();
        if (dash == null)
        {
            var go = new GameObject("ProgressDashboard");
            dash = go.AddComponent<ProgressDashboard>();
        }
        dash.Show();
    }

    public void ReturnToLogin()
    {
        SessionData.UserName = "";
        ShowLoginPanel();
    }

    void EnsureGateReferences()
    {
        if (mainMenuPanel == null) return;

        if (playButton == null)
            playButton = FindChildByPartialName(mainMenuPanel.transform, "Play");
        if (assessmentButton == null)
            assessmentButton = FindChildByPartialName(mainMenuPanel.transform, "Assess");
    }

    static GameObject FindChildByPartialName(Transform parent, string keyword)
    {
        foreach (Transform child in parent)
        {
            if (child.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (child.GetComponent<UnityEngine.UI.Button>() != null)
                    return child.gameObject;
            }
        }
        return null;
    }

    void RefreshPlayGate()
    {
        EnsureGateReferences();
        if (playButton == null) return;

        bool unlocked = ProgressTracker.CanAccessPlay;
        var btn = playButton.GetComponent<UnityEngine.UI.Button>();
        if (btn == null) btn = playButton.GetComponentInChildren<UnityEngine.UI.Button>();
        var img = playButton.GetComponent<UnityEngine.UI.Image>();

        if (btn != null) btn.interactable = unlocked;

        Color targetColor = unlocked
            ? new Color(0.12f, 0.50f, 0.22f, 1f)
            : new Color(0.25f, 0.25f, 0.30f, 1f);

        if (img != null) img.color = targetColor;
        if (btn != null)
        {
            var c = btn.colors;
            c.normalColor = targetColor;
            c.highlightedColor = targetColor * 1.2f;
            c.pressedColor = targetColor * 0.8f;
            c.selectedColor = targetColor * 1.1f;
            btn.colors = c;
        }

        // Find the lock label; if it doesn't exist in the scene, create one at runtime
        if (playLockLabel == null && playButton != null)
        {
            foreach (Transform child in playButton.transform)
            {
                if (child.name.IndexOf("PlayLock", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    playLockLabel = child.GetComponent<UnityEngine.UI.Text>();
                    break;
                }
            }
            if (playLockLabel == null && mainMenuPanel != null)
            {
                foreach (Transform child in mainMenuPanel.transform)
                {
                    if (child.name.IndexOf("PlayLock", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        playLockLabel = child.GetComponent<UnityEngine.UI.Text>();
                        break;
                    }
                }
            }

            if (playLockLabel == null)
            {
                var labelGO = new GameObject("PlayLockLabel_Runtime");
                labelGO.transform.SetParent(playButton.transform, false);
                var rt = labelGO.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0, 1f);
                rt.sizeDelta = new Vector2(0, 18);

                var txt = labelGO.AddComponent<UnityEngine.UI.Text>();
                txt.fontSize = 12;
                txt.fontStyle = FontStyle.Italic;
                txt.color = new Color(0.6f, 0.6f, 0.65f, 1f);
                txt.alignment = TextAnchor.MiddleCenter;
                txt.supportRichText = true;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                playLockLabel = txt;
            }
        }

        if (playLockLabel != null)
        {
            if (unlocked)
            {
                string tDone = "<color=#66FF66>Complete</color>";
                playLockLabel.text = $"Tutorial: {tDone}";
            }
            else
            {
                string tDone = ProgressTracker.TutorialCompleted
                    ? "<color=#66FF66>Complete</color>"
                    : "<color=#FF6666>Incomplete</color>";
                playLockLabel.text = $"LOCKED  |  Tutorial: {tDone}";
            }
        }
    }

    void RefreshAssessmentGate()
    {
        EnsureGateReferences();
        if (assessmentButton == null) return;

        bool unlocked = ProgressTracker.CanAccessAssessment;
        var btn = assessmentButton.GetComponent<UnityEngine.UI.Button>();
        if (btn == null) btn = assessmentButton.GetComponentInChildren<UnityEngine.UI.Button>();
        var img = assessmentButton.GetComponent<UnityEngine.UI.Image>();

        if (btn != null) btn.interactable = unlocked;
        if (img != null)
            img.color = unlocked
                ? new Color(0.50f, 0.20f, 0.60f, 1f)
                : new Color(0.25f, 0.25f, 0.30f, 1f);

        if (assessmentLockLabel != null)
        {
            if (unlocked)
            {
                assessmentLockLabel.text = "";
            }
            else
            {
                string tDone = ProgressTracker.TutorialCompleted ? "<color=#66FF66>Done</color>" : "<color=#FF6666>Incomplete</color>";
                string pDone = ProgressTracker.PlayCompleted ? "<color=#66FF66>Done</color>" : "<color=#FF6666>Incomplete</color>";
                assessmentLockLabel.text = $"LOCKED  |  Tutorial: {tDone}   Play: {pDone}";
            }
        }
    }

    // ========================= PLAY =========================

    public void OnPlayPressed()
    {
        if (!ProgressTracker.CanAccessPlay)
        {
            RefreshPlayGate();
            Debug.Log("[MenuManager] Play blocked until Tutorial is complete.");
            return;
        }

        Debug.Log("[MenuManager] Play pressed.");
        SessionData.IsPlayMode = true;
        SessionData.IsTutorialMode = false;
        SessionData.IsAssessmentMode = false;
        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        yield return StartCoroutine(FadeOutCanvas());

        // Always re-arm the lab so Play starts from a known baseline (clean hands,
        // gloves available on the table) — even after Tutorial finished.
        if (LabToolManager.Instance != null)
            LabToolManager.Instance.ResetLab();

        if (startMenuCanvas != null)
            startMenuCanvas.SetActive(false);

        if (doorController != null)
        {
            doorController.OpenDoors();
            yield return new WaitForSeconds(doorController.openDuration + 0.2f);
        }

        if (movementGate != null)
            movementGate.EnableMovement();

        // Play mode is free, untimed exploration. We deliberately do NOT
        // start SessionTimer or TaskTimerManager here so the HUD timer
        // never appears while students explore. Timing only happens during
        // assessments (MCQ + Live Dissection). SessionLogger is left running
        // for diagnostic logs but does not surface any clock to the user.
        if (sessionTimer != null && sessionTimer.hudPanel != null)
            sessionTimer.hudPanel.SetActive(false);

        if (SessionLogger.Instance != null)
            SessionLogger.Instance.BeginSession(SessionData.UserName);

        if (labIntroduction != null)
            labIntroduction.ShowIntroduction();

        Debug.Log("[MenuManager] Lab is ready. Player can move.");
    }

    // ========================= TUTORIAL =========================

    public void OnTutorialPressed()
    {
        Debug.Log("[MenuManager] Tutorial pressed.");
        SessionData.IsPlayMode = false;
        SessionData.IsTutorialMode = true;
        SessionData.IsAssessmentMode = false;
        StartCoroutine(TutorialSequence());
    }

    private IEnumerator TutorialSequence()
    {
        yield return StartCoroutine(FadeOutCanvas());

        if (startMenuCanvas != null)
            startMenuCanvas.SetActive(false);

        if (doorController != null)
        {
            doorController.OpenDoors();
            yield return new WaitForSeconds(doorController.openDuration + 0.2f);
        }

        if (movementGate != null)
            movementGate.EnableMovement();

        var tutorial = TutorialManager.Instance;
        if (tutorial == null)
        {
            var go = new GameObject("TutorialManager");
            tutorial = go.AddComponent<TutorialManager>();
        }

        tutorial.BeginTutorial();
    }

    // ========================= ASSESSMENT =========================

    public void OnAssessmentPressed()
    {
        Debug.Log("[MenuManager] Assessment pressed.");
        SessionData.IsPlayMode = false;
        SessionData.IsTutorialMode = false;
        SessionData.IsAssessmentMode = true;
        ShowAssessment();
    }

    public void OnMCQQuizPressed()
    {
        Debug.Log("[MenuManager] MCQ Quiz pressed.");
        StartCoroutine(StartAssessmentMode(() =>
        {
            var qm = FindFirstObjectByType<QuizManager>();
            if (qm == null)
            {
                var go = new GameObject("QuizManager");
                qm = go.AddComponent<QuizManager>();
            }
            qm.StartQuiz();
        }));
    }

    public void OnLiveDissectionPressed()
    {
        Debug.Log("[MenuManager] Live Dissection pressed.");
        StartCoroutine(StartAssessmentMode(() =>
        {
            var ldm = FindFirstObjectByType<LiveDissectionManager>();
            if (ldm == null)
            {
                var go = new GameObject("LiveDissectionManager");
                ldm = go.AddComponent<LiveDissectionManager>();
            }
            ldm.StartLiveDissection();
        }));
    }

    public void OnLeaderboardPressed()
    {
        Debug.Log("[MenuManager] Leaderboard pressed.");
        var lb = FindFirstObjectByType<LeaderboardUI>();
        if (lb == null)
        {
            var go = new GameObject("LeaderboardUI");
            lb = go.AddComponent<LeaderboardUI>();
        }
        lb.Show();
    }

    IEnumerator StartAssessmentMode(System.Action onReady)
    {
        yield return StartCoroutine(FadeOutCanvas());

        if (startMenuCanvas != null)
            startMenuCanvas.SetActive(false);

        if (doorController != null)
        {
            doorController.OpenDoors();
            yield return new WaitForSeconds(doorController.openDuration + 0.2f);
        }

        if (movementGate != null)
            movementGate.EnableMovement();

        onReady?.Invoke();
    }

    // ========================= END SESSION (Play mode only) =========================

    public void OnEndSessionPressed()
    {
        Debug.Log("[MenuManager] End session pressed.");

        if (SessionLogger.Instance != null)
            SessionLogger.Instance.EndSession();

        if (TaskTimerManager.Instance != null)
            TaskTimerManager.Instance.EndSession();

        if (sessionTimer != null)
            sessionTimer.StopTimer();

        ProgressTracker.MarkPlayComplete();
        if (AchievementManager.Instance != null)
            AchievementManager.Instance.CheckPlayComplete();

        SessionData.IsPlayMode = false;

        if (LabToolManager.Instance != null)
            LabToolManager.Instance.ResetLab();

        if (movementGate != null)
            movementGate.DisableMovement();

        // Close the doors
        if (doorController != null)
            doorController.CloseDoors();

        // Hide the floating brain dissection panel
        var floatingPanel = FindFirstObjectByType<FloatingInfoPanel>();
        if (floatingPanel != null)
        {
            var cg = floatingPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
        }

        // Teleport XR Origin back to start position
        TeleportToStart();

        StartCoroutine(ReturnToMenuSequence());
    }

    /// <summary>
    /// Teleports XR Origin back to its initial scene position.
    /// Public so TutorialManager can also call it on Return to Menu.
    /// </summary>
    public void TeleportToStart()
    {
        var xrOrigin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (xrOrigin != null)
        {
            if (_startCached)
            {
                xrOrigin.transform.position = _xrStartPos;
                xrOrigin.transform.rotation = _xrStartRot;
            }
            else
            {
                xrOrigin.transform.position = Vector3.zero;
                xrOrigin.transform.rotation = Quaternion.identity;
            }
            Debug.Log($"[MenuManager] XR Origin teleported to start: {xrOrigin.transform.position}");
        }
    }

    IEnumerator ReturnToMenuSequence()
    {
        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 0f;
            menuCanvasGroup.interactable = false;
            menuCanvasGroup.blocksRaycasts = false;
        }
        if (startMenuCanvas != null)
            startMenuCanvas.SetActive(true);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (menuCanvasGroup != null)
                menuCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            yield return null;
        }

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 1f;
            menuCanvasGroup.interactable = true;
            menuCanvasGroup.blocksRaycasts = true;
        }

        ShowMainMenu();
    }

    // ========================= FADE =========================

    private IEnumerator FadeOutCanvas()
    {
        if (menuCanvasGroup == null) yield break;

        menuCanvasGroup.interactable = false;
        menuCanvasGroup.blocksRaycasts = false;

        float startAlpha = menuCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            menuCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
            yield return null;
        }

        menuCanvasGroup.alpha = 0f;
    }

    // ========================= HELPERS =========================

    private void SetPanel(GameObject target)
    {
        if (loginPanel != null) loginPanel.SetActive(target == loginPanel);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(target == mainMenuPanel);
        if (optionsPanel != null) optionsPanel.SetActive(target == optionsPanel);
        if (assessmentPanel != null) assessmentPanel.SetActive(target == assessmentPanel);
    }
}
