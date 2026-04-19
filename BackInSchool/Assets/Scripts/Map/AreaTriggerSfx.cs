using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class AreaTriggerSfx : MonoBehaviour
{
    private enum PlaybackMode
    {
        PlayOnceOnEnter,
        LoopWhileInside,
    }

    private enum VolumeCategory
    {
        Sfx,
        Bgm,
    }

    [Header("Trigger")]
    [SerializeField] private Collider2D triggerZone;
    [SerializeField] private bool onlyPlayer = true;
    [SerializeField] private string requiredTag = "Player";

    [Header("Playback")]
    [SerializeField] private PlaybackMode playbackMode = PlaybackMode.PlayOnceOnEnter;
    [SerializeField] private VolumeCategory volumeCategory = VolumeCategory.Sfx;
    [SerializeField] private AudioClip enterClip;
    [SerializeField] private AudioClip loopClip;
    [SerializeField] private AudioClip exitClip;
    [SerializeField] [Range(0f, 1f)] private float volume = 1f;
    [SerializeField] [Range(-3f, 3f)] private float pitch = 1f;
    [SerializeField] private bool replayOnReenter = true;

    private AudioSource audioSource;
    private bool isInside;
    private bool hasPlayedEnter;

    private void Reset()
    {
        triggerZone = GetComponent<Collider2D>();
        if (triggerZone != null)
            triggerZone.isTrigger = true;
    }

    private void Awake()
    {
        if (triggerZone == null)
            triggerZone = GetComponent<Collider2D>();

        if (triggerZone != null)
            triggerZone.isTrigger = true;

        EnsureAudioSource();
    }

    private void OnDisable()
    {
        isInside = false;
        StopLoop();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsValidTarget(other))
            return;

        isInside = true;

        if (playbackMode == PlaybackMode.PlayOnceOnEnter)
        {
            if (replayOnReenter || !hasPlayedEnter)
            {
                PlayOneShot(enterClip);
                hasPlayedEnter = true;
            }

            return;
        }

        StartLoop();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsValidTarget(other))
            return;

        isInside = false;

        if (playbackMode == PlaybackMode.LoopWhileInside)
            StopLoop();

        PlayOneShot(exitClip);
    }

    private void Update()
    {
        if (audioSource == null)
            return;

        if (playbackMode == PlaybackMode.LoopWhileInside && audioSource.isPlaying)
            audioSource.volume = GetScaledVolume();
    }

    private bool IsValidTarget(Collider2D other)
    {
        if (other == null)
            return false;

        if (!onlyPlayer)
            return string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag);

        return other.CompareTag(string.IsNullOrEmpty(requiredTag) ? "Player" : requiredTag);
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
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
    }

    private void StartLoop()
    {
        EnsureAudioSource();
        if (audioSource == null || loopClip == null)
            return;

        if (audioSource.clip != loopClip)
            audioSource.clip = loopClip;

        audioSource.pitch = pitch;
        audioSource.loop = true;
        audioSource.volume = GetScaledVolume();

        if (!audioSource.isPlaying)
            audioSource.Play();
    }

    private void StopLoop()
    {
        if (audioSource == null)
            return;

        if (audioSource.isPlaying)
            audioSource.Stop();

        audioSource.loop = false;
        audioSource.clip = null;
    }

    private void PlayOneShot(AudioClip clip)
    {
        EnsureAudioSource();
        if (audioSource == null || clip == null)
            return;

        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, GetScaledVolume());
    }

    private float GetScaledVolume()
    {
        float clamped = Mathf.Clamp01(volume);
        return volumeCategory == VolumeCategory.Bgm
            ? AudioSettingsService.ScaleBgm(clamped)
            : AudioSettingsService.ScaleSfx(clamped);
    }
}
