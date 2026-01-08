using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ContextualDialogue
{
    public int day;
    public GameState specificState;
    public DialogueBehavior behavior = DialogueBehavior.Repeatable;

    [Tooltip("단일 대화 ID (Repeatable, PlayOnce 사용 시)")]
    public string conversationID; // 예: "ROBOT_CONVO_DAY1"

    [Tooltip("랜덤 대화 ID 목록 (Random 사용 시, 이 중에서 랜덤 선택)")]
    public List<string> randomConversationIDs = new List<string>();

    [Header("대사 커스터마이징 (각 대사마다 개별 설정)")]
    [Tooltip("대화의 몇 번째 대사에 설정을 적용할지 (0부터 시작, -1이면 모든 대사에 적용 안 함)")]
    public int customLineIndex = -1;

    [Tooltip("해당 대사에 적용할 애니메이션 트리거 이름")]
    public string animationTrigger;

    [Tooltip("해당 대사에 재생할 소리 이펙트 이름 (Resources/Sounds에서 로드)")]
    public string soundEffectName;

    [Header("선택지 설정 (대화 중간에 선택지를 넣을 수 있음)")]
    [Tooltip("대화의 몇 번째 대사에 선택지를 넣을지 (0부터 시작, -1이면 마지막 대사)")]
    public int choiceLineIndex = -1; // -1이면 마지막 대사에 선택지

    [Tooltip("선택지 목록 (최대 4개)")]
    public List<DialogueChoice> choices = new List<DialogueChoice>();

    [HideInInspector]
    public bool hasBeenPlayed = false;
}

public enum DialogueBehavior { Repeatable, PlayOnce, Random }

/*
 * ===================================================================================
 * DialogueTrigger (v4.0 - Random 동작 구현)
 * ===================================================================================
 * - [v4.0 추가 기능]
 * - 1. Random 동작 구현 완료
 * - 2. 랜덤 대화 목록에서 선택
 * ===================================================================================
 */
public class DialogueTrigger : MonoBehaviour
{
    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.E;
    public GameObject interactPrompt;

    [Header("Contextual Dialogues")]
    public List<ContextualDialogue> contextualDialogues;

    [Header("Default Dialogue")]
    [Tooltip("상황에 맞는 대화가 없을 때 사용할 기본 대화")]
    public string defaultConversationID;

    private DialogueManager manager;
    private bool isPlayerInRange = false;
    private GameManager gameManager;

