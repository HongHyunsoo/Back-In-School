using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhoneSettingsAppController : MonoBehaviour
{
    [Header("Optional References")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private ScrollRect settingsScrollRect;
    [SerializeField] private Button settingBackButton;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Button languageButton;

    [SerializeField] private Button bindLeftButton;
    [SerializeField] private Button bindRightButton;
    [SerializeField] private Button bindJumpButton;
    [SerializeField] private Button bindSprintButton;
    [SerializeField] private Button bindStairDownButton;
    [SerializeField] private Button bindStairUpButton;
    [SerializeField] private Button bindInteractButton;
    [SerializeField] private Button bindPhoneButton;

    [SerializeField] private TextMeshProUGUI infoLabel;
    [SerializeField] private TextMeshProUGUI languageButtonLabel;
    [SerializeField] private TextMeshProUGUI bindLeftLabel;
    [SerializeField] private TextMeshProUGUI bindRightLabel;
    [SerializeField] private TextMeshProUGUI bindJumpLabel;
    [SerializeField] private TextMeshProUGUI bindSprintLabel;
    [SerializeField] private TextMeshProUGUI bindStairDownLabel;
    [SerializeField] private TextMeshProUGUI bindStairUpLabel;
    [SerializeField] private TextMeshProUGUI bindInteractLabel;
    [SerializeField] private TextMeshProUGUI bindPhoneLabel;

    private string waitingBindKey;
    private int rebindStartFrame = -1;
    private bool isWired;
    private PhoneAppManager appManager;

    private void Start()
    {
        ResolveReferences();
        Wire();
        RefreshAll();

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnEnable()
    {
        RefreshBindingsNow();
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

    private void ResolveReferences()
    {
        if (settingsPanel == null)
            settingsPanel = FindObjectByNameOrToken("App_Setting", "App_Settings", "Settings");

        Transform root = settingsPanel != null ? settingsPanel.transform : transform;
        if (appManager == null)
            appManager = GetComponent<PhoneAppManager>();

        if (settingsScrollRect == null)
            settingsScrollRect = FindScrollRect(root, "Scroll View_Setting", "ScrollView_Setting", "Scroll View");
        if (settingsScrollRect == null)
            settingsScrollRect = FindScrollRect(transform, "Scroll View_Setting", "ScrollView_Setting", "Scroll View");
        if (settingBackButton == null)
            settingBackButton = FindButton(root, "Setting_Back");
        if (settingBackButton == null)
            settingBackButton = FindButton(transform, "Setting_Back");

        if (masterVolumeSlider == null)
            masterVolumeSlider = FindSlider(root, "Volume", "Sound");
        if (bgmVolumeSlider == null)
            bgmVolumeSlider = FindSlider(root, "BGMSlider");
        if (bgmVolumeSlider == null)
            bgmVolumeSlider = FindSlider(root, "BGM", "Music");
        if (sfxVolumeSlider == null)
            sfxVolumeSlider = FindSlider(root, "SFXSlider");
        if (sfxVolumeSlider == null)
            sfxVolumeSlider = FindSlider(root, "SFX", "SE", "Effect");
        if (bgmVolumeSlider == null)
            bgmVolumeSlider = FindSlider(transform, "BGMSlider", "BGM", "Music");
        if (sfxVolumeSlider == null)
            sfxVolumeSlider = FindSlider(transform, "SFXSlider", "SFX", "SE", "Effect");

        if (languageButton == null)
            languageButton = FindButton(root, "Language");

        if (bindLeftButton == null)
            bindLeftButton = FindButton(root, "Left");
        if (bindRightButton == null)
            bindRightButton = FindButton(root, "Right");
        if (bindJumpButton == null)
            bindJumpButton = FindButton(root, "Jump");
        if (bindSprintButton == null)
            bindSprintButton = FindButton(root, "Sprint", "달리기", "Run", "Shift");
        if (bindStairDownButton == null)
            bindStairDownButton = FindButton(root, "StairDown", "Stair Down", "Down");
        if (bindStairUpButton == null)
            bindStairUpButton = FindButton(root, "StairUp", "Stair Up", "Up");
        if (bindInteractButton == null)
            bindInteractButton = FindButton(root, "Interact");
        if (bindPhoneButton == null)
            bindPhoneButton = FindButton(root, "Phone", "TAB");

        if (languageButtonLabel == null)
            languageButtonLabel = FindLabelUnder(languageButton);
        if (bindLeftLabel == null)
            bindLeftLabel = FindLabelUnder(bindLeftButton) ?? FindLabelByName(root, "Text_Left");
        if (bindRightLabel == null)
            bindRightLabel = FindLabelUnder(bindRightButton) ?? FindLabelByName(root, "Text_Right");
        if (bindJumpLabel == null)
            bindJumpLabel = FindLabelUnder(bindJumpButton) ?? FindLabelByName(root, "Text_Jump");
        if (bindSprintLabel == null)
            bindSprintLabel = FindLabelUnder(bindSprintButton) ?? FindLabelByName(root, "Text_Sprint");
        if (bindStairDownLabel == null)
            bindStairDownLabel = FindLabelUnder(bindStairDownButton) ?? FindLabelByName(root, "Text_Down");
        if (bindStairUpLabel == null)
            bindStairUpLabel = FindLabelUnder(bindStairUpButton) ?? FindLabelByName(root, "Text_Up");
        if (bindInteractLabel == null)
            bindInteractLabel = FindLabelUnder(bindInteractButton) ?? FindLabelByName(root, "Text_Interact");
        if (bindPhoneLabel == null)
            bindPhoneLabel = FindLabelUnder(bindPhoneButton) ?? FindLabelByName(root, "Text_PhoneUI");
    }

    private void Wire()
    {
        if (isWired)
        {
            WireVolumeSliders(true);
            return;
        }

        if (languageButton != null)
            languageButton.onClick.AddListener(ToggleLanguage);
        if (settingBackButton != null)
        {
            settingBackButton.onClick.RemoveListener(BackToHomeFromSettings);
            settingBackButton.onClick.AddListener(BackToHomeFromSettings);
        }

        if (bindLeftButton != null)
            bindLeftButton.onClick.AddListener(() => StartRebind(KeyBindingConfig.LeftKey));
        if (bindRightButton != null)
            bindRightButton.onClick.AddListener(() => StartRebind(KeyBindingConfig.RightKey));
        if (bindJumpButton != null)
            bindJumpButton.onClick.AddListener(() => StartRebind(KeyBindingConfig.JumpKey));
        if (bindSprintButton != null)
            bindSprintButton.onClick.AddListener(() => StartRebind(KeyBindingConfig.SprintKey));
        if (bindStairDownButton != null)
            bindStairDownButton.onClick.AddListener(() => StartRebind(KeyBindingConfig.StairDownKey));
        if (bindStairUpButton != null)
            bindStairUpButton.onClick.AddListener(() => StartRebind(KeyBindingConfig.StairUpKey));
        if (bindInteractButton != null)
            bindInteractButton.onClick.AddListener(() => StartRebind(KeyBindingConfig.InteractKey));
        if (bindPhoneButton != null)
            bindPhoneButton.onClick.AddListener(() => StartRebind(KeyBindingConfig.PhoneKey));

        WireVolumeSliders(false);

        isWired = true;
    }

    private void WireVolumeSliders(bool forceRebind)
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.interactable = true;
            masterVolumeSlider.SetValueWithoutNotify(AudioSettingsService.MasterVolume);
            if (forceRebind)
                masterVolumeSlider.onValueChanged.RemoveListener(AudioSettingsService.SetMasterVolume);
            masterVolumeSlider.onValueChanged.AddListener(AudioSettingsService.SetMasterVolume);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.interactable = true;
            bgmVolumeSlider.SetValueWithoutNotify(AudioSettingsService.BgmVolume);
            if (forceRebind)
                bgmVolumeSlider.onValueChanged.RemoveListener(AudioSettingsService.SetBgmVolume);
            bgmVolumeSlider.onValueChanged.AddListener(AudioSettingsService.SetBgmVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.interactable = true;
            sfxVolumeSlider.SetValueWithoutNotify(AudioSettingsService.SfxVolume);
            if (forceRebind)
                sfxVolumeSlider.onValueChanged.RemoveListener(AudioSettingsService.SetSfxVolume);
            sfxVolumeSlider.onValueChanged.AddListener(AudioSettingsService.SetSfxVolume);
        }
    }

    public void RefreshBindingsNow()
    {
        ResolveReferences();
        Wire();
        BringVolumeSlidersToFront();
        ConfigureSettingsScrollRect();
        RefreshAll();
    }

    private void BringVolumeSlidersToFront()
    {
        if (bgmVolumeSlider != null)
            bgmVolumeSlider.transform.SetAsLastSibling();

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.transform.SetAsLastSibling();

        if (masterVolumeSlider != null)
            masterVolumeSlider.transform.SetAsLastSibling();
    }

    private void RefreshAll()
    {
        RefreshLanguageLabel();
        RefreshBindingLabels();
        ConfigureSettingsScrollRect();
        if (masterVolumeSlider != null)
            masterVolumeSlider.SetValueWithoutNotify(AudioSettingsService.MasterVolume);
        if (bgmVolumeSlider != null)
            bgmVolumeSlider.SetValueWithoutNotify(AudioSettingsService.BgmVolume);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.SetValueWithoutNotify(AudioSettingsService.SfxVolume);
        SetInfo("");
    }

    private void ToggleLanguage()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.ToggleLanguage();

        RefreshLanguageLabel();
        ConfigureSettingsScrollRect();
    }

    private void StartRebind(string keyId)
    {
        waitingBindKey = keyId;
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
        UpdateBindingVisual(bindLeftButton, bindLeftLabel, KeyBindingConfig.LeftKey, KeyCode.A);
        UpdateBindingVisual(bindRightButton, bindRightLabel, KeyBindingConfig.RightKey, KeyCode.D);
        UpdateBindingVisual(bindJumpButton, bindJumpLabel, KeyBindingConfig.JumpKey, KeyCode.Space);
        UpdateBindingVisual(bindSprintButton, bindSprintLabel, KeyBindingConfig.SprintKey, KeyCode.LeftShift);
        UpdateBindingVisual(bindStairDownButton, bindStairDownLabel, KeyBindingConfig.StairDownKey, KeyCode.S);
        UpdateBindingVisual(bindStairUpButton, bindStairUpLabel, KeyBindingConfig.StairUpKey, KeyCode.W);
        UpdateBindingVisual(bindInteractButton, bindInteractLabel, KeyBindingConfig.InteractKey, KeyCode.E);
        UpdateBindingVisual(bindPhoneButton, bindPhoneLabel, KeyBindingConfig.PhoneKey, KeyCode.Tab);
    }

    private void ConfigureSettingsScrollRect()
    {
        if (settingsScrollRect == null)
            return;

        settingsScrollRect.horizontal = false;
        settingsScrollRect.vertical = true;
        settingsScrollRect.movementType = ScrollRect.MovementType.Clamped;
        settingsScrollRect.inertia = true;
        settingsScrollRect.decelerationRate = 0.25f;
        settingsScrollRect.scrollSensitivity = 160f;

        RectTransform viewport = settingsScrollRect.viewport;
        RectTransform content = settingsScrollRect.content;
        if (viewport == null || content == null)
            return;

        NormalizeViewportRect(viewport);
        Canvas.ForceUpdateCanvases();

        float minY = 0f;
        float maxY = 0f;
        bool hasBounds = false;
        RectTransform[] descendants = content.GetComponentsInChildren<RectTransform>(true);
        Vector3[] corners = new Vector3[4];
        for (int i = 0; i < descendants.Length; i++)
        {
            RectTransform child = descendants[i];
            if (child == null || child == content || !child.gameObject.activeInHierarchy)
                continue;

            child.GetWorldCorners(corners);
            float top = float.MinValue;
            float bottom = float.MaxValue;
            for (int c = 0; c < 4; c++)
            {
                Vector3 local = content.InverseTransformPoint(corners[c]);
                if (local.y > top) top = local.y;
                if (local.y < bottom) bottom = local.y;
            }

            if (!hasBounds)
            {
                minY = bottom;
                maxY = top;
                hasBounds = true;
            }
            else
            {
                if (bottom < minY) minY = bottom;
                if (top > maxY) maxY = top;
            }
        }

        if (!hasBounds)
            return;

        float padding = 220f;
        float requiredHeight = (maxY - minY) + padding;
        float minHeight = viewport.rect.height + 220f;

        Vector2 contentSize = content.sizeDelta;
        contentSize.y = Mathf.Max(contentSize.y, minHeight, requiredHeight);
        content.sizeDelta = contentSize;

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

    private void BackToHomeFromSettings()
    {
        if (appManager == null)
            appManager = GetComponent<PhoneAppManager>();

        if (appManager != null)
            appManager.BackToHome();
    }

    private void UpdateBindingVisual(Button button, TextMeshProUGUI label, string keyId, KeyCode fallback)
    {
        KeyCode code = KeyBindingConfig.Get(keyId, fallback);
        string value = code.ToString();

        SetText(label, value);
        SetButtonText(button, value);
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
            label.text = value;
    }

    private static void SetButtonText(Button button, string value)
    {
        if (button == null)
            return;

        var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
            text.text = value;
    }

    private void SetInfo(string value)
    {
        if (infoLabel != null)
            infoLabel.text = value;
    }

    private void OnLanguageChanged(Language _)
    {
        RefreshLanguageLabel();
        RefreshBindingLabels();
    }

    private string L(string ko, string en)
    {
        if (LocalizationManager.Instance == null)
            return ko;

        return LocalizationManager.Instance.GetCurrentLanguage() == Language.Korean ? ko : en;
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

    private static Button FindButton(Transform root, params string[] tokens)
    {
        if (root == null)
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

    private static Slider FindSlider(Transform root, params string[] tokens)
    {
        if (root == null)
            return null;

        var sliders = root.GetComponentsInChildren<Slider>(true);
        for (int i = 0; i < sliders.Length; i++)
        {
            if (HasAnyToken(sliders[i].name, tokens))
                return sliders[i];
        }

        return sliders.Length > 0 ? sliders[0] : null;
    }

    private static ScrollRect FindScrollRect(Transform root, params string[] tokens)
    {
        if (root == null)
            return null;

        var scrollRects = root.GetComponentsInChildren<ScrollRect>(true);
        for (int i = 0; i < scrollRects.Length; i++)
        {
            if (HasAnyToken(scrollRects[i].name, tokens))
                return scrollRects[i];
        }

        return scrollRects.Length > 0 ? scrollRects[0] : null;
    }

    private static TextMeshProUGUI FindLabelUnder(Button button)
    {
        if (button == null)
            return null;

        return button.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private static TextMeshProUGUI FindLabelByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;

        var labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null && labels[i].name == name)
                return labels[i];
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
