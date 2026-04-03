using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject settingsPanel;

    [Header("Main Buttons")]
    public Button startButton;
    public Button settingsButton;
    public Button challengeButton;
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

    [Header("Settings Controls")]
    public Slider masterVolumeSlider;
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

        foreach (KeyCode code in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (!Input.GetKeyDown(code))
                continue;

            KeyBindingConfig.Set(waitingBindKey, code);
            waitingBindKey = null;
            SetInfo(L("키가 변경되었습니다.", "Key binding updated."));
            RefreshBindingLabels();
            break;
        }
    }

    private void WireButtons()
    {
        ResolveBindingButtons();

        if (startButton != null) startButton.onClick.AddListener(OnStartGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (challengeButton != null) challengeButton.onClick.AddListener(OnChallenges);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuit);

        if (backButton != null) backButton.onClick.AddListener(CloseSettings);
        if (languageButton != null) languageButton.onClick.AddListener(ToggleLanguage);

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

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = AudioListener.volume;
            masterVolumeSlider.onValueChanged.AddListener(v => AudioListener.volume = v);
        }

        RefreshLocalizedStaticLabels();
        RefreshLanguageLabel();
        RefreshBindingLabels();
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

        Transform zoomTarget = transitionZoomTarget != null ? transitionZoomTarget : transform;
        Vector3 startScale = zoomTarget.localScale;
        Vector3 endScale = startScale * Mathf.Max(1f, targetZoomScale);

        Camera cam = transitionCamera != null ? transitionCamera : Camera.main;
        bool canZoomCamera = cam != null && cam.orthographic;
        float startOrtho = canZoomCamera ? cam.orthographicSize : 0f;
        float endOrtho = canZoomCamera ? Mathf.Max(0.1f, targetCameraOrthoSize) : 0f;

        float duration = Mathf.Max(0.01f, transitionDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float dt = useUnscaledTimeForTransition ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            if (zoomTarget != null)
                zoomTarget.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);

            if (canZoomCamera)
                cam.orthographicSize = Mathf.LerpUnclamped(startOrtho, endOrtho, eased);

            yield return null;
        }

        var fader = SceneTransitionFader.EnsureInstance();
        fader.PrepareFadeInOnNextScene(fadeInDuration);
        yield return fader.FadeOut(fadeOutDuration);

        // Safety reset before scene load in case camera/root survives scene changes.
        if (zoomTarget != null)
            zoomTarget.localScale = startScale;
        if (canZoomCamera)
            cam.orthographicSize = startOrtho;

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
        PenaltyReasonLog.Clear();
        ChatService.ResetPersistedDataForNewGame();
        PhoneGalleryService.ResetPersistedDataForNewGame();
        fm.PlayCurrent();
    }

    private void SetMainButtonsInteractable(bool interactable)
    {
        if (startButton != null) startButton.interactable = interactable;
        if (settingsButton != null) settingsButton.interactable = interactable;
        if (challengeButton != null) challengeButton.interactable = interactable;
        if (quitButton != null) quitButton.interactable = interactable;
    }

    private void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (ShouldToggleMainPanelForSettings() && mainPanel != null)
            mainPanel.SetActive(false);
        SetInfo("");
    }

    private void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
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
        SetButtonText(quitButton, LK("UI_QUIT", "나가기", "Quit"));
        SetButtonText(backButton, LK("UI_BACK", "뒤로가기", "Back"));
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

    private Button FindSettingsButtonByNameOrText(params string[] tokens)
    {
        if (settingsPanel == null || tokens == null || tokens.Length == 0)
            return null;

        var buttons = settingsPanel.GetComponentsInChildren<Button>(true);
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
