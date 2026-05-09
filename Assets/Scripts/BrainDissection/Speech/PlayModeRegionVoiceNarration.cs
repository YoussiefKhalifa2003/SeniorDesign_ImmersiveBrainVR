using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Play-mode-only orchestrator for the voice narration feature.
///
/// Flow when the user presses the Ask Aloud button while a region is being
/// inspected:
///   1. Verify Play-mode gates (no Tutorial / no Assessment / no Live Dissection).
///   2. Verify a region with non-null <see cref="RegionData"/> is inspected.
///   3. Start the microphone and wait until the user releases the button or
///      until the configured max-duration elapses.
///   4. Hand the captured clip to <see cref="IRegionSpeechToText"/>.
///   5. If the transcript is non-empty, build a spoken script with
///      <see cref="RegionNarrationScriptBuilder"/> and play it through
///      <see cref="IRegionSpeechSynthesis"/>.
///   6. Stop speaking immediately if the inspected region changes (put-back,
///      reset, new extraction, mode exit).
///
/// Backends are abstracted so the same orchestrator can run with the local
/// amplitude STT + Windows SAPI TTS for an offline demo, or be upgraded to a
/// cloud STT/TTS pair later without touching this file.
/// </summary>
public class PlayModeRegionVoiceNarration : MonoBehaviour
{
    public enum State { Idle, Listening, Transcribing, Speaking }

    public static PlayModeRegionVoiceNarration Instance { get; private set; }

    public State CurrentState { get; private set; } = State.Idle;

    public event Action<State> OnStateChanged;

    RegionNarrationSettings _settings;
    IRegionSpeechToText _stt;
    IRegionSpeechSynthesis _tts;
    MicrophoneRecorder _recorder;
    Coroutine _listenWindowCoroutine;
    bool _listenWindowStopRequested;

