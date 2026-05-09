using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;

/// <summary>
/// Menu: Tools > Brain Dissection > Setup Scene
/// Creates BrainSystem, lab tools, professional world-space UI, wires everything.
/// Safe to re-run (skips or rebuilds existing objects).
/// </summary>
public static class BrainDissectionSceneSetup
{
    // ========================= CONSTANTS =========================
    const string BrainRootName  = "BrainRoot";
    const string LeftHemiName   = "Allen_brain_Hemisphere_L";
    const string RightHemiName  = "Allen_brain_Hemisphere_R";

    // ========================= COLORS =========================
    static readonly Color PanelBg       = new Color(0.06f, 0.06f, 0.10f, 0.94f);
    static readonly Color BtnBlue       = new Color(0.18f, 0.35f, 0.62f, 1f);
    static readonly Color BtnBlueBright = new Color(0.25f, 0.45f, 0.75f, 1f);
    static readonly Color BtnRed        = new Color(0.60f, 0.15f, 0.15f, 1f);
    static readonly Color BtnGreen      = new Color(0.12f, 0.50f, 0.22f, 1f);
    static readonly Color BtnOrange     = new Color(0.70f, 0.45f, 0.10f, 1f);
    static readonly Color AccentBlue    = new Color(0.3f, 0.6f, 1f, 0.8f);
    static readonly Color TextWhite     = new Color(0.95f, 0.95f, 0.97f, 1f);
    static readonly Color TextDim       = new Color(0.70f, 0.70f, 0.75f, 1f);
    static readonly Color TextGreen     = new Color(0.45f, 0.85f, 0.45f, 1f);
    static readonly Color TableColor    = new Color(0.25f, 0.20f, 0.16f, 1f);

    static Font GetFont()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    // ========================= ENTRY POINT =========================

    [MenuItem("Tools/Brain Dissection/Setup Scene")]
    public static void SetupScene()
    {
        // ---- 1. BrainRoot (NEVER touch position if it exists) ----
        var brainRoot = GameObject.Find(BrainRootName);
        if (brainRoot == null)
        {
            brainRoot = new GameObject(BrainRootName);
            brainRoot.transform.position = new Vector3(0, 1, 2);
            EditorUtility.DisplayDialog("Brain Root Created",
                "An empty 'BrainRoot' was created.\n\n" +
                "Drag Allen_brain_final from Assets onto BrainRoot in the Hierarchy.\n" +
                "Then run Tools > Brain Dissection > Setup Scene again.", "OK");
            EditorSceneManager.MarkSceneDirty(brainRoot.scene);
            return;
        }
        // DO NOT change brainRoot position/scale -- user may have placed it on the tray

        EnsureKinematic(brainRoot);
        if (brainRoot.GetComponent<BrainPhysicsLock>() == null)
            brainRoot.AddComponent<BrainPhysicsLock>();
        EnsureComp<BrainRotator>(brainRoot);
        foreach (var rb in brainRoot.GetComponentsInChildren<Rigidbody>(true))
        { rb.isKinematic = true; rb.useGravity = false; }

        // Find hemispheres
        Transform leftHemi = null, rightHemi = null;
        foreach (Transform t in brainRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == LeftHemiName) leftHemi = t;
            if (t.name == RightHemiName) rightHemi = t;
        }

        // ---- 2. BrainSystem + BrainManager ----
        var brainSystem = FindOrCreate("BrainSystem");
        var bm = EnsureComp<BrainManager>(brainSystem);
        bm.leftHemisphere  = leftHemi  != null ? leftHemi.gameObject  : null;
        bm.rightHemisphere = rightHemi != null ? rightHemi.gameObject : null;
        bm.brainRoot = brainRoot;

