using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

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

    private PlayableGraph defaultPresentationGraph;
    private bool defaultPresentationSuppressed;

    private void Awake()
    {
        AutoResolveTargetAnimator();
    }

    private void OnEnable()
    {
        ResumeDefaultPresentation();
    }

    private void OnDisable()
    {
        StopDefaultPresentationImmediate();
    }

    private void OnDestroy()
    {
        StopDefaultPresentationImmediate();
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

        if (targetAnimator == null)
        {
            SpriteRenderer targetSprite = GetComponent<SpriteRenderer>();
            Transform targetTransform = transform;

            if (targetSprite == null)
            {
                targetSprite = GetComponentInChildren<SpriteRenderer>(true);
                if (targetSprite != null)
                    targetTransform = targetSprite.transform;
            }

            if (targetSprite == null)
            {
                targetSprite = GetComponentInParent<SpriteRenderer>();
                if (targetSprite != null)
                    targetTransform = targetSprite.transform;
            }

            if (targetSprite != null)
                targetAnimator = targetTransform.gameObject.GetComponent<Animator>() ?? targetTransform.gameObject.AddComponent<Animator>();
        }
    }

    public void SuspendDefaultPresentation()
    {
        defaultPresentationSuppressed = true;
        StopDefaultPresentationImmediate();
    }

    public void ResumeDefaultPresentation()
    {
        defaultPresentationSuppressed = false;
        AutoResolveTargetAnimator();

        if (!isActiveAndEnabled || targetAnimator == null)
            return;

        if (defaultAnimationClip != null)
        {
            if (defaultPresentationGraph.IsValid())
                return;

            defaultPresentationGraph = PlayableGraph.Create($"{name}_DefaultDialoguePresentation");
            var output = AnimationPlayableOutput.Create(defaultPresentationGraph, "DefaultDialoguePresentation", targetAnimator);
            var playable = AnimationClipPlayable.Create(defaultPresentationGraph, defaultAnimationClip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            output.SetSourcePlayable(playable);
            defaultPresentationGraph.Play();
            return;
        }

        if (!string.IsNullOrEmpty(defaultAnimationTrigger))
            targetAnimator.SetTrigger(defaultAnimationTrigger);
    }

    public void RefreshDefaultPresentation()
    {
        if (defaultPresentationSuppressed)
            return;

        StopDefaultPresentationImmediate();
        ResumeDefaultPresentation();
    }

    private void StopDefaultPresentationImmediate()
    {
        if (defaultPresentationGraph.IsValid())
            defaultPresentationGraph.Destroy();
    }

    public DialogueLinePresentation ToPresentation()
    {
        return new DialogueLinePresentation
        {
            lineID = string.Empty,
            lineIndexStart = -1,
            lineIndexEnd = -1,
            targetCharacterId = string.Empty,
            animationTrigger = defaultAnimationTrigger,
            animationClip = defaultAnimationClip,
            animationClipName = string.Empty,
            sneakersAnimationClip = null,
            sneakersAnimationClipName = string.Empty,
            soundEffectName = defaultSoundEffectName,
            beforeTextDelaySeconds = Mathf.Max(0f, defaultBeforeTextDelaySeconds)
        };
    }
}
