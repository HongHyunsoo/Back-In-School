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
        room.unreadCount += Mathf.Max(0, amount);
        Save();
        OnChanged?.Invoke();
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
        if (LocalizationManager.Instance == null)
        {
            Debug.LogWarning("[ChatService] LocalizationManager.Instance가 없어 ChatSegments를 활성화할 수 없음");
            return;
        }

        var segs = LocalizationManager.Instance.GetChatSegments(day, state);
        if (segs == null || segs.Count == 0) return;

        foreach (var seg in segs)
        {
            if (string.IsNullOrEmpty(seg.roomId) || string.IsNullOrEmpty(seg.conversationId))
                continue;

            EnsureRoomExists(seg.roomId);

            // sessionId == conversationId
            var session = GetSession(seg.conversationId);
            bool isNewSession = false;
            if (session == null)
            {
                session = new ChatSessionState
                {
                    sessionId = seg.conversationId,
                    roomId = seg.roomId,
                    progressIndex = 0,
                    completed = false
                };
                Data.sessions.Add(session);
                isNewSession = true;
            }
            else
            {
                // 이미 존재하는 세션인데 roomId가 다르면(데이터 변경) 보정
                session.roomId = seg.roomId;
            }

            if (isNewSession && seg.notify)
                AddUnread(seg.roomId, 1);
        }

        Save();
        OnChanged?.Invoke();

        Debug.Log($"[Chat] ActivateSegmentsFor day={day}, state={state}, segCount={segs.Count}");
        foreach (var seg in segs)
            Debug.Log($"[Chat] + room={seg.roomId} conv={seg.conversationId} notify={seg.notify}");

    }

    private void EnsureRoomExists(string roomId)
    {
        if (GetRoom(roomId) != null) return;

        // title은 일단 roomId 그대로 (나중에 Localization 키로 바꿔도 됨)
        Data.rooms.Add(new ChatRoomState
        {
            roomId = roomId,
            title = roomId,
            unreadCount = 0
        });
    }

    private void EnsureRoomsFromChatSegments()
    {
        if (LocalizationManager.Instance == null) return;

        var roomIds = LocalizationManager.Instance.GetAllChatRoomIds();
        foreach (var roomId in roomIds)
            EnsureRoomExists(roomId);

        Save();
        OnChanged?.Invoke();
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


}
