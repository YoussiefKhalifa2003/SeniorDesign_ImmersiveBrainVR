using System.Collections;
using UnityEngine;

/// <summary>
/// Runs when TutorialScene loads (direct load from Main Menu).
/// Hides menu, opens doors, enables movement, starts TutorialManager.
/// Attach to StartMenuSystem or a persistent object in TutorialScene.
/// </summary>
public class TutorialSceneBootstrapper : MonoBehaviour
{
    [Header("Optional overrides (auto-found if null)")]
    public DoorController doorController;
    public MovementGate movementGate;
    public GameObject startMenuCanvas;

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

        var tutorial = TutorialManager.Instance;
        if (tutorial == null)
        {
            var go = new GameObject("TutorialManager");
            tutorial = go.AddComponent<TutorialManager>();
        }
        tutorial.BeginTutorial();

        Debug.Log("[TutorialSceneBootstrapper] Tutorial started.");
    }
}
