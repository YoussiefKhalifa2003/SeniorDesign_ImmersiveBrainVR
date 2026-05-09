using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Diagnostics;
using System.IO;

/// <summary>
/// Windows Text-to-Speech that plays through Unity's audio system.
///
/// Uses Windows SAPI to render speech into a WAV file, then loads
/// that WAV as an AudioClip and plays it through an AudioSource.
/// Because the AudioSource lives in Unity, audio is routed to
/// whatever device Unity uses -- which is the VR headset in XR.
///
/// Call Speak() from anywhere. Each new call stops previous speech.
/// Call Stop() to silence immediately.
/// </summary>
public class TextToSpeech : MonoBehaviour
{
    public static TextToSpeech Instance { get; private set; }

    AudioSource _src;
    Process _proc;
    string _vbsPath;
    string _wavPath;
    Coroutine _routine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _src = gameObject.AddComponent<AudioSource>();
        _src.spatialBlend = 0f;
        _src.volume = 1f;
        _src.playOnAwake = false;

        _vbsPath = Path.Combine(Application.temporaryCachePath, "tts.vbs");
        _wavPath = Path.Combine(Application.temporaryCachePath, "tts_out.wav");
    }

    static void EnsureInstance()
    {
        if (Instance != null) return;
        var go = new GameObject("TextToSpeech");
        go.AddComponent<TextToSpeech>();
    }

    /// <param name="text">Text to speak.</param>
    /// <param name="rate">SAPI rate: -10 (slowest) to 10 (fastest). 1 = slightly fast.</param>
    public static void Speak(string text, int rate = 1)
    {
        EnsureInstance();
        Instance.InternalSpeak(text, rate, null);
    }

    /// <param name="onComplete">Invoked after speech finishes playing (or is interrupted).</param>
    public static void Speak(string text, int rate, System.Action onComplete)
    {
        EnsureInstance();
        Instance.InternalSpeak(text, rate, onComplete);
    }

    public static void Stop()
    {
        if (Instance != null) Instance.InternalStop();
    }

    public static bool IsSpeaking =>
        Instance != null && Instance._src != null && Instance._src.isPlaying;

    System.Action _onComplete;

    void InternalSpeak(string text, int rate, System.Action onComplete)
    {
        _onComplete = null; // discard old callback when interrupted by a new speak
        InternalStop();
        _onComplete = onComplete;
        if (string.IsNullOrEmpty(text)) { onComplete?.Invoke(); return; }
        _routine = StartCoroutine(SpeakCoroutine(text, rate));
    }

    void InternalStop()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        KillProcess();
        if (_src != null) _src.Stop();
        var cb = _onComplete;
        _onComplete = null;
        cb?.Invoke();
    }

    void KillProcess()
    {
        try { if (_proc != null && !_proc.HasExited) _proc.Kill(); }
        catch { }
        _proc = null;
    }

    IEnumerator SpeakCoroutine(string text, int rate)
    {
        string safe = text
            .Replace("\r", "")
            .Replace("\n", ". ")
            .Replace("\"", "'")
            .Replace(">>", "");

        if (File.Exists(_wavPath))
            try { File.Delete(_wavPath); } catch { }

        string script =
            "Set voice = CreateObject(\"SAPI.SpVoice\")\n" +
            "Set stream = CreateObject(\"SAPI.SpFileStream\")\n" +
            "stream.Open \"" + _wavPath + "\", 3\n" +
            "Set voice.AudioOutputStream = stream\n" +
            "voice.Rate = " + rate + "\n" +
            "voice.Speak \"" + safe + "\"\n" +
            "stream.Close\n";

        File.WriteAllText(_vbsPath, script);

        _proc = new Process();
        _proc.StartInfo = new ProcessStartInfo
        {
            FileName = "wscript.exe",
            Arguments = "\"" + _vbsPath + "\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try { _proc.Start(); }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("[TTS] Could not start: " + e.Message);
            yield break;
        }

        while (_proc != null && !_proc.HasExited)
            yield return new WaitForSeconds(0.05f);

        yield return new WaitForSeconds(0.05f);

        if (!File.Exists(_wavPath))
        {
            UnityEngine.Debug.LogWarning("[TTS] WAV file not found after synthesis.");
            yield break;
        }

        string url = "file:///" + _wavPath.Replace("\\", "/");

        using (var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip != null && _src != null)
                {
                    _src.clip = clip;
                    _src.Play();
                    UnityEngine.Debug.Log("[TTS] Playing through Unity AudioSource (" +
                        clip.length.ToString("F1") + "s).");
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("[TTS] WAV load error: " + req.error);
            }
        }

        while (_src != null && _src.isPlaying)
            yield return null;

        var cb = _onComplete;
        _onComplete = null;
        cb?.Invoke();
    }

    void OnDestroy()
    {
        InternalStop();
    }
}
