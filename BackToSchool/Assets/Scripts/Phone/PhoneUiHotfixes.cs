using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phone UI hotfix:
/// 1) single-choice toggles for Question_*
/// 2) robust app button routing via PhoneAppManager (to keep splash flow)
/// </summary>
public class PhoneUiHotfixes : MonoBehaviour
{
    private readonly List<Button> appButtons = new List<Button>();

    private PhoneAppManager appManager;
    private readonly string[] questionNames = { "Question_1", "Question_2", "Question_3" };

    private void Start()
    {
        appManager = GetComponent<PhoneAppManager>();
        BindButtons();
        SetupExclusiveQuestionToggles();
    }

    private void BindButtons()
    {
        appButtons.Clear();
        var all = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string target = ResolveTargetPanelForButton(all[i]);
            if (!string.IsNullOrEmpty(target))
                appButtons.Add(all[i]);
        }

        for (int i = 0; i < appButtons.Count; i++)
        {
            var button = appButtons[i];
            string target = ResolveTargetPanelForButton(button);
            if (string.IsNullOrEmpty(target))
                continue;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OpenViaManager(target));
        }
    }

    private void OpenViaManager(string targetName)
    {
        if (targetName == "App_Health" && !IsHealthAllowedInCurrentFlow())
            return;

        if (appManager == null)
            appManager = GetComponent<PhoneAppManager>();

        if (appManager == null)
            return;

        switch (targetName)
        {
            case "App_Rules":
                appManager.OpenApp(PhoneAppId.Rules);
                break;
            case "App_Health":
                appManager.OpenApp(PhoneAppId.Health);
                break;
            case "App_Chat":
                appManager.OpenApp(PhoneAppId.Chat);
                break;
            case "App_Music":
                appManager.OpenApp(PhoneAppId.Music);
                break;
            case "App_Settings":
                appManager.OpenApp(PhoneAppId.Settings);
                break;
        }
    }

    private void SetupExclusiveQuestionToggles()
    {
        for (int i = 0; i < questionNames.Length; i++)
        {
            var question = FindByName(questionNames[i]);
            if (question == null)
                continue;

            var group = question.GetComponent<ToggleGroup>();
            if (group == null)
                group = question.AddComponent<ToggleGroup>();

            group.allowSwitchOff = true;

            var toggles = question.GetComponentsInChildren<Toggle>(true);
            for (int j = 0; j < toggles.Length; j++)
                toggles[j].group = group;

            SetAllOff(toggles);
        }
    }

    private static void SetAllOff(Toggle[] toggles)
    {
        for (int i = 0; i < toggles.Length; i++)
            toggles[i].SetIsOnWithoutNotify(false);
    }

    private static bool IsHealthAllowedInCurrentFlow()
    {
        string flowType = PlayerPrefs.GetString("FLOW_TYPE", "");
        string flowId = PlayerPrefs.GetString("FLOW_ID", "");

        if (flowType == "CHAT")
            return true;

        if (flowType == "FREEROAM")
            return string.IsNullOrEmpty(flowId) || flowId.Contains("BEFORE_ASSEMBLY");

        return false;
    }

    private string ResolveTargetPanelForButton(Button button)
    {
        if (button == null)
            return null;

        if (button.name.Contains("Rules")) return "App_Rules";
        if (button.name.Contains("Health")) return "App_Health";
        if (button.name.Contains("Chat")) return "App_Chat";
        if (button.name.Contains("Music")) return "App_Music";
        if (button.name.Contains("Settings")) return "App_Settings";

        var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            string t = text.text.Replace(" ", "").Trim().ToLowerInvariant();
            if (t.Contains("rule") || t.Contains("규칙")) return "App_Rules";
            if (t.Contains("health") || t.Contains("자가진단") || t.Contains("건강")) return "App_Health";
            if (t.Contains("chat") || t.Contains("채팅")) return "App_Chat";
            if (t.Contains("music") || t.Contains("음악")) return "App_Music";
            if (t.Contains("setting") || t.Contains("설정")) return "App_Settings";
        }

        return null;
    }

    private GameObject FindByName(string name)
    {
        var tr = transform.Find(name);
        if (tr != null)
            return tr.gameObject;

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.name == name)
                return child.gameObject;

            var nested = child.GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < nested.Length; j++)
            {
                if (nested[j].name == name)
                    return nested[j].gameObject;
            }
        }

        return null;
    }
}
