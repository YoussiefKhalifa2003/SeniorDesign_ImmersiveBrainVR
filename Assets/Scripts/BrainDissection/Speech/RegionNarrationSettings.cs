using UnityEngine;

/// <summary>
/// Per-project tunables for the Play-mode voice narration system. Optional;
/// the orchestrator falls back to sensible defaults when no asset is present.
///
/// Create via Assets &gt; Create &gt; Brain Dissection &gt; Region Narration Settings.
/// API keys must NOT be checked into source control — set them through a
/// local Resources/RegionNarrationSettings asset that is gitignored, or via
/// environment variables read at runtime.
/// </summary>
[CreateAssetMenu(fileName = "RegionNarrationSettings",
    menuName = "Brain Dissection/Region Narration Settings", order = 10)]
public class RegionNarrationSettings : ScriptableObject
{
    [Header("Recording")]
    [Tooltip("Maximum number of seconds the microphone records when the user holds Ask Aloud.")]
    [Range(2f, 15f)] public float maxRecordingSeconds = 6f;

    [Tooltip("Microphone sample rate. 16000 is enough for speech recognition and keeps clips small.")]
    public int sampleRate = 16000;

    [Header("Speech-to-text (optional cloud)")]
    [Tooltip("If set, the orchestrator will POST captured audio to this endpoint and parse a transcript from the response.")]
    public string sttEndpointUrl = "";

    [Tooltip("Optional bearer token / API key sent in the Authorization header of STT requests.")]
    public string sttApiKey = "";

    [Header("Speech synthesis")]
    [Tooltip("Speech rate (-10 slowest, 10 fastest). Used by the Windows SAPI backend.")]
    [Range(-5, 5)] public int speechRate = 1;

    [Header("UX")]
    [Tooltip("If true, the orchestrator first speaks an intro line ('Here is the description for ...').")]
    public bool speakIntro = true;
}
