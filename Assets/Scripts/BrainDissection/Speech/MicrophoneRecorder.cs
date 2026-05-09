using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Thin wrapper around <see cref="UnityEngine.Microphone"/> tailored for
/// short push-to-talk captures. Starts recording, polls for either an
/// explicit Stop call or a max-duration timeout, then trims the resulting
/// <see cref="AudioClip"/> down to just what the user said.
///
/// Trimming matters because Unity's Microphone API records into a fixed
/// length buffer (e.g. 10s) regardless of how long the user actually
/// speaks. Without trimming the resulting clip is padded with silence,
/// which inflates network payloads to cloud STT and skews amplitude based
/// detection.
/// </summary>
public class MicrophoneRecorder : MonoBehaviour
{
    public bool IsRecording { get; private set; }
    public string ActiveDevice { get; private set; }

    /// <summary>
    /// Optional override. When set to a substring (case-insensitive) the
    /// recorder will pick the first <see cref="Microphone.devices"/> entry
    /// whose name contains it. Useful when the auto-detector misses the
    /// learner's specific hardware.
    /// </summary>
    public string PreferredDeviceContains { get; set; }

    // Substrings we match (case-insensitive) when auto-detecting the
    // headset-mounted microphone over Quest Link / SteamVR / OpenXR. The
    // first match wins, in priority order.
    static readonly string[] HeadsetMicHints = new[]
    {
        "oculus", "quest", "meta",
        "rift",
        "htc", "vive",
        "index", "valve",
        "varjo", "pico", "vr ",
        "headset",
        "xr ",
    };

    int _sampleRate;
    float _startTime;
    float _maxDuration;
    AudioClip _bufferClip;
    Coroutine _watchdog;
    Action<AudioClip> _onComplete;

    /// <summary>
    /// Begin a recording. <paramref name="onComplete"/> receives the trimmed
    /// clip (or null on failure / no permission). Calling Begin again before
    /// completion is a no-op — caller should guard with <see cref="IsRecording"/>.
    /// </summary>
    public void Begin(int sampleRate, float maxDurationSeconds, Action<AudioClip> onComplete)
    {
        if (IsRecording)
        {
            Debug.LogWarning("[MicrophoneRecorder] Begin called while already recording; ignored.");
            return;
        }

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[MicrophoneRecorder] No microphone devices available.");
            onComplete?.Invoke(null);
            return;
        }

        ActiveDevice = SelectHeadsetMicrophone(PreferredDeviceContains);
        _sampleRate = Mathf.Max(8000, sampleRate);
        _maxDuration = Mathf.Max(1f, maxDurationSeconds);
        _onComplete = onComplete;

        int bufferLengthSec = Mathf.CeilToInt(_maxDuration) + 1;
        _bufferClip = Microphone.Start(ActiveDevice, false, bufferLengthSec, _sampleRate);
        if (_bufferClip == null)
        {
            Debug.LogWarning($"[MicrophoneRecorder] Microphone.Start returned null for device '{ActiveDevice}'.");
            onComplete?.Invoke(null);
            return;
        }

        _startTime = Time.unscaledTime;
        IsRecording = true;
        _watchdog = StartCoroutine(WatchForTimeout());
        string deviceLabel = string.IsNullOrEmpty(ActiveDevice) ? "(system default)" : ActiveDevice;
        Debug.Log($"[MicrophoneRecorder] Recording started on '{deviceLabel}' at {_sampleRate} Hz, max {_maxDuration:F1}s.");
    }

    /// <summary>Stop recording immediately and emit the trimmed clip.</summary>
    public void End()
    {
        if (!IsRecording) return;
        FinalizeRecording();
    }

    IEnumerator WatchForTimeout()
    {
        while (IsRecording)
        {
            if (Time.unscaledTime - _startTime >= _maxDuration)
            {
                FinalizeRecording();
                yield break;
            }
            yield return null;
        }
    }

    void FinalizeRecording()
    {
        IsRecording = false;
        if (_watchdog != null) { StopCoroutine(_watchdog); _watchdog = null; }

        int positionAtStop = 0;
        try { positionAtStop = Microphone.GetPosition(ActiveDevice); }
        catch { /* device may already be released */ }

        try { if (Microphone.IsRecording(ActiveDevice)) Microphone.End(ActiveDevice); }
        catch { /* ignore */ }

        AudioClip trimmed = null;
        if (_bufferClip != null && positionAtStop > 0)
        {
            int channels = Mathf.Max(1, _bufferClip.channels);
            var samples = new float[positionAtStop * channels];
            _bufferClip.GetData(samples, 0);
            trimmed = AudioClip.Create("Utterance", positionAtStop, channels, _bufferClip.frequency, false);
            trimmed.SetData(samples, 0);
        }

        var cb = _onComplete;
        _onComplete = null;
        _bufferClip = null;
        cb?.Invoke(trimmed);
    }

    void OnDisable()
    {
        if (IsRecording)
        {
            try { if (Microphone.IsRecording(ActiveDevice)) Microphone.End(ActiveDevice); } catch { }
            IsRecording = false;
            if (_watchdog != null) { StopCoroutine(_watchdog); _watchdog = null; }
            var cb = _onComplete;
            _onComplete = null;
            _bufferClip = null;
            cb?.Invoke(null);
        }
    }

    /// <summary>
    /// Pick the capture device the recorder should use for the next push-
    /// to-talk session. Default behaviour is to pass <c>null</c> to
    /// <see cref="Microphone.Start"/>, which tells Unity to use whatever
    /// mic the system has as the default capture endpoint — exactly what
    /// the learner has set in Windows Sound settings (Quest mic, desktop
    /// mic, headset, etc.). The Windows.Speech recogniser is bound to the
    /// same default, so capture and recognition stay in sync without us
    /// touching any system settings.
    ///
    /// If <paramref name="explicitContains"/> is non-empty, we honour that
    /// override (handy for explicit per-build configuration), otherwise we
    /// trust the system default.
    /// </summary>
    public static string SelectHeadsetMicrophone(string explicitContains = null)
    {
        var devices = Microphone.devices;
        if (devices == null || devices.Length == 0) return null;

        if (!string.IsNullOrEmpty(explicitContains))
        {
            string needle = explicitContains.ToLowerInvariant();
            foreach (var d in devices)
            {
                if (d == null) continue;
                if (d.ToLowerInvariant().Contains(needle))
                {
                    Debug.Log($"[MicrophoneRecorder] Using explicit override device '{d}'.");
                    return d;
                }
            }
            Debug.LogWarning($"[MicrophoneRecorder] Override '{explicitContains}' not found; using system default.");
        }

        return null; // Windows / Unity default capture endpoint
    }
}
