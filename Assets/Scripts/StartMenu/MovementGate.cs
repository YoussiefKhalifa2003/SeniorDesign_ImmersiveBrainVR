using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

/// <summary>
/// Disables ALL locomotion providers on Awake (preventing the "pushed backward on load" bug)
/// and exposes EnableMovement() to re-enable them after the Play button is pressed and doors open.
///
/// Attach to the same GameObject as the XR Origin, or any persistent object in the scene.
/// The editor setup script will place it on the StartMenuSystem object.
/// </summary>
public class MovementGate : MonoBehaviour
{
    private LocomotionProvider[] _providers;
    private bool _movementEnabled;

    private void Awake()
    {
        // Find ALL locomotion providers in the scene (move, turn, teleport, climb, etc.)
        _providers = FindObjectsByType<LocomotionProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        // Disable them all immediately so no movement happens before Play
        foreach (var provider in _providers)
        {
            if (provider != null)
                provider.enabled = false;
        }

        _movementEnabled = false;
        Debug.Log($"[MovementGate] Disabled {_providers.Length} locomotion provider(s) on startup.");
    }

    /// <summary>
    /// Called by MenuManager after doors finish opening.
    /// Re-enables all locomotion providers so the user can move.
    /// </summary>
    public void EnableMovement()
    {
        _providers = FindObjectsByType<LocomotionProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var provider in _providers)
        {
            if (provider != null)
                provider.enabled = true;
        }

        _movementEnabled = true;
        Debug.Log($"[MovementGate] Movement enabled. {_providers.Length} locomotion provider(s) active.");
    }

    /// <summary>
    /// Called by ResetLab or returning to login to disable movement again.
    /// </summary>
    public void DisableMovement()
    {
        _providers = FindObjectsByType<LocomotionProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var provider in _providers)
        {
            if (provider != null)
                provider.enabled = false;
        }

        _movementEnabled = false;
        Debug.Log("[MovementGate] Movement disabled.");
    }
}
