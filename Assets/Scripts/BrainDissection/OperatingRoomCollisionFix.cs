using UnityEngine;

/// <summary>
/// Strips all colliders and rigidbodies from the operating room model.
/// The imported FBX has mesh colliders on every piece of geometry which
/// conflict with the XR rig's CharacterController and push the player
/// around or launch them into the air.  Removing them lets the player
/// walk freely.  The scene's own Plane object acts as the floor.
/// </summary>
public class OperatingRoomCollisionFix : MonoBehaviour
{
    private void Awake()
    {
        int removed = 0;

        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            if (col != null) { DestroyImmediate(col); removed++; }
        }

        foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb != null) DestroyImmediate(rb);
        }

        Debug.Log($"[RoomCollisionFix] Removed {removed} colliders from operating room.");
    }
}
