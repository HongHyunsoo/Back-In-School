using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;

    [Header("Main Buttons")]
    public Button startButton;
    public Button settingsButton;
    public Button challengeButton;
    public Button creditsButton;
    public Button quitButton;

    [Header("Start Transition")]
    public Transform transitionZoomTarget;
    public Camera transitionCamera;
    public float transitionDuration = 0.9f;
    public float targetZoomScale = 1.2f;
    public float targetCameraOrthoSize = 4.2f;
    public bool useUnscaledTimeForTransition = true;
    public float fadeOutDuration = 0.28f;
    public float fadeInDuration = 0.35f;

    [Header("Settings Buttons")]
    public Button backButton;
    public Button languageButton;

    [Header("Credits Buttons")]
    public Button creditsBackButton;

    [Header("Settings Controls")]
    public ScrollRect settingsScrollRect;
    public Slider masterVolumeSlider;
    public Slider bgmVolumeSlider;
    public Slider sfxVolumeSlider;
    public Button bindLeftButton;
    public Button bindRightButton;
    public Button bindJumpButton;
    public Button bindSprintButton;
    public Button bindDownButton;
    public Button bindUpButton;
    public Button bindInteractButton;
    public Button bindPhoneButton;

    [Header("Optional Labels")]
    public TextMeshProUGUI languageButtonLabel;
    public TextMeshProUGUI infoLabel;
    public TextMeshProUGUI bindLeftLabel;
    public TextMeshProUGUI bindRightLabel;
    public TextMeshProUGUI bindJumpLabel;
    public TextMeshProUGUI bindSprintLabel;
    public TextMeshProUGUI bindDownLabel;
    public TextMeshProUGUI bindUpLabel;
    public TextMeshProUGUI bindInteractLabel;
    public TextMeshProUGUI bindPhoneLabel;

    private string waitingBindKey;
    private int rebindStartFrame = -1;
    private bool isStarting;
    private Button resolvedJumpBindButton;
    private Button resolvedPhoneBindButton;
    private Button resolvedSprintBindButton;
    private Button resolvedInteractBindButton;
    private Button resolvedStairUpBindButton;
    private Button resolvedStairDownBindButton;

    private void Start()
    {
        WireButtons();
        InitUI();

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
    }

    private void Update()
    {
        if (string.IsNullOrEmpty(waitingBindKey))
            return;

        if (Time.frameCount <= rebindStartFrame)
            return;

        foreach (KeyCode code in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (!Input.GetKeyDown(code))
                continue;

            if (!KeyBindingConfig.IsAllowedBindingKey(code))
                continue;

            KeyBindingConfig.Set(waitingBindKey, code);
            waitingBindKey = null;
            rebindStartFrame = -1;
            SetInfo(L("키가 변경되었습니다.", "Key binding updated."));
            RefreshBindingLabels();
            break;
        }
    }

    private void WireButtons()
    {
        ResolveCreditsReferences();
        ResolveBindingButtons();

        if (startButton != null) startButton.onClick.AddListener(OnStartGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (challengeButton != null) challengeButton.onClick.AddListener(OnChallenges);
        if (creditsButton != null) creditsButton.onClick.AddListener(OpenCredits);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuit);

        if (backButton != null) backButton.onClick.AddListener(CloseSettings);
        if (languageButton != null) languageButton.onClick.AddListener(ToggleLanguage);
        if (creditsBackButton != null) creditsBackButton.onClick.AddListener(CloseCredits);

        if (bindLeftButton != null) bindLeftButton.onClick.AddListener(() => StartRebind(KeyBindingConfig.LeftKey));
        if (bindRightButton != null) bindRightButton.onClick.AddListener(() => StartRebind(KeyBindingConfig.RightKey));
        if (resolvedJumpBindButton != null) resolvedJumpBindButton.onClick.AddListener(() => StartRebind(KeyBindingConfig.JumpKey));
        if (resolvedSprintBindButton != null) resolvedSprintBindButton.onClick.AddListener(() => StartRebind(KeyBindingConfig.SprintKey));
        if (resolvedStairDownBindButton != null) resolvedStairDownBindButton.onClick.AddListener(() => StartRebind(KeyBindingConfig.StairDownKey));
        if (resolvedStairUpBindButton != null) resolvedStairUpBindButton.onClick.AddListener(() => StartRebind(KeyBindingConfig.StairUpKey));
        if (resolvedInteractBindButton != null) resolvedInteractBindButton.onClick.AddListener(() => StartRebind(KeyBindingConfig.InteractKey));
        if (resolvedPhoneBindButton != null) resolvedPhoneBindButton.onClick.AddListener(() => StartRebind(KeyBindingConfig.PhoneKey));
    }

    private void InitUI()
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);

        ResolveVolumeSliders();

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = AudioSettingsService.MasterVolume;
            masterVolumeSlider.onValueChanged.AddListener(AudioSettingsService.SetMasterVolume);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.value = AudioSettingsService.BgmVolume;
            bgmVolumeSlider.onValueChanged.AddListener(AudioSettingsService.SetBgmVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = AudioSettingsService.SfxVolume;
            sfxVolumeSlider.onValueChanged.AddListener(AudioSettingsService.SetSfxVolume);
        }

        RefreshLocalizedStaticLabels();
        RefreshLanguageLabel();
        RefreshBindingLabels();
        ConfigureSettingsScrollRect();
        SetInfo("");
    }

    private void OnStartGame()
    {
        if (isStarting)
            return;

        StartCoroutine(CoStartGameWithTransition());
    }

    private IEnumerator CoStartGameWithTransition()
    {
        isStarting = true;
        SetMainButtonsInteractable(false);

        var fader = SceneTransitionFader.EnsureInstance();
        float appliedFadeOut = Mathf.Max(0.38f, fadeOutDuration);
        fader.PrepareFadeInOnNextScene(fadeInDuration);

        Coroutine zoomRoutine = StartCoroutine(CoPlaySubtleStartZoom(appliedFadeOut));
        yield return fader.FadeOut(appliedFadeOut);

        if (zoomRoutine != null)
            StopCoroutine(zoomRoutine);

        var fm = FlowManager.Instance;
        if (fm == null)
        {
            var go = new GameObject("FlowManager");
            fm = go.AddComponent<FlowManager>();
            fm.autoStartOnPlay = false;
        }

        fm.day = 1;
        fm.stepIndex = 0;
        fm.penaltyPoints = 0;
        fm.ResetSchoolRuleRuntimeState();
        Day1TutorialController.ResetProgress();
        DialogueProgressState.ClearAllCompletedConversations();
        PenaltyReasonLog.Clear();
        ChatService.ResetPersistedDataForNewGame();
        PhoneGalleryService.ResetPersistedDataForNewGame();
        fm.PlayCurrent(false);
    }

    private IEnumerator CoPlaySubtleStartZoom(float duration)
    {
        Transform zoomTarget = transitionZoomTarget != null ? transitionZoomTarget : transform;
        Camera cam = transitionCamera != null ? transitionCamera : Camera.main;

        Vector3 startScale = zoomTarget != null ? zoomTarget.localScale : Vector3.one;
        Vector3 endScale = startScale * Mathf.Clamp(targetZoomScale, 1f, 1.06f);

        bool canZoomCamera = cam != null && cam.orthographic;
        float startSize = canZoomCamera ? cam.orthographicSize : 0f;
        float desiredSize = startSize;
        if (canZoomCamera)
        {
            float candidate = targetCameraOrthoSize > 0f ? targetCameraOrthoSize : startSize * 0.96f;
            desiredSize = Mathf.Lerp(startSize, candidate, 0.2f);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTimeForTransition ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = duration <= 0.0001f ? 1f : Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            if (zoomTarget != null)
                zoomTarget.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);

            if (canZoomCamera)
                cam.orthographicSize = Mathf.LerpUnclamped(startSize, desiredSize, eased);

            yield return null;
        }
    }

    private void SetMainButtonsInteractable(bool interactable)
    {
        if (startButton != null) startButton.interactable = interactable;
        if (settingsButton != null) settingsButton.interactable = interactable;
        if (challengeButton != null) challengeButton.interactable = interactable;
        if (creditsButton != null) creditsButton.interactable = interactable;
        if (quitButton != null) quitButton.interactable = interactable;
    }

    private void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (ShouldToggleMainPanelForSettings() && mainPanel != null)
            mainPanel.SetActive(false);
        ConfigureSettingsScrollRect();
        SetInfo("");
    }

    private void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
        SetInfo("");
    }

    private void OpenCredits()
    {
        if (creditsPanel != null) creditsPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (ShouldToggleMainPanelForCredits() && mainPanel != null)
            mainPanel.SetActive(false);
        SetInfo("");
    }

    private void CloseCredits()
    {
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
        SetInfo("");
    }

    private void OnChallenges()
    {
        SetInfo(L("도전과제는 아직 준비 중입니다.", "Challenges are not implemented yet."));
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ToggleLanguage()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.ToggleLanguage();
        RefreshLanguageLabel();
        RefreshBindingLabels();
    }

    private void StartRebind(string key)
    {
        waitingBindKey = key;
        rebindStartFrame = Time.frameCount;
        SetInfo(L("변경할 키를 눌러주세요...", "Press any key..."));
    }

    private void RefreshLanguageLabel()
    {
        bool isKorean = LocalizationManager.Instance == null ||
                        LocalizationManager.Instance.GetCurrentLanguage() == Language.Korean;

        string label = isKorean ? "한국어" : "English";

        SetText(languageButtonLabel, label);
        SetButtonText(languageButton, label);
    }

    private void RefreshBindingLabels()
    {
        ResolveBindingButtons();

        UpdateBindingVisual(bindLeftButton, bindLeftLabel, L("왼쪽", "Left"), KeyBindingConfig.LeftKey, KeyCode.A);
        UpdateBindingVisual(bindRightButton, bindRightLabel, L("오른쪽", "Right"), KeyBindingConfig.RightKey, KeyCode.D);
        UpdateBindingVisual(resolvedJumpBindButton, bindJumpLabel, L("점프", "Jump"), KeyBindingConfig.JumpKey, KeyCode.Space);
        UpdateBindingVisual(resolvedSprintBindButton, bindSprintLabel, L("달리기", "Sprint"), KeyBindingConfig.SprintKey, KeyCode.LeftShift);
        UpdateBindingVisual(resolvedStairDownBindButton, bindDownLabel, L("계단 아래", "Stair Down"), KeyBindingConfig.StairDownKey, KeyCode.S);
        UpdateBindingVisual(resolvedStairUpBindButton, bindUpLabel, L("계단 위", "Stair Up"), KeyBindingConfig.StairUpKey, KeyCode.W);
        UpdateBindingVisual(resolvedInteractBindButton, bindInteractLabel, L("상호작용", "Interact"), KeyBindingConfig.InteractKey, KeyCode.E);
        UpdateBindingVisual(resolvedPhoneBindButton, bindPhoneLabel, L("휴대폰", "Phone"), KeyBindingConfig.PhoneKey, KeyCode.Tab);
    }

    private void UpdateBindingVisual(Button button, TextMeshProUGUI label, string actionName, string keyId, KeyCode fallback)
    {
        KeyCode code = KeyBindingConfig.Get(keyId, fallback);
        string value = code.ToString();

        SetText(label, value);
        SetButtonText(button, value);
    }

    private void SetButtonText(Button button, string text)
    {
        if (button == null)
            return;

        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (buttonText != null)
            buttonText.text = text;
    }

    private void SetText(TextMeshProUGUI label, string text)
    {
        if (label != null)
            label.text = text;
    }

    private void SetInfo(string text)
    {
        if (infoLabel != null)
            infoLabel.text = text;
    }

    private void OnLanguageChanged(Language _)
    {
        RefreshLocalizedStaticLabels();
        RefreshLanguageLabel();
        RefreshBindingLabels();
    }

    private void RefreshLocalizedStaticLabels()
    {
        SetButtonText(startButton, LK("UI_START", "시작하기", "Start"));
        SetButtonText(settingsButton, LK("UI_SETTING", "설정", "Settings"));
        SetButtonText(challengeButton, LK("UI_ACHIEVE", "도전과제", "Achievements"));
        SetButtonText(creditsButton, LK("UI_CREDITS", "크레딧", "Credits"));
        SetButtonText(quitButton, LK("UI_QUIT", "나가기", "Quit"));
        SetButtonText(backButton, LK("UI_BACK", "뒤로가기", "Back"));
        SetButtonText(creditsBackButton, LK("UI_BACK", "뒤로가기", "Back"));
    }

    private string L(string ko, string en)
    {
        if (LocalizationManager.Instance == null)
            return ko;

        return LocalizationManager.Instance.GetCurrentLanguage() == Language.Korean ? ko : en;
    }

    private string LK(string key, string fallbackKo, string fallbackEn)
    {
        if (LocalizationManager.Instance == null)
            return L(fallbackKo, fallbackEn);

        string value = LocalizationManager.Instance.GetLine(key);
        if (string.IsNullOrEmpty(value) || value == key)
            return L(fallbackKo, fallbackEn);

        return value;
    }

    private bool ShouldToggleMainPanelForSettings()
    {
        if (mainPanel == null || settingsPanel == null)
            return false;

        if (mainPanel == settingsPanel)
            return false;

        if (settingsPanel.transform.IsChildOf(mainPanel.transform))
            return false;

        return true;
    }

    private bool ShouldToggleMainPanelForCredits()
    {
        if (mainPanel == null || creditsPanel == null)
            return false;

        if (mainPanel == creditsPanel)
            return false;

        if (creditsPanel.transform.IsChildOf(mainPanel.transform))
            return false;

        return true;
    }

    private void ResolveCreditsReferences()
    {
        if (creditsPanel == null)
            creditsPanel = FindObjectByExactName("CreditsPanel", "CreditPanel");

        if (creditsButton == null)
            creditsButton = FindButtonByNameOrText(transform, "Credits", "Credit", "크레딧");

        if (creditsBackButton == null && creditsPanel != null)
            creditsBackButton = FindButtonByNameOrText(creditsPanel.transform, "Back", "뒤로가기");
    }

    private void ResolveBindingButtons()
    {
        if (settingsPanel == null)
            settingsPanel = FindObjectByNameOrToken("Settings", "App_Settings", "Option");

        resolvedJumpBindButton = bindJumpButton;
        resolvedSprintBindButton = bindSprintButton;
        resolvedStairDownBindButton = bindDownButton;
        resolvedStairUpBindButton = bindUpButton;
        resolvedInteractBindButton = bindInteractButton;
        resolvedPhoneBindButton = bindPhoneButton;

        if (resolvedJumpBindButton == null)
            resolvedJumpBindButton = FindSettingsButtonByNameOrText("Jump", "점프");

        if (resolvedSprintBindButton == null)
            resolvedSprintBindButton = FindSettingsButtonByNameOrText("Sprint", "달리기", "Run", "Shift");

        if (bindSprintLabel == null)
            bindSprintLabel = resolvedSprintBindButton != null ? resolvedSprintBindButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;

        if (resolvedInteractBindButton == null)
            resolvedInteractBindButton = FindSettingsButtonByNameOrText("Interact", "상호작용");

        if (resolvedPhoneBindButton == null)
            resolvedPhoneBindButton = FindSettingsButtonByNameOrText("Phone", "휴대폰", "PhoneUI");

        if (resolvedStairUpBindButton == null)
            resolvedStairUpBindButton = FindSettingsButtonByNameOrText("StairUp", "Stair Up", "Up", "계단 위", "위층");

        if (resolvedStairDownBindButton == null)
            resolvedStairDownBindButton = FindSettingsButtonByNameOrText("StairDown", "Stair Down", "Down", "계단 아래", "아래층");
    }

    private void ResolveVolumeSliders()
    {
        if (settingsPanel == null)
            settingsPanel = FindObjectByNameOrToken("Settings", "App_Settings", "Option");

        if (settingsPanel == null)
            return;

        if (settingsScrollRect == null)
            settingsScrollRect = FindSettingsScrollRectByName("Scroll View_Setting", "ScrollView_Setting", "Scroll View");

        if (masterVolumeSlider == null)
            masterVolumeSlider = FindSettingsSliderByName("Volume", "MasterVolumeSlider", "Sound");

        if (bgmVolumeSlider == null)
            bgmVolumeSlider = FindSettingsSliderByName("BGMSlider", "BGM", "Music");

        if (sfxVolumeSlider == null)
            sfxVolumeSlider = FindSettingsSliderByName("SFXSlider", "SFX", "SE", "Effect");
    }

    private void ConfigureSettingsScrollRect()
    {
        ResolveVolumeSliders();

        if (settingsScrollRect == null)
            return;

        settingsScrollRect.horizontal = false;
        settingsScrollRect.vertical = true;
        settingsScrollRect.movementType = ScrollRect.MovementType.Clamped;
        settingsScrollRect.inertia = true;
        settingsScrollRect.decelerationRate = 0.2f;
        settingsScrollRect.scrollSensitivity = 120f;

        RectTransform viewport = settingsScrollRect.viewport;
        RectTransform content = settingsScrollRect.content;
        if (viewport == null || content == null)
            return;

        NormalizeViewportRect(viewport);
        Canvas.ForceUpdateCanvases();

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        settingsScrollRect.StopMovement();
        Canvas.ForceUpdateCanvases();
        settingsScrollRect.verticalNormalizedPosition = 1f;
    }

    private static void NormalizeViewportRect(RectTransform viewport)
    {
        if (viewport == null)
            return;

        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.anchoredPosition = Vector2.zero;
        viewport.sizeDelta = Vector2.zero;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewport.pivot = new Vector2(0.5f, 0.5f);
    }

    private Button FindSettingsButtonByNameOrText(params string[] tokens)
    {
        if (settingsPanel == null || tokens == null || tokens.Length == 0)
            return null;

        return FindButtonByNameOrText(settingsPanel.transform, tokens);
    }

    private Button FindButtonByNameOrText(Transform root, params string[] tokens)
    {
        if (root == null || tokens == null || tokens.Length == 0)
            return null;

        var buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (HasAnyToken(buttons[i].name, tokens))
                return buttons[i];

            var text = buttons[i].GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null && HasAnyToken(text.text, tokens))
                return buttons[i];
        }

        return null;
    }

    private Slider FindSettingsSliderByName(params string[] tokens)
    {
        if (settingsPanel == null || tokens == null || tokens.Length == 0)
            return null;

        var sliders = settingsPanel.GetComponentsInChildren<Slider>(true);
        for (int i = 0; i < sliders.Length; i++)
        {
            if (HasAnyToken(sliders[i].name, tokens))
                return sliders[i];
        }

        return null;
    }

    private ScrollRect FindSettingsScrollRectByName(params string[] tokens)
    {
        if (settingsPanel == null || tokens == null || tokens.Length == 0)
            return null;

        var scrollRects = settingsPanel.GetComponentsInChildren<ScrollRect>(true);
        for (int i = 0; i < scrollRects.Length; i++)
        {
            if (HasAnyToken(scrollRects[i].name, tokens))
                return scrollRects[i];
        }

        return scrollRects.Length > 0 ? scrollRects[0] : null;
    }

    private GameObject FindObjectByNameOrToken(params string[] tokens)
    {
        if (tokens == null || tokens.Length == 0)
            return null;

        var all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string n = all[i].name;
            for (int j = 0; j < tokens.Length; j++)
            {
                if (string.IsNullOrEmpty(tokens[j]))
                    continue;

                if (n.IndexOf(tokens[j], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return all[i].gameObject;
            }
        }

        return null;
    }

    private GameObject FindObjectByExactName(params string[] names)
    {
        if (names == null || names.Length == 0)
            return null;

        var all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string n = all[i].name;
            for (int j = 0; j < names.Length; j++)
            {
                if (string.IsNullOrEmpty(names[j]))
                    continue;

                if (string.Equals(n, names[j], System.StringComparison.OrdinalIgnoreCase))
                    return all[i].gameObject;
            }
        }

        return null;
    }

    private static bool HasAnyToken(string source, params string[] tokens)
    {
        if (string.IsNullOrEmpty(source) || tokens == null)
            return false;

        for (int i = 0; i < tokens.Length; i++)
        {
            if (string.IsNullOrEmpty(tokens[i]))
                continue;

            if (source.IndexOf(tokens[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }
}
