using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MathMinigameController : MonoBehaviour
{
    [Serializable]
    public class MathQuestionDefinition
    {
        [Tooltip("Optional label shown in the header.")]
        public string title = "Question";

        [Tooltip("Optional localization key for the question text.")]
        public string questionTextKey;
        [TextArea(3, 8)]
        public string questionText;

        [Tooltip("Optional image shown with the question.")]
        public Sprite questionSprite;

        [Tooltip("Primary correct answer.")]
        public string correctAnswer = "0";

        [Tooltip("Optional alternative accepted answers.")]
        public string[] alternateAnswers = Array.Empty<string>();

        [Tooltip("Optional localization keys for hint lines.")]
        public string[] hintTextKeys = new string[3];
        [TextArea(2, 4)]
        public string[] hintTexts = new string[3];
    }

    [Header("Flow")]
    public MathMinigameConfig config;
    [Tooltip("Only these FLOW_ID values will run this controller.")]
    public string[] supportedFlowIds = new[] { "CLASS1_D2" };
    [Tooltip("Penalty applied when the player gives up.")]
    public int penaltyOnGiveUp = 1;
    [Tooltip("Delay before advancing after a correct answer.")]
    public float correctAnswerDelaySeconds = 0.55f;

    [Header("Questions")]
    [Tooltip("Three math questions are expected, but the controller supports any count >= 1.")]
    public List<MathQuestionDefinition> questions = new List<MathQuestionDefinition>();

    [Header("Drawing Pad")]
    public Vector2Int drawingTextureSize = new Vector2Int(1024, 1024);
    [Range(1, 32)] public int brushRadius = 5;
    public Color drawingBackgroundColor = new Color(0.98f, 0.98f, 0.98f, 1f);
    public Color brushColor = new Color(0.14f, 0.19f, 0.28f, 1f);
    public bool clearCanvasOnQuestionChange = true;

    [Header("UI")]
    public TMP_FontAsset uiFontAsset;
    public Color dimColor = new Color(0.08f, 0.08f, 0.12f, 0.82f);
    public Color panelColor = new Color(0.96f, 0.94f, 0.90f, 0.98f);
    public Color accentColor = new Color(0.16f, 0.24f, 0.40f, 1f);
    public Color outlineColor = new Color(0.15f, 0.12f, 0.10f, 0.35f);
    public Color hintPanelColor = new Color(0.91f, 0.96f, 1f, 0.96f);
    public Color feedbackSuccessColor = new Color(0.20f, 0.58f, 0.28f, 1f);
    public Color feedbackErrorColor = new Color(0.78f, 0.23f, 0.18f, 1f);
    public string friendBubbleName = "옆 친구";
    public float friendBubbleShowSeconds = 3.2f;

    private Canvas uiCanvas;
    private RectTransform uiRootRect;
    private TextMeshProUGUI headerText;
    private TextMeshProUGUI progressText;
    private TextMeshProUGUI questionTextUI;
    private Image questionImage;
    private TextMeshProUGUI hintsText;
    private TextMeshProUGUI feedbackText;
    private TMP_InputField answerInput;
    private Button hintButton;
    private TextMeshProUGUI hintButtonLabel;
    private ScrollRect hintsScrollRect;
    private RectTransform drawPadRect;
    private RawImage drawPadImage;
    private RectTransform friendBubbleRoot;
    private TextMeshProUGUI friendBubbleNameText;
    private TextMeshProUGUI friendBubbleBodyText;
    private Coroutine friendBubbleRoutine;
    private Coroutine focusRoutine;

    private Texture2D drawingTexture;
    private bool isDrawing;
    private Vector2Int lastDrawPixel = new Vector2Int(-1, -1);
    private int currentQuestionIndex;
    private int revealedHintCount;
    private bool ended;
    private bool advancing;
    private Coroutine advanceRoutine;

    private Camera uiCamera;

    private void Awake()
    {
        if (!ShouldRunForCurrentFlow())
        {
            enabled = false;
            return;
        }

        ApplyConfigIfNeeded();
        EnsureUIFont();
        EnsureQuestionsOrFallback();
        EnsureEventSystem();

        BuildRuntimeUI();
        BuildDrawingTexture();
        LoadQuestion(0);

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;

        CleanupRuntimeObjects();
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;

        CleanupRuntimeObjects();
    }

    private void Update()
    {
        if (ended)
            return;

        HandleDrawingInput();
        HandleAnswerTypingFallback();

        if (!advancing && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            SubmitAnswer();

        if (Input.GetKeyDown(KeyCode.Escape))
            End(false);
    }

    private bool ShouldRunForCurrentFlow()
    {
        string flowId = FlowContext.CurrentId;
        if (string.IsNullOrEmpty(flowId))
            flowId = PlayerPrefs.GetString("FLOW_ID", string.Empty);

        if (string.IsNullOrEmpty(flowId))
            return false;

        if (supportedFlowIds == null || supportedFlowIds.Length == 0)
            return string.Equals(flowId, "CLASS1_D2", StringComparison.OrdinalIgnoreCase);

        for (int i = 0; i < supportedFlowIds.Length; i++)
        {
            if (string.Equals(flowId, supportedFlowIds[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void EnsureQuestionsOrFallback()
    {
        if (questions != null && questions.Count > 0)
            return;

        questions = new List<MathQuestionDefinition>
        {
            new MathQuestionDefinition
            {
                title = "Q1",
                questionText = "문제 텍스트를 여기에 입력하세요.",
                correctAnswer = "0",
                hintTexts = new[] { "첫 번째 힌트", "두 번째 힌트", "세 번째 힌트" }
            },
            new MathQuestionDefinition
            {
                title = "Q2",
                questionText = "문제 이미지는 questionSprite에 넣을 수 있습니다.",
                correctAnswer = "0",
                hintTexts = new[] { "첫 번째 힌트", "두 번째 힌트", "세 번째 힌트" }
            },
            new MathQuestionDefinition
            {
                title = "Q3",
                questionText = "CSV를 붙일 예정이면 questionTextKey / hintTextKeys를 써도 됩니다.",
                correctAnswer = "0",
                hintTexts = new[] { "첫 번째 힌트", "두 번째 힌트", "세 번째 힌트" }
            }
        };
    }

    private void BuildRuntimeUI()
    {
        var canvasGo = new GameObject("__MathMinigameUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        uiCanvas = canvasGo.GetComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.overrideSorting = true;
        uiCanvas.sortingOrder = -10;
        uiCamera = uiCanvas.worldCamera;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var root = CreateUIObject("Root", canvasGo.transform, typeof(Image));
        var rootRect = root.GetComponent<RectTransform>();
        StretchFull(rootRect);
        uiRootRect = rootRect;
        root.GetComponent<Image>().color = new Color(dimColor.r, dimColor.g, dimColor.b, 0.24f);

        var leftPaper = CreateUIObject("QuestionPaper", root.transform, typeof(Image));
        var leftPaperRect = leftPaper.GetComponent<RectTransform>();
        leftPaperRect.anchorMin = new Vector2(0.03f, 0.08f);
        leftPaperRect.anchorMax = new Vector2(0.45f, 0.92f);
        leftPaperRect.offsetMin = Vector2.zero;
        leftPaperRect.offsetMax = Vector2.zero;
        leftPaperRect.localRotation = Quaternion.Euler(0f, 0f, -0.8f);
        StylePaper(leftPaper.GetComponent<Image>(), leftPaper);

        var rightPaper = CreateUIObject("WorkPaper", root.transform, typeof(Image));
        var rightPaperRect = rightPaper.GetComponent<RectTransform>();
        rightPaperRect.anchorMin = new Vector2(0.46f, 0.04f);
        rightPaperRect.anchorMax = new Vector2(0.985f, 0.935f);
        rightPaperRect.offsetMin = Vector2.zero;
        rightPaperRect.offsetMax = Vector2.zero;
        rightPaperRect.localRotation = Quaternion.Euler(0f, 0f, 5.4f);
        StylePaper(rightPaper.GetComponent<Image>(), rightPaper);

        BuildLeftPanel(leftPaper.transform);
        BuildRightPanel(rightPaper.transform);
        BuildFriendBubble(root.transform);
    }

    private void BuildLeftPanel(Transform parent)
    {
        progressText = CreateText("ProgressText", parent, 26f, FontStyles.Bold);
        progressText.rectTransform.anchorMin = new Vector2(0.08f, 0.91f);
        progressText.rectTransform.anchorMax = new Vector2(0.92f, 0.985f);
        progressText.rectTransform.offsetMin = Vector2.zero;
        progressText.rectTransform.offsetMax = Vector2.zero;
        progressText.color = accentColor;
        progressText.alignment = TextAlignmentOptions.MidlineLeft;

        var descriptionFrame = CreateUIObject("DescriptionFrame", parent, typeof(Image));
        var descriptionRect = descriptionFrame.GetComponent<RectTransform>();
        descriptionRect.anchorMin = new Vector2(0.08f, 0.64f);
        descriptionRect.anchorMax = new Vector2(0.92f, 0.84f);
        descriptionRect.offsetMin = Vector2.zero;
        descriptionRect.offsetMax = Vector2.zero;
        descriptionFrame.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.7f);
        AddThinOutline(descriptionFrame);

        questionTextUI = CreateText("QuestionText", descriptionFrame.transform, 25f, FontStyles.Normal);
        questionTextUI.color = new Color(0.10f, 0.10f, 0.12f, 1f);
        questionTextUI.alignment = TextAlignmentOptions.TopLeft;
        questionTextUI.enableWordWrapping = true;
        questionTextUI.overflowMode = TextOverflowModes.Overflow;
        StretchFull(questionTextUI.rectTransform);
        questionTextUI.margin = new Vector4(18f, 18f, 18f, 18f);

        var imageFrame = CreateUIObject("QuestionImageFrame", parent, typeof(Image));
        var imageRect = imageFrame.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.08f, 0.20f);
        imageRect.anchorMax = new Vector2(0.92f, 0.57f);
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        imageFrame.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.82f);
        AddThinOutline(imageFrame);

        questionImage = CreateUIObject("QuestionImage", imageFrame.transform, typeof(Image)).GetComponent<Image>();
        StretchWithPadding(questionImage.rectTransform, 12f);
        questionImage.preserveAspect = true;
        questionImage.color = Color.white;

        var answerLabel = CreateText("AnswerLabel", parent, 24f, FontStyles.Bold);
        answerLabel.text = L("MINIGAME_MATH_ANSWER_SECTION", "정답 제출", "Answer");
        answerLabel.color = accentColor;
        answerLabel.rectTransform.anchorMin = new Vector2(0.08f, 0.10f);
        answerLabel.rectTransform.anchorMax = new Vector2(0.92f, 0.16f);
        answerLabel.rectTransform.offsetMin = Vector2.zero;
        answerLabel.rectTransform.offsetMax = Vector2.zero;
        answerLabel.alignment = TextAlignmentOptions.BottomLeft;

        var answerRow = CreateUIObject("AnswerRow", parent, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var answerRowRect = answerRow.GetComponent<RectTransform>();
        answerRowRect.anchorMin = new Vector2(0.08f, 0.03f);
        answerRowRect.anchorMax = new Vector2(0.92f, 0.095f);
        answerRowRect.offsetMin = Vector2.zero;
        answerRowRect.offsetMax = Vector2.zero;
        var answerLayout = answerRow.GetComponent<HorizontalLayoutGroup>();
        answerLayout.spacing = 12f;
        answerLayout.childControlWidth = false;
        answerLayout.childControlHeight = true;
        answerLayout.childForceExpandWidth = false;
        answerLayout.childForceExpandHeight = false;

        answerInput = CreateInputField("AnswerInput", answerRow.transform);
        var answerInputLayout = answerInput.gameObject.AddComponent<LayoutElement>();
        answerInputLayout.preferredHeight = 66f;
        answerInputLayout.flexibleWidth = 1f;

        var submitButton = CreateButton("SubmitButton", answerRow.transform, out var submitLabel);
        submitButton.onClick.AddListener(SubmitAnswer);
        SetPreferredSize(submitButton.transform as RectTransform, 150f, 66f);
        submitLabel.text = L("MINIGAME_MATH_SUBMIT", "제출", "Submit");

        feedbackText = CreateText("FeedbackText", parent, 22f, FontStyles.Bold);
        feedbackText.color = feedbackSuccessColor;
        feedbackText.rectTransform.anchorMin = new Vector2(0.08f, 0.16f);
        feedbackText.rectTransform.anchorMax = new Vector2(0.92f, 0.20f);
        feedbackText.rectTransform.offsetMin = Vector2.zero;
        feedbackText.rectTransform.offsetMax = Vector2.zero;
        feedbackText.alignment = TextAlignmentOptions.MidlineLeft;
        feedbackText.enableWordWrapping = true;
    }

    private void BuildRightPanel(Transform parent)
    {
        var note = CreateText("DrawTitle", parent, 28f, FontStyles.Bold);
        note.rectTransform.anchorMin = new Vector2(0.08f, 0.91f);
        note.rectTransform.anchorMax = new Vector2(0.86f, 0.985f);
        note.rectTransform.offsetMin = Vector2.zero;
        note.rectTransform.offsetMax = Vector2.zero;
        note.color = accentColor;
        note.alignment = TextAlignmentOptions.TopLeft;

        var drawFrame = CreateUIObject("DrawFrame", parent, typeof(Image));
        var drawFrameRect = drawFrame.GetComponent<RectTransform>();
        drawFrameRect.anchorMin = new Vector2(0.05f, 0.18f);
        drawFrameRect.anchorMax = new Vector2(0.95f, 0.90f);
        drawFrameRect.offsetMin = Vector2.zero;
        drawFrameRect.offsetMax = Vector2.zero;
        drawFrame.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.95f);
        AddThinOutline(drawFrame);

        drawPadImage = CreateUIObject("DrawPad", drawFrame.transform, typeof(RawImage)).GetComponent<RawImage>();
        StretchWithPadding(drawPadImage.rectTransform, 8f);
        drawPadRect = drawPadImage.rectTransform;
        drawPadImage.color = Color.white;

        var hintsPanel = CreateUIObject("HintsPanel", parent, typeof(Image));
        var hintsRect = hintsPanel.GetComponent<RectTransform>();
        hintsRect.anchorMin = new Vector2(0.06f, 0.02f);
        hintsRect.anchorMax = new Vector2(0.58f, 0.18f);
        hintsRect.offsetMin = Vector2.zero;
        hintsRect.offsetMax = Vector2.zero;
        hintsPanel.GetComponent<Image>().color = hintPanelColor;
        AddThinOutline(hintsPanel);
        hintsScrollRect = hintsPanel.AddComponent<ScrollRect>();
        hintsScrollRect.horizontal = false;
        hintsScrollRect.vertical = true;
        hintsScrollRect.movementType = ScrollRect.MovementType.Clamped;
        hintsScrollRect.scrollSensitivity = 24f;

        var hintsViewport = CreateUIObject("HintsViewport", hintsPanel.transform, typeof(Image), typeof(RectMask2D));
        var hintsViewportRect = hintsViewport.GetComponent<RectTransform>();
        StretchFull(hintsViewportRect);
        hintsViewportRect.offsetMin = new Vector2(8f, 8f);
        hintsViewportRect.offsetMax = new Vector2(-8f, -8f);
        var hintsViewportImage = hintsViewport.GetComponent<Image>();
        hintsViewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        hintsViewportImage.raycastTarget = true;

        var hintsContent = CreateUIObject("HintsContent", hintsViewport.transform, typeof(ContentSizeFitter));
        var hintsContentRect = hintsContent.GetComponent<RectTransform>();
        hintsContentRect.anchorMin = new Vector2(0f, 1f);
        hintsContentRect.anchorMax = new Vector2(1f, 1f);
        hintsContentRect.pivot = new Vector2(0.5f, 1f);
        hintsContentRect.anchoredPosition = Vector2.zero;
        hintsContentRect.sizeDelta = Vector2.zero;
        var hintsContentFitter = hintsContent.GetComponent<ContentSizeFitter>();
        hintsContentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        hintsContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        hintsText = CreateText("HintsText", hintsContent.transform, 20f, FontStyles.Normal);
        hintsText.color = new Color(0.14f, 0.18f, 0.24f, 1f);
        hintsText.alignment = TextAlignmentOptions.TopLeft;
        hintsText.enableWordWrapping = true;
        hintsText.overflowMode = TextOverflowModes.Overflow;
        hintsText.rectTransform.anchorMin = new Vector2(0f, 1f);
        hintsText.rectTransform.anchorMax = new Vector2(1f, 1f);
        hintsText.rectTransform.pivot = new Vector2(0.5f, 1f);
        hintsText.rectTransform.anchoredPosition = Vector2.zero;
        hintsText.rectTransform.sizeDelta = Vector2.zero;
        hintsText.margin = new Vector4(12f, 10f, 12f, 10f);
        var hintsTextFitter = hintsText.gameObject.AddComponent<ContentSizeFitter>();
        hintsTextFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        hintsTextFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        hintsScrollRect.viewport = hintsViewportRect;
        hintsScrollRect.content = hintsContentRect;

        var clearButton = CreateButton("ClearButton", parent, out var clearLabel);
        clearButton.onClick.AddListener(ClearDrawingCanvas);
        var clearRect = clearButton.transform as RectTransform;
        clearRect.anchorMin = new Vector2(0.61f, 0.03f);
        clearRect.anchorMax = new Vector2(0.76f, 0.10f);
        clearRect.offsetMin = Vector2.zero;
        clearRect.offsetMax = Vector2.zero;

        hintButton = CreateButton("HintButton", parent, out hintButtonLabel);
        hintButton.onClick.AddListener(RevealNextHint);
        var hintRect = hintButton.transform as RectTransform;
        hintRect.anchorMin = new Vector2(0.78f, 0.02f);
        hintRect.anchorMax = new Vector2(0.96f, 0.11f);
        hintRect.offsetMin = Vector2.zero;
        hintRect.offsetMax = Vector2.zero;

        note.text = L("MINIGAME_MATH_DRAW_TITLE", "풀이 쓰는 곳", "Work Area");
        hintButtonLabel.text = L("MINIGAME_MATH_HINT_BUTTON_SHORT", "물어보기", "Hint");
        clearLabel.text = L("MINIGAME_MATH_CLEAR", "지우기", "Clear");
    }

    private void BuildFriendBubble(Transform parent)
    {
        var bubble = CreateUIObject("FriendBubble", parent, typeof(Image));
        friendBubbleRoot = bubble.GetComponent<RectTransform>();
        friendBubbleRoot.anchorMin = new Vector2(0.43f, 0.69f);
        friendBubbleRoot.anchorMax = new Vector2(0.67f, 0.86f);
        friendBubbleRoot.offsetMin = Vector2.zero;
        friendBubbleRoot.offsetMax = Vector2.zero;
        friendBubbleRoot.localRotation = Quaternion.Euler(0f, 0f, -4f);

        var bubbleImage = bubble.GetComponent<Image>();
        bubbleImage.color = new Color(1f, 1f, 1f, 0.98f);
        AddThinOutline(bubble);

        var shadow = bubble.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.16f);
        shadow.effectDistance = new Vector2(8f, -8f);

        var tail = CreateUIObject("Tail", bubble.transform, typeof(Image));
        var tailRect = tail.GetComponent<RectTransform>();
        tailRect.anchorMin = new Vector2(0f, 0.18f);
        tailRect.anchorMax = new Vector2(0f, 0.18f);
        tailRect.pivot = new Vector2(0.5f, 0.5f);
        tailRect.sizeDelta = new Vector2(34f, 34f);
        tailRect.anchoredPosition = new Vector2(-18f, -12f);
        tailRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tail.GetComponent<Image>().color = bubbleImage.color;

        var nameGo = CreateText("Name", bubble.transform, 20f, FontStyles.Bold);
        friendBubbleNameText = nameGo;
        friendBubbleNameText.color = accentColor;
        friendBubbleNameText.alignment = TextAlignmentOptions.MidlineLeft;
        friendBubbleNameText.rectTransform.anchorMin = new Vector2(0f, 0.70f);
        friendBubbleNameText.rectTransform.anchorMax = new Vector2(1f, 0.98f);
        friendBubbleNameText.rectTransform.offsetMin = new Vector2(18f, 0f);
        friendBubbleNameText.rectTransform.offsetMax = new Vector2(-18f, 0f);

        var bodyGo = CreateText("Body", bubble.transform, 23f, FontStyles.Normal);
        friendBubbleBodyText = bodyGo;
        friendBubbleBodyText.color = new Color(0.12f, 0.12f, 0.14f, 1f);
        friendBubbleBodyText.alignment = TextAlignmentOptions.TopLeft;
        friendBubbleBodyText.enableWordWrapping = true;
        friendBubbleBodyText.rectTransform.anchorMin = new Vector2(0f, 0f);
        friendBubbleBodyText.rectTransform.anchorMax = new Vector2(1f, 0.78f);
        friendBubbleBodyText.rectTransform.offsetMin = new Vector2(18f, 14f);
        friendBubbleBodyText.rectTransform.offsetMax = new Vector2(-18f, -8f);

        friendBubbleRoot.gameObject.SetActive(false);
    }

    private void BuildDrawingTexture()
    {
        int width = Mathf.Max(64, drawingTextureSize.x);
        int height = Mathf.Max(64, drawingTextureSize.y);

        drawingTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        drawingTexture.filterMode = FilterMode.Point;
        drawingTexture.wrapMode = TextureWrapMode.Clamp;

        if (drawPadImage != null)
            drawPadImage.texture = drawingTexture;

        ClearDrawingCanvas();
    }

    private void LoadQuestion(int index)
    {
        currentQuestionIndex = Mathf.Clamp(index, 0, questions.Count - 1);
        revealedHintCount = 0;
        advancing = false;

        if (clearCanvasOnQuestionChange)
            ClearDrawingCanvas();

        if (answerInput != null)
        {
            answerInput.text = string.Empty;
            RequestAnswerInputFocus();
        }

        RefreshTexts();
        feedbackText.text = string.Empty;
    }

    private void RefreshTexts()
    {
        if (questions == null || questions.Count == 0)
            return;

        var question = questions[currentQuestionIndex];
        string displayTitle = string.IsNullOrWhiteSpace(question.title)
            ? $"Q{currentQuestionIndex + 1}"
            : question.title;

        progressText.text = string.Format(
            L("MINIGAME_MATH_PROGRESS", "{0}  |  문제 {1}/{2}", "{0}  |  Question {1}/{2}"),
            displayTitle,
            currentQuestionIndex + 1,
            questions.Count);

        questionTextUI.text = ResolveLocalizedText(question.questionTextKey, question.questionText);
        questionImage.sprite = question.questionSprite;
        questionImage.enabled = question.questionSprite != null;

        hintButtonLabel.text = string.Format(
            L("MINIGAME_MATH_HINT_BUTTON_FMT", "옆 친구 힌트 ({0}/3)", "Hint ({0}/3)"),
            Mathf.Min(revealedHintCount, 3));

        RefreshHints();
    }

    private void RevealNextHint()
    {
        if (advancing || questions == null || questions.Count == 0)
            return;

        int nextIndex = FindNextAvailableHintIndex(revealedHintCount);
        if (nextIndex < 0)
        {
            RefreshHints();
            return;
        }

        revealedHintCount = nextIndex + 1;
        ShowFriendBubble(ResolveHint(questions[currentQuestionIndex], nextIndex));
        RefreshHints();
    }

    private int FindNextAvailableHintIndex(int startIndex)
    {
        var question = questions[currentQuestionIndex];
        for (int i = Mathf.Max(0, startIndex); i < 3; i++)
        {
            string hint = ResolveHint(question, i);
            if (!string.IsNullOrWhiteSpace(hint))
                return i;
        }

        return -1;
    }

    private void RefreshHints()
    {
        if (questions == null || questions.Count == 0)
            return;

        var question = questions[currentQuestionIndex];
        var builder = new StringBuilder();
        int shown = 0;

        for (int i = 0; i < Mathf.Min(revealedHintCount, 3); i++)
        {
            string hint = ResolveHint(question, i);
            if (string.IsNullOrWhiteSpace(hint))
                continue;

            if (builder.Length > 0)
                builder.Append("\n\n");

            builder.Append(string.Format(
                L("MINIGAME_MATH_HINT_FMT", "옆 친구 {0}: {1}", "Classmate {0}: {1}"),
                shown + 1,
                hint));
            shown++;
        }

        if (builder.Length == 0)
            builder.Append(L("MINIGAME_MATH_HINT_EMPTY", "아직 힌트를 보지 않았어요.", "No hints revealed yet."));

        hintsText.text = builder.ToString();
        if (hintsScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            hintsScrollRect.verticalNormalizedPosition = 0f;
        }

        bool hasMore = FindNextAvailableHintIndex(revealedHintCount) >= 0;
        hintButton.interactable = hasMore;
        hintButtonLabel.text = string.Format(
            L("MINIGAME_MATH_HINT_BUTTON_FMT", "옆 친구 힌트 ({0}/3)", "Hint ({0}/3)"),
            shown);
    }

    private void ShowFriendBubble(string line)
    {
        if (friendBubbleRoot == null || friendBubbleBodyText == null)
            return;

        if (string.IsNullOrWhiteSpace(line))
            return;

        if (friendBubbleRoutine != null)
            StopCoroutine(friendBubbleRoutine);

        if (friendBubbleNameText != null)
            friendBubbleNameText.text = friendBubbleName;

        friendBubbleBodyText.text = line;
        friendBubbleRoot.gameObject.SetActive(true);
        friendBubbleRoot.SetAsLastSibling();
        friendBubbleRoutine = StartCoroutine(CoHideFriendBubble());
    }

    private IEnumerator CoHideFriendBubble()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.25f, friendBubbleShowSeconds));

        if (friendBubbleRoot != null)
            friendBubbleRoot.gameObject.SetActive(false);

        friendBubbleRoutine = null;
    }

    private void SubmitAnswer()
    {
        if (advancing || questions == null || questions.Count == 0 || answerInput == null)
            return;

        string answer = answerInput.text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(answer))
        {
            SetFeedback(
                L("MINIGAME_MATH_EMPTY_ANSWER", "정답을 입력해 주세요.", "Please enter an answer."),
                false);
            return;
        }

        if (IsAnswerCorrect(answer, questions[currentQuestionIndex]))
        {
            SetFeedback(L("MINIGAME_MATH_CORRECT", "정답!", "Correct!"), true);

            if (advanceRoutine != null)
                StopCoroutine(advanceRoutine);
            advanceRoutine = StartCoroutine(CoAdvanceAfterCorrect());
            return;
        }

        SetFeedback(
            L("MINIGAME_MATH_WRONG", "앗, 다시 한 번 풀어보자.", "Not quite. Try again."),
            false);
        RequestAnswerInputFocus();
    }

    private IEnumerator CoAdvanceAfterCorrect()
    {
        advancing = true;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, correctAnswerDelaySeconds));

        if (ended)
            yield break;

        int nextQuestion = currentQuestionIndex + 1;
        if (nextQuestion >= questions.Count)
        {
            End(true);
            yield break;
        }

        LoadQuestion(nextQuestion);
        advanceRoutine = null;
    }

    private bool IsAnswerCorrect(string answer, MathQuestionDefinition question)
    {
        string normalized = NormalizeAnswer(answer);
        if (string.IsNullOrEmpty(normalized))
            return false;

        if (normalized == NormalizeAnswer(question.correctAnswer))
            return true;

        if (question.alternateAnswers == null)
            return false;

        for (int i = 0; i < question.alternateAnswers.Length; i++)
        {
            if (normalized == NormalizeAnswer(question.alternateAnswers[i]))
                return true;
        }

        return false;
    }

    private static string NormalizeAnswer(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var builder = new StringBuilder(input.Length);
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (char.IsWhiteSpace(c))
                continue;
            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    private void HandleDrawingInput()
    {
        if (drawPadRect == null || drawingTexture == null || advancing)
            return;

        if (Input.GetMouseButtonUp(0))
        {
            isDrawing = false;
            lastDrawPixel = new Vector2Int(-1, -1);
            return;
        }

        if (!Input.GetMouseButton(0))
            return;

        if (!TryGetDrawPixelFromMouse(out var pixel))
            return;

        if (!isDrawing)
        {
            isDrawing = true;
            lastDrawPixel = pixel;
            PaintCircle(pixel.x, pixel.y, brushRadius);
            drawingTexture.Apply(false);
            return;
        }

        DrawBrushLine(lastDrawPixel, pixel);
        lastDrawPixel = pixel;
        drawingTexture.Apply(false);
    }

    private bool TryGetDrawPixelFromMouse(out Vector2Int pixel)
    {
        pixel = default;
        if (drawPadRect == null || drawingTexture == null)
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(drawPadRect, Input.mousePosition, uiCamera, out var local))
            return false;

        Rect rect = drawPadRect.rect;
        if (!rect.Contains(local))
            return false;

        float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
        float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);

        int px = Mathf.RoundToInt(normalizedX * (drawingTexture.width - 1));
        int py = Mathf.RoundToInt(normalizedY * (drawingTexture.height - 1));
        pixel = new Vector2Int(px, py);
        return true;
    }

    private void DrawBrushLine(Vector2Int from, Vector2Int to)
    {
        int x0 = from.x;
        int y0 = from.y;
        int x1 = to.x;
        int y1 = to.y;

        int dx = Mathf.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            PaintCircle(x0, y0, brushRadius);
            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private void PaintCircle(int cx, int cy, int radius)
    {
        if (drawingTexture == null)
            return;

        int rr = Mathf.Max(1, radius);
        for (int y = -rr; y <= rr; y++)
        {
            for (int x = -rr; x <= rr; x++)
            {
                if ((x * x) + (y * y) > rr * rr)
                    continue;

                int px = cx + x;
                int py = cy + y;
                if (px < 0 || py < 0 || px >= drawingTexture.width || py >= drawingTexture.height)
                    continue;

                drawingTexture.SetPixel(px, py, brushColor);
            }
        }
    }

    private void ClearDrawingCanvas()
    {
        if (drawingTexture == null)
            return;

        for (int y = 0; y < drawingTexture.height; y++)
        {
            for (int x = 0; x < drawingTexture.width; x++)
                drawingTexture.SetPixel(x, y, drawingBackgroundColor);
        }

        drawingTexture.Apply(false);
        isDrawing = false;
        lastDrawPixel = new Vector2Int(-1, -1);
    }

    private void SetFeedback(string message, bool success)
    {
        if (feedbackText == null)
            return;

        feedbackText.color = success ? feedbackSuccessColor : feedbackErrorColor;
        feedbackText.text = message;
    }

    private string ResolveHint(MathQuestionDefinition question, int index)
    {
        if (question == null || index < 0 || index >= 3)
            return string.Empty;

        string key = question.hintTextKeys != null && index < question.hintTextKeys.Length
            ? question.hintTextKeys[index]
            : string.Empty;
        string fallback = question.hintTexts != null && index < question.hintTexts.Length
            ? question.hintTexts[index]
            : string.Empty;

        return ResolveLocalizedText(key, fallback);
    }

    private string ResolveLocalizedText(string key, string fallback)
    {
        if (LocalizationManager.Instance == null || string.IsNullOrWhiteSpace(key))
            return fallback ?? string.Empty;

        string localized = LocalizationManager.Instance.GetLine(key);
        return localized == key ? (fallback ?? string.Empty) : localized;
    }

    private void OnLanguageChanged(Language _)
    {
        RefreshTexts();
    }

    private void End(bool success)
    {
        if (ended)
            return;

        ended = true;

        if (advanceRoutine != null)
        {
            StopCoroutine(advanceRoutine);
            advanceRoutine = null;
        }

        if (focusRoutine != null)
        {
            StopCoroutine(focusRoutine);
            focusRoutine = null;
        }

        CleanupRuntimeObjects();

        if (FlowManager.Instance != null)
        {
            int delta = success ? 0 : penaltyOnGiveUp;
            FlowManager.Instance.CompleteCurrentEvent(delta);
            return;
        }

        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            gm.MinigameFinished(success);
    }

    private void CleanupRuntimeObjects()
    {
        if (friendBubbleRoutine != null)
        {
            StopCoroutine(friendBubbleRoutine);
            friendBubbleRoutine = null;
        }

        if (focusRoutine != null)
        {
            StopCoroutine(focusRoutine);
            focusRoutine = null;
        }

        if (uiCanvas != null)
        {
            Destroy(uiCanvas.gameObject);
            uiCanvas = null;
        }

        if (drawingTexture != null)
        {
            Destroy(drawingTexture);
            drawingTexture = null;
        }
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(go);
    }

    private void RequestAnswerInputFocus()
    {
        if (answerInput == null)
            return;

        if (focusRoutine != null)
            StopCoroutine(focusRoutine);

        focusRoutine = StartCoroutine(CoFocusAnswerInput());
    }

    private IEnumerator CoFocusAnswerInput()
    {
        yield return null;

        if (answerInput == null)
        {
            focusRoutine = null;
            yield break;
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        answerInput.Select();
        answerInput.ActivateInputField();
        answerInput.MoveTextEnd(false);
        focusRoutine = null;
    }

    private void HandleAnswerTypingFallback()
    {
        if (answerInput == null || advancing)
            return;

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == answerInput.gameObject)
            return;

        string typed = Input.inputString;
        if (string.IsNullOrEmpty(typed))
            return;

        bool changed = false;
        for (int i = 0; i < typed.Length; i++)
        {
            char ch = typed[i];
            if (ch == '\b')
            {
                if (!string.IsNullOrEmpty(answerInput.text))
                {
                    answerInput.text = answerInput.text.Substring(0, answerInput.text.Length - 1);
                    changed = true;
                }

                continue;
            }

            if (ch == '\r' || ch == '\n')
                continue;

            if (char.IsControl(ch))
                continue;

            answerInput.text += ch;
            changed = true;
        }

        if (!changed)
            return;

        int caret = answerInput.text.Length;
        answerInput.caretPosition = caret;
        answerInput.stringPosition = caret;
        answerInput.selectionStringAnchorPosition = caret;
        answerInput.selectionStringFocusPosition = caret;
        answerInput.ForceLabelUpdate();
    }

    private void EnsureUIFont()
    {
        if (uiFontAsset != null)
            return;

        #if UNITY_EDITOR
        uiFontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Fonts/Galmuri11-Bold SDF.asset");
        #endif
    }

    private string L(string key, string fallbackKO, string fallbackEN)
    {
        Language lang = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetCurrentLanguage()
            : Language.Korean;
        string fallback = lang == Language.Korean ? fallbackKO : fallbackEN;

        if (LocalizationManager.Instance == null)
            return fallback;

        string value = LocalizationManager.Instance.GetLine(key);
        return value == key ? fallback : value;
    }

    private static GameObject CreateUIObject(string name, Transform parent, params Type[] components)
    {
        var finalTypes = new List<Type> { typeof(RectTransform) };
        if (components != null)
        {
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null || components[i] == typeof(RectTransform))
                    continue;
                finalTypes.Add(components[i]);
            }
        }

        var go = new GameObject(name, finalTypes.ToArray());
        go.transform.SetParent(parent, false);
        return go;
    }

    private void StylePaper(Image image, GameObject host)
    {
        if (image != null)
            image.color = panelColor;

        var shadow = host.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
        shadow.effectDistance = new Vector2(12f, -12f);

        AddThinOutline(host);
    }

    private void AddThinOutline(GameObject host)
    {
        var outline = host.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private void ApplyConfigIfNeeded()
    {
        if (config == null)
            return;

        if (config.supportedFlowIds != null && config.supportedFlowIds.Length > 0)
            supportedFlowIds = (string[])config.supportedFlowIds.Clone();

        penaltyOnGiveUp = config.penaltyOnGiveUp;
        correctAnswerDelaySeconds = config.correctAnswerDelaySeconds;

        if (config.questions != null && config.questions.Count > 0)
            questions = new List<MathQuestionDefinition>(config.questions);

        drawingTextureSize = config.drawingTextureSize;
        brushRadius = config.brushRadius;
        drawingBackgroundColor = config.drawingBackgroundColor;
        brushColor = config.brushColor;
        clearCanvasOnQuestionChange = config.clearCanvasOnQuestionChange;

        if (config.uiFontAsset != null)
            uiFontAsset = config.uiFontAsset;
        dimColor = config.dimColor;
        panelColor = config.panelColor;
        accentColor = config.accentColor;
        outlineColor = config.outlineColor;
        hintPanelColor = config.hintPanelColor;
        feedbackSuccessColor = config.feedbackSuccessColor;
        feedbackErrorColor = config.feedbackErrorColor;
        friendBubbleName = string.IsNullOrEmpty(config.friendBubbleName) ? friendBubbleName : config.friendBubbleName;
        friendBubbleShowSeconds = config.friendBubbleShowSeconds;
    }

    private GameObject CreatePanel(string name, Transform parent, float preferredWidth)
    {
        var panel = CreateUIObject(name, parent, typeof(Image), typeof(LayoutElement));
        panel.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.50f);
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(2f, -2f);

        var layout = panel.GetComponent<LayoutElement>();
        if (preferredWidth > 0f)
            layout.preferredWidth = preferredWidth;
        layout.flexibleHeight = 1f;

        return panel;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles fontStyle)
    {
        var go = CreateUIObject(name, parent, typeof(TextMeshProUGUI));
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.text = string.Empty;
        text.enableWordWrapping = true;
        if (uiFontAsset != null)
            text.font = uiFontAsset;
        return text;
    }

    private Button CreateButton(string name, Transform parent, out TextMeshProUGUI label)
    {
        var buttonGo = CreateUIObject(name, parent, typeof(Image), typeof(Button));
        buttonGo.GetComponent<Image>().color = accentColor;

        var button = buttonGo.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = accentColor;
        colors.highlightedColor = Color.Lerp(accentColor, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(accentColor, Color.black, 0.10f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        button.colors = colors;

        label = CreateText("Label", buttonGo.transform, 22f, FontStyles.Bold);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        return button;
    }

    private TMP_InputField CreateInputField(string name, Transform parent)
    {
        var root = CreateUIObject(name, parent, typeof(Image), typeof(TMP_InputField));
        root.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.95f);
        var outline = root.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(2f, -2f);

        var viewport = CreateUIObject("Viewport", root.transform, typeof(RectMask2D));
        var viewportRect = viewport.GetComponent<RectTransform>();
        StretchWithPadding(viewportRect, 14f, 8f);

        var text = CreateText("Text", viewport.transform, 24f, FontStyles.Normal);
        text.color = new Color(0.1f, 0.1f, 0.12f, 1f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        StretchFull(text.rectTransform);
        text.margin = new Vector4(4f, 0f, 4f, 0f);

        var placeholder = CreateText("Placeholder", viewport.transform, 24f, FontStyles.Italic);
        placeholder.color = new Color(0.3f, 0.3f, 0.34f, 0.55f);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.text = L("MINIGAME_MATH_ANSWER_PLACEHOLDER", "정답 입력", "Enter answer");
        StretchFull(placeholder.rectTransform);
        placeholder.margin = new Vector4(4f, 0f, 4f, 0f);

        var input = root.GetComponent<TMP_InputField>();
        input.textViewport = viewportRect;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterValidation = TMP_InputField.CharacterValidation.None;
        input.onSubmit.AddListener(_ => SubmitAnswer());
        return input;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void StretchWithPadding(RectTransform rect, float horizontal, float vertical = -1f)
    {
        if (vertical < 0f)
            vertical = horizontal;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontal, vertical);
        rect.offsetMax = new Vector2(-horizontal, -vertical);
    }

    private static void SetPreferredHeight(RectTransform rect, float height)
    {
        var layout = rect.GetComponent<LayoutElement>();
        if (layout == null)
            layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
    }

    private static void SetPreferredSize(RectTransform rect, float width, float height)
    {
        var layout = rect.GetComponent<LayoutElement>();
        if (layout == null)
            layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = height;
    }
}
