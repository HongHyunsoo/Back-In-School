using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
    public float typingSpeed = 0.03f;
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
    private Transform currentNpcSpeaker;

    // 성능 개선: CharacterIdentifier/CharacterActor 캐싱
    private Dictionary<string, CharacterIdentifier> characterCache = new Dictionary<string, CharacterIdentifier>();
    private Dictionary<string, Transform> actorCache = new Dictionary<string, Transform>();
    private bool blockAdvanceInputThisFrame = false;

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
        if (IsDialogueActive && currentSpeaker != null && speechBubble != null)
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

            Vector3 targetPos = currentSpeaker.position + worldOffset;
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
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();

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

        if (playerController != null) playerController.enabled = false;
        StopPlayerMotionImmediate();

        lines.Clear();
        foreach (DialogueLine line in dialogueLines) lines.Enqueue(line);

        RefreshCharacterCache();

        // ★ 핵심: 시작 프레임엔 넘김 입력 막기 + 다음 프레임에 첫 줄 출력
        blockAdvanceInputThisFrame = true;
        StartCoroutine(BeginDialogueNextFrame());
    }

    private IEnumerator BeginDialogueNextFrame()
    {
        yield return null; // 한 프레임 대기
        blockAdvanceInputThisFrame = false;
        DisplayNextSentence();
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

        currentLine = lines.Dequeue();
        DialogueLineShown?.Invoke(CurrentConversationId, currentLine.lineID);
        string currentSpeakerID = currentLine.speakerID;

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

        // 이름 표시
        nameText.text = LocalizationManager.Instance.GetName(currentSpeakerID);

        // 애니메이션 재생
        if (!string.IsNullOrEmpty(currentLine.animationTrigger) && currentSpeaker != null)
        {
            Animator animator = currentSpeaker.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetTrigger(currentLine.animationTrigger);
            }
        }

        // 소리 이펙트 재생
        if (!string.IsNullOrEmpty(currentLine.soundEffectName))
        {
            AudioClip clip = Resources.Load<AudioClip>("Sounds/" + currentLine.soundEffectName);
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
            else
            {
                UnityEngine.Debug.LogWarning("소리 이펙트를 찾을 수 없습니다: " + currentLine.soundEffectName);
            }
        }

        // 대사 표시
        string translatedSentence = LocalizationManager.Instance.GetLine(currentLine.lineID);
        StartCoroutine(RunCommandsThenType(translatedSentence));

    }

    private IEnumerator RunCommandsThenType(string translatedSentence)
    {
        isBusy = true;

        // 1) 태그 커맨드 실행 (move/pass/wait/door 등)
        if (commandRunner != null)
            yield return commandRunner.Execute(translatedSentence);

        // 2) 태그 제거한 텍스트만 보여주기
        string clean = TagParser.Strip(translatedSentence);

        isBusy = false;

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
        CurrentConversationId = string.Empty;
        currentSpeaker = null;
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
                DialogueConversationCompleted?.Invoke(completedConversationId);

            if (FlowManager.Instance != null)
                FlowManager.Instance.CompleteCurrentEvent(0);
            else
                Debug.LogError("[DialogueManager] STORY 씬인데 FlowManager가 없음");
        }
        else
        {
            if (!string.IsNullOrEmpty(completedConversationId))
                DialogueConversationCompleted?.Invoke(completedConversationId);

            // 나머지(자유이동/NPC대화 등)는 기존대로
            if (playerController != null) playerController.enabled = true;
            if (gameManager != null) gameManager.DialogueFinished();
        }

    }
}