        // Wire KidneyTray reference
        var kidneyTrayGO = GameObject.Find("KidneyTray");
        if (kidneyTrayGO == null)
        {
            // Also try common child names under operating_room
            var opRoomGO = GameObject.Find("operating_room");
            if (opRoomGO != null)
            {
                foreach (var t in opRoomGO.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.IndexOf("KidneyTray", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        t.name.IndexOf("Kidney_Tray", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        kidneyTrayGO = t.gameObject;
                        break;
                    }
                }
            }
        }
        if (kidneyTrayGO != null)
        {
            bm.kidneyTray = kidneyTrayGO.transform;
            Debug.Log($"[Brain Dissection] KidneyTray found and assigned: {kidneyTrayGO.name}");
        }
        else
        {
            Debug.LogWarning("[Brain Dissection] KidneyTray not found in scene. " +
                "Hemispheres will use fallback separation on split.");
        }

        var rotator = brainRoot.GetComponent<BrainRotator>();
        if (rotator != null) rotator.brainManager = bm;

        // ---- 3. LabToolManager ----
        var ltm = EnsureComp<LabToolManager>(brainSystem);
        ltm.brainManager = bm;

        // ---- 4. GloveEquipper ----
        var ge = EnsureComp<GloveEquipper>(brainSystem);
        var gloveFBX = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MedicalGloves.fbx");
        if (gloveFBX != null) ge.glovePrefab = gloveFBX;
        ltm.gloveEquipper = ge;

        // ---- 5. RegionUIController ----
        var uiCtrl = Object.FindFirstObjectByType<RegionUIController>();
        if (uiCtrl == null)
        {
            var go = new GameObject("RegionUIController");
            go.transform.SetParent(brainSystem.transform);
            uiCtrl = go.AddComponent<RegionUIController>();
        }
        bm.regionUIController = uiCtrl;
        ltm.regionUIController = uiCtrl;

        // ---- 6. Canvas + UI ----
        var canvasGO = FindOrCreateCanvas();
        EnsureComp<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>(canvasGO);
        var floatPanel = EnsureComp<FloatingInfoPanel>(canvasGO);
        EnsureComp<CanvasGroup>(canvasGO);
        canvasGO.GetComponent<RectTransform>().localScale = Vector3.one * 0.001f;

        floatPanel.panelScale = 0.9f;
        floatPanel.followDistance = 0.6f;
        floatPanel.horizontalAngle = 0f;
        floatPanel.verticalAngle = -3f;
        floatPanel.reanchorAngle = 55f;
        floatPanel.moveSpeed = 4f;
        floatPanel.rotateSpeed = 5f;
        floatPanel.holdDuration = 2f;
        floatPanel.minDistance = 0.3f;
        EditorUtility.SetDirty(canvasGO);

        var uiBridge = EnsureComp<BrainDissectionUI>(canvasGO);
        uiBridge.brainManager = bm;

        // Always rebuild UI children for consistency
        DestroyAllChildren(canvasGO.transform);
        BuildUI(canvasGO, bm, uiCtrl, uiBridge);

        // ---- 7. Tool Table + Tools ----
        SetupToolTable(ltm);

        // ---- 8. Cut Zone ----
        SetupCutZone(brainRoot, ltm);

        // ---- 9. Blue Hand Visuals (follow controllers, no hand tracking needed) ----
        SetupBlueHandVisuals(brainSystem);

        // ---- 10. Operating Room Collision Fix ----
        var opRoom = GameObject.Find("operating_room");
        if (opRoom != null)
        {
            EnsureComp<OperatingRoomCollisionFix>(opRoom);
            Debug.Log("[Brain Dissection] Operating room collision fix added.");
        }

        // ---- 10b. SessionLogger (JSON session storage) ----
        EnsureComp<SessionLogger>(brainSystem);
        Debug.Log("[Brain Dissection] SessionLogger added.");

        // ---- 10c. TaskTimerManager (per-task timing) ----
        EnsureComp<TaskTimerManager>(brainSystem);
        Debug.Log("[Brain Dissection] TaskTimerManager added.");

        // ---- 10d. WorldSpaceHoverLabel (floating region name near hovered region) ----
        EnsureComp<WorldSpaceHoverLabel>(brainSystem);
        Debug.Log("[Brain Dissection] WorldSpaceHoverLabel added.");

        // ---- 10e. Anatomy Layer Service + UI Panel ----
        EnsureComp<AnatomyLayerService>(brainSystem);
        EnsureComp<AnatomyLayerPanel>(brainSystem);
        EnsureComp<PlayRegionSearchController>(brainSystem);
        EnsureComp<HandWashStation>(brainSystem);

        // Quest passthrough toggle has been retired (unreliable over Link/standalone).
        // Strip any leftover component from prior setups so the dead panel never spawns.
        var legacyPassthrough = brainSystem.GetComponent<QuestPassthroughToggle>();
        if (legacyPassthrough != null) Object.DestroyImmediate(legacyPassthrough);
        Debug.Log("[Brain Dissection] AnatomyLayerService + Panel + HandWashStation added.");

        // ---- 10f. Exploded View Controller ----
        EnsureComp<ExplodedViewController>(brainSystem);
        Debug.Log("[Brain Dissection] ExplodedViewController added.");

        // ---- 11. Allow Grip-to-rotate (activate on hover) ----
        EnableHoveredActivateOnInteractors();

        // ---- 12. EventSystem ----
        CheckEventSystem();

        EditorUtility.SetDirty(brainSystem);
        EditorSceneManager.MarkSceneDirty(brainSystem.scene);
        Debug.Log("[Brain Dissection] Setup complete. Tools, cut zone, hand visuals, and UI ready.");
    }

