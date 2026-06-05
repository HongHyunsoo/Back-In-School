using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Global runtime screen fader.
/// - FadeOut before scene load
/// - Optional FadeIn automatically on next scene loaded
/// </summary>
public class SceneTransitionFader : MonoBehaviour
{
    public static SceneTransitionFader Instance { get; private set; }
    public const float DefaultFadeOutDuration = 0.3f;
    public const float DefaultFadeInDuration = 0.25f;

    private Canvas canvas;
    private Image fadeImage;
    private TextMeshProUGUI messageText;
    private Coroutine running;

    private bool fadeInOnNextScene;
    private float nextSceneFadeInDuration = 0.35f;
    private bool transitionAudioMuted;
    private bool audioListenerWasPaused;
    private float audioListenerVolumeBeforeMute = 1f;

    public static SceneTransitionFader EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        var go = new GameObject("__SceneTransitionFader");
        Instance = go.AddComponent<SceneTransitionFader>();
        DontDestroyOnLoad(go);
        return Instance;
    }

    public static void LoadSceneWithFade(string sceneName, float fadeOutDuration = DefaultFadeOutDuration, float fadeInDuration = DefaultFadeInDuration)
    {
        var fader = EnsureInstance();
        if (fader == null)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        fader.StartCoroutine(fader.CoLoadScene(sceneName, fadeOutDuration, fadeInDuration));
    }

    public static IEnumerator LoadSceneWithFadeRoutine(string sceneName, float fadeOutDuration = DefaultFadeOutDuration, float fadeInDuration = DefaultFadeInDuration)
    {
        var fader = EnsureInstance();
        if (fader == null)
        {
            SceneManager.LoadScene(sceneName);
            yield break;
        }

        yield return fader.CoLoadScene(sceneName, fadeOutDuration, fadeInDuration);
    }

    public static void LoadSceneWithMessageThenFade(
        string sceneName,
        string message,
        float fadeOutDuration = 0.8f,
        float messageHoldDuration = 1.4f,
        float fadeInDuration = DefaultFadeInDuration)
    {
        var fader = EnsureInstance();
        if (fader == null)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        fader.StartCoroutine(fader.CoLoadSceneWithMessage(sceneName, message, fadeOutDuration, messageHoldDuration, fadeInDuration));
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildOverlay();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        RestoreTransitionAudio();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void BuildOverlay()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        canvas.pixelPerfect = false;

        gameObject.AddComponent<GraphicRaycaster>();

        var imageGO = new GameObject("FadeImage", typeof(RectTransform), typeof(Image));
        imageGO.transform.SetParent(transform, false);

        var rect = imageGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fadeImage = imageGO.GetComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = false;
        fadeImage.enabled = false;

        var messageGO = new GameObject("MessageText", typeof(RectTransform), typeof(TextMeshProUGUI));
        messageGO.transform.SetParent(transform, false);

        var messageRect = messageGO.GetComponent<RectTransform>();
        messageRect.anchorMin = Vector2.zero;
        messageRect.anchorMax = Vector2.one;
        messageRect.offsetMin = Vector2.zero;
        messageRect.offsetMax = Vector2.zero;

        messageText = messageGO.GetComponent<TextMeshProUGUI>();
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.fontSize = 46f;
        messageText.font = ResolveMessageFont();
        messageText.color = new Color(1f, 1f, 1f, 0f);
        messageText.raycastTarget = false;
        messageText.enabled = false;

        canvas.enabled = false;
    }

    public void PrepareFadeInOnNextScene(float duration)
    {
        fadeInOnNextScene = true;
        nextSceneFadeInDuration = Mathf.Max(0.01f, duration);
    }

    public IEnumerator FadeOut(float duration)
    {
        MuteTransitionAudio();

        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        yield return Fade(0f, 1f, duration);
    }

    public IEnumerator FadeIn(float duration)
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        yield return Fade(1f, 0f, duration);
        RestoreTransitionAudio();
    }

    private void MuteTransitionAudio()
    {
        if (transitionAudioMuted)
            return;

        audioListenerWasPaused = AudioListener.pause;
        audioListenerVolumeBeforeMute = AudioListener.volume;
        AudioListener.pause = true;
        if (!HasTransitionSurvivorAudioPlaying())
            AudioListener.volume = 0f;
        StopTransientAudioSources();
        transitionAudioMuted = true;
    }

    private void RestoreTransitionAudio()
    {
        if (!transitionAudioMuted)
            return;

        StopTransientAudioSources();
        AudioListener.pause = audioListenerWasPaused;
        AudioListener.volume = Mathf.Clamp01(audioListenerVolumeBeforeMute);
        transitionAudioMuted = false;
    }

    private static void StopTransientAudioSources()
    {
        AudioSource[] sources = FindObjectsByType<AudioSource>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null || source.loop || ShouldSurviveTransitionMute(source))
                continue;

            source.Stop();
        }
    }

    private static bool HasTransitionSurvivorAudioPlaying()
    {
        AudioSource[] sources = FindObjectsByType<AudioSource>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source != null && source.isPlaying && ShouldSurviveTransitionMute(source))
                return true;
        }

        return false;
    }

    private static bool ShouldSurviveTransitionMute(AudioSource source)
    {
        return source != null && source.GetComponent<LunchFreeTimeTimerController>() != null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeImage == null)
            yield break;

        if (canvas != null)
            canvas.enabled = true;
        fadeImage.enabled = true;

        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            float a = Mathf.Lerp(from, to, k);
            var c = fadeImage.color;
            c.a = a;
            fadeImage.color = c;
            yield return null;
        }

        var final = fadeImage.color;
        final.a = to;
        fadeImage.color = final;

        if (to <= 0.001f)
        {
            fadeImage.enabled = false;
            if (canvas != null)
                canvas.enabled = false;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HideMessage();

        if (!fadeInOnNextScene)
            return;

        fadeInOnNextScene = false;

        if (running != null)
            StopCoroutine(running);

        running = StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        yield return FadeIn(nextSceneFadeInDuration);
        running = null;
    }

    private IEnumerator CoLoadScene(string sceneName, float fadeOutDuration, float fadeInDuration)
    {
        PrepareFadeInOnNextScene(fadeInDuration);
        yield return FadeOut(fadeOutDuration);
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator CoLoadSceneWithMessage(
        string sceneName,
        string message,
        float fadeOutDuration,
        float messageHoldDuration,
        float fadeInDuration)
    {
        PrepareFadeInOnNextScene(fadeInDuration);
        yield return FadeOut(fadeOutDuration);
        ShowMessage(message);

        float hold = Mathf.Max(0f, messageHoldDuration);
        if (hold > 0f)
            yield return new WaitForSecondsRealtime(hold);

        SceneManager.LoadScene(sceneName);
    }

    private void ShowMessage(string message)
    {
        if (canvas != null)
            canvas.enabled = true;

        if (fadeImage != null)
        {
            fadeImage.enabled = true;
            fadeImage.color = new Color(0f, 0f, 0f, 1f);
        }

        if (messageText == null)
            return;

        messageText.text = message ?? string.Empty;
        messageText.color = new Color(1f, 1f, 1f, 1f);
        messageText.enabled = true;
    }

    private void HideMessage()
    {
        if (messageText == null)
            return;

        messageText.enabled = false;
        messageText.text = string.Empty;
        messageText.color = new Color(1f, 1f, 1f, 0f);
    }

    private static TMP_FontAsset ResolveMessageFont()
    {
        TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < loadedFonts.Length; i++)
        {
            TMP_FontAsset candidate = loadedFonts[i];
            if (candidate == null)
                continue;

            string name = candidate.name;
            if (name.Equals("Galmuri11-Bold SDF", System.StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("Galmuri11-Bold", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.Equals("Galmuri11 SDF", System.StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("Galmuri11", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.Equals("DungGeunMo SDF", System.StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("DungGeunMo", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return candidate;
            }
        }

        return TMP_Settings.defaultFontAsset;
    }
}
