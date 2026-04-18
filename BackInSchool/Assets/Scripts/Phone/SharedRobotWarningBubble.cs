using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SharedRobotWarningBubble : MonoBehaviour
{
    private static SharedRobotWarningBubble instance;

    private SpeechBubbleUI bubble;
    private RectTransform bubbleRoot;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI bodyText;
    private Transform targetRobot;
    private Camera worldCamera;
    private Coroutine hideRoutine;

    private const float BubbleYOffset = 120f;
    private static readonly Vector3 RobotWorldOffset = new Vector3(0f, 1.2f, 0f);

    public static void Show(
        Transform robot,
        string message,
        SpeechBubbleUI explicitTemplate = null,
        float scale = 1f,
        float duration = 1.6f)
    {
        if (robot == null || string.IsNullOrWhiteSpace(message))
            return;

        EnsureInstance();
        if (instance == null)
            return;

        instance.ShowInternal(robot, message, explicitTemplate, scale, duration);
    }

    public static void Hide()
    {
        if (instance == null || instance.bubbleRoot == null)
            return;

        if (instance.hideRoutine != null)
        {
            instance.StopCoroutine(instance.hideRoutine);
            instance.hideRoutine = null;
        }

        instance.bubbleRoot.gameObject.SetActive(false);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        instance = FindFirstObjectByType<SharedRobotWarningBubble>();
        if (instance != null)
            return;

        var go = new GameObject("__SharedRobotWarningBubble");
        instance = go.AddComponent<SharedRobotWarningBubble>();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void LateUpdate()
    {
        if (bubbleRoot == null || !bubbleRoot.gameObject.activeSelf || targetRobot == null)
            return;

        PositionBubble();
    }

    private void ShowInternal(
        Transform robot,
        string message,
        SpeechBubbleUI explicitTemplate,
        float scale,
        float duration)
    {
        targetRobot = robot;
        worldCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();

        EnsureBubble(explicitTemplate, scale);
        if (bubbleRoot == null || bodyText == null)
            return;

        if (nameText != null)
        {
            nameText.text = "Robot";
            ForceWhiteText(nameText);
        }

        bodyText.text = message;
        bodyText.alignment = TextAlignmentOptions.Center;
        bodyText.verticalAlignment = VerticalAlignmentOptions.Middle;
        ForceWhiteText(bodyText);

        PositionBubble();
        bubbleRoot.gameObject.SetActive(true);
        bubbleRoot.SetAsLastSibling();

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(CoHideAfter(duration));
    }

    private IEnumerator CoHideAfter(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        if (bubbleRoot != null)
            bubbleRoot.gameObject.SetActive(false);
        hideRoutine = null;
    }

    private void EnsureBubble(SpeechBubbleUI explicitTemplate, float scale)
    {
        var canvas = ResolveCanvas();
        if (canvas == null)
            return;

        if (bubbleRoot != null)
        {
            if (bubbleRoot.parent != canvas.transform)
                bubbleRoot.SetParent(canvas.transform, false);
            bubbleRoot.localScale = Vector3.one * Mathf.Max(0.1f, scale);
            return;
        }

        var template = ResolveTemplate(explicitTemplate);
        if (template == null)
            return;

        bubble = Instantiate(template, canvas.transform);
        bubbleRoot = bubble.transform as RectTransform;
        nameText = bubble.nameText;
        bodyText = bubble.bodyText;

        bubbleRoot.name = "SharedRobotWarningBubble";
        bubbleRoot.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        bubbleRoot.gameObject.SetActive(false);

        NormalizeBubbleLayout(bubbleRoot);
        EnsureVisibleBackground(bubbleRoot);
        if (nameText != null)
            ForceWhiteText(nameText);
        if (bodyText != null)
            ForceWhiteText(bodyText);
    }

    private void PositionBubble()
    {
        if (bubbleRoot == null || targetRobot == null)
            return;
        if (!(bubbleRoot.parent is RectTransform parentRect))
            return;

        var canvas = parentRect.GetComponentInParent<Canvas>();
        Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera
            : null;

        if (worldCamera == null)
            worldCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (worldCamera == null)
            return;

        Vector3 world = targetRobot.position + RobotWorldOffset;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(worldCamera, world);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, uiCam, out var local))
            return;

        Vector2 anchored = local + new Vector2(0f, BubbleYOffset);
        anchored.x = Mathf.Round(anchored.x);
        anchored.y = Mathf.Round(anchored.y);
        bubbleRoot.anchoredPosition = anchored;
    }

    private static Canvas ResolveCanvas()
    {
        var runtimeCanvasGo = GameObject.Find("__RuntimeDialogueCanvas");
        if (runtimeCanvasGo != null)
        {
            var runtimeCanvas = runtimeCanvasGo.GetComponent<Canvas>();
            if (runtimeCanvas != null)
                return runtimeCanvas;
        }

        var active = SceneManager.GetActiveScene();
        var roots = active.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var canvases = roots[i].GetComponentsInChildren<Canvas>(true);
            if (canvases.Length > 0)
                return canvases[0];
        }

        return null;
    }

    private static SpeechBubbleUI ResolveTemplate(SpeechBubbleUI explicitTemplate)
    {
        if (explicitTemplate != null)
            return explicitTemplate;

        var fromResources = Resources.Load<SpeechBubbleUI>("DialogBox");
        if (fromResources != null)
            return fromResources;

        var dm = FindAnyObjectByType<DialogueManager>();
        if (dm == null)
            return null;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var fi = typeof(DialogueManager).GetField("speechBubblePrefab", flags);
        if (fi == null)
            return null;

        return fi.GetValue(dm) as SpeechBubbleUI;
    }

    private static void NormalizeBubbleLayout(RectTransform bubbleRoot)
    {
        if (bubbleRoot == null)
            return;

        var fitters = bubbleRoot.GetComponentsInChildren<ContentSizeFitter>(true);
        for (int i = 0; i < fitters.Length; i++)
            fitters[i].enabled = false;

        var box = bubbleRoot.Find("DialogBox") as RectTransform;
        if (box != null)
        {
            box.anchoredPosition = new Vector2(0f, -40f);
            box.sizeDelta = new Vector2(350f, 256f);
        }

        var dialog = bubbleRoot.Find("DialogBox/Dialog") as RectTransform;
        if (dialog != null)
            dialog.sizeDelta = new Vector2(350f, 256f);
    }

    private static void EnsureVisibleBackground(RectTransform bubbleRoot)
    {
        if (bubbleRoot == null)
            return;

        var images = bubbleRoot.GetComponentsInChildren<Image>(true);
        bool hasRenderableImage = false;
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].sprite != null && images[i].color.a > 0.01f)
            {
                hasRenderableImage = true;
                break;
            }
        }

        if (hasRenderableImage)
            return;

        if (bubbleRoot.Find("__AutoBubbleBG") != null)
            return;

        var bgGo = new GameObject("__AutoBubbleBG", typeof(RectTransform), typeof(RawImage));
        bgGo.transform.SetParent(bubbleRoot, false);
        bgGo.transform.SetAsFirstSibling();

        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var raw = bgGo.GetComponent<RawImage>();
        raw.color = new Color(1f, 1f, 1f, 0.95f);
        raw.raycastTarget = false;
    }

    private static void ForceWhiteText(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.color = Color.white;
        text.alpha = 1f;
        text.alignment = TextAlignmentOptions.Center;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;

        if (text.fontSharedMaterial != null)
        {
            var mat = new Material(text.fontSharedMaterial);
            mat.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
            text.fontMaterial = mat;
        }

        text.SetVerticesDirty();
        text.SetMaterialDirty();
        text.UpdateMeshPadding();
        text.ForceMeshUpdate();
    }
}