    /// <summary>
    /// True when all gates currently allow the user to ask a question
    /// (Play mode active, an inspected region exists, no other narration
    /// step is running).
    /// </summary>
    public bool IsAvailable
    {
        get
        {
            if (CurrentState != State.Idle) return false;
            if (!IsPlayModeContext()) return false;
            return GetActiveRegionData() != null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject(nameof(PlayModeRegionVoiceNarration));
        Instance = go.AddComponent<PlayModeRegionVoiceNarration>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _settings = Resources.Load<RegionNarrationSettings>("RegionNarrationSettings");
        _recorder = gameObject.AddComponent<MicrophoneRecorder>();

        // Audio capture and the Windows speech recogniser both use whatever
        // mic Windows currently has set as the default capture endpoint.
        // We don't override anything — if the learner has set the Quest
        // mic as Windows default, that's what gets used; if they've set a
        // desktop mic, that gets used. They control it via Windows Sound
        // settings.
        if (Microphone.devices != null && Microphone.devices.Length > 0)
            Debug.Log($"[VoiceNarration] Available capture devices: [{string.Join(", ", Microphone.devices)}]. Using Windows system default.");
        else
            Debug.LogWarning("[VoiceNarration] No capture devices visible to Unity.");

        _stt = CreateDefaultStt();
        _tts = new WindowsSapiSpeechSynthesis();
    }

    static IRegionSpeechToText CreateDefaultStt()
    {
        // Windows keeps the reliable Unity mic recording path, then requires
        // a recognised question-like phrase before narration starts. Other
        // platforms keep the simple amplitude fallback until a platform STT
        // intent recogniser is added.
#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && !UNITY_WSA
        return new WindowsQuestionIntentSpeechToText();
#else
        return new AmplitudeSpeechToText();
#endif
    }

    void OnEnable()
    {
        BrainManager.OnInspectionEnded += HandleInspectionEnded;
    }

    void OnDisable()
    {
        BrainManager.OnInspectionEnded -= HandleInspectionEnded;
    }

    /// <summary>
    /// Allow other scripts to swap in a different backend at runtime
    /// (e.g. a cloud TTS adapter selected by build configuration).
    /// </summary>
    public void ConfigureBackends(IRegionSpeechToText stt, IRegionSpeechSynthesis tts)
    {
        if (stt != null) _stt = stt;
        if (tts != null) _tts = tts;
    }

    /// <summary>
    /// Begin push-to-talk recording. Safe to call multiple times — only the
    /// first call while in <see cref="State.Idle"/> takes effect.
    /// </summary>
    public void BeginListening()
    {
        if (CurrentState != State.Idle) return;

        if (!IsPlayModeContext())
        {
            Debug.Log("[VoiceNarration] BeginListening blocked: not in Play mode.");
            return;
        }

        var data = GetActiveRegionData();
        if (data == null)
        {
            SpeakAndRestate(State.Speaking, RegionNarrationScriptBuilder.BuildNoRegionMessage());
            return;
        }

        _tts?.Stop();
        TransitionTo(State.Listening);
        ShowStatus("Listening… ask a question like \"What is this region?\"");

        // Reset the STT's per-window state so any phrase recognised before
        // the user pressed the button doesn't bleed into this request.
        _stt?.BeginListenWindow();

        float maxDuration = _settings != null ? _settings.maxRecordingSeconds : 6f;
        int sampleRate = _settings != null ? _settings.sampleRate : 16000;

        if (UsesUnityMicrophoneCapture())
        {
            _recorder.Begin(sampleRate, maxDuration, OnRecordingFinished);
        }
        else
        {
            _listenWindowStopRequested = false;
            if (_listenWindowCoroutine != null) StopCoroutine(_listenWindowCoroutine);
            _listenWindowCoroutine = StartCoroutine(PassiveListenWindow(maxDuration));
            Debug.Log($"[VoiceNarration] Dictation listen window started without Unity Microphone capture, max {maxDuration:F1}s.");
        }
    }

    /// <summary>Stop recording early (release of push-to-talk).</summary>
    public void EndListening()
    {
        if (CurrentState != State.Listening) return;
        if (UsesUnityMicrophoneCapture())
            _recorder.End();
        else
            _listenWindowStopRequested = true;
    }

    /// <summary>
    /// Cancel anything currently in progress (recording or speaking) and
    /// return the orchestrator to <see cref="State.Idle"/>.
    /// </summary>
    public void Cancel()
    {
        if (CurrentState == State.Idle) return;

        if (_recorder != null && _recorder.IsRecording)
            _recorder.End();
        if (_listenWindowCoroutine != null)
        {
            StopCoroutine(_listenWindowCoroutine);
            _listenWindowCoroutine = null;
        }
        _listenWindowStopRequested = false;
        _tts?.Stop();
        TransitionTo(State.Idle);
        ShowStatus(string.Empty);
    }

    void OnRecordingFinished(AudioClip clip)
    {
        if (CurrentState != State.Listening) return;

        if (clip == null && UsesUnityMicrophoneCapture())
        {
            ShowStatus("Microphone unavailable.");
            TransitionTo(State.Idle);
            return;
        }

        if (!IsPlayModeContext() || GetActiveRegionData() == null)
        {
            TransitionTo(State.Idle);
            return;
        }

        TransitionTo(State.Transcribing);
        ShowStatus("Transcribing…");

        // Grace period: speech recognisers commonly fire their final
        // result event 200–700 ms after the user stops talking. If the
        // learner taps to end push-to-talk immediately after speaking,
        // calling Transcribe right away would miss that trailing event
        // and emit "didn't catch that" even for valid phrases. Wait a
        // short beat first.
        StartCoroutine(WaitThenTranscribe(clip, 0.9f));
    }

    IEnumerator PassiveListenWindow(float maxDuration)
    {
        float deadline = Time.unscaledTime + Mathf.Max(1f, maxDuration);
        while (CurrentState == State.Listening && !_listenWindowStopRequested && Time.unscaledTime < deadline)
            yield return null;

        _listenWindowCoroutine = null;
        _listenWindowStopRequested = false;

        if (CurrentState == State.Listening)
            OnRecordingFinished(null);
    }

    IEnumerator WaitThenTranscribe(AudioClip clip, float waitSeconds)
    {
        float deadline = Time.unscaledTime + waitSeconds;
        while (Time.unscaledTime < deadline)
        {
            if (CurrentState != State.Transcribing) yield break;
            yield return null;
        }
        if (CurrentState != State.Transcribing) yield break;
        _stt.Transcribe(clip, OnTranscriptReady);
    }

    void OnTranscriptReady(string transcript)
    {
        if (CurrentState != State.Transcribing) return;

        if (string.IsNullOrWhiteSpace(transcript))
        {
            SpeakAndRestate(State.Speaking, RegionNarrationScriptBuilder.BuildDidNotCatchMessage());
            return;
        }

        // Special token from the dictation STT meaning "user spoke clearly
        // but said something we don't recognise as a question". Coach the
        // learner with examples instead of reading the description.
#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && !UNITY_WSA
        if (transcript == WindowsDictationSpeechToText.SpeechWithoutKeywordToken ||
            transcript == WindowsQuestionIntentSpeechToText.SpeechWithoutQuestionIntentToken)
        {
            Debug.Log("[VoiceNarration] Heard user but no question intent recognised; prompting for rephrase.");
            SpeakAndRestate(State.Speaking, RegionNarrationScriptBuilder.BuildUnrecognisedQuestionMessage());
            return;
        }
#endif

        var data = GetActiveRegionData();
        if (data == null)
        {
            TransitionTo(State.Idle);
            ShowStatus(string.Empty);
            return;
        }

        bool intro = _settings == null || _settings.speakIntro;
        string script = RegionNarrationScriptBuilder.Build(data, intro);
        Debug.Log($"[VoiceNarration] Transcript=\"{transcript}\". Speaking script ({script.Length} chars) for region '{data.displayName}'.");
        SpeakAndRestate(State.Speaking, script);
    }

    void SpeakAndRestate(State stateWhileSpeaking, string text)
    {
        TransitionTo(stateWhileSpeaking);
        ShowStatus("Reading description…");
        _tts.Speak(text, () =>
        {
            if (CurrentState == stateWhileSpeaking)
            {
                TransitionTo(State.Idle);
                ShowStatus(string.Empty);
            }
        });
    }

    void HandleInspectionEnded()
    {
        if (CurrentState == State.Idle) return;
        Cancel();
    }

    bool UsesUnityMicrophoneCapture()
    {
        return _stt == null || _stt.CapturesAudioViaUnityMicrophone;
    }

    bool IsPlayModeContext()
    {
        if (!SessionData.IsPlayMode) return false;
        if (SessionData.IsTutorialMode) return false;
        if (SessionData.IsAssessmentMode) return false;

        var live = LiveDissectionManager.Instance;
        if (live != null && live.IsLiveDissectionActive) return false;

        var tut = TutorialManager.Instance;
        if (tut != null && tut.IsTutorialActive) return false;

        return true;
    }

    RegionData GetActiveRegionData()
    {
        var bm = FindFirstObjectByType<BrainManager>();
        if (bm == null) return null;
        if (!bm.IsInspectingRegion) return null;
        var region = bm.InspectedRegion;
        return region != null ? region.regionData : null;
    }

    void TransitionTo(State next)
    {
        if (CurrentState == next) return;
        CurrentState = next;
        OnStateChanged?.Invoke(next);
    }

    void ShowStatus(string message)
    {
        var ui = FindFirstObjectByType<RegionUIController>();
        if (ui != null) ui.SetStatusMessage(message);
    }
}
