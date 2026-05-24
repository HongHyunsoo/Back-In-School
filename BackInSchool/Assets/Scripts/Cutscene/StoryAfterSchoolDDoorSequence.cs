using System.Collections;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class StoryAfterSchoolDDoorSequence : MonoBehaviour
{
    [System.Serializable]
    private class MoveInstruction
    {
        public Transform actor = null;
        public Vector2 targetLocalPosition = Vector2.zero;
        public bool instant = false;
        [Min(0.01f)] public float durationSeconds = 0.6f;
    }

    [Header("Target Conversation")]
    [SerializeField] private string conversationId = "D1_AfterSchool_D";
    [SerializeField] private string appearanceCueLineId = "D1_AfterSchool_D_03";
    [SerializeField] private string moveCue07LineId = "D1_AfterSchool_D_07";
    [SerializeField] private string keyCueLineId = "D1_AfterSchool_D_28";
    [SerializeField] private string doorCloseCueLineId = "D1_AfterSchool_D_32";
    [SerializeField] private string moveCue31LineId = "D1_AfterSchool_D_31";
    [SerializeField] private string moveCue35LineId = "D1_AfterSchool_D_35";
    [SerializeField] private string flipCue37LineId = "D1_AfterSchool_D_37";
    [SerializeField] private string afterSchoolFConversationId = "D1_AfterSchool_F";
    [SerializeField] private string subwayAlarmCueLineId = "D1_AfterSchool_F_30";
    [SerializeField] private string subwayPassingCueLineId = "D1_AfterSchool_F_33";

    [Header("Timing")]
    [SerializeField] private float keySoundDelaySeconds = 1f;
    [SerializeField] private float autoAdvanceDelayAfterKeySeconds = 1f;
    [SerializeField] private float doorCloseDelaySeconds = 0.8f;
    [SerializeField] private float line31SilenceSeconds = 4f;
    [SerializeField] private float line35SilenceSeconds = 2f;
    [SerializeField] private float line31DoorPassingDelaySeconds = 3f;
    [SerializeField] private float line31AdvanceDelayAfterPassingSeconds = 3f;
    [SerializeField] private float keyFadeOutSeconds = 1f;
    [SerializeField] private float autoAdvancedLineInputCooldownSeconds = 0.35f;

    [Header("Door Target")]
    [SerializeField] private Transform doorCloseTarget;
    [SerializeField] private string doorCloseObjectName = "DoorClose";
    [SerializeField] private string alternateDoorCloseObjectName = "Door_Close";
    [SerializeField] private AnimationClip doorOpenClip;
    [SerializeField] private AnimationClip doorCloseClip;
    [SerializeField] private AnimationClip doorPassingClip;
    [SerializeField] [Range(0.05f, 2f)] private float doorPassingSpeed = 0.3f;
    [SerializeField] private string doorSpriteResourcePath = "Object/DoorClose";
    [SerializeField] private float manualDoorFrameSeconds = 0.08f;

    [Header("Audio")]
    [SerializeField] private AudioClip keySound;
    [SerializeField] [Range(0f, 1f)] private float keySoundVolume = 1f;
    [SerializeField] private AudioClip doorCloseSound;
    [SerializeField] [Range(0f, 1f)] private float doorCloseSoundVolume = 0.4f;
    [SerializeField] private AudioClip subwayApproachAlarmSound;
    [SerializeField] [Range(0f, 1f)] private float subwayApproachAlarmVolume = 1f;
    [SerializeField] private AudioClip subwayPassingSound;
    [SerializeField] [Range(0f, 1f)] private float subwayPassingVolume = 1f;
    [SerializeField] private float subwayPassingFadeDelaySeconds = 3f;
    [SerializeField] private float subwayPassingFadeOutSeconds = 1.2f;

    [Header("Subway Approach")]
    [SerializeField] private Transform subwayApproachTarget;
    [SerializeField] private string subwayApproachObjectName = "Subway_approach";
    [SerializeField] private AnimationClip subwayApproachOnceClip;
    [SerializeField] private AnimationClip subwayApproachLoopClip;
    [SerializeField] private Sprite[] subwayApproachOnceSprites;
    [SerializeField] private Sprite[] subwayApproachLoopSprites;
    [SerializeField] private float subwayApproachFrameSeconds = 0.083333f;
    [SerializeField] [Range(0.05f, 2f)] private float subwayApproachSpeed = 1f;

    [Header("Appearance")]
    [SerializeField] private Transform appearanceActor;

    [Header("Moves")]
    [SerializeField] private MoveInstruction[] movesAfterLine31;
    [SerializeField] private MoveInstruction[] movesAfterLine35;
    [SerializeField] private MoveInstruction[] movesAfterLine07;
    [SerializeField] private AnimationClip playerWalkClip;
    [SerializeField] private RuntimeAnimatorController playerWalkController;
    [SerializeField] private Sprite[] playerWalkSprites;

    private AudioSource audioSource;
    private AudioSource keyLoopSource;
    private AudioSource subwayPassingSource;
    private Coroutine keySequenceCoroutine;
    private Coroutine doorCloseCoroutine;
    private Coroutine line31MoveCoroutine;
    private Coroutine line35MoveCoroutine;
    private Coroutine line07MoveCoroutine;
    private Coroutine keyFadeCoroutine;
    private Coroutine subwayPassingFadeCoroutine;
    private Coroutine subwayApproachCoroutine;
    private bool handledKeySequence;
    private bool handledDoorClose;
    private bool handledMove31;
    private bool handledMove35;
    private bool handledMove07;
    private bool handledAppearance;
    private bool handledFlip37;
    private bool handledSubwayAlarm;
    private bool handledSubwayPassing;
    private bool initializedDoorOpen;
    private bool conversationPrepared;
    private bool afterSchoolFPrepared;
    private Sprite[] cachedDoorSprites;
    private Animator cachedDoorAnimator;
    private bool cachedDoorAnimatorState;

    private void Awake()
    {
        EnsureAudioSource();
        EnsureDoorStartsOpen();
        SetSubwayApproachVisible(false);
    }

    private void OnEnable()
    {
        DialogueManager.DialogueLineShown += HandleDialogueLineShown;
        ResetIfConversationChanged();
        PrepareConversationStateIfNeeded();
    }

    private void OnDisable()
    {
        DialogueManager.DialogueLineShown -= HandleDialogueLineShown;

        if (keySequenceCoroutine != null)
            StopCoroutine(keySequenceCoroutine);
        if (doorCloseCoroutine != null)
            StopCoroutine(doorCloseCoroutine);
        if (line31MoveCoroutine != null)
            StopCoroutine(line31MoveCoroutine);
        if (line35MoveCoroutine != null)
            StopCoroutine(line35MoveCoroutine);
        if (line07MoveCoroutine != null)
            StopCoroutine(line07MoveCoroutine);
        if (keyFadeCoroutine != null)
            StopCoroutine(keyFadeCoroutine);
        if (subwayPassingFadeCoroutine != null)
            StopCoroutine(subwayPassingFadeCoroutine);
        if (subwayApproachCoroutine != null)
            StopCoroutine(subwayApproachCoroutine);

        keySequenceCoroutine = null;
        doorCloseCoroutine = null;
        line31MoveCoroutine = null;
        line35MoveCoroutine = null;
        line07MoveCoroutine = null;
        keyFadeCoroutine = null;
        subwayPassingFadeCoroutine = null;
        subwayApproachCoroutine = null;
        StopSubwayPassingImmediate();
    }

    private void Update()
    {
        ResetIfConversationChanged();
        PrepareConversationStateIfNeeded();
        PrepareAfterSchoolFStateIfNeeded();
    }

    private void HandleDialogueLineShown(string shownConversationId, string lineId)
    {
        if (string.Equals(shownConversationId, afterSchoolFConversationId, System.StringComparison.OrdinalIgnoreCase))
        {
            HandleAfterSchoolFLine(lineId);
            return;
        }

        if (!string.Equals(shownConversationId, conversationId, System.StringComparison.OrdinalIgnoreCase))
            return;

        PrepareConversationStateIfNeeded();

        if (string.Equals(lineId, appearanceCueLineId, System.StringComparison.OrdinalIgnoreCase) && !handledAppearance)
        {
            handledAppearance = true;
            SetAppearanceActorVisible(true);
        }

        if (string.Equals(lineId, moveCue07LineId, System.StringComparison.OrdinalIgnoreCase) && !handledMove07)
        {
            handledMove07 = true;
            StartMoves(movesAfterLine07);
        }

        if (string.Equals(lineId, keyCueLineId, System.StringComparison.OrdinalIgnoreCase) && !handledKeySequence)
        {
            handledKeySequence = true;
            if (keySequenceCoroutine != null)
                StopCoroutine(keySequenceCoroutine);
            keySequenceCoroutine = StartCoroutine(CoPlayKeyAndAdvance());
            return;
        }

        if (string.Equals(lineId, moveCue31LineId, System.StringComparison.OrdinalIgnoreCase) && !handledMove31)
        {
            handledMove31 = true;
            if (line31MoveCoroutine != null)
                StopCoroutine(line31MoveCoroutine);
            line31MoveCoroutine = StartCoroutine(CoLine31Sequence());
            return;
        }

        if (string.Equals(lineId, doorCloseCueLineId, System.StringComparison.OrdinalIgnoreCase) && !handledDoorClose)
        {
            handledDoorClose = true;
            var dialogueManager = FindAnyObjectByType<DialogueManager>();
            if (dialogueManager != null)
                dialogueManager.BlockAdvanceForSeconds(doorCloseDelaySeconds + 0.1f);
            StartKeyFadeOut();
            if (doorCloseCoroutine != null)
                StopCoroutine(doorCloseCoroutine);
            doorCloseCoroutine = StartCoroutine(CoPlayDoorClose());
            return;
        }

        if (string.Equals(lineId, moveCue35LineId, System.StringComparison.OrdinalIgnoreCase) && !handledMove35)
        {
            handledMove35 = true;
            if (line35MoveCoroutine != null)
                StopCoroutine(line35MoveCoroutine);
            line35MoveCoroutine = StartCoroutine(CoMoveThenAdvance(line35SilenceSeconds, movesAfterLine35, moveCue35LineId, true));
            return;
        }

        if (string.Equals(lineId, flipCue37LineId, System.StringComparison.OrdinalIgnoreCase) && !handledFlip37)
        {
            handledFlip37 = true;
            FlipAppearanceActor();
        }
    }

    private void HandleAfterSchoolFLine(string lineId)
    {
        if (string.Equals(lineId, subwayAlarmCueLineId, System.StringComparison.OrdinalIgnoreCase) && !handledSubwayAlarm)
        {
            handledSubwayAlarm = true;
            PlayOneShot(subwayApproachAlarmSound, subwayApproachAlarmVolume);
            return;
        }

        if (string.Equals(lineId, subwayPassingCueLineId, System.StringComparison.OrdinalIgnoreCase) && !handledSubwayPassing)
        {
            handledSubwayPassing = true;
            var dialogueManager = FindAnyObjectByType<DialogueManager>();
            if (dialogueManager != null)
                dialogueManager.BlockAdvanceForSeconds(2f);
            StartSubwayPassingSound();
            StartSubwayApproachAnimation();
        }
    }

    private IEnumerator CoPlayKeyAndAdvance()
    {
        var dialogueManager = FindAnyObjectByType<DialogueManager>();
        if (dialogueManager != null)
            dialogueManager.BlockAdvanceForSeconds(keySoundDelaySeconds + autoAdvanceDelayAfterKeySeconds + 0.1f);

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, keySoundDelaySeconds));
        StartKeyLoop();

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, autoAdvanceDelayAfterKeySeconds));

        if (dialogueManager != null &&
            dialogueManager.IsDialogueActive &&
            string.Equals(dialogueManager.CurrentConversationId, conversationId, System.StringComparison.OrdinalIgnoreCase) &&
            string.Equals(dialogueManager.CurrentLineId, keyCueLineId, System.StringComparison.OrdinalIgnoreCase))
        {
            dialogueManager.DisplayNextSentence();
            dialogueManager.BlockAdvanceForSeconds(autoAdvancedLineInputCooldownSeconds, true);
        }

        keySequenceCoroutine = null;
    }

    private IEnumerator CoMoveThenAdvance(float silenceSeconds, MoveInstruction[] moves, string expectedLineId, bool stopKeyLoopBeforeAdvance)
    {
        var dialogueManager = FindAnyObjectByType<DialogueManager>();
        if (dialogueManager != null)
            dialogueManager.BlockAdvanceForSeconds(silenceSeconds + 0.1f);

        StartMoves(moves);

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, silenceSeconds));

        if (stopKeyLoopBeforeAdvance)
            StopKeyLoopImmediate();

        if (dialogueManager != null &&
            dialogueManager.IsDialogueActive &&
            string.Equals(dialogueManager.CurrentConversationId, conversationId, System.StringComparison.OrdinalIgnoreCase) &&
            string.Equals(dialogueManager.CurrentLineId, expectedLineId, System.StringComparison.OrdinalIgnoreCase))
        {
            dialogueManager.DisplayNextSentence();
            dialogueManager.BlockAdvanceForSeconds(autoAdvancedLineInputCooldownSeconds, true);
        }

        if (string.Equals(expectedLineId, moveCue31LineId, System.StringComparison.OrdinalIgnoreCase))
            line31MoveCoroutine = null;
        else if (string.Equals(expectedLineId, moveCue35LineId, System.StringComparison.OrdinalIgnoreCase))
            line35MoveCoroutine = null;
    }

    private IEnumerator CoLine31Sequence()
    {
        var dialogueManager = FindAnyObjectByType<DialogueManager>();
        float passingDuration = doorPassingClip != null
            ? Mathf.Max(doorPassingClip.length / Mathf.Max(0.01f, doorPassingSpeed), 0f)
            : 0f;
        float totalDelay = Mathf.Max(0f, line31DoorPassingDelaySeconds) + passingDuration + Mathf.Max(0f, line31AdvanceDelayAfterPassingSeconds);

        if (dialogueManager != null)
        {
            dialogueManager.BlockAdvanceForSeconds(totalDelay + 0.1f);
            dialogueManager.SetSpeechBubbleVisible(false);
        }

        StartMoves(movesAfterLine31);

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, line31DoorPassingDelaySeconds));

        Transform target = ResolveDoorCloseTarget();
        if (target != null && doorPassingClip != null)
            yield return StartCoroutine(CoSampleClip(target, doorPassingClip, doorPassingSpeed));

        StopKeyLoopImmediate();

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, line31AdvanceDelayAfterPassingSeconds));

        if (dialogueManager != null &&
            dialogueManager.IsDialogueActive &&
            string.Equals(dialogueManager.CurrentConversationId, conversationId, System.StringComparison.OrdinalIgnoreCase) &&
            string.Equals(dialogueManager.CurrentLineId, moveCue31LineId, System.StringComparison.OrdinalIgnoreCase))
        {
            dialogueManager.DisplayNextSentence();
            dialogueManager.BlockAdvanceForSeconds(autoAdvancedLineInputCooldownSeconds, true);
        }

        if (dialogueManager != null)
            dialogueManager.SetSpeechBubbleVisible(true);

        line31MoveCoroutine = null;
    }

    private IEnumerator CoPlayDoorClose()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, doorCloseDelaySeconds));

        Transform target = ResolveDoorCloseTarget();
        PlayOneShot(doorCloseSound, doorCloseSoundVolume);
        if (target != null)
        {
            if (HasManualDoorSprites())
                yield return StartCoroutine(CoPlayDoorSprites(target));
            else if (doorCloseClip != null)
                yield return StartCoroutine(CoSampleClip(target, doorCloseClip));
        }

        doorCloseCoroutine = null;
    }

    private IEnumerator CoSampleClip(Transform target, AnimationClip clip, float playbackSpeed = 1f)
    {
        if (target == null || clip == null)
            yield break;

        Animator animator = FindAnimatorOwner(target);
        bool hadAnimator = animator != null;
        bool previousAnimatorEnabled = hadAnimator && animator.enabled;
        if (hadAnimator)
            animator.enabled = false;

        float duration = Mathf.Max(clip.length, 0.0001f);
        float speed = Mathf.Max(0.01f, playbackSpeed);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            clip.SampleAnimation(target.gameObject, elapsed);
            elapsed += Time.unscaledDeltaTime * speed;
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

        Transform target = ResolveDoorCloseTarget();
        if (target == null)
            return;

        if (ApplyOpenDoorSprite(target))
        {
            initializedDoorOpen = true;
            return;
        }

        if (doorOpenClip == null)
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

    private bool ApplyOpenDoorSprite(Transform target)
    {
        if (target == null || !HasManualDoorSprites())
            return false;

        SpriteRenderer spriteRenderer = target.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = target.GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer == null)
            return false;

        spriteRenderer.sprite = cachedDoorSprites[0];
        return true;
    }

    private bool HasManualDoorSprites()
    {
        if (cachedDoorSprites != null)
            return cachedDoorSprites.Length > 0;

        cachedDoorSprites = Resources.LoadAll<Sprite>(doorSpriteResourcePath)
            .OrderBy(sprite => sprite.name)
            .ToArray();
        return cachedDoorSprites.Length > 0;
    }

    private IEnumerator CoPlayDoorSprites(Transform target)
    {
        if (target == null || !HasManualDoorSprites())
            yield break;

        SpriteRenderer spriteRenderer = target.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = target.GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer == null)
            yield break;

        CacheAndDisableDoorAnimator(target);

        for (int i = 0; i < cachedDoorSprites.Length; i++)
        {
            spriteRenderer.sprite = cachedDoorSprites[i];
            if (i < cachedDoorSprites.Length - 1)
                yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, manualDoorFrameSeconds));
        }
    }

    private void CacheAndDisableDoorAnimator(Transform target)
    {
        Animator animator = FindAnimatorOwner(target);
        if (animator == null)
            return;

        cachedDoorAnimator = animator;
        cachedDoorAnimatorState = animator.enabled;
        animator.enabled = false;
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

    private Transform ResolveSubwayApproachTarget()
    {
        if (subwayApproachTarget != null)
            return subwayApproachTarget;

        Transform found = FindTransformByName(subwayApproachObjectName);
        if (found != null)
        {
            subwayApproachTarget = found;
            return subwayApproachTarget;
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
        {
            EnsureKeyLoopSource();
            EnsureSubwayPassingSource();
            return;
        }

        AudioSource[] sources = GetComponents<AudioSource>();
        audioSource = sources.FirstOrDefault();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;

        EnsureKeyLoopSource();
        EnsureSubwayPassingSource();
    }

    private void EnsureKeyLoopSource()
    {
        if (keyLoopSource != null)
            return;

        AudioSource[] sources = GetComponents<AudioSource>();
        keyLoopSource = sources.FirstOrDefault(source => source != audioSource);
        if (keyLoopSource == null)
            keyLoopSource = gameObject.AddComponent<AudioSource>();

        keyLoopSource.playOnAwake = false;
        keyLoopSource.loop = true;
        keyLoopSource.spatialBlend = 0f;
        keyLoopSource.ignoreListenerPause = true;
    }

    private void EnsureSubwayPassingSource()
    {
        if (subwayPassingSource != null)
            return;

        AudioSource[] sources = GetComponents<AudioSource>();
        subwayPassingSource = sources.FirstOrDefault(source => source != audioSource && source != keyLoopSource);
        if (subwayPassingSource == null)
            subwayPassingSource = gameObject.AddComponent<AudioSource>();

        subwayPassingSource.playOnAwake = false;
        subwayPassingSource.loop = false;
        subwayPassingSource.spatialBlend = 0f;
        subwayPassingSource.ignoreListenerPause = true;
    }

    private void StartKeyLoop()
    {
        EnsureAudioSource();
        if (keyLoopSource == null || keySound == null)
            return;

        if (keyFadeCoroutine != null)
        {
            StopCoroutine(keyFadeCoroutine);
            keyFadeCoroutine = null;
        }

        keyLoopSource.clip = keySound;
        keyLoopSource.volume = AudioSettingsService.ScaleSfx(Mathf.Clamp01(keySoundVolume));
        if (!keyLoopSource.isPlaying)
            keyLoopSource.Play();
    }

    private void StartKeyFadeOut()
    {
        EnsureAudioSource();
        if (keyLoopSource == null || !keyLoopSource.isPlaying)
            return;

        if (keyFadeCoroutine != null)
            StopCoroutine(keyFadeCoroutine);
        keyFadeCoroutine = StartCoroutine(CoFadeOutKeyLoop());
    }

    private IEnumerator CoFadeOutKeyLoop()
    {
        if (keyLoopSource == null)
            yield break;

        float startVolume = keyLoopSource.volume;
        float duration = Mathf.Max(0.01f, keyFadeOutSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            keyLoopSource.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        StopKeyLoopImmediate();
        keyFadeCoroutine = null;
    }

    private void StopKeyLoopImmediate()
    {
        if (keyLoopSource == null)
            return;

        keyLoopSource.Stop();
        keyLoopSource.clip = null;
        keyLoopSource.volume = 0f;
    }

    private void StartSubwayPassingSound()
    {
        EnsureAudioSource();
        if (subwayPassingSource == null || subwayPassingSound == null)
            return;

        if (subwayPassingFadeCoroutine != null)
        {
            StopCoroutine(subwayPassingFadeCoroutine);
            subwayPassingFadeCoroutine = null;
        }

        subwayPassingSource.Stop();
        subwayPassingSource.clip = subwayPassingSound;
        subwayPassingSource.volume = AudioSettingsService.ScaleSfx(Mathf.Clamp01(subwayPassingVolume));
        subwayPassingSource.Play();
        subwayPassingFadeCoroutine = StartCoroutine(CoFadeOutSubwayPassing());
    }

    private IEnumerator CoFadeOutSubwayPassing()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, subwayPassingFadeDelaySeconds));

        if (subwayPassingSource == null)
            yield break;

        float startVolume = subwayPassingSource.volume;
        float duration = Mathf.Max(0.01f, subwayPassingFadeOutSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            subwayPassingSource.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        StopSubwayPassingImmediate();
        subwayPassingFadeCoroutine = null;
    }

    private void StopSubwayPassingImmediate()
    {
        if (subwayPassingSource == null)
            return;

        subwayPassingSource.Stop();
        subwayPassingSource.clip = null;
        subwayPassingSource.volume = 0f;
    }

    private void StartSubwayApproachAnimation()
    {
        Transform target = ResolveSubwayApproachTarget();
        if (target == null)
            return;

        SetSubwayApproachVisible(true);

        if (subwayApproachCoroutine != null)
            StopCoroutine(subwayApproachCoroutine);

        subwayApproachCoroutine = StartCoroutine(CoPlaySubwayApproach(target));
    }

    private IEnumerator CoPlaySubwayApproach(Transform target)
    {
        if (target == null)
            yield break;

        SpriteRenderer spriteRenderer = target.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = target.GetComponentInChildren<SpriteRenderer>(true);

        if (spriteRenderer != null && subwayApproachOnceSprites != null && subwayApproachOnceSprites.Length > 0)
        {
            for (int i = 0; i < subwayApproachOnceSprites.Length; i++)
            {
                if (subwayApproachOnceSprites[i] != null)
                    spriteRenderer.sprite = subwayApproachOnceSprites[i];
                yield return new WaitForSecondsRealtime(GetSubwayApproachFrameSeconds());
            }

            while (target != null && subwayApproachLoopSprites != null && subwayApproachLoopSprites.Length > 0)
            {
                for (int i = 0; i < subwayApproachLoopSprites.Length; i++)
                {
                    if (subwayApproachLoopSprites[i] != null)
                        spriteRenderer.sprite = subwayApproachLoopSprites[i];
                    yield return new WaitForSecondsRealtime(GetSubwayApproachFrameSeconds());
                }
            }

            subwayApproachCoroutine = null;
            yield break;
        }

        if (subwayApproachOnceClip != null)
        {
            float onceDuration = Mathf.Max(subwayApproachOnceClip.length, 0.0001f);
            float onceElapsed = 0f;
            float speed = Mathf.Max(0.01f, subwayApproachSpeed);

            while (target != null && onceElapsed < onceDuration)
            {
                subwayApproachOnceClip.SampleAnimation(target.gameObject, onceElapsed);
                onceElapsed += Time.unscaledDeltaTime * speed;
                yield return null;
            }

            if (target != null)
                subwayApproachOnceClip.SampleAnimation(target.gameObject, onceDuration);
        }

        if (target != null && subwayApproachLoopClip != null)
            subwayApproachLoopClip.SampleAnimation(target.gameObject, 0f);

        float loopElapsed = 0f;
        while (target != null && subwayApproachLoopClip != null)
        {
            float loopDuration = Mathf.Max(subwayApproachLoopClip.length, 0.0001f);
            float sampleTime = loopElapsed % loopDuration;
            subwayApproachLoopClip.SampleAnimation(target.gameObject, sampleTime);
            loopElapsed += Time.unscaledDeltaTime * Mathf.Max(0.01f, subwayApproachSpeed);
            yield return null;
        }

        subwayApproachCoroutine = null;
    }

    private float GetSubwayApproachFrameSeconds()
    {
        return Mathf.Max(0.01f, subwayApproachFrameSeconds / Mathf.Max(0.01f, subwayApproachSpeed));
    }

    private void StartMoves(MoveInstruction[] moves)
    {
        if (moves == null)
            return;

        for (int i = 0; i < moves.Length; i++)
        {
            MoveInstruction move = moves[i];
            if (move == null || move.actor == null)
                continue;

            if (move.instant)
            {
                Vector3 localPosition = move.actor.localPosition;
                localPosition.x = move.targetLocalPosition.x;
                localPosition.y = move.targetLocalPosition.y;
                move.actor.localPosition = localPosition;
                continue;
            }

            StartCoroutine(CoMoveActor(move.actor, move.targetLocalPosition, move.durationSeconds));
        }
    }

    private IEnumerator CoMoveActor(Transform actor, Vector2 targetLocalPosition, float durationSeconds)
    {
        if (actor == null)
            yield break;

        string characterId = ResolveCharacterId(actor);
        Animator animator = FindAnimatorOwner(actor);
        SpriteRenderer spriteRenderer = actor.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = actor.GetComponentInChildren<SpriteRenderer>(true);
        Sprite originalSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        Vector3 startPosition = actor.localPosition;
        Vector3 targetPosition = new Vector3(targetLocalPosition.x, targetLocalPosition.y, startPosition.z);
        ApplyFacing(actor, targetPosition.x - startPosition.x);

        AnimationClip walkClip = ResolveWalkClip(characterId, animator);
        bool useDirectPlayerSprites =
            string.Equals(characterId, "NAME_PLAYER", System.StringComparison.OrdinalIgnoreCase) &&
            spriteRenderer != null &&
            playerWalkSprites != null &&
            playerWalkSprites.Length > 0;
        bool sampledWalk = false;
        bool previousAnimatorEnabled = false;
        float walkElapsed = 0f;

        if (useDirectPlayerSprites)
        {
            if (animator != null)
            {
                previousAnimatorEnabled = animator.enabled;
                animator.enabled = false;
            }
            sampledWalk = false;
        }
        else if (walkClip != null)
        {
            if (animator != null)
            {
                previousAnimatorEnabled = animator.enabled;
                animator.enabled = false;
            }
            sampledWalk = true;
        }

        float duration = Mathf.Max(0.01f, durationSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            actor.localPosition = Vector3.Lerp(startPosition, targetPosition, Mathf.Clamp01(elapsed / duration));
            if (useDirectPlayerSprites)
            {
                walkElapsed += Time.unscaledDeltaTime;
                int frameIndex = Mathf.FloorToInt((walkElapsed / 0.12f)) % playerWalkSprites.Length;
                spriteRenderer.sprite = playerWalkSprites[frameIndex];
            }
            else if (sampledWalk)
            {
                walkElapsed += Time.unscaledDeltaTime;
                float sampleTime = walkClip.length > 0f ? walkElapsed % walkClip.length : 0f;
                walkClip.SampleAnimation(actor.gameObject, sampleTime);
            }
            yield return null;
        }

        actor.localPosition = targetPosition;

        if (sampledWalk && animator != null)
        {
            animator.enabled = previousAnimatorEnabled;
            if (previousAnimatorEnabled)
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }

        if (useDirectPlayerSprites)
        {
            if (spriteRenderer != null && originalSprite != null)
                spriteRenderer.sprite = originalSprite;

            if (animator != null)
            {
                animator.enabled = previousAnimatorEnabled;
                if (previousAnimatorEnabled)
                {
                    animator.Rebind();
                    animator.Update(0f);
                }
            }
        }
    }

    private AnimationClip ResolveWalkClip(string characterId, Animator animator)
    {
        if (string.Equals(characterId, "NAME_PLAYER", System.StringComparison.OrdinalIgnoreCase) && playerWalkClip != null)
            return playerWalkClip;

        return FindControllerClip(animator, "Walk");
    }

    private static string ResolveCharacterId(Transform actor)
    {
        if (actor == null)
            return string.Empty;

        CharacterIdentifier identifier = actor.GetComponent<CharacterIdentifier>();
        if (identifier == null)
            identifier = actor.GetComponentInParent<CharacterIdentifier>();
        if (identifier == null)
            identifier = actor.GetComponentInChildren<CharacterIdentifier>(true);

        return identifier != null ? identifier.characterID : actor.name;
    }

    private static AnimationClip FindControllerClip(Animator animator, string keyword)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(keyword))
            return null;

        return animator.runtimeAnimatorController.animationClips
            .FirstOrDefault(clip => clip != null &&
                                    clip.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void ApplyFacing(Transform actor, float deltaX)
    {
        if (actor == null || Mathf.Abs(deltaX) <= 0.001f)
            return;

        Vector3 scale = actor.localScale;
        float absX = Mathf.Abs(scale.x);
        if (absX <= 0.001f)
            absX = 1f;

        scale.x = deltaX > 0f ? absX : -absX;
        actor.localScale = scale;
    }

    private void PrepareConversationStateIfNeeded()
    {
        if (!string.Equals(FlowContext.CurrentId, conversationId, System.StringComparison.OrdinalIgnoreCase))
            return;

        if (conversationPrepared)
            return;

        EnsureDoorStartsOpen();
        SetAppearanceActorVisible(false);
        conversationPrepared = true;
    }

    private void PrepareAfterSchoolFStateIfNeeded()
    {
        if (!string.Equals(FlowContext.CurrentId, afterSchoolFConversationId, System.StringComparison.OrdinalIgnoreCase))
            return;

        if (afterSchoolFPrepared)
            return;

        SetSubwayApproachVisible(false);
        afterSchoolFPrepared = true;
    }

    private void SetAppearanceActorVisible(bool visible)
    {
        if (appearanceActor == null)
            return;

        if (appearanceActor.gameObject.activeSelf != visible)
            appearanceActor.gameObject.SetActive(visible);
    }

    private void FlipAppearanceActor()
    {
        if (appearanceActor == null)
            return;

        Vector3 scale = appearanceActor.localScale;
        scale.x *= -1f;
        appearanceActor.localScale = scale;
    }

    private void SetSubwayApproachVisible(bool visible)
    {
        Transform target = ResolveSubwayApproachTarget();
        if (target == null)
            return;

        if (target.gameObject.activeSelf != visible)
            target.gameObject.SetActive(visible);
    }

    private void ResetIfConversationChanged()
    {
        string activeConversationId = FlowContext.CurrentId;
        if (string.Equals(activeConversationId, conversationId, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(activeConversationId, afterSchoolFConversationId, System.StringComparison.OrdinalIgnoreCase))
            return;

        handledKeySequence = false;
        handledDoorClose = false;
        handledMove31 = false;
        handledMove35 = false;
        handledMove07 = false;
        handledAppearance = false;
        handledFlip37 = false;
        handledSubwayAlarm = false;
        handledSubwayPassing = false;
        initializedDoorOpen = false;
        conversationPrepared = false;
        afterSchoolFPrepared = false;
        StopKeyLoopImmediate();
        StopSubwayPassingImmediate();
        if (subwayPassingFadeCoroutine != null)
        {
            StopCoroutine(subwayPassingFadeCoroutine);
            subwayPassingFadeCoroutine = null;
        }
        if (subwayApproachCoroutine != null)
        {
            StopCoroutine(subwayApproachCoroutine);
            subwayApproachCoroutine = null;
        }
        SetAppearanceActorVisible(true);
    }
}
