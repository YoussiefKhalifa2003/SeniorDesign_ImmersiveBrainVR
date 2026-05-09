using UnityEngine;


[RequireComponent(typeof(BoxCollider))]
public class BrainCutZone : MonoBehaviour
{
    [Header("Cut Settings")]
    [Tooltip("Fraction of the zone height the knife must travel (0-1). 0.4 = 40%.")]
    public float requiredCutFraction = 0.4f;

    [Header("Visual Guide")]
    [Tooltip("The red line showing where to cut (disabled after split)")]
    public LineRenderer cutGuide;

    // State
    private bool _knifeInZone;
    private bool _hasBeenCut;
    private float _knifeMinY;
    private float _knifeMaxY;
    private float _zoneHeight;

    private void Start()
    {
        var bc = GetComponent<BoxCollider>();
        if (bc != null)
        {
            float scaleX = Mathf.Max(Mathf.Abs(transform.lossyScale.x), 0.001f);
            float worldX = bc.size.x * scaleX;
            if (worldX < 0.10f)
            {
                bc.size = new Vector3(0.10f / scaleX, bc.size.y, bc.size.z);
                Debug.Log($"[BrainCutZone] Widened collider X to ~10cm (local {bc.size.x:F4})");
            }

            _zoneHeight = bc.size.y * Mathf.Abs(transform.lossyScale.y);
        }
        if (_zoneHeight < 0.001f) _zoneHeight = 0.3f;

        Debug.Log($"[BrainCutZone] Zone height={_zoneHeight:F4}, requiredFraction={requiredCutFraction}");
    }


    private void OnTriggerEnter(Collider other)
    {
        if (_hasBeenCut) return;
        if (!IsKnife(other)) return;
        if (LabToolManager.Instance != null && !LabToolManager.Instance.glovesEquipped) return;

        _knifeInZone = true;
        float y = other.transform.position.y;
        _knifeMinY = y;
        _knifeMaxY = y;
        HapticFeedback.MediumPulse(other.transform);
        Debug.Log("[BrainCutZone] Knife entered the cut zone. Run it through the full line!");
    }

    private void OnTriggerStay(Collider other)
    {
        if (_hasBeenCut || !_knifeInZone) return;
        if (!IsKnife(other)) return;

        float y = other.transform.position.y;
        if (y < _knifeMinY) _knifeMinY = y;
        if (y > _knifeMaxY) _knifeMaxY = y;

        float travel = _knifeMaxY - _knifeMinY;
        float requiredTravel = _zoneHeight * requiredCutFraction;

        if (travel >= requiredTravel)
        {
            PerformCut();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsKnife(other)) return;
        _knifeInZone = false;
        _knifeMinY = 0f;
        _knifeMaxY = 0f;
    }

    // ========================= CUT LOGIC =========================

    private void PerformCut()
    {
        _hasBeenCut = true;
        _knifeInZone = false;

        HapticFeedback.PulseBoth(0.9f, 0.4f);

        if (cutGuide != null) cutGuide.enabled = false;

        if (LabToolManager.Instance != null)
            LabToolManager.Instance.NotifyBrainSplit();

        Debug.Log("[BrainCutZone] Brain has been cut!");
    }

    public void ResetCutZone()
    {
        _hasBeenCut = false;
        _knifeInZone = false;
        _knifeMinY = 0f;
        _knifeMaxY = 0f;
        if (cutGuide != null) cutGuide.enabled = true;
    }

    // ========================= HELPERS =========================

    private bool IsKnife(Collider col)
    {
        var tool = col.GetComponentInParent<LabTool>();
        return tool != null && tool.toolType == LabTool.ToolType.Knife;
    }
}
