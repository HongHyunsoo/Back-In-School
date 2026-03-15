using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CroquisMinigameController : MonoBehaviour
{
    [Header("Config (Optional)")]
    public CroquisMinigameConfig config;
    public bool overrideStageGoals;
    public bool overridePaperArea;
    public bool overrideStageSketches;
    public bool overridePromptMix;
    public bool overridePromptVisual;
    public bool overrideDragValidation;
    public bool overrideTeacherBubble;
    public bool overrideFlow;
    public bool overrideUIFont;

    [Header("Stage Goals")]
    public int[] successesPerStage = new[] { 10, 15, 20 };

    [Header("Paper Area")]
    public Vector2 paperSize = new Vector2(9.6f, 5.8f);
    public Vector2 paperCenter = Vector2.zero;
    public float paperPadding = 0.45f;
    public Color paperColor = new Color(0.97f, 0.95f, 0.90f, 1f);

    [Header("Stage Sketches (3)")]
    public Sprite[] stageSketchSprites = new Sprite[3];
    [Header("Reveal Frames - Croquis 1 (0~4)")]
    public Sprite[] stage1RevealSprites = new Sprite[5];
    [Header("Reveal Frames - Croquis 2 (5~9)")]
    public Sprite[] stage2RevealSprites = new Sprite[5];
    [Header("Reveal Frames - Croquis 3 (10~14)")]
    public Sprite[] stage3RevealSprites = new Sprite[5];
    [HideInInspector] public Sprite[] stageRevealStepSprites = new Sprite[15]; // legacy
    public Color sketchColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    [Range(0f, 0.25f)] public float stageStartAlpha = 0.02f;

    [Header("Prompt Mix")]
    [Range(0f, 1f)] public float clickPromptChance = 0.55f;

    [Header("Prompt Visual")]
    public GameObject promptVisualPrefab;
    public float markerScale = 0.45f;
    public float markerPulseAmplitude = 0.08f;
    public float markerPulseSpeed = 5f;
    public Color clickColor = new Color(0.20f, 0.85f, 0.95f, 1f);
    public Color dragColor = new Color(0.95f, 0.80f, 0.2f, 1f);
    public float dragArrowLength = 1.0f;

    [Header("Drag Validation")]
    public float requiredDragDistance = 0.65f;
    [Range(0f, 1f)] public float requiredDirectionDot = 0.75f;
    public float maxGrabDistance = 0.45f;

    [Header("Teacher Bubble")]
    public GameObject teacherBubblePrefab;
    public string teacherConversationId = "D1_CLASS1_MINIGAME";
    public string[] teacherLineKeys = new[]
    {
        "MINIGAME_CROQUIS_TEACHER_01","MINIGAME_CROQUIS_TEACHER_02","MINIGAME_CROQUIS_TEACHER_03","MINIGAME_CROQUIS_TEACHER_04","MINIGAME_CROQUIS_TEACHER_05",
        "MINIGAME_CROQUIS_TEACHER_06","MINIGAME_CROQUIS_TEACHER_07","MINIGAME_CROQUIS_TEACHER_08","MINIGAME_CROQUIS_TEACHER_09","MINIGAME_CROQUIS_TEACHER_10",
        "MINIGAME_CROQUIS_TEACHER_11","MINIGAME_CROQUIS_TEACHER_12","MINIGAME_CROQUIS_TEACHER_13","MINIGAME_CROQUIS_TEACHER_14","MINIGAME_CROQUIS_TEACHER_15",
        "MINIGAME_CROQUIS_TEACHER_16","MINIGAME_CROQUIS_TEACHER_17","MINIGAME_CROQUIS_TEACHER_18","MINIGAME_CROQUIS_TEACHER_19","MINIGAME_CROQUIS_TEACHER_20"
    };
    public float bubbleMinInterval = 5f;
    public float bubbleMaxInterval = 9f;
    public float bubbleShowDuration = 2.8f;
    public float stageCompleteDelay = 3f;

    [Header("UI Font")]
    public TMP_FontAsset uiFontAsset;

    [Header("Flow")]
    public int penaltyOnGiveUp = 1;

    private enum PromptType { Click, Drag }

    private const int RevealStepsPerStage = 5;

    private readonly string[] teacherFallbackEN = new[]
    {
        "Observe the whole pose before details.",
        "Find the action line first.",
        "Use quick lines and avoid over-rendering.",
        "Check shoulder and pelvis tilt.",
        "Mark major joints first.",
        "Think in simple volumes.",
        "Keep your stroke rhythm steady.",
        "Silhouette must read clearly.",
        "Commit to forms instead of erasing a lot.",
        "Gesture first, anatomy second.",
        "Compare angle and length constantly.",
        "Push weight and balance of the pose.",
        "Draw what you see, not what you know.",
        "Focus on big masses first.",
        "Use negative space to check accuracy.",
        "Capture movement with fewer lines.",
        "Keep proportions under control.",
        "Simplify and avoid tiny corrections.",
        "Light pressure, faster decisions.",
        "Good. Refine only key edges now."
    };

    private int stageIndex;
    private int stageSuccessCount;
    private int totalSuccessCount;
    private int totalGoalCount;
    private bool ended;

    private PromptType currentPromptType;
    private Vector2 currentPromptPos;
    private Vector2 currentDragDir;

    private bool dragTracking;
    private Vector2 dragStartWorld;

    private Camera mainCam;
    private System.Random rng;

    private GameObject paperRoot;
    private SpriteRenderer[] stageRenderers = new SpriteRenderer[3];

    private GameObject markerRoot;
    private SpriteRenderer markerRenderer;
    private LineRenderer dragLine;

    private Canvas uiCanvas;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI bubbleText;
    private Image bubbleBg;

    private float bubbleTimer;
    private float bubbleHideTimer;
    private int lastBubbleIndex = -1;
    private int nextBubbleIndex;
    private readonly System.Collections.Generic.List<DialogueLine> teacherConversationLines = new();
    private RectTransform bubbleRootRect;
    private Vector2 bubbleDefaultAnchoredPos;
    private bool stageTransitionPending;
    private RectTransform uiRootRect;

    private void Awake()
    {
        ApplyConfigIfNeeded();
        EnsureRevealSpriteSets();

        string flowId = PlayerPrefs.GetString("FLOW_ID", "");
        if (string.IsNullOrEmpty(flowId) || !flowId.StartsWith("CLASS1_"))
        {
            enabled = false;
            return;
        }

        rng = new System.Random();
        mainCam = Camera.main;
        if (mainCam == null) mainCam = FindAnyObjectByType<Camera>();

        EnsureUIFont();
        EnsureEventSystem();
        BuildPaperVisuals();
        BuildRuntimeUI();
        BuildPromptVisual();

        stageIndex = 0;
        stageSuccessCount = 0;
        totalSuccessCount = 0;
        totalGoalCount = ComputeTotalGoal();

        UpdateStageSketches();
        SpawnNextPrompt();
        ResetBubbleTimer();
        RefreshStatus();
        ReloadTeacherConversationLines();

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;

        if (ended) return;
        CleanupRuntimeOnly();
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        CleanupRuntimeOnly();
    }

    private void Update()
    {
        if (ended) return;

        if (stageTransitionPending)
        {
            TickBubble();
            return;
        }

        TickBubble();
        TickPromptPulse();
        HandleInput();

        if (Input.GetKeyDown(KeyCode.Escape))
            End(false);
    }

    private void HandleInput()
    {
        if (mainCam == null) return;

        if (currentPromptType == PromptType.Click)
        {
            if (Input.GetMouseButtonDown(0))
            {
                var world = MouseWorld();
                if (Vector2.Distance(world, currentPromptPos) <= maxGrabDistance)
                    OnPromptSuccess();
            }
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            var world = MouseWorld();
            if (Vector2.Distance(world, currentPromptPos) <= maxGrabDistance)
            {
                dragTracking = true;
                dragStartWorld = world;
            }
        }

        if (Input.GetMouseButtonUp(0) && dragTracking)
        {
            dragTracking = false;
            var end = MouseWorld();
            var delta = end - dragStartWorld;

            if (delta.magnitude < requiredDragDistance)
                return;

            var dir = delta.normalized;
            float dot = Vector2.Dot(dir, currentDragDir);
            if (dot >= requiredDirectionDot)
                OnPromptSuccess();
        }
    }

    private void OnPromptSuccess()
    {
        stageSuccessCount++;
        totalSuccessCount++;

        bool stageCompleted = stageSuccessCount >= CurrentStageGoal();
        if (stageCompleted)
        {
            ForceCurrentStageFullyRevealed();
            StartCoroutine(CoAdvanceStageAfterDelay());
            return;
        }

        UpdateStageSketches();
        SpawnNextPrompt();
        RefreshStatus();
    }

    private System.Collections.IEnumerator CoAdvanceStageAfterDelay()
    {
        stageTransitionPending = true;
        if (markerRoot != null)
            markerRoot.SetActive(false);

        ShowStageCompleteStatus();

        float wait = Mathf.Max(0f, stageCompleteDelay);
        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        if (ended)
            yield break;

        stageIndex++;
        stageSuccessCount = 0;

        if (stageIndex >= 3)
        {
            End(true);
            yield break;
        }

        stageTransitionPending = false;
        UpdateStageSketches();
        SpawnNextPrompt();
        RefreshStatus();
        if (markerRoot != null)
            markerRoot.SetActive(true);
    }

    private void SpawnNextPrompt()
    {
        currentPromptPos = RandomPointInsidePaper();

        bool pickClick = rng.NextDouble() <= clickPromptChance;
        if (pickClick)
        {
            currentPromptType = PromptType.Click;
            currentDragDir = Vector2.right;
        }
        else
        {
            if (TryPickValidDragDirection(currentPromptPos, out var dir))
            {
                currentPromptType = PromptType.Drag;
                currentDragDir = dir;
            }
            else
            {
                currentPromptType = PromptType.Click;
                currentDragDir = Vector2.right;
            }
        }

        ApplyPromptVisual();
    }

    private bool TryPickValidDragDirection(Vector2 point, out Vector2 dir)
    {
        Vector2[] candidates = new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down };
        ShuffleDirections(candidates);

        float need = requiredDragDistance + 0.25f;
        for (int i = 0; i < candidates.Length; i++)
        {
            Vector2 end = point + (candidates[i] * need);
            if (IsInsidePaper(end))
            {
                dir = candidates[i];
                return true;
            }
        }

        dir = Vector2.right;
        return false;
    }

    private void ShuffleDirections(Vector2[] arr)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }

    private Vector2 RandomPointInsidePaper()
    {
        float halfW = (paperSize.x * 0.5f) - paperPadding;
        float halfH = (paperSize.y * 0.5f) - paperPadding;

        halfW = Mathf.Max(0.2f, halfW);
        halfH = Mathf.Max(0.2f, halfH);

        float x = paperCenter.x + Mathf.Lerp(-halfW, halfW, (float)rng.NextDouble());
        float y = paperCenter.y + Mathf.Lerp(-halfH, halfH, (float)rng.NextDouble());
        return new Vector2(x, y);
    }

    private bool IsInsidePaper(Vector2 worldPos)
    {
        float halfW = (paperSize.x * 0.5f) - paperPadding;
        float halfH = (paperSize.y * 0.5f) - paperPadding;
        return worldPos.x >= paperCenter.x - halfW &&
               worldPos.x <= paperCenter.x + halfW &&
               worldPos.y >= paperCenter.y - halfH &&
               worldPos.y <= paperCenter.y + halfH;
    }

    private void BuildPaperVisuals()
    {
        paperRoot = new GameObject("CroquisPaper");
        paperRoot.transform.SetParent(transform, false);
        paperRoot.transform.position = paperCenter;

        var paperRenderer = paperRoot.AddComponent<SpriteRenderer>();
        paperRenderer.sprite = CreateSolidSprite();
        paperRenderer.drawMode = SpriteDrawMode.Sliced;
        paperRenderer.size = paperSize;
        paperRenderer.sortingOrder = 40;
        paperRenderer.color = paperColor;

        var paperCol = paperRoot.AddComponent<BoxCollider2D>();
        paperCol.size = paperSize;
        paperCol.isTrigger = true;

        for (int i = 0; i < 3; i++)
        {
            var stageGo = new GameObject($"SketchStage_{i + 1}");
            stageGo.transform.SetParent(paperRoot.transform, false);

            var sr = stageGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 41 + i;
            sr.sprite = GetInitialStageSprite(i);
            FitSpriteToPaper(sr, paperSize);
            sr.enabled = false;
            stageRenderers[i] = sr;
        }
    }

    private Sprite GetInitialStageSprite(int stage)
    {
        if (TryGetStageStepSprite(stage, 0, out var s) && s != null)
            return s;
        return GetStageSprite(stage);
    }

    private Sprite GetStageSprite(int idx)
    {
        if (stageSketchSprites != null && idx >= 0 && idx < stageSketchSprites.Length && stageSketchSprites[idx] != null)
            return stageSketchSprites[idx];
        return CreateFallbackSketchSprite();
    }

    private void UpdateStageSketches()
    {
        for (int i = 0; i < stageRenderers.Length; i++)
        {
            var sr = stageRenderers[i];
            if (sr == null) continue;

            if (i == stageIndex)
            {
                sr.enabled = true;
                int goal = CurrentStageGoal();
                int step = ComputeRevealStep(stageSuccessCount, goal);

                // step: 0..5
                // frame index must advance at thresholds:
                // goal=10 => success 2/4/6/8/10 -> frame 0/1/2/3/4
                int frameStep = Mathf.Clamp(step - 1, 0, RevealStepsPerStage - 1);

                // If stage frame set (0..4 / 5..9 / 10..14) exists, prioritize it.
                if (TryGetStageStepSprite(i, Mathf.Clamp(Mathf.Max(step, 1) - 1, 0, RevealStepsPerStage - 1), out var stepSprite))
                {
                    sr.sprite = stepSprite;
                    float alpha = step > 0 ? 1f : stageStartAlpha;
                    SetSketchAlpha(sr, alpha);
                }
                else
                {
                    sr.sprite = GetStageSprite(i);
                    float alpha = Mathf.Lerp(stageStartAlpha, 1f, step / (float)RevealStepsPerStage);
                    SetSketchAlpha(sr, alpha);
                }
            }
            else
            {
                sr.enabled = false;
                SetSketchAlpha(sr, stageStartAlpha);
            }
        }
    }

    private void ForceCurrentStageFullyRevealed()
    {
        if (stageIndex < 0 || stageIndex >= stageRenderers.Length) return;
        var sr = stageRenderers[stageIndex];
        if (sr == null) return;
        sr.enabled = true;
        if (TryGetStageStepSprite(stageIndex, RevealStepsPerStage - 1, out var doneSprite))
            sr.sprite = doneSprite;
        else
            sr.sprite = GetStageSprite(stageIndex);
        SetSketchAlpha(sr, 1f);
    }

    private bool TryGetStageStepSprite(int stage, int step, out Sprite sprite)
    {
        sprite = null;
        var arr = GetStageRevealArray(stage);
        if (arr == null || arr.Length == 0)
            return false;

        int t = Mathf.Clamp(step, 0, RevealStepsPerStage - 1);
        if (t < 0 || t >= arr.Length)
            return false;

        sprite = arr[t];
        return sprite != null;
    }

    private Sprite[] GetStageRevealArray(int stage)
    {
        int s = Mathf.Clamp(stage, 0, 2);
        if (s == 0) return stage1RevealSprites;
        if (s == 1) return stage2RevealSprites;
        return stage3RevealSprites;
    }

    private int ComputeRevealStep(int success, int goal)
    {
        if (goal <= 0) return RevealStepsPerStage;
        if (success <= 0) return 0;

        // Use floor buckets so, for goal=15, reveal ticks happen at 3/6/9/12/15.
        float bucket = goal / (float)RevealStepsPerStage;
        int step = Mathf.FloorToInt(success / Mathf.Max(0.0001f, bucket));
        return Mathf.Clamp(step, 0, RevealStepsPerStage);
    }

    private void SetSketchAlpha(SpriteRenderer sr, float alpha)
    {
        var c = sketchColor;
        c.a = Mathf.Clamp01(alpha);
        sr.color = c;
    }

    private void BuildPromptVisual()
    {
        if (promptVisualPrefab != null)
        {
            markerRoot = Instantiate(promptVisualPrefab, transform);
            markerRoot.name = "CroquisPrompt";
            markerRenderer = markerRoot.GetComponentInChildren<SpriteRenderer>(true);
            dragLine = markerRoot.GetComponentInChildren<LineRenderer>(true);
        }
        else
        {
            markerRoot = new GameObject("CroquisPrompt");
            markerRoot.transform.SetParent(transform, false);

            markerRenderer = markerRoot.AddComponent<SpriteRenderer>();
            markerRenderer.sprite = CreateSolidSprite();
            markerRenderer.sortingOrder = 60;
            markerRenderer.drawMode = SpriteDrawMode.Sliced;
            markerRenderer.size = new Vector2(0.9f, 0.9f);

            dragLine = markerRoot.AddComponent<LineRenderer>();
            dragLine.material = new Material(Shader.Find("Sprites/Default"));
            dragLine.widthMultiplier = 0.08f;
            dragLine.positionCount = 2;
            dragLine.sortingOrder = 61;
            dragLine.useWorldSpace = true;
        }

        if (dragLine != null)
        {
            dragLine.positionCount = 2;
            dragLine.useWorldSpace = true;
        }
    }

    private void ApplyPromptVisual()
    {
        if (markerRoot == null) return;

        markerRoot.transform.position = currentPromptPos;
        markerRoot.transform.localScale = Vector3.one * markerScale;

        bool isClick = currentPromptType == PromptType.Click;
        if (markerRenderer != null)
            markerRenderer.color = isClick ? clickColor : dragColor;

        if (dragLine != null)
            dragLine.enabled = !isClick;

        if (!isClick && dragLine != null)
        {
            dragLine.startColor = dragColor;
            dragLine.endColor = dragColor;
            dragLine.SetPosition(0, currentPromptPos);
            dragLine.SetPosition(1, currentPromptPos + (currentDragDir * dragArrowLength));
        }
    }

    private void TickPromptPulse()
    {
        if (markerRoot == null) return;
        float pulse = 1f + (Mathf.Sin(Time.time * markerPulseSpeed) * markerPulseAmplitude);
        markerRoot.transform.localScale = Vector3.one * markerScale * pulse;
    }

    private void BuildRuntimeUI()
    {
        var canvasGo = new GameObject("__CroquisUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        uiCanvas = canvasGo.GetComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 6200;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var root = new GameObject("Root", typeof(RectTransform));
        root.transform.SetParent(canvasGo.transform, false);
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        uiRootRect = rootRect;

        var statusGo = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusGo.transform.SetParent(root.transform, false);
        var statusRect = statusGo.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.anchoredPosition = new Vector2(0f, -20f);
        statusRect.sizeDelta = new Vector2(0f, 72f);

        statusText = statusGo.GetComponent<TextMeshProUGUI>();
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.fontSize = 34f;
        statusText.color = Color.white;
        if (uiFontAsset != null) statusText.font = uiFontAsset;

        GameObject bubbleBgGo;
        if (teacherBubblePrefab != null)
        {
            bubbleBgGo = Instantiate(teacherBubblePrefab, root.transform);
            bubbleBgGo.name = "TeacherBubble";
            var prefabRect = bubbleBgGo.GetComponent<RectTransform>();
            if (prefabRect != null)
            {
                prefabRect.anchorMin = new Vector2(0.5f, 0.5f);
                prefabRect.anchorMax = new Vector2(0.5f, 0.5f);
                prefabRect.pivot = new Vector2(0.5f, 0.5f);
                if (prefabRect.anchoredPosition == Vector2.zero)
                    prefabRect.anchoredPosition = new Vector2(0f, 0f);
            }
            bubbleRootRect = prefabRect;

            bubbleBg = bubbleBgGo.GetComponentInChildren<Image>(true);
            bubbleText = bubbleBgGo.GetComponentInChildren<TextMeshProUGUI>(true);
        }
        else
        {
            bubbleBgGo = new GameObject("TeacherBubble", typeof(RectTransform), typeof(Image));
            bubbleBgGo.transform.SetParent(root.transform, false);
            var bubbleRect = bubbleBgGo.GetComponent<RectTransform>();
            bubbleRect.anchorMin = new Vector2(0.5f, 0.5f);
            bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
            bubbleRect.pivot = new Vector2(0.5f, 0.5f);
            bubbleRect.anchoredPosition = Vector2.zero;
            bubbleRect.sizeDelta = new Vector2(1080f, 120f);
            bubbleRootRect = bubbleRect;

            bubbleBg = bubbleBgGo.GetComponent<Image>();
            bubbleBg.color = new Color(0f, 0f, 0f, 0.68f);

            var bubbleTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            bubbleTextGo.transform.SetParent(bubbleBgGo.transform, false);
            var btRect = bubbleTextGo.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = new Vector2(28f, 10f);
            btRect.offsetMax = new Vector2(-28f, -10f);

            bubbleText = bubbleTextGo.GetComponent<TextMeshProUGUI>();
            bubbleText.alignment = TextAlignmentOptions.Center;
            bubbleText.fontSize = 30f;
            bubbleText.color = Color.white;
            bubbleText.text = "";
            if (uiFontAsset != null) bubbleText.font = uiFontAsset;
        }

        if (bubbleText != null && uiFontAsset != null)
            bubbleText.font = uiFontAsset;

        if (bubbleRootRect != null)
        {
            bubbleDefaultAnchoredPos = bubbleRootRect.anchoredPosition;
            var drag = bubbleRootRect.GetComponent<CroquisBubbleDragHandle>();
            if (drag == null) drag = bubbleRootRect.gameObject.AddComponent<CroquisBubbleDragHandle>();
            drag.Initialize(bubbleRootRect, uiCanvas);
        }

        if (bubbleBg != null) bubbleBg.enabled = false;
        if (bubbleText != null) bubbleText.enabled = false;
    }

    private void RefreshStatus()
    {
        if (statusText == null) return;

        int stageNo = Mathf.Clamp(stageIndex + 1, 1, 3);
        int goal = CurrentStageGoal();
        string promptLabel = currentPromptType == PromptType.Click
            ? L("MINIGAME_CROQUIS_PROMPT_CLICK", "Click Prompt")
            : L("MINIGAME_CROQUIS_PROMPT_DRAG", "Drag Prompt");

        string format = L("MINIGAME_CROQUIS_STATUS_FMT", "Croquis {0}/3 | Success {1}/{2} | {3}");
        statusText.text = string.Format(format, stageNo, stageSuccessCount, goal, promptLabel);
    }

    private void ShowStageCompleteStatus()
    {
        if (statusText == null) return;
        int stageNo = Mathf.Clamp(stageIndex + 1, 1, 3);
        string format = L("MINIGAME_CROQUIS_STAGE_DONE_FMT", "Croquis {0} Complete!");
        statusText.text = string.Format(format, stageNo);
    }

    private void TickBubble()
    {
        if (bubbleHideTimer > 0f)
        {
            bubbleHideTimer -= Time.deltaTime;
            if (bubbleHideTimer <= 0f)
                HideBubble();
        }

        bubbleTimer -= Time.deltaTime;
        if (bubbleTimer <= 0f)
        {
            ShowRandomBubble();
            ResetBubbleTimer();
        }
    }

    private void ShowRandomBubble()
    {
        int count = GetTeacherLineCount();
        if (count <= 0) return;

        int idx = Mathf.Clamp(nextBubbleIndex, 0, count - 1);
        nextBubbleIndex = (idx + 1) % count;
        lastBubbleIndex = idx;

        string line = ResolveTeacherLineAt(idx);
        string prefix = ResolveTeacherPrefixAt(idx);

        PlaceBubbleOnPaperRandom();

        bubbleText.text = $"{prefix}: {line}";
        bubbleBg.enabled = true;
        bubbleText.enabled = true;
        bubbleHideTimer = bubbleShowDuration;
    }

    private void HideBubble()
    {
        bubbleBg.enabled = false;
        bubbleText.enabled = false;
    }

    private void PlaceBubbleOnPaperRandom()
    {
        if (bubbleRootRect == null || uiRootRect == null || mainCam == null)
        {
            if (bubbleRootRect != null)
                bubbleRootRect.anchoredPosition = bubbleDefaultAnchoredPos;
            return;
        }

        Vector2 world = RandomPointInsidePaper();
        Vector3 screen = mainCam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(uiRootRect, screen, null, out var local))
        {
            Vector2 half = bubbleRootRect.sizeDelta * 0.5f;
            Vector2 limit = (uiRootRect.rect.size * 0.5f) - half - new Vector2(24f, 24f);
            local.x = Mathf.Clamp(local.x, -Mathf.Abs(limit.x), Mathf.Abs(limit.x));
            local.y = Mathf.Clamp(local.y, -Mathf.Abs(limit.y), Mathf.Abs(limit.y));
            bubbleRootRect.anchoredPosition = local;
        }
        else
        {
            bubbleRootRect.anchoredPosition = bubbleDefaultAnchoredPos;
        }
    }

    private void ResetBubbleTimer()
    {
        float min = Mathf.Max(0.5f, bubbleMinInterval);
        float max = Mathf.Max(min + 0.1f, bubbleMaxInterval);
        bubbleTimer = UnityEngine.Random.Range(min, max);
    }

    private int CurrentStageGoal()
    {
        if (stageIndex < 0 || stageIndex >= successesPerStage.Length)
            return 1;
        return Mathf.Max(1, successesPerStage[stageIndex]);
    }

    private int ComputeTotalGoal()
    {
        int sum = 0;
        for (int i = 0; i < successesPerStage.Length; i++)
            sum += Mathf.Max(1, successesPerStage[i]);
        return sum <= 0 ? 45 : sum;
    }

    private void OnLanguageChanged(Language _)
    {
        ReloadTeacherConversationLines();
        RefreshStatus();
        if (bubbleText != null && bubbleText.enabled && lastBubbleIndex >= 0 && lastBubbleIndex < GetTeacherLineCount())
        {
            string line = ResolveTeacherLineAt(lastBubbleIndex);
            string prefix = ResolveTeacherPrefixAt(lastBubbleIndex);
            bubbleText.text = $"{prefix}: {line}";
        }
    }

    private void ReloadTeacherConversationLines()
    {
        teacherConversationLines.Clear();
        nextBubbleIndex = 0;
        lastBubbleIndex = -1;

        if (LocalizationManager.Instance == null || string.IsNullOrEmpty(teacherConversationId))
            return;

        var lines = LocalizationManager.Instance.GetConversation(teacherConversationId);
        if (lines == null || lines.Count == 0)
            return;

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i] == null || string.IsNullOrEmpty(lines[i].lineID))
                continue;
            teacherConversationLines.Add(lines[i]);
        }
    }

    private int GetTeacherLineCount()
    {
        if (teacherConversationLines.Count > 0)
            return teacherConversationLines.Count;

        return Mathf.Min(20, Mathf.Min(teacherLineKeys.Length, teacherFallbackEN.Length));
    }

    private string ResolveTeacherLineAt(int idx)
    {
        if (teacherConversationLines.Count > 0)
        {
            int safe = Mathf.Clamp(idx, 0, teacherConversationLines.Count - 1);
            var line = teacherConversationLines[safe];
            return L(line.lineID, line.lineID);
        }

        int legacySafe = Mathf.Clamp(idx, 0, Mathf.Min(teacherLineKeys.Length, teacherFallbackEN.Length) - 1);
        string key = teacherLineKeys[legacySafe];
        return L(key, teacherFallbackEN[legacySafe]);
    }

    private string ResolveTeacherPrefixAt(int idx)
    {
        if (teacherConversationLines.Count > 0)
        {
            int safe = Mathf.Clamp(idx, 0, teacherConversationLines.Count - 1);
            string speakerId = teacherConversationLines[safe].speakerID;
            if (LocalizationManager.Instance != null && !string.IsNullOrEmpty(speakerId))
            {
                string localizedName = LocalizationManager.Instance.GetName(speakerId);
                if (!string.IsNullOrEmpty(localizedName) && localizedName != speakerId)
                    return localizedName;
            }
        }

        return L("MINIGAME_CROQUIS_TEACHER_PREFIX", "Teacher");
    }

    private Vector2 MouseWorld()
    {
        var p = Input.mousePosition;
        p.z = Mathf.Abs(mainCam.transform.position.z);
        var w = mainCam.ScreenToWorldPoint(p);
        return new Vector2(w.x, w.y);
    }

    private void End(bool success)
    {
        if (ended) return;
        ended = true;

        CleanupRuntimeOnly();

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

    private void CleanupRuntimeOnly()
    {
        if (uiCanvas != null)
        {
            Destroy(uiCanvas.gameObject);
            uiCanvas = null;
        }

        if (markerRoot != null)
        {
            Destroy(markerRoot);
            markerRoot = null;
        }

        if (paperRoot != null)
        {
            Destroy(paperRoot);
            paperRoot = null;
        }
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(go);
    }

    private void FitSpriteToPaper(SpriteRenderer sr, Vector2 targetSize)
    {
        if (sr == null || sr.sprite == null) return;
        Vector2 spriteSize = sr.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;
        sr.transform.localScale = new Vector3(targetSize.x / spriteSize.x, targetSize.y / spriteSize.y, 1f);
    }

    private Sprite CreateSolidSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    private Sprite CreateFallbackSketchSprite()
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, clear);
        }

        Color ink = new Color(0f, 0f, 0f, 1f);
        DrawLine(tex, 70, 200, 128, 230, ink, 2);
        DrawLine(tex, 128, 230, 186, 200, ink, 2);
        DrawLine(tex, 70, 200, 64, 150, ink, 2);
        DrawLine(tex, 186, 200, 192, 150, ink, 2);
        DrawLine(tex, 64, 150, 192, 150, ink, 2);
        DrawLine(tex, 128, 150, 128, 82, ink, 2);
        DrawLine(tex, 128, 120, 78, 92, ink, 2);
        DrawLine(tex, 128, 120, 178, 92, ink, 2);
        DrawLine(tex, 78, 92, 58, 38, ink, 2);
        DrawLine(tex, 178, 92, 198, 38, ink, 2);
        DrawLine(tex, 64, 150, 34, 104, ink, 2);
        DrawLine(tex, 192, 150, 222, 106, ink, 2);

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color color, int thickness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            DrawDot(tex, x0, y0, thickness, color);
            if (x0 == x1 && y0 == y1) break;
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

    private void DrawDot(Texture2D tex, int cx, int cy, int radius, Color color)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if ((x * x) + (y * y) > radius * radius) continue;
                int px = cx + x;
                int py = cy + y;
                if (px < 0 || py < 0 || px >= tex.width || py >= tex.height) continue;
                tex.SetPixel(px, py, color);
            }
        }
    }

    private string L(string key, string fallback)
    {
        if (LocalizationManager.Instance == null || string.IsNullOrEmpty(key))
            return fallback;

        string value = LocalizationManager.Instance.GetLine(key);
        return value == key ? fallback : value;
    }

    private void EnsureUIFont()
    {
        if (uiFontAsset != null) return;
        #if UNITY_EDITOR
        uiFontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Fonts/Galmuri11-Bold SDF.asset");
        #endif
    }

    private void ApplyConfigIfNeeded()
    {
        if (config == null)
            return;

        if (!overrideStageGoals)
            successesPerStage = CloneOrFallback(config.successesPerStage, new[] { 10, 15, 20 });

        if (!overridePaperArea)
        {
            paperSize = config.paperSize;
            paperCenter = config.paperCenter;
            paperPadding = config.paperPadding;
            paperColor = config.paperColor;
        }

        if (!overrideStageSketches)
        {
            if (HasAnySprite(config.stageSketchSprites))
                stageSketchSprites = CloneOrFallback(config.stageSketchSprites, stageSketchSprites);

            if (HasAnySprite(config.stage1RevealSprites))
                stage1RevealSprites = CloneOrFallback(config.stage1RevealSprites, stage1RevealSprites);
            if (HasAnySprite(config.stage2RevealSprites))
                stage2RevealSprites = CloneOrFallback(config.stage2RevealSprites, stage2RevealSprites);
            if (HasAnySprite(config.stage3RevealSprites))
                stage3RevealSprites = CloneOrFallback(config.stage3RevealSprites, stage3RevealSprites);

            // Legacy migration path (single 15-array -> 3x5 arrays)
            if (HasAnySprite(config.stageRevealStepSprites))
                MigrateLegacyRevealArray(config.stageRevealStepSprites);

            sketchColor = config.sketchColor;
            stageStartAlpha = config.stageStartAlpha;
        }

        if (!overridePromptMix)
            clickPromptChance = config.clickPromptChance;

        if (!overridePromptVisual)
        {
            promptVisualPrefab = config.promptVisualPrefab;
            markerScale = config.markerScale;
            markerPulseAmplitude = config.markerPulseAmplitude;
            markerPulseSpeed = config.markerPulseSpeed;
            clickColor = config.clickColor;
            dragColor = config.dragColor;
            dragArrowLength = config.dragArrowLength;
        }

        if (!overrideDragValidation)
        {
            requiredDragDistance = config.requiredDragDistance;
            requiredDirectionDot = config.requiredDirectionDot;
            maxGrabDistance = config.maxGrabDistance;
        }

        if (!overrideTeacherBubble)
        {
            teacherBubblePrefab = config.teacherBubblePrefab;
            teacherConversationId = string.IsNullOrEmpty(config.teacherConversationId)
                ? teacherConversationId
                : config.teacherConversationId;
            teacherLineKeys = CloneOrFallback(config.teacherLineKeys, teacherLineKeys);
            bubbleMinInterval = config.bubbleMinInterval;
            bubbleMaxInterval = config.bubbleMaxInterval;
            bubbleShowDuration = config.bubbleShowDuration;
        }

        if (!overrideFlow)
            penaltyOnGiveUp = config.penaltyOnGiveUp;

        if (!overrideUIFont && config.uiFontAsset != null)
            uiFontAsset = config.uiFontAsset;
    }

    private static T[] CloneOrFallback<T>(T[] source, T[] fallback)
    {
        if (source == null || source.Length == 0)
            return fallback;

        var clone = new T[source.Length];
        for (int i = 0; i < source.Length; i++)
            clone[i] = source[i];
        return clone;
    }

    private static bool HasAnySprite(Sprite[] arr)
    {
        if (arr == null || arr.Length == 0)
            return false;

        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] != null)
                return true;
        }

        return false;
    }

    private void EnsureRevealSpriteSets()
    {
        bool groupedExists = HasAnySprite(stage1RevealSprites) || HasAnySprite(stage2RevealSprites) || HasAnySprite(stage3RevealSprites);
        if (groupedExists)
            return;

        if (HasAnySprite(stageRevealStepSprites))
            MigrateLegacyRevealArray(stageRevealStepSprites);
    }

    private void MigrateLegacyRevealArray(Sprite[] legacy)
    {
        if (legacy == null || legacy.Length == 0)
            return;

        if (stage1RevealSprites == null || stage1RevealSprites.Length != RevealStepsPerStage)
            stage1RevealSprites = new Sprite[RevealStepsPerStage];
        if (stage2RevealSprites == null || stage2RevealSprites.Length != RevealStepsPerStage)
            stage2RevealSprites = new Sprite[RevealStepsPerStage];
        if (stage3RevealSprites == null || stage3RevealSprites.Length != RevealStepsPerStage)
            stage3RevealSprites = new Sprite[RevealStepsPerStage];

        for (int i = 0; i < RevealStepsPerStage; i++)
        {
            int idx1 = i;
            int idx2 = RevealStepsPerStage + i;
            int idx3 = (RevealStepsPerStage * 2) + i;

            if (idx1 < legacy.Length && stage1RevealSprites[i] == null)
                stage1RevealSprites[i] = legacy[idx1];
            if (idx2 < legacy.Length && stage2RevealSprites[i] == null)
                stage2RevealSprites[i] = legacy[idx2];
            if (idx3 < legacy.Length && stage3RevealSprites[i] == null)
                stage3RevealSprites[i] = legacy[idx3];
        }
    }
}

public class CroquisBubbleDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private RectTransform dragTarget;
    private Canvas canvasRef;

    public void Initialize(RectTransform target, Canvas canvas)
    {
        dragTarget = target;
        canvasRef = canvas;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (dragTarget == null)
            dragTarget = transform as RectTransform;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragTarget == null)
            return;

        float scale = 1f;
        if (canvasRef != null)
            scale = Mathf.Max(0.01f, canvasRef.scaleFactor);

        dragTarget.anchoredPosition += eventData.delta / scale;
    }
}
