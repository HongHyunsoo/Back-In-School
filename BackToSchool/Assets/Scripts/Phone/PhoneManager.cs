using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * ===================================================================================
 * PhoneManager (v2.0 - 언어 전환 기능 추가)
 * ===================================================================================
 * - [v2.0 추가 기능]
 * - 1. 언어 전환 UI 버튼 추가
 * - 2. 런타임 언어 전환 기능
 * ===================================================================================
 */
public class PhoneManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject phoneUIPanel; // 폰 UI 패널 전체
    public KeyCode phoneKey = KeyCode.Tab; // 폰 열기 키

    [Header("언어 전환 UI")]
    public Button languageToggleButton; // 언어 전환 버튼
    public TextMeshProUGUI languageButtonText; // 언어 버튼 텍스트

    // (추가 예정) public GameObject messengerTab;
    // (추가 예정) public GameObject eventsTab;

    private bool isPhoneOpen = false;
    private PlayerController playerController;
    private GameManager gameManager;

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null) UnityEngine.Debug.LogError("GameManager를 찾을 수 없습니다!");

        playerController = gameManager.playerController;

        phoneUIPanel.SetActive(false);
        isPhoneOpen = false;

        // 언어 전환 버튼 설정
        if (languageToggleButton != null)
        {
            languageToggleButton.onClick.AddListener(ToggleLanguage);
        }

        // 언어 변경 이벤트 구독
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged += UpdateLanguageButtonText;
        }

        UpdateLanguageButtonText(LocalizationManager.Instance != null ? LocalizationManager.Instance.GetCurrentLanguage() : Language.Korean);
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= UpdateLanguageButtonText;
        }
    }

    void Update()
    {
        // 1. 'Tab' 키를 눌렀을 때
        if (Input.GetKeyDown(phoneKey))
        {
            // 2. 폰이 닫혀 있는 경우 -> 폰 열기
            if (!isPhoneOpen)
            {
                // 3. GameManager에서 폰을 열 수 있는 상태인지 확인
                if (CanOpenPhone())
                {
                    OpenPhone();
                }
            }
            // 4. 폰이 열려 있는 경우 -> 폰 닫기
            else
            {
                ClosePhone();
            }
        }
    }

    // 폰을 열 수 있는 '상태'인지 확인
    private bool CanOpenPhone()
    {
        GameState currentState = gameManager.currentState;

        // 자유시간, 방과후, 5일차 방과후일 때만 true
        return currentState == GameState.Lunch_FreeTime ||
               currentState == GameState.AfterSchool ||
               currentState == GameState.Day5_FreeTime;
    }

    private void OpenPhone()
    {
        isPhoneOpen = true;
        phoneUIPanel.SetActive(true);

        // 플레이어 정지
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        // (추가 예정) 메신저 탭 기본으로 열기
        // ShowMessengerTab();
    }

    private void ClosePhone()
    {
        isPhoneOpen = false;
        phoneUIPanel.SetActive(false);

        // 플레이어 다시 활성화
        if (playerController != null)
        {
            playerController.enabled = true;
        }
    }

    // 언어 전환
    private void ToggleLanguage()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.ToggleLanguage();
        }
    }

    // 언어 버튼 텍스트 업데이트
    private void UpdateLanguageButtonText(Language language)
    {
        if (languageButtonText != null)
        {
            languageButtonText.text = language == Language.Korean ? "English" : "한국어";
        }
    }

    // (추가 예정) UI 버튼을 눌렀을 때 호출할 함수들
    // public void ShowMessengerTab() { ... }
    // public void ShowEventsTab() { ... }
}
