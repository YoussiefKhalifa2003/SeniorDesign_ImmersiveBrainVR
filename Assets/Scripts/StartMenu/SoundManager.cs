using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Global sound manager for UI feedback, correct/wrong sounds, and ambient audio.
/// Uses procedurally generated placeholder tones (no external audio assets required).
/// Replace GenerateTone clips with real AudioClips from assets when available.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Volume")]
    [Range(0f, 1f)] public float masterVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 0.7f;
    [Range(0f, 1f)] public float ambientVolume = 0.3f;

    AudioSource _sfxSource;
    AudioSource _ambientSource;

    AudioClip _clipCorrect;
    AudioClip _clipWrong;
    AudioClip _clipClick;
    AudioClip _clipStepComplete;
    AudioClip _clipAchievement;
    AudioClip _clipAmbientLoop;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
    }

    void Init()
    {
        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.spatialBlend = 0f;

        _ambientSource = gameObject.AddComponent<AudioSource>();
        _ambientSource.playOnAwake = false;
        _ambientSource.loop = true;
        _ambientSource.spatialBlend = 0f;

        _clipCorrect = GenerateTone(880f, 0.15f, 0.6f);
        _clipWrong = GenerateTone(220f, 0.25f, 0.5f);
        _clipClick = GenerateTone(1200f, 0.05f, 0.3f);
        _clipStepComplete = GenerateTwoTone(660f, 880f, 0.12f, 0.5f);
        _clipAchievement = GenerateTripleTone(523f, 659f, 784f, 0.15f, 0.6f);
        _clipAmbientLoop = GenerateAmbientHum(0.08f);
    }

    public void PlayCorrect() => PlaySFX(_clipCorrect);
    public void PlayWrong() => PlaySFX(_clipWrong);
    public void PlayClick() => PlaySFX(_clipClick);
    public void PlayStepComplete() => PlaySFX(_clipStepComplete);
    public void PlayAchievement() => PlaySFX(_clipAchievement);

    public void StartAmbient()
    {
        if (_ambientSource == null || _clipAmbientLoop == null) return;
        _ambientSource.clip = _clipAmbientLoop;
        _ambientSource.volume = ambientVolume * masterVolume;
        _ambientSource.Play();
    }

    public void StopAmbient()
    {
        if (_ambientSource != null) _ambientSource.Stop();
    }

    void PlaySFX(AudioClip clip)
    {
        if (_sfxSource == null || clip == null) return;
        _sfxSource.PlayOneShot(clip, sfxVolume * masterVolume);
    }

    public void SetMasterVolume(float vol)
    {
        masterVolume = Mathf.Clamp01(vol);
        if (_ambientSource != null && _ambientSource.isPlaying)
            _ambientSource.volume = ambientVolume * masterVolume;
    }

    // Procedural tone generation

    static AudioClip GenerateTone(float freq, float duration, float amplitude)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        var data = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (t / duration);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * amplitude * envelope;
        }
        var clip = AudioClip.Create("Tone", sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip GenerateTwoTone(float freq1, float freq2, float noteDuration, float amplitude)
    {
        int sampleRate = 44100;
        int noteLen = Mathf.RoundToInt(sampleRate * noteDuration);
        int totalLen = noteLen * 2;
        var data = new float[totalLen];
        for (int i = 0; i < noteLen; i++)
        {
            float t = (float)i / sampleRate;
            float env = 1f - (t / noteDuration);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq1 * t) * amplitude * env;
        }
        for (int i = 0; i < noteLen; i++)
        {
            float t = (float)i / sampleRate;
            float env = 1f - (t / noteDuration);
            data[noteLen + i] = Mathf.Sin(2f * Mathf.PI * freq2 * t) * amplitude * env;
        }
        var clip = AudioClip.Create("TwoTone", totalLen, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip GenerateTripleTone(float f1, float f2, float f3, float noteDuration, float amplitude)
    {
        int sampleRate = 44100;
        int noteLen = Mathf.RoundToInt(sampleRate * noteDuration);
        int totalLen = noteLen * 3;
        var data = new float[totalLen];
        float[] freqs = { f1, f2, f3 };
        for (int n = 0; n < 3; n++)
        {
            for (int i = 0; i < noteLen; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (t / noteDuration);
                data[n * noteLen + i] = Mathf.Sin(2f * Mathf.PI * freqs[n] * t) * amplitude * env;
            }
        }
        var clip = AudioClip.Create("TripleTone", totalLen, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip GenerateAmbientHum(float amplitude)
    {
        int sampleRate = 44100;
        int sampleCount = sampleRate * 5;
        var data = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            data[i] = (Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.3f +
                        Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.2f +
                        Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.1f) * amplitude;
        }
        var clip = AudioClip.Create("AmbientHum", sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
