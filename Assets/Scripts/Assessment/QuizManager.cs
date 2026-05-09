using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Runs the MCQ Quiz: shuffles questions, presents options, tracks score,
/// shows explanations, records to leaderboard on completion.
/// Builds its own world-space UI at runtime attached to the camera.
/// </summary>
public class QuizManager : MonoBehaviour
{
    public static QuizManager Instance { get; private set; }

    [Header("Optional: assign a QuizQuestionBank asset. If null, uses defaults.")]
    public QuizQuestionBank questionBank;

    List<QuizQuestion> _questions;
    int _currentIndex;
    int _score;
    int _streak;
    int _bestStreak;
    bool _active;
    bool _waitingForNext;

    float _quizStartTime;
    int _quizElapsedFrozenSeconds;
    Text _timerText;

    // Category filter
    string _filterLobe = "All";
    string _filterDifficulty = "All";
    GameObject _filterPanel;

    // IdentifyRegion state
    bool _identifyMode;
    GameObject _identifyHighlight;
    BrainRegion _identifyTarget;
    BrainRegion[] _identifyCandidates;

    // UI references (built at runtime)
    GameObject _quizCanvas;
    Text _questionText;
    Text _progressText;
    Text _scoreText;
    Text _explanationText;
    Text _streakText;
    GameObject _explanationPanel;
    GameObject _optionsPanel;
    GameObject _nextButton;
    GameObject _finishPanel;
    List<Button> _optionButtons = new List<Button>();
    List<Text> _optionTexts = new List<Text>();
    Image _progressBar;

    static readonly Color PanelBg = new Color(0.06f, 0.06f, 0.10f, 0.94f);
    static readonly Color BtnBlue = new Color(0.18f, 0.35f, 0.62f, 1f);
    static readonly Color BtnGreen = new Color(0.12f, 0.50f, 0.22f, 1f);
    static readonly Color BtnRed = new Color(0.60f, 0.15f, 0.15f, 1f);
    static readonly Color BtnOrange = new Color(0.70f, 0.45f, 0.10f, 1f);
    static readonly Color TextWhite = new Color(0.95f, 0.95f, 0.97f, 1f);
    static readonly Color TextDim = new Color(0.70f, 0.70f, 0.75f, 1f);
    static readonly Color CorrectGreen = new Color(0.2f, 0.8f, 0.3f, 1f);
    static readonly Color WrongRed = new Color(0.9f, 0.2f, 0.2f, 1f);

