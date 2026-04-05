using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class SubwayAmbientAudioController : MonoBehaviour
{
    [SerializeField] private AudioClip ambientClip;
    [SerializeField] [Range(0f, 1f)] private float ambientVolume = 0.6f;
    [SerializeField] private bool requireChatScene = true;
    [SerializeField] private bool requireSubwayState = true;

    private AudioSource audioSource;
    private GameManager gameManager;

    private void OnEnable()
    {
        EnsureAudioSource();
        RefreshReferences();
        RefreshPlayback();
    }

    private void Update()
    {
        RefreshReferences();
        RefreshPlayback();
    }

    private void OnDisable()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    private void RefreshReferences()
    {
        if (gameManager == null)
            gameManager = FindAnyObjectByType<GameManager>();
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
            return;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0f;
    }

    private void RefreshPlayback()
    {
        if (audioSource == null)
            return;

        bool shouldPlay = ShouldPlayAmbient();
        if (!shouldPlay || ambientClip == null)
        {
            if (audioSource.isPlaying)
                audioSource.Stop();

            audioSource.clip = null;
            return;
        }

        if (audioSource.clip != ambientClip)
        {
            audioSource.clip = ambientClip;
            audioSource.loop = true;
        }

        audioSource.volume = AudioSettingsService.ScaleBgm(ambientVolume);
        if (!audioSource.isPlaying)
            audioSource.Play();
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
