using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;

/*
 * ===================================================================================
 * DialogueManager (v4.0 - 확장된 대화 시스템)
 * ===================================================================================
 * - [v4.0 추가 기능]
 * - 1. 캐릭터 애니메이션 재생
 * - 2. 타이핑 효과
 * - 3. 소리 이펙트 재생
 * - 4. CharacterIdentifier 캐싱으로 성능 개선
 * ===================================================================================
 */
public class DialogueManager : MonoBehaviour
{
    public static event Action<string, string> DialogueLineShown;
    public static event Action<string> DialogueConversationCompleted;

    [SerializeField] private CutsceneCommandRunner commandRunner;
    private bool isBusy; // 연출 진행중이면 true

    [Header("UI (Speech Bubble Prefab)")]
    [SerializeField] private SpeechBubbleUI speechBubblePrefab;
    [SerializeField] private Transform speechBubbleParent;

    private SpeechBubbleUI speechBubble; // runtime instance
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI dialogueText;
    public float typingSpeed = 0.08f;
    public Vector3 worldOffset = new Vector3(0, 0.5f, 0);
    [SerializeField] private float bubbleScreenYOffset = 54f;
    public PlayerController playerController;
    public KeyCode nextSentenceKey = KeyCode.E;

    [Header("오디오")]
    public AudioSource audioSource; // 소리 이펙트 재생용

    private Queue<DialogueLine> lines;
    private bool isTyping = false;
    private DialogueLine currentLine;
    public bool IsDialogueActive { get; private set; }
    public bool inputConsumedThisFrame { get; private set; } = false;
    public string CurrentConversationId { get; private set; } = string.Empty;
    public string CurrentLineId => currentLine != null ? currentLine.lineID : string.Empty;
    private GameManager gameManager;
    private Transform currentSpeaker;
    private Transform currentBubbleSpeaker;
    private Transform currentNpcSpeaker;

    // 성능 개선: CharacterIdentifier/CharacterActor 캐싱
    private Dictionary<string, CharacterIdentifier> characterCache = new Dictionary<string, CharacterIdentifier>();
    private Dictionary<string, Transform> actorCache = new Dictionary<string, Transform>();
    private bool blockAdvanceInputThisFrame = false;
    private readonly List<DialogueLinePresentation> pendingLinePresentations = new List<DialogueLinePresentation>();
    private readonly List<DialogueLinePresentation> activeLinePresentations = new List<DialogueLinePresentation>();
    private int currentLineIndex = -1;
    private PlayableGraph presentationGraph;
    private Animator activePresentationAnimator;
    private AnimationClip activePresentationClip;
    private string activePresentationTrigger = string.Empty;

    private void StopPlayerMotionImmediate()
    {
        Transform t = null;
        if (playerController != null)
            t = playerController.transform;
        if (t == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) t = p.transform;
        }
        if (t == null)
            return;

        var rb = t.GetComponent<Rigidbody2D>();
        if (rb == null)
            return;

        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;

        var anim = t.GetComponent<Animator>();
        if (anim == null)
            return;

