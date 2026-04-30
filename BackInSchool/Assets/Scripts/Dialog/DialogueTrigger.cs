using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

[System.Serializable]
public class ContextualDialogue
{
    public int day;
    public GameState specificState;
    public DialogueBehavior behavior = DialogueBehavior.Repeatable;
    [Tooltip("Optional completed conversation required before this dialogue can be selected.")]
    public string requiredCompletedConversationID;

    [Tooltip("?⑥씪 ???ID (Repeatable, PlayOnce ?ъ슜 ??")]
    public string conversationID; // ?? "ROBOT_CONVO_DAY1"

    [Tooltip("?쒕뜡 ???ID 紐⑸줉 (Random ?ъ슜 ?? ??以묒뿉???쒕뜡 ?좏깮)")]
    public List<string> randomConversationIDs = new List<string>();

    [Header("???而ㅼ뒪?곕쭏?댁쭠 (媛???щ쭏??媛쒕퀎 ?ㅼ젙)")]
    [Tooltip("??붿쓽 紐?踰덉㎏ ??ъ뿉 ?ㅼ젙???곸슜?좎? (0遺???쒖옉, -1?대㈃ 紐⑤뱺 ??ъ뿉 ?곸슜 ????")]
    public int customLineIndex = -1;

    [Tooltip("?대떦 ??ъ뿉 ?곸슜???좊땲硫붿씠???몃━嫄??대쫫")]
    public string animationTrigger;

    [Tooltip("?대떦 ??ъ뿉 ?ъ깮???뚮━ ?댄럺???대쫫 (Resources/Sounds?먯꽌 濡쒕뱶)")]
    public string soundEffectName;

    [Header("Line Presentations")]
    [Tooltip("Inspector-driven per-line presentation settings. Use lineID or lineIndexStart~lineIndexEnd to match dialogue lines.")]
    public List<DialogueLinePresentation> linePresentations = new List<DialogueLinePresentation>();

    [Header("Story Scene Override")]
    public bool playInStoryScene = false;
    public string storyConversationID;
    public string returnSceneName = "FREEROAM";
    public bool preserveReturnPosition = true;
    public bool preserveLunchClock = true;

    [HideInInspector]
    public bool hasBeenPlayed = false;
}

public enum DialogueBehavior { Repeatable, PlayOnce, Random }

/*
 * ===================================================================================
 * DialogueTrigger (v4.0 - Random ?숈옉 援ы쁽)
 * ===================================================================================
 * - [v4.0 異붽? 湲곕뒫]
 * - 1. Random ?숈옉 援ы쁽 ?꾨즺
 * - 2. ?쒕뜡 ???紐⑸줉?먯꽌 ?좏깮
 * ===================================================================================
 */
public class DialogueTrigger : MonoBehaviour
{
    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.E;
    public GameObject interactPrompt;
    public TMP_Text interactKeyText;
    public string interactKeyFormat = "[{0}]";
    public float promptFontSize = 4f;
    [SerializeField] private bool useUnifiedPromptStyle = true;
    [SerializeField] private TMP_FontAsset promptFontAsset;
    [SerializeField] private bool autoCreatePromptWhenMissing = true;
    [SerializeField] private Vector3 autoPromptLocalOffset = new Vector3(0f, 0.9f, 0f);
    [SerializeField] private float autoPromptScale = 1f;
    [SerializeField] private int autoPromptSortingOrder = 50;

    [Header("Contextual Dialogues")]
    public List<ContextualDialogue> contextualDialogues;

    [Header("Default Dialogue")]
    [Tooltip("Fallback conversation ID used when no contextual dialogue matches.")]
    public string defaultConversationID;

    [Header("Facing")]
    [Tooltip("When enabled, this trigger owner will face toward the player before dialogue starts.")]
    [SerializeField] private bool facePlayerOnDialogueStart = false;
    [Tooltip("Optional transform to flip instead of the DialogueTrigger transform.")]
    [SerializeField] private Transform facingRoot;

    private DialogueManager manager;
    private bool isPlayerInRange = false;
    private GameManager gameManager;
    private KeyCode lastInteractKey = KeyCode.None;
    private static TMP_FontAsset cachedPromptFont;

    private void Awake()
    {
        EnsureInteractPromptBinding();
    }

