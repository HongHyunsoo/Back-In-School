using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class LunchFreeTimeAudioController : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField] private bool enableMorningBeforeAssemblyAudio = true;
    [SerializeField] private bool enableLunchFreeTimeAudio = true;

    [Header("Footsteps")]
    [SerializeField] private AudioClip[] walkFootstepClips;
    [SerializeField] private AudioClip[] runFootstepClips;
    [SerializeField] [Min(0.05f)] private float walkStepInterval = 0.42f;
    [SerializeField] [Min(0.05f)] private float runStepInterval = 0.28f;
    [SerializeField] [Range(0f, 1f)] private float footstepVolume = 0.75f;
    [SerializeField] private Vector2 footstepPitchRange = new Vector2(0.96f, 1.04f);

    [Header("Movement Feedback")]
    [SerializeField] private AudioClip jumpSfx;
    [SerializeField] private AudioClip landSfx;
    [SerializeField] [Range(0f, 1f)] private float jumpSfxVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float landSfxVolume = 0.8f;
    [SerializeField] [Min(0f)] private float landSfxDelaySeconds = 0.08f;

    [Header("Ambient Zones")]
    [SerializeField] private Collider2D classroomZone;
    [SerializeField] private Collider2D[] classroomExtraZones;
    [SerializeField] private AudioClip classroomAmbientClip;
    [SerializeField] [Range(0f, 1f)] private float classroomAmbientVolume = 0.45f;
    [SerializeField] private Collider2D hallwayZone;
    [SerializeField] private Collider2D[] hallwayExtraZones;
    [SerializeField] private AudioClip hallwayAmbientClip;
    [SerializeField] [Range(0f, 1f)] private float hallwayAmbientVolume = 0.5f;
    [SerializeField] private Collider2D playgroundZone;
    [SerializeField] private Collider2D[] playgroundExtraZones;
    [SerializeField] private AudioClip playgroundAmbientClip;
    [SerializeField] [Range(0f, 1f)] private float playgroundAmbientVolume = 0.62f;
    [SerializeField] [Min(0.1f)] private float ambientFadeSpeed = 3.25f;

    [Header("Phone Notification")]
    [SerializeField] private AudioClip phoneNotificationClip;
    [SerializeField] [Range(0f, 1f)] private float phoneNotificationVolume = 1f;
    [SerializeField] private TMP_FontAsset toastFontAsset;
    [SerializeField] private string phoneNotificationToastKo = "문자가 왔습니다.";
    [SerializeField] private string phoneNotificationToastEn = "New message received.";
    [SerializeField] [Min(0.2f)] private float phoneNotificationToastSeconds = 2.2f;

    private enum AmbientZoneKind
    {
        None,
        Classroom,
        Hallway,
        Playground
    }

    private PlayerController playerController;
    private AudioSource footstepSource;
    private AudioSource ambientSource;
    private AudioSource notificationSource;
    private AudioSource movementSource;
    private ChatService subscribedChatService;
    private float nextFootstepTime;
    private AmbientZoneKind currentAmbientZone = AmbientZoneKind.None;
    private AudioClip currentAmbientClip;
    private float currentAmbientTargetVolume;
    private int lastFootstepIndex = -1;
    private Canvas toastCanvas;
    private RectTransform toastRoot;
    private TextMeshProUGUI toastText;
    private CanvasGroup toastCanvasGroup;
    private Coroutine toastRoutine;
    private bool movementStateInitialized;
    private bool wasGrounded;
    private Coroutine landSfxRoutine;

    private void OnEnable()
    {
        EnsureAudioSources();
        EnsureDefaultMovementClips();
        SubscribeChatService();
    }

    private void OnDisable()
    {
        UnsubscribeChatService();
    }

    private void Update()
    {
        RefreshReferences();
        SubscribeChatService();

        if (!IsFreeRoamAudioActive())
        {
            ResetFootsteps();
            movementStateInitialized = false;
            FadeOutAmbient();
            return;
        }

        UpdateMovementFeedback();
        UpdateFootsteps();
        UpdateAmbient();
    }

    private void RefreshReferences()
    {
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();
    }

    private void EnsureAudioSources()
    {
        if (footstepSource == null)
            footstepSource = CreateChildSource("__LunchFootsteps", loop: false);

        if (ambientSource == null)
            ambientSource = CreateChildSource("__LunchAmbient", loop: true);

        if (notificationSource == null)
        {
            notificationSource = CreateChildSource("__LunchPhoneNotification", loop: false);
            notificationSource.ignoreListenerPause = true;
        }

        if (movementSource == null)
            movementSource = CreateChildSource("__LunchMovement", loop: false);
    }

    private void EnsureDefaultMovementClips()
    {
        if (jumpSfx == null)
            jumpSfx = AudioSettingsService.LoadResourceClip("SFX/Char/Jump");
        if (landSfx == null)
            landSfx = AudioSettingsService.LoadResourceClip("SFX/Char/Land");
    }

    private void UpdateMovementFeedback()
    {
        if (playerController == null || movementSource == null)
            return;

        bool grounded = playerController.IsGroundedStable;
        if (!movementStateInitialized)
        {
            wasGrounded = grounded;
            movementStateInitialized = true;
            return;
        }

        if (wasGrounded && !grounded && playerController.VerticalVelocity > 0.1f)
            PlayMovementSfx(jumpSfx, jumpSfxVolume);
        else if (!wasGrounded && grounded && playerController.VerticalVelocity <= 0.05f)
            QueueLandSfx();

        wasGrounded = grounded;
    }

    private void PlayMovementSfx(AudioClip clip, float volume)
    {
        if (movementSource != null && clip != null)
            movementSource.PlayOneShot(clip, AudioSettingsService.ScaleSfx(volume));
    }

    private void QueueLandSfx()
    {
        if (landSfxRoutine != null)
            StopCoroutine(landSfxRoutine);

        landSfxRoutine = StartCoroutine(CoPlayLandSfx());
    }

    private System.Collections.IEnumerator CoPlayLandSfx()
    {
        float delay = Mathf.Max(0f, landSfxDelaySeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        PlayMovementSfx(landSfx, landSfxVolume);
        landSfxRoutine = null;
    }

    private AudioSource CreateChildSource(string name, bool loop)
    {
        Transform child = transform.Find(name);
        GameObject go;
        if (child != null)
        {
            go = child.gameObject;
        }
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(transform, false);
        }

        var source = go.GetComponent<AudioSource>();
        if (source == null)
            source = go.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.volume = loop ? 0f : 1f;
        return source;
    }

    private bool IsFreeRoamAudioActive()
    {
        if (SceneManager.GetActiveScene().name != "FREEROAM")
            return false;

        var gm = FindAnyObjectByType<GameManager>();

        bool morningActive = enableMorningBeforeAssemblyAudio &&
            FlowContext.IsMorningBeforeAssemblyFreeRoam() &&
            (gm == null || gm.currentState == GameState.Morning_Slippers);

        bool lunchActive = enableLunchFreeTimeAudio &&
            FlowContext.IsLunchFreeRoam() &&
            (gm == null || gm.currentState == GameState.Lunch_FreeTime);

        return morningActive || lunchActive;
    }

    private void UpdateFootsteps()
    {
        if (playerController == null || footstepSource == null)
            return;

        bool grounded = playerController.IsGrounded;
        bool moving = Mathf.Abs(playerController.HorizontalInput) > 0.01f;
        bool running = playerController.IsActivelyRunning;
        AudioClip[] sourceSet = running ? runFootstepClips : walkFootstepClips;
        if ((sourceSet == null || sourceSet.Length == 0) && running)
            sourceSet = walkFootstepClips;

        if (!grounded || !moving || sourceSet == null || sourceSet.Length == 0)
        {
            ResetFootsteps();
            return;
        }

        if (Time.time < nextFootstepTime)
            return;

        int clipIndex = PickNextFootstepIndex(sourceSet.Length);
        var clip = sourceSet[clipIndex];
        if (clip == null)
        {
            nextFootstepTime = Time.time + (running ? runStepInterval : walkStepInterval);
            return;
        }

        footstepSource.pitch = Random.Range(
            Mathf.Min(footstepPitchRange.x, footstepPitchRange.y),
            Mathf.Max(footstepPitchRange.x, footstepPitchRange.y));
        footstepSource.PlayOneShot(clip, AudioSettingsService.ScaleSfx(footstepVolume));
        nextFootstepTime = Time.time + Mathf.Max(0.05f, running ? runStepInterval : walkStepInterval);
    }

    private int PickNextFootstepIndex(int clipCount)
    {
        if (clipCount <= 1)
        {
            lastFootstepIndex = 0;
            return 0;
        }

        int nextIndex = Random.Range(0, clipCount);
        if (nextIndex == lastFootstepIndex)
            nextIndex = (nextIndex + 1) % clipCount;

        lastFootstepIndex = nextIndex;
        return nextIndex;
    }

    private void ResetFootsteps()
    {
        nextFootstepTime = 0f;
        lastFootstepIndex = -1;
    }

    private void UpdateAmbient()
    {
        if (ambientSource == null || playerController == null)
            return;

        AmbientZoneKind targetZone = ResolveCurrentAmbientZone(playerController.transform.position);
        AudioClip targetClip = GetAmbientClip(targetZone);
        float targetVolume = GetAmbientVolume(targetZone);

        if (currentAmbientZone != targetZone || currentAmbientClip != targetClip)
        {
            currentAmbientZone = targetZone;
            currentAmbientClip = targetClip;
            currentAmbientTargetVolume = Mathf.Clamp01(targetVolume);

            if (targetClip == null)
            {
                ambientSource.Stop();
                ambientSource.clip = null;
                ambientSource.volume = 0f;
            }
            else
            {
                ambientSource.clip = targetClip;
                ambientSource.loop = true;
                if (!ambientSource.isPlaying)
                    ambientSource.Play();
            }
        }
        else
        {
            currentAmbientTargetVolume = Mathf.Clamp01(targetVolume);
        }

        if (ambientSource.clip == null)
            return;

        ambientSource.volume = Mathf.MoveTowards(
            ambientSource.volume,
            AudioSettingsService.ScaleBgm(currentAmbientTargetVolume),
            Mathf.Max(0.1f, ambientFadeSpeed) * Time.deltaTime);
    }

    private void FadeOutAmbient()
    {
        if (ambientSource == null)
            return;

        ambientSource.volume = Mathf.MoveTowards(ambientSource.volume, 0f, Mathf.Max(0.1f, ambientFadeSpeed) * Time.deltaTime);
        if (ambientSource.volume <= 0.001f && ambientSource.isPlaying)
            ambientSource.Stop();

        currentAmbientZone = AmbientZoneKind.None;
        currentAmbientClip = null;
        currentAmbientTargetVolume = 0f;
    }

    private AmbientZoneKind ResolveCurrentAmbientZone(Vector3 worldPosition)
    {
        Vector2 point = worldPosition;

        if (ContainsPoint(classroomZone, classroomExtraZones, point))
            return AmbientZoneKind.Classroom;

        if (ContainsPoint(hallwayZone, hallwayExtraZones, point))
            return AmbientZoneKind.Hallway;

        if (ContainsPoint(playgroundZone, playgroundExtraZones, point))
            return AmbientZoneKind.Playground;

        return AmbientZoneKind.None;
    }

    private static bool ContainsPoint(Collider2D primaryZone, Collider2D[] extraZones, Vector2 point)
    {
        if (primaryZone != null && primaryZone.OverlapPoint(point))
            return true;

        if (extraZones == null || extraZones.Length == 0)
            return false;

        for (int i = 0; i < extraZones.Length; i++)
        {
            Collider2D zone = extraZones[i];
            if (zone != null && zone.OverlapPoint(point))
                return true;
        }

        return false;
    }

    private AudioClip GetAmbientClip(AmbientZoneKind zone)
    {
        switch (zone)
        {
            case AmbientZoneKind.Classroom:
                return classroomAmbientClip;
            case AmbientZoneKind.Hallway:
                return hallwayAmbientClip;
            case AmbientZoneKind.Playground:
                return playgroundAmbientClip;
            default:
                return null;
        }
    }

    private float GetAmbientVolume(AmbientZoneKind zone)
    {
        switch (zone)
        {
            case AmbientZoneKind.Classroom:
                return classroomAmbientVolume;
            case AmbientZoneKind.Hallway:
                return hallwayAmbientVolume;
            case AmbientZoneKind.Playground:
                return playgroundAmbientVolume;
            default:
                return 0f;
        }
    }

    private void SubscribeChatService()
    {
        var service = ChatService.Instance;
        if (service == subscribedChatService)
            return;

        UnsubscribeChatService();
        subscribedChatService = service;
        if (subscribedChatService != null)
            subscribedChatService.OnUnreadAdded += HandleUnreadAdded;
    }

    private void UnsubscribeChatService()
    {
        if (subscribedChatService == null)
            return;

        subscribedChatService.OnUnreadAdded -= HandleUnreadAdded;
        subscribedChatService = null;
    }

    private void HandleUnreadAdded(string roomId, int amount)
    {
        if (amount <= 0)
            return;

        if (!IsFreeRoamAudioActive())
            return;

        if (notificationSource != null && phoneNotificationClip != null)
            notificationSource.PlayOneShot(phoneNotificationClip, AudioSettingsService.ScaleSfx(phoneNotificationVolume));

        ShowNotificationToast();
    }

    private void ShowNotificationToast()
    {
        EnsureToastUi();
        if (toastRoot == null || toastText == null || toastCanvasGroup == null)
            return;

        toastText.text = ResolveToastText();
        toastRoot.gameObject.SetActive(true);

        if (toastRoutine != null)
            StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(CoShowToast());
    }

    private string ResolveToastText()
    {
        if (LocalizationManager.Instance == null)
            return phoneNotificationToastKo;

        return LocalizationManager.Instance.GetCurrentLanguage() == Language.Korean
            ? phoneNotificationToastKo
            : phoneNotificationToastEn;
    }

    private void EnsureToastUi()
    {
        if (toastRoot != null && toastText != null && toastCanvasGroup != null)
            return;

        if (toastCanvas == null)
        {
            Transform existing = transform.Find("__LunchToastCanvas");
            if (existing != null)
                toastCanvas = existing.GetComponent<Canvas>();
        }

        if (toastCanvas == null)
        {
            var canvasGo = new GameObject("__LunchToastCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            toastCanvas = canvasGo.GetComponent<Canvas>();
            toastCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            toastCanvas.sortingOrder = 80;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var raycaster = canvasGo.GetComponent<GraphicRaycaster>();
            raycaster.enabled = false;
        }

        if (toastRoot != null)
            return;

        toastRoot = new GameObject("PhoneNotificationToast", typeof(RectTransform), typeof(Image), typeof(CanvasGroup)).GetComponent<RectTransform>();
        toastRoot.SetParent(toastCanvas.transform, false);
        toastRoot.anchorMin = new Vector2(0.5f, 1f);
        toastRoot.anchorMax = new Vector2(0.5f, 1f);
        toastRoot.pivot = new Vector2(0.5f, 1f);
        toastRoot.anchoredPosition = new Vector2(0f, -44f);
        toastRoot.sizeDelta = new Vector2(420f, 72f);

        var bg = toastRoot.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.16f, 0.92f);
        bg.raycastTarget = false;

        toastCanvasGroup = toastRoot.GetComponent<CanvasGroup>();
        toastCanvasGroup.alpha = 0f;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(toastRoot, false);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(18f, 10f);
        labelRect.offsetMax = new Vector2(-18f, -10f);

        toastText = labelGo.GetComponent<TextMeshProUGUI>();
        toastText.font = ResolveToastFont();
        toastText.fontSize = 28f;
        toastText.alignment = TextAlignmentOptions.Midline;
        toastText.color = Color.white;
        toastText.enableWordWrapping = false;

        toastRoot.gameObject.SetActive(false);
    }

    private TMP_FontAsset ResolveToastFont()
    {
        if (toastFontAsset != null)
            return toastFontAsset;

        return TMP_Settings.defaultFontAsset;
    }

    private System.Collections.IEnumerator CoShowToast()
    {
        float fadeIn = 0.14f;
        float hold = Mathf.Max(0.2f, phoneNotificationToastSeconds);
        float fadeOut = 0.22f;

        toastCanvasGroup.alpha = 0f;
        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.unscaledDeltaTime;
            toastCanvasGroup.alpha = Mathf.Clamp01(t / fadeIn);
            yield return null;
        }

        toastCanvasGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(hold);

        t = 0f;
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            toastCanvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOut);
            yield return null;
        }

        toastCanvasGroup.alpha = 0f;
        toastRoot.gameObject.SetActive(false);
        toastRoutine = null;
    }
}
