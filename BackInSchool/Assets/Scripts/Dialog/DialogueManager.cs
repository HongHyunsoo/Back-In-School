using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    private const string CameraPresentationSfxResource = "SFX/Char/Camera";

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
    public Vector3 worldOffset = new Vector3(0, 0.65f, 0);
    [SerializeField] private float bubbleScreenYOffset = 16f;
    public PlayerController playerController;
    public KeyCode nextSentenceKey = KeyCode.E;

    [Header("오디오")]
    public AudioSource audioSource; // 소리 이펙트 재생용
    [SerializeField] private AudioClip typingSfx;
    [SerializeField] [Range(0f, 1f)] private float typingSfxVolume = 0.2f;
    [SerializeField] [Min(1)] private int typingSfxInterval = 2;
    [SerializeField] private bool typingSfxIgnoreWhitespace = true;

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
    private readonly Dictionary<Animator, PlayableGraph> presentationGraphs = new Dictionary<Animator, PlayableGraph>();
    private readonly Dictionary<Animator, AnimationClipPlayable> activePresentationPlayables = new Dictionary<Animator, AnimationClipPlayable>();
    private readonly Dictionary<Animator, AnimationClip> activePresentationClips = new Dictionary<Animator, AnimationClip>();
    private readonly Dictionary<Animator, string> activePresentationTriggers = new Dictionary<Animator, string>();
    private readonly Dictionary<Animator, DialogueCharacterPresentation> suspendedDefaultSources = new Dictionary<Animator, DialogueCharacterPresentation>();
    private readonly HashSet<Animator> currentLinePresentationAnimators = new HashSet<Animator>();
    private float advanceBlockedUntilUnscaledTime = 0f;
    private bool forceHideSpeechBubble = false;

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
        EnsureSpeechBubbleBindings();
        EnsureAudioSource();


        IsDialogueActive = false;
        gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null) UnityEngine.Debug.LogError("GameManager를 찾을 수 없습니다!");

        // CharacterIdentifier 캐싱
        RefreshCharacterCache();
    }

    private void OnDestroy()
    {
        bool preserveStoryPresentationState =
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "STORY";
        StopPresentationAnimationImmediate(!preserveStoryPresentationState);
    }

    void Update()
    {
        if (!IsDialogueActive) return;

        nextSentenceKey = KeyBindingConfig.Get(KeyBindingConfig.InteractKey, KeyCode.E);

        if (blockAdvanceInputThisFrame) return;
        if (Time.unscaledTime < advanceBlockedUntilUnscaledTime) return;

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
            if (forceHideSpeechBubble)
            {
                speechBubble.gameObject.SetActive(false);
                inputConsumedThisFrame = false;
                return;
            }

            var cam = Camera.main;
            if (cam == null) return;

            // 말풍선은 씬별 런타임 Canvas 기준으로 배치
            var canvas = speechBubbleParent != null
                ? speechBubbleParent.GetComponentInParent<Canvas>()
                : null;

            if (canvas == null)
            {
                canvas = EnsureRuntimeDialogueCanvas();
                if (canvas == null) return;

                speechBubbleParent = canvas.transform;
                speechBubble.transform.SetParent(speechBubbleParent, false);
            }

            Vector3 targetPos = GetBubbleAnchorWorldPosition(currentBubbleSpeaker);
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

                if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
                    canvas.worldCamera = cam;

                // Overlay면 uiCam = null, Camera/World면 worldCamera 필요
                Camera uiCam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCam, out var localPoint))
                {
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

        // 2) 말풍선은 씬별 런타임 Canvas 사용
        var runtimeCanvas = EnsureRuntimeDialogueCanvas();
        if (runtimeCanvas != null)
            speechBubbleParent = runtimeCanvas.transform;

        // 3) 말풍선 인스턴스가 없으면 생성, 있으면 부모만 갱신
        if (speechBubblePrefab == null)
        {
            speechBubblePrefab = Resources.Load<SpeechBubbleUI>("DialogBox");
            if (speechBubblePrefab == null)
            {
                Debug.LogError("[DialogueManager] speechBubblePrefab이 인스펙터에 연결되지 않았고 Resources/DialogBox도 찾지 못했습니다.");
                return;
            }
        }

        if (speechBubble == null)
        {
            speechBubble = Instantiate(speechBubblePrefab, speechBubbleParent);
            speechBubble.gameObject.SetActive(false);
        }
        else
        {
            // 이미 만들어진 말풍선이면, 부모만 씬 Canvas로 옮겨주기
            if (speechBubbleParent != null)
                speechBubble.transform.SetParent(speechBubbleParent, false);
        }

        EnsureSpeechBubbleBindings();
        EnsureSpeechBubbleOnRuntimeCanvas();
        EnsureBubbleVisuals(speechBubble);
        EnsureAudioSource();

        if (nameText == null || dialogueText == null)
        {
            Debug.LogError("[DialogueManager] SpeechBubbleUI에 nameText/bodyText 연결이 필요합니다.");
        }
    }

    private void EnsureSpeechBubbleBindings()
    {
        if (speechBubble == null)
            return;

        if (speechBubble.nameText == null || speechBubble.bodyText == null)
        {
            var texts = speechBubble.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text == null)
                    continue;

                string objectName = text.gameObject.name;

                if (speechBubble.nameText == null &&
                    (string.Equals(objectName, "Name", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(objectName, "NameText", StringComparison.OrdinalIgnoreCase)))
                {
                    speechBubble.nameText = text;
                    continue;
                }

                if (speechBubble.bodyText == null &&
                    (string.Equals(objectName, "Dialog", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(objectName, "Body", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(objectName, "BodyText", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(objectName, "Text", StringComparison.OrdinalIgnoreCase)))
                {
                    speechBubble.bodyText = text;
                }
            }

            if (speechBubble.nameText == null && texts.Length > 0)
                speechBubble.nameText = texts[0];

            if (speechBubble.bodyText == null && texts.Length > 0)
                speechBubble.bodyText = texts[texts.Length - 1];
        }

        nameText = speechBubble.nameText;
        dialogueText = speechBubble.bodyText;

        if (dialogueText != null)
        {
            dialogueText.alignment = TextAlignmentOptions.Center;
            dialogueText.verticalAlignment = VerticalAlignmentOptions.Middle;
            dialogueText.overflowMode = TextOverflowModes.Masking;
            dialogueText.enableWordWrapping = true;
            dialogueText.margin = new Vector4(18f, 18f, 18f, 18f);
            ForceTmpReadable(dialogueText, Color.white);

            var maskRoot = dialogueText.transform.parent as RectTransform;
            if (maskRoot != null && maskRoot.GetComponent<RectMask2D>() == null)
                maskRoot.gameObject.AddComponent<RectMask2D>();
        }

        if (nameText != null)
            ForceTmpReadable(nameText, Color.white);
    }

    private Vector3 GetBubbleAnchorWorldPosition(Transform speaker)
    {
        if (speaker == null)
            return worldOffset;

        float topY = float.NegativeInfinity;
        float centerX = speaker.position.x;

        SpriteRenderer[] spriteRenderers = speaker.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = spriteRenderers[i];
            if (sr == null || !sr.enabled)
                continue;

            Bounds bounds = sr.bounds;
            if (bounds.size.sqrMagnitude <= 0.0001f)
                continue;

            topY = Mathf.Max(topY, bounds.max.y);
            centerX = bounds.center.x;
        }

        if (float.IsNegativeInfinity(topY))
        {
            Collider2D[] colliders = speaker.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];
                if (col == null || !col.enabled)
                    continue;

                Bounds bounds = col.bounds;
                if (bounds.size.sqrMagnitude <= 0.0001f)
                    continue;

                topY = Mathf.Max(topY, bounds.max.y);
                centerX = bounds.center.x;
            }
        }

        if (float.IsNegativeInfinity(topY))
            return speaker.position + worldOffset;

        return new Vector3(centerX, topY + worldOffset.y, speaker.position.z + worldOffset.z);
    }

    private static void ForceTmpReadable(TMP_Text text, Color color)
    {
        if (text == null)
            return;

        text.color = color;
        text.alpha = 1f;
        text.enableVertexGradient = false;
        text.faceColor = color;
        text.outlineColor = Color.black;

        TMP_FontAsset fontAsset = text.font;
        if (fontAsset != null && fontAsset.material != null)
        {
            // TMP 기본 머티리얼로 되돌린 뒤, 텍스트 색만 흰색으로 강제한다.
            text.fontSharedMaterial = fontAsset.material;
            text.fontMaterial = fontAsset.material;

            Material material = text.fontMaterial;
            if (material != null)
            {
                if (material.HasProperty(TMPro.ShaderUtilities.ID_FaceColor))
                    material.SetColor(TMPro.ShaderUtilities.ID_FaceColor, color);

                if (material.HasProperty(TMPro.ShaderUtilities.ID_OutlineColor))
                    material.SetColor(TMPro.ShaderUtilities.ID_OutlineColor, Color.black);

                if (material.HasProperty(TMPro.ShaderUtilities.ID_OutlineWidth))
                    material.SetFloat(TMPro.ShaderUtilities.ID_OutlineWidth, 0.16f);
            }
        }

        text.SetMaterialDirty();
        text.SetVerticesDirty();
        text.UpdateMeshPadding();
        text.ForceMeshUpdate();
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

    private bool IsStoryScene()
    {
        var activeScene = SceneManager.GetActiveScene();
        return string.Equals(activeScene.name, "STORY", StringComparison.OrdinalIgnoreCase);
    }

    private Canvas EnsureRuntimeDialogueCanvas()
    {
        string canvasName = IsStoryScene()
            ? "__RuntimeDialogueCanvas_Story"
            : "__RuntimeDialogueCanvas";
        var existing = GameObject.Find(canvasName);
        if (existing != null)
        {
            var existingCanvas = existing.GetComponent<Canvas>();
            if (existingCanvas != null)
            {
                if (IsStoryScene() && existingCanvas.renderMode == RenderMode.ScreenSpaceCamera && existingCanvas.worldCamera == null)
                    existingCanvas.worldCamera = Camera.main;
                return existingCanvas;
            }
        }

        var go = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        bool isStoryScene = IsStoryScene();
        if (isStoryScene)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 10f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 4;
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
        }

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Debug.Log("[DialogueManager] Runtime dialogue canvas created: " + canvasName);

        return canvas;
    }

    private void EnsureSpeechBubbleOnRuntimeCanvas()
    {
        if (speechBubble == null)
            return;

        Canvas runtimeCanvas = EnsureRuntimeDialogueCanvas();
        if (runtimeCanvas == null)
            return;

        if (speechBubble.transform.parent != runtimeCanvas.transform)
        {
            speechBubble.transform.SetParent(runtimeCanvas.transform, false);
            speechBubbleParent = runtimeCanvas.transform;
        }
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

        if (typingSfx == null)
            typingSfx = AudioSettingsService.LoadResourceClip("SFX/UI/UI_focus");
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

        if (speechBubble == null || dialogueText == null || nameText == null)
            RebindForScene();

        EnsureSpeechBubbleBindings();

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

    public void BlockAdvanceForSeconds(float seconds)
    {
        if (seconds <= 0f)
            return;

        float target = Time.unscaledTime + seconds;
        if (target > advanceBlockedUntilUnscaledTime)
            advanceBlockedUntilUnscaledTime = target;
    }

    public void BlockAdvanceForSeconds(float seconds, bool consumeThisFrame)
    {
        if (consumeThisFrame)
        {
            blockAdvanceInputThisFrame = true;
            StartCoroutine(ReleaseAdvanceBlockNextFrame());
        }

        BlockAdvanceForSeconds(seconds);
    }

    public void SetSpeechBubbleVisible(bool visible)
    {
        if (speechBubble == null)
            return;

        forceHideSpeechBubble = !visible;

        if (!visible)
        {
            speechBubble.gameObject.SetActive(false);
            return;
        }

        EnsureSpeechBubbleOnRuntimeCanvas();
        speechBubble.gameObject.SetActive(IsDialogueActive && currentBubbleSpeaker != null);
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
            string fullSentence = LocalizationManager.Instance.GetLine(currentLine.lineID);
            string cleanSentence = TagParser.Strip(fullSentence);
            dialogueText.text = PrepareBubbleTextForWordWrapping(cleanSentence, dialogueText);
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
        currentLineIndex++;
        DialogueLineShown?.Invoke(CurrentConversationId, currentLine.lineID);
        if (string.Equals(CurrentConversationId, "DAY1_MOR_ADULT", System.StringComparison.OrdinalIgnoreCase) &&
            string.Equals(currentLine.lineID, "DAY1_MOR_ADULT_12", System.StringComparison.OrdinalIgnoreCase))
        {
            PhoneGalleryService.UnlockStatic("DAY1_MOR_ADULT_PHOTO");
        }
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

        // 현재 대화 중인 NPC가 지금 화자 ID와 맞으면 그 오브젝트를 최우선 사용한다.
        if (MatchesCharacterId(currentNpcSpeaker, currentSpeakerID))
        {
            currentSpeaker = currentNpcSpeaker;
        }
        else
        {
            // 캐시에서 캐릭터 찾기 (CharacterIdentifier + CharacterActor 모두 지원)
            currentSpeaker = ResolveSpeakerTransform(currentSpeakerID);
        }
        if (currentSpeaker == null)
        {
            // 캐시 새로고침 후 다시 시도
            RefreshCharacterCache();
            if (MatchesCharacterId(currentNpcSpeaker, currentSpeakerID))
                currentSpeaker = currentNpcSpeaker;
            else
                currentSpeaker = ResolveSpeakerTransform(currentSpeakerID);
        }
        if (currentSpeaker == null)
        {
            // 찾지 못하면 기본 NPC 스피커 사용
            currentSpeaker = currentNpcSpeaker;
        }

        DialogueCharacterPresentation speakerPresentationSource = ResolveSpeakerPresentationComponent(currentSpeaker);
        DialogueLinePresentation speakerDefaults = speakerPresentationSource != null ? speakerPresentationSource.ToPresentation() : null;
        List<DialogueLinePresentation> matchingPresentations = ResolveLinePresentations(currentLine, currentLineIndex);
        List<DialogueLinePresentation> csvLinePresentations = BuildCsvLinePresentations(currentLine);
        DialogueLinePresentation csvSpeakerPresentation = ResolveSpeakerSpecificPresentation(csvLinePresentations, currentSpeakerID);
        DialogueLinePresentation speakerSpecificPresentation = ResolveSpeakerSpecificPresentation(matchingPresentations, currentSpeakerID);
        DialogueLinePresentation speakerPresentation = MergePresentations(MergePresentations(speakerDefaults, csvSpeakerPresentation), speakerSpecificPresentation);
        List<DialogueLinePresentation> combinedPresentations = new List<DialogueLinePresentation>(matchingPresentations);
        if (csvLinePresentations != null && csvLinePresentations.Count > 0)
            combinedPresentations.AddRange(csvLinePresentations);

        currentLinePresentationAnimators.Clear();

        // 애니메이션 재생
        string animationTrigger = speakerPresentation != null ? speakerPresentation.animationTrigger : string.Empty;
        AnimationClip presentationClip = ResolvePresentationAnimationClip(speakerPresentation, currentSpeaker);
        PlayPresentationVisuals(currentSpeaker, speakerPresentationSource, presentationClip, animationTrigger);
        PlayPresentationSound(speakerPresentation, presentationClip);
        PlayAdditionalLinePresentations(combinedPresentations, speakerSpecificPresentation, currentSpeaker, currentSpeakerID);
        RestoreInactivePresentationTargets();

        // 대사 표시
        string translatedSentence = LocalizationManager.Instance.GetLine(currentLine.lineID);
        float beforeTextDelaySeconds = GetLinePresentationDelaySeconds(speakerPresentation, combinedPresentations, speakerSpecificPresentation);
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
        string wrappedForBubble = PrepareBubbleTextForWordWrapping(clean, dialogueText);

        isBusy = false;

        currentBubbleSpeaker = bubbleSpeaker;
        if (nameText != null)
            nameText.text = LocalizationManager.Instance.GetName(speakerId);
        if (dialogueText != null)
        {
            dialogueText.enableWordWrapping = true;
            dialogueText.overflowMode = TextOverflowModes.Masking;
            ForceTmpReadable(dialogueText, Color.white);
            dialogueText.text = string.Empty;
        }
        if (nameText != null)
            ForceTmpReadable(nameText, Color.white);
        if (speechBubble != null)
        {
            EnsureSpeechBubbleOnRuntimeCanvas();
            speechBubble.gameObject.SetActive(currentBubbleSpeaker != null);
        }

        // 3) 타이핑 코루틴 실행 (기존 기능 그대로)
        yield return StartCoroutine(TypeSentence(wrappedForBubble));
    }


    IEnumerator TypeSentence(string sentence)
    {
        isTyping = true;
        dialogueText.text = "";
        int audibleCharacterCount = 0;

        foreach (char letter in sentence.ToCharArray())
        {
            dialogueText.text += letter;
            if (ShouldPlayTypingSfx(letter, ref audibleCharacterCount))
                PlayTypingSfx();
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;

        
    }

    private bool ShouldPlayTypingSfx(char letter, ref int audibleCharacterCount)
    {
        if (typingSfx == null || audioSource == null)
            return false;

        if (typingSfxIgnoreWhitespace && (char.IsWhiteSpace(letter) || char.IsPunctuation(letter)))
            return false;

        audibleCharacterCount++;
        int interval = Mathf.Max(1, typingSfxInterval);
        return audibleCharacterCount % interval == 1;
    }

    private void PlayTypingSfx()
    {
        if (typingSfx == null || audioSource == null)
            return;

        audioSource.PlayOneShot(typingSfx, AudioSettingsService.ScaleSfx(typingSfxVolume));
    }

    private static string PrepareBubbleTextForWordWrapping(string source, TextMeshProUGUI targetText = null)
    {
        if (string.IsNullOrEmpty(source))
            return source;

        if (targetText == null)
            return source;

        // 띄어쓰기 자체가 없으면 단어 단위 줄바꿈을 할 수 없으니 TMP 기본 처리에 맡긴다.
        if (!source.Contains(" "))
            return source;

        float maxWidth = GetBubbleBodyMaxWidth(targetText);
        if (maxWidth <= 0f)
            return source;

        string normalized = source.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] paragraphs = normalized.Split('\n');
        var wrapped = new System.Text.StringBuilder(normalized.Length + 16);

        for (int i = 0; i < paragraphs.Length; i++)
        {
            if (i > 0)
                wrapped.Append('\n');

            string paragraph = paragraphs[i];
            if (string.IsNullOrWhiteSpace(paragraph))
                continue;

            AppendWrappedParagraph(wrapped, paragraph, targetText, maxWidth);
        }

        return wrapped.ToString();
    }

    private static void AppendWrappedParagraph(System.Text.StringBuilder sb, string paragraph, TextMeshProUGUI targetText, float maxWidth)
    {
        string[] words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return;

        string currentLine = words[0];

        for (int i = 1; i < words.Length; i++)
        {
            string candidate = currentLine + " " + words[i];
            float candidateWidth = targetText.GetPreferredValues(candidate).x;
            if (candidateWidth <= maxWidth)
            {
                currentLine = candidate;
                continue;
            }

            if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
                sb.Append('\n');

            sb.Append(currentLine);
            currentLine = words[i];
        }

        if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
            sb.Append('\n');

        sb.Append(currentLine);
    }

    private static float GetBubbleBodyMaxWidth(TextMeshProUGUI targetText)
    {
        if (targetText == null)
            return 0f;

        RectTransform rect = targetText.rectTransform;
        float width = rect.rect.width;
        Vector4 margin = targetText.margin;
        width -= margin.x + margin.z;
        return Mathf.Max(0f, width);
    }

    private static bool ContainsCjk(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if ((c >= '\u1100' && c <= '\u11FF') ||
                (c >= '\u3130' && c <= '\u318F') ||
                (c >= '\uAC00' && c <= '\uD7AF') ||
                (c >= '\u4E00' && c <= '\u9FFF') ||
                (c >= '\u3040' && c <= '\u30FF'))
            {
                return true;
            }
        }

        return false;
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
        forceHideSpeechBubble = false;
        pendingLinePresentations.Clear();
        activeLinePresentations.Clear();
        currentLineIndex = -1;
        bool preserveStoryPresentationState =
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "STORY";
        if (!preserveStoryPresentationState)
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

            if (TemporaryStorySceneFlow.HasPendingStory())
                return;

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
                targetCharacterId = src.targetCharacterId,
                animationTrigger = src.animationTrigger,
                animationClip = src.animationClip,
                animationClipName = src.animationClipName,
                sneakersAnimationClip = src.sneakersAnimationClip,
                sneakersAnimationClipName = src.sneakersAnimationClipName,
                soundEffectName = src.soundEffectName,
                beforeTextDelaySeconds = Mathf.Max(0f, src.beforeTextDelaySeconds)
            });
        }
    }

    private List<DialogueLinePresentation> ResolveLinePresentations(DialogueLine line, int lineIndex)
    {
        var matches = new List<DialogueLinePresentation>();

        if (activeLinePresentations.Count == 0)
            return matches;

        int lineNumber = lineIndex + 1;

        for (int i = 0; i < activeLinePresentations.Count; i++)
        {
            DialogueLinePresentation p = activeLinePresentations[i];
            if (p == null)
                continue;

            bool matchedById = MatchesLineId(p, line.lineID);
            bool matchedByRange = MatchesLineIndexRange(p, lineNumber);
            if (matchedById || matchedByRange)
                matches.Add(p);
        }

        return matches;
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

        if (animator != null && presentationSource != null && presentationSource.targetAnimator == null)
            presentationSource.targetAnimator = animator;

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
            targetCharacterId = !string.IsNullOrEmpty(specific.targetCharacterId) ? specific.targetCharacterId : defaults.targetCharacterId,
            animationTrigger = !string.IsNullOrEmpty(specific.animationTrigger) ? specific.animationTrigger : defaults.animationTrigger,
            animationClip = specific.animationClip != null
                ? specific.animationClip
                : (!string.IsNullOrEmpty(specific.animationClipName) ? null : defaults.animationClip),
            animationClipName = !string.IsNullOrEmpty(specific.animationClipName) ? specific.animationClipName : defaults.animationClipName,
            sneakersAnimationClip = specific.sneakersAnimationClip != null
                ? specific.sneakersAnimationClip
                : (!string.IsNullOrEmpty(specific.sneakersAnimationClipName) ? null : defaults.sneakersAnimationClip),
            sneakersAnimationClipName = !string.IsNullOrEmpty(specific.sneakersAnimationClipName) ? specific.sneakersAnimationClipName : defaults.sneakersAnimationClipName,
            soundEffectName = !string.IsNullOrEmpty(specific.soundEffectName) ? specific.soundEffectName : defaults.soundEffectName,
            beforeTextDelaySeconds = specific.beforeTextDelaySeconds > 0f ? specific.beforeTextDelaySeconds : defaults.beforeTextDelaySeconds
        };
    }

    private Transform ResolvePresentationTarget(DialogueLinePresentation presentation, Transform fallbackTarget)
    {
        if (presentation == null)
            return fallbackTarget;

        if (string.IsNullOrEmpty(presentation.targetCharacterId))
            return fallbackTarget;

        if (MatchesCharacterId(currentNpcSpeaker, presentation.targetCharacterId))
            return currentNpcSpeaker;

        if (MatchesCharacterId(currentSpeaker, presentation.targetCharacterId))
            return currentSpeaker;

        if (MatchesCharacterId(fallbackTarget, presentation.targetCharacterId))
            return fallbackTarget;

        Transform resolved = ResolveSpeakerTransform(presentation.targetCharacterId);
        if (resolved != null)
            return resolved;

        Debug.LogWarning($"[DialogueManager] Presentation target not found for character ID '{presentation.targetCharacterId}'. Conversation='{CurrentConversationId}', Line='{CurrentLineId}'.");
        return null;
    }

    private static bool MatchesCharacterId(Transform target, string characterId)
    {
        if (target == null || string.IsNullOrEmpty(characterId))
            return false;

        CharacterIdentifier identifier = target.GetComponent<CharacterIdentifier>();
        if (identifier == null)
            identifier = target.GetComponentInParent<CharacterIdentifier>();
        if (identifier == null)
            identifier = target.GetComponentInChildren<CharacterIdentifier>(true);

        if (identifier != null && !string.IsNullOrEmpty(identifier.characterID) &&
            string.Equals(identifier.characterID, characterId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        CharacterActor actor = target.GetComponent<CharacterActor>();
        if (actor == null)
            actor = target.GetComponentInParent<CharacterActor>();
        if (actor == null)
            actor = target.GetComponentInChildren<CharacterActor>(true);

        return actor != null &&
               !string.IsNullOrEmpty(actor.characterId) &&
               string.Equals(actor.characterId, characterId, StringComparison.OrdinalIgnoreCase);
    }

    private DialogueLinePresentation ResolveSpeakerSpecificPresentation(List<DialogueLinePresentation> matches, string speakerId)
    {
        if (matches == null || matches.Count == 0)
            return null;

        DialogueLinePresentation best = null;
        for (int i = 0; i < matches.Count; i++)
        {
            DialogueLinePresentation p = matches[i];
            if (p == null)
                continue;

            if (string.IsNullOrEmpty(p.targetCharacterId) ||
                string.Equals(p.targetCharacterId, speakerId, StringComparison.OrdinalIgnoreCase))
            {
                if (IsMoreSpecificPresentation(p, best))
                    best = p;
            }
        }

        return best;
    }

    private void PlayAdditionalLinePresentations(List<DialogueLinePresentation> matches, DialogueLinePresentation speakerSpecificPresentation, Transform currentSpeakerTarget, string currentSpeakerId)
    {
        if (matches == null || matches.Count == 0)
            return;

        Dictionary<string, DialogueLinePresentation> bestByTarget = new Dictionary<string, DialogueLinePresentation>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < matches.Count; i++)
        {
            DialogueLinePresentation presentation = matches[i];
            if (presentation == null)
                continue;

            if (ReferenceEquals(presentation, speakerSpecificPresentation))
                continue;

            if (string.IsNullOrEmpty(presentation.targetCharacterId))
                continue;

            if (!string.IsNullOrEmpty(currentSpeakerId) &&
                string.Equals(presentation.targetCharacterId, currentSpeakerId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!bestByTarget.TryGetValue(presentation.targetCharacterId, out DialogueLinePresentation currentBest) ||
                IsMoreSpecificPresentation(presentation, currentBest))
            {
                bestByTarget[presentation.targetCharacterId] = presentation;
            }
        }

        foreach (var pair in bestByTarget)
        {
            DialogueLinePresentation presentation = pair.Value;
            Transform target = ResolvePresentationTarget(presentation, currentSpeakerTarget);
            if (target == null)
                continue;

            DialogueCharacterPresentation targetSource = ResolveSpeakerPresentationComponent(target);
            AnimationClip resolvedClip = ResolvePresentationAnimationClip(presentation, target);
            PlayPresentationVisuals(target, targetSource, resolvedClip, presentation.animationTrigger);
            PlayPresentationSound(presentation, resolvedClip);
        }
    }

    private static float GetLinePresentationDelaySeconds(DialogueLinePresentation speakerPresentation, List<DialogueLinePresentation> matches, DialogueLinePresentation speakerSpecificPresentation)
    {
        float delay = speakerPresentation != null ? Mathf.Max(0f, speakerPresentation.beforeTextDelaySeconds) : 0f;
        if (matches == null)
            return delay;

        for (int i = 0; i < matches.Count; i++)
        {
            DialogueLinePresentation presentation = matches[i];
            if (presentation == null || ReferenceEquals(presentation, speakerSpecificPresentation))
                continue;

            delay = Mathf.Max(delay, Mathf.Max(0f, presentation.beforeTextDelaySeconds));
        }

        return delay;
    }

    private void PlayPresentationVisuals(Transform target, DialogueCharacterPresentation targetSource, AnimationClip presentationClip, string animationTrigger)
    {
        if (target == null)
            return;

        Animator animator = ResolveSpeakerAnimator(target, targetSource, presentationClip != null);
        if (animator == null)
            return;

        bool hasExplicitPresentation = presentationClip != null || !string.IsNullOrEmpty(animationTrigger);
        if (!hasExplicitPresentation)
        {
            // 명시적 연출이 한 번 걸린 타깃은, 다음 줄에 새 연출이 오기 전까지 마지막 상태를 유지한다.
            if (activePresentationClips.ContainsKey(animator) || activePresentationTriggers.ContainsKey(animator))
            {
                currentLinePresentationAnimators.Add(animator);
                return;
            }

            currentLinePresentationAnimators.Remove(animator);
            RestoreAnimatorToDefault(animator);
            return;
        }

        currentLinePresentationAnimators.Add(animator);

        if (targetSource != null && (presentationClip != null || !string.IsNullOrEmpty(animationTrigger)))
        {
            targetSource.SuspendDefaultPresentation();
            suspendedDefaultSources[animator] = targetSource;
        }

        if (presentationClip != null)
        {
            if (activePresentationClips.TryGetValue(animator, out AnimationClip activeClip) &&
                activeClip == presentationClip)
            {
                return;
            }

            PlayPresentationClip(animator, presentationClip);
        }
        else if (!string.IsNullOrEmpty(animationTrigger))
        {
            if (activePresentationTriggers.TryGetValue(animator, out string activeTrigger) &&
                string.Equals(activeTrigger, animationTrigger, StringComparison.Ordinal))
                return;

            StopPresentationAnimationForAnimator(animator);
            animator.SetTrigger(animationTrigger);
            activePresentationTriggers[animator] = animationTrigger;
        }
    }

    private void PlayPresentationSound(DialogueLinePresentation presentation, AnimationClip resolvedClip)
    {
        string soundEffectName = presentation != null ? presentation.soundEffectName : string.Empty;
        AudioClip clip = null;

        if (!string.IsNullOrEmpty(soundEffectName))
        {
            clip = RuntimeAudioClipCatalog.Load(soundEffectName);
        }
        else if (resolvedClip != null && resolvedClip.name.IndexOf("Photo", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            clip = AudioSettingsService.LoadResourceClip(CameraPresentationSfxResource);
        }

        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, AudioSettingsService.ScaleSfx(1f));
    }

    private void PlayPresentationClip(Animator animator, AnimationClip clip)
    {
        if (animator == null || clip == null)
            return;

        StopPresentationAnimationForAnimator(animator);

        PlayableGraph graph = PlayableGraph.Create("DialoguePresentationClip");
        var output = AnimationPlayableOutput.Create(graph, "DialoguePresentation", animator);
        var playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        output.SetSourcePlayable(playable);
        graph.Play();
        presentationGraphs[animator] = graph;
        activePresentationPlayables[animator] = playable;
        activePresentationClips[animator] = clip;
        activePresentationTriggers.Remove(animator);

    }

    private void StopPresentationAnimationImmediate(bool resumeDefaults = true)
    {
        foreach (var pair in presentationGraphs)
        {
            PlayableGraph graph = pair.Value;
            if (graph.IsValid())
                graph.Destroy();
        }

        presentationGraphs.Clear();
        activePresentationPlayables.Clear();
        activePresentationClips.Clear();
        activePresentationTriggers.Clear();
        suspendedDefaultSources.Clear();
        currentLinePresentationAnimators.Clear();

        if (!resumeDefaults)
            return;

        DialogueCharacterPresentation[] defaults = FindObjectsByType<DialogueCharacterPresentation>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < defaults.Length; i++)
            defaults[i].ResumeDefaultPresentation();
    }

    private void StopPresentationAnimationForAnimator(Animator animator)
    {
        if (animator == null)
            return;

        if (presentationGraphs.TryGetValue(animator, out PlayableGraph graph) && graph.IsValid())
            graph.Destroy();

        presentationGraphs.Remove(animator);
        activePresentationPlayables.Remove(animator);
        activePresentationClips.Remove(animator);
        activePresentationTriggers.Remove(animator);
    }

    private void RestoreInactivePresentationTargets()
    {
        if (presentationGraphs.Count == 0 && suspendedDefaultSources.Count == 0)
            return;

        List<Animator> toRestore = new List<Animator>();

        foreach (var pair in presentationGraphs)
        {
            Animator animator = pair.Key;
            if (animator == null || currentLinePresentationAnimators.Contains(animator))
                continue;

            toRestore.Add(animator);
        }

        foreach (var pair in suspendedDefaultSources)
        {
            Animator animator = pair.Key;
            if (animator == null || currentLinePresentationAnimators.Contains(animator) || toRestore.Contains(animator))
                continue;

            toRestore.Add(animator);
        }

        for (int i = 0; i < toRestore.Count; i++)
            RestoreAnimatorToDefault(toRestore[i]);
    }

    private void RestoreAnimatorToDefault(Animator animator)
    {
        if (animator == null)
            return;

        StopPresentationAnimationForAnimator(animator);

        if (suspendedDefaultSources.TryGetValue(animator, out DialogueCharacterPresentation source) && source != null)
            source.ResumeDefaultPresentation();

        suspendedDefaultSources.Remove(animator);
    }

    private bool IsPresentationClipStillPlaying(Animator animator)
    {
        if (animator == null)
            return false;

        if (!activePresentationPlayables.TryGetValue(animator, out AnimationClipPlayable playable) || !playable.IsValid())
            return false;

        if (!activePresentationClips.TryGetValue(animator, out AnimationClip clip) || clip == null)
            return false;

        if (clip.isLooping)
            return true;

        double duration = playable.GetDuration();
        if (duration <= 0d)
            duration = clip.length;

        if (duration <= 0d)
            return true;

        return playable.GetTime() < duration - 0.01d;
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

    private static bool MatchesLineIndexRange(DialogueLinePresentation presentation, int lineIndex)
    {
        if (presentation == null || lineIndex < 0)
            return false;

        if (presentation.lineIndexStart < 0 || presentation.lineIndexEnd < 0)
            return false;

        return lineIndex >= presentation.lineIndexStart && lineIndex <= presentation.lineIndexEnd;
    }

    private static bool IsMoreSpecificPresentation(DialogueLinePresentation candidate, DialogueLinePresentation current)
    {
        if (candidate == null)
            return false;
        if (current == null)
            return true;

        int candidateScore = GetPresentationSpecificityScore(candidate);
        int currentScore = GetPresentationSpecificityScore(current);
        return candidateScore >= currentScore;
    }

    private static int GetPresentationSpecificityScore(DialogueLinePresentation presentation)
    {
        if (presentation == null)
            return int.MinValue;

        int score = 0;

        if (!string.IsNullOrEmpty(presentation.lineID))
            score += 10000;

        if (presentation.lineIndexStart >= 0 && presentation.lineIndexEnd >= 0)
        {
            int width = Mathf.Max(0, presentation.lineIndexEnd - presentation.lineIndexStart);
            score += 5000 - Mathf.Min(width, 4999);
        }

        if (!string.IsNullOrEmpty(presentation.targetCharacterId))
            score += 1000;

        if (presentation.animationClip != null)
            score += 100;

        if (!string.IsNullOrEmpty(presentation.animationTrigger))
            score += 10;

        return score;
    }

    private AnimationClip ResolvePresentationAnimationClip(DialogueLinePresentation presentation, Transform target)
    {
        if (presentation == null)
            return null;

        bool usesSlippers = IsTargetUsingSlippers(target, presentation.targetCharacterId);
        if (!usesSlippers)
        {
            if (presentation.sneakersAnimationClip != null)
                return presentation.sneakersAnimationClip;

            if (!string.IsNullOrWhiteSpace(presentation.sneakersAnimationClipName))
            {
                AnimationClip sneakersClip = RuntimeAnimationClipCatalog.Load(presentation.sneakersAnimationClipName);
                if (sneakersClip == null)
                {
                    Debug.LogWarning($"[DialogueManager] Sneakers animation clip '{presentation.sneakersAnimationClipName}' not found. Conversation='{CurrentConversationId}', Line='{CurrentLineId}', Target='{presentation.targetCharacterId}'.");
                }

                return sneakersClip;
            }
        }

        if (presentation.animationClip != null)
            return presentation.animationClip;

        if (!string.IsNullOrWhiteSpace(presentation.animationClipName))
        {
            AnimationClip clip = RuntimeAnimationClipCatalog.Load(presentation.animationClipName);
            if (clip == null)
            {
                Debug.LogWarning($"[DialogueManager] Animation clip '{presentation.animationClipName}' not found. Conversation='{CurrentConversationId}', Line='{CurrentLineId}', Target='{presentation.targetCharacterId}'.");
            }

            return clip;
        }

        return null;
    }

    private List<DialogueLinePresentation> BuildCsvLinePresentations(DialogueLine line)
    {
        if (line == null)
            return null;

        if (line.csvPresentations != null && line.csvPresentations.Count > 0)
        {
            List<DialogueLinePresentation> cloned = new List<DialogueLinePresentation>(line.csvPresentations.Count);
            for (int i = 0; i < line.csvPresentations.Count; i++)
            {
                DialogueLinePresentation src = line.csvPresentations[i];
                if (src == null)
                    continue;

                cloned.Add(new DialogueLinePresentation
                {
                    lineID = line.lineID,
                    lineIndexStart = -1,
                    lineIndexEnd = -1,
                    targetCharacterId = src.targetCharacterId,
                    animationTrigger = src.animationTrigger,
                    animationClip = src.animationClip,
                    animationClipName = src.animationClipName,
                    sneakersAnimationClip = src.sneakersAnimationClip,
                    sneakersAnimationClipName = src.sneakersAnimationClipName,
                    soundEffectName = src.soundEffectName,
                    beforeTextDelaySeconds = Mathf.Max(0f, src.beforeTextDelaySeconds)
                });
            }

            if (cloned.Count > 0)
                return cloned;
        }

        bool hasPresentation =
            !string.IsNullOrWhiteSpace(line.animationTrigger) ||
            !string.IsNullOrWhiteSpace(line.targetCharacterId) ||
            !string.IsNullOrWhiteSpace(line.animationClipName) ||
            !string.IsNullOrWhiteSpace(line.sneakersAnimationClipName) ||
            !string.IsNullOrWhiteSpace(line.soundEffectName) ||
            line.beforeTextDelaySeconds > 0f;

        if (!hasPresentation)
            return null;

        return new List<DialogueLinePresentation>
        {
            new DialogueLinePresentation
            {
                lineID = line.lineID,
                lineIndexStart = -1,
                lineIndexEnd = -1,
                targetCharacterId = line.targetCharacterId,
                animationTrigger = line.animationTrigger,
                animationClip = null,
                animationClipName = line.animationClipName,
                sneakersAnimationClip = null,
                sneakersAnimationClipName = line.sneakersAnimationClipName,
                soundEffectName = line.soundEffectName,
                beforeTextDelaySeconds = Mathf.Max(0f, line.beforeTextDelaySeconds)
            }
        };
    }

    private bool IsTargetUsingSlippers(Transform target, string targetCharacterId)
    {
        if (target != null && target.GetComponent<PlayerShoeVisual>() != null)
            return FlowManager.Instance != null && FlowManager.Instance.IsWearingSlippers;

        if (!string.IsNullOrEmpty(targetCharacterId) && IsPlayerSpeakerId(targetCharacterId))
            return FlowManager.Instance != null && FlowManager.Instance.IsWearingSlippers;

        return false;
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

public static class RuntimeAnimationClipCatalog
{
    private const string CatalogResourcePath = "DialogueAnimationClipCatalog";
    private static Dictionary<string, AnimationClip> clipByKey;
    private static bool initialized;

    public static AnimationClip Load(string clipNameOrKey)
    {
        if (string.IsNullOrWhiteSpace(clipNameOrKey))
            return null;

        EnsureInitialized();

        string normalizedInput = NormalizeKey(clipNameOrKey);
        if (clipByKey.TryGetValue(normalizedInput, out var clip))
            return clip;

        string fileNameOnly = NormalizeKey(GetLastSegment(clipNameOrKey));
        if (clipByKey.TryGetValue(fileNameOnly, out clip))
            return clip;

#if UNITY_EDITOR
        clip = TryLoadFromAssetDatabase(fileNameOnly);
        if (clip != null)
        {
            AddKey(normalizedInput, clip);
            AddKey(fileNameOnly, clip);
            return clip;
        }
#endif

        return null;
    }

    private static void EnsureInitialized()
    {
        if (initialized)
            return;

        clipByKey = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

        DialogueAnimationClipCatalogAsset catalog = Resources.Load<DialogueAnimationClipCatalogAsset>(CatalogResourcePath);
        if (catalog != null && catalog.Entries != null)
        {
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                DialogueAnimationClipCatalogAsset.Entry entry = catalog.Entries[i];
                if (entry == null || entry.clip == null)
                    continue;

                AddKey(entry.key, entry.clip);
                AddKey(entry.clip.name, entry.clip);
            }
        }

        AnimationClip[] clips = Resources.FindObjectsOfTypeAll<AnimationClip>();
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null)
                continue;

            AddKey(clip.name, clip);
        }

        RuntimeAnimatorController[] controllers = Resources.FindObjectsOfTypeAll<RuntimeAnimatorController>();
        for (int i = 0; i < controllers.Length; i++)
        {
            RuntimeAnimatorController controller = controllers[i];
            if (controller == null)
                continue;

            AnimationClip[] controllerClips = controller.animationClips;
            for (int j = 0; j < controllerClips.Length; j++)
            {
                AnimationClip clip = controllerClips[j];
                if (clip == null)
                    continue;

                AddKey(clip.name, clip);
            }
        }

        initialized = true;
    }

    private static void AddKey(string key, AnimationClip clip)
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

#if UNITY_EDITOR
    private static AnimationClip TryLoadFromAssetDatabase(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return null;

        string[] guids = AssetDatabase.FindAssets($"t:AnimationClip {clipName}");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
                continue;

            if (string.Equals(clip.name, clipName, StringComparison.OrdinalIgnoreCase))
                return clip;
        }

        return null;
    }
#endif
}
