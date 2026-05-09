using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// Roblox-style hold-to-wash interaction. When the player is near the soap
/// bottle (or sink), a small world-space prompt appears and a progress meter
/// fills while either trigger is held. Once full, hands are marked as washed
/// in LabToolManager so the gloves can be equipped.
///
/// Targets are resolved by GameObject name so they can live anywhere in the
/// scene: sm_handsoap.001 (primary), sm_sink.001 (fallback). An optional
/// running-water clip plays while the player holds.
/// </summary>
public class HandWashStation : MonoBehaviour
{
    public static HandWashStation Instance { get; private set; }

    [Header("Scene Targets (auto-found by name if left null)")]
    public Transform soapTarget;
    public Transform sinkTarget;

    [Header("Interaction")]
    [Tooltip("How close (in metres) the headset must be to the target before the prompt appears.")]
    public float interactionRadius = 1.6f;
    [Tooltip("Seconds the trigger must be held to complete the wash.")]
    public float holdSeconds = 2.0f;
    [Tooltip("How far (in metres) the wash prompt floats in front of the player. Smaller numbers keep the panel close to the player so it never goes through walls or fixtures behind the sink.")]
    public float promptDistance = 0.65f;

    [Header("Audio (optional)")]
    public AudioClip waterLoop;
    [Range(0f, 1f)] public float waterVolume = 0.55f;

    static readonly Color PanelBg = new Color(0.05f, 0.07f, 0.10f, 0.90f);
    static readonly Color FillColor = new Color(0.30f, 0.78f, 0.95f, 1f);
    static readonly Color TextWhite = new Color(0.96f, 0.97f, 1f, 1f);

    GameObject _panelRoot;
    Text _label;
    Image _fillImage;
    AudioSource _audio;

    bool _completed;
    bool _holding;
    float _heldFor;

    readonly List<InputDevice> _deviceBuffer = new List<InputDevice>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ResolveTargets();
        BuildPromptUi();
        BuildAudio();
        SubscribeToReset();
        UpdatePromptVisible(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnsubscribeFromReset();
    }

    void SubscribeToReset()
    {
        var mgr = LabToolManager.Instance;
        if (mgr != null) mgr.OnLabReset += OnLabReset;
    }

    void UnsubscribeFromReset()
    {
        var mgr = LabToolManager.Instance;
        if (mgr != null) mgr.OnLabReset -= OnLabReset;
    }

    void OnLabReset()
    {
        _completed = false;
        _holding = false;
        _heldFor = 0f;
        UpdatePromptVisible(false);
        StopWaterAudio();
    }

    void ResolveTargets()
    {
        if (soapTarget == null) soapTarget = FindByName("sm_handsoap.001", "sm_handsoap");
        if (sinkTarget == null) sinkTarget = FindByName("sm_sink.001", "sm_sink");
    }

    static Transform FindByName(params string[] names)
    {
        foreach (var name in names)
        {
            var go = GameObject.Find(name);
            if (go != null) return go.transform;
        }
        return null;
    }

    void BuildPromptUi()
    {
        if (_panelRoot != null) return;

        _panelRoot = new GameObject("HandWashPrompt");
        _panelRoot.transform.SetParent(transform, false);

        var canvas = _panelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _panelRoot.AddComponent<CanvasScaler>();

        var rt = _panelRoot.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(360f, 110f);
        rt.localScale = Vector3.one * 0.0011f;

        var bg = MakeImage("Bg", _panelRoot.transform, Vector2.zero, new Vector2(360f, 110f), PanelBg);
        Stretch(bg.GetComponent<RectTransform>());

        var titleGO = MakeText("Title", _panelRoot.transform, new Vector2(0f, 28f), new Vector2(330f, 30f),
            "Wash Hands", 22, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter);
        _label = titleGO;

        // Progress bar background
        var barBg = MakeImage("BarBg", _panelRoot.transform, new Vector2(0f, -18f), new Vector2(300f, 18f),
            new Color(0.2f, 0.2f, 0.25f, 1f));

        // Fill
        var fillGO = new GameObject("BarFill");
        fillGO.transform.SetParent(barBg.transform, false);
        var fillRt = fillGO.AddComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.anchoredPosition = Vector2.zero;
        fillRt.sizeDelta = new Vector2(0f, 0f);
        _fillImage = fillGO.AddComponent<Image>();
        _fillImage.color = FillColor;

        var hintGO = MakeText("Hint", _panelRoot.transform, new Vector2(0f, -45f), new Vector2(330f, 22f),
            "Hold TRIGGER to wash", 14, FontStyle.Italic, new Color(0.78f, 0.84f, 0.92f), TextAnchor.MiddleCenter);
        hintGO.gameObject.name = "Hint";

        _panelRoot.SetActive(false);
    }