    void Start()
    {
        manager = FindObjectOfType<DialogueManager>();
        gameManager = FindObjectOfType<GameManager>();
        if (manager == null) UnityEngine.Debug.LogError("DialogueManager를 찾을 수 없습니다!");
        if (gameManager == null) UnityEngine.Debug.LogError("GameManager를 찾을 수 없습니다!");
        EnsureInteractPromptBinding();
        if (interactPrompt != null) interactPrompt.SetActive(false);
        RefreshInteractPromptText(KeyBindingConfig.Get(KeyBindingConfig.InteractKey, KeyCode.E));
    }

    void Update()
    {
        interactKey = KeyBindingConfig.Get(KeyBindingConfig.InteractKey, KeyCode.E);
        if (interactKey != lastInteractKey)
            RefreshInteractPromptText(interactKey);

        if (isPlayerInRange)
        {
            ContextualDialogue currentDialogue = FindCurrentDialogue();
            string currentConversationId = ResolveConversationId(currentDialogue);
            bool playOnceAlreadyCompleted = currentDialogue != null &&
                                           currentDialogue.behavior == DialogueBehavior.PlayOnce &&
                                           DialogueProgressState.HasCompletedConversation(currentDialogue.conversationID);
            bool tutorialAllowsDialogue = Day1TutorialController.IsDialogueConversationAllowed(currentConversationId);

            if (!manager.IsDialogueActive &&
                tutorialAllowsDialogue &&
                (currentDialogue == null || currentDialogue.behavior != DialogueBehavior.PlayOnce || !playOnceAlreadyCompleted))
            {
                if (interactPrompt != null) interactPrompt.SetActive(true);

                if (Input.GetKeyDown(interactKey) && !manager.inputConsumedThisFrame)
                {
                    StartDialogueBasedOnBehavior(currentDialogue);
                }
            }
            else
            {
                if (interactPrompt != null) interactPrompt.SetActive(false);
            }
        }
        else
        {
            if (interactPrompt != null) interactPrompt.SetActive(false);
        }
    }

    private string ResolveConversationId(ContextualDialogue dialogue)
    {
        if (dialogue != null)
        {
            switch (dialogue.behavior)
            {
                case DialogueBehavior.Repeatable:
                case DialogueBehavior.PlayOnce:
                    return dialogue.conversationID;
                case DialogueBehavior.Random:
                    if (dialogue.randomConversationIDs != null && dialogue.randomConversationIDs.Count > 0)
                        return dialogue.randomConversationIDs[0];
                    break;
            }
        }

        return defaultConversationID;
    }

    private void RefreshInteractPromptText(KeyCode key)
    {
        lastInteractKey = key;
        if (interactKeyText == null)
            return;

        ApplyPromptFont(interactKeyText);
        ApplyPromptVisualStyle(interactKeyText);
        interactKeyText.enableWordWrapping = false;
        interactKeyText.overflowMode = TextOverflowModes.Overflow;
        interactKeyText.text = string.Format(interactKeyFormat, key.ToString().ToUpperInvariant());
    }

    private void EnsureInteractPromptBinding()
    {
        if (interactPrompt == null && autoCreatePromptWhenMissing)
        {
            var go = new GameObject("__AutoKeyPrompt", typeof(TextMeshPro));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = autoPromptLocalOffset;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = useUnifiedPromptStyle
                ? Vector3.one
                : Vector3.one * Mathf.Max(1f, autoPromptScale);
            interactPrompt = go;
        }

        if (interactPrompt == null)
            return;

        if (interactKeyText == null)
            interactKeyText = interactPrompt.GetComponentInChildren<TMP_Text>(true);

        if (interactKeyText == null && autoCreatePromptWhenMissing)
        {
            var tmp = interactPrompt.GetComponent<TextMeshPro>();
            if (tmp == null)
                tmp = interactPrompt.AddComponent<TextMeshPro>();

            interactKeyText = tmp;
        }

        ConfigurePromptText(interactKeyText);
        SyncPromptFacing();
    }

    private void ConfigurePromptText(TMP_Text tmp)
    {
        if (tmp == null)
            return;

        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.alignment = TextAlignmentOptions.Center;
        ApplyPromptVisualStyle(tmp);
        ApplyPromptFont(tmp);

        // TextMeshPro (world text) sorting order fallback.
        if (tmp is TextMeshPro worldText)
            worldText.sortingOrder = autoPromptSortingOrder;
    }

