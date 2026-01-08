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

    void Start()
    {
        // (v1.3) 'LoadDialogueFileForState' ȣ�� ����
        ChangeState(currentState);
    }

    // =============================================================
    // (v1.4) ���� ���¸� �����ϴ� �ٽ� �Լ� (������)
    // =============================================================
    public void ChangeState(GameState newState)
    {
        currentState = newState;
        UnityEngine.Debug.Log("���ο� ���·� ����: " + newState.ToString());

        switch (currentState)
        {
            // --- 1~4���� ���� ---
            case GameState.Subway:
                playerController.enabled = false;
                // (���丮 ��) 1���� ����ö �� ���� ��� (�뺻 ID ����)
                if (currentDay == 1) dialogueManager.StartDialogue("SUBWAY_DAY1_INTRO", null);
                // (2~4������ �ٸ� �뺻 ID ���)
                break;

            case GameState.Morning_Slippers:
                playerController.enabled = true; // �ǳ�ȭ ���ƽ����� ���� ��
                // (���߿�) '�ǳ�ȭ' Ʈ���ſ� ������ ���� ���·�
                break;

            case GameState.Morning_Assembly:
                playerController.enabled = false;
                // (���丮 ��) ��¥�� �´� ���� �뺻 ID ���� ���
                dialogueManager.StartDialogue("ASSEMBLY_DAY" + currentDay, null); // ��: "ASSEMBLY_DAY1"
                break;

            // --- 1~4���� ��ƾ ---
            case GameState.Class_Intro_1:
                playerController.enabled = false;
                dialogueManager.StartDialogue("CLASS1_INTRO_DAY" + currentDay, null);
                break;
            case GameState.Class_Minigame_1:
                playerController.enabled = false;
                // (���߿�) '���� �̴ϰ��� 1' �Ŵ��� Ȱ��ȭ
                break;
            case GameState.Class_Outro_1:
                playerController.enabled = false;
                dialogueManager.StartDialogue("CLASS1_OUTRO_DAY" + currentDay, null);
                break;

            case GameState.Lunch_Run:
                playerController.enabled = false;
                // (���߿�) '�޽Ľ� �޸���' �̴ϰ��� Ȱ��ȭ
                break;
            case GameState.Lunch_Tetris:
                playerController.enabled = false;
                // (���߿�) '���� ��Ʈ����' �̴ϰ��� Ȱ��ȭ
                break;
            case GameState.Lunch_FreeTime:
                playerController.enabled = true; // ���� �ð�
                break;

            case GameState.Class_Intro_2:
                playerController.enabled = false;
                dialogueManager.StartDialogue("CLASS2_INTRO_DAY" + currentDay, null);
                break;
            case GameState.Class_Minigame_2:
                playerController.enabled = false;
                // (���߿�) '���� �̴ϰ��� 2' �Ŵ��� Ȱ��ȭ
                break;
            case GameState.Class_Outro_2:
                playerController.enabled = false;
                dialogueManager.StartDialogue("CLASS2_OUTRO_DAY" + currentDay, null);
                break;

            case GameState.Closing_Assembly:
                playerController.enabled = false;
                dialogueManager.StartDialogue("CLOSING_DAY" + currentDay, null);
                break;
            case GameState.AfterSchool:
                playerController.enabled = true; // ���� �ð�
                break;
            case GameState.GoHome:
                playerController.enabled = false;
                currentDay++; // ��¥ +1
                ChangeState(GameState.Subway); // (�ӽ�)
                // (���߿�) SceneManager.LoadScene("SubwayScene");
                break;

            // --- 5���� (�ݿ���) ��ƾ ---
            case GameState.Day5_BigCleaning:
                playerController.enabled = false;
                // (���߿�) '�ٴ� ����' �̴ϰ��� Ȱ��ȭ
                break;
            case GameState.Day5_LockerCleaning:
                playerController.enabled = false;
                // (���߿�) '�繰�� ����' �̴ϰ���/�� Ȱ��ȭ
                break;
            case GameState.Day5_BagPacking:
                playerController.enabled = false;
                // (���߿�) '���� �α� ����' �̴ϰ��� Ȱ��ȭ
                break;
            case GameState.Day5_FreeTime:
                playerController.enabled = true; // 5���� ���� �ð�
                break;
            case GameState.Day5_ClosingAssembly:
                playerController.enabled = false;
                dialogueManager.StartDialogue("CLOSING_DAY5", null); // 5���� ����
                break;
            case GameState.Day5_LunchChoice:
                playerController.enabled = false;
                dialogueManager.StartDialogue("LUNCH_CHOICE_DAY5", null); // (���߿� '������' ��� �ʿ�)
                break;
            case GameState.Day5_EndingCredits:
                playerController.enabled = false;
                // (���߿�) ���� ũ���� �� �ε�
                break;
        }
    }

    // (DialogueFinished, MinigameFinished �Լ��� v1.3�� ����)
    // 대화 종료 시 다음 상태로 전환
    public void DialogueFinished()
    {
        UnityEngine.Debug.Log("대화가 종료되었습니다. 현재 상태: " + currentState.ToString());

        switch (currentState)
        {
            // --- 1~4일차 아침 ---
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

    // (v1.3�� LoadDialogueFileForState �Լ��� ������)
}