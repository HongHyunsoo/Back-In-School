using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class LunchFreeTimeAudioController : MonoBehaviour
{
    [Header("Footsteps")]
    [SerializeField] private AudioClip[] walkFootstepClips;
    [SerializeField] private AudioClip[] runFootstepClips;
    [SerializeField] [Min(0.05f)] private float walkStepInterval = 0.42f;
    [SerializeField] [Min(0.05f)] private float runStepInterval = 0.28f;
    [SerializeField] [Range(0f, 1f)] private float footstepVolume = 0.75f;
    [SerializeField] private Vector2 footstepPitchRange = new Vector2(0.96f, 1.04f);

    [Header("Ambient Zones")]
    [SerializeField] private Collider2D classroomZone;
    [SerializeField] private AudioClip classroomAmbientClip;
    [SerializeField] [Range(0f, 1f)] private float classroomAmbientVolume = 0.45f;
    [SerializeField] private Collider2D hallwayZone;
    [SerializeField] private AudioClip hallwayAmbientClip;
    [SerializeField] [Range(0f, 1f)] private float hallwayAmbientVolume = 0.5f;
    [SerializeField] private Collider2D playgroundZone;
    [SerializeField] private AudioClip playgroundAmbientClip;
    [SerializeField] [Range(0f, 1f)] private float playgroundAmbientVolume = 0.62f;
    [SerializeField] [Min(0.1f)] private float ambientFadeSpeed = 3.25f;

    [Header("Phone Notification")]
    [SerializeField] private AudioClip phoneNotificationClip;
    [SerializeField] [Range(0f, 1f)] private float phoneNotificationVolume = 1f;

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
    private ChatService subscribedChatService;
    private float nextFootstepTime;
    private AmbientZoneKind currentAmbientZone = AmbientZoneKind.None;
    private AudioClip currentAmbientClip;
    private float currentAmbientTargetVolume;
    private int lastFootstepIndex = -1;

    private void OnEnable()
    {
        EnsureAudioSources();
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

        if (!IsLunchAudioActive())
        {
            ResetFootsteps();
            FadeOutAmbient();
            return;
        }

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
        source.volume = 0f;
        return source;
    }

    private bool IsLunchAudioActive()
    {
        if (SceneManager.GetActiveScene().name != "FREEROAM")
            return false;

        if (!FlowContext.IsLunchFreeRoam())
            return false;

        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null && gm.currentState != GameState.Lunch_FreeTime)
            return false;

        return true;
    }

    private void UpdateFootsteps()
    {
        if (playerController == null || footstepSource == null)
            return;

        bool grounded = playerController.IsGrounded;
        bool moving = Mathf.Abs(playerController.HorizontalInput) > 0.01f;
        bool running = playerController.IsActivelyRunning;
        AudioClip[] sourceSet = running ? runFootstepClips : walkFootstepClips;

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

        if (classroomZone != null && classroomZone.OverlapPoint(point))
            return AmbientZoneKind.Classroom;

        if (hallwayZone != null && hallwayZone.OverlapPoint(point))
            return AmbientZoneKind.Hallway;

        if (playgroundZone != null && playgroundZone.OverlapPoint(point))
            return AmbientZoneKind.Playground;

        return AmbientZoneKind.None;
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
        if (amount <= 0 || notificationSource == null || phoneNotificationClip == null)
            return;

        if (!IsLunchAudioActive())
            return;

        notificationSource.PlayOneShot(phoneNotificationClip, AudioSettingsService.ScaleSfx(phoneNotificationVolume));
    }
}
