using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Connects the Brightness slider and FPS counter toggle to the lab.
/// Brightness drives both scene lights AND URP post-processing exposure.
/// FPS toggle controls a small on-screen frame-rate readout via FpsCounterOverlay.
/// Per-user: keys are prefixed with the logged-in username so each student
/// gets their own saved preferences.
/// </summary>
public class OptionsController : MonoBehaviour
{
    [Header("UI (wired by editor setup)")]
    public Slider brightnessSlider;
    public Toggle fpsToggle;

    /// <summary>
    /// Fired whenever the FPS preference changes (or is loaded for a user).
    /// FpsCounterOverlay subscribes to this to show/hide itself.
    /// </summary>
    public static event System.Action<bool> OnFpsPreferenceChanged;

    ColorAdjustments _colorAdjustments;
    bool _hardwareReady;

    struct LightRecord { public Light light; public float baseIntensity; }
    List<LightRecord> _sceneLights = new List<LightRecord>();

    static string UserPrefix => string.IsNullOrEmpty(SessionData.UserName) ? "" : SessionData.UserName;
    static string BrightnessKey => $"BD_{UserPrefix}_Bright";
    static string FpsKey => $"BD_{UserPrefix}_FPS";

    /// <summary>True when the FPS overlay should be visible for the current user.</summary>
    public static bool IsFpsEnabled
    {
        get
        {
            string user = string.IsNullOrEmpty(SessionData.UserName) ? "" : SessionData.UserName;
            return PlayerPrefs.GetInt($"BD_{user}_FPS", 0) == 1;
        }
    }

    void Start()
    {
        InitHardware();
        ApplyUserSettings();
    }

    void OnEnable()
    {
        if (!_hardwareReady)
        {
            InitHardware();
            ApplyUserSettings();
        }
    }

    void InitHardware()
    {
        if (_hardwareReady) return;

        CacheSceneLights();
        EnablePostProcessingOnAllCameras();
        FindOrCreateColorAdjustments();

        if (brightnessSlider != null)
        {
            brightnessSlider.minValue = -2f;
            brightnessSlider.maxValue = 2f;
            brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        }

        if (fpsToggle != null)
        {
            fpsToggle.onValueChanged.AddListener(OnFpsToggleChanged);
        }

        _hardwareReady = true;
        Debug.Log($"[OptionsController] Hardware ready. {_sceneLights.Count} lights cached.");
    }

    /// <summary>
    /// Reads saved preferences for the current SessionData.UserName
    /// and applies them to lights, post-processing, sliders, and FPS overlay.
    /// Call after login or when switching users.
    /// </summary>
    public void ReloadForCurrentUser()
    {
        if (!_hardwareReady) InitHardware();
        ApplyUserSettings();
        Debug.Log($"[OptionsController] Reloaded settings for '{UserPrefix}'.");
    }

    /// <summary>
    /// Resets all scene lights to their true base intensities (brightness multiplier = 1.0),
    /// removing per-user scaling so dark-room modes start from a known baseline.
    /// </summary>
    public void ApplyNeutralLighting()
    {
        if (!_hardwareReady) InitHardware();
        foreach (var rec in _sceneLights)
            if (rec.light != null) rec.light.intensity = rec.baseIntensity;
    }

    /// <summary>
    /// Directly overrides post-processing to fixed values, bypassing per-user settings.
    /// Use this when entering a mode (e.g. Live Dissection) that needs consistent visuals.
    /// Call ReloadForCurrentUser() to restore user settings afterwards.
    /// </summary>
    public void ForcePostProcessing(float exposure, float contrast)
    {
        if (!_hardwareReady) InitHardware();
        if (_colorAdjustments != null)
        {
            _colorAdjustments.postExposure.overrideState = true;
            _colorAdjustments.postExposure.value = exposure;
            _colorAdjustments.contrast.overrideState = true;
            _colorAdjustments.contrast.value = contrast;
            Debug.Log($"[OptionsController] Post-processing forced: exposure={exposure:F2}, contrast={contrast:F0}.");
        }
        else
        {
            Debug.LogWarning("[OptionsController] ForcePostProcessing: no ColorAdjustments found.");
        }
    }

    void ApplyUserSettings()
    {
        float brightness = PlayerPrefs.GetFloat(BrightnessKey, 0f);
        bool fpsOn = PlayerPrefs.GetInt(FpsKey, 0) == 1;

        ApplyLightBrightness(brightness);

        if (_colorAdjustments != null)
        {
            _colorAdjustments.postExposure.overrideState = true;
            _colorAdjustments.postExposure.value = brightness;
            _colorAdjustments.contrast.overrideState = true;
            _colorAdjustments.contrast.value = 0f;
        }

        if (brightnessSlider != null) brightnessSlider.SetValueWithoutNotify(brightness);
        if (fpsToggle != null) fpsToggle.SetIsOnWithoutNotify(fpsOn);

        OnFpsPreferenceChanged?.Invoke(fpsOn);

        Debug.Log($"[OptionsController] Applied: Brightness={brightness:F2}, FPS={(fpsOn ? "on" : "off")} for '{UserPrefix}'");
    }

    void CacheSceneLights()
    {
        _sceneLights.Clear();
        var allLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var l in allLights)
        {
            _sceneLights.Add(new LightRecord { light = l, baseIntensity = l.intensity });
        }
    }

    void ApplyLightBrightness(float sliderValue)
    {
        float multiplier = Mathf.Pow(2f, sliderValue);
        foreach (var rec in _sceneLights)
        {
            if (rec.light != null)
                rec.light.intensity = rec.baseIntensity * multiplier;
        }
    }

    void EnablePostProcessingOnAllCameras()
    {
        var allCams = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cam in allCams)
        {
            var urpCamData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (urpCamData == null)
                urpCamData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            urpCamData.renderPostProcessing = true;
        }
    }

    void FindOrCreateColorAdjustments()
    {
        var volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var vol in volumes)
        {
            if (vol.profile != null && vol.profile.TryGet(out ColorAdjustments ca))
            {
                _colorAdjustments = ca;
                _colorAdjustments.postExposure.overrideState = true;
                _colorAdjustments.contrast.overrideState = true;
                return;
            }
        }

        var volumeGO = new GameObject("OptionsVolume");
        var volume = volumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100;
        volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

        _colorAdjustments = volume.profile.Add<ColorAdjustments>(true);
        _colorAdjustments.postExposure.overrideState = true;
        _colorAdjustments.contrast.overrideState = true;
    }

    void OnBrightnessChanged(float value)
    {
        ApplyLightBrightness(value);

        if (_colorAdjustments != null)
        {
            _colorAdjustments.postExposure.overrideState = true;
            _colorAdjustments.postExposure.value = value;
        }
        PlayerPrefs.SetFloat(BrightnessKey, value);
        PlayerPrefs.Save();
    }

    void OnFpsToggleChanged(bool enabled)
    {
        PlayerPrefs.SetInt(FpsKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        OnFpsPreferenceChanged?.Invoke(enabled);
    }
}
