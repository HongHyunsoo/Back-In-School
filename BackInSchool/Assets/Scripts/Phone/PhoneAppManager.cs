using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public enum PhoneAppId { Home, Rules, Health, Chat, Gallery, Settings }

[Serializable]
public class AppSplashEntry
{
    public PhoneAppId appId;
    public GameObject splashPanel;
    public float duration = 0.45f;
}

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
    [FormerlySerializedAs("musicPanel")]
    [SerializeField] private GameObject galleryPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("App Splash")]
    [SerializeField] private List<AppSplashEntry> appSplashEntries = new();
    [SerializeField] private GameObject appSplashPanel;
    [SerializeField] private float splashDuration = 0.45f;
    [SerializeField] private float splashFadeDuration = 0.18f;
    [SerializeField] private bool useSplash = true;

    [Header("Buttons (Optional wiring)")]
    [SerializeField] private Button btnRules;
    [SerializeField] private Button btnHealth;
    [SerializeField] private Button btnChat;
    [FormerlySerializedAs("btnMusic")]
    [SerializeField] private Button btnGallery;
    [SerializeField] private Button btnSettings;
    [SerializeField] private Button btnBack;       // app -> home
    [SerializeField] private Button btnClosePhone; // close phone (school)
    [SerializeField] private Button btnPower;      // power (context-specific)
    [SerializeField] private Button btnRuleTab;
    [SerializeField] private Button btnSchoolMealTab;
    [SerializeField] private Button btnPenaltyTab;
    [SerializeField] private GameObject rulesScreenRule;
    [SerializeField] private GameObject rulesScreenSchoolMeal;
    [SerializeField] private GameObject rulesScreenPenalty;

    private readonly Dictionary<PhoneAppId, GameObject> appPanels = new();
    private readonly Dictionary<PhoneAppId, AppSplashEntry> splashByApp = new();
    private Coroutine openRoutine;
    private bool ruleTabsWired;

    public PhoneAppId CurrentApp { get; private set; } = PhoneAppId.Home;
    public bool IsLocked { get; private set; }

    public event Action OnRequestClosePhone;
    public event Action OnRequestPower;

    private void Awake()
    {
        appPanels[PhoneAppId.Rules] = rulesPanel;
        appPanels[PhoneAppId.Health] = healthPanel;
        appPanels[PhoneAppId.Chat] = chatPanel;
        appPanels[PhoneAppId.Gallery] = galleryPanel;
        appPanels[PhoneAppId.Settings] = settingsPanel;
        BuildSplashLookup();
        ResolveRuleTabReferences();
        WireRuleTabs();

        if (btnRules) btnRules.onClick.AddListener(() => OpenApp(PhoneAppId.Rules));
        if (btnHealth) btnHealth.onClick.AddListener(() => OpenApp(PhoneAppId.Health));
        if (btnChat) btnChat.onClick.AddListener(() => OpenApp(PhoneAppId.Chat));
        if (btnGallery) btnGallery.onClick.AddListener(() => OpenApp(PhoneAppId.Gallery));
        if (btnSettings) btnSettings.onClick.AddListener(() => OpenApp(PhoneAppId.Settings));

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

        HideAllSplashPanels();

        ShowHome();
        SetLocked(false);
    }

    public void OpenApp(PhoneAppId appId)
    {
        if (IsLocked) return;

        if (!Day1TutorialController.IsPhoneAppAllowed(appId))
            return;

        if (appId == PhoneAppId.Health && !FlowContext.IsHealthCheckAllowed())
            return;

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

        if (TryHandleCurrentAppBack())
            return;

        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
            openRoutine = null;
        }

        HideAllSplashPanels();

        ShowHome();
    }

    private bool TryHandleCurrentAppBack()
    {
        switch (CurrentApp)
        {
            case PhoneAppId.Gallery:
            {
                var galleryController = GetComponent<PhoneGalleryAppController>();
                if (galleryController != null && galleryController.HandleBackRequest())
                    return true;

                var photoSlotController = GetComponent<PhonePhotoSlotUnlockController>();
                if (photoSlotController != null && photoSlotController.HandleBackRequest())
                    return true;
                break;
            }
        }

        return false;
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

        GetSplashConfig(appId, out var splashPanel, out var duration);
        bool showSplash = useSplash && splashPanel != null && duration > 0f;
        if (showSplash)
        {
            HideAllSplashPanels();
            yield return PlaySplashWithFade(splashPanel, duration);
        }

        if (appPanels.TryGetValue(appId, out var panel) && panel != null)
            panel.SetActive(true);

        if (appId == PhoneAppId.Rules)
            ShowRulePage();

        if (appId == PhoneAppId.Settings)
        {
            var settingsController = GetComponent<PhoneSettingsAppController>();
            if (settingsController != null)
                settingsController.RefreshBindingsNow();
        }

        CurrentApp = appId;
        openRoutine = null;
    }

    private void BuildSplashLookup()
    {
        splashByApp.Clear();
        for (int i = 0; i < appSplashEntries.Count; i++)
        {
            var entry = appSplashEntries[i];
            if (entry == null)
                continue;

            splashByApp[entry.appId] = entry;
        }
    }

    private void HideAllSplashPanels()
    {
        if (appSplashPanel != null)
            appSplashPanel.SetActive(false);

        for (int i = 0; i < appSplashEntries.Count; i++)
        {
            var entry = appSplashEntries[i];
            if (entry != null && entry.splashPanel != null)
                entry.splashPanel.SetActive(false);
        }
    }

    private void GetSplashConfig(PhoneAppId appId, out GameObject panel, out float duration)
    {
        if (splashByApp.TryGetValue(appId, out var entry) && entry != null && entry.splashPanel != null)
        {
            panel = entry.splashPanel;
            duration = Mathf.Max(0f, entry.duration);
            return;
        }

        panel = appSplashPanel;
        duration = Mathf.Max(0f, splashDuration);
    }

    private IEnumerator PlaySplashWithFade(GameObject splashPanel, float duration)
    {
        if (splashPanel == null)
            yield break;

        splashPanel.SetActive(true);

        var group = splashPanel.GetComponent<CanvasGroup>();
        if (group == null)
            group = splashPanel.AddComponent<CanvasGroup>();

        float fade = Mathf.Max(0f, splashFadeDuration);
        float hold = Mathf.Max(0f, duration - (fade * 2f));

        if (fade > 0f)
            yield return FadeCanvasGroup(group, 0f, 1f, fade);
        else
            group.alpha = 1f;

        if (hold > 0f)
            yield return new WaitForSecondsRealtime(hold);

        if (fade > 0f)
            yield return FadeCanvasGroup(group, 1f, 0f, fade);
        else
            group.alpha = 0f;

        splashPanel.SetActive(false);
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
            yield break;

        float t = 0f;
        float d = Mathf.Max(0.01f, duration);
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            group.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }

        group.alpha = to;
    }

    private void ResolveRuleTabReferences()
    {
        if (rulesPanel == null)
            return;

        if (btnRuleTab == null)
            btnRuleTab = FindButtonUnder(rulesPanel.transform, "Btn_Rule");

        if (btnSchoolMealTab == null)
            btnSchoolMealTab = FindButtonUnder(rulesPanel.transform, "Btn_SchoolMeal");

        if (btnPenaltyTab == null)
            btnPenaltyTab = FindButtonUnder(rulesPanel.transform, "Btn_Penalty");

        if (rulesScreenRule == null)
            rulesScreenRule = FindChildObject(rulesPanel.transform, "Screen_Rule");

        if (rulesScreenSchoolMeal == null)
            rulesScreenSchoolMeal = FindChildObject(rulesPanel.transform, "Screen_SchoolMeal");

        if (rulesScreenPenalty == null)
            rulesScreenPenalty = FindChildObject(rulesPanel.transform, "Screen_Penalty");
    }

    private void WireRuleTabs()
    {
        if (ruleTabsWired)
            return;

        if (btnRuleTab != null)
            btnRuleTab.onClick.AddListener(() => ShowRulePage());

        if (btnSchoolMealTab != null)
            btnSchoolMealTab.onClick.AddListener(() => ShowSchoolMealPage());

        if (btnPenaltyTab != null)
            btnPenaltyTab.onClick.AddListener(() => ShowPenaltyPage());

        ruleTabsWired = true;
    }

    private void ShowRulePage()
    {
        SetRuleScreens(rule: true, schoolMeal: false, penalty: false);
    }

    private void ShowSchoolMealPage()
    {
        SetRuleScreens(rule: false, schoolMeal: true, penalty: false);
    }

    private void ShowPenaltyPage()
    {
        SetRuleScreens(rule: false, schoolMeal: false, penalty: true);
    }

    private void SetRuleScreens(bool rule, bool schoolMeal, bool penalty)
    {
        if (rulesScreenRule != null)
            rulesScreenRule.SetActive(rule);

        if (rulesScreenSchoolMeal != null)
            rulesScreenSchoolMeal.SetActive(schoolMeal);

        if (rulesScreenPenalty != null)
            rulesScreenPenalty.SetActive(penalty);
    }

    private static Button FindButtonUnder(Transform root, string name)
    {
        GameObject go = FindChildObject(root, name);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private static GameObject FindChildObject(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == name)
                return child.gameObject;
        }

        return null;
    }
}
