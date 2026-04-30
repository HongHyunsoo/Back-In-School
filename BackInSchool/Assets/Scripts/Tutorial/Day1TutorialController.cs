using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class Day1TutorialController
{
    private static Day1TutorialRuntimeController runtime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureRuntime();
    }

    public static void ResetProgress()
    {
        EnsureRuntime()?.ResetProgress();
    }

    public static bool IsPhoneAppAllowed(PhoneAppId appId)
    {
        Day1TutorialRuntimeController controller = EnsureRuntime();
        return controller == null || controller.IsPhoneAppAllowed(appId);
    }

    public static bool IsLockerInteractionAllowed()
    {
        Day1TutorialRuntimeController controller = EnsureRuntime();
        return controller == null || controller.IsLockerInteractionAllowed();
    }

    public static bool IsDialogueConversationAllowed(string conversationId)
    {
        Day1TutorialRuntimeController controller = EnsureRuntime();
        return controller == null || controller.IsDialogueConversationAllowed(conversationId);
    }

    public static bool IsMorningSeatInteractionAllowed()
    {
        Day1TutorialRuntimeController controller = EnsureRuntime();
        return controller == null || controller.IsMorningSeatInteractionAllowed();
    }

    private static Day1TutorialRuntimeController EnsureRuntime()
    {
        if (runtime != null)
            return runtime;

        runtime = UnityEngine.Object.FindAnyObjectByType<Day1TutorialRuntimeController>();
        if (runtime != null)
            return runtime;

        var go = new GameObject("__Day1TutorialController");
        runtime = go.AddComponent<Day1TutorialRuntimeController>();
        UnityEngine.Object.DontDestroyOnLoad(go);
        return runtime;
    }

    internal static void Register(Day1TutorialRuntimeController controller)
    {
        runtime = controller;
    }

    internal static void Unregister(Day1TutorialRuntimeController controller)
    {
        if (runtime == controller)
            runtime = null;
    }
}

internal sealed class Day1TutorialRuntimeController : MonoBehaviour
{
    private const string StagePrefKey = "DAY1_TUTORIAL_STAGE";
    private const string CompletedPrefKey = "DAY1_TUTORIAL_COMPLETED";

    private const string GonyongConversationId = "DAY1_MOR_GONYONG";
    private const string AdultConversationId = "DAY1_MOR_ADULT";
    private const string GonyongEventConversationId = "DAY1_MOR_GONYONG_EVENT";

    private static TMP_FontAsset cachedTutorialFont;

    private Canvas tutorialCanvas;
    private CanvasGroup tutorialCanvasGroup;
    private RectTransform tutorialCanvasRect;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI progressText;

    private Image worldTargetStarImage;
    private Image phoneTargetStarImage;

    private int stage;
    private bool completed;
    private bool visitedRulesApp;
    private int chatCompletedSessionBaseline = -1;

    private Transform trackedPlayer;
    private Vector3 trackedPlayerStartPos;
    private bool playerMoveAnchorValid;

    private readonly Vector3 worldStarOffset = new Vector3(0f, 1.35f, 0f);

    private enum TutorialStage
    {
        SubwayHealth = 0,
        SubwayChat = 1,
        SubwayRules = 2,
        SubwayClosePhone = 3,
        MorningMove = 4,
        MorningLocker = 5,
        MorningConversationChain = 6,
        MorningSeat = 7,
        Done = 8,
    }

    private void Awake()
    {
        Day1TutorialController.Register(this);
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        Day1TutorialController.Unregister(this);
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        stage = PlayerPrefs.GetInt(StagePrefKey, 0);
        completed = PlayerPrefs.GetInt(CompletedPrefKey, 0) == 1;
        EnsureRuntimeUi();
        RefreshVisibility(false);
    }

    private void Update()
    {
        if (completed)
        {
            HideTutorial();
            return;
        }

        TrackPhoneAppVisits();
        EvaluateAutoProgress();
        RefreshVisibility(false);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        trackedPlayer = null;
        playerMoveAnchorValid = false;
        EnsureRuntimeUi();
        RefreshVisibility(false);
    }

    public void ResetProgress()
    {
        PlayerPrefs.DeleteKey(StagePrefKey);
        PlayerPrefs.DeleteKey(CompletedPrefKey);
        PlayerPrefs.Save();

        stage = 0;
        completed = false;
        visitedRulesApp = false;
        chatCompletedSessionBaseline = -1;
        trackedPlayer = null;
        playerMoveAnchorValid = false;
        RefreshVisibility(false);
    }

