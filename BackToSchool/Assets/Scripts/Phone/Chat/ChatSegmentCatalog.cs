using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ChatSegmentCatalog
{
    public sealed class Entry
    {
        public string ConversationId;
        public string RoomId;
        public string FlowIdContains;
        public int Day;
        public GameState State;
        public int Priority;
        public bool Notify;
    }

    private static ChatSegmentCatalog instance;

    private readonly Dictionary<string, List<Entry>> byDayState = new();
    private readonly HashSet<string> roomIds = new();

    public static ChatSegmentCatalog Instance => instance ??= LoadFromResources();

    public IReadOnlyCollection<string> GetAllRoomIds() => roomIds;

    public List<Entry> GetSegments(int day, GameState state, string activeFlowId)
    {
        string key = MakeKey(day, state);
        if (!byDayState.TryGetValue(key, out var list))
            return new List<Entry>();

        var result = new List<Entry>();
        for (int i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            if (!MatchesFlow(entry, activeFlowId))
                continue;
            result.Add(entry);
        }

        return result;
    }

    private static ChatSegmentCatalog LoadFromResources()
    {
        var catalog = new ChatSegmentCatalog();

        TextAsset csv = Resources.Load<TextAsset>("ChatSegments");
        if (csv == null)
            return catalog;

        string[] rows = csv.text.Split('\n');
        for (int i = 1; i < rows.Length; i++)
        {
            string row = rows[i].Trim();
            if (string.IsNullOrEmpty(row))
                continue;

            string[] c = ParseCsvLine(row);
            if (c.Length < 6)
                continue;

            string conversationId = SafeGet(c, 0);
            string roomId = SafeGet(c, 1);
            string flowIdContains = string.Empty;
            int day;
            string stateRaw;
            int priority = 0;
            bool notify = true;

            if (c.Length >= 7)
            {
                flowIdContains = SafeGet(c, 2);
                day = ParseIntOrDefault(SafeGet(c, 3), 1);
                stateRaw = SafeGet(c, 4);
                priority = ParseIntOrDefault(SafeGet(c, 5), 0);
                notify = ParseBoolIntOrDefault(SafeGet(c, 6), true);
            }
            else
            {
                day = ParseIntOrDefault(SafeGet(c, 2), 1);
                stateRaw = SafeGet(c, 3);
                priority = ParseIntOrDefault(SafeGet(c, 4), 0);
                notify = ParseBoolIntOrDefault(SafeGet(c, 5), true);
            }

            if (string.IsNullOrEmpty(conversationId) || string.IsNullOrEmpty(roomId))
                continue;

            if (!TryParseState(stateRaw, out GameState state))
                continue;

            if (c.Length < 7)
                flowIdContains = InferLegacyFlowFilter(stateRaw, conversationId);

            catalog.roomIds.Add(roomId);

            string key = MakeKey(day, state);
            if (!catalog.byDayState.TryGetValue(key, out var list))
            {
                list = new List<Entry>();
                catalog.byDayState.Add(key, list);
            }

            string dedupKey = roomId + "|" + conversationId + "|" + flowIdContains;
            bool merged = false;
            for (int j = 0; j < list.Count; j++)
            {
                var existing = list[j];
                string existingKey = existing.RoomId + "|" + existing.ConversationId + "|" + existing.FlowIdContains;
                if (!string.Equals(existingKey, dedupKey, StringComparison.Ordinal))
                    continue;

                if (priority < existing.Priority)
                    existing.Priority = priority;
                existing.Notify = existing.Notify || notify;
                merged = true;
                break;
            }

            if (merged)
                continue;

            list.Add(new Entry
            {
                ConversationId = conversationId,
                RoomId = roomId,
                FlowIdContains = flowIdContains,
                Day = day,
                State = state,
                Priority = priority,
                Notify = notify
            });
        }

        foreach (var pair in catalog.byDayState)
            pair.Value.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        return catalog;
    }

    private static bool MatchesFlow(Entry entry, string activeFlowId)
    {
        if (entry == null)
            return false;

        if (string.IsNullOrEmpty(entry.FlowIdContains))
            return true;

        if (string.IsNullOrEmpty(activeFlowId))
            return false;

        return activeFlowId.IndexOf(entry.FlowIdContains, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string MakeKey(int day, GameState state) => day + "|" + state;

    private static bool TryParseState(string raw, out GameState state)
    {
        if (Enum.TryParse(raw, true, out state))
            return true;

        string normalized = (raw ?? string.Empty).Trim().ToUpperInvariant();
        switch (normalized)
        {
            case "GO HOME":
            case "GOHOME":
            case "HOME":
                state = GameState.Subway;
                return true;
        }

        state = default;
        return false;
    }

    private static string InferLegacyFlowFilter(string stateRaw, string conversationId)
    {
        string normalized = (stateRaw ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized == "GO HOME" || normalized == "GOHOME" || normalized == "HOME")
            return "CHAT_TO_HOME";

        if (normalized == "SUBWAY")
            return "CHAT_TO_SCHOOL";

        if (!string.IsNullOrEmpty(conversationId) &&
            conversationId.EndsWith("_N", StringComparison.OrdinalIgnoreCase))
            return "CHAT_TO_HOME";

        return string.Empty;
    }

    private static string SafeGet(string[] c, int index)
    {
        if (c == null || index < 0 || index >= c.Length)
            return string.Empty;
        return c[index].Trim();
    }

    private static int ParseIntOrDefault(string raw, int fallback)
    {
        return int.TryParse(raw, out int value) ? value : fallback;
    }

    private static bool ParseBoolIntOrDefault(string raw, bool fallback)
    {
        if (int.TryParse(raw, out int numeric))
            return numeric != 0;
        if (bool.TryParse(raw, out bool parsed))
            return parsed;
        return fallback;
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        string current = string.Empty;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(current);
                current = string.Empty;
                continue;
            }

            current += ch;
        }

        result.Add(current);
        return result.ToArray();
    }
}
