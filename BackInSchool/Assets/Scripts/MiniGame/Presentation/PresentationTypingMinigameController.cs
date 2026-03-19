using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PresentationTypingMinigameController : MonoBehaviour
{
    [Header("Config (Optional)")]
    public PresentationTypingMinigameConfig config;

    [Header("Flow")]
    public string[] supportedFlowIds = new[] { "CLASS2_D2" };
    public int penaltyOnGiveUp = 1;
    public float successDelaySeconds = 0.6f;

    [Header("Rounds")]
    public int cycleRepeatCount = 5;
    public List<PresentationTypingMinigameConfig.PresentationCycleDefinition> cycles = new List<PresentationTypingMinigameConfig.PresentationCycleDefinition>();

    [Header("UI")]
    public TMP_FontAsset uiFontAsset;
    public Color dimColor = new Color(0.06f, 0.08f, 0.12f, 0.88f);
    public Color panelColor = new Color(0.97f, 0.95f, 0.90f, 0.98f);
    public Color accentColor = new Color(0.18f, 0.28f, 0.48f, 1f);
    public Color chipColor = new Color(0.89f, 0.91f, 0.96f, 1f);
    public Color completedChipColor = new Color(0.58f, 0.82f, 0.66f, 1f);
    public Color outlineColor = new Color(0.14f, 0.12f, 0.10f, 0.30f);
    public Color successColor = new Color(0.19f, 0.55f, 0.28f, 1f);
    public Color errorColor = new Color(0.78f, 0.22f, 0.18f, 1f);

    private Canvas uiCanvas;
    private TextMeshProUGUI headerText;
    private TextMeshProUGUI cycleText;
    private TextMeshProUGUI promptText;
    private TextMeshProUGUI sentencePreviewText;
    private TextMeshProUGUI feedbackText;
    private TMP_InputField answerInput;
    private Transform keywordChipRoot;
    private readonly List<Image> keywordChipImages = new List<Image>();
    private readonly List<TextMeshProUGUI> keywordChipLabels = new List<TextMeshProUGUI>();

    private int currentCycleIndex;
    private int currentKeywordIndex;
    private bool waitingForSentence;
    private bool ended;
    private Coroutine advanceRoutine;
    private Coroutine focusRoutine;

    private void Awake()
    {
        ApplyConfigIfNeeded();

        if (!ShouldRunForCurrentFlow())
        {
            enabled = false;
            return;
        }

        EnsureCyclesOrFallback();
        EnsureEventSystem();
        BuildRuntimeUI();
        LoadCycle(0);
    }

    private void OnDisable()
    {
        CleanupRuntimeUI();
    }

    private void OnDestroy()
    {
        CleanupRuntimeUI();
    }

    private void Update()
    {
        if (ended)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            End(false);
    }

    private void ApplyConfigIfNeeded()
    {
        if (config == null)
            return;

        supportedFlowIds = config.supportedFlowIds;
        penaltyOnGiveUp = config.penaltyOnGiveUp;
        successDelaySeconds = config.successDelaySeconds;
        cycleRepeatCount = config.cycleRepeatCount;
        cycles = new List<PresentationTypingMinigameConfig.PresentationCycleDefinition>(config.cycles);

        if (config.uiFontAsset != null)
            uiFontAsset = config.uiFontAsset;
        dimColor = config.dimColor;
        panelColor = config.panelColor;
        accentColor = config.accentColor;
        chipColor = config.chipColor;
        completedChipColor = config.completedChipColor;
        outlineColor = config.outlineColor;
        successColor = config.successColor;
        errorColor = config.errorColor;
    }

    private bool ShouldRunForCurrentFlow()
    {
        string flowId = FlowContext.CurrentId;
        if (string.IsNullOrEmpty(flowId))
            flowId = PlayerPrefs.GetString("FLOW_ID", string.Empty);

        if (string.IsNullOrEmpty(flowId))
            return false;

        if (supportedFlowIds == null || supportedFlowIds.Length == 0)
            return string.Equals(flowId, "CLASS2_D2", StringComparison.OrdinalIgnoreCase);

        for (int i = 0; i < supportedFlowIds.Length; i++)
        {
            if (string.Equals(flowId, supportedFlowIds[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void EnsureCyclesOrFallback()
    {
        if (cycleRepeatCount <= 0)
            cycleRepeatCount = 5;

        if (cycles != null && cycles.Count > 0)
            return;

        cycles = new List<PresentationTypingMinigameConfig.PresentationCycleDefinition>();
        for (int i = 0; i < cycleRepeatCount; i++)
        {
            cycles.Add(new PresentationTypingMinigameConfig.PresentationCycleDefinition
            {
                title = $"Round {i + 1}",
                keywords = new[]
                {
                    "\uC800\uB294",
                    "\uB9CC\uC57D",
                    "\uC788\uB2E4\uBA74",
                    "\uB3C8",
                    "1\uC5B5"
                },
                completedSentence = "\uC800\uB294 \uB9CC\uC57D \uB3C8 1\uC5B5\uC774 \uC788\uB2E4\uBA74"
            });
        }
    }

    private void BuildRuntimeUI()
    {
        var canvasGo = new GameObject("__PresentationTypingUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        uiCanvas = canvasGo.GetComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.overrideSorting = true;
        uiCanvas.sortingOrder = -10;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var root = CreateUIObject("Root", canvasGo.transform, typeof(Image));
        var rootRect = root.GetComponent<RectTransform>();
        StretchFull(rootRect);
        root.GetComponent<Image>().color = dimColor;

        var panel = CreateUIObject("Panel", root.transform, typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.18f, 0.1f);
        panelRect.anchorMax = new Vector2(0.82f, 0.9f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = panelColor;
        AddOutline(panel);

        var panelLayout = panel.GetComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(42, 42, 36, 36);
        panelLayout.spacing = 18f;
        panelLayout.childControlHeight = false;
        panelLayout.childControlWidth = true;
        panelLayout.childForceExpandHeight = false;
        panelLayout.childForceExpandWidth = true;

        headerText = CreateText("Header", panel.transform, 44f, FontStyles.Bold);
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.color = accentColor;
        headerText.text = "Presentation Typing";
        SetPreferredHeight(headerText.rectTransform, 64f);

        cycleText = CreateText("CycleText", panel.transform, 28f, FontStyles.Bold);
        cycleText.alignment = TextAlignmentOptions.Center;
        cycleText.color = accentColor;
        SetPreferredHeight(cycleText.rectTransform, 42f);

        promptText = CreateText("PromptText", panel.transform, 28f, FontStyles.Normal);
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = new Color(0.1f, 0.1f, 0.12f, 1f);
        promptText.enableWordWrapping = true;
        SetPreferredHeight(promptText.rectTransform, 64f);

        var chipPanel = CreateUIObject("KeywordPanel", panel.transform, typeof(Image), typeof(VerticalLayoutGroup));
        chipPanel.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.42f);
        AddOutline(chipPanel);
        SetPreferredHeight(chipPanel.GetComponent<RectTransform>(), 200f);

        var chipPanelLayout = chipPanel.GetComponent<VerticalLayoutGroup>();
        chipPanelLayout.padding = new RectOffset(18, 18, 18, 18);
        chipPanelLayout.spacing = 12f;
        chipPanelLayout.childControlHeight = false;
        chipPanelLayout.childControlWidth = true;
        chipPanelLayout.childForceExpandHeight = false;
        chipPanelLayout.childForceExpandWidth = true;

        var chipTitle = CreateText("KeywordTitle", chipPanel.transform, 24f, FontStyles.Bold);
        chipTitle.text = "Keywords";
        chipTitle.color = accentColor;
        chipTitle.alignment = TextAlignmentOptions.MidlineLeft;
        SetPreferredHeight(chipTitle.rectTransform, 34f);

        var chipsWrap = CreateUIObject("KeywordChips", chipPanel.transform, typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        keywordChipRoot = chipsWrap.transform;
        var chipsLayout = chipsWrap.GetComponent<HorizontalLayoutGroup>();
        chipsLayout.spacing = 10f;
        chipsLayout.childAlignment = TextAnchor.MiddleCenter;
        chipsLayout.childControlWidth = false;
        chipsLayout.childControlHeight = false;
        chipsLayout.childForceExpandWidth = false;
        chipsLayout.childForceExpandHeight = false;
        var chipsFitter = chipsWrap.GetComponent<ContentSizeFitter>();
        chipsFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        chipsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sentencePreviewText = CreateText("SentencePreview", panel.transform, 32f, FontStyles.Bold);
        sentencePreviewText.alignment = TextAlignmentOptions.Center;
        sentencePreviewText.enableWordWrapping = true;
        sentencePreviewText.color = new Color(0.12f, 0.12f, 0.14f, 1f);
        SetPreferredHeight(sentencePreviewText.rectTransform, 110f);

        answerInput = CreateInputField("AnswerInput", panel.transform);
        SetPreferredHeight(answerInput.GetComponent<RectTransform>(), 74f);
        answerInput.onSubmit.AddListener(_ => SubmitCurrentInput());

        var buttonRow = CreateUIObject("Buttons", panel.transform, typeof(HorizontalLayoutGroup));
        var buttonLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 16f;
        buttonLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonLayout.childControlHeight = false;
        buttonLayout.childControlWidth = false;
        buttonLayout.childForceExpandWidth = false;
        buttonLayout.childForceExpandHeight = false;
        SetPreferredHeight(buttonRow.GetComponent<RectTransform>(), 72f);

        var submitButton = CreateButton("SubmitButton", buttonRow.transform, out var submitLabel);
        submitLabel.text = "Submit";
        submitButton.onClick.AddListener(SubmitCurrentInput);
        SetPreferredSize(submitButton.GetComponent<RectTransform>(), 180f, 66f);

        var giveUpButton = CreateButton("GiveUpButton", buttonRow.transform, out var giveUpLabel);
        giveUpLabel.text = "Give Up";
        giveUpButton.onClick.AddListener(() => End(false));
        giveUpButton.GetComponent<Image>().color = errorColor;
        var colors = giveUpButton.colors;
        colors.normalColor = errorColor;
        colors.highlightedColor = Color.Lerp(errorColor, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(errorColor, Color.black, 0.10f);
        giveUpButton.colors = colors;
        SetPreferredSize(giveUpButton.GetComponent<RectTransform>(), 180f, 66f);

        feedbackText = CreateText("Feedback", panel.transform, 24f, FontStyles.Bold);
        feedbackText.alignment = TextAlignmentOptions.Center;
        feedbackText.color = accentColor;
        feedbackText.text = string.Empty;
        SetPreferredHeight(feedbackText.rectTransform, 42f);

        FocusInputNextFrame();
    }

    private void LoadCycle(int cycleIndex)
    {
        if (cycleIndex < 0 || cycleIndex >= cycleRepeatCount || cycles == null || cycles.Count == 0)
            return;

        currentCycleIndex = cycleIndex;
        currentKeywordIndex = 0;
        waitingForSentence = false;

        RefreshCycleUI();
        SetFeedback(string.Empty, accentColor);
        answerInput.text = string.Empty;
        FocusInputNextFrame();
    }

    private void SubmitCurrentInput()
    {
        if (ended || answerInput == null)
            return;

        string typed = NormalizeInput(answerInput.text);
        if (string.IsNullOrEmpty(typed))
        {
            SetFeedback("Type the target text first.", errorColor);
            FocusInputNextFrame();
            return;
        }

        var cycle = GetCurrentCycleDefinition();

        if (!waitingForSentence)
        {
            string expectedKeyword = NormalizeInput(GetExpectedKeyword(cycle, currentKeywordIndex));
            if (typed == expectedKeyword)
            {
                currentKeywordIndex++;
                answerInput.text = string.Empty;

                if (currentKeywordIndex >= GetKeywordCount(cycle))
                {
                    waitingForSentence = true;
                    SetFeedback("Good. Now type the full sentence.", successColor);
                }
                else
                {
                    SetFeedback("Correct. Move to the next keyword.", successColor);
                }

                RefreshCycleUI();
                FocusInputNextFrame();
                return;
            }

            SetFeedback($"Try again: [{GetExpectedKeyword(cycle, currentKeywordIndex)}]", errorColor);
            answerInput.text = string.Empty;
            FocusInputNextFrame();
            return;
        }

        string expectedSentence = NormalizeInput(cycle.completedSentence);
        if (typed == expectedSentence)
        {
            answerInput.text = string.Empty;
            SetFeedback("Sentence complete!", successColor);
            RefreshCycleUI();

            if (advanceRoutine != null)
                StopCoroutine(advanceRoutine);
            advanceRoutine = StartCoroutine(CoAdvanceAfterSuccess());
            return;
        }

        SetFeedback("Type the full sentence again.", errorColor);
        answerInput.text = string.Empty;
        FocusInputNextFrame();
    }

    private IEnumerator CoAdvanceAfterSuccess()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, successDelaySeconds));

        if (ended)
            yield break;

        int nextCycle = currentCycleIndex + 1;
        if (nextCycle >= cycleRepeatCount)
        {
            End(true);
            yield break;
        }

        LoadCycle(nextCycle);
    }

    private void RefreshCycleUI()
    {
        if (cycles == null || cycles.Count == 0)
            return;

        var cycle = GetCurrentCycleDefinition();

        if (cycleText != null)
            cycleText.text = $"라운드 {currentCycleIndex + 1} / {Mathf.Min(cycleRepeatCount, cycles.Count)}";

        if (promptText != null)
        {
            promptText.text = waitingForSentence
                ? "Type the completed sentence exactly."
                : $"Type keyword {currentKeywordIndex + 1} / {GetKeywordCount(cycle)}";
        }

        if (sentencePreviewText != null)
        {
            sentencePreviewText.text = waitingForSentence
                ? $"[{cycle.completedSentence}]"
                : "Finish all keywords to unlock the full sentence.";
        }

        RebuildKeywordChips(cycle);
    }

    private PresentationTypingMinigameConfig.PresentationCycleDefinition GetCurrentCycleDefinition()
    {
        if (cycles == null || cycles.Count == 0)
            return null;

        int index = Mathf.Abs(currentCycleIndex) % cycles.Count;
        return cycles[index];
    }

    private void RebuildKeywordChips(PresentationTypingMinigameConfig.PresentationCycleDefinition cycle)
    {
        for (int i = keywordChipRoot.childCount - 1; i >= 0; i--)
            Destroy(keywordChipRoot.GetChild(i).gameObject);

        keywordChipImages.Clear();
        keywordChipLabels.Clear();

        int keywordCount = GetKeywordCount(cycle);
        for (int i = 0; i < keywordCount; i++)
        {
            var chip = CreateUIObject($"Keyword_{i}", keywordChipRoot, typeof(Image), typeof(LayoutElement));
            chip.GetComponent<Image>().color = i < currentKeywordIndex ? completedChipColor : chipColor;
            AddOutline(chip);

            var layout = chip.GetComponent<LayoutElement>();
            layout.preferredHeight = 50f;
            layout.preferredWidth = Mathf.Max(120f, 34f + (GetExpectedKeyword(cycle, i).Length * 24f));

            var label = CreateText("Label", chip.transform, 24f, FontStyles.Bold);
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.12f, 0.12f, 0.14f, 1f);
            label.text = $"[{GetExpectedKeyword(cycle, i)}]";
            StretchFull(label.rectTransform);

            keywordChipImages.Add(chip.GetComponent<Image>());
            keywordChipLabels.Add(label);
        }
    }

    private void SetFeedback(string message, Color color)
    {
        if (feedbackText == null)
            return;

        feedbackText.text = message;
        feedbackText.color = color;
    }

    private static string NormalizeInput(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace("  ", " ");
    }

    private static int GetKeywordCount(PresentationTypingMinigameConfig.PresentationCycleDefinition cycle)
    {
        return cycle != null && cycle.keywords != null ? cycle.keywords.Length : 0;
    }

    private static string GetExpectedKeyword(PresentationTypingMinigameConfig.PresentationCycleDefinition cycle, int index)
    {
        if (cycle == null || cycle.keywords == null || index < 0 || index >= cycle.keywords.Length)
            return string.Empty;

        return cycle.keywords[index] ?? string.Empty;
    }

    private void End(bool success)
    {
        if (ended)
            return;

        ended = true;

        if (advanceRoutine != null)
            StopCoroutine(advanceRoutine);
        if (focusRoutine != null)
            StopCoroutine(focusRoutine);

        CleanupRuntimeUI();

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

    private void CleanupRuntimeUI()
    {
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
    }

    private void FocusInputNextFrame()
    {
        if (focusRoutine != null)
            StopCoroutine(focusRoutine);
        focusRoutine = StartCoroutine(CoFocusInputNextFrame());
    }

    private IEnumerator CoFocusInputNextFrame()
    {
        yield return null;

        if (answerInput == null)
            yield break;

        answerInput.ActivateInputField();
        answerInput.Select();
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystemGo);
    }

    private GameObject CreateUIObject(string name, Transform parent, params Type[] components)
    {
        var go = new GameObject(name, components);
        go.transform.SetParent(parent, false);
        return go;
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

    private TMP_InputField CreateInputField(string name, Transform parent)
    {
        var root = CreateUIObject(name, parent, typeof(Image), typeof(TMP_InputField));
        root.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.96f);
        AddOutline(root);

        var viewport = CreateUIObject("Viewport", root.transform, typeof(RectMask2D));
        StretchWithPadding(viewport.GetComponent<RectTransform>(), 16f, 10f);

        var text = CreateText("Text", viewport.transform, 28f, FontStyles.Normal);
        text.color = new Color(0.10f, 0.10f, 0.12f, 1f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        StretchFull(text.rectTransform);

        var placeholder = CreateText("Placeholder", viewport.transform, 28f, FontStyles.Italic);
        placeholder.color = new Color(0.28f, 0.28f, 0.34f, 0.52f);
        placeholder.text = "Type here";
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        StretchFull(placeholder.rectTransform);

        var input = root.GetComponent<TMP_InputField>();
        input.textViewport = viewport.GetComponent<RectTransform>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterValidation = TMP_InputField.CharacterValidation.None;
        input.resetOnDeActivation = false;
        return input;
    }

    private Button CreateButton(string name, Transform parent, out TextMeshProUGUI label)
    {
        var buttonGo = CreateUIObject(name, parent, typeof(Image), typeof(Button));
        buttonGo.GetComponent<Image>().color = accentColor;
        AddOutline(buttonGo);

        var button = buttonGo.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = accentColor;
        colors.highlightedColor = Color.Lerp(accentColor, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(accentColor, Color.black, 0.10f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        label = CreateText("Label", buttonGo.transform, 24f, FontStyles.Bold);
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        StretchFull(label.rectTransform);
        return button;
    }

    private void AddOutline(GameObject target)
    {
        var outline = target.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void StretchWithPadding(RectTransform rect, float horizontal, float vertical)
    {
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