    public bool IsPhoneAppAllowed(PhoneAppId appId)
    {
        if (completed || FlowManager.Instance == null || FlowManager.Instance.day != 1 || !FlowContext.IsChat())
            return true;

        if (appId == PhoneAppId.Home)
            return true;

        return (TutorialStage)stage switch
        {
            TutorialStage.SubwayHealth => appId == PhoneAppId.Health,
            TutorialStage.SubwayChat => appId == PhoneAppId.Chat,
            TutorialStage.SubwayRules => appId == PhoneAppId.Rules,
            TutorialStage.SubwayClosePhone => false,
            _ => true,
        };
    }

    public bool IsLockerInteractionAllowed()
    {
        if (completed || FlowManager.Instance == null || FlowManager.Instance.day != 1)
            return true;

        if (!FlowContext.IsMorningBeforeAssemblyFreeRoam())
            return true;

        return (TutorialStage)stage == TutorialStage.MorningLocker;
    }

    public bool IsDialogueConversationAllowed(string conversationId)
    {
        if (completed || FlowManager.Instance == null || FlowManager.Instance.day != 1)
            return true;

        if (!FlowContext.IsMorningBeforeAssemblyFreeRoam())
            return true;

        if (string.IsNullOrEmpty(conversationId))
            return false;

        return (TutorialStage)stage == TutorialStage.MorningConversationChain &&
               string.Equals(conversationId, GetExpectedMorningConversationId(), StringComparison.OrdinalIgnoreCase);
    }

    public bool IsMorningSeatInteractionAllowed()
    {
        if (completed || FlowManager.Instance == null || FlowManager.Instance.day != 1)
            return true;

        if (!FlowContext.IsMorningBeforeAssemblyFreeRoam())
            return true;

        return (TutorialStage)stage == TutorialStage.MorningSeat;
    }

    private void EnsureRuntimeUi()
    {
        if (tutorialCanvas != null)
            return;

        var canvasGo = new GameObject("__Day1TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        tutorialCanvas = canvasGo.GetComponent<Canvas>();
        tutorialCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        tutorialCanvas.sortingOrder = 10000;
        tutorialCanvasRect = tutorialCanvas.transform as RectTransform;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        tutorialCanvasGroup = canvasGo.AddComponent<CanvasGroup>();
        tutorialCanvasGroup.blocksRaycasts = false;
        tutorialCanvasGroup.interactable = false;

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -42f);
        panelRect.sizeDelta = new Vector2(860f, 188f);

        var panelImage = panelGo.GetComponent<Image>();
        panelImage.color = new Color(0.05f, 0.06f, 0.09f, 0.88f);
        panelImage.raycastTarget = false;

        titleText = CreateLabel("Title", panelRect, 34f, FontStyles.Bold, TextAlignmentOptions.Center);
        titleText.rectTransform.anchorMin = new Vector2(0.05f, 0.70f);
        titleText.rectTransform.anchorMax = new Vector2(0.95f, 0.92f);
        titleText.color = Color.white;

        bodyText = CreateLabel("Body", panelRect, 28f, FontStyles.Normal, TextAlignmentOptions.Center);
        bodyText.rectTransform.anchorMin = new Vector2(0.07f, 0.18f);
        bodyText.rectTransform.anchorMax = new Vector2(0.93f, 0.72f);
        bodyText.enableWordWrapping = true;
        bodyText.overflowMode = TextOverflowModes.Ellipsis;
        bodyText.color = new Color(0.96f, 0.97f, 1f, 1f);

        progressText = CreateLabel("Progress", panelRect, 22f, FontStyles.Normal, TextAlignmentOptions.Center);
        progressText.rectTransform.anchorMin = new Vector2(0.05f, 0.02f);
        progressText.rectTransform.anchorMax = new Vector2(0.95f, 0.18f);
        progressText.color = new Color(0.78f, 0.83f, 0.92f, 1f);

        worldTargetStarImage = CreateStarImage(canvasGo.transform, "WorldTargetStar");
        phoneTargetStarImage = CreateStarImage(canvasGo.transform, "PhoneTargetStar");
        if (phoneTargetStarImage != null)
            phoneTargetStarImage.gameObject.SetActive(false);
    }

