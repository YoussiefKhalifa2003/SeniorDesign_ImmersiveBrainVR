using System;

/// <summary>
/// Minimal text-to-speech interface used by the Play-mode voice narration
/// system. Implementations are expected to be safe to call with overlapping
/// requests: a new <see cref="Speak"/> call cancels any in-flight playback.
/// </summary>
public interface IRegionSpeechSynthesis
{
    /// <summary>True while audio is currently being rendered.</summary>
    bool IsSpeaking { get; }

    /// <summary>
    /// Speak the given text. <paramref name="onComplete"/> is invoked when
    /// playback finishes naturally OR is cancelled by <see cref="Stop"/> or a
    /// subsequent Speak call.
    /// </summary>
    void Speak(string text, Action onComplete = null);

    /// <summary>Stop any in-flight playback immediately.</summary>
    void Stop();
}
