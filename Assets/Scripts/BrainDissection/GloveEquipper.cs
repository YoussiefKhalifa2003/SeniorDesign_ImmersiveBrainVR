using UnityEngine;

/// <summary>
/// Handles the "equip gloves" action -- purely logical.
///
/// The blue hand visuals come from XR Hands (hand tracking prefabs added to the scene).
/// They are ALWAYS visible when hand tracking is active, regardless of glove equip state.
///
/// This component just tracks whether the user has interacted with the glove model
/// on the tool table, which gates the lab start.
/// </summary>
public class GloveEquipper : MonoBehaviour
{
    [Header("Glove Model (not used for visuals -- just kept for reference)")]
    public GameObject glovePrefab;

    private bool _equipped;

    /// <summary>Mark gloves as equipped. No visual changes -- just state.</summary>
    public void EquipGloves()
    {
        if (_equipped) return;
        _equipped = true;
        Debug.Log("[GloveEquipper] Gloves equipped (logical gate activated).");
    }

    /// <summary>Clears the equipped flag so the player can equip gloves again
    /// after a lab reset (e.g. when entering Play after Tutorial).</summary>
    public void ResetEquipped()
    {
        if (!_equipped) return;
        _equipped = false;
        Debug.Log("[GloveEquipper] Glove equip state reset.");
    }
}
