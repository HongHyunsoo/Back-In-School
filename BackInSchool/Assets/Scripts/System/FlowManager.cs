using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum FlowEventType { CHAT, FREEROAM, STORY, MINIGAME }

[Serializable]
public class FlowEvent
{
    public FlowEventType type;
    public string id;            // STORY硫?conversationId, MINIGAME硫?minigameId, CHAT?대㈃ chatId, FREEROAM?대㈃ contextId
    public string note;          // ?붾쾭洹몄슜(?좏깮)
    public Func<FlowManager, bool> condition; // 遺꾧린 議곌굔(?좏깮)
}

public class FlowManager : MonoBehaviour
{
    public const string StoryAppendConversationPrefKey = "FLOW_STORY_APPEND_ID";
    public const string LunchFreeTimeStartMinutePrefKey = "FLOW_LUNCH_FREETIME_START_MINUTE";
    public const string LunchFreeTimeStartDayPrefKey = "FLOW_LUNCH_FREETIME_START_DAY";

    public static FlowManager Instance { get; private set; }

    [Header("Progress")]
    public int day = 1;               // 1~5
    public int stepIndex = 0;
    public int penaltyPoints = 0;
    public int penaltyThreshold = 3;  // ?꾩떆 3??

    [Header("Debug")]
    public bool autoStartOnPlay = false;

    [Header("School Rules")]
    [SerializeField] private bool isWearingSlippers;
    [SerializeField] private bool changedToSlippersToday;

