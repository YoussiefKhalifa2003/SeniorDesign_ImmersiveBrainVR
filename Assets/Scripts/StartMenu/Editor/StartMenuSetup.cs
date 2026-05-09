using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;

/// <summary>
/// Menu: Tools > Brain Dissection > Setup Start Menu
/// Creates the start menu system: login panel, main menu, options, VR keyboard,
/// HUD timer, door controller, movement gate. Wires everything together.
///
/// Safe to re-run -- finds existing objects before creating new ones.
/// Does NOT modify any brain dissection scripts or objects.
/// </summary>
public static class StartMenuSetup
{
    // ========================= COLORS (matching brain dissection UI theme) =========================
    static readonly Color PanelBg       = new Color(0.06f, 0.06f, 0.10f, 0.94f);
    static readonly Color BtnBlue       = new Color(0.18f, 0.35f, 0.62f, 1f);
    static readonly Color BtnGreen      = new Color(0.12f, 0.50f, 0.22f, 1f);
    static readonly Color BtnRed        = new Color(0.60f, 0.15f, 0.15f, 1f);
    static readonly Color BtnOrange     = new Color(0.70f, 0.45f, 0.10f, 1f);
    static readonly Color AccentBlue    = new Color(0.3f, 0.6f, 1f, 0.8f);
    static readonly Color TextWhite     = new Color(0.95f, 0.95f, 0.97f, 1f);
    static readonly Color TextDim       = new Color(0.70f, 0.70f, 0.75f, 1f);
    static readonly Color InputBg       = new Color(0.12f, 0.13f, 0.18f, 1f);
    static readonly Color InputPlaceholder = new Color(0.45f, 0.45f, 0.50f, 1f);

    static Font GetFont()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    // ========================= ENTRY POINT =========================

    [MenuItem("Tools/Brain Dissection/Setup Start Menu")]
    public static void SetupStartMenu()
    {
        // ---- 1. StartMenuSystem root object ----
        var systemGO = FindOrCreate("StartMenuSystem");

        // ---- 2. MenuManager ----
        var menuMgr = EnsureComp<MenuManager>(systemGO);

        // ---- 3. DoorController ----
        var doorCtrl = EnsureComp<DoorController>(systemGO);
        menuMgr.doorController = doorCtrl;

        // Auto-find doors
        FindAndAssignDoors(doorCtrl);

        // ---- 4. MovementGate ----
        var moveGate = EnsureComp<MovementGate>(systemGO);
        menuMgr.movementGate = moveGate;

        // ---- 5. SessionTimer ----
        var timer = EnsureComp<SessionTimer>(systemGO);
        menuMgr.sessionTimer = timer;

        // ---- 6. OptionsController ----
        var optCtrl = EnsureComp<OptionsController>(systemGO);

        // ---- 6b. LabIntroduction (welcome message on Play) ----
        var labIntro = EnsureComp<LabIntroduction>(systemGO);
        menuMgr.labIntroduction = labIntro;
        Debug.Log("[Start Menu] LabIntroduction added and wired to MenuManager.");

        // ---- 7. LoginManager ----
        var loginMgr = EnsureComp<LoginManager>(systemGO);
        loginMgr.menuManager = menuMgr;

        // ---- 8. VRKeyboardManager ----
        var kbMgr = EnsureComp<VRKeyboardManager>(systemGO);

        // ---- 9. Start Menu Canvas (world-space, in front of player spawn) ----
        var canvasGO = FindOrCreateStartMenuCanvas();
        EnsureComp<TrackedDeviceGraphicRaycaster>(canvasGO);

        var canvasGroup = EnsureComp<CanvasGroup>(canvasGO);
        menuMgr.menuCanvasGroup = canvasGroup;
        menuMgr.startMenuCanvas = canvasGO;

        // Clear and rebuild children
        DestroyAllChildren(canvasGO.transform);

        // ---- 10. Build all UI panels ----
        BuildStartMenuUI(canvasGO, menuMgr, loginMgr, kbMgr, optCtrl, timer);

        // ---- 11. HUD Timer Canvas ----
        BuildHUDTimer(timer);

        // ---- 12. Keyboard Panel ----
        BuildVRKeyboardPanel(canvasGO, kbMgr);

        // ---- 13. EventSystem check ----
        CheckEventSystem();

        // ---- 14. TutorialManager (in-scene tutorial, no separate scene needed) ----
        EnsureComp<TutorialManager>(systemGO);

        // ---- 15. Assessment components ----
        EnsureComp<AssessmentObjectHider>(systemGO);

        EditorUtility.SetDirty(systemGO);
        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(systemGO.scene);
        Debug.Log("[Start Menu] Setup complete. Login, Menu, Options, Doors, Timer, Keyboard, Tutorial ready.");
    }

