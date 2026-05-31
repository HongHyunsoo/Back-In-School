using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keeps legacy GameState transitions synchronized with scene-based flow.
public class GameManager : MonoBehaviour
{
    public int currentDay = 1;
    public GameState currentState;
    public PlayerController playerController;
    public DialogueManager dialogueManager;



    private IEnumerator Start()
    {
        // 1) Inspector 참조가 없으면 현재 씬에서 자동으로 찾는다.
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();

        if (dialogueManager == null)
            dialogueManager = FindAnyObjectByType<DialogueManager>();

        // 2) LocalizationManager가 준비될 때까지 최대 2초간 기다린다.
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
        // 씬이 바뀔 때마다 상태를 다시 맞춘다.
        ForceStateByScene(scene.name);
        ChangeState(currentState);

        // 씬 전환 후 DialogueManager 참조를 다시 연결한다.
        if (dialogueManager == null) dialogueManager = FindAnyObjectByType<DialogueManager>();
        if (dialogueManager != null)
        {
            dialogueManager.RebindForScene();
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
                currentState = ResolveFreeRoamStateByFlowContext();
                break;

            case "STORY":
                // 스토리 씬은 별도 이벤트에서 상태를 명시적으로 전환한다.
                break;

            case "MINIGAME":
                // 미니게임 씬은 개별 컨트롤러가 상태를 사용한다.
                break;

            // Legacy scene aliases
            case "SubwayScene":
                currentState = GameState.Subway;
                break;

            case "SchoolFreeTime":
                currentState = ResolveFreeRoamStateByFlowContext();
                break;
        }
    }

    private GameState ResolveFreeRoamStateByFlowContext()
    {
        if (FlowContext.IsAfterSchoolFreeRoam())
            return GameState.AfterSchool;
        if (FlowContext.IsMorningBeforeAssemblyFreeRoam())
            return GameState.Morning_Slippers;
        if (FlowContext.IsLunchFreeRoam())
            return GameState.Lunch_FreeTime;
        if (FlowContext.IsDay5FreeRoam())
            return GameState.Day5_FreeTime;

        if (currentState == GameState.Morning_Slippers ||
            currentState == GameState.Lunch_FreeTime ||
            currentState == GameState.AfterSchool ||
            currentState == GameState.Day5_FreeTime)
        {
            return currentState;
        }

        return GameState.Lunch_FreeTime;
    }