    private void ApplyPromptVisualStyle(TMP_Text tmp)
    {
        if (tmp == null)
            return;

        float fontSize = useUnifiedPromptStyle ? InteractionPromptStyle.DefaultFontSize : promptFontSize;
        float worldScale = useUnifiedPromptStyle ? InteractionPromptStyle.DefaultWorldScale : Mathf.Max(0.01f, autoPromptScale);

        tmp.fontSize = fontSize;
        InteractionPromptStyle.ApplyWorldTextScale(tmp, worldScale);
    }

    private void ApplyPromptFont(TMP_Text tmp)
    {
        if (tmp == null)
            return;

        TMP_FontAsset font = ResolvePromptFont(tmp);
        if (font == null)
            return;

        tmp.font = font;

        if (tmp is TextMeshPro worldText && font.material != null)
            worldText.fontSharedMaterial = font.material;
    }

    private TMP_FontAsset ResolvePromptFont(TMP_Text current)
    {
        if (promptFontAsset != null)
            return promptFontAsset;

        if (cachedPromptFont != null)
            return cachedPromptFont;

        TMP_FontAsset[] loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < loaded.Length; i++)
        {
            TMP_FontAsset f = loaded[i];
            if (f == null || string.IsNullOrEmpty(f.name))
                continue;

            if (f.name.Equals("Galmuri11-Bold SDF", System.StringComparison.OrdinalIgnoreCase) ||
                f.name.IndexOf("Galmuri11-Bold", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                cachedPromptFont = f;
                return f;
            }
        }

        TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text t = texts[i];
            if (t == null || t.font == null || string.IsNullOrEmpty(t.font.name))
                continue;

            string n = t.font.name;
            if (n.Equals("Galmuri11-Bold SDF", System.StringComparison.OrdinalIgnoreCase) ||
                n.IndexOf("Galmuri11-Bold", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                cachedPromptFont = t.font;
                return cachedPromptFont;
            }
        }

        if (current != null && current.font != null)
            return current.font;

        cachedPromptFont = TMP_Settings.defaultFontAsset;
        return cachedPromptFont;
    }

    private ContextualDialogue FindCurrentDialogue()
    {
        int today = gameManager.currentDay;
        GameState now = gameManager.currentState;
        foreach (ContextualDialogue cd in contextualDialogues)
        {
            if (cd.day != today || cd.specificState != now)
                continue;

            if (!string.IsNullOrWhiteSpace(cd.requiredCompletedConversationID) &&
                !DialogueProgressState.HasCompletedConversation(cd.requiredCompletedConversationID))
            {
                continue;
            }

            return cd;
        }
        return null; // ?대떦 ?곹솴???놁쓬
    }

    // ????숈옉???곕씪 ????쒖옉
    private void StartDialogueBasedOnBehavior(ContextualDialogue cd)
    {
        string conversationID_ToPlay;

        // 1. ?대떦 ?곹솴??留욌뒗 ??붽? ?놁쑝硫?'湲곕낯 ???ID'瑜??ъ슜
        if (cd == null)
        {
            conversationID_ToPlay = defaultConversationID;
        }
        // 2. ?대떦 ?곹솴??留욌뒗 '???ID'瑜??ъ슜
        else
        {
            // Random ?숈옉: ?쒕뜡 ???紐⑸줉?먯꽌 ?좏깮
            if (cd.behavior == DialogueBehavior.Random)
            {
                if (cd.randomConversationIDs != null && cd.randomConversationIDs.Count > 0)
                {
                    int randomIndex = Random.Range(0, cd.randomConversationIDs.Count);
                    conversationID_ToPlay = cd.randomConversationIDs[randomIndex];
                }
                else if (!string.IsNullOrEmpty(cd.conversationID))
                {
                    // ?쒕뜡 紐⑸줉???놁쑝硫?湲곕낯 ???ID ?ъ슜
                    conversationID_ToPlay = cd.conversationID;
                }
                else
                {
                    conversationID_ToPlay = defaultConversationID;
                }
            }
            // Repeatable, PlayOnce ?숈옉: ?⑥씪 ???ID ?ъ슜
            else
            {
                conversationID_ToPlay = cd.conversationID;
            }
        }

        // 3. '???ID'? '???二쇱씤 NPC(transform)'瑜?DialogueManager???꾨떖
        if (!string.IsNullOrEmpty(conversationID_ToPlay))
        {
            if (facePlayerOnDialogueStart)
                FaceTowardPlayer();

            if (cd != null && cd.playInStoryScene)
            {
                string storyConversationId = !string.IsNullOrWhiteSpace(cd.storyConversationID)
                    ? cd.storyConversationID
                    : conversationID_ToPlay;
                string targetReturnScene = !string.IsNullOrWhiteSpace(cd.returnSceneName)
                    ? cd.returnSceneName
                    : SceneManager.GetActiveScene().name;

                TemporaryStorySceneFlow.Begin(storyConversationId, targetReturnScene, cd.preserveReturnPosition, cd.preserveLunchClock);
                return;
            }

            if (manager != null)
                manager.SetUpcomingLinePresentations(BuildLinePresentations(cd));

            manager.StartDialogue(conversationID_ToPlay, transform);
        }
        else
        {
            UnityEngine.Debug.LogWarning("???ID媛 鍮꾩뼱?덉뒿?덈떎!");
        }
    }

