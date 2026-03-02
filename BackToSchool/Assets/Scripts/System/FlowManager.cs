using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum FlowEventType { CHAT, FREEROAM, STORY, MINIGAME }

[Serializable]
public class FlowEvent
{
    public FlowEventType type;
    public string id;            // STORY면 conversationId, MINIGAME면 minigameId, CHAT이면 chatId, FREEROAM이면 contextId
    public string note;          // 디버그용(선택)
    public Func<FlowManager, bool> condition; // 분기 조건(선택)
}

public class FlowManager : MonoBehaviour
{
    public const string StoryAppendConversationPrefKey = "FLOW_STORY_APPEND_ID";

    public static FlowManager Instance { get; private set; }

    [Header("Progress")]
    public int day = 1;               // 1~5
    public int stepIndex = 0;
    public int penaltyPoints = 0;
    public int penaltyThreshold = 3;  // 임시 3점

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

        // ========== Day 1~4 공통 ==========
        for (int d = 1; d <= 4; d++)
        {
            var list = new List<FlowEvent>();

            list.Add(E(FlowEventType.CHAT, "", "등교 지하철"));
            list.Add(E(FlowEventType.FREEROAM, $"", "조회 전 자유이동"));
            list.Add(E(FlowEventType.STORY, $"DAY{d}_CLASSOPEN", "아침 조회"));

            list.Add(E(FlowEventType.STORY, $"D{d}_CLASS1_START", "수업1 시작 전"));
            list.Add(E(FlowEventType.MINIGAME, $"CLASS1_D{d}", "수업1 미니게임"));
            list.Add(E(FlowEventType.STORY, $"D{d}_CLASS1_END", "수업1 종료"));

            list.Add(E(FlowEventType.MINIGAME, $"LUNCH_Tetris{d}", "점심 미니게임"));
            list.Add(E(FlowEventType.FREEROAM, $"D{d}_LUNCH_FREEROAM", "점심 자유이동"));

            list.Add(E(FlowEventType.STORY, $"D{d}_CLASS2_START", "수업2 시작 전"));
            list.Add(E(FlowEventType.MINIGAME, $"CLASS2_D{d}", "수업2 미니게임"));
            list.Add(E(FlowEventType.STORY, $"D{d}_CLASS2_END", "수업2 종료"));

            list.Add(E(FlowEventType.STORY, $"D{d}_DISMISSAL", "종례"));
            list.Add(E(FlowEventType.STORY, $"D{d}_AFTERSCHOOL", "방과후"));

            // 분기: 벌점 >= threshold면 청소 컷씬
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
            list.Add(E(FlowEventType.FREEROAM, $"D{d}_BEFORE_ASSEMBLY", "조회 전 자유이동"));
            list.Add(E(FlowEventType.STORY, $"D{d}_ASSEMBLY", "아침 조회"));

            list.Add(E(FlowEventType.MINIGAME, $"BIG_CLEANING_D{d}", "대청소 미니게임"));
            list.Add(E(FlowEventType.STORY, $"D{d}_BIG_CLEANING_AFTER", "대청소 후 스토리"));

            list.Add(E(FlowEventType.STORY, $"D{d}_DISMISSAL", "종례"));
            list.Add(E(FlowEventType.STORY, $"D{d}_LUNCH_WITH_FRIENDS", "친구들이랑 점심"));

            list.Add(E(FlowEventType.CHAT, $"D{d}_CHAT_TO_HOME", "하교 지하철"));

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
            Debug.LogError($"[FlowManager] Day {day} 타임라인 없음");
            return;
        }

        var list = timeline[day];

        // 조건 있는 이벤트는 스킵 가능하게 처리
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

        LoadModeScene(ev);
    }

    void LoadModeScene(FlowEvent ev)
    {
        string resolvedId = ResolveFlowId(ev);
        PlayerPrefs.SetString("FLOW_ID", resolvedId);
        PlayerPrefs.SetString("FLOW_TYPE", ev.type.ToString());

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



    // 각 모드가 끝나면 이걸 호출하면 됨
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
                Debug.Log("[FlowManager] 조회 지적 이후 자동으로 실내화 착용 처리");
            }
        }

        penaltyPoints += penaltyDelta;
        stepIndex++;
        PlayCurrent();
    }

    public void AddPenaltyWithReason(int penaltyDelta, string reasonId)
    {
        if (penaltyDelta <= 0)
            return;

        penaltyPoints += penaltyDelta;
        PenaltyReasonLog.Add(reasonId, penaltyDelta, day);
    }

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
    // 지금 이벤트 강제 완료(개발용)
    public void DebugSkip(int penaltyDelta = 0)
    {
        CompleteCurrentEvent(penaltyDelta);
    }

    // 특정 Day/Step으로 순간이동(개발용)
    public void DebugJump(int targetDay, int targetStep, int penalty = 0)
    {
        day = targetDay;
        stepIndex = targetStep;
        penaltyPoints = penalty;
        PlayCurrent();
    }
    #endif

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

        string altId = ev.id + "_NO_SLIPPERS";
        if (HasConversation(altId))
        {
            // 미착용 지적 대사 후 기존 조회 대사를 이어서 재생
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