    // Applies state-specific player and dialogue behavior.
    public void ChangeState(GameState newState)
    {
        currentState = newState;

        // PlayerController와 DialogueManager는 필요한 씬에서 다시 찾는다.
        if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
        if (dialogueManager == null) dialogueManager = FindAnyObjectByType<DialogueManager>();

        // 지하철 상태에서는 씬 구성상 참조가 없을 수 있으므로 경고를 생략한다.
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
            // 지하철 씬에서는 채팅만 사용한다.
            case GameState.Subway:
                // 지하철에서는 플레이어 이동 비활성화
                if (playerController != null) 
                {
                    playerController.enabled = false;
                }
                // ChatService가 현재 상태에 맞는 채팅 세그먼트를 활성화한다.
                if (ChatService.Instance != null)
                {
                    ChatService.Instance.ActivateSegmentsFor(currentDay, GameState.Subway);
                }
                
                break;


            case GameState.Morning_Slippers:
                if (playerController != null)
                    playerController.enabled = true; // 실내화 자유 이동
                else
                    Debug.LogWarning("[GameManager] playerController가 null입니다. (Morning_Slippers)");
                // 실내화 관련 상호작용은 맵 트리거에서 처리한다.
                break;

            case GameState.Morning_Assembly:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (Morning_Assembly)");
                // 날짜에 맞는 조회 대화를 시작한다.
                if (dialogueManager != null)
                    dialogueManager.StartDialogue("ASSEMBLY_DAY" + currentDay, null); // 예: "ASSEMBLY_DAY1"
                else
                    Debug.LogWarning("[GameManager] dialogueManager가 null이라 Morning_Assembly 대화를 시작할 수 없습니다.");
                break;

            // --- 1~4일차 수업 ---
            case GameState.Class_Intro_1:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (Class_Intro_1)");

                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLASS1_INTRO_DAY" + currentDay, null);
                else
                    Debug.LogWarning("[GameManager] dialogueManager가 null이라 Class_Intro_1 대화를 시작할 수 없습니다.");
                break;
            case GameState.Class_Minigame_1:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (Class_Minigame_1)");
                // 수업 미니게임 1 활성화
                break;
            case GameState.Class_Outro_1:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (Class_Outro_1)");

                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLASS1_OUTRO_DAY" + currentDay, null);
                else
                    Debug.LogWarning("[GameManager] dialogueManager가 null이라 Class_Outro_1 대화를 시작할 수 없습니다.");
                break;

            case GameState.Lunch_Run:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (Lunch_Run)");
                // 점심 달리기 미니게임 활성화
                break;
            case GameState.Lunch_Tetris:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (Lunch_Tetris)");
                // 점심 테트리스 미니게임 활성화
                break;
            case GameState.Lunch_FreeTime:
                if (playerController != null)
                    playerController.enabled = true; // 점심 자유 시간
                else
                    Debug.LogWarning("[GameManager] playerController가 null입니다. (Lunch_FreeTime)");
                break;

            case GameState.Class_Intro_2:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (Class_Intro_2)");

                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLASS2_INTRO_DAY" + currentDay, null);
                else
                    Debug.LogWarning("[GameManager] dialogueManager가 null이라 Class_Intro_2 대화를 시작할 수 없습니다.");
                break;
            case GameState.Class_Minigame_2:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (Class_Minigame_2)");
                // 수업 미니게임 2 활성화
                break;
            case GameState.Class_Outro_2:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (Class_Outro_2)");

                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLASS2_OUTRO_DAY" + currentDay, null);
                else
                    Debug.LogWarning("[GameManager] dialogueManager가 null이라 Class_Outro_2 대화를 시작할 수 없습니다.");
                break;

            case GameState.Closing_Assembly:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (Closing_Assembly)");

                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLOSING_DAY" + currentDay, null);
                else
                    Debug.LogWarning("[GameManager] dialogueManager가 null이라 Closing_Assembly 대화를 시작할 수 없습니다.");
                break;
            case GameState.AfterSchool:
                if (playerController != null)
                    playerController.enabled = true; // 방과후 자유 시간
                else
                    Debug.LogWarning("[GameManager] playerController가 null입니다. (AfterSchool)");
                break;
            case GameState.GoHome:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (GoHome)");
                currentDay++; // 날짜 +1
                ChangeState(GameState.Subway); // 다음 날 등교
                // Legacy: SceneManager.LoadScene("SubwayScene");
                break;

            // --- 5일차 특별 수업 ---
            case GameState.Day5_BigCleaning:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (Day5_BigCleaning)");
                // 추후 대청소 미니게임 활성화
                break;
            case GameState.Day5_LockerCleaning:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (Day5_LockerCleaning)");
                // 추후 사물함 정리 미니게임 활성화
                break;
            case GameState.Day5_BagPacking:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (Day5_BagPacking)");
                // 추후 가방 챙기기 미니게임 활성화
                break;
            case GameState.Day5_FreeTime:
                if (playerController != null)
                    playerController.enabled = true; // 5일차 방과후 자유 시간
                else
                    Debug.LogWarning("[GameManager] playerController가 null입니다. (Day5_FreeTime)");
                break;
            case GameState.Day5_ClosingAssembly:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (Day5_ClosingAssembly)");
                
                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLOSING_DAY5", null); // 5일차 종례
                else
                    Debug.LogWarning("[GameManager] dialogueManager가 null이라 Day5_ClosingAssembly 대화를 시작할 수 없습니다.");
                break;
            case GameState.Day5_LunchChoice:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (Day5_LunchChoice)");
                
                if (dialogueManager != null)
                    dialogueManager.StartDialogue("LUNCH_CHOICE_DAY5", null); // 추후 선택지 분기 필요
                else
                    Debug.LogWarning("[GameManager] dialogueManager가 null이라 Day5_LunchChoice 대화를 시작할 수 없습니다.");
                break;
            case GameState.Day5_EndingCredits:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController가 null입니다. (Day5_EndingCredits)");
                // 추후 엔딩 크레딧 로드
                break;
        }
    }

    // 대화 종료 후 다음 상태로 전환한다.
    // Subway 상태에는 DialogueManager가 없으므로 채팅 UI에서 별도로 상태를 전환한다.
    public void DialogueFinished()
    {
        UnityEngine.Debug.Log("대화가 종료되었습니다. 현재 상태: " + currentState.ToString());

        switch (currentState)
        {
            // --- 1~4일차 아침 ---
            // Subway 상태에서는 채팅 종료 후 ChatService 또는 ChatRoomDetailUI에서 직접 전환한다.
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
                ChangeState(GameState.Lunch_Run); // 필요 시 Lunch_Tetris, Lunch_FreeTime으로 분기 가능
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

            // --- 5일차 특별 수업 ---
            case GameState.Day5_ClosingAssembly:
                ChangeState(GameState.Day5_LunchChoice);
                break;

            case GameState.Day5_LunchChoice:
                // 선택지에서 분기 처리
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
    /// 지하철 씬에서 채팅 종료 후 호출하는 함수.
    /// ChatService 또는 ChatRoomDetailUI에서 호출할 수 있다.
    /// </summary>
    public void SubwayChatFinished()
    {
        if (currentState == GameState.Subway)
        {
            UnityEngine.Debug.Log("[GameManager] 지하철 채팅 종료, 다음 상태로 전환");
            ChangeState(GameState.Morning_Slippers);
        }
    }

    // Legacy LoadDialogueFileForState helper removed.
}

