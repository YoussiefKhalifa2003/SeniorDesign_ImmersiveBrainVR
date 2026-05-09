using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns blue hand meshes that visually follow the VR controllers.
/// Does NOT parent anything to the XR rig. Does NOT modify the controllers.
/// Does NOT require hand tracking.
///
/// Works by copying controller position/rotation every frame in LateUpdate.
/// The hand meshes are completely independent root-level GameObjects.
/// </summary>
public class BlueHandVisuals : MonoBehaviour
{
    [Header("Hand Mesh Prefabs (set by editor setup)")]
    public GameObject leftHandPrefab;
    public GameObject rightHandPrefab;

    [Header("Appearance")]
    public Color gloveColor = new Color(0.22f, 0.42f, 0.85f, 1f);

    [Header("Offset Tweaks (adjust in Inspector if hands look wrong)")]
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 leftRotationOffset = new Vector3(0, 0, 0);
    public Vector3 rightRotationOffset = new Vector3(0, 0, 0);
    public float handScale = 1f;

    // Runtime instances
    private GameObject _leftHand;
    private GameObject _rightHand;
    private Transform _leftController;
    private Transform _rightController;
    private bool _initialized;
    private LabToolManager _subscribedManager;

    private void Start()
    {
        // Delay to let XR rig fully initialize
        StartCoroutine(InitializeDelayed());
    }

    private void OnDisable()
    {
        UnsubscribeFromManager();
    }

    private void OnDestroy()
    {
        UnsubscribeFromManager();
    }

    private IEnumerator InitializeDelayed()
    {
        // Wait 1 second for XR to finish setting up
        yield return new WaitForSeconds(1.0f);

        FindControllers();

        if (_leftController == null || _rightController == null)
        {
            Debug.LogWarning("[BlueHandVisuals] Controllers not found after 1s, retrying in 2s...");
            yield return new WaitForSeconds(2.0f);
            FindControllers();
        }

        if (leftHandPrefab != null && _leftController != null)
        {
            _leftHand = SpawnHand(leftHandPrefab, "LeftBlueGlove", true);
            Debug.Log("[BlueHandVisuals] Left blue glove spawned and positioned at: " + _leftController.position);
        }
        else
        {
            Debug.LogWarning("[BlueHandVisuals] Could not spawn left hand. " +
                $"Prefab: {(leftHandPrefab != null ? "OK" : "MISSING")}, " +
                $"Controller: {(_leftController != null ? "OK" : "MISSING")}");
        }

        if (rightHandPrefab != null && _rightController != null)
        {
            _rightHand = SpawnHand(rightHandPrefab, "RightBlueGlove", false);
            Debug.Log("[BlueHandVisuals] Right blue glove spawned and positioned at: " + _rightController.position);
        }
        else
        {
            Debug.LogWarning("[BlueHandVisuals] Could not spawn right hand. " +
                $"Prefab: {(rightHandPrefab != null ? "OK" : "MISSING")}, " +
                $"Controller: {(_rightController != null ? "OK" : "MISSING")}");
        }

        // Wait one more frame for Destroy() to process, then force colors
        yield return null;
        yield return null;

        // Re-apply blue color and force-enable renderers after scripts are destroyed
        if (_leftHand != null) ForceBlueAndEnable(_leftHand);
        if (_rightHand != null) ForceBlueAndEnable(_rightHand);

        // Hide until the player explicitly equips gloves at the tool table.
        if (_leftHand != null) _leftHand.SetActive(false);
        if (_rightHand != null) _rightHand.SetActive(false);

        _initialized = true;
        SubscribeToManager();
        ApplyVisibility();
        Debug.Log("[BlueHandVisuals] Initialization complete. Hands will now follow controllers.");
    }

    private void SubscribeToManager()
    {
        var mgr = LabToolManager.Instance;
        if (mgr == null || mgr == _subscribedManager) return;

        _subscribedManager = mgr;
        mgr.OnGlovesEquipped += ApplyVisibility;
        mgr.OnGlovesUnequipped += ApplyVisibility;
        mgr.OnLabReset += ApplyVisibility;
    }

    private void UnsubscribeFromManager()
    {
        if (_subscribedManager == null) return;
        _subscribedManager.OnGlovesEquipped -= ApplyVisibility;
        _subscribedManager.OnGlovesUnequipped -= ApplyVisibility;
        _subscribedManager.OnLabReset -= ApplyVisibility;
        _subscribedManager = null;
    }

    /// <summary>Show the blue gloves only when LabToolManager has gloves equipped.</summary>
    private void ApplyVisibility()
    {
        bool show = LabToolManager.Instance != null && LabToolManager.Instance.glovesEquipped;
        if (_leftHand != null) _leftHand.SetActive(show);
        if (_rightHand != null) _rightHand.SetActive(show);
    }

    private void LateUpdate()
    {
        if (!_initialized) return;

        if (_subscribedManager == null && LabToolManager.Instance != null)
        {
            SubscribeToManager();
            ApplyVisibility();
        }

        // Match left hand to left controller
        if (_leftHand != null && _leftController != null)
        {
            _leftHand.transform.position = _leftController.position +
                _leftController.TransformDirection(positionOffset);
            _leftHand.transform.rotation = _leftController.rotation *
                Quaternion.Euler(leftRotationOffset);
        }

        // Match right hand to right controller
        if (_rightHand != null && _rightController != null)
        {
            _rightHand.transform.position = _rightController.position +
                _rightController.TransformDirection(positionOffset);
            _rightHand.transform.rotation = _rightController.rotation *
                Quaternion.Euler(rightRotationOffset);
        }
    }