    void BuildAudio()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 0f;
        _audio.loop = true;
        _audio.playOnAwake = false;
        _audio.volume = waterVolume;
        if (waterLoop != null) _audio.clip = waterLoop;
    }

    void Update()
    {
        if (_completed)
        {
            UpdatePromptVisible(false);
            return;
        }

        Transform anchor = soapTarget != null ? soapTarget : sinkTarget;
        if (anchor == null)
        {
            UpdatePromptVisible(false);
            return;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            UpdatePromptVisible(false);
            return;
        }

        float dist = Vector3.Distance(cam.transform.position, anchor.position);
        bool inRange = dist <= interactionRadius;

        UpdatePromptVisible(inRange);
        if (!inRange)
        {
            _holding = false;
            _heldFor = 0f;
            UpdateFill();
            StopWaterAudio();
            return;
        }

        PositionPromptAbove(anchor);

        bool triggerHeld = AnyTriggerHeld();
        if (triggerHeld)
        {
            if (!_holding) StartWaterAudio();
            _holding = true;
            _heldFor += Time.deltaTime;
            if (_heldFor >= holdSeconds) CompleteWash();
        }
        else
        {
            if (_holding) StopWaterAudio();
            _holding = false;
            _heldFor = Mathf.Max(0f, _heldFor - Time.deltaTime * 0.6f);
        }
        UpdateFill();
    }

    void UpdateFill()
    {
        if (_fillImage == null) return;
        float pct = Mathf.Clamp01(_heldFor / Mathf.Max(0.01f, holdSeconds));
        var rt = _fillImage.rectTransform;
        rt.sizeDelta = new Vector2(300f * pct, 0f);
        if (_label != null)
            _label.text = pct >= 0.999f ? "Hands Clean" : "Wash Hands";
    }

    void CompleteWash()
    {
        _completed = true;
        _holding = false;
        _heldFor = holdSeconds;
        StopWaterAudio();

        var mgr = LabToolManager.Instance;
        if (mgr != null) mgr.NotifyHandsWashed();

        UpdatePromptVisible(false);
    }

    void PositionPromptAbove(Transform anchor)
    {
        if (_panelRoot == null) return;
        var cam = Camera.main;
        if (cam == null) return;

        // Place the prompt a fixed distance in front of the camera, biased
        // toward the soap/sink anchor so it sits between the player and the
        // station. This keeps the panel away from the walls behind the sink
        // even when the room is tight, and it also never sits inside the
        // anchor's mesh (the soap pump/sink rim).
        Vector3 camPos = cam.transform.position;
        Vector3 toAnchor = anchor.position - camPos;
        float anchorDist = toAnchor.magnitude;
        Vector3 dir = anchorDist > 0.001f ? toAnchor / anchorDist : cam.transform.forward;

        // Stand the panel roughly 60-70 cm in front of the camera, but never
        // past the anchor. Add a small upward offset so it floats above hands.
        float forwardDist = Mathf.Min(promptDistance, Mathf.Max(0.25f, anchorDist - 0.20f));
        Vector3 pos = camPos + dir * forwardDist + Vector3.up * 0.05f;

        // If something solid is between the player and the chosen position
        // (e.g. a wall partition), pull the panel just inside that obstacle
        // so the panel never renders behind geometry from the player's POV.
        if (Physics.Raycast(camPos, dir, out var hit, forwardDist,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            // Only treat the hit as a wall if it is NOT the anchor itself.
            if (hit.transform != anchor && !hit.transform.IsChildOf(anchor))
            {
                float safeDist = Mathf.Max(0.25f, hit.distance - 0.10f);
                pos = camPos + dir * safeDist + Vector3.up * 0.05f;
            }
        }

        _panelRoot.transform.position = pos;
        _panelRoot.transform.rotation = Quaternion.LookRotation(pos - camPos);
    }

    void UpdatePromptVisible(bool visible)
    {
        if (_panelRoot != null && _panelRoot.activeSelf != visible)
            _panelRoot.SetActive(visible);
    }

    void StartWaterAudio()
    {
        if (_audio == null || _audio.clip == null || _audio.isPlaying) return;
        _audio.Play();
    }

    void StopWaterAudio()
    {
        if (_audio == null || !_audio.isPlaying) return;
        _audio.Stop();
    }

    bool AnyTriggerHeld()
    {
        if (TriggerHeldOn(XRNode.RightHand)) return true;
        if (TriggerHeldOn(XRNode.LeftHand)) return true;
        return false;
    }

    bool TriggerHeldOn(XRNode node)
    {
        _deviceBuffer.Clear();
        InputDevices.GetDevicesAtXRNode(node, _deviceBuffer);
        foreach (var d in _deviceBuffer)
        {
            if (!d.isValid) continue;
            if (d.TryGetFeatureValue(CommonUsages.trigger, out float t) && t >= 0.5f)
                return true;
            if (d.TryGetFeatureValue(CommonUsages.triggerButton, out bool b) && b)
                return true;
        }
        return false;
    }

    GameObject MakeImage(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    Text MakeText(string name, Transform parent, Vector2 pos, Vector2 size,
        string content, int fontSize, FontStyle style, Color color, TextAnchor align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = go.AddComponent<Text>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.font = font;
        t.text = content;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
