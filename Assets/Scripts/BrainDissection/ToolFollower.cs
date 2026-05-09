using UnityEngine;


public class ToolFollower : MonoBehaviour
{
    [HideInInspector] public Transform targetController;
    [HideInInspector] public Vector3 positionOffset = new Vector3(0f, -0.05f, 0.08f);
    [HideInInspector] public Vector3 rotationOffset = new Vector3(-45f, 0f, 0f);

    private void LateUpdate()
    {
        if (targetController == null) return;
        transform.position = targetController.position +
            targetController.TransformDirection(positionOffset);
        transform.rotation = targetController.rotation *
            Quaternion.Euler(rotationOffset);
    }
}
