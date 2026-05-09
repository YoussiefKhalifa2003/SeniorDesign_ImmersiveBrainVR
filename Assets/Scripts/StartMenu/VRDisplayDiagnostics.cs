using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// Lightweight runtime helper for VR readability:
/// 1. Shows a small FPS counter in-view.
/// 2. Increases the pixel density of world-space canvases and slightly
///    raises XR eye texture resolution so legacy UI text appears sharper.
/// 
/// This is intentionally isolated from gameplay logic.
/// </summary>
public class VRDisplayDiagnostics : MonoBehaviour
{
    static VRDisplayDiagnostics _instance;

    [Header("FPS")]
    public bool showFps = true;
    public Vector3 hudLocalPosition = new Vector3(-0.18f, 0.11f, 0.6f);
    public Vector3 hudLocalScale = Vector3.one * 0.00055f;
    public float fpsRefreshInterval = 0.25f;

    [Header("Sharpness")]
    [Range(1f, 2f)]
    public float eyeTextureResolutionScale = 1.2f;
    [Range(1f, 100f)]
    public float worldCanvasDynamicPixelsPerUnit = 30f;
    [Range(1f, 200f)]
    public float worldCanvasReferencePixelsPerUnit = 100f;
    public float canvasRescanInterval = 1f;

    Canvas _fpsCanvas;
    Text _fpsText;
    Transform _cam;
    float _fpsTimer;
    int _fpsFrames;
    float _fpsValue;
    float _scanTimer;

    public static void EnsureExists()
    {
        if (_instance != null) return;

        var go = new GameObject("VRDisplayDiagnostics");
        _instance = go.AddComponent<VRDisplayDiagnostics>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        ApplySharpnessSettings();
        UpgradeWorldSpaceCanvases();
        EnsureFpsHud();
    }

    void Update()
    {
        ApplySharpnessSettings();

        _scanTimer += Time.unscaledDeltaTime;
        if (_scanTimer >= canvasRescanInterval)
        {
            _scanTimer = 0f;
            UpgradeWorldSpaceCanvases();
            EnsureFpsHud();
        }

        if (_cam == null)
            _cam = Camera.main != null ? Camera.main.transform : null;

        if (_fpsCanvas != null && _cam != null && _fpsCanvas.transform.parent != _cam)
        {
            _fpsCanvas.transform.SetParent(_cam, false);
            _fpsCanvas.transform.localPosition = hudLocalPosition;
            _fpsCanvas.transform.localRotation = Quaternion.identity;
            _fpsCanvas.transform.localScale = hudLocalScale;
        }

        if (!showFps || _fpsText == null)
            return;

        _fpsFrames++;
        _fpsTimer += Time.unscaledDeltaTime;
        if (_fpsTimer >= fpsRefreshInterval)
        {
            _fpsValue = _fpsFrames / _fpsTimer;
            _fpsFrames = 0;
            _fpsTimer = 0f;

            Color color = _fpsValue >= 72f
                ? new Color(0.35f, 1f, 0.4f)
                : (_fpsValue >= 45f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.4f, 0.35f));

            _fpsText.color = color;
            _fpsText.text = $"FPS: {Mathf.RoundToInt(_fpsValue)}";
        }
    }

    void ApplySharpnessSettings()
    {
        if (XRSettings.enabled && XRSettings.eyeTextureResolutionScale < eyeTextureResolutionScale)
            XRSettings.eyeTextureResolutionScale = eyeTextureResolutionScale;
    }

    void UpgradeWorldSpaceCanvases()
    {
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                continue;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            if (scaler.dynamicPixelsPerUnit < worldCanvasDynamicPixelsPerUnit)
                scaler.dynamicPixelsPerUnit = worldCanvasDynamicPixelsPerUnit;

            if (scaler.referencePixelsPerUnit < worldCanvasReferencePixelsPerUnit)
                scaler.referencePixelsPerUnit = worldCanvasReferencePixelsPerUnit;
        }
    }

    void EnsureFpsHud()
    {
        if (!showFps)
        {
            if (_fpsCanvas != null)
                _fpsCanvas.gameObject.SetActive(false);
            return;
        }

        if (_cam == null)
            _cam = Camera.main != null ? Camera.main.transform : null;

        if (_cam == null)
            return;

        if (_fpsCanvas == null)
        {
            var go = new GameObject("VR_FPS_HUD");
            go.transform.SetParent(_cam, false);
            go.transform.localPosition = hudLocalPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = hudLocalScale;

            _fpsCanvas = go.AddComponent<Canvas>();
            _fpsCanvas.renderMode = RenderMode.WorldSpace;
            _fpsCanvas.sortingOrder = 500;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = worldCanvasDynamicPixelsPerUnit;
            scaler.referencePixelsPerUnit = worldCanvasReferencePixelsPerUnit;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(240f, 60f);

            var textGO = new GameObject("FPS_Text");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            _fpsText = textGO.AddComponent<Text>();
            _fpsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_fpsText.font == null)
                _fpsText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _fpsText.fontSize = 34;
            _fpsText.fontStyle = FontStyle.Bold;
            _fpsText.alignment = TextAnchor.MiddleLeft;
            _fpsText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _fpsText.verticalOverflow = VerticalWrapMode.Overflow;
            _fpsText.text = "FPS: --";
            _fpsText.color = Color.white;
        }
        else
        {
            _fpsCanvas.gameObject.SetActive(true);
        }
    }
}
