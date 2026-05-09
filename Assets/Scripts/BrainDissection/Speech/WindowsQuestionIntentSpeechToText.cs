#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && !UNITY_WSA
using System;
using UnityEngine;
using UnityEngine.Windows.Speech;

/// <summary>
/// Windows-only intent gate for Ask Aloud. Unity still records the mic audio
/// so we know whether the learner actually spoke, but narration only starts
/// when Windows also recognises one of the supported question-like phrases.
/// This prevents random speech such as "hello, good morning" from reading
/// the region description.
/// </summary>
public class WindowsQuestionIntentSpeechToText : IRegionSpeechToText, IDisposable
{
    public const string SpeechWithoutQuestionIntentToken = "__speech_without_question_intent__";

    static readonly string[] QuestionPhrases =
    {
        "what is this",
        "what's this",
        "what is this region",
        "what's this region",
        "what region is this",
        "which region is this",
        "what am i looking at",
        "what am i seeing",
        "what is this part",
        "what is this structure",
        "what is this brain region",
        "what brain region is this",

        "what does this do",
        "what does this region do",
        "what does this part do",
        "what does this structure do",
        "what is the function",
        "what is its function",
        "what is the function of this region",
        "what is the functionality of this region",
        "what is the purpose",
        "what is its purpose",
        "what is the purpose of this region",
        "what is the role",
        "what is its role",
        "what is the role of this region",

        "describe this",
        "describe this region",
        "describe this part",
        "describe this structure",
        "explain this",
        "explain this region",
        "explain this part",
        "explain this structure",
        "please explain this",
        "please explain this region",
        "please explain this region to me",
        "tell me about this",
        "tell me about this region",
        "tell me about this part",
        "give me information",
        "give me information about this region",
        "give me info",
        "give me info about this region",

        "what did i extract",
        "what did i just extract",
        "what have i extracted",
        "what region did i extract",
        "what is the region i extracted",
        "what is the region that i extracted",
        "what did i pull out",
        "what did i pick up",
        "what did i select",

        "read this",
        "read this region",
        "read the description",
        "read the description of this region",
        "speak about this region",
    };

    readonly AmplitudeSpeechToText _speechDetector = new AmplitudeSpeechToText();
    KeywordRecognizer _recognizer;
    string _lastPhrase;
    float _lastPhraseAt = -1000f;
    float _windowOpenedAt = -1000f;

    public bool CapturesAudioViaUnityMicrophone => true;

    public WindowsQuestionIntentSpeechToText()
    {
        try
        {
            _recognizer = new KeywordRecognizer(QuestionPhrases, ConfidenceLevel.Low);
            _recognizer.OnPhraseRecognized += OnPhraseRecognized;
            _recognizer.Start();
            Debug.Log($"[QuestionIntent] Keyword recognizer started with {QuestionPhrases.Length} question phrases.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[QuestionIntent] Failed to start keyword recognizer ({e.Message}). Ask Aloud will hear the mic but require retry prompts.");
            _recognizer = null;
        }
    }

    public void BeginListenWindow()
    {
        _windowOpenedAt = Time.unscaledTime;
        _lastPhrase = null;
        _lastPhraseAt = -1000f;
    }

    public void Transcribe(AudioClip clip, Action<string> onResult)
    {
        bool phraseInWindow =
            !string.IsNullOrEmpty(_lastPhrase) &&
            _lastPhraseAt >= _windowOpenedAt &&
            Time.unscaledTime - _lastPhraseAt <= 8f;

        if (phraseInWindow)
        {
            Debug.Log($"[QuestionIntent] Accepted phrase: \"{_lastPhrase}\".");
            onResult?.Invoke(_lastPhrase);
            return;
        }

        _speechDetector.Transcribe(clip, transcript =>
        {
            bool heardSpeech = !string.IsNullOrWhiteSpace(transcript);
            Debug.Log($"[QuestionIntent] phraseInWindow=false, heardSpeech={heardSpeech}.");
            onResult?.Invoke(heardSpeech ? SpeechWithoutQuestionIntentToken : string.Empty);
        });
    }

    void OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.text)) return;
        _lastPhrase = args.text;
        _lastPhraseAt = Time.unscaledTime;
        Debug.Log($"[QuestionIntent] Heard question phrase: \"{args.text}\" ({args.confidence}).");
    }

    public void Dispose()
    {
        if (_recognizer == null) return;
        try
        {
            if (_recognizer.IsRunning) _recognizer.Stop();
            _recognizer.OnPhraseRecognized -= OnPhraseRecognized;
            _recognizer.Dispose();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[QuestionIntent] Dispose failed: {e.Message}.");
        }
        _recognizer = null;
    }

    ~WindowsQuestionIntentSpeechToText() => Dispose();
}
#endif
