using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MinigameTutorialOverlay : MonoBehaviour
{
    private static TMP_FontAsset cachedFont;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI progressText;

    public bool IsVisible => canvas != null && canvas.enabled;

    public static MinigameTutorialOverlay Ensure(Transform parent)
    {
        if (parent == null)
            return null;

        MinigameTutorialOverlay overlay = parent.GetComponentInChildren<MinigameTutorialOverlay>(true);
        if (overlay != null)
            return overlay;

        var go = new GameObject("MinigameTutorialOverlay");
        go.transform.SetParent(parent, false);
        overlay = go.AddComponent<MinigameTutorialOverlay>();
        overlay.BuildRuntimeUi();
        return overlay;
    }

    public void Show(string title, string body, string progress)
    {
        if (canvas == null)
            BuildRuntimeUi();

        if (titleText != null)
            titleText.text = title ?? string.Empty;
        if (bodyText != null)
            bodyText.text = body ?? string.Empty;
        if (progressText != null)
            progressText.text = progress ?? string.Empty;

        canvas.enabled = true;
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    public void Hide()
    {
        if (canvas != null)
            canvas.enabled = false;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void Awake()
    {
        BuildRuntimeUi();
        Hide();
    }

    private void BuildRuntimeUi()
    {
        if (canvas != null)
            return;

        canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;

        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        RectTransform rootRect = transform as RectTransform;
        if (rootRect != null)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }

        RectTransform panelRect = CreateRect("Panel", transform);
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -42f);
        panelRect.sizeDelta = new Vector2(860f, 176f);

        Image panelImage = panelRect.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.06f, 0.09f, 0.88f);
        panelImage.raycastTarget = false;

        titleText = CreateLabel("Title", panelRect, 34f, FontStyles.Bold, TextAlignmentOptions.Center);
        titleText.rectTransform.anchorMin = new Vector2(0.06f, 0.68f);
        titleText.rectTransform.anchorMax = new Vector2(0.94f, 0.92f);
        titleText.color = Color.white;

        bodyText = CreateLabel("Body", panelRect, 28f, FontStyles.Normal, TextAlignmentOptions.Center);
        bodyText.rectTransform.anchorMin = new Vector2(0.08f, 0.18f);
        bodyText.rectTransform.anchorMax = new Vector2(0.92f, 0.72f);
        bodyText.color = new Color(0.96f, 0.97f, 1f, 1f);
        bodyText.enableWordWrapping = true;
        bodyText.overflowMode = TextOverflowModes.Ellipsis;

        progressText = CreateLabel("Progress", panelRect, 22f, FontStyles.Normal, TextAlignmentOptions.Center);
        progressText.rectTransform.anchorMin = new Vector2(0.08f, 0.02f);
        progressText.rectTransform.anchorMax = new Vector2(0.92f, 0.18f);
        progressText.color = new Color(0.78f, 0.83f, 0.92f, 1f);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static TextMeshProUGUI CreateLabel(string name, RectTransform parent, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = ResolveFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        return text;
    }

    private static TMP_FontAsset ResolveFont()
    {
        if (cachedFont != null)
            return cachedFont;

        TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < loadedFonts.Length; i++)
        {
            TMP_FontAsset candidate = loadedFonts[i];
            if (candidate == null || string.IsNullOrEmpty(candidate.name))
                continue;

            if (candidate.name.Equals("Galmuri11-Bold SDF", StringComparison.OrdinalIgnoreCase) ||
                candidate.name.IndexOf("Galmuri11-Bold", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                cachedFont = candidate;
                return cachedFont;
            }
        }

        cachedFont = TMP_Settings.defaultFontAsset;
        return cachedFont;
    }
}
