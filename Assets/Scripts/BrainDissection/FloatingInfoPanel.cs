using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Togglable info panel for VR. Hidden by default.
/// Hold A/B/X/Y on either controller for 2 seconds to show/dismiss.
/// Panel spawns centered at eye level, stays anchored while the user
/// looks around, and only repositions when they turn their body.
/// Keyboard fallback: hold Tab.
/// </summary>
public class FloatingInfoPanel : MonoBehaviour
{
    [Header("Scale")]
    [Range(0.3f, 1.5f)]
    public float panelScale = 0.9f;

    [Header("Position")]
    [Tooltip("Distance from camera in meters.")]
    public float followDistance = 0.6f;

    [Tooltip("Horizontal angle. 0 = centered.")]
    public float horizontalAngle = 0f;

    [Tooltip("Vertical angle. Negative = below eye level.")]
    public float verticalAngle = -3f;

    [Header("Follow")]
    [Tooltip("Degrees user must turn before the panel catches up.")]
    public float reanchorAngle = 55f;

    public float moveSpeed = 4f;
    public float rotateSpeed = 5f;

    [Header("Toggle")]
    [Tooltip("Seconds to hold the button.")]
    public float holdDuration = 2f;

    [Header("Fade")]
    public float fadeInSpeed = 5f;
    public float fadeOutSpeed = 7f;

    [Header("Obstacle Avoidance")]
    public LayerMask obstacleLayers = ~(1 << 5 | 1 << 2);
    public float minDistance = 0.3f;

    Transform _cam;
    CanvasGroup _cg;
    Vector3 _baseScale;
    float _alpha;
    bool _visible;
    float _holdTimer;
    bool _toggledThisPress;
    bool _posReady;
    InputAction _toggleAction;
    Vector3 _anchorDir;
    bool _anchored;
    float _shellRadius;

    void Start()
    {
        _cam = Camera.main?.transform;

        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

        if (transform.localScale.x > 0.0012f)
            transform.localScale = Vector3.one * 0.001f;

        _baseScale = transform.localScale;
        _visible = false;
        _alpha = 0f;
        _anchored = false;

        var rt = GetComponent<RectTransform>();
        _shellRadius = rt != null
            ? rt.sizeDelta.x * _baseScale.x * panelScale * 0.3f
            : 0.15f;

        SyncVisuals();

        _toggleAction = new InputAction("TogglePanel", type: InputActionType.Button);
        _toggleAction.AddBinding("<XRController>/primaryButton");
        _toggleAction.AddBinding("<XRController>/secondaryButton");
        _toggleAction.Enable();

        Debug.Log("[FloatingInfoPanel] Ready. Hold A/B/X/Y for 2s to toggle. Editor: hold Tab.");
    }

    void OnDestroy()
    {
        _toggleAction?.Disable();
        _toggleAction?.Dispose();
    }

    void LateUpdate()
    {
        if (_cam == null)
        {
            _cam = Camera.main?.transform;
            if (_cam == null) return;
        }

        PollToggle();

        float goal = _visible ? 1f : 0f;
        float spd = _visible ? fadeInSpeed : fadeOutSpeed;
        _alpha = Mathf.MoveTowards(_alpha, goal, Time.deltaTime * spd);
        SyncVisuals();

        if (_alpha < 0.01f)
        {
            _posReady = false;
            _anchored = false;
            return;
        }

        FollowCamera();
    }

    void PollToggle()
    {
        bool held = _toggleAction != null && _toggleAction.IsPressed();

        if (Keyboard.current != null && Keyboard.current.tabKey.isPressed)
            held = true;

        if (held)
        {
            _holdTimer += Time.deltaTime;
            if (_holdTimer >= holdDuration && !_toggledThisPress)
            {
                _visible = !_visible;
                _toggledThisPress = true;

                if (_visible && _cam != null)
                {
                    _anchorDir = HorizontalForward();
                    _anchored = true;
                    transform.position = SafePosition(ComputeTarget());
                    transform.rotation = FaceCamera();
                    _posReady = true;
                }

                Debug.Log($"[FloatingInfoPanel] Panel {(_visible ? "SHOWN" : "HIDDEN")}");
            }
        }
        else
        {
            _holdTimer = 0f;
            _toggledThisPress = false;
        }
    }

