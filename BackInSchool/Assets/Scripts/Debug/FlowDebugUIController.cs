using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FlowDebugUIController : MonoBehaviour
{
    [Serializable]
    public class JumpPreset
    {
        public string label;
        public int day;
        public int step;
        public int penalty;
    }

    [Header("Toggle")]
    public KeyCode toggleKey = KeyCode.F1;
    public GameObject panelRoot;

    [Header("Preset UI")]
    public Dropdown presetDropdown;
    public JumpPreset[] presets;

    [Header("Buttons")]
    public Button skipButton;
    public Button jumpButton;
    public Button lunchAdvance3MinButton;
    public int lunchAdvanceMinutes = 3;

    [Header("Manual Input (Optional)")]
    public InputField dayInput;
    public InputField stepInput;
    public InputField penaltyInput;

    private int selectedIndex;

    private void Awake()
    {
        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);

        if (panelRoot != null)
            panelRoot.SetActive(false);

        ConfigurePanelRootPassThrough();
        SetupDropdown();
        ApplySelectedPresetToInputs();
        BindButtons();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            TogglePanel();
    }

    private void SetupDropdown()
    {
        if (presetDropdown == null)
            return;

        presetDropdown.ClearOptions();

        var options = new List<Dropdown.OptionData>();
        if (presets == null || presets.Length == 0)
        {
            options.Add(new Dropdown.OptionData("No presets"));
            presetDropdown.AddOptions(options);
            presetDropdown.interactable = false;
            return;
        }

        for (int i = 0; i < presets.Length; i++)
            options.Add(new Dropdown.OptionData(presets[i].label));

        presetDropdown.AddOptions(options);
        presetDropdown.onValueChanged.RemoveAllListeners();
        presetDropdown.onValueChanged.AddListener(OnPresetChanged);
        presetDropdown.value = 0;
        selectedIndex = 0;
    }

    private void OnPresetChanged(int idx)
    {
        selectedIndex = Mathf.Clamp(idx, 0, presets.Length - 1);
        ApplySelectedPresetToInputs();
    }

    private void ApplySelectedPresetToInputs()
    {
        if (presets == null || presets.Length == 0)
            return;

        var preset = presets[selectedIndex];
        if (dayInput != null) dayInput.text = preset.day.ToString();
        if (stepInput != null) stepInput.text = preset.step.ToString();
        if (penaltyInput != null) penaltyInput.text = preset.penalty.ToString();
    }

    public void TogglePanel()
    {
        if (panelRoot == null)
            return;

        panelRoot.SetActive(!panelRoot.activeSelf);
        ConfigurePanelRootPassThrough();
        BindButtons();
    }

    private void ConfigurePanelRootPassThrough()
    {
        if (panelRoot == null)
            return;

        var graphic = panelRoot.GetComponent<Graphic>();
        if (graphic != null)
            graphic.raycastTarget = false;
    }

    private void BindButtons()
    {
        if (skipButton == null)
            skipButton = FindButtonByName("Skip");

        if (jumpButton == null)
            jumpButton = FindButtonByName("Jump");

        if (lunchAdvance3MinButton == null)
            lunchAdvance3MinButton = FindButtonByName("LunchAdvance3Min");

        if (lunchAdvance3MinButton == null)
            lunchAdvance3MinButton = FindButtonByName("Time Skip");

        if (lunchAdvance3MinButton == null)
            lunchAdvance3MinButton = FindButtonByText("Time Skip");

        if (lunchAdvance3MinButton == null)
            lunchAdvance3MinButton = FindButtonByText("점심 -3분");

        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(OnClickSkip);
            skipButton.onClick.AddListener(OnClickSkip);
        }

        if (jumpButton != null)
        {
            jumpButton.onClick.RemoveListener(OnClickJump);
            jumpButton.onClick.AddListener(OnClickJump);
        }

        if (lunchAdvance3MinButton != null)
        {
            lunchAdvance3MinButton.onClick.RemoveListener(OnClickLunchAdvance3Min);
            lunchAdvance3MinButton.onClick.AddListener(OnClickLunchAdvance3Min);
        }
    }

    private Button FindButtonByName(string targetName)
    {
        if (panelRoot == null || string.IsNullOrEmpty(targetName))
            return null;

        var buttons = panelRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && string.Equals(buttons[i].name, targetName, StringComparison.OrdinalIgnoreCase))
                return buttons[i];
        }

        return null;
    }

    private Button FindButtonByText(string targetText)
    {
        if (panelRoot == null || string.IsNullOrEmpty(targetText))
            return null;

        var buttons = panelRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
                continue;

            var legacyText = buttons[i].GetComponentInChildren<Text>(true);
            if (legacyText != null && string.Equals(legacyText.text, targetText, StringComparison.OrdinalIgnoreCase))
                return buttons[i];

            var tmpText = buttons[i].GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null && string.Equals(tmpText.text, targetText, StringComparison.OrdinalIgnoreCase))
                return buttons[i];
        }

        return null;
    }

    public void OnClickSkip()
    {
        var fm = FlowManager.Instance;
        if (fm == null)
            return;

        fm.CompleteCurrentEvent(0);
    }

    public void OnClickJump()
    {
        var fm = FlowManager.Instance;
        if (fm == null)
            return;

        int d = ParseOr(dayInput, fm.day);
        int s = ParseOr(stepInput, fm.stepIndex);
        int p = ParseOr(penaltyInput, fm.penaltyPoints);

        DialogueProgressState.ClearAllCompletedConversations();

        fm.day = d;
        fm.stepIndex = s;
        fm.penaltyPoints = p;
        fm.PlayCurrent();
    }

    public void OnClickLunchAdvance3Min()
    {
        var timer = FindAnyObjectByType<LunchFreeTimeTimerController>();
        if (timer == null)
            return;

        timer.DebugAdvanceMinutes(Mathf.Max(1, lunchAdvanceMinutes));
    }

    private int ParseOr(InputField field, int fallback)
    {
        if (field == null)
            return fallback;

        return int.TryParse(field.text, out int value) ? value : fallback;
    }
}
