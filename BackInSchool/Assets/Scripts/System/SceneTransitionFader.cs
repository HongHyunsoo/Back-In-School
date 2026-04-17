using System.Collections;
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
    private Coroutine running;

    private bool fadeInOnNextScene;
    private float nextSceneFadeInDuration = 0.35f;

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
        canvas.enabled = false;
    }

    public void PrepareFadeInOnNextScene(float duration)
    {
        fadeInOnNextScene = true;
        nextSceneFadeInDuration = Mathf.Max(0.01f, duration);
    }

    public IEnumerator FadeOut(float duration)
    {
        yield return Fade(0f, 1f, duration);
    }

    public IEnumerator FadeIn(float duration)
    {
        yield return Fade(1f, 0f, duration);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeImage == null)
            yield break;

        if (running != null)
            StopCoroutine(running);

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
}
