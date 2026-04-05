using UnityEngine;

[DisallowMultipleComponent]
public class DialogueCharacterPresentation : MonoBehaviour
{
    [Header("Default Dialogue Presentation")]
    [Tooltip("Optional explicit animator used for dialogue presentation. Leave empty to auto-find from this character.")]
    public Animator targetAnimator;

    [Tooltip("Default animation clip played when this character speaks.")]
    public AnimationClip defaultAnimationClip;

    [Tooltip("Fallback default trigger used when no clip is assigned.")]
    public string defaultAnimationTrigger;

    [Tooltip("Default delay before the text appears for this character's lines.")]
    public float defaultBeforeTextDelaySeconds = 0f;

    [Tooltip("Optional default sound effect override for this character's lines.")]
    public string defaultSoundEffectName;

    private void Awake()
    {
        AutoResolveTargetAnimator();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoResolveTargetAnimator();
    }
#endif

    private void AutoResolveTargetAnimator()
    {
        if (targetAnimator != null)
            return;

        targetAnimator = GetComponent<Animator>();
        if (targetAnimator == null)
            targetAnimator = GetComponentInChildren<Animator>(true);
        if (targetAnimator == null)
            targetAnimator = GetComponentInParent<Animator>();
    }

    public DialogueLinePresentation ToPresentation()
    {
        return new DialogueLinePresentation
        {
            lineID = string.Empty,
            lineIndex = -1,
            animationTrigger = defaultAnimationTrigger,
            animationClip = defaultAnimationClip,
            soundEffectName = defaultSoundEffectName,
            beforeTextDelaySeconds = Mathf.Max(0f, defaultBeforeTextDelaySeconds)
        };
    }
}
