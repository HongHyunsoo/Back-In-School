using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum FlowEventType { CHAT, FREEROAM, STORY, MINIGAME }

[Serializable]
public class FlowEvent
{
    public FlowEventType type;
    public string id;            // STORY: conversationId, MINIGAME: minigameId, CHAT: chatId, FREEROAM: contextId
    public string note;          // 디버그용 메모
    public Func<FlowManager, bool> condition; // 선택적 분기 조건
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
    public int penaltyThreshold = 3;  // 임시 기준값

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

        // ========== Day 1 ==========
        {
            var list = new List<FlowEvent>();

            list.Add(E(FlowEventType.CHAT, "D1_CHAT_TO_SCHOOL", "Chat To School"));
            list.Add(E(FlowEventType.MINIGAME, "ARRIVAL_SPACE_D1", "Arrival Space Mash"));
            list.Add(E(FlowEventType.FREEROAM, "D1_BEFORE_ASSEMBLY", "Before Assembly"));
            list.Add(E(FlowEventType.STORY, "DAY1_CLASSOPEN", "Class Open"));

            list.Add(E(FlowEventType.STORY, "D1_CLASS1_START", "Class1 Start"));
            list.Add(E(FlowEventType.MINIGAME, "CLASS1_D1", "Class1 Minigame"));
            list.Add(E(FlowEventType.STORY, "D1_CLASS1_END", "Class1 End"));

            list.Add(E(FlowEventType.MINIGAME, "LUNCH_Tetris1", "Lunch Tetris"));
            list.Add(E(FlowEventType.FREEROAM, "D1_LUNCH_FREEROAM", "Lunch FreeRoam"));

            list.Add(E(FlowEventType.STORY, "D1_CLASS2_START", "Class2 Start"));
            list.Add(E(FlowEventType.MINIGAME, "CLASS2_D1", "Class2 Minigame"));
            list.Add(E(FlowEventType.STORY, "D1_CLASS2_END", "Class2 End"));

            list.Add(E(FlowEventType.STORY, "DAY1_CLASSEND", "Class End"));
            list.Add(E(FlowEventType.STORY, "D1_AfterSchool_A", "AfterSchool A"));
            list.Add(E(FlowEventType.MINIGAME, "AFTERSCHOOL_ENGLISH_D1", "AfterSchool English"));
            list.Add(E(FlowEventType.STORY, "D1_AfterSchool_B", "AfterSchool B"));
            list.Add(E(FlowEventType.STORY, "D1_AfterSchool_C", "AfterSchool C"));
            list.Add(E(FlowEventType.STORY, "D1_AfterSchool_D", "AfterSchool D"));
            list.Add(E(FlowEventType.STORY, "D1_AfterSchool_E", "AfterSchool E"));
            list.Add(E(FlowEventType.STORY, "D1_AfterSchool_F", "AfterSchool F"));
            list.Add(E(FlowEventType.CHAT, "D1_CHAT_TO_HOME", "Chat To Home"));

            timeline[1] = list;
        }

