using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/*
 * ===================================================================================
 * GameManager (v1.4 - '�뺻 ID' ���� ���)
 * ===================================================================================
 * - [v1.4 ������]
 * - 1. (v1.3) CSV �ε� ���� ��� ���� (LocalizationManager�� Awake���� �˾Ƽ� ��)
 * - 2. (�ű�) ChangeState()�� '���丮 ��' ���°� �Ǹ�,
 * DialogueManager.StartDialogue()�� '�뺻 ID'�� ���� ȣ��
 * ===================================================================================
 */
public class GameManager : MonoBehaviour
{
    // ... (v1.3�� ���� ���� ����) ...
    public int currentDay = 1;
    public GameState currentState;
    public PlayerController playerController;
    public DialogueManager dialogueManager;



    private IEnumerator Start()
    {
        // 1) inspector 미할당이면 자동으로 찾기 (지하철 씬에서는 없을 수 있음 - 정상)
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();

        if (dialogueManager == null)
            dialogueManager = FindAnyObjectByType<DialogueManager>();

        // 2) LocalizationManager 준비될 때까지 대기 (최대 2초 정도)
        float t = 0f;
        while (LocalizationManager.Instance == null && t < 2f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (LocalizationManager.Instance == null)
        {
            Debug.LogError("[GameManager] LocalizationManager.Instance가 준비되지 않았습니다. 상태 전환을 건너뜁니다.");
            yield break;
        }

        // 3) 상태 진입
        ForceStateByScene(SceneManager.GetActiveScene().name);
        ChangeState(currentState);

    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 바뀔 때마다 상태 강제 세팅
        ForceStateByScene(scene.name);
        ChangeState(currentState);

        // (중요) 씬 바뀌면 DialogueManager가 씬 오브젝트 다시 잡아야 함
        if (dialogueManager == null) dialogueManager = FindAnyObjectByType<DialogueManager>();
        if (dialogueManager != null)
        {
            dialogueManager.RebindForScene(); // 아래 2번에서 DialogueManager에 추가할 함수
        }
    }

    private void ForceStateByScene(string sceneName)
    {
        switch (sceneName)
        {
            case "CHAT":
                currentState = GameState.Subway;
                break;

            case "FREEROAM":
                currentState = GameState.Lunch_FreeTime; // 일단 자유이동이면 이걸로
                break;

            case "STORY":
                // 스토리 씬은 플레이어 이동 기본 off일 테니
                // currentState를 굳이 바꿀 필요 없거나, 별도 Story 상태를 두거나
                // 일단 유지해도 됨
                break;

            case "MINIGAME":
                // 미니게임도 이동 off니까 유지/별도 처리
                break;

            // (기존)
            case "SubwayScene":
                currentState = GameState.Subway;
                break;

            case "SchoolFreeTime":
                currentState = GameState.Lunch_FreeTime;
                break;
        }
    }



    // =============================================================
    // (v1.4) ���� ���¸� �����ϴ� �ٽ� �Լ� (������)
    // =============================================================
    public void ChangeState(GameState newState)
    {
        currentState = newState;

        // PlayerController와 DialogueManager는 맵 씬에서만 필요 (지하철 씬에서는 선택사항)
        if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
        if (dialogueManager == null) dialogueManager = FindAnyObjectByType<DialogueManager>();

        // Subway 상태가 아닐 때만 경고 (지하철 씬에서는 정상적으로 없을 수 있음)
        if (newState != GameState.Subway)
        {
            if (playerController == null)
            {
                Debug.LogWarning("[GameManager] PlayerController를 찾을 수 없습니다. (상태: " + newState.ToString() + ")");
            }

            if (dialogueManager == null)
            {
                Debug.LogWarning("[GameManager] DialogueManager를 찾을 수 없습니다. (상태: " + newState.ToString() + ")");
            }
        }

        UnityEngine.Debug.Log("새로운 상태로 전환: " + newState.ToString());

        switch (currentState)
        {
            // --- 1~4일차 아침 ---
            // 지하철 씬: 폰 앱 채팅만 사용, DialogueManager 말풍선 사용 안 함
            case GameState.Subway:
                // PlayerController가 있으면 비활성화 (지하철에서는 이동 불가)
                if (playerController != null) 
                {
                    playerController.enabled = false;
                }
                // DialogueManager는 지하철 씬에서 사용 안 함 (채팅은 폰 앱으로만)
                // ChatService.ActivateSegmentsFor()가 자동으로 채팅 세그먼트를 활성화함
                if (ChatService.Instance != null)
                {
                    ChatService.Instance.ActivateSegmentsFor(currentDay, GameState.Subway);
                }
                
                break;


            case GameState.Morning_Slippers:
                if (playerController != null)
                    playerController.enabled = true; // �ǳ�ȭ ���ƽ����� ���� ��
                else
                    Debug.LogWarning("[GameManager] playerController가 null (Morning_Slippers)");
                // (���߿�) '�ǳ�ȭ' Ʈ���ſ� ������ ���� ���·�
                break;

            case GameState.Morning_Assembly:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (Morning_Assembly)");
                // (���丮 ��) ��¥�� �´� ���� �뺻 ID ���� ���
                if (dialogueManager != null)
                    dialogueManager.StartDialogue("ASSEMBLY_DAY" + currentDay, null); // ��: "ASSEMBLY_DAY1"
                else
                    Debug.LogWarning("[GameManager] dialogueManager가 null (Morning_Assembly StartDialogue 못함)");
                break;

            // --- 1~4���� ��ƾ ---
            case GameState.Class_Intro_1:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (Class_Intro_1)");

                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLASS1_INTRO_DAY" + currentDay, null);
                else
                    Debug.LogWarning("[GameManager] dialogueManager가 null (Class_Intro_1 StartDialogue 못함)");
                break;
            case GameState.Class_Minigame_1:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (Class_Minigame_1)");
                // (���߿�) '���� �̴ϰ��� 1' �Ŵ��� Ȱ��ȭ
                break;
            case GameState.Class_Outro_1:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (Class_Outro_1)");

                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLASS1_OUTRO_DAY" + currentDay, null);
                else
                    Debug.LogWarning("[GameManager] dialogueManager가 null (Class_Outro_1 StartDialogue 못함)");
                break;

            case GameState.Lunch_Run:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (Lunch_Run)");
                // (���߿�) '�޽Ľ� �޸���' �̴ϰ��� Ȱ��ȭ
                break;
            case GameState.Lunch_Tetris:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (Lunch_Tetris)");
                // (���߿�) '���� ��Ʈ����' �̴ϰ��� Ȱ��ȭ
                break;
            case GameState.Lunch_FreeTime:
                if (playerController != null)
                    playerController.enabled = true; // ���� �ð�
                else
                    Debug.LogWarning("[GameManager] playerController가 null (Lunch_FreeTime)");
                break;

            case GameState.Class_Intro_2:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (Class_Intro_2)");

                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLASS2_INTRO_DAY" + currentDay, null);
                else
                    Debug.LogWarning("[GameManager] dialogueManager가 null (Class_Intro_2 StartDialogue 못함)");
                break;
            case GameState.Class_Minigame_2:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (Class_Minigame_2)");
                // (���߿�) '���� �̴ϰ��� 2' �Ŵ��� Ȱ��ȭ
                break;
            case GameState.Class_Outro_2:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (Class_Outro_2)");

                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLASS2_OUTRO_DAY" + currentDay, null);
                else
                    Debug.LogWarning("[GameManager] dialogueManager가 null (Class_Outro_2 StartDialogue 못함)");
                break;

            case GameState.Closing_Assembly:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (Closing_Assembly)");

                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLOSING_DAY" + currentDay, null);
                else
                    Debug.LogWarning("[GameManager] dialogueManager가 null (Closing_Assembly StartDialogue 못함)");
                break;
            case GameState.AfterSchool:
                if (playerController != null)
                    playerController.enabled = true; // ���� �ð�
                else
                    Debug.LogWarning("[GameManager] playerController가 null (AfterSchool)");
                break;
            case GameState.GoHome:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (GoHome)");
                currentDay++; // ��¥ +1
                ChangeState(GameState.Subway); // (�ӽ�)
                // (���߿�) SceneManager.LoadScene("SubwayScene");
                break;

            // --- 5일차 (특별) 수업 ---
            case GameState.Day5_BigCleaning:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (Day5_BigCleaning)");
                // (추후) '대청소' 미니게임 활성화
                break;
            case GameState.Day5_LockerCleaning:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (Day5_LockerCleaning)");
                // (추후) '사물함 정리' 미니게임/씬 활성화
                break;
            case GameState.Day5_BagPacking:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (Day5_BagPacking)");
                // (추후) '가방 싸기' 미니게임 활성화
                break;
            case GameState.Day5_FreeTime:
                if (playerController != null)
                    playerController.enabled = true; // 5일차 방과후 시간
                else
                    Debug.LogWarning("[GameManager] playerController가 null (Day5_FreeTime)");
                break;
            case GameState.Day5_ClosingAssembly:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (Day5_ClosingAssembly)");
                
                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLOSING_DAY5", null); // 5일차 종례
                else
                    Debug.LogWarning("[GameManager] dialogueManager가 null (Day5_ClosingAssembly StartDialogue 못함)");
                break;
            case GameState.Day5_LunchChoice:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (Day5_LunchChoice)");
                
                if (dialogueManager != null)
                    dialogueManager.StartDialogue("LUNCH_CHOICE_DAY5", null); // (추후 '선택지' 분기 필요)
                else
                    Debug.LogWarning("[GameManager] dialogueManager가 null (Day5_LunchChoice StartDialogue 못함)");
                break;
            case GameState.Day5_EndingCredits:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null (Day5_EndingCredits)");
                // (추후) 엔딩 크레딧 씬 로드
                break;
        }
    }

    // (DialogueFinished, MinigameFinished �Լ��� v1.3�� ����)
    // 대화 종료 시 다음 상태로 전환
    // 주의: Subway 상태에서는 DialogueManager가 없으므로 이 함수가 호출되지 않음
    //       지하철 씬에서는 채팅 완료 시 별도로 다음 상태로 전환해야 함
    public void DialogueFinished()
    {
        UnityEngine.Debug.Log("대화가 종료되었습니다. 현재 상태: " + currentState.ToString());

        switch (currentState)
        {
            // --- 1~4일차 아침 ---
            // 주의: Subway 상태에서는 DialogueManager가 없으므로 이 케이스는 실행되지 않음
            //       채팅 완료 시 ChatService나 ChatRoomDetailUI에서 직접 호출 필요
            case GameState.Subway:
                ChangeState(GameState.Morning_Slippers);
                break;

            case GameState.Morning_Assembly:
                ChangeState(GameState.Class_Intro_1);
                break;

            // --- 1~4일차 수업 ---
            case GameState.Class_Intro_1:
                ChangeState(GameState.Class_Minigame_1);
                break;

            case GameState.Class_Outro_1:
                ChangeState(GameState.Lunch_Run); // 또는 Lunch_Tetris, Lunch_FreeTime (선택지로 분기 가능)
                break;

            case GameState.Class_Intro_2:
                ChangeState(GameState.Class_Minigame_2);
                break;

            case GameState.Class_Outro_2:
                ChangeState(GameState.Closing_Assembly);
                break;

            case GameState.Closing_Assembly:
                ChangeState(GameState.AfterSchool);
                break;

            // --- 5일차 (특별) 수업 ---
            case GameState.Day5_ClosingAssembly:
                ChangeState(GameState.Day5_LunchChoice);
                break;

            case GameState.Day5_LunchChoice:
                // 선택지에서 분기 처리됨
                break;

            // 대화가 없는 상태는 그대로 유지
            default:
                break;
        }
    }

    public void MinigameFinished(bool success)
    {
        UnityEngine.Debug.Log("미니게임 종료: " + (success ? "성공" : "실패"));

        switch (currentState)
        {
            case GameState.Class_Minigame_1:
                ChangeState(GameState.Class_Outro_1);
                break;

            case GameState.Class_Minigame_2:
                ChangeState(GameState.Class_Outro_2);
                break;

            case GameState.Lunch_Run:
            case GameState.Lunch_Tetris:
                ChangeState(GameState.Lunch_FreeTime);
                break;

            case GameState.Day5_BigCleaning:
                ChangeState(GameState.Day5_LockerCleaning);
                break;

            case GameState.Day5_LockerCleaning:
                ChangeState(GameState.Day5_BagPacking);
                break;

            case GameState.Day5_BagPacking:
                ChangeState(GameState.Day5_FreeTime);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// 지하철 씬에서 채팅 완료 시 호출하는 함수
    /// ChatService나 ChatRoomDetailUI에서 호출 가능
    /// </summary>
    public void SubwayChatFinished()
    {
        if (currentState == GameState.Subway)
        {
            UnityEngine.Debug.Log("[GameManager] 지하철 채팅 완료, 다음 상태로 전환");
            ChangeState(GameState.Morning_Slippers);
        }
    }

    // (v1.3�� LoadDialogueFileForState �Լ��� ������)
}