    static bool IsPureMcqQuestion(QuizQuestion q)
    {
        return q != null && q.questionType != QuizQuestionType.IdentifyRegion;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public void StartQuiz()
    {
        ShowFilterPanel();
    }

    void ShowFilterPanel()
    {
        if (_quizCanvas != null) Destroy(_quizCanvas);

        _quizCanvas = new GameObject("QuizFilterCanvas");
        var canvas = _quizCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _quizCanvas.AddComponent<CanvasScaler>();
        _quizCanvas.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

        var crt = _quizCanvas.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(700, 400);
        crt.localScale = Vector3.one * 0.0008f;

        var cam = Camera.main;
        if (cam != null)
        {
            _quizCanvas.transform.position = cam.transform.position + cam.transform.forward * 0.8f;
            _quizCanvas.transform.rotation = Quaternion.LookRotation(
                _quizCanvas.transform.position - cam.transform.position);
        }

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        _filterPanel = MakeRect("FilterBg", _quizCanvas.transform, Vector2.zero, crt.sizeDelta).gameObject;
        _filterPanel.AddComponent<Image>().color = PanelBg;

        MakeText("Title", _filterPanel.transform, new Vector2(0, 160), new Vector2(600, 36),
            "MCQ Quiz — Filter Questions", 24, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);

        MakeText("LobeLabel", _filterPanel.transform, new Vector2(0, 115), new Vector2(600, 24),
            "Filter by Brain Region:", 18, FontStyle.Normal, TextDim, TextAnchor.MiddleCenter, font);

        string[] lobes = { "All", "Frontal", "Temporal", "Parietal", "Occipital", "Limbic" };
        for (int i = 0; i < lobes.Length; i++)
        {
            float x = -250f + i * 100f;
            string lobe = lobes[i];
            var btn = MakeButton($"Lobe_{lobe}", _filterPanel.transform, new Vector2(x, 80), new Vector2(90, 32),
                lobe, _filterLobe == lobe ? BtnGreen : BtnBlue, font);
            btn.GetComponent<Button>().onClick.AddListener(() => {
                _filterLobe = lobe;
                ShowFilterPanel();
            });
        }

        MakeText("DiffLabel", _filterPanel.transform, new Vector2(0, 35), new Vector2(600, 24),
            "Filter by Difficulty:", 18, FontStyle.Normal, TextDim, TextAnchor.MiddleCenter, font);

        string[] diffs = { "All", "Easy", "Medium", "Hard" };
        for (int i = 0; i < diffs.Length; i++)
        {
            float x = -150f + i * 100f;
            string diff = diffs[i];
            var btn = MakeButton($"Diff_{diff}", _filterPanel.transform, new Vector2(x, 0), new Vector2(90, 32),
                diff, _filterDifficulty == diff ? BtnGreen : BtnBlue, font);
            btn.GetComponent<Button>().onClick.AddListener(() => {
                _filterDifficulty = diff;
                ShowFilterPanel();
            });
        }

        int matchCount = CountFilteredQuestions();
        MakeText("Count", _filterPanel.transform, new Vector2(0, -50), new Vector2(600, 24),
            $"Matching questions: {matchCount}", 18, FontStyle.Normal, TextDim, TextAnchor.MiddleCenter, font);

        var startBtn = MakeButton("StartQuizBtn", _filterPanel.transform, new Vector2(0, -105), new Vector2(250, 50),
            "Start Quiz", BtnGreen, font);
        startBtn.GetComponent<Button>().onClick.AddListener(BeginFilteredQuiz);

        var backBtn = MakeButton("BackBtn", _filterPanel.transform, new Vector2(0, -165), new Vector2(200, 36),
            "Cancel", BtnOrange, font);
        backBtn.GetComponent<Button>().onClick.AddListener(EndQuiz);
    }

    int CountFilteredQuestions()
    {
        var all = questionBank != null && questionBank.questions.Count > 0
            ? questionBank.questions : DefaultQuizData.GetQuestions();
        int c = 0;
        foreach (var q in all)
        {
            if (!IsPureMcqQuestion(q)) continue;
            if (_filterLobe != "All" && !string.IsNullOrEmpty(q.lobe) && q.lobe != _filterLobe) continue;
            if (_filterDifficulty != "All")
            {
                var d = _filterDifficulty == "Easy" ? QuizDifficulty.Easy
                    : _filterDifficulty == "Medium" ? QuizDifficulty.Medium : QuizDifficulty.Hard;
                if (q.difficulty != d) continue;
            }
            c++;
        }
        return c;
    }

    void BeginFilteredQuiz()
    {
        var all = questionBank != null && questionBank.questions.Count > 0
            ? new List<QuizQuestion>(questionBank.questions)
            : DefaultQuizData.GetQuestions();

        _questions = new List<QuizQuestion>();
        foreach (var q in all)
        {
            if (!IsPureMcqQuestion(q)) continue;
            if (_filterLobe != "All" && !string.IsNullOrEmpty(q.lobe) && q.lobe != _filterLobe) continue;
            if (_filterDifficulty != "All")
            {
                var d = _filterDifficulty == "Easy" ? QuizDifficulty.Easy
                    : _filterDifficulty == "Medium" ? QuizDifficulty.Medium : QuizDifficulty.Hard;
                if (q.difficulty != d) continue;
            }
            _questions.Add(q);
        }

        if (_questions.Count == 0)
            _questions = all.FindAll(IsPureMcqQuestion);

        Shuffle(_questions);
        _currentIndex = 0;
        _score = 0;
        _streak = 0;
        _bestStreak = 0;
        _active = true;
        _waitingForNext = false;

        _quizStartTime = Time.time;
        _quizElapsedFrozenSeconds = 0;

        if (_quizCanvas != null) Destroy(_quizCanvas);
        _quizCanvas = null;
        _filterPanel = null;

        BuildUI();
        ShowQuestion();
        Debug.Log($"[QuizManager] Quiz started with {_questions.Count} questions (lobe={_filterLobe}, diff={_filterDifficulty}).");
    }

    void Update()
    {
        if (_active && _timerText != null)
        {
            int secs = Mathf.FloorToInt(Time.time - _quizStartTime);
            _timerText.text = $"Time: {LeaderboardManager.FormatElapsed(Mathf.Max(1, secs))}";
        }

        if (!_active || !_identifyMode || _waitingForNext || _identifyTarget == null) return;
        PollIdentifyRegionClick();
    }

    void PollIdentifyRegionClick()
    {
        bool triggerDown = false;
        UnityEngine.XR.InputDevice device = default;

        var rightDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        if (rightDevice.isValid && rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool rt) && rt)
        { triggerDown = true; device = rightDevice; }

        if (!triggerDown)
        {
            var leftDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
            if (leftDevice.isValid && leftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool lt) && lt)
            { triggerDown = true; device = leftDevice; }
        }