        // ========== Day 2~4 공통 ==========
        for (int d = 2; d <= 4; d++)
        {
            var list = new List<FlowEvent>();

            list.Add(E(FlowEventType.CHAT, $"D{d}_CHAT_TO_SCHOOL", "등교 지하철"));
            list.Add(E(FlowEventType.MINIGAME, $"ARRIVAL_SPACE_D{d}", "Arrival Space Mash"));
            list.Add(E(FlowEventType.FREEROAM, $"D{d}_BEFORE_ASSEMBLY", "조회 전 자유 이동"));
            list.Add(E(FlowEventType.STORY, $"DAY{d}_CLASSOPEN", "아침 조회"));

            list.Add(E(FlowEventType.STORY, $"D{d}_CLASS1_START", "Class1 Start"));
            list.Add(E(FlowEventType.MINIGAME, $"CLASS1_D{d}", "수업 1 미니게임"));
            list.Add(E(FlowEventType.STORY, $"D{d}_CLASS1_END", "수업 1 종료"));

            list.Add(E(FlowEventType.MINIGAME, $"LUNCH_Tetris{d}", "점심 미니게임"));
            list.Add(E(FlowEventType.FREEROAM, $"D{d}_LUNCH_FREEROAM", "점심 자유 이동"));

            list.Add(E(FlowEventType.STORY, $"D{d}_CLASS2_START", "Class2 Start"));
            list.Add(E(FlowEventType.MINIGAME, $"CLASS2_D{d}", "수업 2 미니게임"));
            list.Add(E(FlowEventType.STORY, $"D{d}_CLASS2_END", "수업 2 종료"));

            list.Add(E(FlowEventType.STORY, $"D{d}_DISMISSAL", "종례"));
            list.Add(E(FlowEventType.STORY, $"D{d}_AFTERSCHOOL", "AfterSchool"));
            list.Add(E(FlowEventType.FREEROAM, $"D{d}_AFTERSCHOOL_FREEROAM", "AfterSchool FreeRoam"));

            // 분기: 벌점이 기준값 이상이면 청소 컷신 재생
            list.Add(E(FlowEventType.STORY, $"D{d}_CLEANING", "벌점 청소")
                .WithCondition(gm => gm.penaltyPoints >= gm.penaltyThreshold));

            list.Add(E(FlowEventType.CHAT, $"D{d}_CHAT_TO_HOME", "하교 지하철"));

            timeline[d] = list;
        }