    // day -> event list
    Dictionary<int, List<FlowEvent>> timeline;
    int shoeStateDay = -1;
    bool noSlippersPenaltyAppliedToday;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildTimeline();
    }

    private void Start()
    {
        if (!autoStartOnPlay) return;

        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "Bootstrap" || sceneName == "MainMenu")
            return;

        PlayCurrent();
    }



    void BuildTimeline()
    {
        timeline = new Dictionary<int, List<FlowEvent>>();

        // ========== Day 1~4 怨듯넻 ==========
        for (int d = 1; d <= 4; d++)
        {
            var list = new List<FlowEvent>();

            list.Add(E(FlowEventType.CHAT, $"D{d}_CHAT_TO_SCHOOL", "?깃탳 吏?섏쿋"));
            list.Add(E(FlowEventType.MINIGAME, $"ARRIVAL_SPACE_D{d}", "Arrival Space Mash"));
            list.Add(E(FlowEventType.FREEROAM, $"D{d}_BEFORE_ASSEMBLY", "議고쉶 ???먯쑀?대룞"));
            list.Add(E(FlowEventType.STORY, $"DAY{d}_CLASSOPEN", "?꾩묠 議고쉶"));

            list.Add(E(FlowEventType.STORY, $"D{d}_CLASS1_START", "Class1 Start"));
            list.Add(E(FlowEventType.MINIGAME, $"CLASS1_D{d}", "?섏뾽1 誘몃땲寃뚯엫"));
            list.Add(E(FlowEventType.STORY, $"D{d}_CLASS1_END", "?섏뾽1 醫낅즺"));

            list.Add(E(FlowEventType.MINIGAME, $"LUNCH_Tetris{d}", "?먯떖 誘몃땲寃뚯엫"));
            list.Add(E(FlowEventType.FREEROAM, $"D{d}_LUNCH_FREEROAM", "?먯떖 ?먯쑀?대룞"));

            list.Add(E(FlowEventType.STORY, $"D{d}_CLASS2_START", "Class2 Start"));
            list.Add(E(FlowEventType.MINIGAME, $"CLASS2_D{d}", "?섏뾽2 誘몃땲寃뚯엫"));
            list.Add(E(FlowEventType.STORY, $"D{d}_CLASS2_END", "?섏뾽2 醫낅즺"));

            list.Add(E(FlowEventType.STORY, $"D{d}_DISMISSAL", "醫낅?"));
            list.Add(E(FlowEventType.STORY, $"D{d}_AFTERSCHOOL", "AfterSchool"));
            list.Add(E(FlowEventType.FREEROAM, $"D{d}_AFTERSCHOOL_FREEROAM", "AfterSchool FreeRoam"));

            // 遺꾧린: 踰뚯젏 >= threshold硫?泥?냼 而룹뵮
            list.Add(E(FlowEventType.STORY, $"D{d}_CLEANING", "踰뚯젏 泥?냼")
                .WithCondition(gm => gm.penaltyPoints >= gm.penaltyThreshold));

            list.Add(E(FlowEventType.CHAT, $"D{d}_CHAT_TO_HOME", "?섍탳 吏?섏쿋"));

            timeline[d] = list;
        }

        // ========== Day 5 ==========
        {
            int d = 5;
            var list = new List<FlowEvent>();

            list.Add(E(FlowEventType.CHAT, $"D{d}_CHAT_TO_SCHOOL", "?깃탳 吏?섏쿋"));
            list.Add(E(FlowEventType.MINIGAME, $"ARRIVAL_SPACE_D{d}", "Arrival Space Mash"));
            list.Add(E(FlowEventType.FREEROAM, $"D{d}_BEFORE_ASSEMBLY", "議고쉶 ???먯쑀?대룞"));
            list.Add(E(FlowEventType.STORY, $"D{d}_ASSEMBLY", "?꾩묠 議고쉶"));

            list.Add(E(FlowEventType.MINIGAME, $"BIG_CLEANING_D{d}", "?泥?냼 誘몃땲寃뚯엫"));
            list.Add(E(FlowEventType.STORY, $"D{d}_BIG_CLEANING_AFTER", "BigCleaning After"));

            list.Add(E(FlowEventType.STORY, $"D{d}_DISMISSAL", "醫낅?"));
            list.Add(E(FlowEventType.STORY, $"D{d}_LUNCH_WITH_FRIENDS", "移쒓뎄?ㅼ씠???먯떖"));

            list.Add(E(FlowEventType.CHAT, $"D{d}_CHAT_TO_HOME", "?섍탳 吏?섏쿋"));

            timeline[d] = list;
        }
    }

    FlowEvent E(FlowEventType type, string id, string note = null)
        => new FlowEvent { type = type, id = id, note = note };

    public void PlayCurrent()
    {
        if (stepIndex == 0)
            shoeStateDay = -1;

        EnsureShoeStateForCurrentDay();

        if (!timeline.ContainsKey(day))
        {
            Debug.LogError($"[FlowManager] Day {day} ??꾨씪???놁쓬");
            return;
        }

        var list = timeline[day];

        // 議곌굔 ?덈뒗 ?대깽?몃뒗 ?ㅽ궢 媛?ν븯寃?泥섎━
        while (stepIndex < list.Count && list[stepIndex].condition != null && !list[stepIndex].condition(this))
        {
            stepIndex++;
        }

        if (stepIndex >= list.Count)
        {
            Debug.Log($"[FlowManager] Day {day} ?꾨즺");
            return;
        }

        var ev = list[stepIndex];
        Debug.Log($"[FlowManager] Day {day} Step {stepIndex}: {ev.type} {ev.id} ({ev.note})");

        LoadModeScene(ev);
    }

    void LoadModeScene(FlowEvent ev)
    {
        string resolvedId = ResolveFlowId(ev);
        FlowContext.Set(resolvedId, ev.type);
        PhoneGalleryService.NotifyFlowVisited(resolvedId);

        // Keep day in sync only. Scene-based state enter is handled by GameManager.OnSceneLoaded.
        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            gm.currentDay = day;
        }

        switch (ev.type)
        {
            case FlowEventType.CHAT: SceneManager.LoadScene("CHAT"); break;
            case FlowEventType.FREEROAM: SceneManager.LoadScene("FREEROAM"); break;
            case FlowEventType.STORY: SceneManager.LoadScene("STORY"); break;
            case FlowEventType.MINIGAME: SceneManager.LoadScene("MINIGAME"); break;
        }
    }



    // 媛?紐⑤뱶媛 ?앸굹硫??닿구 ?몄텧?섎㈃ ??
    public void CompleteCurrentEvent(int penaltyDelta = 0)
    {
        if (timeline.TryGetValue(day, out var list) && stepIndex < list.Count)
        {
            var finishedEvent = list[stepIndex];
            if (finishedEvent.type == FlowEventType.STORY &&
                IsMorningAssemblyEvent(finishedEvent.id) &&
                !isWearingSlippers)
            {
                ForceWearSlippers();
                Debug.Log("[FlowManager] 議고쉶 吏???댄썑 ?먮룞?쇰줈 ?ㅻ궡??李⑹슜 泥섎━");
            }
        }

        penaltyPoints += penaltyDelta;
        stepIndex++;
        PlayCurrent();
    }

    public void SetLunchFreeTimeStartMinuteForCurrentDay(int minute)
    {
        PlayerPrefs.SetInt(LunchFreeTimeStartMinutePrefKey, minute);
        PlayerPrefs.SetInt(LunchFreeTimeStartDayPrefKey, day);
    }

    public int GetLunchFreeTimeStartMinuteForCurrentDay(int fallbackMinute)
    {
        int savedDay = PlayerPrefs.GetInt(LunchFreeTimeStartDayPrefKey, -1);
        if (savedDay != day)
            return fallbackMinute;

        return PlayerPrefs.GetInt(LunchFreeTimeStartMinutePrefKey, fallbackMinute);
    }

    public void AddPenaltyWithReason(int penaltyDelta, string reasonId)
    {
        if (penaltyDelta <= 0)
            return;

        penaltyPoints += penaltyDelta;
        PenaltyReasonLog.Add(reasonId, penaltyDelta, day);
    }
    // 吏湲??대깽??媛뺤젣 ?꾨즺(媛쒕컻??
    public void DebugSkip(int penaltyDelta = 0)
    {
        CompleteCurrentEvent(penaltyDelta);
    }

    // ?뱀젙 Day/Step?쇰줈 ?쒓컙?대룞(媛쒕컻??
    public void DebugJump(int targetDay, int targetStep, int penalty = 0)
    {
        day = targetDay;
        stepIndex = targetStep;
        penaltyPoints = penalty;
        PlayCurrent();
    }
    public bool IsWearingSlippers => isWearingSlippers;
    public bool ChangedToSlippersToday => changedToSlippersToday;

    public void ResetSchoolRuleRuntimeState()
    {
        shoeStateDay = -1;
        isWearingSlippers = false;
        changedToSlippersToday = false;
        noSlippersPenaltyAppliedToday = false;
        PhoneSubwayFlowGate.ClearHealthChecks();
        PlayerPrefs.DeleteKey(StoryAppendConversationPrefKey);
        PlayerPrefs.DeleteKey(LunchFreeTimeStartMinutePrefKey);
        PlayerPrefs.DeleteKey(LunchFreeTimeStartDayPrefKey);
    }

    public bool TryChangeToSlippers()
    {
        EnsureShoeStateForCurrentDay();

        if (isWearingSlippers)
            return false;

        isWearingSlippers = true;
        changedToSlippersToday = true;
        return true;
    }

    public void ForceWearSlippers()
    {
        EnsureShoeStateForCurrentDay();
        isWearingSlippers = true;
    }

    void EnsureShoeStateForCurrentDay()
    {
        if (shoeStateDay == day)
            return;

        shoeStateDay = day;
        isWearingSlippers = false;
        changedToSlippersToday = false;
        noSlippersPenaltyAppliedToday = false;
    }

    string ResolveFlowId(FlowEvent ev)
    {
        if (ev.type != FlowEventType.STORY || !IsMorningAssemblyEvent(ev.id))
        {
            PlayerPrefs.DeleteKey(StoryAppendConversationPrefKey);
            return ev.id;
        }

        if (isWearingSlippers)
        {
            PlayerPrefs.DeleteKey(StoryAppendConversationPrefKey);
            return ev.id;
        }

        if (!noSlippersPenaltyAppliedToday)
        {
            AddPenaltyWithReason(1, PenaltyReasonLog.ReasonNoSlippers);
            noSlippersPenaltyAppliedToday = true;
        }

        // New key convention: *_NS (legacy fallback: *_NO_SLIPPERS)
        string altId = ev.id + "_NS";
        if (!HasConversation(altId))
            altId = ev.id + "_NO_SLIPPERS";

        if (HasConversation(altId))
        {
            // 誘몄갑??吏???????湲곗〈 議고쉶 ??щ? ?댁뼱???ъ깮
            PlayerPrefs.SetString(StoryAppendConversationPrefKey, ev.id);
            return altId;
        }

        PlayerPrefs.DeleteKey(StoryAppendConversationPrefKey);
        return ev.id;
    }

    static bool IsMorningAssemblyEvent(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        if (id.StartsWith("DAY", StringComparison.Ordinal) && id.IndexOf("_CLASSOPEN", StringComparison.Ordinal) >= 0)
            return true;

        if (id.StartsWith("D", StringComparison.Ordinal) && id.EndsWith("_ASSEMBLY", StringComparison.Ordinal))
            return true;

        return false;
    }

    static bool HasConversation(string conversationId)
    {
        if (LocalizationManager.Instance == null || string.IsNullOrEmpty(conversationId))
            return false;

        var lines = LocalizationManager.Instance.GetConversation(conversationId);
        return lines != null && lines.Count > 0;
    }
}

// Fluent helper
public static class FlowEventExt
{
    public static FlowEvent WithCondition(this FlowEvent ev, Func<FlowManager, bool> cond)
    {
        ev.condition = cond;
        return ev;
    }
}



