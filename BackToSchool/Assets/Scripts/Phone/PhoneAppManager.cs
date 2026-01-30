using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

public enum PhoneAppId { Home, Rules, Health, Chat, Music }

public class PhoneAppManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject homePanel;
    [SerializeField] private GameObject appContainer;
    [SerializeField] private GameObject overlayLock;

    [Header("App Panels")]
    [SerializeField] private GameObject rulesPanel;
    [SerializeField] private GameObject healthPanel;
    [SerializeField] private GameObject chatPanel;
    [SerializeField] private GameObject musicPanel;

    [Header("Buttons (Optional wiring)")]
    [SerializeField] private Button btnRules;
    [SerializeField] private Button btnHealth;
    [SerializeField] private Button btnChat;
    [SerializeField] private Button btnMusic;
    [SerializeField] private Button btnBack;       // 앱에서 홈으로
    [SerializeField] private Button btnClosePhone; // 폰 닫기(학교)
    [SerializeField] private Button btnPower;      // 전원(컨텍스트별 처리)

    private readonly Dictionary<PhoneAppId, GameObject> appPanels = new();

    public PhoneAppId CurrentApp { get; private set; } = PhoneAppId.Home;

    /// 채팅 세션 중일 때 뒤로/닫기 막는 용도
    public bool IsLocked { get; private set; }

    public event Action OnRequestClosePhone; // (학교) 폰 닫기 요청
    public event Action OnRequestPower;      // (지하철) 내리기 같은 특수 처리

    private void Awake()
    {
        appPanels[PhoneAppId.Rules] = rulesPanel;
        appPanels[PhoneAppId.Health] = healthPanel;
        appPanels[PhoneAppId.Chat] = chatPanel;
        appPanels[PhoneAppId.Music] = musicPanel;

        if (btnRules) btnRules.onClick.AddListener(() => OpenApp(PhoneAppId.Rules));
        if (btnHealth) btnHealth.onClick.AddListener(() => OpenApp(PhoneAppId.Health));
        if (btnChat) btnChat.onClick.AddListener(() => OpenApp(PhoneAppId.Chat));
        if (btnMusic) btnMusic.onClick.AddListener(() => OpenApp(PhoneAppId.Music));

        if (btnBack) btnBack.onClick.AddListener(BackToHome);

        if (btnClosePhone) btnClosePhone.onClick.AddListener(() =>
        {
            if (IsLocked) return;
            OnRequestClosePhone?.Invoke();
        });

        if (btnPower) btnPower.onClick.AddListener(() =>
        {
            if (IsLocked) return;
            OnRequestPower?.Invoke();
        });

        ShowHome();
        SetLocked(false);
    }

    public void OpenApp(PhoneAppId appId)
    {
        if (IsLocked) return;

        if (appId == PhoneAppId.Home)
        {
            ShowHome();
            return;
        }

        homePanel.SetActive(false);
        appContainer.SetActive(true);

        foreach (var kv in appPanels)
            kv.Value.SetActive(false);

        if (appPanels.TryGetValue(appId, out var panel))
            panel.SetActive(true);

        CurrentApp = appId;
    }

    public void BackToHome()
    {
        if (IsLocked) return;
        ShowHome();
    }

    private void ShowHome()
    {
        homePanel.SetActive(true);
        appContainer.SetActive(true);

        foreach (var kv in appPanels)
            kv.Value.SetActive(false);

        CurrentApp = PhoneAppId.Home;
    }

    public void SetLocked(bool locked)
    {
        IsLocked = locked;
        if (overlayLock) overlayLock.SetActive(locked);
    }
}