    // ========================= CANVAS =========================

    static GameObject FindOrCreateStartMenuCanvas()
    {
        var existing = GameObject.Find("StartMenuCanvas");
        if (existing != null) return existing;

        var go = new GameObject("StartMenuCanvas");
        // Position in front of player spawn (XR Origin typically at 0,0,0)
        // Canvas faces -Z so user sees it when looking forward
        go.transform.position = new Vector3(0f, 1.5f, 1.5f);
        go.transform.rotation = Quaternion.identity;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(800, 600);
        rt.localScale = Vector3.one * 0.002f;

        return go;
    }

    // ========================= BUILD UI =========================

    static void BuildStartMenuUI(GameObject canvasGO, MenuManager menuMgr,
        LoginManager loginMgr, VRKeyboardManager kbMgr, OptionsController optCtrl, SessionTimer timer)
    {
        var font = GetFont();
        var root = canvasGO.GetComponent<RectTransform>();

        // ==================== LOGIN PANEL ====================
        var loginPanel = MakePanel("LoginPanel", root, Vector2.zero, new Vector2(650, 500), PanelBg);
        menuMgr.loginPanel = loginPanel;

        // Title
        MakeLabel("WelcomeTitle", loginPanel.transform, new Vector2(0, 190), new Vector2(580, 50),
            "Welcome to the", 20, FontStyle.Normal, TextDim, TextAnchor.MiddleCenter, font);
        MakeLabel("LabTitle", loginPanel.transform, new Vector2(0, 145), new Vector2(580, 55),
            "BRAIN DISSECTION LAB", 30, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);
        MakeAccentLine(loginPanel.transform, new Vector2(0, 115), new Vector2(450, 3));

        // Subtitle
        MakeLabel("VRLabel", loginPanel.transform, new Vector2(0, 85), new Vector2(580, 30),
            "Virtual Reality Training Environment", 16, FontStyle.Italic, TextDim, TextAnchor.MiddleCenter, font);

        // Name field
        MakeLabel("NameLabel", loginPanel.transform, new Vector2(-180, 10), new Vector2(100, 30),
            "Name:", 18, FontStyle.Bold, TextWhite, TextAnchor.MiddleRight, font);
        var nameField = MakeInputField("NameInput", loginPanel.transform, new Vector2(60, 10), new Vector2(320, 45),
            "Enter your name...", font);
        loginMgr.nameInputField = nameField.GetComponent<InputField>();
        kbMgr.RegisterInputField(loginMgr.nameInputField);

        // Submit button
        var submitBtn = MakeButton("SubmitBtn", loginPanel.transform, new Vector2(0, -80), new Vector2(250, 55),
            "LOGIN", BtnGreen, font);
        loginMgr.submitButton = submitBtn.GetComponent<Button>();

        // Footer
        MakeLabel("LoginFooter", loginPanel.transform, new Vector2(0, -200), new Vector2(580, 25),
            "Please enter your details to begin the lab session", 12,
            FontStyle.Italic, TextDim, TextAnchor.MiddleCenter, font);

        // ==================== MAIN MENU PANEL ====================
        var mainMenuPanel = MakePanel("MainMenuPanel", root, Vector2.zero, new Vector2(650, 500), PanelBg);
        menuMgr.mainMenuPanel = mainMenuPanel;

        MakeLabel("MenuTitle", mainMenuPanel.transform, new Vector2(0, 190), new Vector2(580, 55),
            "BRAIN DISSECTION LAB", 30, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);
        MakeAccentLine(mainMenuPanel.transform, new Vector2(0, 160), new Vector2(450, 3));
        MakeLabel("MenuSubtitle", mainMenuPanel.transform, new Vector2(0, 130), new Vector2(580, 30),
            "Main Menu", 18, FontStyle.Normal, TextDim, TextAnchor.MiddleCenter, font);

        // Play button (locked until Tutorial is done)
        var playBtn = MakeButton("PlayBtn", mainMenuPanel.transform, new Vector2(0, 50), new Vector2(320, 65),
            "PLAY", new Color(0.25f, 0.25f, 0.30f, 1f), font, 24);
        UnityEventTools.AddPersistentListener(
            playBtn.GetComponent<Button>().onClick,
            new UnityEngine.Events.UnityAction(menuMgr.OnPlayPressed));
        playBtn.GetComponent<Button>().interactable = false;
        menuMgr.playButton = playBtn;

        var playLockLabel = MakeLabel("PlayLockLabel", mainMenuPanel.transform,
            new Vector2(0, 8), new Vector2(500, 22),
            "LOCKED  |  Tutorial: Incomplete",
            11, FontStyle.Italic, new Color(0.6f, 0.6f, 0.65f, 1f), TextAnchor.MiddleCenter, font);
        playLockLabel.GetComponent<Text>().supportRichText = true;
        menuMgr.playLockLabel = playLockLabel.GetComponent<Text>();

        // Tutorial button
        var tutBtn = MakeButton("TutorialBtn", mainMenuPanel.transform, new Vector2(0, -30), new Vector2(320, 50),
            "TUTORIAL", BtnBlue, font);
        UnityEventTools.AddPersistentListener(
            tutBtn.GetComponent<Button>().onClick,
            new UnityEngine.Events.UnityAction(menuMgr.OnTutorialPressed));

        // Assessment button (always visible, locked/grayed until Tutorial + Play completed)
        var assessBtn = MakeButton("AssessmentBtn", mainMenuPanel.transform, new Vector2(0, -95), new Vector2(320, 50),
            "ASSESSMENT", new Color(0.25f, 0.25f, 0.30f, 1f), font);
        UnityEventTools.AddPersistentListener(
            assessBtn.GetComponent<Button>().onClick,
            new UnityEngine.Events.UnityAction(menuMgr.OnAssessmentPressed));
        assessBtn.GetComponent<Button>().interactable = false;
        menuMgr.assessmentButton = assessBtn;

        // Lock status label below the assessment button
        var lockLabel = MakeLabel("AssessmentLockLabel", mainMenuPanel.transform,
            new Vector2(0, -128), new Vector2(500, 22),
            "LOCKED  |  Tutorial: Incomplete   Play: Incomplete",
            11, FontStyle.Italic, new Color(0.6f, 0.6f, 0.65f, 1f), TextAnchor.MiddleCenter, font);
        lockLabel.GetComponent<Text>().supportRichText = true;
        menuMgr.assessmentLockLabel = lockLabel.GetComponent<Text>();

        // Options button
        var optBtn = MakeButton("OptionsBtn", mainMenuPanel.transform, new Vector2(0, -160), new Vector2(320, 50),
            "OPTIONS", BtnBlue, font);
        UnityEventTools.AddPersistentListener(
            optBtn.GetComponent<Button>().onClick,
            new UnityEngine.Events.UnityAction(menuMgr.ShowOptions));

        // Back to Login button
        var backBtn = MakeButton("BackToLoginBtn", mainMenuPanel.transform, new Vector2(0, -225), new Vector2(320, 50),
            "RETURN TO LOGIN", BtnOrange, font);
        UnityEventTools.AddPersistentListener(
            backBtn.GetComponent<Button>().onClick,
            new UnityEngine.Events.UnityAction(menuMgr.ReturnToLogin));

        // ==================== ASSESSMENT PANEL ====================
        var assessPanel = MakePanel("AssessmentPanel", root, Vector2.zero, new Vector2(650, 500), PanelBg);
        menuMgr.assessmentPanel = assessPanel;

        MakeLabel("AssessTitle", assessPanel.transform, new Vector2(0, 190), new Vector2(580, 55),
            "ASSESSMENT", 28, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);
        MakeAccentLine(assessPanel.transform, new Vector2(0, 160), new Vector2(450, 3));
        MakeLabel("AssessSubtitle", assessPanel.transform, new Vector2(0, 130), new Vector2(580, 30),
            "Select Assessment Mode", 16, FontStyle.Normal, TextDim, TextAnchor.MiddleCenter, font);

        var mcqBtn = MakeButton("MCQQuizBtn", assessPanel.transform, new Vector2(0, 50), new Vector2(320, 65),
            "MCQ QUIZ", BtnGreen, font, 22);
        UnityEventTools.AddPersistentListener(
            mcqBtn.GetComponent<Button>().onClick,
            new UnityEngine.Events.UnityAction(menuMgr.OnMCQQuizPressed));

        var ldBtn = MakeButton("LiveDissectionBtn", assessPanel.transform, new Vector2(0, -30), new Vector2(320, 55),
            "LIVE DISSECTION", BtnBlue, font, 20);
        UnityEventTools.AddPersistentListener(
            ldBtn.GetComponent<Button>().onClick,
            new UnityEngine.Events.UnityAction(menuMgr.OnLiveDissectionPressed));

        // Bigger, clearer leaderboard entry point — students missed the old
        // small "LEADERBOARD" button. The label now spells out what is inside,
        // and a distinct gold-toned color separates it from the assessment
        // mode buttons above so it reads as a different kind of action.
        var lbBtn = MakeButton("LeaderboardBtn", assessPanel.transform, new Vector2(0, -110), new Vector2(380, 58),
            "LEADERBOARD  (Scores & Times)", new Color(0.55f, 0.40f, 0.10f, 1f), font, 18);
        UnityEventTools.AddPersistentListener(
            lbBtn.GetComponent<Button>().onClick,
            new UnityEngine.Events.UnityAction(menuMgr.OnLeaderboardPressed));

        var assessBackBtn = MakeButton("AssessBackBtn", assessPanel.transform, new Vector2(0, -180), new Vector2(250, 50),
            "BACK", BtnOrange, font);
        UnityEventTools.AddPersistentListener(
            assessBackBtn.GetComponent<Button>().onClick,
            new UnityEngine.Events.UnityAction(menuMgr.BackToMenuFromAssessment));

        assessPanel.SetActive(false);

        // ==================== OPTIONS PANEL ====================
        var optionsPanel = MakePanel("OptionsPanel", root, Vector2.zero, new Vector2(650, 500), PanelBg);
        menuMgr.optionsPanel = optionsPanel;

        MakeLabel("OptTitle", optionsPanel.transform, new Vector2(0, 190), new Vector2(580, 55),
            "OPTIONS", 28, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);
        MakeAccentLine(optionsPanel.transform, new Vector2(0, 160), new Vector2(450, 3));

        // Brightness
        MakeLabel("BrightLabel", optionsPanel.transform, new Vector2(-200, 80), new Vector2(160, 30),
            "Brightness:", 18, FontStyle.Normal, TextWhite, TextAnchor.MiddleRight, font);
        var brightSlider = MakeSlider("BrightnessSlider", optionsPanel.transform,
            new Vector2(60, 80), new Vector2(300, 30), -2f, 2f, 0f);
        optCtrl.brightnessSlider = brightSlider.GetComponent<Slider>();

        // FPS Counter Toggle
        MakeLabel("FpsLabel", optionsPanel.transform, new Vector2(-200, 10), new Vector2(160, 30),
            "FPS Counter:", 18, FontStyle.Normal, TextWhite, TextAnchor.MiddleRight, font);
        var fpsToggle = MakeFpsToggle("FpsToggle", optionsPanel.transform,
            new Vector2(60, 10), new Vector2(60, 30), font);
        optCtrl.fpsToggle = fpsToggle;

        // Cleanup any contrast UI left over from earlier setups
        var oldContrastLabel = optionsPanel.transform.Find("ContrastLabel");
        if (oldContrastLabel != null) Object.DestroyImmediate(oldContrastLabel.gameObject);
        var oldContrastSlider = optionsPanel.transform.Find("ContrastSlider");
        if (oldContrastSlider != null) Object.DestroyImmediate(oldContrastSlider.gameObject);

        // Back button
        var optBackBtn = MakeButton("OptBackBtn", optionsPanel.transform, new Vector2(0, -120), new Vector2(250, 50),
            "BACK", BtnOrange, font);
        UnityEventTools.AddPersistentListener(
            optBackBtn.GetComponent<Button>().onClick,
            new UnityEngine.Events.UnityAction(menuMgr.BackToLoginFromOptions));

        // Start with login panel visible, others hidden
        loginPanel.SetActive(true);
        mainMenuPanel.SetActive(false);
        assessPanel.SetActive(false);
        optionsPanel.SetActive(false);
    }

