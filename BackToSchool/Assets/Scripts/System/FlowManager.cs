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
    public static FlowManager Instance { get; private set; }

    [Header("Progress")]
    public int day = 1;               // 1~5
    public int stepIndex = 0;
    public int penaltyPoints = 0;
    public int penaltyThreshold = 3;  // 임시 3점

    [Header("Debug")]
    public bool autoStartOnPlay = true;

    // day -> event list
    Dictionary<int, List<FlowEvent>> timeline;

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
        if (autoStartOnPlay)
        {
            PlayCurrent();
        }
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
            list.Add(E(FlowEventType.MINIGAME, $"", "수업1 미니게임"));
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
        PlayerPrefs.SetString("FLOW_ID", ev.id);
        PlayerPrefs.SetString("FLOW_TYPE", ev.type.ToString());

        // ✅ GameManager 상태는 필요한 모드만 건드림
        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            gm.currentDay = day;

            if (ev.type == FlowEventType.CHAT)
                gm.ChangeState(GameState.Subway);

            else if (ev.type == FlowEventType.FREEROAM)
                gm.ChangeState(GameState.Lunch_FreeTime);
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
        penaltyPoints += penaltyDelta;
        stepIndex++;
        PlayCurrent();
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