    // ========================= TOOL TABLE =========================

    static void SetupToolTable(LabToolManager ltm)
    {
        // PRESERVE existing objects if they exist. Only create what's missing.
        // This prevents resetting positions/scales the user manually adjusted.

        var table = GameObject.Find("ToolTable");
        if (table == null)
        {
            table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "ToolTable";
            table.transform.position = new Vector3(0.6f, 0.75f, 1.0f);
            table.transform.localScale = new Vector3(0.8f, 0.03f, 0.4f);
            var tableRend = table.GetComponent<Renderer>();
            if (tableRend != null && tableRend.sharedMaterial != null)
            {
                var tableMat = new Material(tableRend.sharedMaterial);
                tableMat.color = TableColor;
                tableRend.material = tableMat;
            }
            EnsureKinematic(table);
            Debug.Log("[Brain Dissection] ToolTable created.");
        }
        else
        {
            Debug.Log("[Brain Dissection] ToolTable already exists, keeping position/scale.");
        }

        // Load tool FBX models
        var gloveFBX = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MedicalGloves.fbx");
        var knifeFBX = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SurgicalKnife.fbx");
        var tweezersFBX = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/tweezers.fbx");

        float tableTop = 0.78f; // default position (only used for NEW objects)

        // ----- Gloves -----
        var existingGloves = GameObject.Find("LabGloves");
        if (existingGloves == null && gloveFBX != null)
        {
            var gloves = (GameObject)PrefabUtility.InstantiatePrefab(gloveFBX);
            gloves.name = "LabGloves";
            gloves.transform.position = new Vector3(0.35f, tableTop, 1.0f);
            SetupClickableTool(gloves, LabTool.ToolType.Gloves);
            AddToolLabel(gloves, "Gloves", new Vector3(0, 0.08f, 0));
        }
        else if (existingGloves != null)
        {
            // Ensure components exist but don't touch transform
            SetupClickableTool(existingGloves, LabTool.ToolType.Gloves);
            Debug.Log("[Brain Dissection] LabGloves already exists, keeping position/scale.");
        }

        // ----- Knife -----
        var existingKnife = GameObject.Find("LabKnife");
        if (existingKnife == null && knifeFBX != null)
        {
            var knife = (GameObject)PrefabUtility.InstantiatePrefab(knifeFBX);
            knife.name = "LabKnife";
            knife.transform.position = new Vector3(0.6f, tableTop, 1.0f);
            SetupClickableTool(knife, LabTool.ToolType.Knife);
            AddToolLabel(knife, "Dissection Knife", new Vector3(0, 0.08f, 0));
        }
        else if (existingKnife != null)
        {
            SetupClickableTool(existingKnife, LabTool.ToolType.Knife);
            Debug.Log("[Brain Dissection] LabKnife already exists, keeping position/scale.");
        }

        // ----- Tweezers -----
        var existingTweezers = GameObject.Find("LabTweezers");
        if (existingTweezers == null && tweezersFBX != null)
        {
            var tweezers = (GameObject)PrefabUtility.InstantiatePrefab(tweezersFBX);
            tweezers.name = "LabTweezers";
            tweezers.transform.position = new Vector3(0.85f, tableTop, 1.0f);
            SetupClickableTool(tweezers, LabTool.ToolType.Tweezers);
            AddToolLabel(tweezers, "Tweezers", new Vector3(0, 0.08f, 0));
        }
        else if (existingTweezers != null)
        {
            SetupClickableTool(existingTweezers, LabTool.ToolType.Tweezers);
            Debug.Log("[Brain Dissection] LabTweezers already exists, keeping position/scale.");
        }

        Debug.Log("[Brain Dissection] Tool table setup complete.");
    }

