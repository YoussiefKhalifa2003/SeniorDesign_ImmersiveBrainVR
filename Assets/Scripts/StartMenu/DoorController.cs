using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Finds LeftDoor and RightDoor children under the operating_room model
/// and animates them open (smooth rotation) when OpenDoors() is called.
///
/// LeftDoor rotates -90 degrees around Y, RightDoor rotates +90 degrees around Y.
/// Duration is configurable. Fires OnDoorsOpened when complete.
/// </summary>
public class DoorController : MonoBehaviour
{
    [Header("Door References (auto-found if null)")]
    public Transform leftDoor;
    public Transform rightDoor;

    [Header("Animation Settings")]
    [Tooltip("How long the door opening animation takes in seconds.")]
    public float openDuration = 2.0f;

    [Tooltip("Degrees to rotate each door (LeftDoor = negative, RightDoor = positive around Y).")]
    public float openAngle = 90f;

    public event Action OnDoorsOpened;

    private bool _doorsOpened;
    private Quaternion _leftClosed;
    private Quaternion _rightClosed;
    private bool _closedSaved;

    private void Start()
    {
        if (leftDoor == null || rightDoor == null)
            FindDoors();

        if (leftDoor != null) _leftClosed = leftDoor.localRotation;
        if (rightDoor != null) _rightClosed = rightDoor.localRotation;
        _closedSaved = true;
    }

    private void FindDoors()
    {
        var opRoom = GameObject.Find("operating_room");
        if (opRoom == null)
        {
            Debug.LogWarning("[DoorController] operating_room not found in scene.");
            return;
        }

        foreach (var t in opRoom.GetComponentsInChildren<Transform>(true))
        {
            string name = t.name.ToLower();
            if (leftDoor == null && name.Contains("leftdoor"))
                leftDoor = t;
            if (rightDoor == null && name.Contains("rightdoor"))
                rightDoor = t;
        }

        if (leftDoor != null) Debug.Log($"[DoorController] Found LeftDoor: {leftDoor.name}");
        else Debug.LogWarning("[DoorController] LeftDoor not found under operating_room.");

        if (rightDoor != null) Debug.Log($"[DoorController] Found RightDoor: {rightDoor.name}");
        else Debug.LogWarning("[DoorController] RightDoor not found under operating_room.");
    }

    /// <summary>
    /// Begins the door opening animation. Called by MenuManager when Play is pressed.
    /// </summary>
    public void OpenDoors()
    {
        if (_doorsOpened) return;
        _doorsOpened = true;

        if (leftDoor == null || rightDoor == null)
            FindDoors();

        StartCoroutine(AnimateDoors());
    }

    private IEnumerator AnimateDoors()
    {
        Quaternion leftStart = leftDoor != null ? leftDoor.localRotation : Quaternion.identity;
        Quaternion rightStart = rightDoor != null ? rightDoor.localRotation : Quaternion.identity;

        // Target rotations: rotate around local Y axis
        Quaternion leftTarget = leftStart * Quaternion.Euler(0f, -openAngle, 0f);
        Quaternion rightTarget = rightStart * Quaternion.Euler(0f, openAngle, 0f);

        float elapsed = 0f;

        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / openDuration);

            if (leftDoor != null)
                leftDoor.localRotation = Quaternion.Slerp(leftStart, leftTarget, t);
            if (rightDoor != null)
                rightDoor.localRotation = Quaternion.Slerp(rightStart, rightTarget, t);

            yield return null;
        }

        // Snap to exact final rotation
        if (leftDoor != null) leftDoor.localRotation = leftTarget;
        if (rightDoor != null) rightDoor.localRotation = rightTarget;

        Debug.Log("[DoorController] Doors fully opened.");
        OnDoorsOpened?.Invoke();
    }

    /// <summary>
    /// Closes doors back to their original rotation. Called on End Session.
    /// </summary>
    public void CloseDoors()
    {
        if (!_doorsOpened) return;
        _doorsOpened = false;
        StartCoroutine(AnimateClose());
    }

    private IEnumerator AnimateClose()
    {
        Quaternion leftStart = leftDoor != null ? leftDoor.localRotation : Quaternion.identity;
        Quaternion rightStart = rightDoor != null ? rightDoor.localRotation : Quaternion.identity;

        Quaternion leftTarget = _closedSaved && leftDoor != null ? _leftClosed : leftStart;
        Quaternion rightTarget = _closedSaved && rightDoor != null ? _rightClosed : rightStart;

        float elapsed = 0f;
        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / openDuration);
            if (leftDoor != null)
                leftDoor.localRotation = Quaternion.Slerp(leftStart, leftTarget, t);
            if (rightDoor != null)
                rightDoor.localRotation = Quaternion.Slerp(rightStart, rightTarget, t);
            yield return null;
        }

        if (leftDoor != null) leftDoor.localRotation = leftTarget;
        if (rightDoor != null) rightDoor.localRotation = rightTarget;
        Debug.Log("[DoorController] Doors closed.");
    }
}
