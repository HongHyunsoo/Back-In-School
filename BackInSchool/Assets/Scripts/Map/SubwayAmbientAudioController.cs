using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class SubwayAmbientAudioController : MonoBehaviour
{
    [SerializeField] [HideInInspector] private AudioClip ambientClip;
    [SerializeField] private AudioClip[] ambientClips;
    [SerializeField] [Range(0f, 1f)] private float ambientVolume = 0.6f;
    [SerializeField] private bool requireChatScene = true;
    [SerializeField] private bool requireSubwayState = true;

    private readonly List<AudioSource> audioSources = new();
    private readonly List<AudioClip> configuredClips = new();
    private GameManager gameManager;

    private void OnEnable()
    {
        RebuildClipCache();
        EnsureAudioSources();
        RefreshReferences();
        RefreshPlayback();
    }

    private void Update()
    {
        RebuildClipCache();
        EnsureAudioSources();
        RefreshReferences();
        RefreshPlayback();
    }

    private void OnDisable()
    {
        StopAllSources(clearClips: false);
    }

    private void RefreshReferences()
    {
        if (gameManager == null)
            gameManager = FindAnyObjectByType<GameManager>();
    }

    private void RebuildClipCache()
    {
        configuredClips.Clear();

        if (ambientClips != null)
        {
            for (int i = 0; i < ambientClips.Length; i++)
            {
                if (ambientClips[i] != null)
                    configuredClips.Add(ambientClips[i]);
            }
        }

        if (configuredClips.Count == 0 && ambientClip != null)
            configuredClips.Add(ambientClip);
    }

    private void EnsureAudioSources()
    {
        while (audioSources.Count < configuredClips.Count)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            audioSources.Add(source);
        }

        for (int i = configuredClips.Count; i < audioSources.Count; i++)
        {
            AudioSource source = audioSources[i];
            if (source == null)
                continue;

            if (source.isPlaying)
                source.Stop();

            source.clip = null;
        }
    }

    private void RefreshPlayback()
    {
        bool shouldPlay = ShouldPlayAmbient();
        if (!shouldPlay || configuredClips.Count == 0)
        {
            StopAllSources(clearClips: true);
            return;
        }

        float scaledVolume = AudioSettingsService.ScaleBgm(ambientVolume);
        for (int i = 0; i < configuredClips.Count; i++)
        {
            AudioSource source = audioSources[i];
            AudioClip clip = configuredClips[i];
            if (source == null || clip == null)
                continue;

            if (source.clip != clip)
            {
                source.clip = clip;
                source.loop = true;
            }

            source.volume = scaledVolume;
            if (!source.isPlaying)
                source.Play();
        }
    }

    private void StopAllSources(bool clearClips)
    {
        for (int i = 0; i < audioSources.Count; i++)
        {
            AudioSource source = audioSources[i];
            if (source == null)
                continue;

            if (source.isPlaying)
                source.Stop();

            if (clearClips)
                source.clip = null;
        }
    }

    private bool ShouldPlayAmbient()
    {
        if (requireChatScene && SceneManager.GetActiveScene().name != "CHAT")
            return false;

        if (requireSubwayState)
        {
            if (gameManager == null)
                return false;

            if (gameManager.currentState != GameState.Subway)
                return false;
        }

        return true;
    }
}
