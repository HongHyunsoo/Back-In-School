using System;
using System.Collections;
using System.Collections.Generic;
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

    [Header("App Splash")]
    [SerializeField] private GameObject appSplashPanel;
    [SerializeField] private float splashDuration = 0.45f;
    [SerializeField] private bool useSplash = true;

    [Header("Buttons (Optional wiring)")]
    [SerializeField] private Button btnRules;
    [SerializeField] private Button btnHealth;
    [SerializeField] private Button btnChat;
    [SerializeField] private Button btnMusic;
    [SerializeField] private Button btnBack;       // app -> home
    [SerializeField] private Button btnClosePhone; // close phone (school)
    [SerializeField] private Button btnPower;      // power (context-specific)

    private readonly Dictionary<PhoneAppId, GameObject> appPanels = new();
    private Coroutine openRoutine;

    public PhoneAppId CurrentApp { get; private set; } = PhoneAppId.Home;
    public bool IsLocked { get; private set; }

    public event Action OnRequestClosePhone;
    public event Action OnRequestPower;

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

        if (appSplashPanel != null)
            appSplashPanel.SetActive(false);

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

        if (openRoutine != null)
            StopCoroutine(openRoutine);

        openRoutine = StartCoroutine(CoOpenAppWithSplash(appId));
    }

    public void BackToHome()
    {
        if (IsLocked) return;

        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
            openRoutine = null;
        }

        if (appSplashPanel != null)
            appSplashPanel.SetActive(false);

        ShowHome();
    }

    private void ShowHome()
    {
        if (homePanel != null) homePanel.SetActive(true);
        if (appContainer != null) appContainer.SetActive(true);

        foreach (var kv in appPanels)
        {
            if (kv.Value != null)
                kv.Value.SetActive(false);
        }

        CurrentApp = PhoneAppId.Home;
    }

    public void SetLocked(bool locked)
    {
        IsLocked = locked;
        if (overlayLock) overlayLock.SetActive(locked);
    }

    private IEnumerator CoOpenAppWithSplash(PhoneAppId appId)
    {
        if (homePanel != null) homePanel.SetActive(false);
        if (appContainer != null) appContainer.SetActive(true);

        foreach (var kv in appPanels)
        {
            if (kv.Value != null)
                kv.Value.SetActive(false);
        }

        bool showSplash = useSplash && appSplashPanel != null && splashDuration > 0f;
        if (showSplash)
        {
            appSplashPanel.SetActive(true);
            yield return new WaitForSecondsRealtime(splashDuration);
            appSplashPanel.SetActive(false);
        }

        if (appPanels.TryGetValue(appId, out var panel) && panel != null)
            panel.SetActive(true);

        CurrentApp = appId;
        openRoutine = null;
    }
}
