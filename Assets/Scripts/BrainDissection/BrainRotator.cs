using UnityEngine;

/// <summary>
/// Placeholder component on BrainRoot. Rotation is now handled by BrainManager.RotateBrain()
/// called from the UI Rotate button. This component just holds the BrainManager reference
/// and keeps the brain frozen in place (no physics movement).
/// </summary>
public class BrainRotator : MonoBehaviour
{
    [Tooltip("Auto-found if not set")]
    public BrainManager brainManager;

    private void Awake()
    {
        if (brainManager == null)
            brainManager = FindFirstObjectByType<BrainManager>();

        // Ensure this object never moves due to physics
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }
}