        // Force immediate visual state sync so run animation does not remain while dialogue starts.
        SetAnimatorIfExists(anim, "moveSpeed", 0f);
        SetAnimatorIfExists(anim, "yVelocity", 0f);
        SetAnimatorIfExists(anim, "isGrounded", true);
    }

    private static void SetAnimatorIfExists(Animator anim, string paramName, float value)
    {
        if (anim == null || string.IsNullOrEmpty(paramName))
            return;

        int hash = Animator.StringToHash(paramName);
        var ps = anim.parameters;
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].nameHash == hash && ps[i].type == AnimatorControllerParameterType.Float)
            {
                anim.SetFloat(hash, value);
                return;
            }
        }
    }

    private static void SetAnimatorIfExists(Animator anim, string paramName, bool value)
    {
        if (anim == null || string.IsNullOrEmpty(paramName))
            return;

        int hash = Animator.StringToHash(paramName);
        var ps = anim.parameters;
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].nameHash == hash && ps[i].type == AnimatorControllerParameterType.Bool)
            {
                anim.SetBool(hash, value);
                return;
            }
        }
    }

    void Start()
    {
        lines = new Queue<DialogueLine>();

        RebindForScene();


        IsDialogueActive = false;
        gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null) UnityEngine.Debug.LogError("GameManager를 찾을 수 없습니다!");

        // CharacterIdentifier 캐싱
        RefreshCharacterCache();
    }

    private void OnDestroy()
    {
        StopPresentationAnimationImmediate();
    }

    void Update()
    {
        if (!IsDialogueActive) return;

        nextSentenceKey = KeyBindingConfig.Get(KeyBindingConfig.InteractKey, KeyCode.E);

        if (blockAdvanceInputThisFrame) return;

        if (isBusy) return;

        if (Input.GetKeyDown(nextSentenceKey) || Input.GetMouseButtonDown(0))
        {
            inputConsumedThisFrame = true;
            DisplayNextSentence();
        }
    }


    void LateUpdate()
    {
        if (IsDialogueActive && currentBubbleSpeaker != null && speechBubble != null)
        {
            var cam = Camera.main;
            if (cam == null) return;

            // speechBubbleParent 기준으로 Canvas 찾기
            var canvas = speechBubbleParent != null
                ? speechBubbleParent.GetComponentInParent<Canvas>()
                : FindSceneCanvasInActiveScene();

            if (canvas == null)
            {
                canvas = EnsureRuntimeDialogueCanvas();
                if (canvas == null) return;

                speechBubbleParent = canvas.transform;
                speechBubble.transform.SetParent(speechBubbleParent, false);
            }

            Vector3 targetPos = currentBubbleSpeaker.position + worldOffset;
            Vector3 screenPos = cam.WorldToScreenPoint(targetPos);

            // 카메라 뒤면 숨김
            if (screenPos.z < 0f)
            {
                speechBubble.gameObject.SetActive(false);
            }
            else
            {
                speechBubble.gameObject.SetActive(true);

                RectTransform canvasRect = canvas.transform as RectTransform;
                RectTransform bubbleRect = speechBubble.transform as RectTransform;

                // Overlay면 uiCam = null, Camera/World면 worldCamera 필요
                Camera uiCam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCam, out var localPoint))
                {
                    // Snap to pixel to reduce sub-pixel shimmer/jitter.
                    Vector2 target = localPoint + new Vector2(0f, bubbleScreenYOffset);
                    target.x = Mathf.Round(target.x);
                    target.y = Mathf.Round(target.y);

                    if ((bubbleRect.anchoredPosition - target).sqrMagnitude > 0.0001f)
                        bubbleRect.anchoredPosition = target;

                }
            }
        }

        inputConsumedThisFrame = false;
    }



    public void RebindForScene()
    {
        // 1) 씬 PlayerController 다시 잡기 (태그 있으면 태그 추천)
        playerController = FindAnyObjectByType<PlayerController>();
        gameManager = FindAnyObjectByType<GameManager>();

        // 2) speechBubbleParent를 "현재 씬" Canvas로 강제
        var sceneCanvas = FindSceneCanvasInActiveScene();
        if (sceneCanvas == null)
            sceneCanvas = EnsureRuntimeDialogueCanvas();
        if (sceneCanvas != null)
            speechBubbleParent = sceneCanvas.transform;

        // 3) 말풍선 인스턴스가 없으면 생성, 있으면 부모만 갱신
        if (speechBubblePrefab == null)
        {
            Debug.LogError("[DialogueManager] speechBubblePrefab이 인스펙터에 연결되지 않았습니다.");
            return;
        }

        if (speechBubble == null)
        {
            speechBubble = Instantiate(speechBubblePrefab, speechBubbleParent);
            speechBubble.gameObject.SetActive(false);

            nameText = speechBubble.nameText;
            dialogueText = speechBubble.bodyText;
        }
        else
        {
            // 이미 만들어진 말풍선이면, 부모만 씬 Canvas로 옮겨주기
            if (speechBubbleParent != null)
                speechBubble.transform.SetParent(speechBubbleParent, false);
        }

        EnsureBubbleVisuals(speechBubble);

        if (nameText == null || dialogueText == null)
        {
            Debug.LogError("[DialogueManager] SpeechBubbleUI에 nameText/bodyText 연결이 필요합니다.");
        }
    }

    private Canvas FindSceneCanvasInActiveScene()
    {
        var active = SceneManager.GetActiveScene();
        var roots = active.GetRootGameObjects();

        Canvas best = null;

        for (int i = 0; i < roots.Length; i++)
        {
            // 루트 밑에서 Canvas 찾기
            var canvases = roots[i].GetComponentsInChildren<Canvas>(true);
            for (int j = 0; j < canvases.Length; j++)
            {
                // 우선순위: 활성화된 Canvas
                if (canvases[j].isActiveAndEnabled && canvases[j].gameObject.activeInHierarchy)
                    return canvases[j];

                if (best == null)
                    best = canvases[j];
            }
        }
        return best;
    }

    private Canvas EnsureRuntimeDialogueCanvas()
    {
        const string canvasName = "__RuntimeDialogueCanvas";
        var existing = GameObject.Find(canvasName);
        if (existing != null)
        {
            var existingCanvas = existing.GetComponent<Canvas>();
            if (existingCanvas != null)
                return existingCanvas;
        }

        var go = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Debug.Log("[DialogueManager] Runtime dialogue canvas created: " + canvasName);

        return canvas;
    }

    private void EnsureBubbleVisuals(SpeechBubbleUI bubble)
    {
        if (bubble == null) return;

        var images = bubble.GetComponentsInChildren<Image>(true);
        bool hasRenderableImage = false;
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].sprite != null && images[i].color.a > 0.01f)
            {
                hasRenderableImage = true;
                break;
            }
        }

        if (!hasRenderableImage)
        {
            var bg = bubble.transform.Find("__AutoBubbleBG");
            if (bg == null)
            {
                var bgGo = new GameObject("__AutoBubbleBG", typeof(RectTransform), typeof(RawImage));
                bgGo.transform.SetParent(bubble.transform, false);
                bgGo.transform.SetAsFirstSibling();

                var bgRect = bgGo.GetComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;

                var raw = bgGo.GetComponent<RawImage>();
                raw.color = new Color(1f, 1f, 1f, 0.95f);
                raw.raycastTarget = false;
            }
        }
    }


    // CharacterIdentifier 캐시 새로고침
    private void RefreshCharacterCache()
    {
        characterCache.Clear();
        actorCache.Clear();
        CharacterIdentifier[] allCharacters = FindObjectsOfType<CharacterIdentifier>();
        foreach (CharacterIdentifier character in allCharacters)
        {
            if (!string.IsNullOrEmpty(character.characterID))
            {
                characterCache[character.characterID] = character;
            }
        }

        CharacterActor[] allActors = FindObjectsOfType<CharacterActor>();
        foreach (CharacterActor actor in allActors)
        {
            if (!string.IsNullOrEmpty(actor.characterId))
            {
                actorCache[actor.characterId] = actor.transform;
            }
        }
    }

    private Transform ResolveSpeakerTransform(string speakerId)
    {
        if (string.IsNullOrEmpty(speakerId)) return null;

        Transform playerSpeaker = ResolvePlayerSpeakerTransform(speakerId);
        if (playerSpeaker != null)
            return playerSpeaker;

        if (characterCache.TryGetValue(speakerId, out var ci) && ci != null)
            return ci.transform;
        if (actorCache.TryGetValue(speakerId, out var at))
            return at;

        string noPrefix = speakerId.StartsWith("NAME_") ? speakerId.Substring(5) : speakerId;
        if (characterCache.TryGetValue(noPrefix, out var ci2) && ci2 != null)
            return ci2.transform;
        if (actorCache.TryGetValue(noPrefix, out var at2))
            return at2;

        // 마지막 시도: 대소문자 무시 매칭
        foreach (var kv in actorCache)
        {
            if (string.Equals(kv.Key, speakerId, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kv.Key, noPrefix, System.StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }
        foreach (var kv in characterCache)
        {
            if (string.Equals(kv.Key, speakerId, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kv.Key, noPrefix, System.StringComparison.OrdinalIgnoreCase))
                return kv.Value != null ? kv.Value.transform : null;
        }

        return null;
    }

    private Transform ResolvePlayerSpeakerTransform(string speakerId)
    {
        if (!IsPlayerSpeakerId(speakerId))
            return null;

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();

        if (playerController != null)
            return playerController.transform;

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        return playerGo != null ? playerGo.transform : null;
    }

    private static bool IsPlayerSpeakerId(string speakerId)
    {
        if (string.IsNullOrEmpty(speakerId))
            return false;

        return string.Equals(speakerId, "PLAYER", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(speakerId, "NAME_PLAYER", StringComparison.OrdinalIgnoreCase);
    }

    // 대화 시작
    public void StartDialogue(string conversationID, Transform npcSpeaker)
    {
        if (LocalizationManager.Instance == null)
        {
            Debug.LogError("[DialogueManager] LocalizationManager.Instance가 없습니다. 대화를 시작할 수 없습니다. (conversationID=" + conversationID + ")");
            return;
        }

        List<DialogueLine> dialogueLines = LocalizationManager.Instance.GetConversation(conversationID);

        if (dialogueLines.Count == 0)
        {
            Debug.LogError(conversationID + " 대화를 찾을 수 없습니다.");
            return;
        }

        StopAllCoroutines();
        isTyping = false;
        isBusy = false;
        blockAdvanceInputThisFrame = false;

        if (dialogueText == null)
        {
            Debug.LogError("[DialogueManager] dialogueText가 null입니다. SpeechBubbleUI 연결을 확인하세요.");
            return;
        }
        dialogueText.text = "";

        if (speechBubble != null) speechBubble.gameObject.SetActive(true);
        IsDialogueActive = true;
        CurrentConversationId = conversationID;
        currentLine = null;
        currentNpcSpeaker = npcSpeaker;
        currentLineIndex = -1;
        activeLinePresentations.Clear();
        if (pendingLinePresentations.Count > 0)
        {
            activeLinePresentations.AddRange(pendingLinePresentations);
            pendingLinePresentations.Clear();
        }

        if (playerController != null) playerController.enabled = false;
        StopPlayerMotionImmediate();

        lines.Clear();
        foreach (DialogueLine line in dialogueLines) lines.Enqueue(line);

        RefreshCharacterCache();

        // Start the first line immediately, but keep the original interact key
        // from skipping it in the same frame.
        blockAdvanceInputThisFrame = true;
        DisplayNextSentence();
        StartCoroutine(ReleaseAdvanceBlockNextFrame());
    }

    private IEnumerator ReleaseAdvanceBlockNextFrame()
    {
        yield return null;
        blockAdvanceInputThisFrame = false;
    }


    /*  public void DisplayNextLine()
      {
          if (isBusy) return;          // 연출 중에는 다음으로 못 넘어가게
          if (isTyping) { SkipTyping(); return; } // 타자 중이면 즉시완성(너 기존 기능 유지)

          if (noMoreLines) { EndDialogue(); return; }

          var line = GetNextLine();             // 너가 큐/리스트에서 다음 줄 꺼내는 부분
          StartCoroutine(PlayLineRoutine(line)); // ⭐ 여기 핵심
      }*/

    //private IEnumerator PlayLineRoutine(DialogueLine line)
    //{
    //    isBusy = true;

    //    // 1) 태그 실행 (이동/지나감이면 끝날때까지 여기서 기다림)
    //    if (commandRunner != null)
    //        yield return commandRunner.Execute(line.text);

    //    isBusy = false;

    //    // 2) 태그 제거한 텍스트만 출력
    //    string clean = TagParser.Strip(line.text);
    //    StartTyping(clean); // 너가 기존에 쓰는 “타자치기 출력” 함수
    //}


    public void DisplayNextSentence()
    {
        // 타이핑 중이면 즉시 완성
        if (isTyping)
        {
            StopAllCoroutines();
            dialogueText.text = LocalizationManager.Instance.GetLine(currentLine.lineID);
            isTyping = false;

            if (lines.Count == 0) EndDialogue();
            return;
        }

        if (lines.Count == 0)
        {
            EndDialogue();
            return;
        }

        if (currentLine != null && TryTakeAfterLinePresentation(currentLine, currentLineIndex, out DialogueLinePresentation afterPresentation))
        {
            StartCoroutine(PlayAfterLinePresentationThenContinue(afterPresentation));
            return;
        }

        currentLine = lines.Dequeue();
        currentLineIndex++;
        DialogueLineShown?.Invoke(CurrentConversationId, currentLine.lineID);
        string currentSpeakerID = currentLine.speakerID;
        if (string.Equals(CurrentConversationId, "DAY1_LUNCH_MING", StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log($"[DialogueDebug] enter line idx={currentLineIndex} lineID={currentLine.lineID} speaker={currentSpeakerID}");
        }
        // STORY line-level set switching: lineID -> setId
        if (SceneManager.GetActiveScene().name == "STORY")
        {
            string setId = StoryLineSetMeta.GetSetIdForLine(currentLine.lineID);
            if (!string.IsNullOrEmpty(setId))
            {
                var switcher = FindAnyObjectByType<StorySetSwitcher>();
                if (switcher != null && switcher.ApplySetById(setId))
                    RefreshCharacterCache();
            }
        }

        // 캐시에서 캐릭터 찾기 (CharacterIdentifier + CharacterActor 모두 지원)
        currentSpeaker = ResolveSpeakerTransform(currentSpeakerID);
        if (currentSpeaker == null)
        {
            // 캐시 새로고침 후 다시 시도
            RefreshCharacterCache();
            currentSpeaker = ResolveSpeakerTransform(currentSpeakerID);
        }
        if (currentSpeaker == null)
        {
            // 찾지 못하면 기본 NPC 스피커 사용
            currentSpeaker = currentNpcSpeaker;
        }

        DialogueCharacterPresentation speakerPresentationSource = ResolveSpeakerPresentationComponent(currentSpeaker);
        DialogueLinePresentation speakerDefaults = speakerPresentationSource != null ? speakerPresentationSource.ToPresentation() : null;
        DialogueLinePresentation linePresentation = ResolveLinePresentation(currentLine, currentLineIndex);
        DialogueLinePresentation presentation = MergePresentations(speakerDefaults, linePresentation);
        Transform presentationTarget = ResolvePresentationTarget(presentation, currentSpeaker);
        DialogueCharacterPresentation presentationTargetSource = ResolveSpeakerPresentationComponent(presentationTarget);

        // 애니메이션 재생
        string animationTrigger = presentation != null && !string.IsNullOrEmpty(presentation.animationTrigger)
            ? presentation.animationTrigger
            : currentLine.animationTrigger;
        AnimationClip presentationClip = presentation != null ? presentation.animationClip : null;
        PlayPresentationVisuals(presentationTarget, presentationTargetSource, presentationClip, animationTrigger);

        // 소리 이펙트 재생
        string soundEffectName = presentation != null && !string.IsNullOrEmpty(presentation.soundEffectName)
            ? presentation.soundEffectName
            : currentLine.soundEffectName;
        if (!string.IsNullOrEmpty(soundEffectName))
        {
            AudioClip clip = RuntimeAudioClipCatalog.Load(soundEffectName);
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip, AudioSettingsService.ScaleSfx(1f));
            }
            else
            {
                UnityEngine.Debug.LogWarning("소리 이펙트를 찾을 수 없습니다: " + soundEffectName);
            }
        }

        // 대사 표시
        string translatedSentence = LocalizationManager.Instance.GetLine(currentLine.lineID);
        float beforeTextDelaySeconds = presentation != null ? Mathf.Max(0f, presentation.beforeTextDelaySeconds) : 0f;
        StartCoroutine(RunCommandsThenType(translatedSentence, beforeTextDelaySeconds, currentSpeaker, currentSpeakerID));

    }

    private IEnumerator RunCommandsThenType(string translatedSentence, float beforeTextDelaySeconds, Transform bubbleSpeaker, string speakerId)
    {
        isBusy = true;

        if (beforeTextDelaySeconds > 0f)
            yield return new WaitForSeconds(beforeTextDelaySeconds);

        // 1) 태그 커맨드 실행 (move/pass/wait/door 등)
        if (commandRunner != null)
            yield return commandRunner.Execute(translatedSentence);

        // 2) 태그 제거한 텍스트만 보여주기
        string clean = TagParser.Strip(translatedSentence);

        isBusy = false;

        currentBubbleSpeaker = bubbleSpeaker;
        if (nameText != null)
            nameText.text = LocalizationManager.Instance.GetName(speakerId);
        if (dialogueText != null)
            dialogueText.text = string.Empty;
        if (speechBubble != null)
            speechBubble.gameObject.SetActive(currentBubbleSpeaker != null);

        // 3) 타이핑 코루틴 실행 (기존 기능 그대로)
        yield return StartCoroutine(TypeSentence(clean));
    }


    IEnumerator TypeSentence(string sentence)
    {
        isTyping = true;
        dialogueText.text = "";

        foreach (char letter in sentence.ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;

        
    }

    public void EndDialogue()
    {
        // 모든 코루틴 중지
        StopAllCoroutines();

        string completedConversationId = CurrentConversationId;

        if (speechBubble != null) speechBubble.gameObject.SetActive(false);
        IsDialogueActive = false;
        isTyping = false;
        isBusy = false;
        blockAdvanceInputThisFrame = false;
        pendingLinePresentations.Clear();
        activeLinePresentations.Clear();
        currentLineIndex = -1;
        StopPresentationAnimationImmediate();
        CurrentConversationId = string.Empty;
        currentSpeaker = null;
        currentBubbleSpeaker = null;
        currentLine = null;
        currentNpcSpeaker = null;

        // STORY 씬이면 FlowManager에게 "이번 이벤트 끝남"만 보고
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "STORY")
        {
            string appendConversationId = PlayerPrefs.GetString(FlowManager.StoryAppendConversationPrefKey, "");
            if (!string.IsNullOrEmpty(appendConversationId))
            {
                PlayerPrefs.DeleteKey(FlowManager.StoryAppendConversationPrefKey);

                if (LocalizationManager.Instance != null)
                {
                    var appendLines = LocalizationManager.Instance.GetConversation(appendConversationId);
                    if (appendLines != null && appendLines.Count > 0)
                    {
                        StartDialogue(appendConversationId, null);
                        return;
                    }
                }
            }

            if (!string.IsNullOrEmpty(completedConversationId))
            {
                DialogueProgressState.MarkConversationCompleted(completedConversationId);
                DialogueConversationCompleted?.Invoke(completedConversationId);
            }

            if (FlowManager.Instance != null)
                FlowManager.Instance.CompleteCurrentEvent(0);
            else
                Debug.LogError("[DialogueManager] STORY 씬인데 FlowManager가 없음");
        }
        else
        {
            if (!string.IsNullOrEmpty(completedConversationId))
            {
                DialogueProgressState.MarkConversationCompleted(completedConversationId);
                DialogueConversationCompleted?.Invoke(completedConversationId);
            }

            // 나머지(자유이동/NPC대화 등)는 기존대로
            if (playerController != null) playerController.enabled = true;
            if (gameManager != null) gameManager.DialogueFinished();
        }

    }

    public void SetUpcomingLinePresentations(List<DialogueLinePresentation> presentations)
    {
        pendingLinePresentations.Clear();
        if (presentations == null)
            return;

        for (int i = 0; i < presentations.Count; i++)
        {
            DialogueLinePresentation src = presentations[i];
            if (src == null)
                continue;

            pendingLinePresentations.Add(new DialogueLinePresentation
            {
                lineID = src.lineID,
                lineIndexStart = src.lineIndexStart,
                lineIndexEnd = src.lineIndexEnd,
                afterLineID = src.afterLineID,
                afterLineIndexStart = src.afterLineIndexStart,
                afterLineIndexEnd = src.afterLineIndexEnd,
                targetCharacterId = src.targetCharacterId,
                animationTrigger = src.animationTrigger,
                animationClip = src.animationClip,
                soundEffectName = src.soundEffectName,
                beforeTextDelaySeconds = Mathf.Max(0f, src.beforeTextDelaySeconds)
            });
        }
    }

    private DialogueLinePresentation ResolveLinePresentation(DialogueLine line, int lineIndex)
    {
        if (activeLinePresentations.Count == 0)
            return null;

        int lineNumber = lineIndex + 1;

        for (int i = 0; i < activeLinePresentations.Count; i++)
        {
            DialogueLinePresentation p = activeLinePresentations[i];
            if (p == null)
                continue;

            if (MatchesLineId(p, line.lineID))
                return p;
        }

        for (int i = 0; i < activeLinePresentations.Count; i++)
        {
            DialogueLinePresentation p = activeLinePresentations[i];
            if (p == null)
                continue;

            if (!string.IsNullOrEmpty(p.lineID))
                continue;

            if (MatchesLineIndexRange(p, lineNumber))
                return p;
        }

        return null;
    }

    private DialogueCharacterPresentation ResolveSpeakerPresentationComponent(Transform speaker)
    {
        if (speaker == null)
            return null;

        var presentation = speaker.GetComponent<DialogueCharacterPresentation>();
        if (presentation == null)
            presentation = speaker.GetComponentInParent<DialogueCharacterPresentation>();
        if (presentation == null)
            presentation = speaker.GetComponentInChildren<DialogueCharacterPresentation>(true);

        return presentation;
    }

    private Animator ResolveSpeakerAnimator(Transform speaker, DialogueCharacterPresentation presentationSource, bool allowRuntimeAnimatorForClip)
    {
        if (presentationSource != null && presentationSource.targetAnimator != null)
            return presentationSource.targetAnimator;

        if (speaker == null)
            return null;

        Animator animator = speaker.GetComponent<Animator>();
        if (animator == null)
            animator = speaker.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = speaker.GetComponentInParent<Animator>();

        if (animator == null && allowRuntimeAnimatorForClip)
        {
            Transform target = speaker;
            SpriteRenderer targetSprite = target.GetComponent<SpriteRenderer>();
            if (targetSprite == null)
            {
                targetSprite = target.GetComponentInChildren<SpriteRenderer>(true);
                if (targetSprite != null)
                    target = targetSprite.transform;
            }

            if (targetSprite != null)
                animator = target.gameObject.GetComponent<Animator>() ?? target.gameObject.AddComponent<Animator>();
        }

        return animator;
    }

    private static DialogueLinePresentation MergePresentations(DialogueLinePresentation defaults, DialogueLinePresentation specific)
    {
        if (defaults == null)
            return specific;
        if (specific == null)
            return defaults;

        return new DialogueLinePresentation
        {
            lineID = !string.IsNullOrEmpty(specific.lineID) ? specific.lineID : defaults.lineID,
            lineIndexStart = specific.lineIndexStart >= 0 ? specific.lineIndexStart : defaults.lineIndexStart,
            lineIndexEnd = specific.lineIndexEnd >= 0 ? specific.lineIndexEnd : defaults.lineIndexEnd,
            afterLineID = !string.IsNullOrEmpty(specific.afterLineID) ? specific.afterLineID : defaults.afterLineID,
            afterLineIndexStart = specific.afterLineIndexStart >= 0 ? specific.afterLineIndexStart : defaults.afterLineIndexStart,
            afterLineIndexEnd = specific.afterLineIndexEnd >= 0 ? specific.afterLineIndexEnd : defaults.afterLineIndexEnd,
            targetCharacterId = !string.IsNullOrEmpty(specific.targetCharacterId) ? specific.targetCharacterId : defaults.targetCharacterId,
            animationTrigger = !string.IsNullOrEmpty(specific.animationTrigger) ? specific.animationTrigger : defaults.animationTrigger,
            animationClip = specific.animationClip != null ? specific.animationClip : defaults.animationClip,
            soundEffectName = !string.IsNullOrEmpty(specific.soundEffectName) ? specific.soundEffectName : defaults.soundEffectName,
            beforeTextDelaySeconds = specific.beforeTextDelaySeconds > 0f ? specific.beforeTextDelaySeconds : defaults.beforeTextDelaySeconds
        };
    }

    private Transform ResolvePresentationTarget(DialogueLinePresentation presentation, Transform fallbackTarget)
    {
        if (presentation == null || string.IsNullOrEmpty(presentation.targetCharacterId))
            return fallbackTarget;

        Transform resolved = ResolveSpeakerTransform(presentation.targetCharacterId);
        return resolved != null ? resolved : fallbackTarget;
    }

    private bool TryTakeAfterLinePresentation(DialogueLine line, int lineIndex, out DialogueLinePresentation presentation)
    {
        presentation = null;
        if (line == null || activeLinePresentations.Count == 0)
            return false;

        int lineNumber = lineIndex + 1;

        for (int i = 0; i < activeLinePresentations.Count; i++)
        {
            DialogueLinePresentation candidate = activeLinePresentations[i];
            if (candidate == null)
                continue;

            bool matchesById = MatchesAfterLineId(candidate, line.lineID);
            bool matchesByIndex = MatchesAfterLineIndexRange(candidate, lineNumber);

            if (!matchesById && !matchesByIndex)
                continue;

            presentation = candidate;
            activeLinePresentations.RemoveAt(i);
            if (string.Equals(CurrentConversationId, "DAY1_LUNCH_MING", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[DialogueDebug] after-line trigger from lineID={line.lineID} idx={lineIndex} -> target={candidate.targetCharacterId} clip={(candidate.animationClip != null ? candidate.animationClip.name : "(none)")}");
            }
            return true;
        }

        return false;
    }

    private IEnumerator PlayAfterLinePresentationThenContinue(DialogueLinePresentation presentation)
    {
        isBusy = true;

        Transform target = ResolvePresentationTarget(presentation, currentSpeaker != null ? currentSpeaker : currentNpcSpeaker);
        DialogueCharacterPresentation targetSource = ResolveSpeakerPresentationComponent(target);
        string animationTrigger = presentation != null ? presentation.animationTrigger : string.Empty;
        AnimationClip presentationClip = presentation != null ? presentation.animationClip : null;

        if (string.Equals(CurrentConversationId, "DAY1_LUNCH_MING", StringComparison.OrdinalIgnoreCase))
        {
            string targetName = target != null ? target.name : "(null)";
            Debug.Log($"[DialogueDebug] play after-line target={targetName} trigger={animationTrigger} clip={(presentationClip != null ? presentationClip.name : "(none)")}");
        }

        PlayPresentationVisuals(target, targetSource, presentationClip, animationTrigger);
        PlayPresentationSound(presentation);

        float delaySeconds = presentation != null ? Mathf.Max(0f, presentation.beforeTextDelaySeconds) : 0f;
        float clipSeconds = presentationClip != null ? Mathf.Max(0f, presentationClip.length) : 0f;
        float waitSeconds = Mathf.Max(delaySeconds, clipSeconds);
        if (waitSeconds > 0f)
            yield return new WaitForSeconds(waitSeconds);

        // After-line insert animations should finish before the next spoken line begins.
        StopPresentationAnimationImmediate();

        isBusy = false;
        DisplayNextSentence();
    }

    private void PlayPresentationVisuals(Transform target, DialogueCharacterPresentation targetSource, AnimationClip presentationClip, string animationTrigger)
    {
        if (target == null)
            return;

        Animator animator = ResolveSpeakerAnimator(target, targetSource, presentationClip != null);
        if (animator == null)
            return;

        if (targetSource != null && (presentationClip != null || !string.IsNullOrEmpty(animationTrigger)))
            targetSource.SuspendDefaultPresentation();

        if (presentationClip != null)
        {
            if (activePresentationAnimator == animator && activePresentationClip == presentationClip)
                return;

            PlayPresentationClip(animator, presentationClip);
            activePresentationTrigger = string.Empty;
        }
        else if (!string.IsNullOrEmpty(animationTrigger))
        {
            if (activePresentationAnimator == animator &&
                string.Equals(activePresentationTrigger, animationTrigger, StringComparison.Ordinal))
                return;

            animator.SetTrigger(animationTrigger);
            activePresentationAnimator = animator;
            activePresentationClip = null;
            activePresentationTrigger = animationTrigger;
        }
    }

    private void PlayPresentationSound(DialogueLinePresentation presentation)
    {
        string soundEffectName = presentation != null ? presentation.soundEffectName : string.Empty;
        if (string.IsNullOrEmpty(soundEffectName))
            return;

        AudioClip clip = RuntimeAudioClipCatalog.Load(soundEffectName);
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, AudioSettingsService.ScaleSfx(1f));
    }

    private void PlayPresentationClip(Animator animator, AnimationClip clip)
    {
        if (animator == null || clip == null)
            return;

        StopPresentationAnimationImmediate();

        presentationGraph = PlayableGraph.Create("DialoguePresentationClip");
        var output = AnimationPlayableOutput.Create(presentationGraph, "DialoguePresentation", animator);
        var playable = AnimationClipPlayable.Create(presentationGraph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        output.SetSourcePlayable(playable);
        presentationGraph.Play();
        activePresentationAnimator = animator;
        activePresentationClip = clip;
        activePresentationTrigger = string.Empty;

    }

    private void StopPresentationAnimationImmediate()
    {
        if (presentationGraph.IsValid())
            presentationGraph.Destroy();

        activePresentationAnimator = null;
        activePresentationClip = null;
        activePresentationTrigger = string.Empty;

        DialogueCharacterPresentation[] defaults = FindObjectsByType<DialogueCharacterPresentation>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < defaults.Length; i++)
            defaults[i].ResumeDefaultPresentation();
    }

    private static bool MatchesLineId(DialogueLinePresentation presentation, string lineId)
    {
        if (presentation == null || string.IsNullOrEmpty(lineId))
            return false;

        if (!string.IsNullOrEmpty(presentation.lineID) &&
            string.Equals(presentation.lineID, lineId, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool MatchesAfterLineId(DialogueLinePresentation presentation, string lineId)
    {
        if (presentation == null || string.IsNullOrEmpty(lineId))
            return false;

        if (!string.IsNullOrEmpty(presentation.afterLineID) &&
            string.Equals(presentation.afterLineID, lineId, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool MatchesLineIndexRange(DialogueLinePresentation presentation, int lineIndex)
    {
        if (presentation == null || lineIndex < 0)
            return false;

        if (presentation.lineIndexStart < 0 || presentation.lineIndexEnd < 0)
            return false;

        return lineIndex >= presentation.lineIndexStart && lineIndex <= presentation.lineIndexEnd;
    }

    private static bool MatchesAfterLineIndexRange(DialogueLinePresentation presentation, int lineIndex)
    {
        if (presentation == null || lineIndex < 0)
            return false;

        if (presentation.afterLineIndexStart < 0 || presentation.afterLineIndexEnd < 0)
            return false;

        return lineIndex >= presentation.afterLineIndexStart && lineIndex <= presentation.afterLineIndexEnd;
    }
}

public static class RuntimeAudioClipCatalog
{
    private static readonly string[] ResourceRoots = { "SFX", "Sounds" };
    private static Dictionary<string, AudioClip> clipByKey;
    private static bool initialized;

    public static AudioClip Load(string clipNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(clipNameOrPath))
            return null;

        EnsureInitialized();

        string normalizedInput = NormalizeKey(clipNameOrPath);
        if (clipByKey.TryGetValue(normalizedInput, out var clip))
            return clip;

        string fileNameOnly = NormalizeKey(GetLastSegment(clipNameOrPath));
        if (clipByKey.TryGetValue(fileNameOnly, out clip))
            return clip;

        return null;
    }

    private static void EnsureInitialized()
    {
        if (initialized)
            return;

        clipByKey = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < ResourceRoots.Length; i++)
        {
            string root = ResourceRoots[i];
            AudioClip[] clips = Resources.LoadAll<AudioClip>(root);
            for (int j = 0; j < clips.Length; j++)
            {
                AudioClip clip = clips[j];
                if (clip == null)
                    continue;

                AddKey(clip.name, clip);
                AddKey(root + "/" + clip.name, clip);
            }
        }

        initialized = true;
    }

    private static void AddKey(string key, AudioClip clip)
    {
        string normalized = NormalizeKey(key);
        if (string.IsNullOrEmpty(normalized) || clipByKey.ContainsKey(normalized))
            return;

        clipByKey[normalized] = clip;
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().Replace("\\", "/");
    }

    private static string GetLastSegment(string value)
    {
        string normalized = NormalizeKey(value);
        int slashIndex = normalized.LastIndexOf('/');
        return slashIndex >= 0 ? normalized.Substring(slashIndex + 1) : normalized;
    }
}
