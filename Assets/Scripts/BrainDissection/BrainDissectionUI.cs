using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bridge for UI buttons and slider to call BrainManager / LabToolManager methods.
/// Wired by the Editor setup script using persistent listeners.
///
/// All actions are gated through LabToolManager state checks.
/// Includes cooldown to prevent VR ray from firing buttons every frame.
///
/// The End Session button is created dynamically at runtime when in Play mode.
/// </summary>
public class BrainDissectionUI : MonoBehaviour
{
    public BrainManager brainManager;

    private float _buttonCooldown = 0.4f;
    private float _lastButtonTime = -10f;
    private GameObject _endSessionBtn;
    private bool _endSessionCreated;

    void LateUpdate()
    {
        if (_endSessionCreated) return;
        if (!SessionData.IsPlayMode) return;
        EnsureSingleEndSessionButton();
        _endSessionCreated = true;
    }

    void EnsureSingleEndSessionButton()
    {
        var uiCtrl = GetComponent<RegionUIController>();
        Transform parent = (uiCtrl != null && uiCtrl.mainButtonPanel != null)
            ? uiCtrl.mainButtonPanel.transform
            : transform;

        // Destroy ALL existing End Session buttons to prevent duplicates
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (child.name.Contains("EndSession"))
                Destroy(child.gameObject);
        }
        // Also check canvas root
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name.Contains("EndSession"))
                Destroy(child.gameObject);
        }

        var go = new GameObject("Btn_EndSession_Runtime");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(190, 46);
        rt.anchoredPosition = new Vector2(-10, -8);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.60f, 0.12f, 0.12f, 1f);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = img.color;
        colors.highlightedColor = new Color(0.75f, 0.18f, 0.18f, 1f);
        colors.pressedColor = new Color(0.45f, 0.08f, 0.08f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(OnEndSessionClicked);

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var txt = txtGO.AddComponent<Text>();
        txt.text = "End Session";
        txt.fontSize = 18;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(0.95f, 0.95f, 0.97f, 1f);
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        _endSessionBtn = go;
        Debug.Log("[BrainDissectionUI] Single End Session button created (top-right of panel).");
    }

    private bool CanPress()
    {
        if (Time.time - _lastButtonTime < _buttonCooldown) return false;
        _lastButtonTime = Time.time;
        return true;
    }

    // ---- Hemisphere Viewing (requires brain to be split) ----

    public void OnLeftClicked()
    {
        if (!CanPress()) return;
        if (!RequireGloves()) return;
        if (brainManager != null) brainManager.ShowLeftHemisphere();
    }

    public void OnRightClicked()
    {
        if (!CanPress()) return;
        if (!RequireGloves()) return;
        if (brainManager != null) brainManager.ShowRightHemisphere();
    }

    public void OnShowWholeClicked()
    {
        if (!CanPress()) return;
        if (!RequireGloves()) return;
        if (brainManager != null) brainManager.ShowWholeBrain();
    }

    // ---- Put Back Region (returns region to hemisphere, does NOT reset) ----

    public void OnPutBackClicked()
    {
        if (!CanPress()) return;
        if (brainManager != null) brainManager.PutBackRegion();
    }

    // ---- Reset (goes through LabToolManager to also reset split state) ----

    public void OnResetClicked()
    {
        if (!CanPress()) return;
        if (LabToolManager.Instance != null)
            LabToolManager.Instance.ResetLab();
        else if (brainManager != null)
            brainManager.ResetBrain();
    }

    // ---- Rotate / Zoom (requires gloves) ----

    public void OnRotateClicked()
    {
        if (!CanPress()) return;
        if (!RequireGloves()) return;
        if (brainManager != null) brainManager.RotateBrain();
    }

    public void OnZoomInClicked()
    {
        if (!CanPress()) return;
        if (!RequireGloves()) return;
        if (brainManager != null) brainManager.ZoomIn();
    }

    public void OnZoomOutClicked()
    {
        if (!CanPress()) return;
        if (!RequireGloves()) return;
        if (brainManager != null) brainManager.ZoomOut();
    }

    // ---- Opacity (requires gloves, NO cooldown -- slider is continuous) ----

    public void OnOpacityChanged(float value)
    {
        if (!RequireGloves()) return;
        if (brainManager != null) brainManager.SetBrainOpacity(value);
    }

    // ---- End Session (Play mode only) ----

    public void OnEndSessionClicked()
    {
        if (!CanPress()) return;
        var mm = FindFirstObjectByType<MenuManager>();
        if (mm != null)
            mm.OnEndSessionPressed();
    }

    // ---- Helper ----

    private bool RequireGloves()
    {
        if (LabToolManager.Instance == null) return true;
        return LabToolManager.Instance.glovesEquipped;
    }
}