    /// <summary>
    /// Setup for gloves: XRSimpleInteractable (no grab, no physics).
    /// User just points and clicks to equip. Zero camera interference.
    /// </summary>
    static void SetupClickableTool(GameObject tool, LabTool.ToolType type)
    {
        // Collider only (NO Rigidbody -- no physics at all)
        FitBoxCollider(tool);

        // XRSimpleInteractable: fires select events without grab/move physics
        EnsureComp<XRSimpleInteractable>(tool);

        // LabTool component
        var lt = EnsureComp<LabTool>(tool);
        lt.toolType = type;
    }

    /// <summary>
    /// Setup for knife / tweezers: XRGrabInteractable (user physically holds them).
    /// </summary>
    static void SetupGrabbableTool(GameObject tool, LabTool.ToolType type)
    {
        // Rigidbody (kinematic so it doesn't fall)
        var rb = EnsureComp<Rigidbody>(tool);
        rb.isKinematic = true;
        rb.useGravity = false;

        // Box Collider (fitted to mesh bounds)
        FitBoxCollider(tool);

        // XR Grab Interactable
        var grab = EnsureComp<XRGrabInteractable>(tool);
        grab.throwOnDetach = false;

        // LabTool component
        var lt = EnsureComp<LabTool>(tool);
        lt.toolType = type;
    }

    static void AddToolLabel(GameObject tool, string text, Vector3 offset)
    {
        // Small 3D TextMesh label floating above each tool
        var labelGO = new GameObject("Label_" + text);
        labelGO.transform.SetParent(tool.transform, false);
        labelGO.transform.localPosition = offset;

        var tm = labelGO.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 24;
        tm.characterSize = 0.012f;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = TextWhite;
        tm.font = GetFont();

        // Make label face the user (billboard effect at runtime would need a script,
        // but for a static label, facing -Z is fine since user approaches from that side)
        labelGO.transform.localRotation = Quaternion.identity;
    }

    static void FitBoxCollider(GameObject go)
    {
        if (go.GetComponent<BoxCollider>() != null) return; // already has one

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.size = Vector3.one * 0.05f;
            return;
        }

        Bounds world = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            world.Encapsulate(renderers[i].bounds);