    // ========================= HUD TIMER =========================

    static void BuildHUDTimer(SessionTimer timer)
    {
        var existing = GameObject.Find("SessionHUDCanvas");
        if (existing != null) Object.DestroyImmediate(existing);

        var font = GetFont();

        var hudGO = new GameObject("SessionHUDCanvas");

        var mainCam = Camera.main;
        if (mainCam != null)
        {
            hudGO.transform.SetParent(mainCam.transform, false);
            hudGO.transform.localPosition = new Vector3(0.35f, 0.25f, 0.8f);
            hudGO.transform.localRotation = Quaternion.identity;
        }
        else
        {
            hudGO.transform.position = new Vector3(0.5f, 2.2f, 1.5f);
        }

        var canvas = hudGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        hudGO.AddComponent<CanvasScaler>();

        var rt = hudGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(360, 80);
        rt.localScale = Vector3.one * 0.001f;

        var bgPanel = new GameObject("TimerBg");
        bgPanel.transform.SetParent(hudGO.transform, false);
        var bgRT = bgPanel.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bgPanel.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.08f, 0.80f);

        var sessionTextGO = new GameObject("TimerText");
        sessionTextGO.transform.SetParent(bgPanel.transform, false);
        var sessionRT = sessionTextGO.AddComponent<RectTransform>();
        sessionRT.anchorMin = new Vector2(0, 0.5f);
        sessionRT.anchorMax = new Vector2(1, 1f);
        sessionRT.offsetMin = new Vector2(12, 2);
        sessionRT.offsetMax = new Vector2(-12, -2);
        var timerText = sessionTextGO.AddComponent<Text>();
        timerText.text = "User  |  Session: 00:00";
        timerText.fontSize = 20;
        timerText.fontStyle = FontStyle.Bold;
        timerText.color = TextWhite;
        timerText.alignment = TextAnchor.MiddleCenter;
        timerText.font = font;

