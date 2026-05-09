using System;

/// <summary>
/// <see cref="IRegionSpeechSynthesis"/> backend for Windows Editor / standalone
/// builds. Delegates to the existing <see cref="TextToSpeech"/> Windows-SAPI
/// implementation which renders WAV through Unity's audio system.
///
/// Not used on Android/Quest builds — pick a cloud backend there.
/// </summary>
public class WindowsSapiSpeechSynthesis : IRegionSpeechSynthesis
{
    public bool IsSpeaking => TextToSpeech.IsSpeaking;

    public void Speak(string text, Action onComplete = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            onComplete?.Invoke();
            return;
        }
        TextToSpeech.Speak(text, 1, onComplete);
    }

    public void Stop() => TextToSpeech.Stop();
}
