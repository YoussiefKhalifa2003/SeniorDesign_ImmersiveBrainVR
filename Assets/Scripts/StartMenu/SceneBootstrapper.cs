using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// When PlayScene or TutorialScene loads directly (from Main Menu),
/// runs the appropriate bootstrap sequence. Ignored in MainMenuScene.
/// Attach to StartMenuSystem in all lab scenes.
/// </summary>
public class SceneBootstrapper : MonoBehaviour
{
    [Header("Optional overrides (auto-found if null)")]
    public DoorController doorController;
    public MovementGate movementGate;
    public SessionTimer sessionTimer;
    public GameObject startMenuCanvas;
    public LabIntroduction labIntroduction;

    [Header("Settings")]
    public float doorWaitExtra = 0.2f;

    private void Start()
    {
        VRDisplayDiagnostics.EnsureExists();

        var sceneName = SceneManager.GetActiveScene().name;

        if (sceneName == SceneFlowManager.PlayScene)
            StartCoroutine(PlayBootstrap());
        else if (sceneName == SceneFlowManager.TutorialScene)
            StartCoroutine(TutorialBootstrap());
    }

    private IEnumerator PlayBootstrap()
    {
        if (startMenuCanvas == null)
            startMenuCanvas = GameObject.Find("StartMenuCanvas");
        if (startMenuCanvas != null)
            startMenuCanvas.SetActive(false);

        if (doorController == null)
            doorController = FindFirstObjectByType<DoorController>();
        if (doorController != null)
        {
            doorController.OpenDoors();
            yield return new WaitForSeconds(doorController.openDuration + doorWaitExtra);
        }

        if (movementGate == null)
            movementGate = FindFirstObjectByType<MovementGate>();
        if (movementGate != null)
            movementGate.EnableMovement();

        if (sessionTimer != null)
            sessionTimer.StartTimer();
        else
        {
            var timer = FindFirstObjectByType<SessionTimer>();
            if (timer != null) timer.StartTimer();
        }

        if (SessionLogger.Instance != null)
            SessionLogger.Instance.BeginSession(SessionData.UserName);

        if (TaskTimerManager.Instance != null)
            TaskTimerManager.Instance.BeginSession();

        if (labIntroduction == null)
            labIntroduction = FindFirstObjectByType<LabIntroduction>();
        if (labIntroduction != null)
            labIntroduction.ShowIntroduction();

        Debug.Log("[SceneBootstrapper] Play lab ready.");
    }

    private IEnumerator TutorialBootstrap()
    {
        if (startMenuCanvas == null)
            startMenuCanvas = GameObject.Find("StartMenuCanvas");
        if (startMenuCanvas != null)
            startMenuCanvas.SetActive(false);

        if (doorController == null)
            doorController = FindFirstObjectByType<DoorController>();
        if (doorController != null)
        {
            doorController.OpenDoors();
            yield return new WaitForSeconds(doorController.openDuration + doorWaitExtra);
        }

        if (movementGate == null)
            movementGate = FindFirstObjectByType<MovementGate>();
        if (movementGate != null)
            movementGate.EnableMovement();

        var tutorial = TutorialManager.Instance;
        if (tutorial == null)
        {
            var go = new GameObject("TutorialManager");
            tutorial = go.AddComponent<TutorialManager>();
        }
        tutorial.BeginTutorial();

        Debug.Log("[SceneBootstrapper] Tutorial started.");
    }
}