        var col = go.AddComponent<BoxCollider>();
        col.center = go.transform.InverseTransformPoint(world.center);
        Vector3 ls = go.transform.lossyScale;
        col.size = new Vector3(
            world.size.x / Mathf.Max(Mathf.Abs(ls.x), 0.001f),
            world.size.y / Mathf.Max(Mathf.Abs(ls.y), 0.001f),
            world.size.z / Mathf.Max(Mathf.Abs(ls.z), 0.001f)
        );
    }

    // ========================= CUT ZONE =========================

    static void SetupCutZone(GameObject brainRoot, LabToolManager ltm)
    {
        // PRESERVE existing BrainCutZone if it exists (user may have repositioned/scaled it).
        var existing = brainRoot.transform.Find("BrainCutZone");
        if (existing != null)
        {
            // Just ensure components are wired up, don't touch position/scale
            var czComp = EnsureComp<BrainCutZone>(existing.gameObject);
            ltm.cutZone = czComp;

            // Ensure CutGuide line uses local space
            var guideT = existing.Find("CutGuide");
            if (guideT != null)
            {
                var lr = guideT.GetComponent<LineRenderer>();
                if (lr != null) lr.useWorldSpace = false;
                czComp.cutGuide = lr;
            }

            Debug.Log("[Brain Dissection] BrainCutZone already exists, keeping position/scale.");
            return;
        }

        // --- Only create fresh if nothing exists ---

        // Compute brain bounds in WORLD space
        Bounds worldBounds = new Bounds(brainRoot.transform.position, Vector3.one * 0.1f);
        bool hasBounds = false;
        foreach (var r in brainRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (!hasBounds) { worldBounds = r.bounds; hasBounds = true; }
            else worldBounds.Encapsulate(r.bounds);
        }

        var cutZoneGO = new GameObject("BrainCutZone");
        cutZoneGO.transform.SetParent(brainRoot.transform, true);
        cutZoneGO.transform.position = worldBounds.center;
        cutZoneGO.transform.rotation = brainRoot.transform.rotation;

        var bc = cutZoneGO.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.center = Vector3.zero;

        Vector3 ls = cutZoneGO.transform.lossyScale;
        float safeX = Mathf.Max(Mathf.Abs(ls.x), 0.001f);
        float safeY = Mathf.Max(Mathf.Abs(ls.y), 0.001f);
        float safeZ = Mathf.Max(Mathf.Abs(ls.z), 0.001f);

        float worldH = hasBounds ? worldBounds.size.y * 1.2f : 0.3f;
        float worldD = hasBounds ? worldBounds.size.z * 1.2f : 0.3f;

        bc.size = new Vector3(
            0.02f / safeX,
            worldH / safeY,
            worldD / safeZ
        );

        var czComp2 = cutZoneGO.AddComponent<BrainCutZone>();
        ltm.cutZone = czComp2;

        // Visual cut guide (red line) -- LOCAL SPACE so it moves with the brain
        var guideGO = new GameObject("CutGuide");
        guideGO.transform.SetParent(cutZoneGO.transform, false);
        guideGO.transform.localPosition = Vector3.zero;

        var lr2 = guideGO.AddComponent<LineRenderer>();
        lr2.useWorldSpace = false;
        lr2.startWidth = 0.005f;
        lr2.endWidth = 0.005f;
        lr2.startColor = Color.red;
        lr2.endColor = Color.red;
        lr2.material = new Material(Shader.Find("Sprites/Default"));
        lr2.positionCount = 2;

        float localHalfH = (worldH * 0.5f) / Mathf.Max(Mathf.Abs(cutZoneGO.transform.lossyScale.y), 0.001f);
        lr2.SetPosition(0, new Vector3(0, -localHalfH, 0));
        lr2.SetPosition(1, new Vector3(0, localHalfH, 0));

        czComp2.cutGuide = lr2;

        Debug.Log($"[Brain Dissection] Cut zone created at brain center: {worldBounds.center}, " +
                  $"brain bounds size: {worldBounds.size}");
    }

    // ========================= BLUE HAND VISUALS =========================

    /// <summary>
    /// Adds BlueHandVisuals component to BrainSystem and assigns the Quest hand
    /// visual prefabs. At runtime, the component spawns blue hand meshes that
    /// follow the controllers via LateUpdate (NO parenting to the XR rig).
    ///
    /// This approach works on ALL headsets -- no hand tracking required.
    /// Also cleans up any old LeftHandBlueGlove / RightHandBlueGlove objects.
    /// </summary>
    static void SetupBlueHandVisuals(GameObject brainSystem)
    {
        // Clean up old XR Hand approach if present
        CleanupOldHandVisuals();

        // Add BlueHandVisuals component
        var bhv = EnsureComp<BlueHandVisuals>(brainSystem);

        // Find hand prefabs
        GameObject leftPrefab = FindHandPrefab("Left");
        GameObject rightPrefab = FindHandPrefab("Right");

        if (leftPrefab != null) bhv.leftHandPrefab = leftPrefab;
        if (rightPrefab != null) bhv.rightHandPrefab = rightPrefab;

        if (leftPrefab == null || rightPrefab == null)
        {
            Debug.LogWarning(
                "[Brain Dissection] Could not find hand visual prefabs.\n" +
                "Blue hands won't appear. To fix:\n" +
                "  Window > Package Manager > XR Interaction Toolkit > Samples > " +
                "Hands Interaction Demo > Import\n" +
                "Then run Setup Scene again.");
        }
        else
        {
            Debug.Log("[Brain Dissection] Blue hand visuals configured. " +
                      "Hands will follow controllers at runtime.");
        }
    }

    static void CleanupOldHandVisuals()
    {
        // Remove old XR Hand tracking objects if they exist
        string[] oldNames = { "LeftHandBlueGlove", "RightHandBlueGlove" };
        foreach (var name in oldNames)
        {
            var old = GameObject.Find(name);
            if (old != null)
            {
                Object.DestroyImmediate(old);
                Debug.Log($"[Brain Dissection] Removed old hand visual: {name}");
            }
        }
    }

    /// <summary>
    /// Searches the project for a hand visual prefab (left or right).
    /// Tries multiple known locations from XRI and XR Hands samples.
    /// </summary>
    static GameObject FindHandPrefab(string hand)
    {
        // Strategy 1: Search by name using AssetDatabase
        string[] searchTerms = new string[]
        {
            $"{hand}HandQuestVisual",
            $"{hand} Hand Tracking",
            $"{hand}HandAndroidXRVisual"
        };

        foreach (string term in searchTerms)
        {
            string[] guids = AssetDatabase.FindAssets(term + " t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.ToLower().Contains("hand") &&
                    (path.ToLower().Contains("visual") || path.ToLower().Contains("tracking")))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        Debug.Log($"[Brain Dissection] Found {hand} hand prefab: {path}");
                        return prefab;
                    }
                }
            }
        }

        // Strategy 2: Try exact known paths
        string[] knownPaths = new string[]
        {
            $"Assets/Samples/XR Interaction Toolkit/3.3.1/Hands Interaction Demo/Prefabs/{hand}HandQuestVisual.prefab",
            $"Assets/Samples/XR Hands/1.7.3/HandVisualizer/Prefabs/{hand} Hand Tracking.prefab",
        };

        foreach (string path in knownPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;
        }

        return null;
    }

    // ========================= BUILD UI =========================

    static void BuildUI(GameObject canvasGO, BrainManager bm, RegionUIController uiCtrl,
        BrainDissectionUI bridge)
    {
        var font = GetFont();
        var root = canvasGO.GetComponent<RectTransform>();

        // ========== MAIN PANEL (taller to fit lab tool elements) ==========
        var main = MakePanel("MainPanel", root, Vector2.zero, new Vector2(780, 520), PanelBg);
        uiCtrl.mainButtonPanel = main;

        // Title
        MakeLabel("Title", main.transform, new Vector2(0, 220), new Vector2(650, 40),
            "BRAIN DISSECTION LAB", 26, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);
        MakeAccentLine(main.transform, new Vector2(0, 196), new Vector2(550, 3));

        // Tool Status (green text showing equip state)
        var toolStatusGO = MakeLabel("ToolStatus", main.transform, new Vector2(0, 172), new Vector2(700, 28),
            "Gloves: --     Knife: --     Tweezers: --", 18, FontStyle.Normal, TextGreen, TextAnchor.MiddleCenter, font);
        uiCtrl.toolStatusText = toolStatusGO.GetComponent<Text>();

        // Status Message (instruction text)
        var statusMsgGO = MakeLabel("StatusMessage", main.transform, new Vector2(0, 140), new Vector2(700, 35),
            "Please equip your gloves to begin the lab.", 20, FontStyle.Italic, TextDim, TextAnchor.MiddleCenter, font);
        uiCtrl.statusMessageText = statusMsgGO.GetComponent<Text>();

        // ========== HEMISPHERE PANEL (hidden until brain is split) ==========
        var hemiPanel = MakeInvisiblePanel("HemispherePanel", main.transform);
        uiCtrl.hemispherePanel = hemiPanel;
        hemiPanel.SetActive(false);

        float hemiY = 85f;
        MakeBtn("ViewLeftHemisphere", hemiPanel.transform, new Vector2(-110, hemiY), new Vector2(200, 50),
            "View Left Hemi", BtnBlue, bridge, "OnLeftClicked", font);
        MakeBtn("ViewRightHemisphere", hemiPanel.transform, new Vector2(110, hemiY), new Vector2(200, 50),
            "View Right Hemi", BtnBlue, bridge, "OnRightClicked", font);
        // No "Show Whole" button -- in dissection mode, brain stays split

        // ========== CONTROL PANEL (hidden until gloves equipped) ==========
        var ctrlPanel = MakeInvisiblePanel("ControlPanel", main.transform);
        uiCtrl.controlPanel = ctrlPanel;
        ctrlPanel.SetActive(false);

        // Row: Zoom (brain rotation is grab-to-rotate: point at brain, hold trigger, move controller)
        float ctrlRow1 = 15f;
        MakeBtn("ZoomIn", ctrlPanel.transform, new Vector2(-100, ctrlRow1), new Vector2(175, 50),
            "Zoom In (+)", BtnBlueBright, bridge, "OnZoomInClicked", font);
        MakeBtn("ZoomOut", ctrlPanel.transform, new Vector2(100, ctrlRow1), new Vector2(175, 50),
            "Zoom Out (\u2013)", BtnBlueBright, bridge, "OnZoomOutClicked", font);

        // Row: Reset
        float ctrlRow2 = -50f;
        MakeBtn("Reset", ctrlPanel.transform, new Vector2(0, ctrlRow2), new Vector2(200, 50),
            "Reset Brain", BtnRed, bridge, "OnResetClicked", font);

        // Row: Opacity slider
        float ctrlRow3 = -115f;
        MakeLabel("OpacityLbl", ctrlPanel.transform, new Vector2(-220, ctrlRow3), new Vector2(150, 28),
            "Opacity:", 20, FontStyle.Normal, TextDim, TextAnchor.MiddleRight, font);
        var sliderGO = MakeSlider("OpacitySlider", ctrlPanel.transform, new Vector2(50, ctrlRow3), new Vector2(320, 28));
        var slider = sliderGO.GetComponent<Slider>();
        uiCtrl.opacitySlider = slider;
        UnityEventTools.AddPersistentListener(slider.onValueChanged,
            new UnityEngine.Events.UnityAction<float>(bridge.OnOpacityChanged));

        // Instructions
        MakeLabel("Instructions", main.transform, new Vector2(0, -225), new Vector2(700, 25),
            "Gloves  >  Cut with knife  >  Grip to rotate  >  Tweezers + Trigger to inspect region", 16,
            FontStyle.Italic, TextDim, TextAnchor.MiddleCenter, font);

        // End Session button is now created dynamically at runtime by BrainDissectionUI
        // when SessionData.IsPlayMode is true -- no static setup needed here.

        // ========== HOVER LABEL (above main panel) ==========
        var hoverBg = MakePanel("HoverNamePanel", root, new Vector2(0, 295), new Vector2(500, 50),
            new Color(0.04f, 0.04f, 0.08f, 0.80f));
        var hoverTxt = MakeLabel("HoverText", hoverBg.transform, Vector2.zero, new Vector2(480, 45),
            "", 28, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);
        hoverBg.SetActive(false);
        uiCtrl.hoverPanel = hoverBg;
        uiCtrl.hoverNameTextLegacy = hoverTxt.GetComponent<Text>();

        // ========== DETAILS PANEL (shown when region is selected) ==========
        // Layout (top to bottom inside an 820x480 panel, pivoted at center):
        //   Title (bold)            y = 200
        //   Accent line             y = 170
        //   Subtitle (italic gray)  y = 142
        //   Body paragraph block    y = -10  (centered, fills the gap to the buttons)
        //   Bottom buttons          y = -195
        // Body uses MiddleCenter so short and long descriptions both sit
        // visually centered, matching the Angular Gyrus reference and avoiding
        // the large empty band that appeared with UpperLeft on tall rects.
        var details = MakePanel("DetailsPanel", root, new Vector2(0, 0), new Vector2(820, 480), PanelBg);
        details.SetActive(false);
        uiCtrl.detailsPanel = details;

        MakeLabel("DTitle", details.transform, new Vector2(0, 200), new Vector2(740, 44),
            "Region Name", 30, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);
        uiCtrl.regionTitleTextLegacy = details.transform.Find("DTitle").GetComponent<Text>();

        MakeAccentLine(details.transform, new Vector2(0, 170), new Vector2(620, 3));

        var shortD = MakeLabel("ShortDesc", details.transform, new Vector2(0, 142), new Vector2(740, 26),
            "", 17, FontStyle.Italic, TextDim, TextAnchor.MiddleCenter, font);
        var shortText = shortD.GetComponent<Text>();
        shortText.lineSpacing = 1.0f;
        shortText.verticalOverflow = VerticalWrapMode.Overflow;
        uiCtrl.regionShortDescriptionTextLegacy = shortText;

        var longD = MakeLabel("DetailedDesc", details.transform, new Vector2(0, -10), new Vector2(720, 240),
            "", 22, FontStyle.Italic, TextWhite, TextAnchor.MiddleCenter, font);
        var longText = longD.GetComponent<Text>();
        longText.lineSpacing = 1.30f;
        longText.verticalOverflow = VerticalWrapMode.Overflow;
        longText.supportRichText = true;
        uiCtrl.regionDetailedDescriptionTextLegacy = longText;

        MakeBtn("PutBack", details.transform, new Vector2(-160, -195), new Vector2(220, 50),
            "Put Back Into Brain", BtnGreen, bridge, "OnPutBackClicked", font);
        MakeBtn("DZoomIn", details.transform, new Vector2(70, -195), new Vector2(120, 50),
            "Zoom +", BtnBlueBright, bridge, "OnZoomInClicked", font);
        MakeBtn("DZoomOut", details.transform, new Vector2(210, -195), new Vector2(120, 50),
            "Zoom \u2013", BtnBlueBright, bridge, "OnZoomOutClicked", font);
    }

    // ========================= UI PRIMITIVES =========================

    static GameObject MakePanel(string name, Transform parent, Vector2 pos, Vector2 size, Color bg)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        go.AddComponent<Image>().color = bg;
        return go;
    }

    /// <summary>Invisible container panel that can be toggled to show/hide a group.</summary>
    static GameObject MakeInvisiblePanel(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    static GameObject MakeLabel(string name, Transform parent, Vector2 pos, Vector2 size,
        string text, int fontSize, FontStyle style, Color color, TextAnchor align, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var t = go.AddComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.font = font;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        return go;
    }

    static void MakeAccentLine(Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("AccentLine");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        go.AddComponent<Image>().color = AccentBlue;
    }

    static GameObject MakeBtn(string name, Transform parent, Vector2 pos, Vector2 size,
        string label, Color bg, BrainDissectionUI bridge, string method, Font font)
    {
        var go = new GameObject("Btn_" + name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        go.AddComponent<Image>().color = bg;
        var btn = go.AddComponent<Button>();

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var txt = txtGO.AddComponent<Text>();
        txt.text = label; txt.fontSize = 16; txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter; txt.color = TextWhite; txt.font = font;

        var call = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(
            typeof(UnityEngine.Events.UnityAction), bridge, method);
        UnityEventTools.AddPersistentListener(btn.onClick, call);
        return go;
    }

    static GameObject MakeSlider(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;

        var slider = go.AddComponent<Slider>();
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;

        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(go.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.3f); bgRT.anchorMax = new Vector2(1, 0.7f);
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bgGO.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 1f);

        var fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(go.transform, false);
        var faRT = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0, 0.3f); faRT.anchorMax = new Vector2(1, 0.7f);
        faRT.offsetMin = faRT.offsetMax = Vector2.zero;
        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fRT = fill.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero; fRT.anchorMax = Vector2.one;
        fRT.offsetMin = fRT.offsetMax = Vector2.zero;
        fill.AddComponent<Image>().color = AccentBlue;
        slider.fillRect = fRT;

        var hArea = new GameObject("HandleArea");
        hArea.transform.SetParent(go.transform, false);
        var haRT = hArea.AddComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
        haRT.offsetMin = haRT.offsetMax = Vector2.zero;
        var handle = new GameObject("Handle");
        handle.transform.SetParent(hArea.transform, false);
        var hRT = handle.AddComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(18, 28);
        var hImg = handle.AddComponent<Image>();
        hImg.color = Color.white;
        slider.handleRect = hRT;
        slider.targetGraphic = hImg;

        return go;
    }

    // ========================= COMPARISON PANEL =========================



    // ========================= UTILITY =========================

    static void DestroyAllChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    static GameObject FindOrCreate(string name)
    {
        var e = GameObject.Find(name);
        return e != null ? e : new GameObject(name);
    }

    static GameObject FindOrCreateCanvas()
    {
        var e = GameObject.Find("BrainDissectionCanvas");
        if (e != null) return e;
        var go = new GameObject("BrainDissectionCanvas");
        go.transform.position = new Vector3(0, 1.5f, 2f);
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(950, 650);
        rt.localScale = Vector3.one * 0.001f;
        return go;
    }

    static void EnsureKinematic(GameObject go)
    {
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false;
    }

    static T EnsureComp<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    /// <summary>
    /// Enables "Allow Hovered Activate" on all ray/controller interactors so that
    /// Grip (activate) can be used to rotate the brain while pointing at it, without
    /// having to press Trigger (select) first.
    /// </summary>
    static void EnableHoveredActivateOnInteractors()
    {
        var interactors = Object.FindObjectsByType<XRBaseInputInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        foreach (var inter in interactors)
        {
            if (inter == null) continue;
            if (!inter.allowHoveredActivate)
            {
                inter.allowHoveredActivate = true;
                count++;
                EditorUtility.SetDirty(inter);
            }
        }
        if (count > 0)
            Debug.Log($"[Brain Dissection] Enabled Allow Hovered Activate on {count} interactor(s) for Grip-to-rotate.");
    }

    static void CheckEventSystem()
    {
        var es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null) { Debug.LogWarning("[Brain Dissection] No EventSystem found."); return; }
        if (es.GetComponent<BaseInputModule>() != null) return;
        es.gameObject.AddComponent<XRUIInputModule>();
        EditorUtility.SetDirty(es.gameObject);
    }
}
