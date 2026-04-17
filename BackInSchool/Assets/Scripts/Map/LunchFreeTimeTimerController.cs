using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LunchFreeTimeTimerController : MonoBehaviour
{
    [Header("Clock")]
    [SerializeField] private int normalStartHour = 12;
    [SerializeField] [Range(0, 59)] private int normalStartMinute = 30;
    [SerializeField] private int failedStartHour = 12;
    [SerializeField] [Range(0, 59)] private int failedStartMinute = 40;
    [SerializeField] private int endHour = 13;
    [SerializeField] [Range(0, 59)] private int endMinute = 0;
    [SerializeField] [Min(0.1f)] private float realSecondsPerGameMinute = 6f;

    [Header("Timeout Sequence")]
    [SerializeField] private bool pauseDuringDialogue = true;
    [SerializeField] private bool waitForLandingBeforeFreeze = true;
    [SerializeField] private bool freezeSceneWithTimeScale = true;
    [SerializeField] [Min(0)] private int bellWarningMinutes = 2;
    [SerializeField] private float bellLeadSeconds = 0.85f;
    [SerializeField] private float fadeOutSeconds = 0.45f;
    [SerializeField] private float fadeInSeconds = 0.35f;
    [SerializeField] private int timeoutPenaltyDelta = 0;
    [SerializeField] private AudioClip bellClip;
    [SerializeField] [Range(0f, 1f)] private float bellVolume = 1f;

    [Header("UI")]
    [SerializeField] private bool showTimerUI = true;
    [SerializeField] private GameObject uiPrefab;
    [SerializeField] private Vector2 uiAnchorMin = new Vector2(0f, 1f);
    [SerializeField] private Vector2 uiAnchorMax = new Vector2(0f, 1f);
    [SerializeField] private Vector2 uiPivot = new Vector2(0f, 1f);
    [SerializeField] private Vector2 uiAnchoredPosition = new Vector2(28f, -26f);
    [SerializeField] private Vector2 uiSize = new Vector2(260f, 88f);
    [SerializeField] private Color panelColor = new Color(0.97f, 0.94f, 0.85f, 0.96f);
    [SerializeField] private Color titleColor = new Color(0.18f, 0.23f, 0.39f, 1f);
    [SerializeField] private Color timeColor = new Color(0.16f, 0.16f, 0.16f, 1f);
    [SerializeField] private Color fillColor = new Color(0.43f, 0.75f, 0.28f, 1f);
    [SerializeField] private Color warningFillColor = new Color(0.94f, 0.52f, 0.19f, 1f);
    [SerializeField] private Color pausedFillColor = new Color(0.58f, 0.61f, 0.7f, 1f);
    [SerializeField] private Color barBackgroundColor = new Color(0.84f, 0.84f, 0.84f, 1f);
    [SerializeField] private TMP_FontAsset uiFontAsset;

    [Header("Text")]
    [SerializeField] private string titleKo = "\uC810\uC2EC\uC2DC\uAC04";
    [SerializeField] private string titleEn = "Lunch Break";
    [SerializeField] private string pausedKo = "\uB300\uD654 \uC911";
    [SerializeField] private string pausedEn = "Paused";
    [SerializeField] private string timeoutKo = "\uC810\uC2EC\uC2DC\uAC04 \uB05D";
    [SerializeField] private string timeoutEn = "Lunch Over";
    [SerializeField] private string remainingKo = "\uB0A8\uC740 \uC2DC\uAC04";
    [SerializeField] private string remainingEn = "Left";

    private GameManager gameManager;
    private DialogueManager dialogueManager;
    private PlayerController playerController;
    private AudioSource audioSource;
    private string activeFlowId = string.Empty;
    private float durationSeconds;
    private float remainingSeconds;
    private int startClockMinutes;
    private int endClockMinutes;
    private bool timerInitialized;
    private bool completionQueued;
    private bool sceneFrozen;
    private bool warningBellPlayed;
    private float cachedTimeScale = 1f;
    private float defaultAudioSourceVolume = 1f;

    private Canvas runtimeCanvas;
    private GameObject runtimeUiInstance;
    private RectTransform panelRoot;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI timeText;
    private TextMeshProUGUI statusText;
    private Image fillImage;
    private Slider fillSlider;

    private void OnEnable()
    {
        RefreshReferences();
        EnsureAudioSource();
        EnsureUi();
        RefreshState(forceReset: true);
    }

    private void OnDisable()
    {
        UnfreezeScene();
    }

    private void Update()
    {
        RefreshReferences();
        bool lunchActive = IsLunchTimerActive();
        RefreshState(forceReset: false);

        if (!lunchActive)
        {
            SetUiVisible(false);
            return;
        }

        EnsureTimerInitialized();

        bool paused = pauseDuringDialogue && dialogueManager != null && dialogueManager.IsDialogueActive;
        if (!paused && !completionQueued)
        {
            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.unscaledDeltaTime);
            TryPlayWarningBell();
            if (remainingSeconds <= 0f)
                HandleTimeout();
        }

        UpdateUi(paused);
    }

    private void RefreshReferences()
    {
        if (gameManager == null)
            gameManager = FindAnyObjectByType<GameManager>();
        if (dialogueManager == null)
            dialogueManager = FindAnyObjectByType<DialogueManager>();
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();
    }

    private void RefreshState(bool forceReset)
    {
        string flowId = FlowContext.CurrentId ?? string.Empty;
        if (!forceReset && string.Equals(activeFlowId, flowId, System.StringComparison.Ordinal))
            return;

        activeFlowId = flowId;
        timerInitialized = false;
        completionQueued = false;
        remainingSeconds = 0f;
        durationSeconds = 0f;
        startClockMinutes = 0;
        endClockMinutes = 0;
        warningBellPlayed = false;
        UnfreezeScene();
        if (playerController != null)
            playerController.ExternalInputLocked = false;
    }

    private bool IsLunchTimerActive()
    {
        if (SceneManager.GetActiveScene().name != "FREEROAM")
            return false;

        if (!FlowContext.IsLunchFreeRoam())
            return false;

        if (gameManager != null && gameManager.currentState != GameState.Lunch_FreeTime)
            return false;

        return true;
    }

    private void EnsureTimerInitialized()
    {
        if (timerInitialized)
            return;

        startClockMinutes = ResolveStartClockMinutes();
        endClockMinutes = Mathf.Max(startClockMinutes + 1, (endHour * 60) + Mathf.Clamp(endMinute, 0, 59));
        durationSeconds = Mathf.Max(1f, (endClockMinutes - startClockMinutes) * Mathf.Max(0.1f, realSecondsPerGameMinute));
        remainingSeconds = durationSeconds;
        timerInitialized = true;
    }

    private int ResolveStartClockMinutes()
    {
        int fallback = (normalStartHour * 60) + Mathf.Clamp(normalStartMinute, 0, 59);
        if (FlowManager.Instance != null)
        {
            int savedMinute = FlowManager.Instance.GetLunchFreeTimeStartMinuteForCurrentDay(normalStartMinute);
            if (savedMinute == failedStartMinute)
                return (failedStartHour * 60) + Mathf.Clamp(savedMinute, 0, 59);

            return (normalStartHour * 60) + Mathf.Clamp(savedMinute, 0, 59);
        }

        return fallback;
    }

    private void TryPlayWarningBell()
    {
        if (warningBellPlayed || bellClip == null)
            return;

        float triggerSeconds = Mathf.Max(0f, bellWarningMinutes) * Mathf.Max(0.1f, realSecondsPerGameMinute);
        if (remainingSeconds > triggerSeconds)
            return;

        warningBellPlayed = true;
        PlayBell();
    }

    private void HandleTimeout()
    {
        if (completionQueued)
            return;

        completionQueued = true;
        remainingSeconds = 0f;
        UpdateUi(paused: false, forcedStatus: Localized(timeoutKo, timeoutEn));
        StartCoroutine(CoTimeoutSequence());
    }

    public bool DebugAdvanceMinutes(int minutes)
    {
        if (minutes <= 0 || !IsLunchTimerActive())
            return false;

        EnsureTimerInitialized();

        if (completionQueued)
            return false;

        float deltaSeconds = minutes * Mathf.Max(0.1f, realSecondsPerGameMinute);
        remainingSeconds = Mathf.Max(0f, remainingSeconds - deltaSeconds);

        if (remainingSeconds <= 0f)
            HandleTimeout();
        else
            UpdateUi(paused: pauseDuringDialogue && dialogueManager != null && dialogueManager.IsDialogueActive);

        return true;
    }

    public int GetCurrentClockMinuteComponent()
    {
        EnsureTimerInitialized();
        int roundedMinutes = Mathf.RoundToInt(ResolveCurrentClockMinutes());
        int minute = roundedMinutes % 60;
        if (minute < 0)
            minute += 60;
        return minute;
    }

    private IEnumerator CoTimeoutSequence()
    {
        RefreshReferences();

        if (playerController != null)
            playerController.ExternalInputLocked = true;

        if (waitForLandingBeforeFreeze && playerController != null)
        {
            while (!playerController.IsGrounded)
                yield return null;
        }

        ClosePhoneIfOpen();
        FreezeScene();

        float bellFadeDuration = Mathf.Max(0f, fadeOutSeconds + fadeInSeconds);
        if (audioSource != null && audioSource.isPlaying && bellFadeDuration > 0f)
            StartCoroutine(FadeBellOutRealtime(bellFadeDuration));

        float bellWait = Mathf.Max(0f, bellLeadSeconds);
        if (bellWait > 0f)
            yield return new WaitForSecondsRealtime(bellWait);

        var fader = SceneTransitionFader.EnsureInstance();
        fader.PrepareFadeInOnNextScene(fadeInSeconds);
        yield return fader.FadeOut(fadeOutSeconds);

        UnfreezeScene();
        if (playerController != null)
            playerController.ExternalInputLocked = false;

        if (FlowManager.Instance != null)
            FlowManager.Instance.CompleteCurrentEvent(timeoutPenaltyDelta);
    }

    private void FreezeScene()
    {
        if (sceneFrozen)
            return;

        sceneFrozen = true;
        if (playerController != null)
        {
            playerController.ExternalInputLocked = true;
            var rb = playerController.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.velocity = Vector2.zero;
        }

        if (freezeSceneWithTimeScale)
        {
            cachedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
    }

    private void UnfreezeScene()
    {
        if (!sceneFrozen)
            return;

        sceneFrozen = false;
        if (freezeSceneWithTimeScale)
            Time.timeScale = cachedTimeScale <= 0f ? 1f : cachedTimeScale;
    }

    private void PlayBell()
    {
        EnsureAudioSource();
        if (audioSource == null || bellClip == null)
            return;

        audioSource.volume = defaultAudioSourceVolume;
        audioSource.PlayOneShot(bellClip, AudioSettingsService.ScaleSfx(bellVolume));
    }

    private void ClosePhoneIfOpen()
    {
        if (PhoneSystem.Instance != null && PhoneSystem.Instance.IsOpen)
            PhoneSystem.Instance.Close();
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
            return;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.volume = 1f;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
        defaultAudioSourceVolume = audioSource.volume;
    }

    private IEnumerator FadeBellOutRealtime(float duration)
    {
        if (audioSource == null)
            yield break;

        float startVolume = audioSource.volume;
        if (startVolume <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration && audioSource != null && audioSource.isPlaying)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            audioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        if (audioSource != null)
        {
            audioSource.volume = defaultAudioSourceVolume;
            if (audioSource.isPlaying)
                audioSource.Stop();
        }
    }

    private void EnsureUi()
    {
        if (!showTimerUI || panelRoot != null)
            return;

        if (TryCreateUiFromPrefab())
        {
            SetUiVisible(false);
            return;
        }

        var canvasGo = new GameObject("__LunchTimerCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        runtimeCanvas = canvasGo.GetComponent<Canvas>();
        runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        runtimeCanvas.sortingOrder = 5;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var raycaster = canvasGo.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        panelRoot = CreateRect("LunchTimerPanel", canvasGo.transform as RectTransform, uiAnchorMin, uiAnchorMax, uiPivot, uiAnchoredPosition, Vector2.zero, uiSize);
        var bg = panelRoot.gameObject.AddComponent<Image>();
        bg.color = panelColor;
        bg.raycastTarget = false;
        panelRoot.gameObject.AddComponent<Outline>().effectColor = new Color(0.18f, 0.23f, 0.39f, 0.55f);

        titleText = CreateLabel("Title", panelRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -10f), new Vector2(-14f, -34f), 23f, FontStyles.Bold, TextAlignmentOptions.Left, ResolveUiFont());
        titleText.color = titleColor;

        timeText = CreateLabel("Time", panelRoot, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(14f, 10f), new Vector2(-14f, 42f), 32f, FontStyles.Bold, TextAlignmentOptions.Left, ResolveUiFont());
        timeText.color = timeColor;

        statusText = CreateLabel("Status", panelRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 8f), new Vector2(-14f, 26f), 17f, FontStyles.Normal, TextAlignmentOptions.Left, ResolveUiFont());
        statusText.color = titleColor;

        var barBg = CreateRect("BarBg", panelRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(14f, 12f), new Vector2(-14f, 28f), Vector2.zero);
        var barBgImage = barBg.gameObject.AddComponent<Image>();
        barBgImage.color = barBackgroundColor;
        barBgImage.raycastTarget = false;

        var fillRect = CreateRect("BarFill", barBg, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero, Vector2.zero);
        fillImage = fillRect.gameObject.AddComponent<Image>();
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;

        SetUiVisible(false);
    }

    private bool TryCreateUiFromPrefab()
    {
        if (uiPrefab == null)
            return false;

        runtimeUiInstance = Instantiate(uiPrefab, transform);
        runtimeUiInstance.name = uiPrefab.name;

        runtimeCanvas = runtimeUiInstance.GetComponent<Canvas>();
        if (runtimeCanvas == null)
            runtimeCanvas = runtimeUiInstance.GetComponentInChildren<Canvas>(true);

        if (runtimeCanvas != null)
        {
            runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            runtimeCanvas.sortingOrder = 5;

            var scaler = runtimeCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            var raycaster = runtimeCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = false;
        }

        panelRoot = FindRectByName(runtimeUiInstance.transform, "Root");
        if (panelRoot == null)
            panelRoot = runtimeUiInstance.GetComponent<RectTransform>();

        titleText = FindTextByName(runtimeUiInstance.transform, "Title");
        if (titleText == null)
            titleText = FindTextByName(runtimeUiInstance.transform, "TItle");

        timeText = FindTextByName(runtimeUiInstance.transform, "Time");
        statusText = FindTextByName(runtimeUiInstance.transform, "Status");
        fillSlider = FindSliderByName(runtimeUiInstance.transform, "Slider");

        if (fillSlider != null)
        {
            fillSlider.interactable = false;
            fillSlider.minValue = 0f;
            fillSlider.maxValue = 1f;
            fillImage = fillSlider.fillRect != null ? fillSlider.fillRect.GetComponent<Image>() : null;
        }

        if (panelRoot == null || titleText == null || timeText == null || statusText == null || (fillSlider == null && fillImage == null))
        {
            Debug.LogWarning("[LunchFreeTimeTimerController] Lunch timer prefab is missing required references. Falling back to generated UI.", this);
            if (runtimeUiInstance != null)
                Destroy(runtimeUiInstance);

            runtimeUiInstance = null;
            runtimeCanvas = null;
            panelRoot = null;
            titleText = null;
            timeText = null;
            statusText = null;
            fillSlider = null;
            fillImage = null;
            return false;
        }

        return true;
    }

    private void UpdateUi(bool paused, string forcedStatus = null)
    {
        if (!showTimerUI || panelRoot == null)
            return;

        SetUiVisible(true);

        titleText.text = BuildTitle();
        timeText.text = FormatClock(ResolveCurrentClockMinutes());

        if (!string.IsNullOrEmpty(forcedStatus))
            statusText.text = forcedStatus;
        else if (paused)
            statusText.text = $"{Localized(pausedKo, pausedEn)}  |  {BuildScheduleText()}";
        else
            statusText.text = BuildScheduleText();

        float normalized = durationSeconds > 0.01f ? Mathf.Clamp01(remainingSeconds / durationSeconds) : 0f;
        if (fillSlider != null)
            fillSlider.SetValueWithoutNotify(normalized);

        if (fillImage != null)
        {
            if (fillSlider == null)
                fillImage.rectTransform.localScale = new Vector3(Mathf.Max(0f, normalized), 1f, 1f);

            if (!string.IsNullOrEmpty(forcedStatus))
                fillImage.color = warningFillColor;
            else if (paused)
                fillImage.color = pausedFillColor;
            else if (normalized <= 0.25f)
                fillImage.color = warningFillColor;
            else
                fillImage.color = fillColor;
        }
    }

    private string BuildTitle()
    {
        return Localized(titleKo, titleEn);
    }

    private string BuildScheduleText()
    {
        return $"{FormatClock(startClockMinutes)}~{FormatClock(endClockMinutes)}";
    }

    private float ResolveCurrentClockMinutes()
    {
        if (durationSeconds <= 0.01f)
            return startClockMinutes;

        float elapsedRatio = 1f - Mathf.Clamp01(remainingSeconds / durationSeconds);
        return Mathf.Lerp(startClockMinutes, endClockMinutes, elapsedRatio);
    }

    private void SetUiVisible(bool visible)
    {
        if (panelRoot != null && panelRoot.gameObject.activeSelf != visible)
            panelRoot.gameObject.SetActive(visible);
    }

    private string FormatClock(float clockMinutesFloat)
    {
        int totalMinutes = Mathf.Clamp(Mathf.RoundToInt(clockMinutesFloat), 0, 24 * 60);
        int hour24 = totalMinutes / 60;
        int minute = totalMinutes % 60;
        int displayHour = hour24 % 12;
        if (displayHour == 0)
            displayHour = 12;
        return $"{displayHour}:{minute:00}";
    }

    private string FormatSeconds(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = total / 60;
        int remain = total % 60;
        return $"{minutes:00}:{remain:00}";
    }

    private string Localized(string ko, string en)
    {
        if (LocalizationManager.Instance != null &&
            LocalizationManager.Instance.GetCurrentLanguage() == Language.English)
        {
            return string.IsNullOrEmpty(en) ? ko : en;
        }

        return ko;
    }

    private TMP_FontAsset ResolveUiFont()
    {
        if (uiFontAsset != null)
            return uiFontAsset;

        return TMP_Settings.defaultFontAsset;
    }

    private static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;

        if (anchorMin == anchorMax)
        {
            rect.anchoredPosition = offsetMin;
            rect.sizeDelta = sizeDelta;
        }
        else
        {
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        return rect;
    }

    private static TextMeshProUGUI CreateLabel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, float fontSize, FontStyles style, TextAlignmentOptions alignment, TMP_FontAsset fontAsset)
    {
        var rect = CreateRect(name, parent, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), offsetMin, offsetMax, Vector2.zero);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform FindRectByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (string.Equals(rect.name, targetName, System.StringComparison.OrdinalIgnoreCase))
                return rect;
        }

        return null;
    }

    private static TextMeshProUGUI FindTextByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (string.Equals(text.name, targetName, System.StringComparison.OrdinalIgnoreCase))
                return text;
        }

        return null;
    }

    private static Slider FindSliderByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        foreach (var slider in root.GetComponentsInChildren<Slider>(true))
        {
            if (string.Equals(slider.name, targetName, System.StringComparison.OrdinalIgnoreCase))
                return slider;
        }

        return null;
    }
}
