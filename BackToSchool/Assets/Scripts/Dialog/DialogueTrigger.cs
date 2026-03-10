using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[System.Serializable]
public class ContextualDialogue
{
    public int day;
    public GameState specificState;
    public DialogueBehavior behavior = DialogueBehavior.Repeatable;

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

            if (!manager.IsDialogueActive &&
                (currentDialogue == null || (currentDialogue.behavior != DialogueBehavior.PlayOnce || !currentDialogue.hasBeenPlayed)))
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
            if (cd.day == today && cd.specificState == now) return cd;
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

                if (cd.behavior == DialogueBehavior.PlayOnce)
                {
                    cd.hasBeenPlayed = true;
                }
            }
        }

        // 3. '???ID'? '???二쇱씤 NPC(transform)'瑜?DialogueManager???꾨떖
        if (!string.IsNullOrEmpty(conversationID_ToPlay))
        {
            // ???而ㅼ뒪?곕쭏?댁쭠 ?곸슜 (?좊땲硫붿씠?? ?댄럺?? ?뚮━)
            if (cd != null && cd.customLineIndex >= 0)
            {
                ApplyLineCustomization(conversationID_ToPlay, cd.customLineIndex, cd);
            }

            manager.StartDialogue(conversationID_ToPlay, transform);
        }
        else
        {
            UnityEngine.Debug.LogWarning("???ID媛 鍮꾩뼱?덉뒿?덈떎!");
        }
    }

    // ??ъ뿉 而ㅼ뒪?곕쭏?댁쭠 ?곸슜 (?좊땲硫붿씠?? ?댄럺?? ?뚮━)
    private void ApplyLineCustomization(string conversationID, int lineIndex, ContextualDialogue cd)
    {
        List<DialogueLine> lines = LocalizationManager.Instance.GetConversation(conversationID);
        
        if (lines == null || lines.Count == 0)
        {
            UnityEngine.Debug.LogWarning($"??붾? 李얠쓣 ???놁뒿?덈떎: {conversationID}");
            return;
        }

        if (lineIndex < 0 || lineIndex >= lines.Count)
        {
            UnityEngine.Debug.LogWarning($"????몃뜳?ㅺ? 踰붿쐞瑜?踰쀬뼱?ъ뒿?덈떎: {lineIndex} / {lines.Count}");
            return;
        }

        DialogueLine targetLine = lines[lineIndex];

        // ?좊땲硫붿씠???몃━嫄??ㅼ젙
        if (!string.IsNullOrEmpty(cd.animationTrigger))
        {
            targetLine.animationTrigger = cd.animationTrigger;
        }

        // ?뚮━ ?댄럺???ㅼ젙
        if (!string.IsNullOrEmpty(cd.soundEffectName))
        {
            targetLine.soundEffectName = cd.soundEffectName;
        }

        UnityEngine.Debug.Log($"Applied customization to {conversationID} line {lineIndex + 1}.");
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



