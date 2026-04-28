using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueChoice
{
    [Tooltip("Choice text ID from Localization.csv (LINE_ID).")]
    public string choiceTextID;

    [Tooltip("Next conversation ID triggered by this choice. Empty ends the dialogue.")]
    public string nextConversationID;

    [Tooltip("Optional scene name loaded by this choice.")]
    public string sceneToLoad;

    [Tooltip("Optional game state change applied by this choice.")]
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

    [Tooltip("Optional character ID that should receive this presentation. Leave empty to use the speaking character.")]
    public string targetCharacterId;

    [Tooltip("Animator trigger to fire when this line is shown.")]
    public string animationTrigger;

    [Tooltip("Optional direct animation clip to play for this line.")]
    public AnimationClip animationClip;

    [Tooltip("Optional direct animation clip name/key loaded at runtime.")]
    public string animationClipName;

    [Tooltip("Optional animation clip used when the target character is wearing sneakers / not using slippers.")]
    public AnimationClip sneakersAnimationClip;

    [Tooltip("Optional sneakers animation clip name/key loaded at runtime.")]
    public string sneakersAnimationClipName;

    [Tooltip("Optional sound effect override played for this line.")]
    public string soundEffectName;

    [Tooltip("Delay before the text of this line starts showing. Useful for expression animation timing.")]
    public float beforeTextDelaySeconds = 0f;
}

[System.Serializable]
public class DialogueLine
{
    [Header("Basic")]
    public string speakerID;
    public string lineID;

    [Header("Presentation")]
    [Tooltip("Animator trigger fired for this line.")]
    public string animationTrigger;

    [Tooltip("Optional target character ID for this line presentation. Leave empty to apply to the speaker.")]
    public string targetCharacterId;

    [Tooltip("Optional direct animation clip name/key for this line.")]
    public string animationClipName;

    [Tooltip("Optional sneakers animation clip name/key for this line.")]
    public string sneakersAnimationClipName;

    [Tooltip("Optional sound effect name/path for this line.")]
    public string soundEffectName;

    [Tooltip("Delay before the text of this line starts showing.")]
    public float beforeTextDelaySeconds = 0f;

    [Header("CSV Presentations")]
    public List<DialogueLinePresentation> csvPresentations = new List<DialogueLinePresentation>();

    [Header("Choices")]
    public bool hasChoices = false;
    public List<DialogueChoice> choices = new List<DialogueChoice>();
}
