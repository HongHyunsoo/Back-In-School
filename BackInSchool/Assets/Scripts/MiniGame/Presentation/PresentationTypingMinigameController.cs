using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PresentationTypingMinigameController : MonoBehaviour
{
    private enum PresentationPhase
    {
        FallingWords,
        Sentence,
        Speech,
        Ended
    }

    private sealed class FallingWordView
    {
        public string text;
        public RectTransform rect;
        public Image background;
        public float spawnTime;
        public bool typed;
        public bool missed;
    }

    private const string PerfectAchievementPrefKey = "ACH_PRESENTATION_D2_PERFECT";

    [Header("Config (Optional)")]
    public PresentationTypingMinigameConfig config;

    [Header("Flow")]
    public string[] supportedFlowIds = new[] { "CLASS2_D2" };

    [Header("Rounds")]
    public int cycleRepeatCount = 5;
    public List<PresentationTypingMinigameConfig.PresentationCycleDefinition> cycles = new List<PresentationTypingMinigameConfig.PresentationCycleDefinition>();

    [Header("Presentation Rules")]
    public float wordFallDuration = 4.5f;
    public float wordSpawnInterval = 1.1f;
    public float sentenceTimeLimit = 7f;
    public int tensionGainOnWordMiss = 10;
    public int tensionGainOnSentenceMiss = 20;
    public int maxTension = 100;
    public float speechBubbleShowSeconds = 1.5f;
    public int penaltyOnFail = 1;

    [Header("UI")]
    public TMP_FontAsset uiFontAsset;
    public Color backgroundColor = new Color(0.97f, 0.97f, 0.95f, 1f);
    public Color frameColor = new Color(0.12f, 0.12f, 0.12f, 1f);
    public Color stageColor = new Color(1f, 1f, 1f, 1f);
    public Color wordColor = new Color(1f, 1f, 1f, 1f);
    public Color wordMissColor = new Color(1f, 0.82f, 0.82f, 1f);
    public Color wordTypedColor = new Color(0.79f, 1f, 0.83f, 1f);
    public Color tensionFillColor = new Color(0.94f, 0.38f, 0.32f, 1f);
    public Color timerFillColor = new Color(0.28f, 0.85f, 0.21f, 1f);

    private Canvas uiCanvas;
    private RectTransform stageRect;
    private RectTransform fallingWordsLayer;
    private TMP_InputField inputField;
    private TextMeshProUGUI feedbackText;
    private TextMeshProUGUI roundText;
    private TextMeshProUGUI tensionText;
    private Image tensionFill;
    private RectTransform sentencePanel;
    private TextMeshProUGUI sentenceText;
    private Image sentenceTimerFill;
    private RectTransform playerSpeechBubble;
    private TextMeshProUGUI playerSpeechText;
    private RectTransform teacherSpeechBubble;
    private TextMeshProUGUI teacherSpeechText;

    private readonly List<FallingWordView> activeWords = new List<FallingWordView>();
    private readonly List<float> currentSpawnAnchorXs = new List<float>();

    private int currentCycleIndex;
    private int nextWordSpawnIndex;
    private int resolvedWordCount;
    private int tension;
    private bool ended;
    private PresentationPhase phase;
    private float phaseElapsed;
    private float speechEndAt = -1f;
    private float failEndAt = -1f;

    private void Awake()
    {
        ApplyConfigIfNeeded();

        if (!ShouldRunForCurrentFlow())
        {
            enabled = false;
            return;
        }

        EnsureCyclesOrFallback();
        EnsureUIFont();
        EnsureEventSystem();
        BuildRuntimeUI();
        StartCycle(0);
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
        if (ended && phase != PresentationPhase.Ended)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            FailPresentation();
            return;
        }

        if ((phase == PresentationPhase.FallingWords || phase == PresentationPhase.Sentence) &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            SubmitCurrentInput();
        }

        phaseElapsed += Time.unscaledDeltaTime;

        switch (phase)
        {
            case PresentationPhase.FallingWords:
                TickFallingWords();
                break;
            case PresentationPhase.Sentence:
                TickSentencePhase();
                break;
            case PresentationPhase.Speech:
                TickSpeechPhase();
                break;
            case PresentationPhase.Ended:
                TickFailEnd();
                break;
        }
    }

    private void ApplyConfigIfNeeded()
    {
        if (config == null)
            return;

        supportedFlowIds = config.supportedFlowIds;
        cycleRepeatCount = config.cycleRepeatCount;
        cycles = new List<PresentationTypingMinigameConfig.PresentationCycleDefinition>(config.cycles);
        wordFallDuration = config.wordFallDuration;
        wordSpawnInterval = config.wordSpawnInterval;
        sentenceTimeLimit = config.sentenceTimeLimit;
        tensionGainOnWordMiss = config.tensionGainOnWordMiss;
        tensionGainOnSentenceMiss = config.tensionGainOnSentenceMiss;
        maxTension = config.maxTension;
        speechBubbleShowSeconds = config.speechBubbleShowSeconds;
        penaltyOnFail = config.penaltyOnFail;

        if (config.uiFontAsset != null)
            uiFontAsset = config.uiFontAsset;
        backgroundColor = config.backgroundColor;
        frameColor = config.frameColor;
        stageColor = config.stageColor;
        wordColor = config.wordColor;
        wordMissColor = config.wordMissColor;
        wordTypedColor = config.wordTypedColor;
        tensionFillColor = config.tensionFillColor;
        timerFillColor = config.timerFillColor;
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

        cycles = new List<PresentationTypingMinigameConfig.PresentationCycleDefinition>
        {
            new PresentationTypingMinigameConfig.PresentationCycleDefinition()
        };
    }

    private void StartCycle(int cycleIndex)
    {
        currentCycleIndex = cycleIndex;
        nextWordSpawnIndex = 0;
        resolvedWordCount = 0;
        phase = PresentationPhase.FallingWords;
        phaseElapsed = 0f;
        speechEndAt = -1f;
        failEndAt = -1f;
        ClearFallingWords();
        BuildRandomSpawnAnchors(GetKeywordCount(GetCurrentCycleDefinition()));
        HideSpeechBubbles();
        HideSentencePanel();
        SetFeedback("\uB2E8\uC5B4\uAC00 \uB0B4\uB824\uC635\uB2C8\uB2E4. \uBE60\uB974\uAC8C \uC785\uB825\uD574\uC8FC\uC138\uC694.");
        RefreshHud();
        ResetInputField();
    }

    private void TickFallingWords()
    {
        var cycle = GetCurrentCycleDefinition();
        if (cycle == null)
            return;

        while (nextWordSpawnIndex < GetKeywordCount(cycle) &&
               phaseElapsed >= nextWordSpawnIndex * wordSpawnInterval)
        {
            SpawnWord(cycle.keywords[nextWordSpawnIndex], nextWordSpawnIndex);
            nextWordSpawnIndex++;
        }

        for (int i = 0; i < activeWords.Count; i++)
        {
            FallingWordView word = activeWords[i];
            if (word == null || word.rect == null || word.typed || word.missed)
                continue;

            float progress = Mathf.Clamp01((Time.unscaledTime - word.spawnTime) / Mathf.Max(0.01f, wordFallDuration));
            SetWordPosition(word.rect, i, progress);
            if (progress >= 1f)
            {
                word.missed = true;
                resolvedWordCount++;
                word.background.color = wordMissColor;
                SetFeedback($"[{word.text}] \uB2E8\uC5B4 \uC2E4\uD328. \uAE34\uC7A5\uAC10 +{tensionGainOnWordMiss}");
                AddTension(tensionGainOnWordMiss);
            }
        }

        if (resolvedWordCount >= GetKeywordCount(cycle))
            BeginSentencePhase();
    }

    private void TickSentencePhase()
    {
        float remaining = Mathf.Clamp01(1f - (phaseElapsed / Mathf.Max(0.01f, sentenceTimeLimit)));
        UpdateSentenceTimerVisual(remaining);

        if (remaining <= 0f)
        {
            SetFeedback($"\uBB38\uC7A5 \uBC1C\uD45C \uC2E4\uD328. \uAE34\uC7A5\uAC10 +{tensionGainOnSentenceMiss}");
            AddTension(tensionGainOnSentenceMiss);
            if (!ended)
                AdvanceAfterSentence(false);
        }
    }

    private void TickSpeechPhase()
    {
        if (Time.unscaledTime < speechEndAt)
            return;

        int nextCycle = currentCycleIndex + 1;
        if (nextCycle >= cycleRepeatCount)
        {
            CompletePresentation();
            return;
        }

        StartCycle(nextCycle);
    }

    private void SubmitCurrentInput(string _ = null)
    {
        if (ended || inputField == null)
            return;

        string typed = NormalizeInput(inputField.text);
        if (string.IsNullOrEmpty(typed))
        {
            ResetInputField();
            return;
        }

        if (phase == PresentationPhase.FallingWords)
            TryTypeFallingWord(typed);
        else if (phase == PresentationPhase.Sentence)
            TryTypeSentence(typed);
    }

    private void TryTypeFallingWord(string typed)
    {
        FallingWordView candidate = null;
        float lowestY = float.MaxValue;

        for (int i = 0; i < activeWords.Count; i++)
        {
            FallingWordView word = activeWords[i];
            if (word == null || word.typed || word.missed)
                continue;

            if (!string.Equals(NormalizeInput(word.text), typed, StringComparison.Ordinal))
                continue;

            float y = word.rect.anchoredPosition.y;
            if (y < lowestY)
            {
                lowestY = y;
                candidate = word;
            }
        }

        if (candidate == null)
        {
            SetFeedback("\uD574\uB2F9 \uB2E8\uC5B4\uAC00 \uC5C6\uC5B4\uC694.");
            ResetInputField();
            return;
        }

        candidate.typed = true;
        candidate.background.color = wordTypedColor;
        resolvedWordCount++;
        SetFeedback($"[{candidate.text}] \uC131\uACF5!");
        ResetInputField();

        if (resolvedWordCount >= GetKeywordCount(GetCurrentCycleDefinition()))
            BeginSentencePhase();
    }

    private void TryTypeSentence(string typed)
    {
        var cycle = GetCurrentCycleDefinition();
        if (cycle == null)
            return;

        if (!string.Equals(NormalizeInput(cycle.completedSentence), typed, StringComparison.Ordinal))
        {
            SetFeedback("\uBB38\uC7A5\uC744 \uADF8\uB300\uB85C \uC785\uB825\uD574\uC8FC\uC138\uC694.");
            ResetInputField();
            return;
        }

        SetFeedback("\uBC1C\uD45C \uC131\uACF5!");
        AdvanceAfterSentence(true);
    }

    private void BeginSentencePhase()
    {
        if (phase != PresentationPhase.FallingWords)
            return;

        phase = PresentationPhase.Sentence;
        phaseElapsed = 0f;
        ShowSentencePanel();
        SetFeedback("\uC81C\uD55C \uC2DC\uAC04 \uC548\uC5D0 \uBB38\uC7A5\uC744 \uC785\uB825\uD574\uC8FC\uC138\uC694.");
        ResetInputField();
    }

    private void AdvanceAfterSentence(bool success)
    {
        if (ended)
            return;

        HideSentencePanel();

        if (!success)
        {
            int nextCycle = currentCycleIndex + 1;
            if (nextCycle >= cycleRepeatCount)
            {
                CompletePresentation();
                return;
            }

            StartCycle(nextCycle);
            return;
        }

        phase = PresentationPhase.Speech;
        phaseElapsed = 0f;
        speechEndAt = Time.unscaledTime + Mathf.Max(0.2f, speechBubbleShowSeconds);
        ShowPlayerSpeech(GetCurrentCycleDefinition().completedSentence);
        ResetInputField(false);
    }

    private void CompletePresentation()
    {
        if (tension <= 0)
            PlayerPrefs.SetInt(PerfectAchievementPrefKey, 1);

        End(true);
    }

    private void FailPresentation()
    {
        ShowTeacherSpeech("\uB4E4\uC5B4\uAC00\uBD10\uB77C.");
        phase = PresentationPhase.Ended;
        failEndAt = Time.unscaledTime + 1.2f;
        ended = true;
    }

    private void TickFailEnd()
    {
        if (failEndAt < 0f || Time.unscaledTime < failEndAt)
            return;

        End(false);
    }

    private void AddTension(int amount)
    {
        tension = Mathf.Clamp(tension + Mathf.Max(0, amount), 0, Mathf.Max(1, maxTension));
        RefreshHud();

        if (tension >= maxTension)
            FailPresentation();
    }

    private void RefreshHud()
    {
        if (roundText != null)
            roundText.text = $"{currentCycleIndex + 1} / {cycleRepeatCount}";

        if (tensionText != null)
            tensionText.text = $"Tension {tension} / {maxTension}";

        if (tensionFill != null)
            tensionFill.fillAmount = maxTension <= 0 ? 0f : Mathf.Clamp01((float)tension / maxTension);
    }

    private void SpawnWord(string wordText, int index)
    {
        var root = CreateUIObject($"Word_{index}", fallingWordsLayer, typeof(Image), typeof(LayoutElement));
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(Mathf.Max(140f, 38f + (wordText.Length * 30f)), 64f);

        var image = root.GetComponent<Image>();
        image.color = wordColor;
        AddOutline(root);

        var label = CreateText("Label", root.transform, 28f, FontStyles.Bold);
        label.alignment = TextAlignmentOptions.Center;
        label.text = wordText;
        label.color = frameColor;
        StretchFull(label.rectTransform);

        activeWords.Add(new FallingWordView
        {
            text = wordText,
            rect = rect,
            background = image,
            spawnTime = Time.unscaledTime
        });

        SetWordPosition(rect, index, 0f);
    }

    private void SetWordPosition(RectTransform rect, int index, float progress)
    {
        if (stageRect == null || rect == null)
            return;

        float width = stageRect.rect.width;
        float height = stageRect.rect.height;
        float anchorX = 0.5f;
        if (index >= 0 && index < currentSpawnAnchorXs.Count)
            anchorX = currentSpawnAnchorXs[index];

        float x = (anchorX * width) - (width * 0.5f);
        float startY = (height * 0.36f) - ((index % 2) * 30f);
        float endY = -(height * 0.24f);
        rect.anchoredPosition = new Vector2(x, Mathf.Lerp(startY, endY, progress));
    }

    private void BuildRandomSpawnAnchors(int count)
    {
        currentSpawnAnchorXs.Clear();
        if (count <= 0)
            return;

        const float minAnchor = 0.14f;
        const float maxAnchor = 0.86f;
        const float minSpacing = 0.12f;

        int guard = 0;
        while (currentSpawnAnchorXs.Count < count && guard < 500)
        {
            guard++;
            float candidate = UnityEngine.Random.Range(minAnchor, maxAnchor);
            bool overlaps = false;

            for (int i = 0; i < currentSpawnAnchorXs.Count; i++)
            {
                if (Mathf.Abs(currentSpawnAnchorXs[i] - candidate) < minSpacing)
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
                currentSpawnAnchorXs.Add(candidate);
        }

        while (currentSpawnAnchorXs.Count < count)
        {
            float t = count == 1 ? 0.5f : (float)currentSpawnAnchorXs.Count / (count - 1);
            currentSpawnAnchorXs.Add(Mathf.Lerp(minAnchor, maxAnchor, t));
        }
    }

    private void ClearFallingWords()
    {
        for (int i = 0; i < activeWords.Count; i++)
        {
            if (activeWords[i] != null && activeWords[i].rect != null)
                Destroy(activeWords[i].rect.gameObject);
        }

        activeWords.Clear();
    }

    private void ShowSentencePanel()
    {
        if (sentencePanel == null)
            return;

        sentencePanel.gameObject.SetActive(true);
        if (sentenceText != null)
            sentenceText.text = GetCurrentCycleDefinition().completedSentence;
        UpdateSentenceTimerVisual(1f);
    }

    private void HideSentencePanel()
    {
        if (sentencePanel != null)
            sentencePanel.gameObject.SetActive(false);
    }

    private void ShowPlayerSpeech(string text)
    {
        HideTeacherSpeech();
        if (playerSpeechBubble == null || playerSpeechText == null)
            return;

        playerSpeechText.text = text;
        playerSpeechBubble.gameObject.SetActive(true);
    }

    private void ShowTeacherSpeech(string text)
    {
        HidePlayerSpeech();
        if (teacherSpeechBubble == null || teacherSpeechText == null)
            return;

        teacherSpeechText.text = text;
        teacherSpeechBubble.gameObject.SetActive(true);
    }

    private void HideSpeechBubbles()
    {
        HidePlayerSpeech();
        HideTeacherSpeech();
    }

    private void HidePlayerSpeech()
    {
        if (playerSpeechBubble != null)
            playerSpeechBubble.gameObject.SetActive(false);
    }

    private void HideTeacherSpeech()
    {
        if (teacherSpeechBubble != null)
            teacherSpeechBubble.gameObject.SetActive(false);
    }

    private void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }

    private void ResetInputField(bool focus = true)
    {
        if (inputField == null)
            return;

        inputField.text = string.Empty;
        if (!focus)
            return;

        inputField.ActivateInputField();
        inputField.Select();
    }

    private PresentationTypingMinigameConfig.PresentationCycleDefinition GetCurrentCycleDefinition()
    {
        if (cycles == null || cycles.Count == 0)
            return null;

        return cycles[currentCycleIndex % cycles.Count];
    }

    private static int GetKeywordCount(PresentationTypingMinigameConfig.PresentationCycleDefinition cycle)
    {
        return cycle != null && cycle.keywords != null ? cycle.keywords.Length : 0;
    }

    private static string NormalizeInput(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private void End(bool success)
    {
        if (phase == PresentationPhase.Ended && failEndAt >= 0f)
            failEndAt = -1f;
        else if (ended)
            return;

        ended = true;
        phase = PresentationPhase.Ended;

        CleanupRuntimeUI();

        if (FlowManager.Instance != null)
        {
            FlowManager.Instance.CompleteCurrentEvent(success ? 0 : penaltyOnFail);
            return;
        }

        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            gm.MinigameFinished(success);
    }

    private void CleanupRuntimeUI()
    {
        ClearFallingWords();

        if (uiCanvas != null)
        {
            Destroy(uiCanvas.gameObject);
            uiCanvas = null;
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
        root.GetComponent<Image>().color = backgroundColor;

        BuildTopHud(root.transform);
        BuildStage(root.transform);
        BuildInputBar(root.transform);
    }

    private void BuildTopHud(Transform parent)
    {
        var hud = CreateUIObject("Hud", parent, typeof(Image));
        var hudRect = hud.GetComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0.08f, 0.88f);
        hudRect.anchorMax = new Vector2(0.92f, 0.98f);
        hudRect.offsetMin = Vector2.zero;
        hudRect.offsetMax = Vector2.zero;
        hud.GetComponent<Image>().color = stageColor;
        AddOutline(hud);

        roundText = CreateText("RoundText", hud.transform, 26f, FontStyles.Bold);
        roundText.alignment = TextAlignmentOptions.MidlineLeft;
        roundText.color = frameColor;
        roundText.rectTransform.anchorMin = new Vector2(0.02f, 0.55f);
        roundText.rectTransform.anchorMax = new Vector2(0.24f, 0.95f);
        roundText.rectTransform.offsetMin = Vector2.zero;
        roundText.rectTransform.offsetMax = Vector2.zero;

        tensionText = CreateText("TensionText", hud.transform, 24f, FontStyles.Bold);
        tensionText.alignment = TextAlignmentOptions.MidlineRight;
        tensionText.color = frameColor;
        tensionText.rectTransform.anchorMin = new Vector2(0.62f, 0.55f);
        tensionText.rectTransform.anchorMax = new Vector2(0.98f, 0.95f);
        tensionText.rectTransform.offsetMin = Vector2.zero;
        tensionText.rectTransform.offsetMax = Vector2.zero;

        var tensionBarBg = CreateUIObject("TensionBarBg", hud.transform, typeof(Image));
        var tensionBarBgRect = tensionBarBg.GetComponent<RectTransform>();
        tensionBarBgRect.anchorMin = new Vector2(0.02f, 0.10f);
        tensionBarBgRect.anchorMax = new Vector2(0.98f, 0.38f);
        tensionBarBgRect.offsetMin = Vector2.zero;
        tensionBarBgRect.offsetMax = Vector2.zero;
        tensionBarBg.GetComponent<Image>().color = new Color(0.9f, 0.9f, 0.9f, 1f);
        AddOutline(tensionBarBg);

        tensionFill = CreateUIObject("TensionFill", tensionBarBg.transform, typeof(Image)).GetComponent<Image>();
        tensionFill.color = tensionFillColor;
        tensionFill.type = Image.Type.Filled;
        tensionFill.fillMethod = Image.FillMethod.Horizontal;
        StretchFull(tensionFill.rectTransform);
        tensionFill.fillAmount = 0f;
    }

    private void BuildStage(Transform parent)
    {
        var stage = CreateUIObject("Stage", parent, typeof(Image));
        stageRect = stage.GetComponent<RectTransform>();
        stageRect.anchorMin = new Vector2(0.08f, 0.20f);
        stageRect.anchorMax = new Vector2(0.92f, 0.84f);
        stageRect.offsetMin = Vector2.zero;
        stageRect.offsetMax = Vector2.zero;
        stage.GetComponent<Image>().color = stageColor;
        AddOutline(stage);

        sentencePanel = CreateUIObject("SentencePanel", stage.transform, typeof(Image)).GetComponent<RectTransform>();
        sentencePanel.anchorMin = new Vector2(0.04f, 0.78f);
        sentencePanel.anchorMax = new Vector2(0.96f, 0.95f);
        sentencePanel.offsetMin = Vector2.zero;
        sentencePanel.offsetMax = Vector2.zero;
        sentencePanel.GetComponent<Image>().color = stageColor;
        AddOutline(sentencePanel.gameObject);

        sentenceText = CreateText("SentenceText", sentencePanel.transform, 30f, FontStyles.Bold);
        sentenceText.rectTransform.anchorMin = new Vector2(0.02f, 0.44f);
        sentenceText.rectTransform.anchorMax = new Vector2(0.98f, 0.94f);
        sentenceText.rectTransform.offsetMin = Vector2.zero;
        sentenceText.rectTransform.offsetMax = Vector2.zero;
        sentenceText.alignment = TextAlignmentOptions.MidlineLeft;
        sentenceText.color = frameColor;

        var timerBg = CreateUIObject("SentenceTimerBg", sentencePanel.transform, typeof(Image));
        var timerBgRect = timerBg.GetComponent<RectTransform>();
        timerBgRect.anchorMin = new Vector2(0.02f, 0.08f);
        timerBgRect.anchorMax = new Vector2(0.98f, 0.28f);
        timerBgRect.offsetMin = Vector2.zero;
        timerBgRect.offsetMax = Vector2.zero;
        timerBg.GetComponent<Image>().color = new Color(0.92f, 0.92f, 0.92f, 1f);
        AddOutline(timerBg);

        sentenceTimerFill = CreateUIObject("SentenceTimerFill", timerBg.transform, typeof(Image)).GetComponent<Image>();
        sentenceTimerFill.color = timerFillColor;
        StretchFull(sentenceTimerFill.rectTransform);
        sentenceTimerFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        UpdateSentenceTimerVisual(1f);

        fallingWordsLayer = CreateUIObject("FallingWordsLayer", stage.transform).GetComponent<RectTransform>();
        StretchFull(fallingWordsLayer);

        BuildCharacter(stage.transform);
        BuildSpeechBubbles(stage.transform);
        HideSentencePanel();
    }

    private void BuildCharacter(Transform parent)
    {
        var characterRoot = CreateUIObject("CharacterRoot", parent).GetComponent<RectTransform>();
        characterRoot.anchorMin = new Vector2(0.40f, 0.18f);
        characterRoot.anchorMax = new Vector2(0.60f, 0.56f);
        characterRoot.offsetMin = Vector2.zero;
        characterRoot.offsetMax = Vector2.zero;

        CreateDoodleLine(characterRoot, new Vector2(0.48f, 0.72f), new Vector2(0.52f, 0.72f), 8f);
        CreateDoodleLine(characterRoot, new Vector2(0.43f, 0.58f), new Vector2(0.57f, 0.58f), 8f);
        CreateDoodleLine(characterRoot, new Vector2(0.50f, 0.58f), new Vector2(0.50f, 0.24f), 8f);
        CreateDoodleLine(characterRoot, new Vector2(0.50f, 0.24f), new Vector2(0.43f, 0.02f), 8f);
        CreateDoodleLine(characterRoot, new Vector2(0.50f, 0.24f), new Vector2(0.57f, 0.02f), 8f);
        CreateDoodleLine(characterRoot, new Vector2(0.48f, 0.72f), new Vector2(0.44f, 0.96f), 8f);
        CreateDoodleLine(characterRoot, new Vector2(0.52f, 0.72f), new Vector2(0.56f, 0.96f), 8f);

        var head = CreateUIObject("Head", characterRoot, typeof(Image));
        var headRect = head.GetComponent<RectTransform>();
        headRect.anchorMin = new Vector2(0.38f, 0.48f);
        headRect.anchorMax = new Vector2(0.62f, 0.74f);
        headRect.offsetMin = Vector2.zero;
        headRect.offsetMax = Vector2.zero;
        head.GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
        AddOutline(head);
    }

    private void BuildSpeechBubbles(Transform parent)
    {
        playerSpeechBubble = CreateSpeechBubble(parent, out playerSpeechText);
        playerSpeechBubble.anchorMin = new Vector2(0.18f, 0.52f);
        playerSpeechBubble.anchorMax = new Vector2(0.82f, 0.78f);
        playerSpeechBubble.offsetMin = Vector2.zero;
        playerSpeechBubble.offsetMax = Vector2.zero;

        teacherSpeechBubble = CreateSpeechBubble(parent, out teacherSpeechText);
        teacherSpeechBubble.anchorMin = new Vector2(0.14f, 0.58f);
        teacherSpeechBubble.anchorMax = new Vector2(0.86f, 0.84f);
        teacherSpeechBubble.offsetMin = Vector2.zero;
        teacherSpeechBubble.offsetMax = Vector2.zero;

        HideSpeechBubbles();
    }

    private RectTransform CreateSpeechBubble(Transform parent, out TextMeshProUGUI label)
    {
        var bubble = CreateUIObject("SpeechBubble", parent, typeof(Image));
        var rect = bubble.GetComponent<RectTransform>();
        bubble.GetComponent<Image>().color = stageColor;
        AddOutline(bubble);

        label = CreateText("SpeechText", bubble.transform, 28f, FontStyles.Bold);
        label.alignment = TextAlignmentOptions.Center;
        label.color = frameColor;
        label.enableWordWrapping = true;
        StretchWithPadding(label.rectTransform, 26f, 18f);
        bubble.SetActive(false);
        return rect;
    }

    private void BuildInputBar(Transform parent)
    {
        var inputBar = CreateUIObject("InputBar", parent, typeof(Image));
        var inputBarRect = inputBar.GetComponent<RectTransform>();
        inputBarRect.anchorMin = new Vector2(0.08f, 0.06f);
        inputBarRect.anchorMax = new Vector2(0.92f, 0.16f);
        inputBarRect.offsetMin = Vector2.zero;
        inputBarRect.offsetMax = Vector2.zero;
        inputBar.GetComponent<Image>().color = stageColor;
        AddOutline(inputBar);

        inputField = CreateInputField("TypeInput", inputBar.transform);
        StretchWithPadding(inputField.GetComponent<RectTransform>(), 18f, 16f);
        inputField.onSubmit.AddListener(SubmitCurrentInput);

        feedbackText = CreateText("FeedbackText", parent, 22f, FontStyles.Bold);
        feedbackText.rectTransform.anchorMin = new Vector2(0.08f, 0.16f);
        feedbackText.rectTransform.anchorMax = new Vector2(0.92f, 0.20f);
        feedbackText.rectTransform.offsetMin = Vector2.zero;
        feedbackText.rectTransform.offsetMax = Vector2.zero;
        feedbackText.alignment = TextAlignmentOptions.Center;
        feedbackText.color = frameColor;
    }

    private void CreateDoodleLine(RectTransform parent, Vector2 startAnchor, Vector2 endAnchor, float thickness)
    {
        var go = CreateUIObject("Line", parent, typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        go.GetComponent<Image>().color = frameColor;

        Vector2 size = parent.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            size = new Vector2(240f, 320f);

        Vector2 start = new Vector2((startAnchor.x - 0.5f) * size.x, (startAnchor.y - 0.5f) * size.y);
        Vector2 end = new Vector2((endAnchor.x - 0.5f) * size.x, (endAnchor.y - 0.5f) * size.y);
        Vector2 delta = end - start;

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(delta.magnitude, thickness);
        rect.anchoredPosition = (start + end) * 0.5f;
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    private void EnsureUIFont()
    {
        if (uiFontAsset != null)
            return;

        #if UNITY_EDITOR
        uiFontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Fonts/Galmuri11-Bold SDF.asset");
        #endif

        if (uiFontAsset != null)
            return;

        TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < loadedFonts.Length; i++)
        {
            TMP_FontAsset candidate = loadedFonts[i];
            if (candidate == null)
                continue;

            string name = candidate.name;
            if (name.Equals("Galmuri11-Bold SDF", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("Galmuri11-Bold", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.Equals("DungGeunMo SDF", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("DungGeunMo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                uiFontAsset = candidate;
                return;
            }
        }
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystemGo);
    }

    private GameObject CreateUIObject(string name, Transform parent, params Type[] extraTypes)
    {
        var components = new List<Type> { typeof(RectTransform) };
        if (extraTypes != null)
        {
            for (int i = 0; i < extraTypes.Length; i++)
            {
                if (extraTypes[i] == null || extraTypes[i] == typeof(RectTransform))
                    continue;
                components.Add(extraTypes[i]);
            }
        }

        var go = new GameObject(name, components.ToArray());
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
        root.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

        var viewport = CreateUIObject("Viewport", root.transform, typeof(RectMask2D));
        StretchFull(viewport.GetComponent<RectTransform>());

        var text = CreateText("Text", viewport.transform, 30f, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = frameColor;
        StretchFull(text.rectTransform);

        var placeholder = CreateText("Placeholder", viewport.transform, 30f, FontStyles.Bold);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.color = new Color(frameColor.r, frameColor.g, frameColor.b, 0.35f);
        placeholder.text = "TYPE";
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

    private void AddOutline(GameObject target)
    {
        var outline = target.AddComponent<Outline>();
        outline.effectColor = frameColor;
        outline.effectDistance = new Vector2(3f, -3f);
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

    private void UpdateSentenceTimerVisual(float normalized)
    {
        if (sentenceTimerFill == null)
            return;

        float safe = Mathf.Clamp01(normalized);
        sentenceTimerFill.rectTransform.localScale = new Vector3(safe, 1f, 1f);
    }
}
