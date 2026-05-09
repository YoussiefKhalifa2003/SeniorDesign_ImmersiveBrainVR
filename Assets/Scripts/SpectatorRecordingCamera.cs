using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.XR.CoreUtils;

// Refresh bump — forces Unity to recompile this file if it was stuck stale.

/// <summary>
/// Spawns a wider "RecordingCamera" under the headset camera for flat captures.
/// In the Editor, renders to an OBS Preview window so you don't have to capture the XR Left Eye Game view.
/// URP: disables XR on this camera so it is not an HMD eye pass.
/// </summary>
[DefaultExecutionOrder(-40)]
public class SpectatorRecordingCamera : MonoBehaviour
{
    public static RenderTexture PreviewTexture { get; private set; }
    public static Camera ActiveRecordingCamera { get; private set; }

    [Header("Mount")]
    [SerializeField] bool autoFindXROrigin = true;

    [Header("Camera")]
    [SerializeField] string cameraObjectName = "RecordingCamera";
    [Tooltip("Default matches headset position. Use small values only if you want a slightly pulled-back view.")]
    [SerializeField] Vector3 localOffsetMeters = Vector3.zero;
    [Tooltip("Recording-only angle offset in degrees. X adjusts pitch; use this to move floating panels up/down in OBS without changing VR.")]
    [SerializeField] Vector3 localEulerOffsetDegrees = Vector3.zero;
    [Tooltip("Wider than HMD for poster-style framing; tune 85–110.")]
    [SerializeField] [Range(60f, 120f)] float fieldOfView = 100f;
    [SerializeField] int cameraDepth = 30;
    [SerializeField] float nearClip = 0.05f;
    [SerializeField] float farClip = 200f;

    [Header("OBS Preview")]
    [SerializeField] bool renderToObsPreview = true;
    [SerializeField] int previewWidth = 1920;
    [SerializeField] int previewHeight = 1080;

    [Header("Platform")]
    [Tooltip("Skip creating the camera on Android builds (saves GPU on device).")]
    [SerializeField] bool skipOnAndroidDevice = true;

    Camera _recording;

    void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (skipOnAndroidDevice)
            return;
#endif
        Transform mount = ResolveMount();
        if (mount == null)
        {
            Debug.LogWarning("[SpectatorRecordingCamera] No XR Origin / camera mount found; disabled.");
            enabled = false;
            return;
        }

        Transform existing = mount.Find(cameraObjectName);
        if (existing != null)
        {
            _recording = existing.GetComponent<Camera>();
            if (_recording == null)
                _recording = existing.gameObject.AddComponent<Camera>();
        }
        else
        {
            var go = new GameObject(cameraObjectName);
            go.transform.SetParent(mount, false);
            _recording = go.AddComponent<Camera>();
        }

        ApplyTransformAndCamera();
        ConfigureUrp();
        ConfigurePreviewTarget();
    }

    Transform ResolveMount()
    {
        if (!autoFindXROrigin)
            return transform;

        XROrigin origin = FindFirstObjectByType<XROrigin>();
        if (origin == null)
            return null;

        // Mount under the actual HMD/Main Camera, not Camera Offset.
        // Camera Offset is floor-level in XR rigs, which makes a child camera look stuck in the ground.
        if (origin.Camera != null)
            return origin.Camera.transform;

        return origin.transform;
    }

    void ApplyTransformAndCamera()
    {
        Transform t = _recording.transform;
        t.localPosition = localOffsetMeters;
        t.localRotation = Quaternion.Euler(localEulerOffsetDegrees);
        t.localScale = Vector3.one;

        _recording.fieldOfView = fieldOfView;
        _recording.nearClipPlane = nearClip;
        _recording.farClipPlane = farClip;
        _recording.depth = cameraDepth;
        _recording.clearFlags = CameraClearFlags.Skybox;
        _recording.tag = "Untagged";
        _recording.stereoTargetEye = StereoTargetEyeMask.None;

        if (_recording.GetComponent<AudioListener>() != null)
            Destroy(_recording.GetComponent<AudioListener>());
    }

    void ConfigureUrp()
    {
        UniversalAdditionalCameraData data = _recording.GetUniversalAdditionalCameraData();
        data.renderType = CameraRenderType.Base;
        data.allowXRRendering = false;
    }

    void ConfigurePreviewTarget()
    {
        ActiveRecordingCamera = _recording;

        if (!renderToObsPreview)
        {
            _recording.targetTexture = null;
            return;
        }

        int width = Mathf.Max(640, previewWidth);
        int height = Mathf.Max(360, previewHeight);
        if (PreviewTexture == null || PreviewTexture.width != width || PreviewTexture.height != height)
        {
            if (PreviewTexture != null)
                PreviewTexture.Release();

            PreviewTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "OBS Recording Camera Preview"
            };
            PreviewTexture.Create();
        }

        _recording.targetTexture = PreviewTexture;
    }

    void OnDestroy()
    {
        if (ActiveRecordingCamera == _recording)
            ActiveRecordingCamera = null;
    }

    void OnValidate()
    {
        if (_recording == null)
            return;

        ApplyTransformAndCamera();
        ConfigurePreviewTarget();
    }

#if UNITY_EDITOR
    [ContextMenu("Debug: Log camera path")]
    void DebugLogPath()
    {
        if (_recording == null)
            Awake();
        if (_recording != null)
            Debug.Log($"[SpectatorRecordingCamera] Recording camera: {GetPath(_recording.transform)}", this);
    }

    static string GetPath(Transform t)
    {
        if (t.parent == null)
            return t.name;
        return GetPath(t.parent) + "/" + t.name;
    }
#endif
}
