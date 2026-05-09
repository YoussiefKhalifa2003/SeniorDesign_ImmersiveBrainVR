using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small in-headset FPS readout (head-up display) that respects the per-user
/// FPS preference saved by OptionsController.
///
/// Uses a WorldSpace canvas parented to the active camera so the value
/// renders inside the VR headset (ScreenSpaceOverlay canvases never reach
/// the headset eye buffers — they only render to the desktop mirror).
/// The canvas re-parents itself if the active camera changes between scenes.
///
/// Spawned automatically at runtime via [RuntimeInitializeOnLoadMethod] so it
/// works in every scene without requiring editor setup.
/// </summary>
public class FpsCounterOverlay : MonoBehaviour
{
    static FpsCounterOverlay _instance;

    Canvas _canvas;
    Text _label;
    GameObject _root;
    Camera _attachedCamera;

    float _smoothedDt;
    const float Smoothing = 0.1f;

    // Position relative to the camera (in metres). Slight forward offset
    // keeps the readout in focus; up-right tucks it into the upper-right
    // corner of view so it doesn't compete with the central UI.
    static readonly Vector3 LocalOffset = new Vector3(0.18f, 0.10f, 0.6f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("FpsCounterOverlay");
        _instance = go.AddComponent<FpsCounterOverlay>();
        DontDestroyOnLoad(go);
    }

    void OnEnable()
    {
        OptionsController.OnFpsPreferenceChanged += SetVisible;
        BuildUiIfNeeded();
        SetVisible(OptionsController.IsFpsEnabled);
    }

    void OnDisable()
    {
        OptionsController.OnFpsPreferenceChanged -= SetVisible;
    }

    void BuildUiIfNeeded()
    {
        if (_root != null) return;

        _root = new GameObject("FpsCounterCanvas");
        // Initial parent is the controller GO; AttachToCamera() reparents
        // to the head camera as soon as Camera.main becomes available.
        _root.transform.SetParent(transform, false);

        _canvas = _root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 5000;
        _root.AddComponent<CanvasScaler>();
        _root.AddComponent<GraphicRaycaster>();

        var canvasRT = _root.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(180f, 50f);
        // 0.001 = the standard 1 unit / 1000 mapping used by the rest of
        // the world-space UI in this project (matches LeaderboardUI etc).
        canvasRT.localScale = Vector3.one * 0.001f;

        var bgGO = new GameObject("Bg");
        bgGO.transform.SetParent(_root.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.sizeDelta = new Vector2(180f, 50f);
        bgGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(bgGO.transform, false);
        var txtRect = txtGO.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(8f, 4f);
        txtRect.offsetMax = new Vector2(-8f, -4f);
        _label = txtGO.AddComponent<Text>();
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = new Color(0.9f, 1f, 0.6f);
        _label.fontSize = 22;
        _label.fontStyle = FontStyle.Bold;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _label.font = font;

        _root.SetActive(false);
    }

    /// <summary>
    /// Attach (or re-attach) the world-space canvas to the current head
    /// camera. Called every frame because the active camera can change
    /// across scene loads (start menu → play scene, etc) and Camera.main
    /// may be null in the very first frames.
    /// </summary>
    void AttachToCamera()
    {
        if (_root == null) return;
        var cam = Camera.main;
        if (cam == null || _attachedCamera == cam) return;

        _attachedCamera = cam;
        _root.transform.SetParent(cam.transform, false);
        _root.transform.localPosition = LocalOffset;
        _root.transform.localRotation = Quaternion.identity;
    }

    void SetVisible(bool visible)
    {
        BuildUiIfNeeded();
        if (_root != null) _root.SetActive(visible);
        if (visible) AttachToCamera();
    }

    void Update()
    {
        if (_root == null || !_root.activeSelf) return;

        // Keep the readout glued to the headset even if the camera changes.
        AttachToCamera();

        _smoothedDt = Mathf.Lerp(_smoothedDt, Time.unscaledDeltaTime, Smoothing);
        if (_smoothedDt <= 0f) return;

        float fps = 1f / _smoothedDt;
        float ms = _smoothedDt * 1000f;
        if (_label != null)
            _label.text = $"{fps:0.} FPS  ({ms:0.0} ms)";
    }
}
