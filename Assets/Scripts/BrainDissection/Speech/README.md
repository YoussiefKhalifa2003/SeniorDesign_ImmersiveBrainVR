# Play-mode voice narration

Voice-guided narration of an extracted brain region. Available **only in Play
mode** — disabled during Tutorial, MCQ Quiz and Live Dissection.

## Flow

1. Student equips tweezers and extracts a region. `BrainManager` enters
   `RegionSelected` and exposes the region via `BrainManager.InspectedRegion`.
2. The "Ask Aloud" button on the details panel becomes interactable.
3. Tap → microphone records up to `maxRecordingSeconds` (configurable).
4. The captured clip is passed to `IRegionSpeechToText`. If the user spoke
   audibly, the orchestrator builds a script from the region's
   `shortDescription` / `detailedDescription` and plays it via
   `IRegionSpeechSynthesis`.
5. Putting the region back (or resetting the brain) immediately cancels any
   in-flight recording or playback.

## Default backends

| Concern | Default | Where |
|---------|---------|-------|
| Microphone capture | `UnityEngine.Microphone` | `MicrophoneRecorder.cs` |
| Speech-to-text | `AmplitudeSpeechToText` (no network, treats any audible utterance as a request to describe the current region) | `AmplitudeSpeechToText.cs` |
| Text-to-speech | `WindowsSapiSpeechSynthesis` (wraps the existing Windows-only `TextToSpeech.cs`) | `WindowsSapiSpeechSynthesis.cs` |

Both backends can be swapped at runtime via
`PlayModeRegionVoiceNarration.Instance.ConfigureBackends(...)` — for example
to plug in a cloud STT/TTS pair on Android/Quest builds where Windows SAPI is
unavailable.

## Configuration

Optional `RegionNarrationSettings` ScriptableObject (Assets > Create > Brain
Dissection > Region Narration Settings). Place under a `Resources` folder and
name it `RegionNarrationSettings` to be auto-loaded.

| Field | Purpose |
|-------|---------|
| `maxRecordingSeconds` | Hard cap on how long the mic listens after a tap. |
| `sampleRate` | Microphone sample rate (16 000 Hz is enough for STT). |
| `speechRate` | SAPI rate, -5..5. |
| `speakIntro` | If true, narration is prefixed with "Here is the description for {region}". |
| `sttEndpointUrl` / `sttApiKey` | Reserved for an HTTP STT backend (not used by the default `AmplitudeSpeechToText`). |

**API keys must never be committed to source control.** If you wire a cloud
STT/TTS backend later, store keys in a local `Resources/` asset that is
gitignored, or read them from environment variables at runtime.

## Platform notes

* **Windows Editor / Standalone** — TTS works out of the box (Windows SAPI).
* **Android / Quest** — the default Windows TTS backend will silently fail on
  `wscript.exe` startup (it logs a warning and the user sees the on-screen
  status but hears nothing). To enable spoken playback in Quest builds, plug
  a cross-platform TTS adapter into `ConfigureBackends`.
* **Microphone permission** — Android requires `RECORD_AUDIO` in the
  `AndroidManifest.xml`. The Unity XR Plugin Management → Oculus settings or a
  custom manifest under `Assets/Plugins/Android/` should declare it.

## Disabling the feature

Remove the `RegionVoiceNarrationButton` runtime hook by deleting
`RegionVoiceNarrationButton.cs`, or guard `EnsureAttached()` behind your own
preference flag. The orchestrator itself is harmless: it only ever runs when
the user taps the button.