        // ========== Day 5 ==========
        {
            int d = 5;
            var list = new List<FlowEvent>();

            list.Add(E(FlowEventType.CHAT, $"D{d}_CHAT_TO_SCHOOL", "등교 지하철"));
            list.Add(E(FlowEventType.MINIGAME, $"ARRIVAL_SPACE_D{d}", "Arrival Space Mash"));
            list.Add(E(FlowEventType.FREEROAM, $"D{d}_BEFORE_ASSEMBLY", "조회 전 자유 이동"));
            list.Add(E(FlowEventType.STORY, $"D{d}_ASSEMBLY", "아침 조회"));

            list.Add(E(FlowEventType.MINIGAME, $"BIG_CLEANING_D{d}", "대청소 미니게임"));
            list.Add(E(FlowEventType.STORY, $"D{d}_BIG_CLEANING_AFTER", "BigCleaning After"));

            list.Add(E(FlowEventType.STORY, $"D{d}_DISMISSAL", "종례"));
            list.Add(E(FlowEventType.STORY, $"D{d}_LUNCH_WITH_FRIENDS", "친구들과의 점심"));

            list.Add(E(FlowEventType.CHAT, $"D{d}_CHAT_TO_HOME", "하교 지하철"));

            timeline[d] = list;
        }
    }

    FlowEvent E(FlowEventType type, string id, string note = null)
        => new FlowEvent { type = type, id = id, note = note };

    public void PlayCurrent(bool useSceneFade = true)
    {
        if (stepIndex == 0)
            shoeStateDay = -1;

        EnsureShoeStateForCurrentDay();

        if (!timeline.ContainsKey(day))
        {
            Debug.LogError($"[FlowManager] Day {day} 타임라인이 없습니다.");
            return;
        }

        var list = timeline[day];

        // 조건을 만족하지 않는 이벤트는 연속으로 건너뛴다.
        while (stepIndex < list.Count && list[stepIndex].condition != null && !list[stepIndex].condition(this))
        {
            stepIndex++;
        }

        if (stepIndex >= list.Count)
        {
            Debug.Log($"[FlowManager] Day {day} 완료");
            return;
        }

        var ev = list[stepIndex];
        Debug.Log($"[FlowManager] Day {day} Step {stepIndex}: {ev.type} {ev.id} ({ev.note})");

        LoadModeScene(ev, useSceneFade);
    }

    void LoadModeScene(FlowEvent ev, bool useSceneFade = true)
    {
        string resolvedId = ResolveFlowId(ev);
        FlowContext.Set(resolvedId, ev.type);
        Day1TutorialController.SyncToFlowPosition(day, stepIndex, resolvedId, ev.type);
        PhoneGalleryService.NotifyFlowVisited(resolvedId);

        // Keep day in sync only. Scene-based state enter is handled by GameManager.OnSceneLoaded.
        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            gm.currentDay = day;
        }

        if (!useSceneFade)
        {
            switch (ev.type)
            {
                case FlowEventType.CHAT: SceneManager.LoadScene("CHAT"); break;
                case FlowEventType.FREEROAM: SceneManager.LoadScene("FREEROAM"); break;
                case FlowEventType.STORY: SceneManager.LoadScene("STORY"); break;
                case FlowEventType.MINIGAME: SceneManager.LoadScene("MINIGAME"); break;
            }
            return;
        }

        switch (ev.type)
        {
            case FlowEventType.CHAT: SceneTransitionFader.LoadSceneWithFade("CHAT"); break;
            case FlowEventType.FREEROAM: SceneTransitionFader.LoadSceneWithFade("FREEROAM"); break;
            case FlowEventType.STORY: SceneTransitionFader.LoadSceneWithFade("STORY"); break;
            case FlowEventType.MINIGAME: SceneTransitionFader.LoadSceneWithFade("MINIGAME"); break;
        }
    }



    // 현재 이벤트를 완료하고 다음 이벤트로 이동한다.
    public void CompleteCurrentEvent(int penaltyDelta = 0, bool useSceneFade = true)
    {
        if (timeline.TryGetValue(day, out var list) && stepIndex < list.Count)
        {
            var finishedEvent = list[stepIndex];
            if (finishedEvent.type == FlowEventType.STORY &&
                IsMorningAssemblyEvent(finishedEvent.id) &&
                !isWearingSlippers)
            {
                ForceWearSlippers();
                Debug.Log("[FlowManager] 조회 종료 후 자동으로 실내화 착용 처리");
            }
        }

        penaltyPoints += penaltyDelta;
        stepIndex++;
        PlayCurrent(useSceneFade);
    }

    public bool TryGetNextPlayableEvent(out FlowEvent nextEvent, out int nextIndex)
    {
        nextEvent = null;
        nextIndex = -1;

        if (!timeline.TryGetValue(day, out var list))
            return false;

        int probeIndex = stepIndex + 1;
        while (probeIndex < list.Count && list[probeIndex].condition != null && !list[probeIndex].condition(this))
            probeIndex++;

        if (probeIndex < 0 || probeIndex >= list.Count)
            return false;

        nextIndex = probeIndex;
        nextEvent = list[probeIndex];
        return nextEvent != null;
    }

    public bool TryPrepareNextEventWithoutSceneLoad(FlowEventType expectedType, int penaltyDelta, out string resolvedId)
    {
        resolvedId = null;

        if (!TryGetNextPlayableEvent(out var nextEvent, out int nextIndex))
            return false;

        if (nextEvent == null || nextEvent.type != expectedType)
            return false;

        penaltyPoints += penaltyDelta;
        stepIndex = nextIndex;
        EnsureShoeStateForCurrentDay();

        resolvedId = ResolveFlowId(nextEvent);
        FlowContext.Set(resolvedId, nextEvent.type);
        Day1TutorialController.SyncToFlowPosition(day, stepIndex, resolvedId, nextEvent.type);
        PhoneGalleryService.NotifyFlowVisited(resolvedId);

        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            gm.currentDay = day;

        return true;
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
    // 현재 이벤트 강제 완료(개발용)
    public void DebugSkip(int penaltyDelta = 0)
    {
        CompleteCurrentEvent(penaltyDelta);
    }

    // 지정 Day/Step으로 이동(개발용)
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
            // 미착용 분기 대사 뒤에 기존 조회 대사를 이어서 재생한다.
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