    void FollowCamera()
    {
        Vector3 currentFwd = HorizontalForward();

        if (!_anchored || Vector3.Angle(_anchorDir, currentFwd) > reanchorAngle)
        {
            _anchorDir = currentFwd;
            _anchored = true;
        }

        Vector3 target = SafePosition(ComputeTarget());

        if (!_posReady)
        {
            transform.position = target;
            transform.rotation = FaceCamera();
            _posReady = true;
            return;
        }

        transform.position = Vector3.Lerp(
            transform.position, target, Time.deltaTime * moveSpeed);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, FaceCamera(), Time.deltaTime * rotateSpeed);
    }

    Vector3 SafePosition(Vector3 ideal)
    {
        Vector3 origin = _cam.position;
        Vector3 toIdeal = ideal - origin;
        float idealDist = toIdeal.magnitude;
        if (idealDist < 0.05f) return ideal;

        Vector3 dir = toIdeal / idealDist;
        float useDist = idealDist;

        float startOffset = _shellRadius + 0.05f;
        if (idealDist > startOffset + 0.05f)
        {
            float castLen = idealDist - startOffset;
            if (Physics.SphereCast(origin + dir * startOffset, _shellRadius, dir,
                    out RaycastHit hit, castLen, obstacleLayers,
                    QueryTriggerInteraction.Ignore))
            {
                useDist = Mathf.Max(startOffset + hit.distance - 0.05f, minDistance);
            }
        }

        Vector3 result = origin + dir * useDist;

        for (int i = 0; i < 4 && Physics.CheckSphere(result, _shellRadius * 0.5f,
                obstacleLayers, QueryTriggerInteraction.Ignore); i++)
        {
            useDist = Mathf.Max(useDist - 0.06f, minDistance);
            result = origin + dir * useDist;
        }

        return result;
    }

    /// <summary>
    /// Target position uses horizontal-only forward so the panel stays at
    /// a fixed eye-level height regardless of where the user is looking.
    /// </summary>
    Vector3 ComputeTarget()
    {
        Vector3 dir = Quaternion.AngleAxis(horizontalAngle, Vector3.up) * _anchorDir;
        Vector3 pos = _cam.position + dir * followDistance;
        pos.y = _cam.position.y +
            followDistance * Mathf.Tan(verticalAngle * Mathf.Deg2Rad);
        return pos;
    }

    Vector3 HorizontalForward()
    {
        Vector3 fwd = _cam.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        return fwd.normalized;
    }

    Quaternion FaceCamera()
    {
        Vector3 look = transform.position - _cam.position;
        if (look.sqrMagnitude < 0.001f) look = _cam.forward;
        return Quaternion.LookRotation(look);
    }

    void SyncVisuals()
    {
        float t = Mathf.SmoothStep(0f, 1f, _alpha);
        _cg.alpha = t;
        transform.localScale = _baseScale * panelScale * Mathf.Max(t, 0.001f);
        _cg.interactable = t > 0.8f;
        _cg.blocksRaycasts = t > 0.8f;
    }

    public void Show()
    {
        _visible = true;
        if (_cam != null)
        {
            _anchorDir = HorizontalForward();
            _anchored = true;
            transform.position = SafePosition(ComputeTarget());
            transform.rotation = FaceCamera();
            _posReady = true;
        }
    }

    public void Hide()
    {
        _visible = false;
    }

    public void SnapToView()
    {
        if (_cam == null) _cam = Camera.main?.transform;
        if (_cam == null) return;
        _anchorDir = HorizontalForward();
        _anchored = true;
        transform.position = SafePosition(ComputeTarget());
        transform.rotation = FaceCamera();
        _posReady = true;
    }
}
