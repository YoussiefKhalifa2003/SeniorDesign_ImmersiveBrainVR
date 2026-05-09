using System.Collections;
using UnityEngine;

/// <summary>
/// Runs when PlayScene loads (direct load from Main Menu).
/// Hides menu, opens doors, enables movement, starts session.
/// Attach to StartMenuSystem or a persistent object in PlayScene.
/// </summary>
public class PlaySceneBootstrapper : MonoBehaviour
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
        StartCoroutine(BootstrapSequence());
    }

    private IEnumerator BootstrapSequence()
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

        // Play mode is untimed. The visible HUD timer and per-task timer are
        // intentionally suppressed here so students can explore freely.
        // Timing is only shown and recorded during assessments.
        if (sessionTimer == null) sessionTimer = FindFirstObjectByType<SessionTimer>();
        if (sessionTimer != null && sessionTimer.hudPanel != null)
            sessionTimer.hudPanel.SetActive(false);

        if (SessionLogger.Instance != null)
            SessionLogger.Instance.BeginSession(SessionData.UserName);

        if (labIntroduction == null)
            labIntroduction = FindFirstObjectByType<LabIntroduction>();
        if (labIntroduction != null)
            labIntroduction.ShowIntroduction();

        Debug.Log("[PlaySceneBootstrapper] Lab ready.");
    }
}
