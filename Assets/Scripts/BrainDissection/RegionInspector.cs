using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Slow auto-rotation of a selected brain region.
/// The region stays EXACTLY in its current position -- no drifting.
///
/// Method: record visual center, rotate, snap center back.
/// Same approach as BrainManager.RotateBrain but continuous.
///
/// Pauses when user holds trigger (so they can inspect it still).
/// </summary>
public class RegionInspector : MonoBehaviour
{
    [Header("Auto Rotation")]
    [Tooltip("Degrees per second for the slow showcase spin")]
    public float autoRotateSpeed = 15f;

    private Vector3 _lockedCenter;
    private bool _active;

    /// <summary>Called by BrainManager when a region is selected for inspection.</summary>
    public void StartInspecting()
    {
        // Cache the region's visual center
        var rend = GetComponent<Renderer>();
        _lockedCenter = (rend != null && rend.enabled)
            ? rend.bounds.center
            : transform.position;
        _active = true;
    }

    private void Update()
    {
        if (!_active) return;
        if (IsTriggerHeld()) return;

        // 1. Record center before rotation
        Vector3 centerBefore = GetCurrentCenter();

        // 2. Rotate around world Y axis
        transform.Rotate(Vector3.up, autoRotateSpeed * Time.deltaTime, Space.World);

        // 3. Compute where center ended up
        Vector3 centerAfter = GetCurrentCenter();

        // 4. Snap center back so region doesn't drift
        transform.position += (centerBefore - centerAfter);
    }

    private Vector3 GetCurrentCenter()
    {
        var rend = GetComponent<Renderer>();
        return (rend != null && rend.enabled) ? rend.bounds.center : transform.position;
    }

    private bool IsTriggerHeld()
    {
        var rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHand.isValid &&
            rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool rT) && rT)
            return true;

        var leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHand.isValid &&
            leftHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool lT) && lT)
            return true;

        return false;
    }
}