        if (!triggerDown) return;

        var cam = Camera.main;
        if (cam == null) return;

        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 handPos) &&
            device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion handRot))
        {
            Vector3 origin = handPos;
            Vector3 forward = handRot * Vector3.forward;

            if (Physics.Raycast(origin, forward, out RaycastHit hit, 10f))
            {
                var br = hit.collider.GetComponentInParent<BrainRegion>();
                if (br != null)
                {
                    bool correct = br == _identifyTarget;
                    _waitingForNext = true;

                    var q = _questions[_currentIndex];
                    if (correct)
                    {
                        _score++;
                        _streak++;
                        if (_streak > _bestStreak) _bestStreak = _streak;
                        HapticFeedback.PulseBoth(0.3f, 0.2f);
                        _explanationText.text = $"Correct! That is {_identifyTarget.regionData.displayName}. {q.explanation}";
                    }
                    else
                    {
                        _streak = 0;
                        HapticFeedback.PulseBoth(0.8f, 0.4f);
                        string selected = br.regionData != null ? br.regionData.displayName : "Unknown";
                        _explanationText.text = $"Incorrect. You selected {selected}. The correct region was {_identifyTarget.regionData.displayName}. {q.explanation}";
                    }

                    _scoreText.text = $"Score: {_score}";
                    _streakText.text = _streak > 1 ? $"Streak: {_streak}x" : "";
                    _explanationPanel.SetActive(true);
                    _nextButton.SetActive(true);
                    ClearIdentifyHighlight();
                    _identifyMode = false;
                }
            }
        }
    }

    public void EndQuiz()
    {
        _active = false;
        ClearIdentifyHighlight();
        _identifyMode = false;
        if (_quizCanvas != null) Destroy(_quizCanvas);

        var mm = FindFirstObjectByType<MenuManager>();
        if (mm != null) mm.ShowAssessment();
    }

    public void EndQuizToMainMenu()
    {
        _active = false;
        ClearIdentifyHighlight();
        _identifyMode = false;
        if (_quizCanvas != null) Destroy(_quizCanvas);

        var mm = FindFirstObjectByType<MenuManager>();
        if (mm != null)
        {
            SessionData.IsAssessmentMode = false;

            if (mm.movementGate != null)
                mm.movementGate.DisableMovement();

            if (mm.doorController != null)
                mm.doorController.CloseDoors();

            var floatingPanel = FindFirstObjectByType<FloatingInfoPanel>();
            if (floatingPanel != null)
            {
                var cg = floatingPanel.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 0f;
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                }
            }

            mm.TeleportToStart();

            if (mm.startMenuCanvas != null)
                mm.startMenuCanvas.SetActive(true);

            if (mm.menuCanvasGroup != null)
            {
                mm.menuCanvasGroup.alpha = 1f;
                mm.menuCanvasGroup.interactable = true;
                mm.menuCanvasGroup.blocksRaycasts = true;
            }

            mm.ShowMainMenu();
        }
    }

    void ShowQuestion()
    {
        ClearIdentifyHighlight();
        _identifyMode = false;

        if (_currentIndex >= _questions.Count)
        {
            ShowResults();
            return;
        }

        var q = _questions[_currentIndex];
        _questionText.text = q.questionText;
        _progressText.text = $"Question {_currentIndex + 1} / {_questions.Count}";
        _scoreText.text = $"Score: {_score}";
        _streakText.text = _streak > 1 ? $"Streak: {_streak}x" : "";

        if (_progressBar != null)
            _progressBar.fillAmount = (float)_currentIndex / _questions.Count;

        _explanationPanel.SetActive(false);
        _nextButton.SetActive(false);

        if (q.questionType == QuizQuestionType.IdentifyRegion)
        {
            _optionsPanel.SetActive(false);
            SetupIdentifyRegion(q);
        }
        else
        {
            _optionsPanel.SetActive(true);
            var answers = new List<string> { q.correctAnswer };
            int wrongCount = q.difficulty == QuizDifficulty.Easy ? 1
                : q.difficulty == QuizDifficulty.Medium ? 3 : 4;
            for (int i = 0; i < Mathf.Min(wrongCount, q.wrongAnswers.Length); i++)
                answers.Add(q.wrongAnswers[i]);
            Shuffle(answers);

            for (int i = 0; i < _optionButtons.Count; i++)
            {
                if (i < answers.Count)
                {
                    _optionButtons[i].gameObject.SetActive(true);
                    _optionTexts[i].text = answers[i];
                    _optionButtons[i].image.color = BtnBlue;
                    _optionButtons[i].interactable = true;
                    string ans = answers[i];
                    _optionButtons[i].onClick.RemoveAllListeners();
                    _optionButtons[i].onClick.AddListener(() => OnAnswerSelected(ans));
                }
                else
                {
                    _optionButtons[i].gameObject.SetActive(false);
                }
            }
        }

        _waitingForNext = false;
    }

    void SetupIdentifyRegion(QuizQuestion q)
    {
        _identifyMode = true;
        _identifyTarget = null;

        var bm = FindFirstObjectByType<BrainManager>();
        if (bm == null || bm.brainRoot == null) { SkipToMultipleChoice(q); return; }

        var allRegions = bm.brainRoot.GetComponentsInChildren<BrainRegion>(true);
        foreach (var r in allRegions)
        {
            if (r.regionData != null && r.regionData.displayName != null &&
                r.regionData.displayName.IndexOf(q.targetRegionKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _identifyTarget = r;
                break;
            }
        }

        if (_identifyTarget == null) { SkipToMultipleChoice(q); return; }

        var rend = _identifyTarget.GetComponent<Renderer>();
        if (rend != null)
        {
            _identifyHighlight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _identifyHighlight.name = "QuizIdentifyHighlight";
            var col = _identifyHighlight.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _identifyHighlight.transform.position = rend.bounds.center;
            _identifyHighlight.transform.localScale = rend.bounds.size * 1.15f;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(1f, 0.3f, 0.3f, 0.5f);
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            _identifyHighlight.GetComponent<Renderer>().material = mat;
        }

        _questionText.text = $"{q.questionText}\n\n<i>Click on the highlighted region on the brain.</i>";
        _identifyCandidates = allRegions;
    }

    void SkipToMultipleChoice(QuizQuestion q)
    {
        _identifyMode = false;
        _optionsPanel.SetActive(true);
        var answers = new List<string> { q.correctAnswer };
        if (q.wrongAnswers != null)
            foreach (var w in q.wrongAnswers) answers.Add(w);
        if (answers.Count < 2)
        {
            answers.Add("Unknown");
            answers.Add("Not sure");
        }
        Shuffle(answers);
        for (int i = 0; i < _optionButtons.Count; i++)
        {
            if (i < answers.Count)
            {
                _optionButtons[i].gameObject.SetActive(true);
                _optionTexts[i].text = answers[i];
                _optionButtons[i].image.color = BtnBlue;
                _optionButtons[i].interactable = true;
                string ans = answers[i];
                _optionButtons[i].onClick.RemoveAllListeners();
                _optionButtons[i].onClick.AddListener(() => OnAnswerSelected(ans));
            }
            else _optionButtons[i].gameObject.SetActive(false);
        }
    }

    void ClearIdentifyHighlight()
    {
        if (_identifyHighlight != null) Destroy(_identifyHighlight);
        _identifyHighlight = null;
        _identifyTarget = null;
        _identifyCandidates = null;
    }

    void OnAnswerSelected(string answer)
    {
        if (_waitingForNext || !_active) return;
        _waitingForNext = true;

        var q = _questions[_currentIndex];
        bool correct = answer == q.correctAnswer;

        if (correct)
        {
            _score++;
            _streak++;
            if (_streak > _bestStreak) _bestStreak = _streak;
            HapticFeedback.PulseBoth(0.3f, 0.2f);
            if (SoundManager.Instance != null) SoundManager.Instance.PlayCorrect();
        }
        else
        {
            _streak = 0;
            HapticFeedback.PulseBoth(0.8f, 0.4f);
            if (SoundManager.Instance != null) SoundManager.Instance.PlayWrong();
        }

        foreach (var btn in _optionButtons)
        {
            btn.interactable = false;
            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null && txt.text == q.correctAnswer)
                btn.image.color = CorrectGreen;
            else if (txt != null && txt.text == answer && !correct)
                btn.image.color = WrongRed;
        }

        _explanationText.text = (correct ? "Correct! " : "Incorrect. ") + q.explanation;
        _explanationPanel.SetActive(true);
        _nextButton.SetActive(true);

        _scoreText.text = $"Score: {_score}";
        _streakText.text = _streak > 1 ? $"Streak: {_streak}x" : "";
    }

    public void OnNextPressed()
    {
        _currentIndex++;
        ShowQuestion();
    }

    void ShowResults()
    {
        _optionsPanel.SetActive(false);
        _explanationPanel.SetActive(false);
        _nextButton.SetActive(false);

        // Freeze the elapsed time before doing anything else so the recorded
        // score reflects the real "time taken to complete" rather than the
        // time spent on the results screen.
        _quizElapsedFrozenSeconds = Mathf.Max(1, Mathf.FloorToInt(Time.time - _quizStartTime));
        _active = false;

        LeaderboardManager.RecordScore(SessionData.UserName, _score, _questions.Count,
            "MCQ", _quizElapsedFrozenSeconds);

        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.CheckMCQScore(_score, _questions.Count);
            AchievementManager.Instance.CheckStreak(_bestStreak);
        }

        _finishPanel.SetActive(true);
        var resultText = _finishPanel.GetComponentInChildren<Text>();
        if (resultText != null)
        {
            float pct = _questions.Count > 0 ? (float)_score / _questions.Count * 100f : 0;
            string timeStr = LeaderboardManager.FormatElapsed(_quizElapsedFrozenSeconds);
            resultText.text = $"Quiz Complete!\n\nScore: {_score} / {_questions.Count}  ({pct:F0}%)\n" +
                              $"Time: {timeStr}     Best Streak: {_bestStreak}x\n\n" +
                              "Check the Leaderboard to compare!";
        }

        if (_progressBar != null) _progressBar.fillAmount = 1f;
        _progressText.text = "Complete!";
        if (_timerText != null)
            _timerText.text = $"Time: {LeaderboardManager.FormatElapsed(_quizElapsedFrozenSeconds)}";
    }

    // ========================= UI BUILDING =========================

    void BuildUI()
    {
        if (_quizCanvas != null) Destroy(_quizCanvas);

        var cam = Camera.main;
        _quizCanvas = new GameObject("QuizCanvas");
        if (cam != null)
        {
            _quizCanvas.transform.SetParent(cam.transform, false);
            _quizCanvas.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            _quizCanvas.transform.localRotation = Quaternion.identity;
        }

        var canvas = _quizCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _quizCanvas.AddComponent<CanvasScaler>();
        _quizCanvas.AddComponent<GraphicRaycaster>();

        var rt = _quizCanvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(900, 700);
        rt.localScale = Vector3.one * 0.001f;

        var trackedRaycaster = _quizCanvas.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // Background
        var bg = MakeRect("Bg", _quizCanvas.transform, Vector2.zero, rt.sizeDelta);
        bg.gameObject.AddComponent<Image>().color = PanelBg;

        // Progress
        _progressText = MakeText("Progress", bg.transform, new Vector2(-280, 320), new Vector2(260, 30),
            "", 20, FontStyle.Normal, TextDim, TextAnchor.MiddleLeft, font);
        _scoreText = MakeText("Score", bg.transform, new Vector2(280, 320), new Vector2(260, 30),
            "Score: 0", 20, FontStyle.Bold, TextWhite, TextAnchor.MiddleRight, font);
        _streakText = MakeText("Streak", bg.transform, new Vector2(0, 320), new Vector2(180, 30),
            "", 20, FontStyle.Bold, new Color(1f, 0.8f, 0.2f), TextAnchor.MiddleCenter, font);
        // Live timer (assessment mode only)
        _timerText = MakeText("Timer", bg.transform, new Vector2(280, 350), new Vector2(260, 28),
            "Time: 0:00", 18, FontStyle.Bold, new Color(0.45f, 0.85f, 1f), TextAnchor.MiddleRight, font);

        // Progress bar
        var barBg = MakeRect("BarBg", bg.transform, new Vector2(0, 295), new Vector2(800, 8));
        barBg.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f);
        var barFill = MakeRect("BarFill", barBg.transform, Vector2.zero, new Vector2(800, 8));
        var fillImg = barFill.gameObject.AddComponent<Image>();
        fillImg.color = new Color(0.3f, 0.7f, 1f);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 0f;
        _progressBar = fillImg;

        // Question
        _questionText = MakeText("Question", bg.transform, new Vector2(0, 210), new Vector2(780, 100),
            "", 28, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);

        // Options panel
        _optionsPanel = new GameObject("Options");
        _optionsPanel.transform.SetParent(bg.transform, false);
        var optRT = _optionsPanel.AddComponent<RectTransform>();
        optRT.anchoredPosition = new Vector2(0, 30);
        optRT.sizeDelta = new Vector2(700, 300);

        _optionButtons.Clear();
        _optionTexts.Clear();
        for (int i = 0; i < 5; i++)
        {
            float y = 120 - i * 65;
            var btnGO = MakeRect($"Opt{i}", optRT, new Vector2(0, y), new Vector2(650, 55));
            var img = btnGO.gameObject.AddComponent<Image>();
            img.color = BtnBlue;
            var btn = btnGO.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var txt = MakeText($"OptText{i}", btnGO.transform, Vector2.zero, new Vector2(620, 50),
                "", 22, FontStyle.Normal, TextWhite, TextAnchor.MiddleCenter, font);
            _optionButtons.Add(btn);
            _optionTexts.Add(txt);
        }

        // Explanation panel
        _explanationPanel = MakeRect("Explanation", bg.transform, new Vector2(0, -190), new Vector2(780, 100)).gameObject;
        _explanationPanel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.14f, 0.9f);
        _explanationText = MakeText("ExplText", _explanationPanel.transform, Vector2.zero, new Vector2(750, 90),
            "", 20, FontStyle.Italic, TextDim, TextAnchor.MiddleCenter, font);
        _explanationPanel.SetActive(false);

        // Next button
        _nextButton = MakeRect("NextBtn", bg.transform, new Vector2(0, -280), new Vector2(200, 50)).gameObject;
        _nextButton.AddComponent<Image>().color = BtnGreen;
        var nextBtn = _nextButton.AddComponent<Button>();
        nextBtn.targetGraphic = _nextButton.GetComponent<Image>();
        MakeText("NextTxt", _nextButton.transform, Vector2.zero, new Vector2(180, 45),
            "Next", 24, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);
        nextBtn.onClick.AddListener(OnNextPressed);
        _nextButton.SetActive(false);

        // Finish panel
        _finishPanel = MakeRect("FinishPanel", bg.transform, new Vector2(0, 0), new Vector2(700, 350)).gameObject;
        _finishPanel.AddComponent<Image>().color = new Color(0.08f, 0.1f, 0.15f, 0.95f);
        MakeText("FinishText", _finishPanel.transform, new Vector2(0, 40), new Vector2(650, 250),
            "", 26, FontStyle.Normal, TextWhite, TextAnchor.MiddleCenter, font);
        var doneGO = MakeRect("DoneBtn", _finishPanel.transform, new Vector2(0, -140), new Vector2(250, 55));
        doneGO.gameObject.AddComponent<Image>().color = BtnOrange;
        var doneBtn = doneGO.gameObject.AddComponent<Button>();
        doneBtn.targetGraphic = doneGO.GetComponent<Image>();
        MakeText("DoneTxt", doneGO.transform, Vector2.zero, new Vector2(230, 50),
            "Return to Menu", 20, FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);
        doneBtn.onClick.AddListener(EndQuizToMainMenu);
        _finishPanel.SetActive(false);
    }

    // ========================= HELPERS =========================

    static RectTransform MakeRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Text MakeText(string name, Transform parent, Vector2 pos, Vector2 size,
        string text, int fontSize, FontStyle style, Color color, TextAnchor align, Font font)
    {
        var rt = MakeRect(name, parent, pos, size);
        var t = rt.gameObject.AddComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.font = font;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static GameObject MakeButton(string name, Transform parent, Vector2 pos, Vector2 size,
        string label, Color bg, Font font)
    {
        var rt = MakeRect(name, parent, pos, size);
        var go = rt.gameObject;
        go.AddComponent<Image>().color = bg;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        var colors = btn.colors;
        colors.normalColor = bg; colors.highlightedColor = bg * 1.2f; colors.pressedColor = bg * 0.8f;
        btn.colors = colors;

        MakeText(name + "_Lbl", go.transform, Vector2.zero, size,
            label, Mathf.Min(18, (int)(size.y * 0.5f)), FontStyle.Bold, TextWhite, TextAnchor.MiddleCenter, font);
        return go;
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
