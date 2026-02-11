using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 폰 UI 핫픽스:
/// 1) Question_* 내부 체크박스 단일선택(기본 미선택 허용)
/// 2) Rules/Health 버튼이 뒤바뀐 경우를 런타임에서 강제로 보정
/// </summary>
public class PhoneUiHotfixes : MonoBehaviour
{
    private readonly List<Button> appButtons = new List<Button>();

    private GameObject appRules;
    private GameObject appHealth;
    private GameObject appChat;
    private GameObject appMusic;
    private GameObject homePanel;

    private readonly string[] questionNames = { "Question_1", "Question_2", "Question_3" };

    private void Start()
    {
        BindPanelRefs();
        BindButtons();
        SetupExclusiveQuestionToggles();
    }

    private void Update()
    {
    }

    private void BindPanelRefs()
    {
        appRules = FindByName("App_Rules");
        appHealth = FindByName("App_Health");
        appChat = FindByName("App_Chat");
        appMusic = FindByName("App_Music");
        homePanel = FindByName("Home");
    }

    private void BindButtons()
    {
        appButtons.Clear();
        var all = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name.Contains("App"))
                appButtons.Add(all[i]);
        }

        for (int i = 0; i < appButtons.Count; i++)
        {
            var button = appButtons[i];
            string target = ResolveTargetPanelForButton(button);
            if (string.IsNullOrEmpty(target)) continue;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ForceOpenByName(target));
        }
    }

    private void ForceOpenByName(string targetName)
    {
        if (targetName == "App_Health" && !IsHealthAllowedInCurrentFlow())
            return;

        var target = FindByName(targetName);
        if (target == null) return;

        if (homePanel != null) homePanel.SetActive(false);

        if (appRules != null) appRules.SetActive(target == appRules);
        if (appHealth != null) appHealth.SetActive(target == appHealth);
        if (appChat != null) appChat.SetActive(target == appChat);
        if (appMusic != null) appMusic.SetActive(target == appMusic);

        // panel 부모 컨테이너가 꺼져 있으면 강제로 켠다.
        if (target.transform.parent != null)
            target.transform.parent.gameObject.SetActive(true);
    }

    private void SetupExclusiveQuestionToggles()
    {
        for (int i = 0; i < questionNames.Length; i++)
        {
            var question = FindByName(questionNames[i]);
            if (question == null) continue;

            var group = question.GetComponent<ToggleGroup>();
            if (group == null) group = question.AddComponent<ToggleGroup>();
            group.allowSwitchOff = true; // 기본 미선택 허용

            var toggles = question.GetComponentsInChildren<Toggle>(true);
            for (int j = 0; j < toggles.Length; j++)
            {
                toggles[j].group = group;
            }

            // 기본 상태: 아무것도 체크 안됨
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
        if (button == null) return null;

        // 이름 우선
        if (button.name.Contains("Rules")) return "App_Rules";
        if (button.name.Contains("Health")) return "App_Health";
        if (button.name.Contains("Chat")) return "App_Chat";
        if (button.name.Contains("Music")) return "App_Music";

        // 텍스트 폴백 (버튼 이름/배치가 뒤바뀐 경우 대응)
        var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            string t = text.text.Replace(" ", "").Trim().ToLowerInvariant();
            if (t.Contains("rule") || t.Contains("규칙")) return "App_Rules";
            if (t.Contains("health") || t.Contains("자가진단") || t.Contains("건강")) return "App_Health";
            if (t.Contains("chat") || t.Contains("채팅")) return "App_Chat";
            if (t.Contains("music") || t.Contains("음악")) return "App_Music";
        }

        return null;
    }

    private GameObject FindByName(string name)
    {
        var tr = transform.Find(name);
        if (tr != null) return tr.gameObject;

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.name == name) return child.gameObject;

            var nested = child.GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < nested.Length; j++)
                if (nested[j].name == name) return nested[j].gameObject;
        }

        return null;
    }
}
