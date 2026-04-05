using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ChatRoomState
{
    public string roomId;
    public string title;
    public int unreadCount;
}

[Serializable]
public class ChatSessionState
{
    // sessionId == Conversation_ID (Conversations.csv)
    public string sessionId;
    public string roomId;
    public int progressIndex;
    public bool completed;
}

[Serializable]
public class ChatSaveData
{
    public List<ChatRoomState> rooms = new();
    public List<ChatSessionState> sessions = new();
    public string activeSessionId = null;
}

/// <summary>
/// ChatService (v2 - 세그먼트 기반)
/// - ChatSegments.csv (Day+State -> {Room_ID, Conversation_ID})를 기반으로 세션을 '그때그때' 활성화
/// - Conversations.csv는 LocalizationManager.GetConversation()으로 읽고,
///   ChatLineMeta.csv는 LocalizationManager.TryGetChatLineMeta()로 읽는 구조
/// </summary>
public class ChatService : MonoBehaviour
{
    public static ChatService Instance { get; private set; }

    private const string PREF_KEY = "CHAT_SAVE_V2";

    public ChatSaveData Data { get; private set; } = new ChatSaveData();

    public bool HasActiveSession => !string.IsNullOrEmpty(Data.activeSessionId);

    public event Action OnChanged; // UI 갱신용
    public event Action<string, int> OnUnreadAdded;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadOrCreateDefault();

