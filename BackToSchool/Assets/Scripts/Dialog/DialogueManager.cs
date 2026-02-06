using System.Collections;
using System.Collections.Generic;
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
 * - 3. 선택지 시스템 (최대 4개, 화살표 선택)
 * - 4. 소리 이펙트 재생
 * - 5. CharacterIdentifier 캐싱으로 성능 개선
 * ===================================================================================
 */
public class DialogueManager : MonoBehaviour
{
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
    public PlayerController playerController;
    public KeyCode nextSentenceKey = KeyCode.E;

    [Header("선택지 UI")]
    public GameObject choicePanel;
    public GameObject[] choiceButtons = new GameObject[4]; // 최대 4개 선택지 버튼
    public TextMeshProUGUI[] choiceTexts = new TextMeshProUGUI[4]; // 선택지 텍스트
    public GameObject choiceArrow; // 선택지 화살표

    [Header("오디오")]
    public AudioSource audioSource; // 소리 이펙트 재생용

    private Queue<DialogueLine> lines;
    private bool isTyping = false;
    private DialogueLine currentLine;
    public bool IsDialogueActive { get; private set; }
    public bool inputConsumedThisFrame { get; private set; } = false;
    private GameManager gameManager;
    private Transform currentSpeaker;
    private Transform currentNpcSpeaker;

    // 선택지 관련
    private bool isWaitingForChoice = false;
    private int selectedChoiceIndex = 0;
    private List<DialogueChoice> currentChoices = new List<DialogueChoice>();

    // 성능 개선: CharacterIdentifier 캐싱
    private Dictionary<string, CharacterIdentifier> characterCache = new Dictionary<string, CharacterIdentifier>();
    private bool blockAdvanceInputThisFrame = false;

    void Start()
    {
        lines = new Queue<DialogueLine>();

        RebindForScene();


        IsDialogueActive = false;
        gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null) UnityEngine.Debug.LogError("GameManager를 찾을 수 없습니다!");

