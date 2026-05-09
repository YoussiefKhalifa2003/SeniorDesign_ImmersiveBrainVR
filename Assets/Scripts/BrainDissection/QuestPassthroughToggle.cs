using UnityEngine;

/// <summary>
/// Deprecated. The Quest passthrough toggle was removed from the project because
/// it was unreliable across Link/standalone builds. This stub remains so that any
/// scene asset still referencing the component does not break — the component
/// destroys itself silently on Awake.
/// </summary>
public class QuestPassthroughToggle : MonoBehaviour
{
    public BrainManager brainManager;
    public Camera targetCamera;

    void Awake()
    {
        Destroy(this);
    }
}
