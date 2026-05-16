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
    public class EnglishMatchingPairDefinition
    {
        public string word;
        public string meaning;
    }

    [Serializable]
    public class EnglishOrderingQuestionDefinition
    {
        [TextArea(2, 5)]
        public string prompt = "다음 단어를 올바른 순서로 배열하시오.";
        public string[] shuffledWords = Array.Empty<string>();
        public string[] correctOrder = Array.Empty<string>();
        [TextArea(2, 4)]
        public string answerSentence = string.Empty;
    }

    [Serializable]
    public class EnglishTrueFalseQuestionDefinition
    {
        [TextArea(2, 5)]
        public string prompt = "다음 문장이 맞으면 True, 틀리면 False를 고르시오.";
        [TextArea(2, 4)]
        public string statement = string.Empty;
        public bool correctAnswer = true;
        [TextArea(2, 4)]
        public string explanation = string.Empty;
    }

    [Serializable]
    public class EnglishListeningBlankQuestionDefinition
    {
        [TextArea(2, 5)]
        public string prompt = "음성을 듣고 빈칸에 들어갈 알맞은 단어를 고르시오.";
        [TextArea(2, 4)]
        public string sentenceWithBlank = string.Empty;
        public AudioClip voiceClip;
        public string[] choices = Array.Empty<string>();
        public int correctChoiceIndex;
        [TextArea(2, 4)]
        public string completedSentence = string.Empty;
    }

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
    public string[] supportedFlowIds = new[] { "CLASS1_D2", "AFTERSCHOOL_ENGLISH_D1" };
    [Tooltip("Penalty applied when the player gives up.")]
    public int penaltyOnGiveUp = 1;
    [Tooltip("Delay before advancing after a correct answer.")]
    public float correctAnswerDelaySeconds = 0.55f;

    [Header("Questions")]
    [Tooltip("Three math questions are expected, but the controller supports any count >= 1.")]
    public List<MathQuestionDefinition> questions = new List<MathQuestionDefinition>();

    [Header("AfterSchool English")]
    public string afterSchoolEnglishFlowId = "AFTERSCHOOL_ENGLISH_D1";
    public string englishMatchingTitle = "알맞은 짝을 찾아요";
    [TextArea(2, 5)]
    public string englishMatchingDescription = "영단어와 알맞은 뜻을 찾아 선으로 이어 보세요.";
    public List<EnglishMatchingPairDefinition> englishMatchingPairs = new List<EnglishMatchingPairDefinition>();
    public float englishMatchSuccessDelaySeconds = 0.45f;
    public string englishOrderingTitle = "다음 단어를 올바른 순서로 배열하시오.";
    public EnglishOrderingQuestionDefinition englishOrderingQuestion = new EnglishOrderingQuestionDefinition();
    public string englishTrueFalseTitle = "True or False";
    public EnglishTrueFalseQuestionDefinition englishTrueFalseQuestion = new EnglishTrueFalseQuestionDefinition();
    public List<EnglishTrueFalseQuestionDefinition> englishTrueFalseQuestions = new List<EnglishTrueFalseQuestionDefinition>();
    public GameObject englishWordPrefab;
    public string englishListeningTitle = "듣고 알맞은 단어를 고르시오.";
    public EnglishListeningBlankQuestionDefinition englishListeningQuestion = new EnglishListeningBlankQuestionDefinition();
    public List<EnglishListeningBlankQuestionDefinition> englishListeningQuestions = new List<EnglishListeningBlankQuestionDefinition>();

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
    private bool isAfterSchoolEnglishMode;
    private string currentFlowId;

    private RectTransform englishLineLayer;
    private readonly List<Button> englishLeftButtons = new List<Button>();
    private readonly List<Button> englishRightButtons = new List<Button>();
    private readonly List<TextMeshProUGUI> englishLeftLabels = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> englishRightLabels = new List<TextMeshProUGUI>();
    private readonly List<int> englishRightPairIndices = new List<int>();
    private readonly HashSet<int> englishMatchedPairs = new HashSet<int>();
    private readonly List<GameObject> englishLineObjects = new List<GameObject>();
    private readonly List<RectTransform> englishLeftButtonRects = new List<RectTransform>();
    private readonly List<RectTransform> englishRightButtonRects = new List<RectTransform>();
    private int selectedLeftPairIndex = -1;
    private int selectedRightDisplayIndex = -1;
    private int englishDraggingLeftPairIndex = -1;
    private bool englishMatchDropResolvedThisDrag;
    private Image englishPreviewLineImage;
    private RectTransform englishPreviewLineRect;
    private int currentEnglishStage;
    private readonly List<int> englishOrderingCurrentOrder = new List<int>();
    private TextMeshProUGUI englishOrderingAnswerText;
    private Button englishOrderingResetButton;
    private Button englishOrderingSubmitButton;
    private RectTransform englishOrderingTilesRoot;
    private readonly List<EnglishOrderingCardDragHandle> englishOrderingCardHandles = new List<EnglishOrderingCardDragHandle>();
    private int currentEnglishTrueFalseQuestionIndex;
    private int currentEnglishListeningQuestionIndex;
    private int englishListeningSelectedChoiceIndex = -1;
    private AudioSource englishAudioSource;
    private bool englishUseSceneHierarchy;
    private Transform englishMinigameRoot;
    private RectTransform englishCanvasRoot;
    private RectTransform englishQuiz01Root;
    private RectTransform englishQuiz02Root;
    private RectTransform englishQuiz03Root;
    private RectTransform englishQuiz04Root;
    private TextMeshProUGUI englishQuiz01PromptText;
    private TextMeshProUGUI englishQuiz02PromptText;
    private TextMeshProUGUI englishQuiz03PromptText;
    private TextMeshProUGUI englishQuiz03QuestionText;
    private TextMeshProUGUI englishQuiz04PromptText;
    private TextMeshProUGUI englishQuiz04QuestionText;
    private RectTransform englishQuiz01WordsRoot;
    private RectTransform englishQuiz01MeaningRoot;
    private RectTransform englishQuiz02WordsRoot;
    private RectTransform englishQuiz04ChoicesRoot;
    private Button englishQuiz03TrueButton;
    private Button englishQuiz03FalseButton;
    private Button englishQuiz04SoundButton;
    private Button englishQuiz04SceneSubmitButton;
    private Button englishQuiz02SubmitButton;
    private Button englishQuiz02ResetButton;
    private Button englishQuiz04SubmitButton;
    private Button englishQuiz02SceneSubmitButton;
    private Button englishQuiz02SceneAgainButton;
    private readonly List<Button> englishSceneChoiceButtons = new List<Button>();
    private GameObject englishSceneHintDrawer;
    private GameObject englishSceneFeedbackObject;
    private EnglishOrderingCardDragHandle activeEnglishOrderingDragHandle;

    private Camera uiCamera;

    private sealed class EnglishMatchingDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
    {
        public MathMinigameController controller;
        public int pairIndex;

        public void OnPointerDown(PointerEventData eventData)
        {
            controller?.PrepareEnglishMatchDrag(pairIndex);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            controller?.BeginEnglishMatchDrag(pairIndex, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            controller?.UpdateEnglishMatchDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            controller?.EndEnglishMatchDrag();
        }
    }

    private sealed class EnglishMatchingDropTarget : MonoBehaviour, IDropHandler
    {
        public MathMinigameController controller;
        public int displayIndex;

        public void OnDrop(PointerEventData eventData)
        {
            controller?.ResolveEnglishDraggedMatch(displayIndex);
        }
    }

    private sealed class EnglishOrderingCardDragHandle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public MathMinigameController controller;
        public int displayIndex;
        public Vector2 dragPointerOffset;
        public Vector2 dragStartAnchoredPosition;
        public Vector2 dragStartPointerLocalPosition;
        public string dragWordText;
        public Transform originalParent;
        public int originalSiblingIndex;

        private RectTransform rectTransform;
        private LayoutElement layoutElement;
        private CanvasGroup canvasGroup;

        public RectTransform RectTransform
        {
            get
            {
                if (rectTransform == null)
                    rectTransform = GetComponent<RectTransform>();
                return rectTransform;
            }
        }

        public LayoutElement LayoutElement
        {
            get
            {
                if (layoutElement == null)
                    layoutElement = GetComponent<LayoutElement>();
                return layoutElement;
            }
        }

        public CanvasGroup CanvasGroup
        {
            get
            {
                if (canvasGroup == null)
                    canvasGroup = GetComponent<CanvasGroup>();
                return canvasGroup;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            controller?.BeginEnglishOrderingPointerDrag(this, eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            controller?.EndEnglishOrderingPointerDrag(this, eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            controller?.BeginEnglishOrderingPointerDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            controller?.UpdateEnglishOrderingDrag(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            controller?.EndEnglishOrderingPointerDrag(this, eventData);
        }
    }

    private sealed class HoverHintDrawer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform drawerRect;
        public float closedX = 260f;
        public float openX = 0f;
        public float slideSpeed = 12f;

        private bool isHovered;

        private void OnEnable()
        {
            if (drawerRect == null)
                return;

            Vector2 pos = drawerRect.anchoredPosition;
            pos.x = closedX;
            drawerRect.anchoredPosition = pos;
            isHovered = false;
        }

        private void Update()
        {
            if (drawerRect == null)
                return;

            float target = isHovered ? openX : closedX;
            Vector2 pos = drawerRect.anchoredPosition;
            pos.x = Mathf.Lerp(pos.x, target, Time.unscaledDeltaTime * slideSpeed);
            if (Mathf.Abs(pos.x - target) < 0.25f)
                pos.x = target;
            drawerRect.anchoredPosition = pos;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
        }
    }

    private void Awake()
    {
        currentFlowId = ResolveCurrentFlowId();
        isAfterSchoolEnglishMode = string.Equals(currentFlowId, afterSchoolEnglishFlowId, StringComparison.OrdinalIgnoreCase);

        if (!ShouldRunForCurrentFlow())
        {
            enabled = false;
            return;
        }

        ApplyConfigIfNeeded();
        EnsureEventSystem();

        EnsureUIFont();

        if (isAfterSchoolEnglishMode)
        {
            EnsureEnglishMatchingPairsOrFallback();
            EnsureEnglishOrderingQuestionOrFallback();
            EnsureEnglishTrueFalseQuestionOrFallback();
            EnsureEnglishListeningQuestionOrFallback();
            NormalizeEnglishMinigameStrings();
            currentEnglishStage = 0;
            englishUseSceneHierarchy = TryBindEnglishSceneHierarchy();
            BuildCurrentEnglishStageUI();

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;

            return;
        }

        EnsureQuestionsOrFallback();

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

        if (isAfterSchoolEnglishMode)
        {
            if (currentEnglishStage == 1 && activeEnglishOrderingDragHandle == null && Input.GetMouseButtonDown(0))
                TryBeginEnglishOrderingDragFromMouse();

            if (activeEnglishOrderingDragHandle != null)
            {
                var pointerData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };

                if (Input.GetMouseButton(0))
                {
                    UpdateEnglishOrderingDrag(activeEnglishOrderingDragHandle, pointerData);
                }
                else
                {
                    EndEnglishOrderingPointerDrag(activeEnglishOrderingDragHandle, pointerData);
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape))
                End(false);
            return;
        }

        HandleDrawingInput();
        HandleAnswerTypingFallback();

        if (!advancing && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            SubmitAnswer();

        if (Input.GetKeyDown(KeyCode.Escape))
            End(false);
    }

    private bool ShouldRunForCurrentFlow()
    {
        if (string.IsNullOrEmpty(currentFlowId))
            return false;

        if (string.Equals(currentFlowId, "AFTERSCHOOL_ENGLISH_D1", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(afterSchoolEnglishFlowId) &&
            string.Equals(currentFlowId, afterSchoolEnglishFlowId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (supportedFlowIds == null || supportedFlowIds.Length == 0)
            return string.Equals(currentFlowId, "CLASS1_D2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(currentFlowId, "AFTERSCHOOL_ENGLISH_D1", StringComparison.OrdinalIgnoreCase);

        for (int i = 0; i < supportedFlowIds.Length; i++)
        {
            if (string.Equals(currentFlowId, supportedFlowIds[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private string ResolveCurrentFlowId()
    {
        string flowId = FlowContext.CurrentId;
        if (string.IsNullOrEmpty(flowId))
            flowId = PlayerPrefs.GetString("FLOW_ID", string.Empty);
        return flowId;
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
        englishTrueFalseQuestions = new List<EnglishTrueFalseQuestionDefinition>
        {
            englishTrueFalseQuestion,
            new EnglishTrueFalseQuestionDefinition
            {
                prompt = "?ㅼ쓬 臾몄옣??留욎쑝硫?True, ?由щ㈃ False瑜?怨좊Ⅴ?쒖삤.",
                statement = "Birds can swim underwater for an hour.",
                correctAnswer = false,
                explanation = "False."
            },
            new EnglishTrueFalseQuestionDefinition
            {
                prompt = "?ㅼ쓬 臾몄옣??留욎쑝硫?True, ?由щ㈃ False瑜?怨좊Ⅴ?쒖삤.",
                statement = "Winter comes after autumn.",
                correctAnswer = true,
                explanation = "True."
            }
        };
    }

    private void EnsureEnglishMatchingPairsOrFallback()
    {
        if (englishMatchingPairs != null && englishMatchingPairs.Count > 0)
            return;

        englishMatchingPairs = new List<EnglishMatchingPairDefinition>
        {
            new EnglishMatchingPairDefinition { word = "sun", meaning = "태양" },
            new EnglishMatchingPairDefinition { word = "egg", meaning = "계란" },
            new EnglishMatchingPairDefinition { word = "paper", meaning = "종이" },
            new EnglishMatchingPairDefinition { word = "vegetable", meaning = "채소" },
        };
    }

    private void EnsureEnglishOrderingQuestionOrFallback()
    {
        bool hasWords = englishOrderingQuestion != null
            && englishOrderingQuestion.shuffledWords != null
            && englishOrderingQuestion.shuffledWords.Length > 0
            && englishOrderingQuestion.correctOrder != null
            && englishOrderingQuestion.correctOrder.Length > 0;

        if (hasWords)
            return;

        englishOrderingQuestion = new EnglishOrderingQuestionDefinition
        {
            prompt = "다음 단어를 올바른 순서로 배열하시오.",
            shuffledWords = new[] { "the problem", "difficult", "I", "found" },
            correctOrder = new[] { "I", "found", "the problem", "difficult" },
            answerSentence = "I found the problem difficult."
        };
    }

    private void EnsureEnglishTrueFalseQuestionOrFallback()
    {
        bool listValid = englishTrueFalseQuestions != null && englishTrueFalseQuestions.Count > 0;
        if (listValid)
            return;

        bool valid = englishTrueFalseQuestion != null && !string.IsNullOrWhiteSpace(englishTrueFalseQuestion.statement);
        if (valid)
        {
            englishTrueFalseQuestions = new List<EnglishTrueFalseQuestionDefinition>
            {
                englishTrueFalseQuestion
            };
            return;
        }

        englishTrueFalseQuestion = new EnglishTrueFalseQuestionDefinition
        {
            prompt = "다음 문장이 맞으면 True, 틀리면 False를 고르시오.",
            statement = "The sun rises in the west.",
            correctAnswer = false,
            explanation = "False. The sun rises in the east."
        };

        englishTrueFalseQuestions = new List<EnglishTrueFalseQuestionDefinition>
        {
            englishTrueFalseQuestion,
            new EnglishTrueFalseQuestionDefinition
            {
                prompt = "다음 문장이 맞으면 True, 틀리면 False를 고르시오.",
                statement = "Birds can swim underwater for an hour.",
                correctAnswer = false,
                explanation = "False."
            },
            new EnglishTrueFalseQuestionDefinition
            {
                prompt = "다음 문장이 맞으면 True, 틀리면 False를 고르시오.",
                statement = "Winter comes after autumn.",
                correctAnswer = true,
                explanation = "True."
            }
        };
    }

    private void EnsureEnglishListeningQuestionOrFallback()
    {
        bool IsValidListeningQuestion(EnglishListeningBlankQuestionDefinition question)
        {
            return question != null
                && !string.IsNullOrWhiteSpace(question.sentenceWithBlank)
                && question.choices != null
                && question.choices.Length > 0;
        }

        if (englishListeningQuestions == null)
            englishListeningQuestions = new List<EnglishListeningBlankQuestionDefinition>();

        englishListeningQuestions.RemoveAll(question => !IsValidListeningQuestion(question));

        if (englishListeningQuestions.Count == 0)
        {
            if (IsValidListeningQuestion(englishListeningQuestion))
            {
                englishListeningQuestions.Add(englishListeningQuestion);
            }
            else
            {
                englishListeningQuestions.Add(new EnglishListeningBlankQuestionDefinition
                {
                    prompt = "Listen to the audio and choose the word that best fits the blank.",
                    sentenceWithBlank = "I ____ to school every day.",
                    choices = new[] { "go", "goes", "going" },
                    correctChoiceIndex = 0,
                    completedSentence = "I go to school every day."
                });
            }
        }

        currentEnglishListeningQuestionIndex = Mathf.Clamp(currentEnglishListeningQuestionIndex, 0, englishListeningQuestions.Count - 1);
        englishListeningQuestion = englishListeningQuestions[currentEnglishListeningQuestionIndex];
    }

    private Transform FindSceneTransformByNameIncludeInactive(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        var activeAndInactive = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (activeAndInactive != null)
        {
            for (int i = 0; i < activeAndInactive.Length; i++)
            {
                Transform candidate = activeAndInactive[i];
                if (candidate != null && candidate.name == targetName && candidate.gameObject.scene.IsValid())
                    return candidate;
            }
        }

        var all = Resources.FindObjectsOfTypeAll<Transform>();
        if (all == null)
            return null;

        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate == null)
                continue;

            if (!candidate.gameObject.scene.IsValid())
                continue;

            if (candidate.name == targetName)
                return candidate;
        }

        return null;
    }

    private bool TryBindEnglishSceneHierarchy()
    {
        englishMinigameRoot = FindSceneTransformByNameIncludeInactive("EnglishMinigame");
        if (englishMinigameRoot == null)
            return false;

        englishCanvasRoot = FindDeepChild(englishMinigameRoot, "Canvas") as RectTransform;
        englishQuiz01Root = FindDeepChild(englishMinigameRoot, "Quiz_01") as RectTransform;
        englishQuiz02Root = FindDeepChild(englishMinigameRoot, "Quiz_02") as RectTransform;
        englishQuiz03Root = FindDeepChild(englishMinigameRoot, "Quiz_03") as RectTransform;
        englishQuiz04Root = FindDeepChild(englishMinigameRoot, "Quiz_04") as RectTransform;

        if (englishCanvasRoot == null || englishQuiz01Root == null || englishQuiz02Root == null || englishQuiz03Root == null || englishQuiz04Root == null)
            return false;

        englishQuiz01PromptText = FindDeepChild(englishQuiz01Root, "Text (TMP)")?.GetComponent<TextMeshProUGUI>();
        englishQuiz01WordsRoot = FindDeepChild(englishQuiz01Root, "Words") as RectTransform;
        englishQuiz01MeaningRoot = FindDeepChild(englishQuiz01Root, "Meaning") as RectTransform;

        englishQuiz02PromptText = FindDeepChild(englishQuiz02Root, "Text (TMP)")?.GetComponent<TextMeshProUGUI>();
        englishQuiz02WordsRoot = FindDeepChild(englishQuiz02Root, "Words") as RectTransform;
        englishQuiz02SceneAgainButton = FindDeepChild(englishQuiz02Root, "Btn_Again")?.GetComponent<Button>();
        englishQuiz02SceneSubmitButton = FindDeepChild(englishQuiz02Root, "Btn_Submit")?.GetComponent<Button>();

        englishQuiz03PromptText = FindDeepChild(englishQuiz03Root, "Text (TMP)")?.GetComponent<TextMeshProUGUI>();
        englishQuiz03QuestionText = FindDeepChild(englishQuiz03Root, "Question")?.GetComponent<TextMeshProUGUI>();
        englishQuiz03TrueButton = FindDeepChild(englishQuiz03Root, "True")?.GetComponent<Button>();
        englishQuiz03FalseButton = FindDeepChild(englishQuiz03Root, "False")?.GetComponent<Button>();

        englishQuiz04PromptText = FindDeepChild(englishQuiz04Root, "Text (TMP)")?.GetComponent<TextMeshProUGUI>();
        englishQuiz04QuestionText = FindDeepChild(englishQuiz04Root, "Quiz_04_Question")?.GetComponent<TextMeshProUGUI>();
        englishQuiz04SoundButton = FindDeepChild(englishQuiz04Root, "SoundButton")?.GetComponent<Button>();
        englishQuiz04SceneSubmitButton = FindDeepChild(englishQuiz04Root, "Btn_Submit")?.GetComponent<Button>();
        englishQuiz04ChoicesRoot = FindDeepChild(englishQuiz04Root, "Words") as RectTransform;
        if (englishQuiz04ChoicesRoot == null)
            englishQuiz04ChoicesRoot = FindDeepChild(englishQuiz04Root, "ChoicesRoot") as RectTransform;

        if (englishQuiz01PromptText != null) englishQuiz01PromptText.raycastTarget = false;
        if (englishQuiz02PromptText != null) englishQuiz02PromptText.raycastTarget = false;
        if (englishQuiz03PromptText != null) englishQuiz03PromptText.raycastTarget = false;
        if (englishQuiz03QuestionText != null) englishQuiz03QuestionText.raycastTarget = false;
        if (englishQuiz04PromptText != null) englishQuiz04PromptText.raycastTarget = false;
        if (englishQuiz04QuestionText != null) englishQuiz04QuestionText.raycastTarget = false;

        englishMinigameRoot.gameObject.SetActive(true);
        SetEnglishSceneStageActive(null);
        return true;
    }

    private void BuildCurrentEnglishStageUI()
    {
        if (englishUseSceneHierarchy)
        {
            BuildCurrentEnglishStageSceneUI();
            return;
        }

        if (uiCanvas != null)
        {
            Destroy(uiCanvas.gameObject);
            uiCanvas = null;
            uiRootRect = null;
        }

        if (currentEnglishStage <= 0)
        {
            BuildEnglishMatchingUI();
            return;
        }

        if (currentEnglishStage == 1)
        {
            BuildEnglishOrderingUI();
            return;
        }

        if (currentEnglishStage == 2)
        {
            BuildEnglishTrueFalseUI();
            return;
        }

        BuildEnglishListeningUI();
    }

    private void BuildCurrentEnglishStageSceneUI()
    {
        if (englishMinigameRoot == null)
        {
            englishUseSceneHierarchy = false;
            BuildCurrentEnglishStageUI();
            return;
        }

        englishMinigameRoot.gameObject.SetActive(true);
        ClearEnglishSceneRuntime();

        if (currentEnglishStage <= 0)
        {
            BuildEnglishMatchingSceneUI();
            return;
        }

        if (currentEnglishStage == 1)
        {
            BuildEnglishOrderingSceneUI();
            return;
        }

        if (currentEnglishStage == 2)
        {
            BuildEnglishTrueFalseSceneUI();
            return;
        }

        BuildEnglishListeningSceneUI();
    }

    private string GetEnglishStageHintText(int stageIndex)
    {
        switch (stageIndex)
        {
            case 0:
                return "왼쪽 단어를 누른 채 드래그해서 알맞은 뜻 위에 놓아보세요.";
            case 1:
                return "단어 타일을 마우스로 드래그해 순서를 바꾼 뒤 제출하세요.";
            case 2:
                return "문장을 읽고 사실이면 True, 아니면 False를 고르세요.";
            case 3:
                return "음성을 듣고 빈칸에 들어갈 단어를 고른 뒤 제출하세요.";
            default:
                return string.Empty;
        }
    }

    private void NormalizeEnglishMinigameStrings()
    {
        englishMatchingTitle = "Match the Correct Pair";
        englishMatchingDescription = "Drag each word to the matching meaning.";
        englishOrderingTitle = "Arrange the following words in the correct order.";
        englishTrueFalseTitle = "True or False";
        englishListeningTitle = "Listen and Choose the Correct Word";

        if (englishOrderingQuestion != null)
            englishOrderingQuestion.prompt = "Arrange the following words in the correct order.";

        if (englishTrueFalseQuestion != null)
            englishTrueFalseQuestion.prompt = "Choose True if the sentence is correct, or False if it is not.";

        if (englishTrueFalseQuestions != null)
        {
            for (int i = 0; i < englishTrueFalseQuestions.Count; i++)
            {
                if (englishTrueFalseQuestions[i] != null)
                    englishTrueFalseQuestions[i].prompt = "Choose True if the sentence is correct, or False if it is not.";
            }
        }

        if (englishListeningQuestion != null)
            englishListeningQuestion.prompt = "Listen to the audio and choose the word that best fits the blank.";
        if (englishListeningQuestions != null)
        {
            for (int i = 0; i < englishListeningQuestions.Count; i++)
            {
                if (englishListeningQuestions[i] != null)
                    englishListeningQuestions[i].prompt = "Listen to the audio and choose the word that best fits the blank.";
            }
        }
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        return existing != null ? existing : target.AddComponent<T>();
    }

    private Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform nested = FindDeepChild(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void SetEnglishSceneStageActive(RectTransform activeStage)
    {
        if (englishQuiz01Root != null) englishQuiz01Root.gameObject.SetActive(activeStage == englishQuiz01Root);
        if (englishQuiz02Root != null) englishQuiz02Root.gameObject.SetActive(activeStage == englishQuiz02Root);
        if (englishQuiz03Root != null) englishQuiz03Root.gameObject.SetActive(activeStage == englishQuiz03Root);
        if (englishQuiz04Root != null) englishQuiz04Root.gameObject.SetActive(activeStage == englishQuiz04Root);
    }

    private void ClearEnglishSceneRuntime()
    {
        if (englishSceneHintDrawer != null)
        {
            Destroy(englishSceneHintDrawer);
            englishSceneHintDrawer = null;
        }

        if (englishSceneFeedbackObject != null)
        {
            Destroy(englishSceneFeedbackObject);
            englishSceneFeedbackObject = null;
            feedbackText = null;
        }

        if (englishQuiz02SubmitButton != null && englishQuiz02SubmitButton.name == "SubmitButton_Runtime")
        {
            Destroy(englishQuiz02SubmitButton.gameObject);
            englishQuiz02SubmitButton = null;
        }

        if (englishQuiz02ResetButton != null && englishQuiz02ResetButton.name == "ResetButton_Runtime")
        {
            Destroy(englishQuiz02ResetButton.gameObject);
            englishQuiz02ResetButton = null;
        }

        if (englishQuiz04SubmitButton != null && englishQuiz04SubmitButton.name == "SubmitButton_Runtime")
        {
            Destroy(englishQuiz04SubmitButton.gameObject);
            englishQuiz04SubmitButton = null;
        }

        if (englishQuiz04ChoicesRoot != null)
        {
            for (int i = englishQuiz04ChoicesRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = englishQuiz04ChoicesRoot.GetChild(i);
                if (child.name.StartsWith("Choice_", StringComparison.Ordinal))
                    Destroy(child.gameObject);
            }
        }

        if (englishQuiz02WordsRoot != null)
        {
            for (int i = englishQuiz02WordsRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = englishQuiz02WordsRoot.GetChild(i);
                if (child.name.StartsWith("OrderWord_", StringComparison.Ordinal))
                    Destroy(child.gameObject);
            }
        }

        foreach (GameObject lineObject in englishLineObjects)
        {
            if (lineObject != null)
                Destroy(lineObject);
        }

        englishLineObjects.Clear();
        englishLeftButtons.Clear();
        englishRightButtons.Clear();
        englishLeftLabels.Clear();
        englishRightLabels.Clear();
        englishRightPairIndices.Clear();
        englishLeftButtonRects.Clear();
        englishRightButtonRects.Clear();
        englishMatchedPairs.Clear();
        englishOrderingCardHandles.Clear();
        englishSceneChoiceButtons.Clear();

        selectedLeftPairIndex = -1;
        selectedRightDisplayIndex = -1;
        englishDraggingLeftPairIndex = -1;
        englishListeningSelectedChoiceIndex = -1;

        if (englishPreviewLineImage != null)
        {
            Destroy(englishPreviewLineImage.gameObject);
            englishPreviewLineImage = null;
            englishPreviewLineRect = null;
        }
    }

    private TextMeshProUGUI CreateEnglishSceneFeedback(RectTransform parent)
    {
        var feedbackParent = englishCanvasRoot != null ? englishCanvasRoot : parent;
        var feedback = CreateText("Feedback_Runtime", feedbackParent, 24f, FontStyles.Bold);
        var rect = feedback.rectTransform;
        rect.anchorMin = new Vector2(0.22f, 0.18f);
        rect.anchorMax = new Vector2(0.78f, 0.30f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        feedback.alignment = TextAlignmentOptions.Center;
        feedback.enableWordWrapping = true;
        feedback.overflowMode = TextOverflowModes.Overflow;
        feedback.raycastTarget = false;
        feedback.color = feedbackSuccessColor;
        feedback.text = string.Empty;
        feedback.rectTransform.SetAsLastSibling();
        englishSceneFeedbackObject = feedback.gameObject;
        return feedback;
    }

    private Button CreateEnglishSceneActionButton(RectTransform parent, string runtimeName, string labelText, Vector2 anchorMin, Vector2 anchorMax)
    {
        var button = CreateButton(runtimeName, parent, out var label);
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        label.fontSize = 24f;
        label.text = labelText;
        return button;
    }

    private void EnsureEnglishSceneHintDrawer(RectTransform parent, int stageIndex)
    {
        string hint = GetEnglishStageHintText(stageIndex);
        if (string.IsNullOrWhiteSpace(hint))
            return;

        Transform hintParent = englishCanvasRoot != null ? englishCanvasRoot : parent;
        BuildEnglishHintDrawer(hintParent, hint);
        englishSceneHintDrawer = hintParent.Find("HintDrawer")?.gameObject;
    }

    private Button CreateEnglishWordInstance(RectTransform parent, string objectName, string text, Vector2? sizeOverride = null, bool preservePrefabLayout = false)
    {
        GameObject wordObject;
        bool usingPrefab = englishWordPrefab != null;
        if (englishWordPrefab != null)
        {
            wordObject = Instantiate(englishWordPrefab, parent);
            wordObject.name = objectName;
        }
        else
        {
            wordObject = CreateButton(objectName, parent, out _).gameObject;
        }

        var button = GetOrAddComponent<Button>(wordObject);
        var image = GetOrAddComponent<Image>(wordObject);
        GetOrAddComponent<CanvasGroup>(wordObject);
        button.targetGraphic = image;
        image.raycastTarget = true;
        var rect = wordObject.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;

        var label = wordObject.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null)
            label = CreateText("Text (TMP)", wordObject.transform, 30f, FontStyles.Bold);

        label.text = text;
        label.raycastTarget = false;

        if (!usingPrefab)
        {
            image.color = new Color(0.98f, 0.84f, 0.84f, 1f);
            label.fontSize = 30f;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = new Color(0.16f, 0.16f, 0.18f, 1f);
        }

        if (!(usingPrefab && preservePrefabLayout))
        {
            var layout = GetOrAddComponent<LayoutElement>(wordObject);
            float preferredWidth = Mathf.Clamp(label.GetPreferredValues(text).x + 56f, 150f, 420f);
            float preferredHeight = sizeOverride?.y ?? (usingPrefab ? Mathf.Max(rect.rect.height, rect.sizeDelta.y, 74f) : 74f);
            Vector2 preferredSize = sizeOverride ?? new Vector2(preferredWidth, preferredHeight);
            layout.preferredWidth = preferredSize.x;
            layout.minWidth = preferredSize.x;
            layout.preferredHeight = preferredSize.y;
            layout.minHeight = preferredSize.y;
            rect.sizeDelta = preferredSize;
        }

        return button;
    }

    private void BuildEnglishMatchingSceneUI()
    {
        SetEnglishSceneStageActive(englishQuiz01Root);
        if (englishQuiz01PromptText != null)
            englishQuiz01PromptText.text = englishMatchingDescription;

        feedbackText = CreateEnglishSceneFeedback(englishQuiz01Root);
        EnsureEnglishSceneHintDrawer(englishQuiz01Root, 0);

        if (englishQuiz01WordsRoot == null || englishQuiz01MeaningRoot == null)
            return;

        Transform lineLayer = FindDeepChild(englishQuiz01Root, "LineLayer");
        if (lineLayer == null)
        {
            var lineLayerGo = CreateUIObject("LineLayer", englishQuiz01Root);
            englishLineLayer = lineLayerGo.GetComponent<RectTransform>();
            englishLineLayer.anchorMin = Vector2.zero;
            englishLineLayer.anchorMax = Vector2.one;
            englishLineLayer.offsetMin = Vector2.zero;
            englishLineLayer.offsetMax = Vector2.zero;
            englishLineLayer.SetSiblingIndex(0);
        }
        else
        {
            englishLineLayer = lineLayer.GetComponent<RectTransform>();
        }

        var leftRoots = new List<Button>(englishQuiz01WordsRoot.GetComponentsInChildren<Button>(true));
        leftRoots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        var rightRoots = new List<Button>(englishQuiz01MeaningRoot.GetComponentsInChildren<Button>(true));
        rightRoots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        int pairCount = Mathf.Min(englishMatchingPairs.Count, leftRoots.Count, rightRoots.Count);
        var displayOrder = new List<int>();
        for (int i = 0; i < pairCount; i++)
            displayOrder.Add(i);
        for (int i = 0; i < displayOrder.Count; i++)
        {
            int swapIndex = UnityEngine.Random.Range(i, displayOrder.Count);
            (displayOrder[i], displayOrder[swapIndex]) = (displayOrder[swapIndex], displayOrder[i]);
        }

        for (int i = 0; i < leftRoots.Count; i++)
        {
            bool active = i < pairCount;
            leftRoots[i].gameObject.SetActive(active);
            if (!active)
                continue;

            var label = leftRoots[i].GetComponentInChildren<TextMeshProUGUI>(true);
            label.text = englishMatchingPairs[i].word;
            englishLeftButtons.Add(leftRoots[i]);
            englishLeftLabels.Add(label);
            englishLeftButtonRects.Add(leftRoots[i].GetComponent<RectTransform>());
            var dragSource = GetOrAddComponent<EnglishMatchingDragSource>(leftRoots[i].gameObject);
            dragSource.controller = this;
            dragSource.pairIndex = i;
        }

        for (int i = 0; i < rightRoots.Count; i++)
        {
            bool active = i < pairCount;
            rightRoots[i].gameObject.SetActive(active);
            if (!active)
                continue;

            int pairIndex = displayOrder[i];
            var label = rightRoots[i].GetComponentInChildren<TextMeshProUGUI>(true);
            label.text = englishMatchingPairs[pairIndex].meaning;
            englishRightButtons.Add(rightRoots[i]);
            englishRightLabels.Add(label);
            englishRightButtonRects.Add(rightRoots[i].GetComponent<RectTransform>());
            englishRightPairIndices.Add(pairIndex);
            var dropTarget = GetOrAddComponent<EnglishMatchingDropTarget>(rightRoots[i].gameObject);
            dropTarget.controller = this;
            dropTarget.displayIndex = i;
        }

        Canvas.ForceUpdateCanvases();
        UpdateEnglishButtonStates();
    }

    private void BuildEnglishOrderingSceneUI()
    {
        SetEnglishSceneStageActive(englishQuiz02Root);
        if (englishQuiz02PromptText != null)
            englishQuiz02PromptText.text = englishOrderingTitle;

        feedbackText = CreateEnglishSceneFeedback(englishQuiz02Root);
        EnsureEnglishSceneHintDrawer(englishQuiz02Root, 1);

        if (englishQuiz02WordsRoot == null)
            return;

        var layout = englishQuiz02WordsRoot.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
            layout.enabled = false;

        var fitter = englishQuiz02WordsRoot.GetComponent<ContentSizeFitter>();
        if (fitter != null)
            fitter.enabled = false;

        englishOrderingTilesRoot = englishQuiz02WordsRoot;
        englishOrderingAnswerText = null;
        activeEnglishOrderingDragHandle = null;
        ResetEnglishOrderingLayout();
        UpdateEnglishOrderingUI();

        englishQuiz02ResetButton = englishQuiz02SceneAgainButton != null
            ? englishQuiz02SceneAgainButton
            : CreateEnglishSceneActionButton(
            englishQuiz02Root,
            "ResetButton_Runtime",
            "처음 배열",
            new Vector2(0.24f, 0.08f),
            new Vector2(0.44f, 0.16f));
        englishQuiz02ResetButton.onClick.RemoveAllListeners();
        englishQuiz02ResetButton.onClick.AddListener(() =>
        {
            ResetEnglishOrderingLayout();
            UpdateEnglishOrderingUI();
            SetFeedback(string.Empty, true);
        });

        englishQuiz02SubmitButton = englishQuiz02SceneSubmitButton != null
            ? englishQuiz02SceneSubmitButton
            : CreateEnglishSceneActionButton(
            englishQuiz02Root,
            "SubmitButton_Runtime",
            "제출",
            new Vector2(0.56f, 0.08f),
            new Vector2(0.76f, 0.16f));
        englishQuiz02SubmitButton.onClick.RemoveAllListeners();
        englishQuiz02SubmitButton.onClick.AddListener(EvaluateEnglishOrderingAnswer);
    }

    private void BuildEnglishTrueFalseSceneUI()
    {
        SyncCurrentEnglishTrueFalseQuestion();
        SetEnglishSceneStageActive(englishQuiz03Root);

        if (englishQuiz03PromptText != null)
            englishQuiz03PromptText.text = englishTrueFalseTitle;
        if (englishQuiz03QuestionText != null)
            englishQuiz03QuestionText.text = englishTrueFalseQuestion.statement;

        feedbackText = CreateEnglishSceneFeedback(englishQuiz03Root);
        EnsureEnglishSceneHintDrawer(englishQuiz03Root, 2);

        if (englishQuiz03TrueButton != null)
        {
            englishQuiz03TrueButton.onClick.RemoveAllListeners();
            englishQuiz03TrueButton.onClick.AddListener(() => EvaluateEnglishTrueFalseAnswer(true));
        }

        if (englishQuiz03FalseButton != null)
        {
            englishQuiz03FalseButton.onClick.RemoveAllListeners();
            englishQuiz03FalseButton.onClick.AddListener(() => EvaluateEnglishTrueFalseAnswer(false));
        }
    }

    private void SyncCurrentEnglishListeningQuestion()
    {
        if (englishListeningQuestions == null || englishListeningQuestions.Count == 0)
            return;

        currentEnglishListeningQuestionIndex = Mathf.Clamp(currentEnglishListeningQuestionIndex, 0, englishListeningQuestions.Count - 1);
        englishListeningQuestion = englishListeningQuestions[currentEnglishListeningQuestionIndex];
    }

    private void BuildEnglishListeningSceneUI()
    {
        SyncCurrentEnglishListeningQuestion();
        SetEnglishSceneStageActive(englishQuiz04Root);
        if (englishQuiz04PromptText != null)
            englishQuiz04PromptText.text = englishListeningTitle;
        if (englishQuiz04QuestionText != null)
            englishQuiz04QuestionText.text = englishListeningQuestion.sentenceWithBlank;

        feedbackText = CreateEnglishSceneFeedback(englishQuiz04Root);
        EnsureEnglishSceneHintDrawer(englishQuiz04Root, 3);

        if (englishQuiz04SoundButton != null)
        {
            englishQuiz04SoundButton.onClick.RemoveAllListeners();
            englishQuiz04SoundButton.onClick.AddListener(PlayEnglishListeningAudio);
        }

        if (englishQuiz04ChoicesRoot == null)
        {
            var choicesRootGo = CreateUIObject("ChoicesRoot_Runtime", englishQuiz04Root, typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            englishQuiz04ChoicesRoot = choicesRootGo.GetComponent<RectTransform>();
            englishQuiz04ChoicesRoot.anchorMin = new Vector2(0.16f, 0.18f);
            englishQuiz04ChoicesRoot.anchorMax = new Vector2(0.84f, 0.30f);
            englishQuiz04ChoicesRoot.offsetMin = Vector2.zero;
            englishQuiz04ChoicesRoot.offsetMax = Vector2.zero;
        }

        var choiceLayout = GetOrAddComponent<HorizontalLayoutGroup>(englishQuiz04ChoicesRoot.gameObject);
        choiceLayout.childAlignment = TextAnchor.MiddleCenter;
        choiceLayout.childControlWidth = false;
        choiceLayout.childControlHeight = false;
        choiceLayout.childForceExpandWidth = false;
        choiceLayout.childForceExpandHeight = false;
        choiceLayout.spacing = 18f;
        var choiceFitter = GetOrAddComponent<ContentSizeFitter>(englishQuiz04ChoicesRoot.gameObject);
        choiceFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        choiceFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        englishSceneChoiceButtons.Clear();
        englishListeningSelectedChoiceIndex = -1;
        for (int i = 0; i < englishListeningQuestion.choices.Length; i++)
        {
            int choiceIndex = i;
            var button = CreateEnglishWordInstance(englishQuiz04ChoicesRoot, $"Choice_{i}", englishListeningQuestion.choices[i], preservePrefabLayout: true);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectEnglishListeningChoice(choiceIndex));
            button.transition = Selectable.Transition.None;
            var choiceImage = button.GetComponent<Image>();
            if (choiceImage != null)
                choiceImage.color = new Color(1f, 1f, 1f, 0f);
            var choiceLabel = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (choiceLabel != null)
                choiceLabel.raycastTarget = false;
            englishSceneChoiceButtons.Add(button);
        }

        englishQuiz04SubmitButton = CreateEnglishSceneActionButton(
            englishQuiz04Root,
            "SubmitButton_Runtime",
            "제출",
            new Vector2(0.40f, 0.08f),
            new Vector2(0.60f, 0.15f));
        englishQuiz04SubmitButton.onClick.RemoveAllListeners();
        englishQuiz04SubmitButton.onClick.AddListener(EvaluateEnglishListeningAnswer);
        if (englishQuiz04SceneSubmitButton != null && englishQuiz04SceneSubmitButton != englishQuiz04SubmitButton)
        {
            Destroy(englishQuiz04SubmitButton.gameObject);
            englishQuiz04SubmitButton = englishQuiz04SceneSubmitButton;
            englishQuiz04SubmitButton.onClick.RemoveAllListeners();
            englishQuiz04SubmitButton.onClick.AddListener(EvaluateEnglishListeningAnswer);
        }

        if (englishQuiz04SceneSubmitButton != null)
        {
            englishQuiz04SubmitButton = englishQuiz04SceneSubmitButton;
            englishQuiz04SubmitButton.onClick.RemoveAllListeners();
            englishQuiz04SubmitButton.onClick.AddListener(EvaluateEnglishListeningAnswer);
        }

        var quiz04SubmitLabel = englishQuiz04SubmitButton != null
            ? englishQuiz04SubmitButton.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;
        if (quiz04SubmitLabel != null)
        {
            quiz04SubmitLabel.text = "Submit";
            quiz04SubmitLabel.raycastTarget = false;
        }

        EnsureEnglishAudioSource();
        UpdateEnglishListeningChoiceVisuals();
    }

    private void BuildEnglishHintDrawer(Transform parent, string hintText)
    {
        if (string.IsNullOrWhiteSpace(hintText))
            return;

        var drawer = CreateUIObject("HintDrawer", parent, typeof(Image));
        var drawerRect = drawer.GetComponent<RectTransform>();
        drawerRect.anchorMin = new Vector2(1f, 0.50f);
        drawerRect.anchorMax = new Vector2(1f, 0.50f);
        drawerRect.pivot = new Vector2(1f, 0.5f);
        drawerRect.sizeDelta = new Vector2(360f, 220f);
        drawerRect.anchoredPosition = new Vector2(260f, 0f);

        var drawerImage = drawer.GetComponent<Image>();
        drawerImage.color = hintPanelColor;
        drawerImage.raycastTarget = true;
        AddThinOutline(drawer);

        var tab = CreateUIObject("HintTab", drawer.transform, typeof(Image));
        var tabRect = tab.GetComponent<RectTransform>();
        tabRect.anchorMin = new Vector2(0f, 0.28f);
        tabRect.anchorMax = new Vector2(0f, 0.72f);
        tabRect.pivot = new Vector2(0f, 0.5f);
        tabRect.sizeDelta = new Vector2(96f, 96f);
        tabRect.anchoredPosition = Vector2.zero;
        var tabImage = tab.GetComponent<Image>();
        tabImage.color = new Color(0.99f, 0.94f, 0.80f, 1f);
        tabImage.raycastTarget = false;
        AddThinOutline(tab);

        var tabLabel = CreateText("HintTabLabel", tab.transform, 24f, FontStyles.Bold);
        StretchFull(tabLabel.rectTransform);
        tabLabel.alignment = TextAlignmentOptions.Center;
        tabLabel.color = accentColor;
        tabLabel.text = "Hint";
        tabLabel.raycastTarget = false;

        var bodyLabel = CreateText("HintBody", drawer.transform, 22f, FontStyles.Normal);
        bodyLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        bodyLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        bodyLabel.rectTransform.offsetMin = new Vector2(110f, 18f);
        bodyLabel.rectTransform.offsetMax = new Vector2(-20f, -18f);
        bodyLabel.alignment = TextAlignmentOptions.MidlineLeft;
        bodyLabel.enableWordWrapping = true;
        bodyLabel.overflowMode = TextOverflowModes.Overflow;
        bodyLabel.color = new Color(0.16f, 0.16f, 0.18f, 1f);
        bodyLabel.text = hintText;
        bodyLabel.raycastTarget = false;

        var drawerHandle = drawer.AddComponent<HoverHintDrawer>();
        drawerHandle.drawerRect = drawerRect;
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

    private void BuildEnglishMatchingUI()
    {
        var canvasGo = new GameObject("__AfterSchoolEnglishUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
        root.GetComponent<Image>().color = new Color(dimColor.r, dimColor.g, dimColor.b, 0.18f);

        var panel = CreateUIObject("MainPanel", root.transform, typeof(Image));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.08f, 0.08f);
        panelRect.anchorMax = new Vector2(0.92f, 0.92f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = panelColor;
        AddThinOutline(panel);

        var titleText = CreateText("Title", panel.transform, 52f, FontStyles.Bold);
        titleText.rectTransform.anchorMin = new Vector2(0.08f, 0.86f);
        titleText.rectTransform.anchorMax = new Vector2(0.92f, 0.96f);
        titleText.rectTransform.offsetMin = Vector2.zero;
        titleText.rectTransform.offsetMax = Vector2.zero;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = accentColor;
        titleText.text = englishMatchingTitle;

        var descText = CreateText("Description", panel.transform, 26f, FontStyles.Normal);
        descText.rectTransform.anchorMin = new Vector2(0.10f, 0.77f);
        descText.rectTransform.anchorMax = new Vector2(0.90f, 0.85f);
        descText.rectTransform.offsetMin = Vector2.zero;
        descText.rectTransform.offsetMax = Vector2.zero;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = new Color(0.20f, 0.20f, 0.24f, 1f);
        descText.text = englishMatchingDescription;

        feedbackText = CreateText("Feedback", panel.transform, 22f, FontStyles.Bold);
        feedbackText.rectTransform.anchorMin = new Vector2(0.10f, 0.70f);
        feedbackText.rectTransform.anchorMax = new Vector2(0.90f, 0.76f);
        feedbackText.rectTransform.offsetMin = Vector2.zero;
        feedbackText.rectTransform.offsetMax = Vector2.zero;
        feedbackText.alignment = TextAlignmentOptions.Center;
        feedbackText.color = feedbackSuccessColor;
        feedbackText.text = string.Empty;

        BuildEnglishHintDrawer(panel.transform, GetEnglishStageHintText(0));

        var board = CreateUIObject("Board", panel.transform);
        var boardRect = board.GetComponent<RectTransform>();
        boardRect.anchorMin = new Vector2(0.06f, 0.10f);
        boardRect.anchorMax = new Vector2(0.94f, 0.68f);
        boardRect.offsetMin = Vector2.zero;
        boardRect.offsetMax = Vector2.zero;

        var leftColumn = CreateUIObject("LeftColumn", board.transform, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var leftRect = leftColumn.GetComponent<RectTransform>();
        leftRect.anchorMin = new Vector2(0.00f, 0.0f);
        leftRect.anchorMax = new Vector2(0.28f, 1.0f);
        leftRect.offsetMin = Vector2.zero;
        leftRect.offsetMax = Vector2.zero;
        ConfigureMatchColumn(leftColumn.GetComponent<VerticalLayoutGroup>(), leftColumn.GetComponent<ContentSizeFitter>());

        var rightColumn = CreateUIObject("RightColumn", board.transform, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var rightRect = rightColumn.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(0.72f, 0.0f);
        rightRect.anchorMax = new Vector2(1.00f, 1.0f);
        rightRect.offsetMin = Vector2.zero;
        rightRect.offsetMax = Vector2.zero;
        ConfigureMatchColumn(rightColumn.GetComponent<VerticalLayoutGroup>(), rightColumn.GetComponent<ContentSizeFitter>());

        var lineLayer = CreateUIObject("LineLayer", board.transform);
        englishLineLayer = lineLayer.GetComponent<RectTransform>();
        englishLineLayer.anchorMin = Vector2.zero;
        englishLineLayer.anchorMax = Vector2.one;
        englishLineLayer.offsetMin = Vector2.zero;
        englishLineLayer.offsetMax = Vector2.zero;

        BuildEnglishMatchingButtons(leftColumn.transform, rightColumn.transform);
    }

    private void ConfigureMatchColumn(VerticalLayoutGroup layout, ContentSizeFitter fitter)
    {
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 24f;

        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    private void BuildEnglishMatchingButtons(Transform leftParent, Transform rightParent)
    {
        englishLeftButtons.Clear();
        englishRightButtons.Clear();
        englishLeftLabels.Clear();
        englishRightLabels.Clear();
        englishRightPairIndices.Clear();
        englishMatchedPairs.Clear();
        englishLeftButtonRects.Clear();
        englishRightButtonRects.Clear();
        selectedLeftPairIndex = -1;
        selectedRightDisplayIndex = -1;
        englishDraggingLeftPairIndex = -1;
        englishMatchDropResolvedThisDrag = false;

        if (englishPreviewLineImage != null)
        {
            Destroy(englishPreviewLineImage.gameObject);
            englishPreviewLineImage = null;
            englishPreviewLineRect = null;
        }

        var displayOrder = new List<int>();
        for (int i = 0; i < englishMatchingPairs.Count; i++)
            displayOrder.Add(i);

        for (int i = 0; i < displayOrder.Count; i++)
        {
            int swapIndex = UnityEngine.Random.Range(i, displayOrder.Count);
            (displayOrder[i], displayOrder[swapIndex]) = (displayOrder[swapIndex], displayOrder[i]);
        }

        for (int i = 0; i < englishMatchingPairs.Count; i++)
        {
            int leftPairIndex = i;
            var leftButton = CreateButton($"Left_{i}", leftParent, out var leftLabel);
            var leftRect = leftButton.GetComponent<RectTransform>();
            englishLeftButtons.Add(leftButton);
            englishLeftLabels.Add(leftLabel);
            englishLeftButtonRects.Add(leftRect);
            SetPreferredHeight(leftRect, 116f);
            leftLabel.fontSize = 34f;
            leftLabel.color = new Color(0.16f, 0.16f, 0.18f, 1f);
            leftLabel.text = englishMatchingPairs[leftPairIndex].word;
            var leftSource = leftButton.gameObject.AddComponent<EnglishMatchingDragSource>();
            leftSource.controller = this;
            leftSource.pairIndex = leftPairIndex;

            int rightPairIndex = displayOrder[i];
            englishRightPairIndices.Add(rightPairIndex);
            int rightDisplayIndex = i;
            var rightButton = CreateButton($"Right_{i}", rightParent, out var rightLabel);
            var rightRect = rightButton.GetComponent<RectTransform>();
            englishRightButtons.Add(rightButton);
            englishRightLabels.Add(rightLabel);
            englishRightButtonRects.Add(rightRect);
            SetPreferredHeight(rightRect, 116f);
            rightLabel.fontSize = 34f;
            rightLabel.color = new Color(0.16f, 0.16f, 0.18f, 1f);
            rightLabel.text = englishMatchingPairs[rightPairIndex].meaning;
            var rightTarget = rightButton.gameObject.AddComponent<EnglishMatchingDropTarget>();
            rightTarget.controller = this;
            rightTarget.displayIndex = rightDisplayIndex;
        }

        Canvas.ForceUpdateCanvases();
        UpdateEnglishButtonStates();
    }

    private void PrepareEnglishMatchDrag(int pairIndex)
    {
        if (advancing || englishMatchedPairs.Contains(pairIndex))
            return;

        selectedLeftPairIndex = pairIndex;
        selectedRightDisplayIndex = -1;
        UpdateEnglishButtonStates();
    }

    private void BeginEnglishMatchDrag(int pairIndex, PointerEventData eventData)
    {
        if (advancing || englishMatchedPairs.Contains(pairIndex))
            return;

        englishDraggingLeftPairIndex = pairIndex;
        englishMatchDropResolvedThisDrag = false;
        selectedLeftPairIndex = pairIndex;
        selectedRightDisplayIndex = -1;
        EnsureEnglishPreviewLine();
        UpdateEnglishMatchDrag(eventData);
        UpdateEnglishButtonStates();
    }

    private void UpdateEnglishMatchDrag(PointerEventData eventData)
    {
        if (englishDraggingLeftPairIndex < 0 || englishPreviewLineRect == null || englishLineLayer == null)
            return;

        RectTransform leftRect = englishLeftButtonRects[englishDraggingLeftPairIndex];
        Vector2 leftPoint = GetRectCenterInLayer(leftRect, englishLineLayer);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                englishLineLayer,
                eventData.position,
                GetUICamera(),
                out var dragPoint))
        {
            return;
        }

        UpdateEnglishLineRect(englishPreviewLineRect, leftPoint, dragPoint, 6f);
    }

    private void ResolveEnglishDraggedMatch(int displayIndex)
    {
        if (englishDraggingLeftPairIndex < 0 || advancing)
            return;

        englishMatchDropResolvedThisDrag = true;
        selectedRightDisplayIndex = displayIndex;
        UpdateEnglishButtonStates();
        TryResolveEnglishSelection();
        EndEnglishMatchDrag();
    }

    private void TryResolveEnglishSelection()
    {
        if (selectedLeftPairIndex < 0 || selectedRightDisplayIndex < 0)
            return;

        int rightPairIndex = englishRightPairIndices[selectedRightDisplayIndex];
        if (selectedLeftPairIndex == rightPairIndex)
        {
            englishMatchedPairs.Add(selectedLeftPairIndex);
            DrawEnglishMatchLine(selectedLeftPairIndex, selectedRightDisplayIndex);
            SetFeedback("정답!", true);
            selectedLeftPairIndex = -1;
            selectedRightDisplayIndex = -1;
            englishDraggingLeftPairIndex = -1;
            UpdateEnglishButtonStates();

            if (englishMatchedPairs.Count >= englishMatchingPairs.Count)
            {
                if (advanceRoutine != null)
                    StopCoroutine(advanceRoutine);
                advanceRoutine = StartCoroutine(CoAdvanceAfterEnglishMatchSuccess());
            }

            return;
        }

        SetFeedback("다시 연결해 보자.", false);
        selectedLeftPairIndex = -1;
        selectedRightDisplayIndex = -1;
        englishDraggingLeftPairIndex = -1;
        UpdateEnglishButtonStates();
    }

    private void EndEnglishMatchDrag()
    {
        HideEnglishPreviewLine();

        if (englishMatchDropResolvedThisDrag)
        {
            englishMatchDropResolvedThisDrag = false;
            return;
        }

        englishDraggingLeftPairIndex = -1;
        selectedLeftPairIndex = -1;
        selectedRightDisplayIndex = -1;
        UpdateEnglishButtonStates();
    }

    private IEnumerator CoAdvanceAfterEnglishMatchSuccess()
    {
        advancing = true;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, englishMatchSuccessDelaySeconds));

        if (ended)
            yield break;

        advanceRoutine = null;
        advancing = false;

        if (HasEnglishOrderingStage())
        {
            currentEnglishStage = 1;
            BuildCurrentEnglishStageUI();
            yield break;
        }

        End(true);
    }

    private bool HasEnglishOrderingStage()
    {
        return englishOrderingQuestion != null
            && englishOrderingQuestion.shuffledWords != null
            && englishOrderingQuestion.correctOrder != null
            && englishOrderingQuestion.shuffledWords.Length > 0
            && englishOrderingQuestion.correctOrder.Length > 0;
    }

    private bool HasEnglishTrueFalseStage()
    {
        return englishTrueFalseQuestions != null
            && englishTrueFalseQuestions.Count > 0
            && currentEnglishTrueFalseQuestionIndex < englishTrueFalseQuestions.Count
            && !string.IsNullOrWhiteSpace(englishTrueFalseQuestions[currentEnglishTrueFalseQuestionIndex].statement);
    }

    private void SyncCurrentEnglishTrueFalseQuestion()
    {
        if (englishTrueFalseQuestions == null || englishTrueFalseQuestions.Count == 0)
            return;

        currentEnglishTrueFalseQuestionIndex = Mathf.Clamp(
            currentEnglishTrueFalseQuestionIndex,
            0,
            englishTrueFalseQuestions.Count - 1);
        englishTrueFalseQuestion = englishTrueFalseQuestions[currentEnglishTrueFalseQuestionIndex];
    }

    private bool HasEnglishListeningStage()
    {
        return englishListeningQuestion != null
            && !string.IsNullOrWhiteSpace(englishListeningQuestion.sentenceWithBlank)
            && englishListeningQuestion.choices != null
            && englishListeningQuestion.choices.Length > 0;
    }

    private void DrawEnglishMatchLine(int leftPairIndex, int rightDisplayIndex)
    {
        if (englishLineLayer == null)
            return;

        RectTransform leftRect = englishLeftButtonRects[leftPairIndex];
        RectTransform rightRect = englishRightButtonRects[rightDisplayIndex];

        Vector2 leftPoint = GetRectCenterInLayer(leftRect, englishLineLayer);
        Vector2 rightPoint = GetRectCenterInLayer(rightRect, englishLineLayer);

        var lineGo = CreateUIObject($"Line_{leftPairIndex}", englishLineLayer, typeof(Image));
        var lineRect = lineGo.GetComponent<RectTransform>();
        var lineImage = lineGo.GetComponent<Image>();
        lineImage.color = new Color(0.94f, 0.38f, 0.36f, 1f);
        UpdateEnglishLineRect(lineRect, leftPoint, rightPoint, 8f);

        englishLineObjects.Add(lineGo);
    }

    private void EnsureEnglishPreviewLine()
    {
        if (englishPreviewLineImage != null || englishLineLayer == null)
            return;

        var previewGo = CreateUIObject("PreviewLine", englishLineLayer, typeof(Image));
        englishPreviewLineRect = previewGo.GetComponent<RectTransform>();
        englishPreviewLineImage = previewGo.GetComponent<Image>();
        englishPreviewLineImage.color = new Color(0.94f, 0.38f, 0.36f, 0.55f);
        englishPreviewLineImage.raycastTarget = false;
    }

    private void HideEnglishPreviewLine()
    {
        if (englishPreviewLineImage != null)
            englishPreviewLineImage.enabled = false;
    }

    private void UpdateEnglishLineRect(RectTransform lineRect, Vector2 startPoint, Vector2 endPoint, float thickness)
    {
        if (lineRect == null)
            return;

        Vector2 diff = endPoint - startPoint;
        float length = diff.magnitude;
        lineRect.sizeDelta = new Vector2(length, thickness);
        lineRect.anchoredPosition = startPoint + (diff * 0.5f);
        lineRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);

        if (englishPreviewLineImage != null && lineRect == englishPreviewLineRect)
            englishPreviewLineImage.enabled = true;
    }

    private Camera GetUICamera()
    {
        return uiCanvas != null && uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? uiCanvas.worldCamera
            : null;
    }

    private static Vector2 GetRectCenterInLayer(RectTransform target, RectTransform layer)
    {
        Vector3 world = target.TransformPoint(target.rect.center);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            layer,
            RectTransformUtility.WorldToScreenPoint(null, world),
            null,
            out var local);
        return local;
    }

    private void UpdateEnglishButtonStates()
    {
        for (int i = 0; i < englishLeftButtons.Count; i++)
        {
            bool isMatched = englishMatchedPairs.Contains(i);
            bool isSelected = selectedLeftPairIndex == i;
            ApplyEnglishButtonVisual(englishLeftButtons[i], englishLeftLabels[i], isMatched, isSelected);
        }

        for (int i = 0; i < englishRightButtons.Count; i++)
        {
            bool isMatched = englishMatchedPairs.Contains(englishRightPairIndices[i]);
            bool isSelected = selectedRightDisplayIndex == i;
            ApplyEnglishButtonVisual(englishRightButtons[i], englishRightLabels[i], isMatched, isSelected);
        }
    }

    private void BuildEnglishOrderingUI()
    {
        var canvasGo = new GameObject("__AfterSchoolEnglishOrderUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
        root.GetComponent<Image>().color = new Color(dimColor.r, dimColor.g, dimColor.b, 0.18f);

        var panel = CreateUIObject("MainPanel", root.transform, typeof(Image));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.08f, 0.08f);
        panelRect.anchorMax = new Vector2(0.92f, 0.92f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = panelColor;
        AddThinOutline(panel);

        var titleText = CreateText("Title", panel.transform, 48f, FontStyles.Bold);
        titleText.rectTransform.anchorMin = new Vector2(0.08f, 0.88f);
        titleText.rectTransform.anchorMax = new Vector2(0.92f, 0.96f);
        titleText.rectTransform.offsetMin = Vector2.zero;
        titleText.rectTransform.offsetMax = Vector2.zero;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = accentColor;
        titleText.text = englishOrderingTitle;

        var promptText = CreateText("Prompt", panel.transform, 28f, FontStyles.Normal);
        promptText.rectTransform.anchorMin = new Vector2(0.10f, 0.76f);
        promptText.rectTransform.anchorMax = new Vector2(0.90f, 0.86f);
        promptText.rectTransform.offsetMin = Vector2.zero;
        promptText.rectTransform.offsetMax = Vector2.zero;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = new Color(0.20f, 0.20f, 0.24f, 0.7f);
        promptText.text = string.Empty;

        feedbackText = CreateText("Feedback", panel.transform, 22f, FontStyles.Bold);
        feedbackText.rectTransform.anchorMin = new Vector2(0.10f, 0.60f);
        feedbackText.rectTransform.anchorMax = new Vector2(0.90f, 0.68f);
        feedbackText.rectTransform.offsetMin = Vector2.zero;
        feedbackText.rectTransform.offsetMax = Vector2.zero;
        feedbackText.alignment = TextAlignmentOptions.Center;
        feedbackText.color = feedbackSuccessColor;
        feedbackText.text = string.Empty;

        BuildEnglishHintDrawer(panel.transform, GetEnglishStageHintText(1));

        englishOrderingAnswerText = null;

        var wordsPanel = CreateUIObject("WordsPanel", panel.transform, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var wordsRect = wordsPanel.GetComponent<RectTransform>();
        wordsRect.anchorMin = new Vector2(0.10f, 0.24f);
        wordsRect.anchorMax = new Vector2(0.90f, 0.60f);
        wordsRect.offsetMin = Vector2.zero;
        wordsRect.offsetMax = Vector2.zero;
        var wordsLayout = wordsPanel.GetComponent<VerticalLayoutGroup>();
        wordsLayout.childAlignment = TextAnchor.UpperCenter;
        wordsLayout.childControlHeight = false;
        wordsLayout.childControlWidth = true;
        wordsLayout.childForceExpandHeight = false;
        wordsLayout.childForceExpandWidth = true;
        wordsLayout.spacing = 18f;
        var wordsFitter = wordsPanel.GetComponent<ContentSizeFitter>();
        wordsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        wordsFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var instructionText = CreateText("OrderingInstruction", wordsPanel.transform, 22f, FontStyles.Normal);
        instructionText.alignment = TextAlignmentOptions.Center;
        instructionText.color = new Color(0.24f, 0.24f, 0.28f, 1f);
        instructionText.text = "단어를 좌우로 옮겨 순서를 맞춘 뒤 제출하세요.";
        instructionText.text = "단어 타일을 마우스로 드래그해 올바른 순서로 배열한 뒤 제출하세요.";
        SetPreferredHeight(instructionText.rectTransform, 42f);

        var tilesRoot = CreateUIObject("TilesRoot", wordsPanel.transform, typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        englishOrderingTilesRoot = tilesRoot.GetComponent<RectTransform>();
        var tilesLayout = tilesRoot.GetComponent<HorizontalLayoutGroup>();
        tilesLayout.childAlignment = TextAnchor.MiddleCenter;
        tilesLayout.childControlHeight = false;
        tilesLayout.childControlWidth = true;
        tilesLayout.childForceExpandHeight = false;
        tilesLayout.childForceExpandWidth = false;
        tilesLayout.spacing = 16f;
        var tilesFitter = tilesRoot.GetComponent<ContentSizeFitter>();
        tilesFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        tilesFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        SetPreferredHeight(englishOrderingTilesRoot, 156f);

        var resetButton = CreateButton("ResetButton", panel.transform, out var resetLabel);
        englishOrderingResetButton = resetButton;
        var resetRect = resetButton.GetComponent<RectTransform>();
        resetRect.anchorMin = new Vector2(0.24f, 0.08f);
        resetRect.anchorMax = new Vector2(0.44f, 0.14f);
        resetRect.offsetMin = Vector2.zero;
        resetRect.offsetMax = Vector2.zero;
        resetLabel.fontSize = 24f;
        resetLabel.text = "처음 배열";
        resetButton.onClick.AddListener(ResetEnglishOrderingLayout);

        var submitButton = CreateButton("SubmitButton", panel.transform, out var submitLabel);
        englishOrderingSubmitButton = submitButton;
        var submitRect = submitButton.GetComponent<RectTransform>();
        submitRect.anchorMin = new Vector2(0.56f, 0.08f);
        submitRect.anchorMax = new Vector2(0.76f, 0.14f);
        submitRect.offsetMin = Vector2.zero;
        submitRect.offsetMax = Vector2.zero;
        submitLabel.fontSize = 24f;
        submitLabel.text = "제출";
        submitButton.onClick.AddListener(EvaluateEnglishOrderingAnswer);

        ResetEnglishOrderingLayout();

        UpdateEnglishOrderingUI();
    }

    private void MoveEnglishOrderingWord(int currentIndex, int direction)
    {
        if (advancing)
            return;

        int nextIndex = currentIndex + direction;
        if (currentIndex < 0 || currentIndex >= englishOrderingCurrentOrder.Count)
            return;
        if (nextIndex < 0 || nextIndex >= englishOrderingCurrentOrder.Count)
            return;

        (englishOrderingCurrentOrder[currentIndex], englishOrderingCurrentOrder[nextIndex]) =
            (englishOrderingCurrentOrder[nextIndex], englishOrderingCurrentOrder[currentIndex]);

        UpdateEnglishOrderingUI();
    }

    private void BeginEnglishOrderingDrag(EnglishOrderingCardDragHandle handle, PointerEventData eventData)
    {
        if (advancing || handle == null || englishOrderingTilesRoot == null)
            return;

        int wordIndex = (handle.displayIndex >= 0 && handle.displayIndex < englishOrderingCurrentOrder.Count)
            ? englishOrderingCurrentOrder[handle.displayIndex]
            : -1;
        handle.dragWordText = wordIndex >= 0 && wordIndex < englishOrderingQuestion.shuffledWords.Length
            ? englishOrderingQuestion.shuffledWords[wordIndex]
            : string.Empty;

        RectTransform dragLayer = englishCanvasRoot != null ? englishCanvasRoot : englishOrderingTilesRoot;
        handle.originalParent = handle.RectTransform.parent;
        handle.originalSiblingIndex = handle.RectTransform.GetSiblingIndex();

        Vector2 dragStartPosition = handle.RectTransform.anchoredPosition;
        Vector3 worldCenter = handle.RectTransform.TransformPoint(handle.RectTransform.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(GetUICamera(), worldCenter);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragLayer,
                screenPoint,
                GetUICamera(),
                out var dragLocalPoint))
        {
            dragStartPosition = dragLocalPoint;
        }

        handle.RectTransform.SetParent(dragLayer, true);
        handle.RectTransform.SetAsLastSibling();
        handle.RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        handle.RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        handle.RectTransform.pivot = new Vector2(0.5f, 0.5f);
        handle.RectTransform.anchoredPosition = dragStartPosition;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragLayer,
                eventData.position,
                GetUICamera(),
                out var localPoint))
        {
            handle.dragStartPointerLocalPosition = localPoint;
            handle.dragPointerOffset = handle.RectTransform.anchoredPosition - localPoint;
        }
        else
        {
            handle.dragStartPointerLocalPosition = Vector2.zero;
            handle.dragPointerOffset = Vector2.zero;
        }

        handle.CanvasGroup.blocksRaycasts = false;
        handle.CanvasGroup.alpha = 0.92f;
        SetFeedback(string.Empty, true);
    }

    private void BeginEnglishOrderingPointerDrag(EnglishOrderingCardDragHandle handle, PointerEventData eventData)
    {
        if (handle == null || activeEnglishOrderingDragHandle == handle)
            return;

        activeEnglishOrderingDragHandle = handle;
        BeginEnglishOrderingDrag(handle, eventData);
    }

    private void TryBeginEnglishOrderingDragFromMouse()
    {
        if (EventSystem.current == null || englishOrderingTilesRoot == null)
            return;

        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        for (int i = 0; i < results.Count; i++)
        {
            var target = results[i].gameObject;
            if (target == null)
                continue;

            var handle = target.GetComponentInParent<EnglishOrderingCardDragHandle>();
            if (handle == null)
                continue;

            if (handle.RectTransform == null || handle.RectTransform.parent != englishOrderingTilesRoot)
                continue;

            BeginEnglishOrderingPointerDrag(handle, pointerData);
            return;
        }
    }

    private void UpdateEnglishOrderingDrag(EnglishOrderingCardDragHandle handle, PointerEventData eventData)
    {
        if (advancing || handle == null)
            return;

        RectTransform dragLayer = englishCanvasRoot != null ? englishCanvasRoot : englishOrderingTilesRoot;
        if (dragLayer == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragLayer,
                eventData.position,
                GetUICamera(),
                out var localPoint))
        {
            return;
        }

        handle.RectTransform.anchoredPosition = localPoint + handle.dragPointerOffset;
    }

    private void EndEnglishOrderingDrag(EnglishOrderingCardDragHandle handle, PointerEventData eventData)
    {
        if (handle == null)
            return;

        handle.CanvasGroup.alpha = 1f;
        handle.CanvasGroup.blocksRaycasts = true;

        if (handle.originalParent != null)
        {
            handle.RectTransform.SetParent(handle.originalParent, false);
            handle.RectTransform.SetSiblingIndex(handle.originalSiblingIndex);
        }

        int targetIndex = GetEnglishOrderingDropIndex(eventData.position, handle.displayIndex);
        ApplyEnglishOrderingDragReorder(handle.displayIndex, targetIndex);
    }

    private void EndEnglishOrderingPointerDrag(EnglishOrderingCardDragHandle handle, PointerEventData eventData)
    {
        if (handle == null)
            return;

        EndEnglishOrderingDrag(handle, eventData);
        if (activeEnglishOrderingDragHandle == handle)
            activeEnglishOrderingDragHandle = null;
    }

    private int GetEnglishOrderingDropIndex(Vector2 screenPosition, int fallbackIndex)
    {
        if (englishOrderingTilesRoot == null || englishOrderingCardHandles.Count == 0)
            return fallbackIndex;

        int targetIndex = englishOrderingCardHandles.Count - 1;
        for (int i = 0; i < englishOrderingCardHandles.Count; i++)
        {
            if (i == fallbackIndex)
                continue;

            var handle = englishOrderingCardHandles[i];
            if (handle == null)
                continue;

            Vector3 worldCenter = handle.RectTransform.TransformPoint(handle.RectTransform.rect.center);
            Vector2 handleScreenCenter = RectTransformUtility.WorldToScreenPoint(GetUICamera(), worldCenter);
            if (screenPosition.x < handleScreenCenter.x)
            {
                targetIndex = i;
                break;
            }
        }

        return Mathf.Clamp(targetIndex, 0, englishOrderingCardHandles.Count - 1);
    }

    private void ApplyEnglishOrderingDragReorder(int currentIndex, int targetIndex)
    {
        if (currentIndex < 0 || currentIndex >= englishOrderingCurrentOrder.Count)
        {
            UpdateEnglishOrderingUI();
            return;
        }

        targetIndex = Mathf.Clamp(targetIndex, 0, englishOrderingCurrentOrder.Count - 1);
        int wordIndex = englishOrderingCurrentOrder[currentIndex];
        englishOrderingCurrentOrder.RemoveAt(currentIndex);
        if (targetIndex > currentIndex)
            targetIndex--;
        englishOrderingCurrentOrder.Insert(targetIndex, wordIndex);
        UpdateEnglishOrderingUI();
    }

    private void ResetEnglishOrderingLayout()
    {
        if (advancing)
            return;

        englishOrderingCurrentOrder.Clear();
        for (int i = 0; i < englishOrderingQuestion.shuffledWords.Length; i++)
            englishOrderingCurrentOrder.Add(i);

        SetFeedback(string.Empty, true);
        UpdateEnglishOrderingUI();
    }

    private void LayoutEnglishOrderingCardsManually()
    {
        if (englishOrderingTilesRoot == null || englishOrderingCardHandles.Count == 0)
            return;

        const float spacing = 18f;
        float totalWidth = 0f;

        for (int i = 0; i < englishOrderingCardHandles.Count; i++)
        {
            var handle = englishOrderingCardHandles[i];
            if (handle == null)
                continue;

            var rect = handle.RectTransform;
            var layout = handle.LayoutElement;
            if (layout != null)
                layout.ignoreLayout = true;

            float width = rect.rect.width;
            if (width <= 1f && layout != null)
                width = layout.preferredWidth;
            if (width <= 1f)
                width = 160f;

            totalWidth += width;
            if (i < englishOrderingCardHandles.Count - 1)
                totalWidth += spacing;
        }

        float currentX = -totalWidth * 0.5f;
        for (int i = 0; i < englishOrderingCardHandles.Count; i++)
        {
            var handle = englishOrderingCardHandles[i];
            if (handle == null)
                continue;

            var rect = handle.RectTransform;
            var layout = handle.LayoutElement;
            float width = rect.rect.width;
            if (width <= 1f && layout != null)
                width = layout.preferredWidth;
            if (width <= 1f)
                width = 160f;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(currentX + (width * 0.5f), 0f);
            rect.localScale = Vector3.one;
            rect.SetSiblingIndex(i);

            currentX += width + spacing;
        }
    }

    private void UpdateEnglishOrderingUI()
    {
        if (englishOrderingAnswerText != null)
        {
            if (englishOrderingCurrentOrder.Count == 0)
            {
                englishOrderingAnswerText.text = "_";
            }
            else
            {
                var arrangedWords = new List<string>();
                for (int i = 0; i < englishOrderingCurrentOrder.Count; i++)
                    arrangedWords.Add(englishOrderingQuestion.shuffledWords[englishOrderingCurrentOrder[i]]);
                englishOrderingAnswerText.text = string.Join(" ", arrangedWords);
            }
        }

        englishOrderingCardHandles.Clear();

        if (englishOrderingTilesRoot != null)
        {
            for (int i = englishOrderingTilesRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = englishOrderingTilesRoot.GetChild(i);
                if (!englishUseSceneHierarchy
                    || child.name.StartsWith("OrderWord_", StringComparison.Ordinal)
                    || child.name.StartsWith("Word_", StringComparison.Ordinal))
                    Destroy(child.gameObject);
            }

            for (int i = 0; i < englishOrderingCurrentOrder.Count; i++)
            {
                int wordIndex = englishOrderingCurrentOrder[i];
                string word = englishOrderingQuestion.shuffledWords[wordIndex];

                Button button;
                GameObject card;
                if (englishUseSceneHierarchy)
                {
                    button = CreateEnglishWordInstance(englishOrderingTilesRoot, $"OrderWord_{i}", word);
                    card = button.gameObject;
                }
                else
                {
                    card = CreateUIObject(
                        $"WordCard_{i}",
                        englishOrderingTilesRoot,
                        typeof(Image),
                        typeof(LayoutElement),
                        typeof(CanvasGroup));
                    var cardImage = card.GetComponent<Image>();
                    cardImage.color = new Color(0.98f, 0.84f, 0.84f, 1f);
                    AddThinOutline(card);
                    var cardLayout = card.GetComponent<LayoutElement>();
                    cardLayout.minWidth = 160f;
                    cardLayout.preferredHeight = 140f;

                    var wordLabel = CreateText("WordLabel", card.transform, 28f, FontStyles.Bold);
                    StretchFull(wordLabel.rectTransform);
                    wordLabel.margin = new Vector4(18f, 18f, 18f, 18f);
                    wordLabel.alignment = TextAlignmentOptions.Center;
                    wordLabel.color = new Color(0.16f, 0.16f, 0.18f, 1f);
                    wordLabel.enableWordWrapping = false;
                    wordLabel.overflowMode = TextOverflowModes.Overflow;
                    wordLabel.raycastTarget = false;
                    wordLabel.text = word;
                    float preferredWordWidth = wordLabel.GetPreferredValues(word).x + 54f;
                    cardLayout.preferredWidth = Mathf.Clamp(preferredWordWidth, 160f, 420f);
                    card.GetComponent<RectTransform>().sizeDelta = new Vector2(cardLayout.preferredWidth, cardLayout.preferredHeight);
                }

                var dragHandle = GetOrAddComponent<EnglishOrderingCardDragHandle>(card);
                dragHandle.controller = this;
                dragHandle.displayIndex = i;
                englishOrderingCardHandles.Add(dragHandle);
            }

            Canvas.ForceUpdateCanvases();
            LayoutEnglishOrderingCardsManually();
        }
    }

    private void EvaluateEnglishOrderingAnswer()
    {
        bool correct = englishOrderingCurrentOrder.Count == englishOrderingQuestion.correctOrder.Length;
        for (int i = 0; correct && i < englishOrderingQuestion.correctOrder.Length; i++)
        {
            string selectedWord = englishOrderingQuestion.shuffledWords[englishOrderingCurrentOrder[i]];
            if (!string.Equals(selectedWord, englishOrderingQuestion.correctOrder[i], StringComparison.Ordinal))
                correct = false;
        }

        if (correct)
        {
            SetFeedback(englishOrderingQuestion.answerSentence, true);
            if (advanceRoutine != null)
                StopCoroutine(advanceRoutine);
            advanceRoutine = StartCoroutine(CoAdvanceAfterEnglishOrderingSuccess());
            return;
        }

        SetFeedback("순서를 다시 맞춰보자.", false);
        ResetEnglishOrderingLayout();
        UpdateEnglishOrderingUI();
    }

    private IEnumerator CoAdvanceAfterEnglishOrderingSuccess()
    {
        advancing = true;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, englishMatchSuccessDelaySeconds));

        if (ended)
            yield break;

        advanceRoutine = null;
        advancing = false;

        if (HasEnglishTrueFalseStage())
        {
            currentEnglishTrueFalseQuestionIndex = 0;
            currentEnglishStage = 2;
            BuildCurrentEnglishStageUI();
            yield break;
        }

        if (HasEnglishListeningStage())
        {
            currentEnglishStage = 3;
            BuildCurrentEnglishStageUI();
            yield break;
        }

        End(true);
    }

    private void BuildEnglishTrueFalseUI()
    {
        SyncCurrentEnglishTrueFalseQuestion();
        EnglishTrueFalseQuestionDefinition question = englishTrueFalseQuestion;
        var canvasGo = new GameObject("__AfterSchoolEnglishTrueFalseUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
        StretchFull(root.GetComponent<RectTransform>());
        root.GetComponent<Image>().color = new Color(dimColor.r, dimColor.g, dimColor.b, 0.18f);

        var panel = CreateUIObject("MainPanel", root.transform, typeof(Image));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.12f, 0.12f);
        panelRect.anchorMax = new Vector2(0.88f, 0.88f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = panelColor;
        AddThinOutline(panel);

        var titleText = CreateText("Title", panel.transform, 52f, FontStyles.Bold);
        titleText.rectTransform.anchorMin = new Vector2(0.08f, 0.84f);
        titleText.rectTransform.anchorMax = new Vector2(0.92f, 0.94f);
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = accentColor;
        titleText.text = englishTrueFalseTitle;

        var promptText = CreateText("Prompt", panel.transform, 28f, FontStyles.Normal);
        promptText.rectTransform.anchorMin = new Vector2(0.10f, 0.72f);
        promptText.rectTransform.anchorMax = new Vector2(0.90f, 0.82f);
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = new Color(0.20f, 0.20f, 0.24f, 1f);
        promptText.text = question.prompt;

        var statementPanel = CreateUIObject("StatementPanel", panel.transform, typeof(Image));
        var statementRect = statementPanel.GetComponent<RectTransform>();
        statementRect.anchorMin = new Vector2(0.12f, 0.45f);
        statementRect.anchorMax = new Vector2(0.88f, 0.67f);
        statementPanel.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.94f);
        AddThinOutline(statementPanel);

        var statementText = CreateText("Statement", statementPanel.transform, 36f, FontStyles.Bold);
        StretchFull(statementText.rectTransform);
        statementText.margin = new Vector4(20f, 20f, 20f, 20f);
        statementText.alignment = TextAlignmentOptions.Center;
        statementText.color = new Color(0.16f, 0.16f, 0.18f, 1f);
        statementText.text = question.statement;

        feedbackText = CreateText("Feedback", panel.transform, 24f, FontStyles.Bold);
        feedbackText.rectTransform.anchorMin = new Vector2(0.10f, 0.32f);
        feedbackText.rectTransform.anchorMax = new Vector2(0.90f, 0.40f);
        feedbackText.alignment = TextAlignmentOptions.Center;
        feedbackText.color = feedbackSuccessColor;
        feedbackText.text = string.Empty;

        BuildEnglishHintDrawer(panel.transform, GetEnglishStageHintText(2));

        var trueButton = CreateButton("TrueButton", panel.transform, out var trueLabel);
        var trueRect = trueButton.GetComponent<RectTransform>();
        trueRect.anchorMin = new Vector2(0.22f, 0.14f);
        trueRect.anchorMax = new Vector2(0.44f, 0.24f);
        trueLabel.fontSize = 34f;
        trueLabel.text = "True";
        trueButton.onClick.AddListener(() => EvaluateEnglishTrueFalseAnswer(true));

        var falseButton = CreateButton("FalseButton", panel.transform, out var falseLabel);
        var falseRect = falseButton.GetComponent<RectTransform>();
        falseRect.anchorMin = new Vector2(0.56f, 0.14f);
        falseRect.anchorMax = new Vector2(0.78f, 0.24f);
        falseLabel.fontSize = 34f;
        falseLabel.text = "False";
        falseButton.onClick.AddListener(() => EvaluateEnglishTrueFalseAnswer(false));
    }

    private void EvaluateEnglishTrueFalseAnswer(bool selected)
    {
        if (advancing)
            return;

        SyncCurrentEnglishTrueFalseQuestion();
        EnglishTrueFalseQuestionDefinition question = englishTrueFalseQuestion;
        bool correct = selected == question.correctAnswer;
        if (correct)
        {
            SetFeedback(string.IsNullOrWhiteSpace(englishTrueFalseQuestion.explanation) ? "정답!" : englishTrueFalseQuestion.explanation, true);
            if (advanceRoutine != null)
                StopCoroutine(advanceRoutine);
            advanceRoutine = StartCoroutine(CoAdvanceAfterEnglishTrueFalseSuccess());
            return;
        }

        SetFeedback("다시 생각해 보자.", false);
    }

    private IEnumerator CoAdvanceAfterEnglishTrueFalseSuccess()
    {
        advancing = true;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, englishMatchSuccessDelaySeconds));

        if (ended)
            yield break;

        advanceRoutine = null;
        advancing = false;

        if (englishTrueFalseQuestions != null && currentEnglishTrueFalseQuestionIndex + 1 < englishTrueFalseQuestions.Count)
        {
            currentEnglishTrueFalseQuestionIndex++;
            SyncCurrentEnglishTrueFalseQuestion();
            BuildCurrentEnglishStageUI();
            yield break;
        }

        if (HasEnglishListeningStage())
        {
            currentEnglishStage = 3;
            BuildCurrentEnglishStageUI();
            yield break;
        }

        End(true);
    }

    private void BuildEnglishListeningUI()
    {
        var canvasGo = new GameObject("__AfterSchoolEnglishListeningUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
        StretchFull(root.GetComponent<RectTransform>());
        root.GetComponent<Image>().color = new Color(dimColor.r, dimColor.g, dimColor.b, 0.18f);

        var panel = CreateUIObject("MainPanel", root.transform, typeof(Image));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.10f, 0.10f);
        panelRect.anchorMax = new Vector2(0.90f, 0.90f);
        panel.GetComponent<Image>().color = panelColor;
        AddThinOutline(panel);

        var titleText = CreateText("Title", panel.transform, 48f, FontStyles.Bold);
        titleText.rectTransform.anchorMin = new Vector2(0.08f, 0.86f);
        titleText.rectTransform.anchorMax = new Vector2(0.92f, 0.94f);
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = accentColor;
        titleText.text = englishListeningTitle;

        var promptText = CreateText("Prompt", panel.transform, 28f, FontStyles.Normal);
        promptText.rectTransform.anchorMin = new Vector2(0.10f, 0.75f);
        promptText.rectTransform.anchorMax = new Vector2(0.90f, 0.84f);
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = new Color(0.20f, 0.20f, 0.24f, 1f);
        promptText.text = englishListeningQuestion.prompt;

        var playButton = CreateButton("PlayAudioButton", panel.transform, out var playLabel);
        var playRect = playButton.GetComponent<RectTransform>();
        playRect.anchorMin = new Vector2(0.36f, 0.64f);
        playRect.anchorMax = new Vector2(0.64f, 0.72f);
        playLabel.fontSize = 30f;
        playLabel.text = "음성 재생";
        playButton.onClick.AddListener(PlayEnglishListeningAudio);

        var sentencePanel = CreateUIObject("SentencePanel", panel.transform, typeof(Image));
        var sentenceRect = sentencePanel.GetComponent<RectTransform>();
        sentenceRect.anchorMin = new Vector2(0.12f, 0.48f);
        sentenceRect.anchorMax = new Vector2(0.88f, 0.60f);
        sentencePanel.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.94f);
        AddThinOutline(sentencePanel);

        englishOrderingAnswerText = CreateText("SentenceText", sentencePanel.transform, 34f, FontStyles.Bold);
        StretchFull(englishOrderingAnswerText.rectTransform);
        englishOrderingAnswerText.margin = new Vector4(20f, 14f, 20f, 14f);
        englishOrderingAnswerText.alignment = TextAlignmentOptions.Center;
        englishOrderingAnswerText.color = new Color(0.16f, 0.16f, 0.18f, 1f);
        englishOrderingAnswerText.text = englishListeningQuestion.sentenceWithBlank;

        feedbackText = CreateText("Feedback", panel.transform, 22f, FontStyles.Bold);
        feedbackText.rectTransform.anchorMin = new Vector2(0.10f, 0.39f);
        feedbackText.rectTransform.anchorMax = new Vector2(0.90f, 0.45f);
        feedbackText.alignment = TextAlignmentOptions.Center;
        feedbackText.color = feedbackSuccessColor;
        feedbackText.text = string.Empty;

        BuildEnglishHintDrawer(panel.transform, GetEnglishStageHintText(3));

        var choicesRoot = CreateUIObject("ChoicesRoot", panel.transform, typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        var choicesRect = choicesRoot.GetComponent<RectTransform>();
        choicesRect.anchorMin = new Vector2(0.12f, 0.20f);
        choicesRect.anchorMax = new Vector2(0.88f, 0.34f);
        var choicesLayout = choicesRoot.GetComponent<HorizontalLayoutGroup>();
        choicesLayout.childAlignment = TextAnchor.MiddleCenter;
        choicesLayout.childControlHeight = false;
        choicesLayout.childControlWidth = false;
        choicesLayout.childForceExpandHeight = false;
        choicesLayout.childForceExpandWidth = false;
        choicesLayout.spacing = 16f;
        var choicesFitter = choicesRoot.GetComponent<ContentSizeFitter>();
        choicesFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        choicesFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        englishOrderingCurrentOrder.Clear();
        englishListeningSelectedChoiceIndex = -1;

        for (int i = 0; i < englishListeningQuestion.choices.Length; i++)
        {
            int choiceIndex = i;
            var button = CreateButton($"Choice_{i}", choicesRoot.transform, out var label);
            var rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 86f);
            label.fontSize = 28f;
            label.text = englishListeningQuestion.choices[i];
            label.color = new Color(0.16f, 0.16f, 0.18f, 1f);
            button.onClick.AddListener(() => SelectEnglishListeningChoice(choiceIndex));
        }

        var submitButton = CreateButton("SubmitButton", panel.transform, out var submitLabel);
        var submitRect = submitButton.GetComponent<RectTransform>();
        submitRect.anchorMin = new Vector2(0.38f, 0.08f);
        submitRect.anchorMax = new Vector2(0.62f, 0.14f);
        submitLabel.fontSize = 24f;
        submitLabel.text = "제출";
        submitButton.onClick.AddListener(EvaluateEnglishListeningAnswer);

        EnsureEnglishAudioSource();
    }

    private void EnsureEnglishAudioSource()
    {
        if (englishAudioSource != null)
            return;

        var host = new GameObject("__AfterSchoolEnglishAudio");
        englishAudioSource = host.AddComponent<AudioSource>();
        englishAudioSource.playOnAwake = false;
        DontDestroyOnLoad(host);
    }

    private void PlayEnglishListeningAudio()
    {
        if (englishListeningQuestion == null || englishListeningQuestion.voiceClip == null)
            return;

        EnsureEnglishAudioSource();
        englishAudioSource.Stop();
        englishAudioSource.clip = englishListeningQuestion.voiceClip;
        englishAudioSource.Play();
    }

    private void SelectEnglishListeningChoice(int choiceIndex)
    {
        if (advancing)
            return;

        englishListeningSelectedChoiceIndex = choiceIndex;
        UpdateEnglishListeningChoiceVisuals();
    }

    private void UpdateEnglishListeningChoiceVisuals()
    {
        if (englishUseSceneHierarchy)
        {
            for (int i = 0; i < englishSceneChoiceButtons.Count; i++)
            {
                Button button = englishSceneChoiceButtons[i];
                if (button == null)
                    continue;

                var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                bool isSelected = i == englishListeningSelectedChoiceIndex;
                if (label != null)
                    label.color = isSelected
                        ? new Color(0.82f, 0.26f, 0.26f, 1f)
                        : new Color(0.18f, 0.18f, 0.20f, 1f);
                var image = button.GetComponent<Image>();
                if (image != null)
                    image.color = new Color(1f, 1f, 1f, 0f);
            }

            return;
        }

        if (uiCanvas == null)
            return;

        var buttons = uiCanvas.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (!buttons[i].name.StartsWith("Choice_", StringComparison.Ordinal))
                continue;

            int idx = int.Parse(buttons[i].name.Substring("Choice_".Length));
            var colors = buttons[i].colors;
            colors.normalColor = idx == englishListeningSelectedChoiceIndex
                ? new Color(0.98f, 0.86f, 0.54f, 1f)
                : new Color(0.98f, 0.84f, 0.84f, 1f);
            colors.highlightedColor = Color.Lerp(colors.normalColor, Color.white, 0.08f);
            colors.pressedColor = Color.Lerp(colors.normalColor, Color.black, 0.08f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = colors.normalColor;
            buttons[i].colors = colors;
        }
    }

    private void EvaluateEnglishListeningAnswer()
    {
        if (advancing || englishListeningSelectedChoiceIndex < 0)
            return;

        bool correct = englishListeningSelectedChoiceIndex == englishListeningQuestion.correctChoiceIndex;
        if (correct)
        {
            if (englishOrderingAnswerText != null)
                englishOrderingAnswerText.text = string.IsNullOrWhiteSpace(englishListeningQuestion.completedSentence)
                    ? englishListeningQuestion.sentenceWithBlank
                    : englishListeningQuestion.completedSentence;

            SetFeedback("정답!", true);
            if (advanceRoutine != null)
                StopCoroutine(advanceRoutine);
            advanceRoutine = StartCoroutine(CoAdvanceAfterEnglishListeningSuccess());
            return;
        }

        SetFeedback("다시 들어보고 골라보자.", false);
    }

    private IEnumerator CoAdvanceAfterEnglishListeningSuccess()
    {
        advancing = true;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, englishMatchSuccessDelaySeconds));

        if (ended)
            yield break;

        advanceRoutine = null;
        advancing = false;

        if (englishListeningQuestions != null && currentEnglishListeningQuestionIndex + 1 < englishListeningQuestions.Count)
        {
            currentEnglishListeningQuestionIndex++;
            SyncCurrentEnglishListeningQuestion();
            BuildCurrentEnglishStageUI();
            yield break;
        }

        End(true);
    }

    private void ApplyEnglishButtonVisual(Button button, TextMeshProUGUI label, bool isMatched, bool isSelected)
    {
        if (button == null)
            return;

        var colors = button.colors;
        if (isMatched)
        {
            colors.normalColor = new Color(0.78f, 0.92f, 0.78f, 1f);
            button.interactable = false;
            if (label != null)
                label.color = new Color(0.22f, 0.44f, 0.24f, 1f);
        }
        else if (isSelected)
        {
            colors.normalColor = new Color(0.98f, 0.86f, 0.54f, 1f);
            button.interactable = true;
            if (label != null)
                label.color = new Color(0.20f, 0.18f, 0.12f, 1f);
        }
        else
        {
            colors.normalColor = new Color(0.98f, 0.84f, 0.84f, 1f);
            button.interactable = true;
            if (label != null)
                label.color = new Color(0.16f, 0.16f, 0.18f, 1f);
        }

        colors.highlightedColor = Color.Lerp(colors.normalColor, Color.white, 0.08f);
        colors.pressedColor = Color.Lerp(colors.normalColor, Color.black, 0.08f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = colors.normalColor;
        button.colors = colors;
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

        if (englishUseSceneHierarchy)
        {
            ClearEnglishSceneRuntime();
            SetEnglishSceneStageActive(null);
            if (englishMinigameRoot != null)
                englishMinigameRoot.gameObject.SetActive(false);
        }

        if (uiCanvas != null)
        {
            Destroy(uiCanvas.gameObject);
            uiCanvas = null;
        }

        englishPreviewLineImage = null;
        englishPreviewLineRect = null;
        englishLineLayer = null;
        englishOrderingTilesRoot = null;
        englishOrderingCardHandles.Clear();
        englishLeftButtonRects.Clear();
        englishRightButtonRects.Clear();

        if (englishAudioSource != null)
            englishAudioSource.Stop();

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
        supportedFlowIds = MergeSupportedFlowIds(supportedFlowIds, new[] { "AFTERSCHOOL_ENGLISH_D1", afterSchoolEnglishFlowId });

        if (config == null)
            return;

        if (config.supportedFlowIds != null && config.supportedFlowIds.Length > 0)
            supportedFlowIds = MergeSupportedFlowIds(supportedFlowIds, config.supportedFlowIds);

        penaltyOnGiveUp = config.penaltyOnGiveUp;
        correctAnswerDelaySeconds = config.correctAnswerDelaySeconds;

        if (config.questions != null && config.questions.Count > 0)
            questions = new List<MathQuestionDefinition>(config.questions);

        afterSchoolEnglishFlowId = string.IsNullOrWhiteSpace(config.afterSchoolEnglishFlowId)
            ? afterSchoolEnglishFlowId
            : config.afterSchoolEnglishFlowId;
        englishMatchingTitle = string.IsNullOrWhiteSpace(config.englishMatchingTitle)
            ? englishMatchingTitle
            : config.englishMatchingTitle;
        englishMatchingDescription = string.IsNullOrWhiteSpace(config.englishMatchingDescription)
            ? englishMatchingDescription
            : config.englishMatchingDescription;
        englishMatchSuccessDelaySeconds = config.englishMatchSuccessDelaySeconds;

        if (config.englishMatchingPairs != null && config.englishMatchingPairs.Count > 0)
        {
            englishMatchingPairs = new List<EnglishMatchingPairDefinition>();
            for (int i = 0; i < config.englishMatchingPairs.Count; i++)
            {
                var pair = config.englishMatchingPairs[i];
                englishMatchingPairs.Add(new EnglishMatchingPairDefinition
                {
                    word = pair.word,
                    meaning = pair.meaning
                });
            }
        }

        if (config.englishOrderingQuestion != null)
        {
            englishOrderingTitle = string.IsNullOrWhiteSpace(config.englishOrderingTitle)
                ? englishOrderingTitle
                : config.englishOrderingTitle;

            englishOrderingQuestion = new EnglishOrderingQuestionDefinition
            {
                prompt = string.IsNullOrWhiteSpace(config.englishOrderingQuestion.prompt)
                    ? englishOrderingQuestion.prompt
                    : config.englishOrderingQuestion.prompt,
                shuffledWords = config.englishOrderingQuestion.shuffledWords != null
                    ? (string[])config.englishOrderingQuestion.shuffledWords.Clone()
                    : Array.Empty<string>(),
                correctOrder = config.englishOrderingQuestion.correctOrder != null
                    ? (string[])config.englishOrderingQuestion.correctOrder.Clone()
                    : Array.Empty<string>(),
                answerSentence = config.englishOrderingQuestion.answerSentence ?? string.Empty
            };
        }

        if (config.englishTrueFalseQuestion != null)
        {
            englishTrueFalseTitle = string.IsNullOrWhiteSpace(config.englishTrueFalseTitle)
                ? englishTrueFalseTitle
                : config.englishTrueFalseTitle;

            englishTrueFalseQuestion = new EnglishTrueFalseQuestionDefinition
            {
                prompt = string.IsNullOrWhiteSpace(config.englishTrueFalseQuestion.prompt)
                    ? englishTrueFalseQuestion.prompt
                    : config.englishTrueFalseQuestion.prompt,
                statement = config.englishTrueFalseQuestion.statement ?? string.Empty,
                correctAnswer = config.englishTrueFalseQuestion.correctAnswer,
                explanation = config.englishTrueFalseQuestion.explanation ?? string.Empty
            };
        }

        if (config.englishTrueFalseQuestions != null && config.englishTrueFalseQuestions.Count > 0)
        {
            englishTrueFalseQuestions = new List<EnglishTrueFalseQuestionDefinition>();
            for (int i = 0; i < config.englishTrueFalseQuestions.Count; i++)
            {
                var source = config.englishTrueFalseQuestions[i];
                englishTrueFalseQuestions.Add(new EnglishTrueFalseQuestionDefinition
                {
                    prompt = source.prompt ?? string.Empty,
                    statement = source.statement ?? string.Empty,
                    correctAnswer = source.correctAnswer,
                    explanation = source.explanation ?? string.Empty
                });
            }
        }

        if (config.englishListeningQuestion != null)
        {
            englishListeningTitle = string.IsNullOrWhiteSpace(config.englishListeningTitle)
                ? englishListeningTitle
                : config.englishListeningTitle;

            englishListeningQuestion = new EnglishListeningBlankQuestionDefinition
            {
                prompt = string.IsNullOrWhiteSpace(config.englishListeningQuestion.prompt)
                    ? englishListeningQuestion.prompt
                    : config.englishListeningQuestion.prompt,
                sentenceWithBlank = config.englishListeningQuestion.sentenceWithBlank ?? string.Empty,
                voiceClip = config.englishListeningQuestion.voiceClip,
                choices = config.englishListeningQuestion.choices != null
                    ? (string[])config.englishListeningQuestion.choices.Clone()
                    : Array.Empty<string>(),
                correctChoiceIndex = config.englishListeningQuestion.correctChoiceIndex,
                completedSentence = config.englishListeningQuestion.completedSentence ?? string.Empty
            };
        }

        englishListeningQuestions = new List<EnglishListeningBlankQuestionDefinition>();
        if (config.englishListeningQuestions != null && config.englishListeningQuestions.Count > 0)
        {
            for (int i = 0; i < config.englishListeningQuestions.Count; i++)
            {
                var source = config.englishListeningQuestions[i];
                if (source == null)
                    continue;

                englishListeningQuestions.Add(new EnglishListeningBlankQuestionDefinition
                {
                    prompt = string.IsNullOrWhiteSpace(source.prompt)
                        ? "Listen to the audio and choose the word that best fits the blank."
                        : source.prompt,
                    sentenceWithBlank = source.sentenceWithBlank ?? string.Empty,
                    voiceClip = source.voiceClip,
                    choices = source.choices != null ? (string[])source.choices.Clone() : Array.Empty<string>(),
                    correctChoiceIndex = source.correctChoiceIndex,
                    completedSentence = source.completedSentence ?? string.Empty
                });
            }
        }

        if (englishListeningQuestions.Count == 0 && englishListeningQuestion != null)
            englishListeningQuestions.Add(englishListeningQuestion);

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

    private static string[] MergeSupportedFlowIds(string[] current, string[] fromConfig)
    {
        var merged = new List<string>();

        void AddRange(string[] source)
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Length; i++)
            {
                string id = source[i];
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                bool exists = false;
                for (int j = 0; j < merged.Count; j++)
                {
                    if (string.Equals(merged[j], id, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    merged.Add(id);
            }
        }

        AddRange(current);
        AddRange(fromConfig);
        return merged.ToArray();
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