        // 선택지 UI 초기화
        if (choicePanel != null) choicePanel.SetActive(false);
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] != null) choiceButtons[i].SetActive(false);
        }

        // CharacterIdentifier 캐싱
        RefreshCharacterCache();
    }

    void Update()
    {
        if (!IsDialogueActive) return;

        if (blockAdvanceInputThisFrame) return;

        if (isBusy) return;

        if (isWaitingForChoice)
        {
            HandleChoiceInput();
            return;
        }
       


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
                : FindObjectOfType<Canvas>();

            if (canvas == null) return;

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
                    bubbleRect.anchoredPosition = localPoint + new Vector2(0f, -150f);

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
                // ScreenSpaceOverlay/Camera 아무거나 OK
                best = canvases[j];
                return best;
            }
        }
        return best;
    }


    // CharacterIdentifier 캐시 새로고침
    private void RefreshCharacterCache()
    {
        characterCache.Clear();
        CharacterIdentifier[] allCharacters = FindObjectsOfType<CharacterIdentifier>();
        foreach (CharacterIdentifier character in allCharacters)
        {
            if (!string.IsNullOrEmpty(character.characterID))
            {
                characterCache[character.characterID] = character;
            }
        }
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
        isWaitingForChoice = false;
        
        if (dialogueText == null)
        {
            Debug.LogError("[DialogueManager] dialogueText가 null입니다. SpeechBubbleUI 연결을 확인하세요.");
            return;
        }
        dialogueText.text = "";

        if (speechBubble != null) speechBubble.gameObject.SetActive(true);
        IsDialogueActive = true;
        currentNpcSpeaker = npcSpeaker;

        if (playerController != null) playerController.enabled = false;

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

            // 선택지가 있으면 표시
            /*if (currentLine.hasChoices && currentLine.choices.Count > 0)
            {
                ShowChoices();
                return;
            }*/

            if (lines.Count == 0) EndDialogue();
            return;
        }

        if (lines.Count == 0)
        {
            EndDialogue();
            return;
        }

        currentLine = lines.Dequeue();
        string currentSpeakerID = currentLine.speakerID;

        // 캐시에서 캐릭터 찾기
        currentSpeaker = null;
        
        // 먼저 원본 ID로 찾기 시도
        if (characterCache.ContainsKey(currentSpeakerID))
        {
            currentSpeaker = characterCache[currentSpeakerID].transform;
        }
        else
        {
            // NAME_ 접두사 제거 후 찾기 시도 (예: NAME_SWORD -> SWORD)
            string characterIDWithoutPrefix = currentSpeakerID;
            if (currentSpeakerID.StartsWith("NAME_"))
            {
                characterIDWithoutPrefix = currentSpeakerID.Substring(5); // "NAME_" 제거
            }
            
            if (characterCache.ContainsKey(characterIDWithoutPrefix))
            {
                currentSpeaker = characterCache[characterIDWithoutPrefix].transform;
            }
            else
            {
                // 캐시 새로고침 후 다시 시도
                RefreshCharacterCache();
                if (characterCache.ContainsKey(currentSpeakerID))
                {
                    currentSpeaker = characterCache[currentSpeakerID].transform;
                }
                else if (characterCache.ContainsKey(characterIDWithoutPrefix))
                {
                    currentSpeaker = characterCache[characterIDWithoutPrefix].transform;
                }
                else
                {
                    // 찾지 못하면 기본 NPC 스피커 사용
                    currentSpeaker = currentNpcSpeaker;
                }
            }
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

        // 타이핑 완료 후 선택지 표시 (대화 중간에도 가능)
        /*if (currentLine.hasChoices && currentLine.choices.Count > 0)
        {
            yield return new WaitForSeconds(0.1f); // 짧은 딜레이
            ShowChoices();
        }*/
    }

    // 선택지 표시
    void ShowChoices()
    {
        if (currentLine.choices == null || currentLine.choices.Count == 0) return;

        isWaitingForChoice = true;
        currentChoices = currentLine.choices;
        selectedChoiceIndex = 0;

        if (choicePanel != null) choicePanel.SetActive(true);

        // 선택지 버튼 활성화 및 텍스트 설정
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (i < currentChoices.Count)
            {
                choiceButtons[i].SetActive(true);
                string choiceText = LocalizationManager.Instance.GetLine(currentChoices[i].choiceTextID);
                choiceTexts[i].text = choiceText;
            }
            else
            {
                choiceButtons[i].SetActive(false);
            }
        }

        UpdateChoiceArrow();
    }

    // 선택지 입력 처리
    void HandleChoiceInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            selectedChoiceIndex = Mathf.Max(0, selectedChoiceIndex - 1);
            UpdateChoiceArrow();
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            selectedChoiceIndex = Mathf.Min(currentChoices.Count - 1, selectedChoiceIndex + 1);
            UpdateChoiceArrow();
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
        {
            SelectChoice(selectedChoiceIndex);
        }
    }

    // 선택지 화살표 업데이트
    void UpdateChoiceArrow()
    {
        if (choiceArrow == null || selectedChoiceIndex >= choiceButtons.Length) return;

        GameObject selectedButton = choiceButtons[selectedChoiceIndex];
        if (selectedButton != null && selectedButton.activeSelf)
        {
            RectTransform buttonRect = selectedButton.GetComponent<RectTransform>();
            RectTransform arrowRect = choiceArrow.GetComponent<RectTransform>();
            if (buttonRect != null && arrowRect != null)
            {
                arrowRect.position = new Vector3(
                    buttonRect.position.x - buttonRect.rect.width / 2 - 20f,
                    buttonRect.position.y,
                    arrowRect.position.z
                );
            }
        }
    }

    // 선택지 선택
    void SelectChoice(int index)
    {
        if (index < 0 || index >= currentChoices.Count) return;

        DialogueChoice selectedChoice = currentChoices[index];

        // 선택지 UI 숨기기
        if (choicePanel != null) choicePanel.SetActive(false);
        isWaitingForChoice = false;

        // 씬 이동
        if (!string.IsNullOrEmpty(selectedChoice.sceneToLoad))
        {
            SceneManager.LoadScene(selectedChoice.sceneToLoad);
            EndDialogue();
            return;
        }

        // 게임 상태 변경
        if (selectedChoice.stateToChange != GameState.Morning_Slippers && gameManager != null)
        {
            gameManager.ChangeState(selectedChoice.stateToChange);
        }

        // 다음 대화 시작 또는 종료
        if (!string.IsNullOrEmpty(selectedChoice.nextConversationID))
        {
            StartDialogue(selectedChoice.nextConversationID, currentNpcSpeaker);
        }
        else
        {
            EndDialogue();
        }
    }

    public void EndDialogue()
    {
        // 모든 코루틴 중지
        StopAllCoroutines();

        if (speechBubble != null) speechBubble.gameObject.SetActive(false);
        if (choicePanel != null) choicePanel.SetActive(false);
        IsDialogueActive = false;
        isWaitingForChoice = false;
        currentSpeaker = null;
        currentNpcSpeaker = null;
        currentChoices.Clear();

        // STORY 씬이면 FlowManager에게 "이번 이벤트 끝남"만 보고
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "STORY")
        {
            if (FlowManager.Instance != null)
                FlowManager.Instance.CompleteCurrentEvent(0);
            else
                Debug.LogError("[DialogueManager] STORY 씬인데 FlowManager가 없음");
        }
        else
        {
            // 나머지(자유이동/NPC대화 등)는 기존대로
            if (playerController != null) playerController.enabled = true;
            if (gameManager != null) gameManager.DialogueFinished();
        }

    }
}
