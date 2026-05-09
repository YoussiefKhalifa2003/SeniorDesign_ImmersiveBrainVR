using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

/// <summary>
/// Centralized haptic feedback for VR controllers.
/// Call static methods from anywhere (BrainRegion, BrainCutZone, LabTool, etc.)
/// to pulse the correct controller. Works with OpenXR on any headset.
/// </summary>
public static class HapticFeedback
{
    public static void LightPulse(Transform interactor)
    {
        SendHaptic(interactor, 0.15f, 0.08f);
    }

    public static void MediumPulse(Transform interactor)
    {
        SendHaptic(interactor, 0.4f, 0.12f);
    }

    public static void StrongPulse(Transform interactor)
    {
        SendHaptic(interactor, 0.8f, 0.2f);
    }

    public static void CutFeedback(Transform interactor)
    {
        SendHaptic(interactor, 0.6f, 0.35f);
    }

    public static void PulseLeft(float amplitude, float duration)
    {
        SendToNode(XRNode.LeftHand, amplitude, duration);
    }

    public static void PulseRight(float amplitude, float duration)
    {
        SendToNode(XRNode.RightHand, amplitude, duration);
    }

    public static void PulseBoth(float amplitude, float duration)
    {
        SendToNode(XRNode.LeftHand, amplitude, duration);
        SendToNode(XRNode.RightHand, amplitude, duration);
    }

    static void SendHaptic(Transform interactor, float amplitude, float duration)
    {
        if (interactor == null) return;

        string name = interactor.name.ToLowerInvariant();
        bool left = name.Contains("left");
        bool right = name.Contains("right");

        if (!left && !right)
        {
            Transform parent = interactor.parent;
            while (parent != null)
            {
                string pn = parent.name.ToLowerInvariant();
                if (pn.Contains("left")) { left = true; break; }
                if (pn.Contains("right")) { right = true; break; }
                parent = parent.parent;
            }
        }

        if (left) SendToNode(XRNode.LeftHand, amplitude, duration);
        else if (right) SendToNode(XRNode.RightHand, amplitude, duration);
        else
        {
            SendToNode(XRNode.LeftHand, amplitude, duration);
            SendToNode(XRNode.RightHand, amplitude, duration);
        }
    }

    static void SendToNode(XRNode node, float amplitude, float duration)
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(node, devices);
        foreach (var device in devices)
        {
            if (device.isValid)
                device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), duration);
        }
    }
}
