using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueChoice
{
    [Tooltip("선택지에 표시될 텍스트 ID (Localization.csv의 LINE_ID)")]
    public string choiceTextID;
    
    [Tooltip("선택 후 진행할 대화 ID (비어있으면 대화 종료)")]
    public string nextConversationID;
    
    [Tooltip("선택 후 이동할 씬 이름 (비어있으면 씬 이동 안 함)")]
    public string sceneToLoad;
    
    [Tooltip("선택 후 변경할 게임 상태 (None이면 상태 변경 안 함)")]
    public GameState stateToChange;
}

[System.Serializable]
public class DialogueLinePresentation
{
    [Tooltip("Target line ID from Localization/Conversations. Recommended when available.")]
    public string lineID;

    [Tooltip("Inclusive start line order (0-based) for applying this presentation to a line range.")]
    public int lineIndexStart = -1;

    [Tooltip("Inclusive end line order (0-based) for applying this presentation to a line range.")]
    public int lineIndexEnd = -1;

    [Tooltip("Optional line ID that triggers this presentation after that line completes.")]
    public string afterLineID;

    [Tooltip("Inclusive start completed line order (0-based) for after-line trigger ranges.")]
    public int afterLineIndexStart = -1;

    [Tooltip("Inclusive end completed line order (0-based) for after-line trigger ranges.")]
    public int afterLineIndexEnd = -1;

    [Tooltip("Optional character ID that should receive this presentation. Leave empty to use the speaking character.")]
    public string targetCharacterId;

    [Tooltip("Animator trigger to fire when this line is shown.")]
    public string animationTrigger;

    [Tooltip("Optional direct animation clip to play for this line.")]
    public AnimationClip animationClip;

    [Tooltip("Optional sound effect override played for this line.")]
    public string soundEffectName;

    [Tooltip("Delay before the text of this line starts showing. Useful for expression animation timing.")]
    public float beforeTextDelaySeconds = 0f;
}

[System.Serializable]
public class DialogueLine
{
    [Header("기본 정보")]
    public string speakerID; // "PLAYER", "FRIEND_A"
    public string lineID; // "LINE_ROBOT_01"

    [Header("애니메이션")]
    [Tooltip("대사 시작 시 재생할 애니메이션 트리거 이름 (비어있으면 애니메이션 재생 안 함)")]
    public string animationTrigger;

    [Header("소리 이펙트")]
    [Tooltip("대사 시작 시 재생할 소리 이펙트 이름 (Resources/Sounds에서 로드, 비어있으면 소리 재생 안 함)")]
    public string soundEffectName;

    [Header("선택지")]
    [Tooltip("이 대사에 선택지가 있는지 여부")]
    public bool hasChoices = false;
    
    [Tooltip("선택지 목록 (최대 4개)")]
    public List<DialogueChoice> choices = new List<DialogueChoice>();
}
