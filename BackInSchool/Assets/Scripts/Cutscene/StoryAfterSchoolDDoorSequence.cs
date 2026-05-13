using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class StoryAfterSchoolDDoorSequence : MonoBehaviour
{
    [Header("Target Conversation")]
    [SerializeField] private string conversationId = "D1_AfterSchool_D";
    [SerializeField] private string keyCueLineId = "D1_AfterSchool_D_28";
    [SerializeField] private string doorCloseCueLineId = "D1_AfterSchool_D_32";

    [Header("Timing")]
    [SerializeField] private float keySoundDelaySeconds = 1f;
    [SerializeField] private float autoAdvanceDelayAfterKeySeconds = 1f;
    [SerializeField] private float doorCloseDelaySeconds = 0.8f;

    [Header("Door Target")]
    [SerializeField] private Transform doorCloseTarget;
    [SerializeField] private string doorCloseObjectName = "DoorClose";
    [SerializeField] private string alternateDoorCloseObjectName = "Door_Close";
    [SerializeField] private AnimationClip doorOpenClip;
    [SerializeField] private AnimationClip doorCloseClip;

    [Header("Audio")]
    [SerializeField] private AudioClip keySound;
    [SerializeField] [Range(0f, 1f)] private float keySoundVolume = 1f;
    [SerializeField] private AudioClip doorCloseSound;
    [SerializeField] [Range(0f, 1f)] private float doorCloseSoundVolume = 0.4f;

    private AudioSource audioSource;
    private Coroutine keySequenceCoroutine;
    private Coroutine doorCloseCoroutine;
    private bool handledKeySequence;
    private bool handledDoorClose;
    private bool initializedDoorOpen;

    private void Awake()
    {
        EnsureAudioSource();
        EnsureDoorStartsOpen();
    }

    private void OnEnable()
    {
        DialogueManager.DialogueLineShown += HandleDialogueLineShown;
        ResetIfConversationChanged();
        EnsureDoorStartsOpen();
    }

    private void OnDisable()
    {
        DialogueManager.DialogueLineShown -= HandleDialogueLineShown;

        if (keySequenceCoroutine != null)
            StopCoroutine(keySequenceCoroutine);
        if (doorCloseCoroutine != null)
            StopCoroutine(doorCloseCoroutine);

        keySequenceCoroutine = null;
        doorCloseCoroutine = null;
    }

    private void Update()
    {
        ResetIfConversationChanged();
        EnsureDoorStartsOpen();
    }

    private void HandleDialogueLineShown(string shownConversationId, string lineId)
    {
        if (!string.Equals(shownConversationId, conversationId, System.StringComparison.OrdinalIgnoreCase))
            return;

        if (string.Equals(lineId, keyCueLineId, System.StringComparison.OrdinalIgnoreCase) && !handledKeySequence)
        {
            handledKeySequence = true;
            if (keySequenceCoroutine != null)
                StopCoroutine(keySequenceCoroutine);
            keySequenceCoroutine = StartCoroutine(CoPlayKeyAndAdvance());
            return;
        }

        if (string.Equals(lineId, doorCloseCueLineId, System.StringComparison.OrdinalIgnoreCase) && !handledDoorClose)
        {
            handledDoorClose = true;
            var dialogueManager = FindAnyObjectByType<DialogueManager>();
            if (dialogueManager != null)
                dialogueManager.BlockAdvanceForSeconds(doorCloseDelaySeconds + 0.1f);
            if (doorCloseCoroutine != null)
                StopCoroutine(doorCloseCoroutine);
            doorCloseCoroutine = StartCoroutine(CoPlayDoorClose());
        }
    }

    private IEnumerator CoPlayKeyAndAdvance()
    {
        var dialogueManager = FindAnyObjectByType<DialogueManager>();
        if (dialogueManager != null)
            dialogueManager.BlockAdvanceForSeconds(keySoundDelaySeconds + autoAdvanceDelayAfterKeySeconds + 0.1f);

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, keySoundDelaySeconds));
        PlayOneShot(keySound, keySoundVolume);

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, autoAdvanceDelayAfterKeySeconds));

        if (dialogueManager != null &&
            dialogueManager.IsDialogueActive &&
            string.Equals(dialogueManager.CurrentConversationId, conversationId, System.StringComparison.OrdinalIgnoreCase) &&
            string.Equals(dialogueManager.CurrentLineId, keyCueLineId, System.StringComparison.OrdinalIgnoreCase))
        {
            dialogueManager.DisplayNextSentence();
        }

        keySequenceCoroutine = null;
    }

    private IEnumerator CoPlayDoorClose()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, doorCloseDelaySeconds));

        Transform target = ResolveDoorCloseTarget();
        PlayOneShot(doorCloseSound, doorCloseSoundVolume);
        if (target != null && doorCloseClip != null)
            yield return StartCoroutine(CoSampleClip(target, doorCloseClip));

        doorCloseCoroutine = null;
    }

    private IEnumerator CoSampleClip(Transform target, AnimationClip clip)
    {
        if (target == null || clip == null)
            yield break;

        Animator animator = FindAnimatorOwner(target);
        bool hadAnimator = animator != null;
        bool previousAnimatorEnabled = hadAnimator && animator.enabled;
        if (hadAnimator)
            animator.enabled = false;

        float duration = Mathf.Max(clip.length, 0.0001f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            clip.SampleAnimation(target.gameObject, elapsed);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        clip.SampleAnimation(target.gameObject, duration);

        if (hadAnimator)
            animator.enabled = previousAnimatorEnabled;
    }

    private void EnsureDoorStartsOpen()
    {
        if (initializedDoorOpen)
            return;

        if (doorOpenClip == null)
            return;

        Transform target = ResolveDoorCloseTarget();
        if (target == null)
            return;

        Animator animator = FindAnimatorOwner(target);
        bool hadAnimator = animator != null;
        bool previousAnimatorEnabled = hadAnimator && animator.enabled;
        if (hadAnimator)
            animator.enabled = false;

        doorOpenClip.SampleAnimation(target.gameObject, Mathf.Max(doorOpenClip.length, 0f));

        if (hadAnimator)
            animator.enabled = previousAnimatorEnabled;

        initializedDoorOpen = true;
    }

    private Transform ResolveDoorCloseTarget()
    {
        if (doorCloseTarget != null)
            return ResolveRenderableDoorTarget(doorCloseTarget);

        Transform found = FindTransformByName(doorCloseObjectName);
        if (found != null)
        {
            doorCloseTarget = found;
            return ResolveRenderableDoorTarget(doorCloseTarget);
        }

        found = FindTransformByName(alternateDoorCloseObjectName);
        if (found != null)
        {
            doorCloseTarget = found;
            return ResolveRenderableDoorTarget(doorCloseTarget);
        }

        Animator[] animators = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
                continue;

            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == null)
                continue;

            string controllerName = controller.name ?? string.Empty;
            if (controllerName.IndexOf("DoorClose", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                controllerName.IndexOf("Door_Close", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                doorCloseTarget = animator.transform;
                return ResolveRenderableDoorTarget(doorCloseTarget);
            }
        }

        return null;
    }

    private static Transform ResolveRenderableDoorTarget(Transform target)
    {
        if (target == null)
            return null;

        if (target.GetComponent<SpriteRenderer>() != null)
            return target;

        SpriteRenderer childRenderer = target.GetComponentInChildren<SpriteRenderer>(true);
        if (childRenderer != null)
            return childRenderer.transform;

        return target;
    }

    private static Transform FindTransformByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform transform = allTransforms[i];
            if (transform != null &&
                string.Equals(transform.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return transform;
            }
        }

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform transform = allTransforms[i];
            if (transform != null &&
                transform.name.IndexOf(objectName, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return transform;
            }
        }

        return null;
    }

    private static Animator FindAnimatorOwner(Transform target)
    {
        if (target == null)
            return null;

        Animator animator = target.GetComponent<Animator>();
        if (animator != null)
            return animator;

        animator = target.GetComponentInParent<Animator>();
        if (animator != null)
            return animator;

        return target.GetComponentInChildren<Animator>(true);
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        EnsureAudioSource();
        if (audioSource == null || clip == null)
            return;

        audioSource.PlayOneShot(clip, AudioSettingsService.ScaleSfx(Mathf.Clamp01(volume)));
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
            return;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
    }

    private void ResetIfConversationChanged()
    {
        string activeConversationId = FlowContext.CurrentId;
        if (string.Equals(activeConversationId, conversationId, System.StringComparison.OrdinalIgnoreCase))
            return;

        handledKeySequence = false;
        handledDoorClose = false;
        initializedDoorOpen = false;
    }
}