    private void FaceTowardPlayer()
    {
        Transform player = ResolvePlayerTransform();
        Transform subject = facingRoot != null ? facingRoot : transform;
        if (player == null || subject == null)
            return;

        float deltaX = player.position.x - subject.position.x;
        if (Mathf.Abs(deltaX) <= 0.01f)
            return;

        Vector3 scale = subject.localScale;
        float absX = Mathf.Abs(scale.x);
        if (absX <= 0.0001f)
            absX = 1f;

        scale.x = deltaX >= 0f ? -absX : absX;
        subject.localScale = scale;
        SyncPromptFacing();
    }

    private Transform ResolvePlayerTransform()
    {
        if (manager != null && manager.playerController != null)
            return manager.playerController.transform;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    private void SyncPromptFacing()
    {
        if (interactPrompt == null)
            return;

        Transform subject = facingRoot != null ? facingRoot : transform;
        Transform prompt = interactPrompt.transform;
        if (subject == null || !prompt.IsChildOf(subject))
            return;

        Vector3 promptScale = prompt.localScale;
        float absX = Mathf.Abs(promptScale.x);
        if (absX <= 0.0001f)
            absX = 1f;

        float subjectSign = Mathf.Sign(subject.localScale.x);
        if (Mathf.Approximately(subjectSign, 0f))
            subjectSign = 1f;

        promptScale.x = absX * subjectSign;
        prompt.localScale = promptScale;
    }

    private List<DialogueLinePresentation> BuildLinePresentations(ContextualDialogue cd)
    {
        var result = new List<DialogueLinePresentation>();
        if (cd == null)
            return result;

        if (cd.linePresentations != null)
        {
            for (int i = 0; i < cd.linePresentations.Count; i++)
            {
                var src = cd.linePresentations[i];
                if (src == null)
                    continue;

                result.Add(new DialogueLinePresentation
                {
                    lineID = src.lineID,
                    lineIndexStart = src.lineIndexStart,
                    lineIndexEnd = src.lineIndexEnd,
                    targetCharacterId = src.targetCharacterId,
                    animationTrigger = src.animationTrigger,
                    animationClip = src.animationClip,
                    animationClipName = src.animationClipName,
                    sneakersAnimationClip = src.sneakersAnimationClip,
                    sneakersAnimationClipName = src.sneakersAnimationClipName,
                    soundEffectName = src.soundEffectName,
                    beforeTextDelaySeconds = Mathf.Max(0f, src.beforeTextDelaySeconds)
                });
            }
        }

        if (cd.customLineIndex >= 0 || !string.IsNullOrEmpty(cd.animationTrigger) || !string.IsNullOrEmpty(cd.soundEffectName))
        {
            result.Add(new DialogueLinePresentation
            {
                lineIndexStart = cd.customLineIndex,
                lineIndexEnd = cd.customLineIndex,
                targetCharacterId = string.Empty,
                animationTrigger = cd.animationTrigger,
                soundEffectName = cd.soundEffectName,
                beforeTextDelaySeconds = 0f
            });
        }

        return result;
    }

    private void OnTriggerEnter2D(Collider2D other) 
    { 
        if (other.CompareTag("Player")) isPlayerInRange = true; 
    }

    private void OnTriggerExit2D(Collider2D other) 
    { 
        if (other.CompareTag("Player")) isPlayerInRange = false; 
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }
}



