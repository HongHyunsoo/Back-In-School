using System;
using System.Collections.Generic;
using UnityEngine;

public enum Language
{
    Korean,
    English
}

public enum ChatMsgType
{
    TEXT,
    IMAGE,
    BOTH
}

[Serializable]
public class ChatSegmentDef
{
    public string conversationId;
    public string roomId;
    public int day;
    public GameState state;
    public int priority;
    public bool notify;
}

[Serializable]
public class ChatLineMetaDef
{
    public string conversationId;
    public int order;
    public ChatMsgType msgType;
    public string imageId;
    public bool waitTap;
    public float delay; // <0이면 기본값 사용
}

/*
 * ===================================================================================
 * LocalizationManager (v4.1 - ChatSegments/ChatLineMeta 로드 추가)
 * ===================================================================================
 * - 기존: Localization.csv + Conversations.csv 로드
 * - 추가: ChatSegments.csv + ChatLineMeta.csv 로드
 * ===================================================================================
 */
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    // 언어 설정
    public Language currentLanguage = Language.Korean;
    public Action<Language> OnLanguageChanged;

    // 1. '번역' (ID -> 번역 텍스트)
    private Dictionary<string, string> nameTableKOR;
    private Dictionary<string, string> lineTableKOR;
    private Dictionary<string, string> nameTableENG;
    private Dictionary<string, string> lineTableENG;

    // 현재 사용 중인 테이블
    private Dictionary<string, string> nameTable;
    private Dictionary<string, string> lineTable;

    // 2. '대화' (ID -> 대화 스크립트)
    private Dictionary<string, List<DialogueLine>> conversationScript;

    // 3. '채팅 세그먼트' (Day+State -> 세그먼트 목록)
    // key: $"{day}|{state}"
    private Dictionary<string, List<ChatSegmentDef>> chatSegmentsByDayState;

    // 4. '채팅 라인 메타' (conversationId+order -> meta)
    // key: $"{conversationId}|{order}"
    private Dictionary<string, ChatLineMetaDef> chatLineMetaByKey;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadLocalizationFile("Localization");
            LoadConversationFile("Conversations");

            // 채팅용 CSV (없어도 게임은 돌도록 경고만)
            LoadChatSegmentsFile("ChatSegments");
            LoadChatLineMetaFile("ChatLineMeta");

            SetLanguage(currentLanguage);
        }
    }

    // --- 1. 'Localization.csv' (번역) 로드 ---
    private void LoadLocalizationFile(string fileName)
    {
        nameTableKOR = new Dictionary<string, string>();
        lineTableKOR = new Dictionary<string, string>();
        nameTableENG = new Dictionary<string, string>();
        lineTableENG = new Dictionary<string, string>();

        TextAsset csvFile = Resources.Load<TextAsset>(fileName);
        if (csvFile == null)
        {
            Debug.LogError(fileName + ".csv 파일을 Resources 폴더에서 찾을 수 없습니다!");
            return;
        }

        string[] lines = csvFile.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            string row = lines[i].Trim();
            if (string.IsNullOrEmpty(row)) continue;

            string[] columns = ParseCSVLine(row);
            if (columns.Length >= 3)
            {
                string id = columns[0].Trim();
                string kor = columns[1].Trim().Trim('\"');
                string eng = columns.Length >= 3 ? columns[2].Trim().Trim('\"') : "";

                // 이름(ID가 NAME_ 로 시작)과 나머지 텍스트를 분리
                if (id.StartsWith("NAME_"))
                {
                    if (!nameTableKOR.ContainsKey(id)) nameTableKOR.Add(id, kor);
                    if (!string.IsNullOrEmpty(eng) && !nameTableENG.ContainsKey(id)) nameTableENG.Add(id, eng);
                }
                else
                {
                    // 기존에는 LINE_ 접두사만 대사로 인식했는데,
                    // DAY1_CHAT_M_01, DAY1_LUNCH_SOWRD_01 같은 ID도 모두 대사로 취급하도록 변경
                    if (!lineTableKOR.ContainsKey(id)) lineTableKOR.Add(id, kor);
                    if (!string.IsNullOrEmpty(eng) && !lineTableENG.ContainsKey(id)) lineTableENG.Add(id, eng);
                }
            }
        }
    }

    // CSV 라인 파싱 (따옴표 처리)
    private string[] ParseCSVLine(string line)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        string currentField = "";

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '\"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentField);
                currentField = "";
            }
            else
            {
                currentField += c;
            }
        }
        result.Add(currentField);

        return result.ToArray();
    }

    // --- 2. 'Conversations.csv' (대화) 로드 ---
    private void LoadConversationFile(string fileName)
    {
        conversationScript = new Dictionary<string, List<DialogueLine>>();

        TextAsset csvFile = Resources.Load<TextAsset>(fileName);
        if (csvFile == null)
        {
            Debug.LogError(fileName + ".csv 파일을 Resources 폴더에서 찾을 수 없습니다!");
            return;
        }

        string[] lines = csvFile.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            string row = lines[i].Trim();
            if (string.IsNullOrEmpty(row)) continue;

            string[] columns = ParseCSVLine(row);
            if (columns.Length >= 4)
            {
                string conversationID = columns[0].Trim();
                string speakerID = columns[2].Trim();
                string lineID = columns[3].Trim();

                if (!conversationScript.ContainsKey(conversationID))
                    conversationScript.Add(conversationID, new List<DialogueLine>());

                DialogueLine dialogueLine = new DialogueLine
                {
                    speakerID = speakerID,
                    lineID = lineID
                };
                conversationScript[conversationID].Add(dialogueLine);
            }
        }
    }

    // --- 3. 'ChatSegments.csv' 로드 ---
    // 기대 컬럼: Conversation_ID,Room_ID,(Trigger),Day,State,Priority,Notify
    private void LoadChatSegmentsFile(string fileName)
    {
        chatSegmentsByDayState = new Dictionary<string, List<ChatSegmentDef>>();

        TextAsset csvFile = Resources.Load<TextAsset>(fileName);
        if (csvFile == null)
        {
            Debug.LogWarning(fileName + ".csv 파일을 Resources 폴더에서 찾을 수 없습니다! (채팅 세그먼트 비활성)");
            return;
        }

        string[] lines = csvFile.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            string row = lines[i].Trim();
            if (string.IsNullOrEmpty(row)) continue;

            string[] c = ParseCSVLine(row);
            if (c.Length < 6) continue;

            // 파일 포맷이 바뀌어도 어느 정도 버티도록 index를 방어적으로 처리
            string conversationId = c[0].Trim();
            string roomId = c.Length > 1 ? c[1].Trim() : "";
            int day = 1;
            string stateStr = "";
            int priority = 0;
            bool notify = true;

            // Day/State/priority/notify는 기존 샘플 기준으로 [3],[4],[5],[6]
            if (c.Length >= 5)
            {
                int.TryParse(c.Length > 3 ? c[3].Trim() : "1", out day);
                stateStr = c.Length > 4 ? c[4].Trim() : "";
            }
            if (c.Length > 5) int.TryParse(c[5].Trim(), out priority);
            if (c.Length > 6)
            {
                int n;
                if (int.TryParse(c[6].Trim(), out n)) notify = (n != 0);
            }

            GameState state;
            if (!Enum.TryParse(stateStr, out state))
            {
                // 혹시 Trigger 컬럼을 state로 잘못 넣었을 때 대비
                if (c.Length > 2 && Enum.TryParse(c[2].Trim(), out state))
                {
                    // ok
                }
                else
                {
                    Debug.LogWarning($"ChatSegments: GameState 파싱 실패: '{stateStr}' (Conversation_ID={conversationId})");
                    continue;
                }
            }

            string key = MakeDayStateKey(day, state);

            if (!chatSegmentsByDayState.ContainsKey(key))
                chatSegmentsByDayState[key] = new List<ChatSegmentDef>();

            chatSegmentsByDayState[key].Add(new ChatSegmentDef
            {
                conversationId = conversationId,
                roomId = roomId,
                day = day,
                state = state,
                priority = priority,
                notify = notify
            });
        }

        // (중요) 동일 conversationId가 같은 Day/State/Room에 중복으로 들어온 경우 자동 정리
        DedupChatSegments();
    }

    private void DedupChatSegments()
    {
        if (chatSegmentsByDayState == null) return;

        var keys = new List<string>(chatSegmentsByDayState.Keys);
        foreach (var key in keys)
        {
            var list = chatSegmentsByDayState[key];
            var map = new Dictionary<string, ChatSegmentDef>(); // segKey -> def

            foreach (var seg in list)
            {
                // 같은 방에서 같은 conversation을 여러 번 enqueue 방지
                string segKey = $"{seg.roomId}|{seg.conversationId}";
                if (!map.ContainsKey(segKey))
                {
                    map[segKey] = seg;
                }
                else
                {
                    // priority는 더 작은 걸, notify는 OR
                    var cur = map[segKey];
                    if (seg.priority < cur.priority) cur.priority = seg.priority;
                    cur.notify = cur.notify || seg.notify;
                    map[segKey] = cur;
                }
            }

            var deduped = new List<ChatSegmentDef>(map.Values);
            deduped.Sort((a, b) => a.priority.CompareTo(b.priority));
            chatSegmentsByDayState[key] = deduped;
        }
    }

    // --- 4. 'ChatLineMeta.csv' 로드 ---
    // 기대 컬럼: Conversation_ID,Order,MsgType,Image_ID,WaitTap,Delay
    private void LoadChatLineMetaFile(string fileName)
    {
        chatLineMetaByKey = new Dictionary<string, ChatLineMetaDef>();

        TextAsset csvFile = Resources.Load<TextAsset>(fileName);
        if (csvFile == null)
        {
            Debug.LogWarning(fileName + ".csv 파일을 Resources 폴더에서 찾을 수 없습니다! (채팅 이미지/메타 비활성)");
            return;
        }

        string[] lines = csvFile.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            string row = lines[i].Trim();
            if (string.IsNullOrEmpty(row)) continue;

            string[] c = ParseCSVLine(row);
            if (c.Length < 2) continue;

            string conversationId = c[0].Trim();
            int order = 0;
            int.TryParse(c[1].Trim(), out order);

            ChatMsgType msgType = ChatMsgType.TEXT;
            string msgTypeStr = c.Length > 2 ? c[2].Trim() : "TEXT";
            if (!Enum.TryParse(msgTypeStr, true, out msgType))
            {
                // 호환: TEXT_IMAGE 같은 값이 오면 BOTH로 처리
                if (string.Equals(msgTypeStr, "TEXT_IMAGE", StringComparison.OrdinalIgnoreCase))
                    msgType = ChatMsgType.BOTH;
            }

            string imageId = c.Length > 3 ? c[3].Trim() : "";
            bool waitTap = false;
            if (c.Length > 4)
            {
                int w;
                if (int.TryParse(c[4].Trim(), out w)) waitTap = (w != 0);
            }

            float delay = -1f;
            if (c.Length > 5)
            {
                float d;
                if (float.TryParse(c[5].Trim(), out d)) delay = d;
            }

            string key = MakeConvOrderKey(conversationId, order);
            chatLineMetaByKey[key] = new ChatLineMetaDef
            {
                conversationId = conversationId,
                order = order,
                msgType = msgType,
                imageId = imageId,
                waitTap = waitTap,
                delay = delay
            };
        }
    }

    private static string MakeDayStateKey(int day, GameState state) => day + "|" + state.ToString();
    private static string MakeConvOrderKey(string conversationId, int order) => conversationId + "|" + order;

    // --- 5. 언어 전환 ---
    public void SetLanguage(Language language)
    {
        currentLanguage = language;

        if (language == Language.Korean)
        {
            nameTable = nameTableKOR;
            lineTable = lineTableKOR;
        }
        else
        {
            nameTable = nameTableENG;
            lineTable = lineTableENG;
        }

        OnLanguageChanged?.Invoke(language);
        Debug.Log("언어가 변경되었습니다: " + language);
    }

    public Language GetCurrentLanguage() => currentLanguage;

    public void ToggleLanguage()
    {
        SetLanguage(currentLanguage == Language.Korean ? Language.English : Language.Korean);
    }

    // --- 6. '번역'과 '대화' 조회 함수 ---
    public string GetName(string speakerID)
    {
        if (nameTable != null && nameTable.ContainsKey(speakerID)) return nameTable[speakerID];
        return speakerID;
    }

    public string GetLine(string lineID)
    {
        if (lineTable != null && lineTable.ContainsKey(lineID)) return lineTable[lineID];
        Debug.LogWarning("Localization.csv 파일에 '" + lineID + "'가 없습니다!");
        return lineID;
    }

    public List<DialogueLine> GetConversation(string conversationID)
    {
        if (conversationScript != null && conversationScript.ContainsKey(conversationID))
            return conversationScript[conversationID];

        Debug.LogError("Conversations.csv 대화에서 '" + conversationID + "'를 찾을 수 없습니다!");
        return new List<DialogueLine>();
    }

    // --- 7. 채팅 API ---
    public List<ChatSegmentDef> GetChatSegments(int day, GameState state)
    {
        if (chatSegmentsByDayState == null) return new List<ChatSegmentDef>();
        string key = MakeDayStateKey(day, state);
        if (chatSegmentsByDayState.TryGetValue(key, out var list))
            return list;
        return new List<ChatSegmentDef>();
    }

    public bool TryGetChatLineMeta(string conversationId, int order, out ChatLineMetaDef meta)
    {
        meta = null;
        if (chatLineMetaByKey == null) return false;
        string key = MakeConvOrderKey(conversationId, order);
        return chatLineMetaByKey.TryGetValue(key, out meta);
    }

    public HashSet<string> GetAllChatRoomIds()
    {
        var set = new HashSet<string>();
        if (chatSegmentsByDayState == null) return set;

        foreach (var kv in chatSegmentsByDayState)
        {
            foreach (var seg in kv.Value)
            {
                if (!string.IsNullOrEmpty(seg.roomId))
                    set.Add(seg.roomId);
            }
        }
        return set;
    }
}
