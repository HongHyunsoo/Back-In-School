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
    [Header("UI Elements (TextMeshPro)")]
    public GameObject speechBubbleObject;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogueText;
    public float typingSpeed = 0.03f;
    public Vector3 worldOffset = new Vector3(0, 2f, 0);
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
        if (speechBubbleObject == null)
        {
            Debug.LogError("[DialogueManager] speechBubbleObject가 인스펙터에 연결되지 않았습니다.");
        }
        else
        {
            speechBubbleObject.SetActive(false);
        }
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
        if (IsDialogueActive && currentSpeaker != null && speechBubbleObject != null)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 targetPos = currentSpeaker.position + worldOffset;
                Vector3 screenPos = cam.WorldToScreenPoint(targetPos);
                speechBubbleObject.transform.position = screenPos;
            }
            else
            {
                Debug.LogWarning("[DialogueManager] Camera.main 이 null 입니다. 말풍선 위치를 갱신할 수 없습니다.");
            }
        }
        inputConsumedThisFrame = false;
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
        dialogueText.text = "";

        speechBubbleObject.SetActive(true);
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


    public void DisplayNextSentence()
    {
        // 타이핑 중이면 즉시 완성
        if (isTyping)
        {
            StopAllCoroutines();
            dialogueText.text = LocalizationManager.Instance.GetLine(currentLine.lineID);
            isTyping = false;

            // 선택지가 있으면 표시
            if (currentLine.hasChoices && currentLine.choices.Count > 0)
            {
                ShowChoices();
                return;
            }

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
        StartCoroutine(TypeSentence(translatedSentence));
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
        if (currentLine.hasChoices && currentLine.choices.Count > 0)
        {
            yield return new WaitForSeconds(0.1f); // 짧은 딜레이
            ShowChoices();
        }
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

        speechBubbleObject.SetActive(false);
        if (choicePanel != null) choicePanel.SetActive(false);
        IsDialogueActive = false;
        isWaitingForChoice = false;
        currentSpeaker = null;
        currentNpcSpeaker = null;
        currentChoices.Clear();

        if (playerController != null) playerController.enabled = true;
        if (gameManager != null) gameManager.DialogueFinished();
    }
}
