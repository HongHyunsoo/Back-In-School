#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
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
    public Dropdown presetDropdown; // UnityEngine.UI.Dropdown
    public JumpPreset[] presets;

    [Header("Buttons")]
    public Button skipButton;
    public Button jumpButton;

    [Header("Manual Input (Optional)")]
    public InputField dayInput;
    public InputField stepInput;
    public InputField penaltyInput;

    int selectedIndex = 0;

    void Awake()
    {
        // ✅ 루트로 올린 뒤 DontDestroy
        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);

        if (panelRoot != null)
            panelRoot.SetActive(false);

        SetupDropdown();
        ApplySelectedPresetToInputs();
        BindButtons();
    }


    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            TogglePanel();
    }

    void SetupDropdown()
    {
        if (presetDropdown == null) return;

        presetDropdown.ClearOptions();

        var options = new System.Collections.Generic.List<Dropdown.OptionData>();

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

    void OnPresetChanged(int idx)
    {
        selectedIndex = Mathf.Clamp(idx, 0, presets.Length - 1);
        ApplySelectedPresetToInputs();
    }

    void ApplySelectedPresetToInputs()
    {
        if (presets == null || presets.Length == 0) return;

        var p = presets[selectedIndex];

        if (dayInput != null) dayInput.text = p.day.ToString();
        if (stepInput != null) stepInput.text = p.step.ToString();
        if (penaltyInput != null) penaltyInput.text = p.penalty.ToString();
    }

    public void TogglePanel()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(!panelRoot.activeSelf);
    }

    void BindButtons()
    {
        if (skipButton == null)
            skipButton = FindButtonByName("Skip");

        if (jumpButton == null)
            jumpButton = FindButtonByName("Jump");

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
    }

    Button FindButtonByName(string targetName)
    {
        if (panelRoot == null) return null;

        var buttons = panelRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == targetName)
                return buttons[i];
        }

        return null;
    }

    // --- Buttons ---

    public void OnClickSkip()
    {
        Debug.Log("[FlowDebugUI] Skip button clicked");

        var fm = FlowManager.Instance;
        Debug.Log($"[FlowDebugUI] FlowManager.Instance = {(fm == null ? "NULL" : fm.name)}");

        if (fm == null) return;

        // DebugSkip이 #if로 날아갔을 가능성 대비: CompleteCurrentEvent로 직접 호출
        fm.CompleteCurrentEvent(0);
    }

    public void OnClickJump()
    {
        Debug.Log("[FlowDebugUI] Jump button clicked");

        var fm = FlowManager.Instance;
        Debug.Log($"[FlowDebugUI] FlowManager.Instance = {(fm == null ? "NULL" : fm.name)}");

        if (fm == null) return;

        int d = ParseOr(dayInput, fm.day);
        int s = ParseOr(stepInput, fm.stepIndex);
        int p = ParseOr(penaltyInput, fm.penaltyPoints);

        Debug.Log($"[FlowDebugUI] Jump to day={d}, step={s}, penalty={p}");

        // DebugJump이 #if로 날아갔을 가능성 대비: 내부 로직 직접 수행
        fm.day = d;
        fm.stepIndex = s;
        fm.penaltyPoints = p;
        fm.PlayCurrent();
    }


    int ParseOr(InputField field, int fallback)
    {
        if (field == null) return fallback;
        if (int.TryParse(field.text, out int v)) return v;
        return fallback;
    }
}
#endif
