#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && !UNITY_WSA
using System;
using UnityEngine;
using UnityEngine.Windows.Speech;

/// <summary>
/// Speech-to-text backend that uses Windows 10/11's built-in
/// <see cref="DictationRecognizer"/> for free-form transcription, then
/// applies a generous substring keyword check to decide whether the
/// learner asked an actual question about the inspected region.
///
/// Why dictation instead of <c>KeywordRecognizer</c>:
///   - <c>KeywordRecognizer</c> only fires on phrases that are an exact
///     enough match of one of its predefined strings. Realistic learner
///     phrasings vary — accent, hesitation, swallowed syllables — and
///     would routinely miss recognition.
///   - <c>DictationRecognizer</c> returns whatever Windows transcribed,
///     and we keyword-check the transcript ourselves with cheap substring
///     matches. Any natural question phrasing that contains "what",
///     "describe", "explain", "tell me", "this region", etc. triggers
///     narration; gibberish like "hello hello" doesn't match any trigger
///     and is rejected.
///
/// The recogniser also exposes interim hypotheses, so when a learner
/// taps to stop early we still have the last partial transcript to act
/// on instead of waiting for a full final result.
/// </summary>
public class WindowsDictationSpeechToText : IRegionSpeechToText, IDisposable
{
    /// <summary>
    /// Token returned when the captured clip clearly contained sustained
    /// speech (or a transcript came back) but the words did not contain a
    /// recognised question pattern. The orchestrator translates this into
    /// "I didn't quite get that — try asking …" instead of triggering
    /// narration.
    /// </summary>
    public const string SpeechWithoutKeywordToken = "__speech_without_keyword__";

    DictationRecognizer _dictation;

    string _bestTranscript;
    float _lastUtteranceAt = -1000f;
    float _windowOpenedAt = -1000f;

    // Speech detection thresholds for the captured-clip fallback.
    const float SpeechRmsThreshold = 0.012f;
    const float SpeechMinVoicedSeconds = 0.5f;

    public bool CapturesAudioViaUnityMicrophone => _dictation == null;

    public WindowsDictationSpeechToText()
    {
        try
        {
            _dictation = new DictationRecognizer(ConfidenceLevel.Low);
            // Disable the built-in silence timeouts so the recogniser
            // keeps listening across the whole session — we control
            // start/stop windows ourselves with BeginListenWindow.
            _dictation.AutoSilenceTimeoutSeconds = 0f;
            _dictation.InitialSilenceTimeoutSeconds = 0f;

            _dictation.DictationResult += OnResult;
            _dictation.DictationHypothesis += OnHypothesis;
            _dictation.DictationError += OnError;
            _dictation.DictationComplete += OnComplete;

            _dictation.Start();
            Debug.Log($"[Dictation] Recognizer started. Status: {_dictation.Status}.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Dictation] Failed to start DictationRecognizer ({e.Message}). Voice narration will fall back to amplitude detection.");
            _dictation = null;
        }
    }

    void OnResult(string text, ConfidenceLevel confidence)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _bestTranscript = text;
        _lastUtteranceAt = Time.unscaledTime;
        Debug.Log($"[Dictation] Final result: \"{text}\" (confidence={confidence}).");
    }

    void OnHypothesis(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _bestTranscript = text;
        _lastUtteranceAt = Time.unscaledTime;
        // Hypothesis logging is intentionally quiet — fires often.
    }

    void OnError(string error, int hresult)
    {
        Debug.LogWarning($"[Dictation] Error: {error} (0x{hresult:X8}).");
    }

    void OnComplete(DictationCompletionCause cause)
    {
        // The recogniser stops itself on network drops, timeouts, etc.
        // Restart so future Ask Aloud presses still work.
        if (_dictation == null) return;
        if (cause == DictationCompletionCause.Complete) return;
        try
        {
            if (_dictation.Status != SpeechSystemStatus.Running)
            {
                _dictation.Start();
                Debug.Log($"[Dictation] Restarted after completion cause: {cause}.");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Dictation] Restart failed: {e.Message}.");
        }
    }

    public void BeginListenWindow()
    {
        _windowOpenedAt = Time.unscaledTime;
        // Drop any transcript heard before this push-to-talk session.
        _bestTranscript = null;
        _lastUtteranceAt = -1000f;
    }

    public void Transcribe(AudioClip clip, Action<string> onResult)
    {
        bool transcriptInWindow =
            !string.IsNullOrEmpty(_bestTranscript) &&
            _lastUtteranceAt >= _windowOpenedAt &&
            Time.unscaledTime - _lastUtteranceAt <= 12f;

        string transcript = transcriptInWindow ? _bestTranscript : null;
        bool hasSpeechAudio = HasSustainedSpeech(clip);

        Debug.Log($"[Dictation.Transcribe] inWindow={transcriptInWindow}, transcript=\"{transcript ?? "<null>"}\", hasSpeechAudio={hasSpeechAudio}");

        if (_dictation == null)
        {
            onResult?.Invoke(hasSpeechAudio ? "describe this region" : string.Empty);
            return;
        }

        if (!string.IsNullOrEmpty(transcript) && IsQuestionLike(transcript))
        {
            onResult?.Invoke(transcript);
            return;
        }

        // Decide which "didn't catch" message to emit.
        bool somethingWasSpoken = !string.IsNullOrEmpty(transcript) || hasSpeechAudio;
        onResult?.Invoke(somethingWasSpoken ? SpeechWithoutKeywordToken : string.Empty);
    }

    /// <summary>
    /// Generous match. The transcript is treated as a question about the
    /// inspected region whenever it contains any of these substrings.
    /// The list is kept aggressive on purpose — false-positive narration
    /// on innocuous phrases that happen to contain "what" is a much smaller
    /// failure mode than the system silently ignoring real questions.
    /// </summary>
    static bool IsQuestionLike(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        string l = text.ToLowerInvariant();
        string[] triggers =
        {
            // Question stems
            "what", "which", "how",
            // Verbs students actually say
            "describe", "explain", "tell me", "show me",
            "speak", "say",
            // Topic words
            "info", "information",
            "function", "purpose", "role", "job",
            "point", "meaning",
            "control", "represent",
            "name",
            "extract", "extracted", "selected", "picked", "pulled", "just",
            // Region anchor words
            "this region", "this part", "this structure", "this thing",
            "this is", "this one",
        };
        foreach (var t in triggers)
            if (l.Contains(t)) return true;
        return false;
    }

    static bool HasSustainedSpeech(AudioClip clip)
    {
        if (clip == null || clip.samples == 0 || clip.frequency <= 0) return false;

        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        int voicedSampleCount = 0;
        for (int i = 0; i < samples.Length; i++)
            if (Mathf.Abs(samples[i]) >= SpeechRmsThreshold) voicedSampleCount++;

        float voicedSeconds =
            (float)voicedSampleCount / Mathf.Max(1, clip.channels) / clip.frequency;
        return voicedSeconds >= SpeechMinVoicedSeconds;
    }

    public void Dispose()
    {
        if (_dictation == null) return;
        try
        {
            if (_dictation.Status == SpeechSystemStatus.Running) _dictation.Stop();
            _dictation.Dispose();
        }
        catch (Exception e) { Debug.LogWarning($"[Dictation] Dispose failed: {e.Message}."); }
        _dictation = null;
    }

    ~WindowsDictationSpeechToText() => Dispose();
}
#endif
