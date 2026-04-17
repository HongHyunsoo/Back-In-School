using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ArrivalSpaceMashMinigameController : MonoBehaviour
{
    [Header("Flow")]
    public string[] supportedFlowPrefixes = new[] { "ARRIVAL_SPACE_" };
    public int penaltyOnGiveUp = 0;
    public float completeDelaySeconds = 0.35f;

    [Header("Progress")]
    [Range(0.4f, 0.95f)] public float easyPhaseCap = 0.7f;
    [Range(0.01f, 0.25f)] public float easyPhaseGainPerPress = 0.045f;
    [Range(0.005f, 0.15f)] public float finalPhaseGainPerPress = 0.016f;
    [Range(0.05f, 0.4f)] public float rapidPressWindow = 0.11f;
    [Range(0.05f, 2f)] public float finalPhaseFallbackSpeed = 1.35f;

    [Header("Scene Layout")]
    public bool useSceneLayout = true;
    public Transform sceneLayoutRoot;
    public SpriteRenderer backgroundRenderer;
    public SpriteRenderer gaugeRenderer;
    public SpriteRenderer gaugeFillRenderer;
    public SpriteRenderer characterRenderer;
    [Tooltip("Optional source renderer used only for the 80%+ expression swap.")]
    public SpriteRenderer finalPhaseCharacterSource;
    [Tooltip("Optional direct sprite override used for the 80%+ expression swap.")]
    public Sprite finalPhaseCharacterSprite;
    [Tooltip("Manual local offset for the yellow path fill.")]
    public Vector2 gaugeFillOffset = new Vector2(0f, 1.0f);
    public Transform characterStartPoint;
    public Transform characterEndPoint;
    [Tooltip("Optional curve path points in scene order. If 2 or more are assigned, the character moves along this curve instead of a straight line.")]
    public Transform[] characterPathPoints;
    public TMP_Text percentLabel;
    public TMP_Text phaseLabel;
    public TMP_Text guideLabel;
    public float fallbackSceneCharacterTravelX = 8f;

    [Header("Runtime UI")]
    public TMP_FontAsset uiFontAsset;
    public Color dimColor = new Color(0.07f, 0.12f, 0.22f, 0.58f);
    public Color panelColor = new Color(0.98f, 0.97f, 0.92f, 1f);
    public Color accentColor = new Color(0.14f, 0.23f, 0.40f, 1f);
    public Color barBackColor = new Color(0.79f, 0.82f, 0.88f, 1f);
    public Color barFillColor = new Color(0.23f, 0.48f, 0.86f, 1f);
    public Color barDangerColor = new Color(0.96f, 0.46f, 0.20f, 1f);
    public Vector2 characterSize = new Vector2(120f, 120f);
    [Range(0f, 1f)] public float characterStartX = 0.08f;
    [Range(0f, 1f)] public float characterEndX = 0.92f;
    [Range(0f, 1f)] public float characterY = 0.18f;

    private Canvas uiCanvas;
    private TextMeshProUGUI runtimePercentText;
    private TextMeshProUGUI runtimePhaseText;
    private TextMeshProUGUI runtimeGuideText;
    private Image runtimeProgressFill;
    private RectTransform runtimeCharacterRect;

    private bool usingSceneLayout;
    private float progress;
    private float lastSpacePressedTime = -999f;
    private bool ended;
    private bool advancing;
    private Coroutine completeRoutine;
    private Coroutine preloadRoutine;
    private AsyncOperation pendingFreeroamLoad;
    private bool useAsFreeroamLoadingScreen;

    private Sprite sceneBackgroundSprite;
    private Sprite sceneGaugeSprite;
    private Sprite sceneGaugeFillSprite;
    private Sprite sceneCharacterSprite;
    private Sprite sceneFinalCharacterSprite;
    private Vector3 sceneCharacterStartPosition;
    private Vector3 sceneCharacterEndPosition;
    private Vector3[] sceneCharacterPathPositions;
    private bool sceneCharacterPathReady;
    private Vector3 gaugeFillOriginalLocalPosition;
    private Vector3 gaugeFillAlignedLocalPosition;
    private Vector3 gaugeFillOriginalLocalScale;
    private Vector2 gaugeFillOriginalSize;
    private float gaugeFillOriginalWidth;
    private float gaugeFillOriginalHeight;
    private readonly System.Collections.Generic.Dictionary<int, Sprite> gaugeFillSpriteCache = new System.Collections.Generic.Dictionary<int, Sprite>();
    private int lastGaugeFillPixelWidth = -1;

    private void Awake()
    {
        if (!ShouldRunForCurrentFlow())
        {
            enabled = false;
            return;
        }

        EnsureUIFont();
        EnsureEventSystem();
        usingSceneLayout = TryInitializeSceneLayout();
        if (!usingSceneLayout)
            BuildRuntimeUI();

        RefreshUI();
        TryBeginFreeroamPreload();
    }

    private void OnDisable()
    {
        CleanupRuntimeObjects();
    }

    private void OnDestroy()
    {
        if (gaugeFillSpriteCache.Count > 0)
        {
            foreach (var entry in gaugeFillSpriteCache)
            {
                if (entry.Value != null && entry.Value != sceneGaugeFillSprite)
                    Destroy(entry.Value);
            }

            gaugeFillSpriteCache.Clear();
        }

        CleanupRuntimeObjects();
    }

    private void Update()
    {
        if (ended)
            return;

        TickFinalPhaseFallback();

        if (Input.GetKeyDown(KeyCode.Space))
            HandleSpacePressed();

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

        if (supportedFlowPrefixes == null || supportedFlowPrefixes.Length == 0)
            return flowId.StartsWith("ARRIVAL_SPACE_", System.StringComparison.OrdinalIgnoreCase);

        for (int i = 0; i < supportedFlowPrefixes.Length; i++)
        {
            string prefix = supportedFlowPrefixes[i];
            if (!string.IsNullOrEmpty(prefix) && flowId.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool TryInitializeSceneLayout()
    {
        if (!useSceneLayout)
            return false;

        Transform root = sceneLayoutRoot != null ? sceneLayoutRoot : transform;
        if (root == null)
            return false;

        AutoBindSceneReferences(root);

        if (backgroundRenderer == null || gaugeFillRenderer == null || characterRenderer == null)
            return false;

        sceneBackgroundSprite = backgroundRenderer.sprite;
        sceneGaugeSprite = gaugeRenderer != null ? gaugeRenderer.sprite : null;
        sceneGaugeFillSprite = gaugeFillRenderer.sprite;
        sceneCharacterSprite = characterRenderer.sprite;
        sceneFinalCharacterSprite = finalPhaseCharacterSprite != null
            ? finalPhaseCharacterSprite
            : finalPhaseCharacterSource != null && finalPhaseCharacterSource.sprite != null
            ? finalPhaseCharacterSource.sprite
            : sceneCharacterSprite;

        gaugeFillOriginalLocalPosition = gaugeFillRenderer.transform.localPosition;
        gaugeFillOriginalLocalScale = gaugeFillRenderer.transform.localScale;
        gaugeFillOriginalSize = gaugeFillRenderer.size;
        if (sceneGaugeFillSprite != null)
            gaugeFillOriginalSize = sceneGaugeFillSprite.bounds.size;
        gaugeFillOriginalWidth = Mathf.Max(0.0001f, gaugeFillOriginalSize.x * Mathf.Abs(gaugeFillOriginalLocalScale.x));
        gaugeFillOriginalHeight = Mathf.Max(0.0001f, gaugeFillOriginalSize.y * Mathf.Abs(gaugeFillOriginalLocalScale.y));
        gaugeFillAlignedLocalPosition = gaugeFillOriginalLocalPosition + (Vector3)gaugeFillOffset;

        PrepareGaugeFillRenderer();
        PrepareSceneCharacterPath();
        EnsureSceneLabelFallbackUI();
        return true;
    }

    private void AutoBindSceneReferences(Transform root)
    {
        if (backgroundRenderer == null)
            backgroundRenderer = FindSpriteRendererByName(root, "Background");
        if (gaugeRenderer == null)
            gaugeRenderer = FindSpriteRendererByName(root, "Gauge");
        if (gaugeFillRenderer == null)
            gaugeFillRenderer = FindSpriteRendererByName(root, "GaugeFill");
        if (characterRenderer == null)
            characterRenderer = FindSpriteRendererByName(root, "Character");
        if (finalPhaseCharacterSource == null)
            finalPhaseCharacterSource = FindSpriteRendererByName(root, "CharacterFinal");
        if (characterStartPoint == null)
            characterStartPoint = FindTransformByName(root, "CharacterStart");
        if (characterEndPoint == null)
            characterEndPoint = FindTransformByName(root, "CharacterEnd");
        if (characterPathPoints == null || characterPathPoints.Length == 0)
            characterPathPoints = FindPathPoints(root, "CharacterPath");
        if (percentLabel == null)
            percentLabel = FindTextByName(root, "PercentLabel");
        if (phaseLabel == null)
            phaseLabel = FindTextByName(root, "PhaseLabel");
        if (guideLabel == null)
            guideLabel = FindTextByName(root, "GuideLabel");
    }

    private void HandleSpacePressed()
    {
        if (advancing)
            return;

        float now = Time.unscaledTime;

        if (progress < easyPhaseCap - 0.0001f)
        {
            progress = Mathf.Min(easyPhaseCap, progress + easyPhaseGainPerPress);
        }
        else
        {
            bool isRapid = now - lastSpacePressedTime <= rapidPressWindow;
            if (!isRapid && progress > easyPhaseCap)
                progress = easyPhaseCap;

            progress = Mathf.Clamp01(progress + finalPhaseGainPerPress);
        }

        lastSpacePressedTime = now;
        RefreshUI();

        if (progress >= 1f)
        {
            if (completeRoutine != null)
                StopCoroutine(completeRoutine);
            completeRoutine = StartCoroutine(CoCompleteAfterDelay());
        }
    }

    private void TickFinalPhaseFallback()
    {
        if (progress <= easyPhaseCap || progress >= 1f)
            return;

        if (Time.unscaledTime - lastSpacePressedTime <= rapidPressWindow)
            return;

        float before = progress;
        progress = Mathf.MoveTowards(progress, easyPhaseCap, finalPhaseFallbackSpeed * Time.unscaledDeltaTime);
        if (!Mathf.Approximately(before, progress))
            RefreshUI();
    }

    private IEnumerator CoCompleteAfterDelay()
    {
        advancing = true;
        RefreshUI();
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, completeDelaySeconds));
        End(true);
    }

    private void End(bool success)
    {
        if (ended)
            return;

        ended = true;

        if (completeRoutine != null)
        {
            StopCoroutine(completeRoutine);
            completeRoutine = null;
        }

        if (preloadRoutine != null)
        {
            StopCoroutine(preloadRoutine);
            preloadRoutine = null;
        }

        CleanupRuntimeObjects();

        if (useAsFreeroamLoadingScreen && FlowManager.Instance != null)
        {
            if (FlowManager.Instance.TryPrepareNextEventWithoutSceneLoad(FlowEventType.FREEROAM, success ? 0 : penaltyOnGiveUp, out _))
            {
                if (pendingFreeroamLoad != null)
                {
                    pendingFreeroamLoad.allowSceneActivation = true;
                    pendingFreeroamLoad = null;
                }
                else
                {
                    SceneTransitionFader.LoadSceneWithFade("FREEROAM");
                }

                return;
            }
        }

        if (FlowManager.Instance != null)
        {
            FlowManager.Instance.CompleteCurrentEvent(success ? 0 : penaltyOnGiveUp);
            return;
        }

        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            gm.MinigameFinished(success);
    }

    private void TryBeginFreeroamPreload()
    {
        if (FlowManager.Instance == null)
            return;

        if (!ShouldRunForCurrentFlow())
            return;

        if (!FlowManager.Instance.TryGetNextPlayableEvent(out var nextEvent, out _))
            return;

        if (nextEvent == null || nextEvent.type != FlowEventType.FREEROAM)
            return;

        useAsFreeroamLoadingScreen = true;
        preloadRoutine = StartCoroutine(CoPreloadFreeroamScene());
    }

    private IEnumerator CoPreloadFreeroamScene()
    {
        yield return null;

        pendingFreeroamLoad = SceneManager.LoadSceneAsync("FREEROAM");
        if (pendingFreeroamLoad == null)
            yield break;

        pendingFreeroamLoad.allowSceneActivation = false;
        while (pendingFreeroamLoad.progress < 0.9f)
            yield return null;

        preloadRoutine = null;
    }

    private void BuildRuntimeUI()
    {
        var canvasGo = new GameObject("__ArrivalSpaceMashUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        uiCanvas = canvasGo.GetComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.overrideSorting = true;
        uiCanvas.sortingOrder = -10;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var root = CreateUIObject("Root", canvasGo.transform, typeof(Image));
        StretchFull(root.GetComponent<RectTransform>());
        var rootImage = root.GetComponent<Image>();
        rootImage.color = dimColor;
        rootImage.raycastTarget = false;

        var panel = CreateUIObject("Panel", root.transform, typeof(Image));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.18f, 0.12f);
        panelRect.anchorMax = new Vector2(0.82f, 0.88f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = panelColor;
        AddOutline(panel, new Color(0f, 0f, 0f, 0.38f), new Vector2(3f, -3f));
        AddShadow(panel, new Color(0f, 0f, 0f, 0.16f), new Vector2(14f, -14f));

        runtimeGuideText = CreateText("Guide", panel.transform, 28f, FontStyles.Normal);
        runtimeGuideText.alignment = TextAlignmentOptions.Center;
        runtimeGuideText.color = new Color(0.14f, 0.18f, 0.25f, 1f);
        runtimeGuideText.rectTransform.anchorMin = new Vector2(0.10f, 0.82f);
        runtimeGuideText.rectTransform.anchorMax = new Vector2(0.90f, 0.89f);
        runtimeGuideText.rectTransform.offsetMin = Vector2.zero;
        runtimeGuideText.rectTransform.offsetMax = Vector2.zero;

        var barFrame = CreateUIObject("BarFrame", panel.transform, typeof(Image));
        var barFrameRect = barFrame.GetComponent<RectTransform>();
        barFrameRect.anchorMin = new Vector2(0.10f, 0.16f);
        barFrameRect.anchorMax = new Vector2(0.90f, 0.22f);
        barFrameRect.offsetMin = Vector2.zero;
        barFrameRect.offsetMax = Vector2.zero;
        barFrame.GetComponent<Image>().color = barBackColor;
        AddOutline(barFrame, new Color(0f, 0f, 0f, 0.35f), new Vector2(2f, -2f));

        runtimeProgressFill = CreateUIObject("Fill", barFrame.transform, typeof(Image)).GetComponent<Image>();
        runtimeProgressFill.type = Image.Type.Filled;
        runtimeProgressFill.fillMethod = Image.FillMethod.Horizontal;
        runtimeProgressFill.fillOrigin = 0;
        runtimeProgressFill.fillAmount = 0f;
        runtimeProgressFill.color = barFillColor;
        StretchWithPadding(runtimeProgressFill.rectTransform, 8f, 8f);

        runtimePercentText = CreateText("Percent", panel.transform, 64f, FontStyles.Bold);
        runtimePercentText.alignment = TextAlignmentOptions.Center;
        runtimePercentText.color = accentColor;
        runtimePercentText.rectTransform.anchorMin = new Vector2(0.10f, 0.22f);
        runtimePercentText.rectTransform.anchorMax = new Vector2(0.90f, 0.29f);
        runtimePercentText.rectTransform.offsetMin = Vector2.zero;
        runtimePercentText.rectTransform.offsetMax = Vector2.zero;

        runtimePhaseText = CreateText("Phase", panel.transform, 26f, FontStyles.Bold);
        runtimePhaseText.alignment = TextAlignmentOptions.Center;
        runtimePhaseText.color = accentColor;
        runtimePhaseText.rectTransform.anchorMin = new Vector2(0.10f, 0.08f);
        runtimePhaseText.rectTransform.anchorMax = new Vector2(0.90f, 0.14f);
        runtimePhaseText.rectTransform.offsetMin = Vector2.zero;
        runtimePhaseText.rectTransform.offsetMax = Vector2.zero;

        var runtimeCharacter = CreateUIObject("Character", panel.transform, typeof(Image));
        runtimeCharacterRect = runtimeCharacter.GetComponent<RectTransform>();
        runtimeCharacterRect.anchorMin = new Vector2(characterStartX, characterY);
        runtimeCharacterRect.anchorMax = new Vector2(characterStartX, characterY);
        runtimeCharacterRect.pivot = new Vector2(0.5f, 0.5f);
        runtimeCharacterRect.sizeDelta = characterSize;
    }

    private void RefreshUI()
    {
        string guide = progress < easyPhaseCap
            ? L("MINIGAME_ARRIVAL_GUIDE_EASY", "스페이스를 눌러서 등교 게이지를 올리자.", "Press Space to build your arrival meter.")
            : L("MINIGAME_ARRIVAL_GUIDE_FINAL", "마지막 30%! 빠르게 연타하지 않으면 70%로 떨어진다.", "Final 30%! Mash fast or it falls back to 70%.");

        string phase;
        if (advancing || progress >= 1f)
            phase = L("MINIGAME_ARRIVAL_PHASE_DONE", "완료! 잠시 후 이동", "Done! Moving on...");
        else if (progress < easyPhaseCap)
            phase = L("MINIGAME_ARRIVAL_PHASE_BUILD", "0% ~ 70%: 차근차근", "0% ~ 70%: steady taps");
        else
            phase = L("MINIGAME_ARRIVAL_PHASE_MASH", "70% ~ 100%: 빠른 연타", "70% ~ 100%: rapid mash");

        if (usingSceneLayout)
            RefreshSceneLayoutUI(guide, phase);
        else
            RefreshRuntimeUI(guide, phase);
    }

    private void RefreshSceneLayoutUI(string guide, string phase)
    {
        if (backgroundRenderer != null)
            backgroundRenderer.sprite = sceneBackgroundSprite;

        if (gaugeRenderer != null && sceneGaugeSprite != null)
            gaugeRenderer.sprite = sceneGaugeSprite;

        if (gaugeFillRenderer != null)
        {
            UpdateGaugeFillSprite(Mathf.Clamp01(progress));
        }

        if (characterRenderer != null)
        {
            characterRenderer.sprite = progress >= easyPhaseCap ? sceneFinalCharacterSprite : sceneCharacterSprite;
            if (sceneCharacterPathReady)
                characterRenderer.transform.position = EvaluateSceneCharacterPosition(Mathf.Clamp01(progress));
        }

        if (percentLabel != null)
            percentLabel.text = $"{Mathf.RoundToInt(progress * 100f)}%";
        if (phaseLabel != null)
            phaseLabel.text = phase;
        if (guideLabel != null)
            guideLabel.text = guide;

        if (runtimePercentText != null)
            runtimePercentText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
        if (runtimePhaseText != null)
            runtimePhaseText.text = phase;
        if (runtimeGuideText != null)
            runtimeGuideText.text = guide;
    }

    private void RefreshRuntimeUI(string guide, string phase)
    {
        if (runtimeProgressFill != null)
        {
            runtimeProgressFill.fillAmount = Mathf.Clamp01(progress);
            runtimeProgressFill.color = progress < easyPhaseCap ? barFillColor : barDangerColor;
        }

        if (runtimePercentText != null)
            runtimePercentText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
        if (runtimePhaseText != null)
            runtimePhaseText.text = phase;
        if (runtimeGuideText != null)
            runtimeGuideText.text = guide;

        if (runtimeCharacterRect != null)
        {
            float anchorX = Mathf.Lerp(characterStartX, characterEndX, Mathf.Clamp01(progress));
            runtimeCharacterRect.anchorMin = new Vector2(anchorX, characterY);
            runtimeCharacterRect.anchorMax = new Vector2(anchorX, characterY);
            runtimeCharacterRect.anchoredPosition = Vector2.zero;
            runtimeCharacterRect.sizeDelta = characterSize;
        }
    }

    private void CleanupRuntimeObjects()
    {
        if (uiCanvas != null)
        {
            Destroy(uiCanvas.gameObject);
            uiCanvas = null;
        }
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(go);
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

        if (!LocalizationManager.Instance.TryGetLine(key, out string value))
            return fallback;

        return string.IsNullOrEmpty(value) || value == key ? fallback : value;
    }

    private void PrepareGaugeFillRenderer()
    {
        if (gaugeFillRenderer == null)
            return;

        if (gaugeRenderer != null)
        {
            gaugeFillRenderer.sortingLayerID = gaugeRenderer.sortingLayerID;
            gaugeFillRenderer.sortingOrder = gaugeRenderer.sortingOrder + 1;
        }

        gaugeFillRenderer.drawMode = SpriteDrawMode.Simple;
        gaugeFillRenderer.maskInteraction = SpriteMaskInteraction.None;
        gaugeFillRenderer.transform.localScale = gaugeFillOriginalLocalScale;
        gaugeFillRenderer.transform.localPosition = gaugeFillAlignedLocalPosition;
        gaugeFillRenderer.enabled = true;
        UpdateGaugeFillSprite(Mathf.Clamp01(progress));
    }

    private void UpdateGaugeFillSprite(float normalizedProgress)
    {
        if (gaugeFillRenderer == null || sceneGaugeFillSprite == null)
            return;

        float clamped = Mathf.Clamp01(normalizedProgress);
        int fullPixelWidth = Mathf.Max(1, Mathf.RoundToInt(sceneGaugeFillSprite.rect.width));
        int clippedPixelWidth = Mathf.RoundToInt(fullPixelWidth * clamped);

        gaugeFillRenderer.transform.localScale = gaugeFillOriginalLocalScale;
        gaugeFillRenderer.transform.localPosition = gaugeFillAlignedLocalPosition;

        if (clippedPixelWidth <= 0)
        {
            gaugeFillRenderer.enabled = false;
            lastGaugeFillPixelWidth = 0;
            return;
        }

        gaugeFillRenderer.enabled = true;
        if (lastGaugeFillPixelWidth == clippedPixelWidth)
            return;

        lastGaugeFillPixelWidth = clippedPixelWidth;

        if (!gaugeFillSpriteCache.TryGetValue(clippedPixelWidth, out Sprite cropped) || cropped == null)
        {
            Rect rect = sceneGaugeFillSprite.rect;
            rect.width = clippedPixelWidth;
            cropped = Sprite.Create(
                sceneGaugeFillSprite.texture,
                rect,
                new Vector2(0f, 0f),
                sceneGaugeFillSprite.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            gaugeFillSpriteCache[clippedPixelWidth] = cropped;
        }

        gaugeFillRenderer.sprite = cropped;
    }

    private void EnsureSceneLabelFallbackUI()
    {
        if (percentLabel != null && phaseLabel != null && guideLabel != null)
            return;
        if (uiCanvas != null)
            return;

        var canvasGo = new GameObject("__ArrivalSceneLabelUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        uiCanvas = canvasGo.GetComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.overrideSorting = true;
        uiCanvas.sortingOrder = 20;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var root = CreateUIObject("Root", canvasGo.transform, typeof(Image));
        StretchFull(root.GetComponent<RectTransform>());
        var rootImage = root.GetComponent<Image>();
        rootImage.color = Color.clear;
        rootImage.raycastTarget = false;

        if (guideLabel == null)
        {
            runtimeGuideText = CreateText("Guide", root.transform, 26f, FontStyles.Bold);
            runtimeGuideText.alignment = TextAlignmentOptions.Center;
            runtimeGuideText.color = accentColor;
            runtimeGuideText.rectTransform.anchorMin = new Vector2(0.30f, 0.80f);
            runtimeGuideText.rectTransform.anchorMax = new Vector2(0.70f, 0.86f);
            runtimeGuideText.rectTransform.offsetMin = Vector2.zero;
            runtimeGuideText.rectTransform.offsetMax = Vector2.zero;
        }

        if (percentLabel == null)
        {
            runtimePercentText = CreateText("Percent", root.transform, 54f, FontStyles.Bold);
            runtimePercentText.alignment = TextAlignmentOptions.Center;
            runtimePercentText.color = accentColor;
            runtimePercentText.rectTransform.anchorMin = new Vector2(0.38f, 0.20f);
            runtimePercentText.rectTransform.anchorMax = new Vector2(0.62f, 0.28f);
            runtimePercentText.rectTransform.offsetMin = Vector2.zero;
            runtimePercentText.rectTransform.offsetMax = Vector2.zero;
        }

        if (phaseLabel == null)
        {
            runtimePhaseText = CreateText("Phase", root.transform, 24f, FontStyles.Bold);
            runtimePhaseText.alignment = TextAlignmentOptions.Center;
            runtimePhaseText.color = accentColor;
            runtimePhaseText.rectTransform.anchorMin = new Vector2(0.34f, 0.14f);
            runtimePhaseText.rectTransform.anchorMax = new Vector2(0.66f, 0.20f);
            runtimePhaseText.rectTransform.offsetMin = Vector2.zero;
            runtimePhaseText.rectTransform.offsetMax = Vector2.zero;
        }
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles fontStyle)
    {
        var go = CreateUIObject(name, parent, typeof(TextMeshProUGUI));
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.enableWordWrapping = true;
        if (uiFontAsset != null)
            text.font = uiFontAsset;
        return text;
    }

    private static GameObject CreateUIObject(string name, Transform parent, params System.Type[] components)
    {
        var types = new System.Collections.Generic.List<System.Type> { typeof(RectTransform) };
        if (components != null)
        {
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null || components[i] == typeof(RectTransform))
                    continue;
                types.Add(components[i]);
            }
        }

        var go = new GameObject(name, types.ToArray());
        go.transform.SetParent(parent, false);
        return go;
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

    private static void AddOutline(GameObject host, Color color, Vector2 distance)
    {
        var outline = host.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    private static void AddShadow(GameObject host, Color color, Vector2 distance)
    {
        var shadow = host.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
    }

    private static SpriteRenderer FindSpriteRendererByName(Transform root, string name)
    {
        Transform target = FindTransformByName(root, name);
        return target != null ? target.GetComponent<SpriteRenderer>() : null;
    }

    private static TMP_Text FindTextByName(Transform root, string name)
    {
        Transform target = FindTransformByName(root, name);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private void PrepareSceneCharacterPath()
    {
        var positions = new System.Collections.Generic.List<Vector3>();
        if (characterPathPoints != null)
        {
            for (int i = 0; i < characterPathPoints.Length; i++)
            {
                if (characterPathPoints[i] != null)
                    positions.Add(characterPathPoints[i].position);
            }
        }

        if (positions.Count >= 2)
        {
            sceneCharacterPathPositions = positions.ToArray();
            sceneCharacterStartPosition = sceneCharacterPathPositions[0];
            sceneCharacterEndPosition = sceneCharacterPathPositions[sceneCharacterPathPositions.Length - 1];
            sceneCharacterPathReady = true;
            return;
        }

        sceneCharacterPathPositions = null;
        sceneCharacterStartPosition = characterStartPoint != null ? characterStartPoint.position : characterRenderer.transform.position;
        sceneCharacterEndPosition = characterEndPoint != null
            ? characterEndPoint.position
            : sceneCharacterStartPosition + Vector3.right * fallbackSceneCharacterTravelX;
        sceneCharacterPathReady = true;
    }

    private Vector3 EvaluateSceneCharacterPosition(float t)
    {
        if (sceneCharacterPathPositions == null || sceneCharacterPathPositions.Length < 2)
            return Vector3.Lerp(sceneCharacterStartPosition, sceneCharacterEndPosition, t);

        if (sceneCharacterPathPositions.Length == 2)
            return Vector3.Lerp(sceneCharacterPathPositions[0], sceneCharacterPathPositions[1], t);

        int segmentCount = sceneCharacterPathPositions.Length - 1;
        float scaledT = Mathf.Clamp01(t) * segmentCount;
        int segmentIndex = Mathf.Min(Mathf.FloorToInt(scaledT), segmentCount - 1);
        float localT = scaledT - segmentIndex;

        Vector3 p0 = sceneCharacterPathPositions[Mathf.Max(segmentIndex - 1, 0)];
        Vector3 p1 = sceneCharacterPathPositions[segmentIndex];
        Vector3 p2 = sceneCharacterPathPositions[segmentIndex + 1];
        Vector3 p3 = sceneCharacterPathPositions[Mathf.Min(segmentIndex + 2, sceneCharacterPathPositions.Length - 1)];

        return EvaluateCatmullRom(p0, p1, p2, p3, localT);
    }

    private static Vector3 EvaluateCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static Transform[] FindPathPoints(Transform root, string containerName)
    {
        Transform container = FindTransformByName(root, containerName);
        if (container == null || container.childCount == 0)
            return null;

        var points = new Transform[container.childCount];
        for (int i = 0; i < container.childCount; i++)
            points[i] = container.GetChild(i);
        return points;
    }

    private static Transform FindTransformByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;

        if (root.name == name)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindTransformByName(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }
}