        var taskTextGO = new GameObject("TaskText");
        taskTextGO.transform.SetParent(bgPanel.transform, false);
        var taskRT = taskTextGO.AddComponent<RectTransform>();
        taskRT.anchorMin = new Vector2(0, 0f);
        taskRT.anchorMax = new Vector2(1, 0.5f);
        taskRT.offsetMin = new Vector2(12, 2);
        taskRT.offsetMax = new Vector2(-12, -2);
        var taskText = taskTextGO.AddComponent<Text>();
        taskText.text = "";
        taskText.fontSize = 14;
        taskText.fontStyle = FontStyle.Normal;
        taskText.color = new Color(0.55f, 0.85f, 1f, 1f);
        taskText.alignment = TextAnchor.MiddleCenter;
        taskText.font = font;

        timer.timerText = timerText;
        timer.taskText = taskText;
        timer.hudPanel = hudGO;

        EditorUtility.SetDirty(hudGO);
    }

    // ========================= VR KEYBOARD =========================

    static void BuildVRKeyboardPanel(GameObject canvasGO, VRKeyboardManager kbMgr)
    {
        // Create keyboard panel as child of the start menu canvas
        var existing = canvasGO.transform.Find("VRKeyboardPanel");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var kbPanel = new GameObject("VRKeyboardPanel");
        kbPanel.transform.SetParent(canvasGO.transform, false);
        var rt = kbPanel.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(750, 350);
        rt.anchoredPosition = new Vector2(0, -340);

        // Background
        var bg = kbPanel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

        kbMgr.keyboardPanel = kbPanel;
        kbMgr.BuildKeyboard();

        // Start hidden
        kbPanel.SetActive(false);
    }

    // ========================= DOOR FINDING =========================

    static void FindAndAssignDoors(DoorController doorCtrl)
    {
        var opRoom = GameObject.Find("operating_room");
        if (opRoom == null)
        {
            Debug.LogWarning("[Start Menu] operating_room not found. Door animation will auto-find at runtime.");
            return;
        }

        foreach (var t in opRoom.GetComponentsInChildren<Transform>(true))
        {
            string name = t.name.ToLower();
            if (doorCtrl.leftDoor == null && name.Contains("leftdoor"))
                doorCtrl.leftDoor = t;
            if (doorCtrl.rightDoor == null && name.Contains("rightdoor"))
                doorCtrl.rightDoor = t;
        }

        if (doorCtrl.leftDoor != null)
            Debug.Log($"[Start Menu] LeftDoor found: {doorCtrl.leftDoor.name}");
        if (doorCtrl.rightDoor != null)
            Debug.Log($"[Start Menu] RightDoor found: {doorCtrl.rightDoor.name}");
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

    static GameObject MakeButton(string name, Transform parent, Vector2 pos, Vector2 size,
        string label, Color bg, Font font, int fontSize = 18)
    {
        var go = new GameObject("Btn_" + name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        go.AddComponent<Image>().color = bg;
        var btn = go.AddComponent<Button>();

        // Hover/press colors
        var colors = btn.colors;
        colors.normalColor = bg;
        colors.highlightedColor = bg * 1.2f;
        colors.pressedColor = bg * 0.8f;
        colors.selectedColor = bg * 1.1f;
        btn.colors = colors;

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var txt = txtGO.AddComponent<Text>();
        txt.text = label;
        txt.fontSize = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = TextWhite;
        txt.font = font;

        return go;
    }

    static GameObject MakeInputField(string name, Transform parent, Vector2 pos, Vector2 size,
        string placeholder, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        go.AddComponent<Image>().color = InputBg;

        var inputField = go.AddComponent<InputField>();

        // Text child
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10, 2);
        textRT.offsetMax = new Vector2(-10, -2);
        var text = textGO.AddComponent<Text>();
        text.fontSize = 16;
        text.color = TextWhite;
        text.font = font;
        text.alignment = TextAnchor.MiddleLeft;
        text.supportRichText = false;
        inputField.textComponent = text;

        // Placeholder child
        var phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(go.transform, false);
        var phRT = phGO.AddComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(10, 2);
        phRT.offsetMax = new Vector2(-10, -2);
        var phText = phGO.AddComponent<Text>();
        phText.text = placeholder;
        phText.fontSize = 16;
        phText.fontStyle = FontStyle.Italic;
        phText.color = InputPlaceholder;
        phText.font = font;
        phText.alignment = TextAnchor.MiddleLeft;
        inputField.placeholder = phText;

        return go;
    }

    static GameObject MakeSlider(string name, Transform parent, Vector2 pos, Vector2 size,
        float min, float max, float defaultValue)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        var slider = go.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = defaultValue;

        // Background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(go.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.3f);
        bgRT.anchorMax = new Vector2(1, 0.7f);
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bgGO.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 1f);

        // Fill area
        var fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(go.transform, false);
        var faRT = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0, 0.3f);
        faRT.anchorMax = new Vector2(1, 0.7f);
        faRT.offsetMin = faRT.offsetMax = Vector2.zero;
        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fRT = fill.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero;
        fRT.anchorMax = Vector2.one;
        fRT.offsetMin = fRT.offsetMax = Vector2.zero;
        fill.AddComponent<Image>().color = AccentBlue;
        slider.fillRect = fRT;

        // Handle
        var hArea = new GameObject("HandleArea");
        hArea.transform.SetParent(go.transform, false);
        var haRT = hArea.AddComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero;
        haRT.anchorMax = Vector2.one;
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

    static Toggle MakeFpsToggle(string name, Transform parent, Vector2 pos, Vector2 size, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        var toggle = go.AddComponent<Toggle>();
        toggle.targetGraphic = bg;

        var checkmarkGO = new GameObject("Checkmark");
        checkmarkGO.transform.SetParent(go.transform, false);
        var cRT = checkmarkGO.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0.1f, 0.15f);
        cRT.anchorMax = new Vector2(0.9f, 0.85f);
        cRT.offsetMin = cRT.offsetMax = Vector2.zero;
        var cImg = checkmarkGO.AddComponent<Image>();
        cImg.color = AccentBlue;
        toggle.graphic = cImg;
        toggle.isOn = false;

        return toggle;
    }

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

    static T EnsureComp<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void CheckEventSystem()
    {
        var es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            Debug.LogWarning("[Start Menu] No EventSystem found. Create one or run Brain Dissection > Setup Scene first.");
            return;
        }
        if (es.GetComponent<BaseInputModule>() != null) return;
        es.gameObject.AddComponent<XRUIInputModule>();
        EditorUtility.SetDirty(es.gameObject);
    }
}