    private static TextMeshProUGUI CreateLabel(string name, RectTransform parent, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = ResolveTutorialFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private Image CreateStarImage(Transform parent, string name)
    {
        var starGo = new GameObject(name, typeof(RectTransform), typeof(Image));
        starGo.transform.SetParent(parent, false);

        var starRect = starGo.GetComponent<RectTransform>();
        starRect.anchorMin = new Vector2(0.5f, 0.5f);
        starRect.anchorMax = new Vector2(0.5f, 0.5f);
        starRect.pivot = new Vector2(0.5f, 0.5f);
        starRect.sizeDelta = new Vector2(72f, 72f);

        var image = starGo.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = ResolveStarSprite();
        image.color = Color.white;
        image.preserveAspect = true;
        starGo.SetActive(image.sprite != null);
        return image;
    }

    private static TMP_FontAsset ResolveTutorialFont()
    {
        if (cachedTutorialFont != null)
            return cachedTutorialFont;

        TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < loadedFonts.Length; i++)
        {
            TMP_FontAsset candidate = loadedFonts[i];
            if (candidate == null || string.IsNullOrEmpty(candidate.name))
                continue;

            if (candidate.name.Equals("Galmuri11-Bold SDF", StringComparison.OrdinalIgnoreCase) ||
                candidate.name.IndexOf("Galmuri11-Bold", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                cachedTutorialFont = candidate;
                return cachedTutorialFont;
            }
        }

        cachedTutorialFont = TMP_Settings.defaultFontAsset;
        return cachedTutorialFont;
    }

    private static Sprite ResolveStarSprite()
    {
        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null || string.IsNullOrEmpty(sprite.name))
                continue;

            if (sprite.name.Equals("Star", StringComparison.OrdinalIgnoreCase) ||
                sprite.name.IndexOf("Star", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return sprite;
            }
        }

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Star.png");
#else
        return null;
#endif
    }

    private void TrackPhoneAppVisits()
    {
        var appManager = FindAnyObjectByType<PhoneAppManager>();
        if (appManager == null)
            return;

        if (appManager.CurrentApp == PhoneAppId.Rules)
            visitedRulesApp = true;
    }

    private void EvaluateAutoProgress()
    {
        if (FlowManager.Instance == null || FlowManager.Instance.day != 1)
            return;

        while (!completed)
        {
            bool advance = false;

            switch ((TutorialStage)stage)
            {
                case TutorialStage.SubwayHealth:
                    advance = PhoneSubwayFlowGate.IsHealthChecked(1);
                    break;
                case TutorialStage.SubwayChat:
                    advance = HasCompletedChatTutorialSession();
                    break;
                case TutorialStage.SubwayRules:
                    advance = visitedRulesApp;
                    break;
                case TutorialStage.SubwayClosePhone:
                    advance = PhoneSystem.Instance == null || !PhoneSystem.Instance.IsOpen;
                    break;
                case TutorialStage.MorningMove:
                    advance = HasPlayerMovedEnough();
                    break;
                case TutorialStage.MorningLocker:
                    advance = FlowManager.Instance.IsWearingSlippers;
                    break;
                case TutorialStage.MorningConversationChain:
                    advance = DialogueProgressState.HasCompletedConversation(GonyongEventConversationId);
                    break;
                case TutorialStage.MorningSeat:
                    advance = !FlowContext.IsMorningBeforeAssemblyFreeRoam();
                    break;
                case TutorialStage.Done:
                    MarkCompleted();
                    return;
            }

            if (!advance)
                break;

            SetStage(stage + 1);
        }
    }

    private bool HasCompletedChatTutorialSession()
    {
        if (!FlowContext.IsChat())
            return false;

        ChatService chatService = ChatService.Instance;
        if (chatService == null)
            return false;

        if (chatCompletedSessionBaseline < 0)
            chatCompletedSessionBaseline = chatService.GetCompletedSessionCount();

        return chatService.GetCompletedSessionCount() > chatCompletedSessionBaseline;
    }

    private bool HasPlayerMovedEnough()
    {
        if (!FlowContext.IsMorningBeforeAssemblyFreeRoam())
            return false;

        if (trackedPlayer == null)
            trackedPlayer = FindPlayerTransform();

        if (trackedPlayer == null)
            return false;

        if (!playerMoveAnchorValid)
        {
            trackedPlayerStartPos = trackedPlayer.position;
            playerMoveAnchorValid = true;
            return false;
        }

        return Vector3.Distance(trackedPlayer.position, trackedPlayerStartPos) >= 1.2f;
    }