    void Start()
    {
        manager = FindObjectOfType<DialogueManager>();
        gameManager = FindObjectOfType<GameManager>();
        if (manager == null) UnityEngine.Debug.LogError("DialogueManager를 찾을 수 없습니다!");
        if (gameManager == null) UnityEngine.Debug.LogError("GameManager를 찾을 수 없습니다!");
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    void Update()
    {
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

    private ContextualDialogue FindCurrentDialogue()
    {
        int today = gameManager.currentDay;
        GameState now = gameManager.currentState;
        foreach (ContextualDialogue cd in contextualDialogues)
        {
            if (cd.day == today && cd.specificState == now) return cd;
        }
        return null; // 해당 상황에 없음
    }

    // 대화 동작에 따라 대화 시작
    private void StartDialogueBasedOnBehavior(ContextualDialogue cd)
    {
        string conversationID_ToPlay;

        // 1. 해당 상황에 맞는 대화가 없으면 '기본 대화 ID'를 사용
        if (cd == null)
        {
            conversationID_ToPlay = defaultConversationID;
        }
        // 2. 해당 상황에 맞는 '대화 ID'를 사용
        else
        {
            // Random 동작: 랜덤 대화 목록에서 선택
            if (cd.behavior == DialogueBehavior.Random)
            {
                if (cd.randomConversationIDs != null && cd.randomConversationIDs.Count > 0)
                {
                    int randomIndex = Random.Range(0, cd.randomConversationIDs.Count);
                    conversationID_ToPlay = cd.randomConversationIDs[randomIndex];
                }
                else if (!string.IsNullOrEmpty(cd.conversationID))
                {
                    // 랜덤 목록이 없으면 기본 대화 ID 사용
                    conversationID_ToPlay = cd.conversationID;
                }
                else
                {
                    conversationID_ToPlay = defaultConversationID;
                }
            }
            // Repeatable, PlayOnce 동작: 단일 대화 ID 사용
            else
            {
                conversationID_ToPlay = cd.conversationID;

                if (cd.behavior == DialogueBehavior.PlayOnce)
                {
                    cd.hasBeenPlayed = true;
                }
            }
        }

        // 3. '대화 ID'와 '대화 주인 NPC(transform)'를 DialogueManager에 전달
        if (!string.IsNullOrEmpty(conversationID_ToPlay))
        {
            // 대사 커스터마이징 적용 (애니메이션, 이펙트, 소리)
            if (cd != null && cd.customLineIndex >= 0)
            {
                ApplyLineCustomization(conversationID_ToPlay, cd.customLineIndex, cd);
            }

            // 선택지가 설정되어 있으면 대화에 선택지 추가
            if (cd != null && cd.choices != null && cd.choices.Count > 0)
            {
                AddChoicesToConversation(conversationID_ToPlay, cd.choiceLineIndex, cd.choices);
            }

            manager.StartDialogue(conversationID_ToPlay, transform);
        }
        else
        {
            UnityEngine.Debug.LogWarning("대화 ID가 비어있습니다!");
        }
    }

    // 대사에 커스터마이징 적용 (애니메이션, 이펙트, 소리)
    private void ApplyLineCustomization(string conversationID, int lineIndex, ContextualDialogue cd)
    {
        List<DialogueLine> lines = LocalizationManager.Instance.GetConversation(conversationID);
        
        if (lines == null || lines.Count == 0)
        {
            UnityEngine.Debug.LogWarning($"대화를 찾을 수 없습니다: {conversationID}");
            return;
        }

        if (lineIndex < 0 || lineIndex >= lines.Count)
        {
            UnityEngine.Debug.LogWarning($"대사 인덱스가 범위를 벗어났습니다: {lineIndex} / {lines.Count}");
            return;
        }

        DialogueLine targetLine = lines[lineIndex];

        // 애니메이션 트리거 설정
        if (!string.IsNullOrEmpty(cd.animationTrigger))
        {
            targetLine.animationTrigger = cd.animationTrigger;
        }

        // 소리 이펙트 설정
        if (!string.IsNullOrEmpty(cd.soundEffectName))
        {
            targetLine.soundEffectName = cd.soundEffectName;
        }

        UnityEngine.Debug.Log($"대화 '{conversationID}'의 {lineIndex + 1}번째 대사에 커스터마이징 적용됨");
    }

    // 대화에 선택지 추가
    private void AddChoicesToConversation(string conversationID, int lineIndex, List<DialogueChoice> choices)
    {
        List<DialogueLine> lines = LocalizationManager.Instance.GetConversation(conversationID);
        
        if (lines == null || lines.Count == 0)
        {
            UnityEngine.Debug.LogWarning($"대화를 찾을 수 없습니다: {conversationID}");
            return;
        }

        // lineIndex가 -1이면 마지막 대사에 선택지 추가
        int targetIndex = (lineIndex == -1) ? lines.Count - 1 : lineIndex;
        
        if (targetIndex < 0 || targetIndex >= lines.Count)
        {
            UnityEngine.Debug.LogWarning($"선택지를 넣을 대사 인덱스가 범위를 벗어났습니다: {targetIndex} / {lines.Count}");
            return;
        }

        // 선택지 추가
        DialogueLine targetLine = lines[targetIndex];
        targetLine.hasChoices = true;
        targetLine.choices = new List<DialogueChoice>(choices);

        UnityEngine.Debug.Log($"대화 '{conversationID}'의 {targetIndex + 1}번째 대사에 선택지 {choices.Count}개 추가됨");
    }

    private void OnTriggerEnter2D(Collider2D other) 
    { 
        if (other.CompareTag("Player")) isPlayerInRange = true; 
    }

    private void OnTriggerExit2D(Collider2D other) 
    { 
        if (other.CompareTag("Player")) isPlayerInRange = false; 
    }
}
