using System;
using UnityEngine;

/// <summary>
/// Zero-network <see cref="IRegionSpeechToText"/> used as the default v1
/// backend. It does not actually recognise words; it inspects the captured
/// audio buffer and returns either a placeholder "describe this region"
/// transcript when the user spoke (audio level above threshold) or an empty
/// string when the clip is silent.
///
/// This matches the prototype v1 intent rule: <em>"any successful utterance while
/// a valid region is active is treated as a request for that region's
/// description"</em>. It lets the entire voice loop (push-to-talk to speech to
/// hear description) work on Windows and Quest with no API keys, and can be
/// swapped for a real cloud STT later by setting a different implementation.
/// </summary>
public class AmplitudeSpeechToText : IRegionSpeechToText
{
    public bool CapturesAudioViaUnityMicrophone => true;

    /// <summary>
    /// RMS amplitude threshold above which the buffer is considered "voice".
    /// Tuned for typical headset microphones at ~16 kHz mono. Lower values
    /// trigger on whispers and background noise; higher values demand
    /// clearer speech.
    /// </summary>
    public float VoiceThreshold = 0.006f;

    /// <summary>
    /// Minimum number of seconds of clearly voiced audio that must be
    /// detected before the clip is considered an actual question. Filters
    /// out coughs, taps, and one-syllable noises like "uh".
    /// </summary>
    public float MinVoicedSeconds = 0.25f;

    public void BeginListenWindow() { /* stateless backend */ }

    public void Transcribe(AudioClip clip, Action<string> onResult)
    {
        if (clip == null || clip.samples == 0)
        {
            onResult?.Invoke(string.Empty);
            return;
        }

        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        double sumSq = 0.0;
        int voicedSampleCount = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float s = samples[i];
            sumSq += s * s;
            if (Mathf.Abs(s) >= VoiceThreshold) voicedSampleCount++;
        }
        float rms = samples.Length > 0 ? Mathf.Sqrt((float)(sumSq / samples.Length)) : 0f;
        float voicedSeconds = clip.frequency > 0
            ? (float)voicedSampleCount / Mathf.Max(1, clip.channels) / clip.frequency
            : 0f;

        Debug.Log($"[AmplitudeSpeechToText] Captured {clip.length:F2}s, RMS={rms:F4}, voiced={voicedSeconds:F2}s (rms>={VoiceThreshold:F4}, voicedMin={MinVoicedSeconds:F2}s)");

        if (rms >= VoiceThreshold && voicedSeconds >= MinVoicedSeconds)
            onResult?.Invoke("describe this region");
        else
            onResult?.Invoke(string.Empty);
    }
}