    // ===================== FINDING CONTROLLERS =====================

    private void FindControllers()
    {
        Camera cam = Camera.main;
        if (cam == null) { Debug.LogWarning("[BlueHandVisuals] No main camera."); return; }

        Transform cameraOffset = cam.transform.parent;
        if (cameraOffset == null)
        {
            Debug.LogWarning("[BlueHandVisuals] Main camera has no parent (Camera Offset).");
            return;
        }

        // Search all children of Camera Offset for controllers
        foreach (Transform child in cameraOffset)
        {
            string n = child.name.ToLower();
            if (_leftController == null && n.Contains("left") && n.Contains("controller"))
            {
                _leftController = child;
                Debug.Log($"[BlueHandVisuals] Found left controller: {child.name}");
            }
            if (_rightController == null && n.Contains("right") && n.Contains("controller"))
            {
                _rightController = child;
                Debug.Log($"[BlueHandVisuals] Found right controller: {child.name}");
            }
        }

        if (_leftController == null)
            Debug.LogWarning("[BlueHandVisuals] Left controller NOT found under Camera Offset.");
        if (_rightController == null)
            Debug.LogWarning("[BlueHandVisuals] Right controller NOT found under Camera Offset.");
    }

    // ===================== SPAWNING HANDS =====================

    private GameObject SpawnHand(GameObject prefab, string handName, bool isLeft)
    {
        // Destroy any existing instance with same name
        var old = GameObject.Find(handName);
        if (old != null) Destroy(old);

        // Instantiate as a ROOT object (not child of anything in the XR rig)
        // Temporarily disable Unity logger to suppress Awake()-time warnings from
        // XR affordance scripts that expect interactor components we're about to strip.
        bool logWasEnabled = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = false;
        var hand = Instantiate(prefab);
        Debug.unityLogger.logEnabled = logWasEnabled;
        hand.name = handName;

        // ---- STRIP ALL NON-VISUAL COMPONENTS ----
        // Use Destroy() (not DestroyImmediate) to avoid dependency errors.
        // Destroy() defers to end of frame, bypassing RequireComponent checks.
        StripAllScriptsAndPhysics(hand);

        // Scale (mirror X for left hand)
        float s = handScale;
        hand.transform.localScale = isLeft
            ? new Vector3(-s, s, s)
            : new Vector3(s, s, s);

        // Set initial position to controller
        Transform ctrl = isLeft ? _leftController : _rightController;
        if (ctrl != null)
        {
            hand.transform.position = ctrl.position;
            hand.transform.rotation = ctrl.rotation;
        }

        // Apply blue material immediately
        ApplyFreshBlueMaterial(hand);

        return hand;
    }

    /// <summary>
    /// Removes ALL scripts, physics, animators from the hand mesh.
    /// Uses Destroy() (deferred) instead of DestroyImmediate() to avoid
    /// RequireComponent dependency errors that pollute the console.
    /// Destroy() queues removal for end of frame, bypassing dependency checks.
    /// </summary>
    private void StripAllScriptsAndPhysics(GameObject go)
    {
        // Destroy Rigidbodies
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
            if (rb != null) Destroy(rb);

        // Destroy Colliders
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            if (col != null) Destroy(col);

        // Destroy Animators
        foreach (var anim in go.GetComponentsInChildren<Animator>(true))
            if (anim != null) Destroy(anim);

        // Destroy Animation (legacy)
        foreach (var anim in go.GetComponentsInChildren<Animation>(true))
            if (anim != null) Destroy(anim);

        // Destroy ALL MonoBehaviours (except this script)
        // Destroy() is deferred so dependency order doesn't matter.
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null || mb == this) continue;
            Destroy(mb);
        }
    }

    /// <summary>
    /// Creates a brand new blue material and replaces ALL materials on all renderers.
    /// This ensures no old shader / affordance system can override the color.
    /// </summary>
    private void ApplyFreshBlueMaterial(GameObject hand)
    {
        // Create a new simple material
        Material blueMat = CreateBlueMaterial();

        foreach (var rend in hand.GetComponentsInChildren<Renderer>(true))
        {
            rend.enabled = true;
            // Activate the GameObject too in case it was disabled
            rend.gameObject.SetActive(true);

            // Replace ALL material slots with our blue material
            var mats = new Material[rend.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = blueMat;
            rend.sharedMaterials = mats;
        }
    }

    /// <summary>
    /// Called after Destroy() has processed (next frame) to re-apply blue
    /// and re-enable renderers, in case the dying scripts disabled anything
    /// in their OnDestroy/OnDisable.
    /// </summary>
    private void ForceBlueAndEnable(GameObject hand)
    {
        if (hand == null) return;
        Material blueMat = CreateBlueMaterial();
        foreach (var rend in hand.GetComponentsInChildren<Renderer>(true))
        {
            if (rend == null) continue;
            rend.enabled = true;
            rend.gameObject.SetActive(true);
            var mats = new Material[rend.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = blueMat;
            rend.sharedMaterials = mats;
        }
    }

    /// <summary>Creates a fresh blue URP Lit (or Standard) material.</summary>
    private Material CreateBlueMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");

        Material mat = new Material(shader);
        mat.name = "BlueGloveRuntime";
        mat.color = gloveColor;

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", gloveColor);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color", gloveColor);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.7f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.7f);
        if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", 0f);

        return mat;
    }
}
