using UnityEngine;

/// <summary>
/// Attach to BrainRoot. At the very start of Play mode, finds ALL Rigidbodies
/// in the brain hierarchy and makes them kinematic with no gravity.
/// This prevents the brain from falling.
/// </summary>
public class BrainPhysicsLock : MonoBehaviour
{
    private void Awake()
    {
        LockAllRigidbodies();
    }

    private void Start()
    {
        // Run again in Start in case anything was added between Awake and Start
        LockAllRigidbodies();
    }

    private void LockAllRigidbodies()
    {
        // Lock this object
        var myRb = GetComponent<Rigidbody>();
        if (myRb != null)
        {
            myRb.isKinematic = true;
            myRb.useGravity = false;
        }

        // Lock ALL children recursively
        var allRigidbodies = GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in allRigidbodies)
        {
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        Debug.Log($"[BrainPhysicsLock] Locked {allRigidbodies.Length} Rigidbodies as kinematic (no gravity).");
    }
}
