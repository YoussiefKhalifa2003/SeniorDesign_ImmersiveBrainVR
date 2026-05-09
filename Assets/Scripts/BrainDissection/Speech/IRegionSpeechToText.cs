using System;
using UnityEngine;

/// <summary>
/// Minimal speech-to-text interface used by the Play-mode voice narration
/// system. Some backends consume an <see cref="AudioClip"/> captured through
/// Unity's <see cref="UnityEngine.Microphone"/> API; others listen directly
/// through the platform speech service and only need a timed listen window.
/// </summary>
public interface IRegionSpeechToText
{
    /// <summary>
    /// True when the orchestrator must capture audio through Unity's
    /// microphone API and pass the clip to <see cref="Transcribe"/>.
    /// False for real-time recognisers that already listen to the system
    /// default capture endpoint themselves.
    /// </summary>
    bool CapturesAudioViaUnityMicrophone { get; }

    /// <summary>
    /// Called by the orchestrator when a new push-to-talk window opens.
    /// Backends that maintain real-time state (e.g. a keyword recogniser
    /// running continuously in the background) reset their match tracker
    /// here so utterances heard before the window opened don't bleed into
    /// the next request. Stateless backends can leave this empty.
    /// </summary>
    void BeginListenWindow();

    /// <summary>
    /// Transcribe the given audio. <paramref name="onResult"/> receives the
    /// raw transcript (possibly empty) on success, or null on failure /
    /// network error.
    /// </summary>
    void Transcribe(AudioClip clip, Action<string> onResult);
}