    private Transform FindPlayerTransform()
    {
        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
            return tagged.transform;

        PlayerController playerController = FindAnyObjectByType<PlayerController>();
        return playerController != null ? playerController.transform : null;
    }

    private void SetStage(int value)
    {
        stage = Mathf.Clamp(value, 0, (int)TutorialStage.Done);
        PlayerPrefs.SetInt(StagePrefKey, stage);

        if (stage >= (int)TutorialStage.Done)
        {
            MarkCompleted();
            return;
        }

        if (stage != (int)TutorialStage.MorningMove)
            playerMoveAnchorValid = false;

        if (stage != (int)TutorialStage.SubwayChat)
            chatCompletedSessionBaseline = -1;

        PlayerPrefs.Save();
    }

    private void MarkCompleted()
    {
        completed = true;
        PlayerPrefs.SetInt(CompletedPrefKey, 1);
        PlayerPrefs.Save();
        HideTutorial();
    }

    private void RefreshVisibility(bool forceHide)
    {
        if (tutorialCanvas == null)
            return;

        if (forceHide || completed || FlowManager.Instance == null || FlowManager.Instance.day != 1 || !IsTutorialSceneRelevant())
        {
            HideTutorial();
            return;
        }

        if (!TryBuildCurrentTutorialText(out string title, out string body, out string progress))
        {
            HideTutorial();
            return;
        }

        tutorialCanvas.enabled = true;
        tutorialCanvasGroup.alpha = 1f;
        titleText.text = title;
        bodyText.text = body;
        progressText.text = progress;
        UpdateTargetStar();
    }