        // ChatSegments 정의 기반으로 방이 누락되지 않게 보정
        EnsureRoomsFromChatSegments();
        EnsureRoomsFromConversationTriggers();
        RefreshRoomTitlesFromLocalization(notify: false);
        DialogueManager.DialogueConversationCompleted += HandleDialogueConversationCompleted;

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        DialogueManager.DialogueConversationCompleted -= HandleDialogueConversationCompleted;

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
    }

    // -------------------------
    // Public API (Rooms)
    // -------------------------

    public IReadOnlyList<ChatRoomState> GetRooms() => Data.rooms;

    public ChatRoomState GetRoom(string roomId)
        => Data.rooms.Find(r => r.roomId == roomId);

    public int GetTotalUnread()
    {
        int sum = 0;
        foreach (var r in Data.rooms) sum += r.unreadCount;
        return sum;
    }

    public void AddUnread(string roomId, int amount = 1)
    {
        var room = GetRoom(roomId);
        if (room == null) return;
        int appliedAmount = Mathf.Max(0, amount);
        if (appliedAmount <= 0)
            return;

        room.unreadCount += appliedAmount;
        Save();
        OnChanged?.Invoke();
        OnUnreadAdded?.Invoke(roomId, appliedAmount);
    }

    public void MarkRoomRead(string roomId)
    {
        var room = GetRoom(roomId);
        if (room == null) return;
        room.unreadCount = 0;
        Save();
        OnChanged?.Invoke();
    }

    // -------------------------
    // Public API (Sessions)
    // -------------------------

    public ChatSessionState GetSession(string sessionId)
        => Data.sessions.Find(s => s.sessionId == sessionId);

    /// <summary>
    /// roomId에 대해 아직 완료되지 않은(또는 아직 시작 안 한) 세션 중 첫 번째를 반환.
    /// UI가 room -> conversationId를 Inspector에서 매핑하기 싫으면 이걸 쓰면 됨.
    /// </summary>
    public string GetNextSessionIdForRoom(string roomId)
    {
        foreach (var s in Data.sessions)
        {
            if (s.roomId == roomId && !s.completed)
                return s.sessionId;
        }
        return null;
    }

    public void StartSession(string sessionId)
    {
        // 이미 다른 세션 진행 중이면 막기(네 규칙)
        if (HasActiveSession) return;

        var session = GetSession(sessionId);
        if (session == null) return;
        if (session.completed) return;

        Data.activeSessionId = sessionId;
        Save();
        OnChanged?.Invoke();
    }

    public void AdvanceSession(string sessionId)
    {
        var session = GetSession(sessionId);
        if (session == null || session.completed) return;

        session.progressIndex++;
        Save();
        OnChanged?.Invoke();
    }

    public void CompleteSession(string sessionId)
    {
        var session = GetSession(sessionId);
        if (session == null) return;

        session.completed = true;

        if (Data.activeSessionId == sessionId)
            Data.activeSessionId = null;

        Save();
        OnChanged?.Invoke();
    }

    // -------------------------
    // Segments (Day+State -> enqueue sessions)
    // -------------------------

    /// <summary>
    /// GameManager의 (currentDay, currentState)가 바뀔 때 호출:
    /// 해당 시점에 도착해야 하는 채팅 세그먼트(Conversation_ID)를 각 Room에 활성화한다.
    /// </summary>
    public void ActivateSegmentsFor(int day, GameState state)
    {
        string activeFlowId = PlayerPrefs.GetString("FLOW_ID", "");
        var segs = ChatSegmentCatalog.Instance.GetSegments(day, state, activeFlowId);
        if (segs == null || segs.Count == 0)
            return;

        foreach (var seg in segs)
        {
            if (string.IsNullOrEmpty(seg.RoomId) || string.IsNullOrEmpty(seg.ConversationId))
                continue;

            EnsureRoomExists(seg.RoomId);

            // sessionId == conversationId
            var session = GetSession(seg.ConversationId);
            bool isNewSession = false;
            if (session == null)
            {
                session = new ChatSessionState
                {
                    sessionId = seg.ConversationId,
                    roomId = seg.RoomId,
                    progressIndex = 0,
                    completed = false
                };
                Data.sessions.Add(session);
                isNewSession = true;
            }
            else
            {
                // 이미 존재하는 세션인데 roomId가 다르면(데이터 변경) 보정
                session.roomId = seg.RoomId;
            }

            if (isNewSession && seg.Notify)
                AddUnread(seg.RoomId, 1);
        }

        Save();
        OnChanged?.Invoke();

        Debug.Log($"[Chat] ActivateSegmentsFor day={day}, state={state}, flowId={activeFlowId}, segCount={segs.Count}");
        foreach (var seg in segs)
            Debug.Log($"[Chat] + room={seg.RoomId} conv={seg.ConversationId} notify={seg.Notify}");

    }
    private void EnsureRoomExists(string roomId)
    {
        if (GetRoom(roomId) != null) return;

        Data.rooms.Add(new ChatRoomState
        {
            roomId = roomId,
            title = ResolveRoomTitle(roomId, roomId),
            unreadCount = 0
        });
    }
    private void EnsureRoomsFromChatSegments()
    {
        var roomIds = ChatSegmentCatalog.Instance.GetAllRoomIds();
        foreach (var roomId in roomIds)
            EnsureRoomExists(roomId);

        Save();
        OnChanged?.Invoke();
    }

    private void EnsureRoomsFromConversationTriggers()
    {
        var roomIds = ChatConversationTriggerCatalog.Instance.GetAllRoomIds();
        foreach (var roomId in roomIds)
            EnsureRoomExists(roomId);

        Save();
        OnChanged?.Invoke();
    }

    private void OnLanguageChanged(Language _)
    {
        RefreshRoomTitlesFromLocalization(notify: true);
    }

    private void RefreshRoomTitlesFromLocalization(bool notify)
    {
        bool changed = false;
        for (int i = 0; i < Data.rooms.Count; i++)
        {
            var room = Data.rooms[i];
            string resolved = ResolveRoomTitle(room.roomId, room.title);
            if (room.title == resolved)
                continue;

            room.title = resolved;
            changed = true;
        }

        if (notify && changed)
            OnChanged?.Invoke();
    }

    private static string ResolveRoomTitle(string roomId, string fallback)
    {
        if (string.IsNullOrEmpty(roomId))
            return string.IsNullOrEmpty(fallback) ? "Room" : fallback;

        if (LocalizationManager.Instance != null)
        {
            if (LocalizationManager.Instance.TryGetLine(roomId, out string localizedById) && !string.IsNullOrEmpty(localizedById))
                return localizedById;

            if (!roomId.StartsWith("ROOM_", StringComparison.Ordinal) &&
                LocalizationManager.Instance.TryGetLine("ROOM_" + roomId, out string localizedByPrefixed) &&
                !string.IsNullOrEmpty(localizedByPrefixed))
            {
                return localizedByPrefixed;
            }
        }

        if (!string.IsNullOrEmpty(fallback))
            return fallback;

        return roomId;
    }

    // -------------------------
    // Save/Load
    // -------------------------

    public void Save()
    {
        string json = JsonUtility.ToJson(Data);
        PlayerPrefs.SetString(PREF_KEY, json);
        PlayerPrefs.Save();
    }

    private void LoadOrCreateDefault()
    {
        if (!PlayerPrefs.HasKey(PREF_KEY))
        {
            CreateDefaultData();
            Save();
            return;
        }

        string json = PlayerPrefs.GetString(PREF_KEY, "");
        if (string.IsNullOrEmpty(json))
        {
            CreateDefaultData();
            Save();
            return;
        }

        try
        {
            Data = JsonUtility.FromJson<ChatSaveData>(json);
            if (Data == null || Data.rooms == null || Data.sessions == null)
            {
                CreateDefaultData();
                Save();
            }
        }
        catch
        {
            CreateDefaultData();
            Save();
        }
    }

    private void CreateDefaultData()
    {
        Data = new ChatSaveData();

        // (중요) Room/Session은 ChatSegments 기반으로 생성/활성화되도록 변경
        // - rooms: EnsureRoomsFromChatSegments()에서 자동 생성
        // - sessions: ActivateSegmentsFor(day, state)에서 그때그때 생성
        Data.activeSessionId = null;
    }

    public ChatSessionState EnsureSession(string sessionId, string roomId)
    {
        var s = GetSession(sessionId);
        if (s == null)
        {
            s = new ChatSessionState
            {
                sessionId = sessionId,
                roomId = roomId,
                progressIndex = 0,
                completed = false
            };
            Data.sessions.Add(s);
            Save();
            OnChanged?.Invoke();
        }
        else
        {
            // 혹시 room 매핑이 바뀌었을 때 보정
            if (!string.IsNullOrEmpty(roomId))
                s.roomId = roomId;
        }

        return s;
    }

    public void ResetAllChatForTest()
    {
        // 진행 중 세션도 해제
        Data.activeSessionId = null;

        // 진행도/완료 전부 리셋
        foreach (var s in Data.sessions)
        {
            s.progressIndex = 0;
            s.completed = false;
        }

        // 안 읽음도 리셋(원하면 이 줄은 빼도 됨)
        foreach (var r in Data.rooms)
            r.unreadCount = 0;

        Save();
        OnChanged?.Invoke();
    }

    public void ResetForNewGame()
    {
        Data = new ChatSaveData();
        EnsureRoomsFromChatSegments();
        Save();
        OnChanged?.Invoke();
    }

    public static void ResetPersistedDataForNewGame()
    {
        PlayerPrefs.DeleteKey(PREF_KEY);
        PlayerPrefs.Save();

        if (Instance != null)
            Instance.ResetForNewGame();
    }

    private void HandleDialogueConversationCompleted(string conversationId)
    {
        if (!FlowContext.IsFreeRoam())
            return;

        if (string.IsNullOrEmpty(conversationId))
            return;

        int day = 0;
        if (FlowManager.Instance != null)
            day = Mathf.Max(1, FlowManager.Instance.day);
        else
        {
            var gm = FindAnyObjectByType<GameManager>();
            if (gm != null)
                day = Mathf.Max(1, gm.currentDay);
        }

        var gmState = FindAnyObjectByType<GameManager>();
        if (day <= 0 || gmState == null)
            return;

        ActivateConversationTriggeredSegments(conversationId, day, gmState.currentState, FlowContext.CurrentId);
    }

    private void ActivateConversationTriggeredSegments(string triggerConversationId, int day, GameState state, string activeFlowId)
    {
        var entries = ChatConversationTriggerCatalog.Instance.GetEntries(triggerConversationId, day, state, activeFlowId);
        if (entries == null || entries.Count == 0)
            return;

        bool changed = false;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (string.IsNullOrEmpty(entry.RoomId) || string.IsNullOrEmpty(entry.ConversationId))
                continue;

            EnsureRoomExists(entry.RoomId);

            var session = GetSession(entry.ConversationId);
            bool isNewSession = false;
            if (session == null)
            {
                session = new ChatSessionState
                {
                    sessionId = entry.ConversationId,
                    roomId = entry.RoomId,
                    progressIndex = 0,
                    completed = false
                };
                Data.sessions.Add(session);
                isNewSession = true;
                changed = true;
            }
            else
            {
                session.roomId = entry.RoomId;
            }

            if (isNewSession && entry.Notify)
                AddUnread(entry.RoomId, 1);
        }

        if (!changed)
            return;

        Save();
        OnChanged?.Invoke();
    }


}

