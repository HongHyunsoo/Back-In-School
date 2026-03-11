using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LunchRunningTeacherWatcher : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private Collider2D detectionTrigger;
    [SerializeField] private bool autoCreateDetectionChild = true;
    [SerializeField] private float detectionRadius = 2.6f;

    [Header("Rule")]
    [SerializeField] private int warningsPerPenalty = 3;
    [SerializeField] private int penaltyAmount = 1;
    [SerializeField] private float warningCooldownSeconds = 1.4f;

    [Header("Patrol")]
    [SerializeField] private bool enablePatrol = true;
    [SerializeField] private float patrolDistance = 1.6f;
    [SerializeField] private float patrolSpeed = 1.1f;
    [SerializeField] private bool flipSpriteWithDirection = true;

    [Header("Bubble")]
    [SerializeField] private SpeechBubbleUI warningBubbleTemplate;
    [SerializeField] private Vector3 bubbleWorldOffset = new Vector3(0f, 1.25f, 0f);
    [SerializeField] private float bubbleScale = 0.65f;
    [SerializeField] private float bubbleVisibleSeconds = 1.8f;
    [SerializeField] private string speakerNameKo = "\uC120\uC0DD\uB2D8";
    [SerializeField] private string speakerNameEn = "Teacher";
    [TextArea]
    [SerializeField] private string warningTextKo = "\uBCF5\uB3C4\uC5D0\uC11C \uB6F0\uC9C0 \uB9C8.";
    [TextArea]
    [SerializeField] private string warningTextEn = "No running in the hallway.";
    [TextArea]
    [SerializeField] private string penaltyTextKo = "\uC138 \uBC88\uC774\uB2E4. \uBC8C\uC810\uC774\uB2E4.";
    [TextArea]
    [SerializeField] private string penaltyTextEn = "That's three times. Penalty.";

    private static string activeLunchFlowId = string.Empty;
    private static int accumulatedWarnings;

    private PlayerController playerController;
    private bool playerInRange;
    private bool consumedCurrentRun;
    private float nextWarnAllowedTime;

    private RectTransform bubbleRoot;
    private TextMeshProUGUI bubbleNameText;
    private TextMeshProUGUI bubbleBodyText;
    private Coroutine bubbleRoutine;
    private DialogueManager cachedDialogueManager;
    private Camera cachedWorldCamera;
    private SpriteRenderer cachedSpriteRenderer;
    private Vector3 patrolOriginLocalPosition;
    private bool patrolOriginInitialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeLunchFlowId = string.Empty;
        accumulatedWarnings = 0;
    }

    private void Awake()
    {
        EnsureDetectionTrigger();
        cachedSpriteRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        patrolOriginLocalPosition = transform.localPosition;
        patrolOriginInitialized = true;
    }

    private void Update()
    {
        if (!IsLunchRuleActive())
        {
            playerInRange = false;
            consumedCurrentRun = false;
            HideBubble();
            return;
        }

        TickPatrol();

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();

        if (playerController == null)
            return;

        SyncSharedCounterWithCurrentFlow();

        bool running = playerInRange && playerController.IsActivelyRunning;
        if (!running)
        {
            consumedCurrentRun = false;
            return;
        }

        if (consumedCurrentRun || Time.unscaledTime < nextWarnAllowedTime)
            return;

        consumedCurrentRun = true;
        nextWarnAllowedTime = Time.unscaledTime + Mathf.Max(0.1f, warningCooldownSeconds);

        accumulatedWarnings++;

        bool applyPenalty = accumulatedWarnings >= Mathf.Max(1, warningsPerPenalty);
        if (applyPenalty)
            accumulatedWarnings = 0;

        if (applyPenalty)
            AddPenalty();

        ShowBubble(applyPenalty ? GetPenaltyText() : GetWarningText());
    }

    private void LateUpdate()
    {
        if (bubbleRoot != null && bubbleRoot.gameObject.activeSelf)
            PositionBubble();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = true;
        if (playerController == null)
            playerController = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = false;
        consumedCurrentRun = false;
    }

    private void EnsureDetectionTrigger()
    {
        if (detectionTrigger != null)
        {
            detectionTrigger.isTrigger = true;
            return;
        }

        if (!autoCreateDetectionChild)
            return;

        var circle = GetComponent<CircleCollider2D>();
        if (circle == null)
            circle = gameObject.AddComponent<CircleCollider2D>();

        circle.isTrigger = true;
        circle.radius = Mathf.Max(0.1f, detectionRadius);
        detectionTrigger = circle;
    }

    private bool IsLunchRuleActive()
    {
        if (SceneManager.GetActiveScene().name != "FREEROAM")
            return false;

        string flowType = PlayerPrefs.GetString("FLOW_TYPE", "");
        if (!string.Equals(flowType, "FREEROAM", System.StringComparison.OrdinalIgnoreCase))
            return false;

        string flowId = PlayerPrefs.GetString("FLOW_ID", "");
        if (string.IsNullOrEmpty(flowId) || flowId.IndexOf("LUNCH", System.StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null && gm.currentState != GameState.Lunch_FreeTime)
            return false;

        return true;
    }

    private void SyncSharedCounterWithCurrentFlow()
    {
        string flowId = PlayerPrefs.GetString("FLOW_ID", "");
        if (string.Equals(activeLunchFlowId, flowId, System.StringComparison.Ordinal))
            return;

        activeLunchFlowId = flowId;
        accumulatedWarnings = 0;
    }

    private void AddPenalty()
    {
        if (FlowManager.Instance != null)
        {
            FlowManager.Instance.AddPenaltyWithReason(penaltyAmount, PenaltyReasonLog.ReasonRunningAtLunch);
            return;
        }

        int day = 1;
        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            day = Mathf.Max(1, gm.currentDay);

        PenaltyReasonLog.Add(PenaltyReasonLog.ReasonRunningAtLunch, penaltyAmount, day);
    }

    private void ShowBubble(string body)
    {
        EnsureBubble();
        if (bubbleRoot == null || bubbleBodyText == null)
            return;

        if (bubbleNameText != null)
            bubbleNameText.text = GetSpeakerName();

        bubbleBodyText.text = body;
        bubbleRoot.gameObject.SetActive(true);
        bubbleRoot.SetAsLastSibling();
        PositionBubble();

        if (bubbleRoutine != null)
            StopCoroutine(bubbleRoutine);
        bubbleRoutine = StartCoroutine(CoHideBubbleLater());
    }

    private IEnumerator CoHideBubbleLater()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.2f, bubbleVisibleSeconds));
        HideBubble();
        bubbleRoutine = null;
    }

    private void HideBubble()
    {
        if (bubbleRoot != null)
            bubbleRoot.gameObject.SetActive(false);
    }

    private void EnsureBubble()
    {
        if (bubbleRoot != null && bubbleBodyText != null)
        {
            PositionBubble();
            return;
        }

        Transform parent = ResolveBubbleParent();
        if (parent == null)
            return;

        SpeechBubbleUI template = ResolveBubbleTemplate();
        if (template != null)
        {
            var bubble = Instantiate(template, parent);
            bubbleRoot = bubble.transform as RectTransform;
            bubbleNameText = bubble.nameText;
            bubbleBodyText = bubble.bodyText;
            bubbleRoot.localScale = Vector3.one * bubbleScale;
            bubbleRoot.gameObject.name = "LunchRunTeacherBubble";
            bubbleRoot.gameObject.SetActive(false);
            EnsureBubbleBackground(bubbleRoot);
            return;
        }

        var go = new GameObject("LunchRunTeacherBubble", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        bubbleRoot = go.GetComponent<RectTransform>();
        bubbleRoot.anchorMin = new Vector2(0.5f, 0.5f);
        bubbleRoot.anchorMax = new Vector2(0.5f, 0.5f);
        bubbleRoot.pivot = new Vector2(0.5f, 0f);
        bubbleRoot.sizeDelta = new Vector2(340f, 130f);
        bubbleRoot.localScale = Vector3.one * bubbleScale;

        var bg = go.GetComponent<RawImage>();
        bg.color = new Color(1f, 1f, 1f, 0.95f);
        bg.raycastTarget = false;

        var textGo = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 10f);
        textRect.offsetMax = new Vector2(-12f, -10f);

        bubbleBodyText = textGo.GetComponent<TextMeshProUGUI>();
        bubbleBodyText.alignment = TextAlignmentOptions.Center;
        bubbleBodyText.fontSize = 24f;
        bubbleBodyText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        bubbleBodyText.enableWordWrapping = true;

        bubbleRoot.gameObject.SetActive(false);
    }

    private void PositionBubble()
    {
        if (bubbleRoot == null)
            return;
        if (!(bubbleRoot.parent is RectTransform parentRect))
            return;

        var canvas = parentRect.GetComponentInParent<Canvas>();
        Camera uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Camera worldCam = ResolveWorldCamera();
        if (worldCam == null)
            return;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(worldCam, transform.position + bubbleWorldOffset);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, uiCam, out var local))
        {
            local.x = Mathf.Round(local.x);
            local.y = Mathf.Round(local.y + 18f);
            bubbleRoot.anchoredPosition = local;
        }
    }

    private void TickPatrol()
    {
        if (!enablePatrol)
            return;

        if (!patrolOriginInitialized)
        {
            patrolOriginLocalPosition = transform.localPosition;
            patrolOriginInitialized = true;
        }

        float distance = Mathf.Max(0.05f, patrolDistance);
        float speed = Mathf.Max(0.05f, patrolSpeed);
        float offset = Mathf.Sin(Time.time * speed) * distance;

        Vector3 local = patrolOriginLocalPosition;
        local.x += offset;
        transform.localPosition = local;

        if (flipSpriteWithDirection && cachedSpriteRenderer != null)
        {
            float dir = Mathf.Cos(Time.time * speed);
            if (Mathf.Abs(dir) > 0.001f)
                cachedSpriteRenderer.flipX = dir < 0f;
        }
    }

    private Transform ResolveBubbleParent()
    {
        var runtimeCanvas = GameObject.Find("__RuntimeDialogueCanvas");
        if (runtimeCanvas != null)
        {
            var canvas = runtimeCanvas.GetComponent<Canvas>();
            if (canvas != null)
                return canvas.transform;
        }

        var active = SceneManager.GetActiveScene();
        var roots = active.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var canvases = roots[i].GetComponentsInChildren<Canvas>(true);
            if (canvases.Length > 0)
                return canvases[0].transform;
        }

        return null;
    }

    private SpeechBubbleUI ResolveBubbleTemplate()
    {
        if (warningBubbleTemplate != null)
            return warningBubbleTemplate;

        if (cachedDialogueManager == null)
            cachedDialogueManager = FindAnyObjectByType<DialogueManager>();
        if (cachedDialogueManager == null)
            return null;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var field = typeof(DialogueManager).GetField("speechBubblePrefab", flags);
        if (field == null)
            return null;

        return field.GetValue(cachedDialogueManager) as SpeechBubbleUI;
    }

    private Camera ResolveWorldCamera()
    {
        if (Camera.main != null)
        {
            cachedWorldCamera = Camera.main;
            return cachedWorldCamera;
        }

        if (cachedWorldCamera == null)
            cachedWorldCamera = FindAnyObjectByType<Camera>();
        return cachedWorldCamera;
    }

    private void EnsureBubbleBackground(RectTransform root)
    {
        if (root == null)
            return;

        var images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].sprite != null && images[i].color.a > 0.01f)
                return;
        }

        var bg = root.Find("__AutoBubbleBG");
        if (bg != null)
            return;

        var bgGo = new GameObject("__AutoBubbleBG", typeof(RectTransform), typeof(RawImage));
        bgGo.transform.SetParent(root, false);
        bgGo.transform.SetAsFirstSibling();

        var rect = bgGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var raw = bgGo.GetComponent<RawImage>();
        raw.color = new Color(1f, 1f, 1f, 0.95f);
        raw.raycastTarget = false;
    }

    private string GetSpeakerName()
    {
        return IsEnglish() ? speakerNameEn : speakerNameKo;
    }

    private string GetWarningText()
    {
        return IsEnglish() ? warningTextEn : warningTextKo;
    }

    private string GetPenaltyText()
    {
        return IsEnglish() ? penaltyTextEn : penaltyTextKo;
    }

    private bool IsEnglish()
    {
        return LocalizationManager.Instance != null &&
               LocalizationManager.Instance.GetCurrentLanguage() == Language.English;
    }
}