    private static bool IsTutorialSceneRelevant()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName.Equals("CHAT", StringComparison.OrdinalIgnoreCase) ||
               sceneName.Equals("FREEROAM", StringComparison.OrdinalIgnoreCase);
    }

    private void HideTutorial()
    {
        if (tutorialCanvas == null)
            return;

        tutorialCanvas.enabled = false;
        if (tutorialCanvasGroup != null)
            tutorialCanvasGroup.alpha = 0f;
        if (worldTargetStarImage != null)
            worldTargetStarImage.gameObject.SetActive(false);
        if (phoneTargetStarImage != null)
            phoneTargetStarImage.gameObject.SetActive(false);
    }

    private void UpdateTargetStar()
    {
        bool phoneStarShown = TryAttachPhoneTargetStar();
        bool worldStarShown = !phoneStarShown && TryPlaceWorldTargetStar();

        if (!phoneStarShown && phoneTargetStarImage != null)
            phoneTargetStarImage.gameObject.SetActive(false);

        if (!worldStarShown && worldTargetStarImage != null)
            worldTargetStarImage.gameObject.SetActive(false);
    }

    private bool TryAttachPhoneTargetStar()
    {
        if (phoneTargetStarImage == null)
            return false;

        string targetName = GetCurrentPhoneTargetName();
        if (string.IsNullOrEmpty(targetName))
            return false;

        if (PhoneSystem.Instance == null || !PhoneSystem.Instance.IsOpen)
            return false;

        RectTransform target = FindPhoneUiRectTransformByName(targetName);
        if (target == null || !target.gameObject.activeInHierarchy)
            return false;

        RectTransform starRect = phoneTargetStarImage.rectTransform;
        if (starRect.parent != target)
            starRect.SetParent(target, false);

        starRect.anchorMin = new Vector2(0.5f, 0.82f);
        starRect.anchorMax = new Vector2(0.5f, 0.82f);
        starRect.pivot = new Vector2(0.5f, 0.5f);
        starRect.anchoredPosition = Vector2.zero;
        starRect.sizeDelta = new Vector2(54f, 54f);
        starRect.SetAsLastSibling();

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.08f;
        starRect.localScale = new Vector3(pulse, pulse, 1f);
        phoneTargetStarImage.gameObject.SetActive(phoneTargetStarImage.sprite != null);
        return phoneTargetStarImage.gameObject.activeSelf;
    }

    private bool TryPlaceWorldTargetStar()
    {
        if (worldTargetStarImage == null || tutorialCanvasRect == null)
            return false;

        if (!TryGetWorldTargetScreenPosition(out Vector2 screenPosition))
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(tutorialCanvasRect, screenPosition, null, out Vector2 localPoint))
            return false;

        RectTransform rect = worldTargetStarImage.rectTransform;
        rect.SetParent(tutorialCanvasRect, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = localPoint;
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.08f;
        rect.localScale = new Vector3(pulse, pulse, 1f);
        worldTargetStarImage.gameObject.SetActive(worldTargetStarImage.sprite != null);
        return worldTargetStarImage.gameObject.activeSelf;
    }

    private string GetCurrentPhoneTargetName()
    {
        return (TutorialStage)stage switch
        {
            TutorialStage.SubwayHealth => "Btn_AppHealth",
            TutorialStage.SubwayChat => "Btn_AppChat",
            TutorialStage.SubwayRules => "Btn_AppRules",
            TutorialStage.SubwayClosePhone => "Btn_ClosePhone",
            _ => null,
        };
    }

    private bool TryGetWorldTargetScreenPosition(out Vector2 screenPosition)
    {
        screenPosition = default;

        Transform target = (TutorialStage)stage switch
        {
            TutorialStage.MorningLocker => FindMorningLockerTarget(),
            TutorialStage.MorningConversationChain => FindMorningConversationTarget(),
            TutorialStage.MorningSeat => FindMorningSeatTarget(),
            _ => null,
        };

        if (target == null)
            return false;

        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Vector3 worldPoint = target.position + worldStarOffset;
        Vector3 sp = cam.WorldToScreenPoint(worldPoint);
        if (sp.z <= 0f)
            return false;

        screenPosition = new Vector2(sp.x, sp.y);
        return true;
    }

    private RectTransform FindPhoneUiRectTransformByName(string targetName)
    {
        PhoneAppManager appManager = FindAnyObjectByType<PhoneAppManager>();
        if (appManager == null)
            return null;

        RectTransform[] rects = appManager.transform.root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null)
                continue;

            if (!rect.name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                continue;

            return rect;
        }

        return null;
    }

    private Transform FindMorningLockerTarget()
    {
        SchoolLockerInteraction[] lockers = FindObjectsByType<SchoolLockerInteraction>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < lockers.Length; i++)
        {
            if (lockers[i] != null && lockers[i].gameObject.activeInHierarchy)
                return lockers[i].transform;
        }

        return null;
    }

    private Transform FindMorningConversationTarget()
    {
        string expectedConversationId = GetExpectedMorningConversationId();
        if (string.IsNullOrEmpty(expectedConversationId))
            return null;

        DialogueTrigger[] triggers = FindObjectsByType<DialogueTrigger>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < triggers.Length; i++)
        {
            DialogueTrigger trigger = triggers[i];
            if (trigger == null || !trigger.gameObject.activeInHierarchy)
                continue;

            if (MatchesConversation(trigger, expectedConversationId))
                return trigger.transform;
        }

        return null;
    }

    private Transform FindMorningSeatTarget()
    {
        FlowStepInteractionTrigger[] triggers = FindObjectsByType<FlowStepInteractionTrigger>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < triggers.Length; i++)
        {
            FlowStepInteractionTrigger trigger = triggers[i];
            if (trigger == null || !trigger.gameObject.activeInHierarchy)
                continue;

            return trigger.transform;
        }

        return null;
    }

    private static bool MatchesConversation(DialogueTrigger trigger, string conversationId)
    {
        if (trigger == null || string.IsNullOrEmpty(conversationId))
            return false;

        if (string.Equals(trigger.defaultConversationID, conversationId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (trigger.contextualDialogues == null)
            return false;

        for (int i = 0; i < trigger.contextualDialogues.Count; i++)
        {
            ContextualDialogue dialogue = trigger.contextualDialogues[i];
            if (dialogue == null)
                continue;

            if (string.Equals(dialogue.conversationID, conversationId, StringComparison.OrdinalIgnoreCase))
                return true;

            if (dialogue.randomConversationIDs == null)
                continue;

            for (int j = 0; j < dialogue.randomConversationIDs.Count; j++)
            {
                if (string.Equals(dialogue.randomConversationIDs[j], conversationId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private bool TryBuildCurrentTutorialText(out string title, out string body, out string progress)
    {
        title = string.Empty;
        body = string.Empty;
        progress = string.Empty;

        switch ((TutorialStage)stage)
        {
            case TutorialStage.SubwayHealth:
                if (!FlowContext.IsChat())
                    return false;
                title = L("튜토리얼", "Tutorial");
                body = L("폰에서 건강 자가진단을 먼저 끝내자.", "Finish the health survey on your phone first.");
                progress = L("1 / 8 지하철", "1 / 8 Subway");
                return true;

            case TutorialStage.SubwayChat:
                if (!FlowContext.IsChat())
                    return false;
                title = L("튜토리얼", "Tutorial");
                body = L("이제 채팅을 열고 대화를 끝까지 확인하자.", "Open the chat app and finish one chat session.");
                progress = L("2 / 8 지하철", "2 / 8 Subway");
                return true;

            case TutorialStage.SubwayRules:
                if (!FlowContext.IsChat())
                    return false;
                title = L("튜토리얼", "Tutorial");
                body = L("룰 북을 열어 학교 규칙도 확인하자.", "Open the rule book and check the school rules.");
                progress = L("3 / 8 지하철", "3 / 8 Subway");
                return true;

            case TutorialStage.SubwayClosePhone:
                if (!FlowContext.IsChat())
                    return false;
                title = L("튜토리얼", "Tutorial");
                body = L("이제 닫기 버튼을 눌러 폰을 내리자.", "Press the close button and put the phone away.");
                progress = L("4 / 8 지하철", "4 / 8 Subway");
                return true;

            case TutorialStage.MorningMove:
                if (!FlowContext.IsMorningBeforeAssemblyFreeRoam())
                    return false;
                title = L("튜토리얼", "Tutorial");
                body = L("이동 키로 교실 안을 조금 걸어보자.", "Use the movement keys and walk around a little.");
                progress = L("5 / 8 아침 자유시간", "5 / 8 Morning Free Time");
                return true;

            case TutorialStage.MorningLocker:
                if (!FlowContext.IsMorningBeforeAssemblyFreeRoam())
                    return false;
                title = L("튜토리얼", "Tutorial");
                body = L("사물함과 상호작용해서 실내화로 갈아신자.", "Interact with the locker and change into slippers.");
                progress = L("6 / 8 아침 자유시간", "6 / 8 Morning Free Time");
                return true;

            case TutorialStage.MorningConversationChain:
                if (!FlowContext.IsMorningBeforeAssemblyFreeRoam())
                    return false;
                title = L("튜토리얼", "Tutorial");
                body = BuildMorningConversationBody();
                progress = L("7 / 8 아침 자유시간", "7 / 8 Morning Free Time");
                return true;

            case TutorialStage.MorningSeat:
                if (!FlowContext.IsMorningBeforeAssemblyFreeRoam())
                    return false;
                title = L("튜토리얼", "Tutorial");
                body = L("이제 자리에 상호작용해서 다음 흐름으로 넘어가자.", "Interact with your seat to move on to the next flow.");
                progress = L("8 / 8 아침 자유시간", "8 / 8 Morning Free Time");
                return true;
        }

        return false;
    }

    private string BuildMorningConversationBody()
    {
        bool talkedGonyong = DialogueProgressState.HasCompletedConversation(GonyongConversationId);
        bool talkedAdult = DialogueProgressState.HasCompletedConversation(AdultConversationId);
        bool didPhotoEvent = DialogueProgressState.HasCompletedConversation(GonyongEventConversationId);

        if (didPhotoEvent)
            return L("사진까지 확인했어. 이제 자리로 돌아가자.", "You finished the photo event. Head back to your seat.");

        if (talkedAdult)
            return L("이제 고뇽이에게 다시 가서 사진 얘기를 해보자.", "Go back to Gonyong and show the photo.");

        if (talkedGonyong)
            return L("이번에는 엉인이와 대화해보자.", "Next, talk to Adult.");

        return L("먼저 고뇽이와 대화해보자.", "First, talk to Gonyong.");
    }

    private string GetExpectedMorningConversationId()
    {
        bool talkedGonyong = DialogueProgressState.HasCompletedConversation(GonyongConversationId);
        bool talkedAdult = DialogueProgressState.HasCompletedConversation(AdultConversationId);
        bool didPhotoEvent = DialogueProgressState.HasCompletedConversation(GonyongEventConversationId);

        if (didPhotoEvent)
            return string.Empty;

        if (talkedAdult)
            return GonyongEventConversationId;

        if (talkedGonyong)
            return AdultConversationId;

        return GonyongConversationId;
    }

    private static string L(string ko, string en)
    {
        if (LocalizationManager.Instance == null)
            return ko;

        return LocalizationManager.Instance.GetCurrentLanguage() == Language.Korean ? ko : en;
    }
}
