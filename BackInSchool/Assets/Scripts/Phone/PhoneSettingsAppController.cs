using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhoneSettingsAppController : MonoBehaviour
{
    [Header("Optional References")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider masterVolumeSlider;
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
    private bool isWired;

    private void Start()
    {
        ResolveReferences();
        Wire();
        RefreshAll();

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

    private void ResolveReferences()
    {
        if (settingsPanel == null)
            settingsPanel = FindObjectByNameOrToken("App_Settings", "Settings");

        Transform root = settingsPanel != null ? settingsPanel.transform : transform;

        if (masterVolumeSlider == null)
            masterVolumeSlider = FindSlider(root, "Volume", "Sound");

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
            bindLeftLabel = FindLabelUnder(bindLeftButton);
        if (bindRightLabel == null)
            bindRightLabel = FindLabelUnder(bindRightButton);
        if (bindJumpLabel == null)
            bindJumpLabel = FindLabelUnder(bindJumpButton);
        if (bindSprintLabel == null)
            bindSprintLabel = FindLabelUnder(bindSprintButton);
        if (bindStairDownLabel == null)
            bindStairDownLabel = FindLabelUnder(bindStairDownButton);
        if (bindStairUpLabel == null)
            bindStairUpLabel = FindLabelUnder(bindStairUpButton);
        if (bindInteractLabel == null)
            bindInteractLabel = FindLabelUnder(bindInteractButton);
        if (bindPhoneLabel == null)
            bindPhoneLabel = FindLabelUnder(bindPhoneButton);
    }

    private void Wire()
    {
        if (isWired)
            return;

        if (languageButton != null)
            languageButton.onClick.AddListener(ToggleLanguage);

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

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(AudioListener.volume);
            masterVolumeSlider.onValueChanged.AddListener(v => AudioListener.volume = v);
        }

        isWired = true;
    }

    private void RefreshAll()
    {
        RefreshLanguageLabel();
        RefreshBindingLabels();
        SetInfo("");
    }

    private void ToggleLanguage()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.ToggleLanguage();

        RefreshLanguageLabel();
    }

    private void StartRebind(string keyId)
    {
        waitingBindKey = keyId;
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

    private static TextMeshProUGUI FindLabelUnder(Button button)
    {
        if (button == null)
            return null;

        return button.GetComponentInChildren<TextMeshProUGUI>(true);
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
