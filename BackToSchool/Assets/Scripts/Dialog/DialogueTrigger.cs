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

    [Header("?좏깮吏 ?ㅼ젙 (???以묎컙???좏깮吏瑜??ｌ쓣 ???덉쓬)")]
    [Tooltip("??붿쓽 紐?踰덉㎏ ??ъ뿉 ?좏깮吏瑜??ｌ쓣吏 (0遺???쒖옉, -1?대㈃ 留덉?留????")]
    public int choiceLineIndex = -1; // -1?대㈃ 留덉?留???ъ뿉 ?좏깮吏

    [Tooltip("?좏깮吏 紐⑸줉 (理쒕? 4媛?")]
    public List<DialogueChoice> choices = new List<DialogueChoice>();

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

    [Header("Contextual Dialogues")]
    public List<ContextualDialogue> contextualDialogues;

    [Header("Default Dialogue")]
    [Tooltip("Fallback conversation ID used when no contextual dialogue matches.")]
    public string defaultConversationID;

    private DialogueManager manager;
    private bool isPlayerInRange = false;
    private GameManager gameManager;
    private KeyCode lastInteractKey = KeyCode.None;

    void Start()
    {
        manager = FindObjectOfType<DialogueManager>();
        gameManager = FindObjectOfType<GameManager>();
        if (manager == null) UnityEngine.Debug.LogError("DialogueManager를 찾을 수 없습니다!");
        if (gameManager == null) UnityEngine.Debug.LogError("GameManager를 찾을 수 없습니다!");
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

        interactKeyText.enableWordWrapping = false;
        interactKeyText.overflowMode = TextOverflowModes.Overflow;
        interactKeyText.fontSize = promptFontSize;
        interactKeyText.text = string.Format(interactKeyFormat, key.ToString().ToUpperInvariant());
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

            // ?좏깮吏媛 ?ㅼ젙?섏뼱 ?덉쑝硫???붿뿉 ?좏깮吏 異붽?
            if (cd != null && cd.choices != null && cd.choices.Count > 0)
            {
                AddChoicesToConversation(conversationID_ToPlay, cd.choiceLineIndex, cd.choices);
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

    // ??붿뿉 ?좏깮吏 異붽?
    private void AddChoicesToConversation(string conversationID, int lineIndex, List<DialogueChoice> choices)
    {
        List<DialogueLine> lines = LocalizationManager.Instance.GetConversation(conversationID);
        
        if (lines == null || lines.Count == 0)
        {
            UnityEngine.Debug.LogWarning($"??붾? 李얠쓣 ???놁뒿?덈떎: {conversationID}");
            return;
        }

        // lineIndex媛 -1?대㈃ 留덉?留???ъ뿉 ?좏깮吏 異붽?
        int targetIndex = (lineIndex == -1) ? lines.Count - 1 : lineIndex;
        
        if (targetIndex < 0 || targetIndex >= lines.Count)
        {
            UnityEngine.Debug.LogWarning($"?좏깮吏瑜??ｌ쓣 ????몃뜳?ㅺ? 踰붿쐞瑜?踰쀬뼱?ъ뒿?덈떎: {targetIndex} / {lines.Count}");
            return;
        }

        // ?좏깮吏 異붽?
        DialogueLine targetLine = lines[targetIndex];
        targetLine.hasChoices = true;
        targetLine.choices = new List<DialogueChoice>(choices);

        UnityEngine.Debug.Log($"Added {choices.Count} choices to {conversationID} line {targetIndex + 1}.");
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



