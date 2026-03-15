using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ChatConversationTriggerCatalog
{
    public sealed class Entry
    {
        public string TriggerConversationId;
        public string ConversationId;
        public string RoomId;
        public string FlowIdContains;
        public int Day;
        public GameState State;
        public bool Notify;
    }

    private static ChatConversationTriggerCatalog instance;

    private readonly Dictionary<string, List<Entry>> byTriggerConversation = new();
    private readonly HashSet<string> roomIds = new();

    public static ChatConversationTriggerCatalog Instance => instance ??= LoadFromResources();

    public IReadOnlyCollection<string> GetAllRoomIds() => roomIds;

    public List<Entry> GetEntries(string triggerConversationId, int day, GameState state, string activeFlowId)
    {
        if (string.IsNullOrEmpty(triggerConversationId))
            return new List<Entry>();

        if (!byTriggerConversation.TryGetValue(triggerConversationId, out var list))
            return new List<Entry>();

        var result = new List<Entry>();
        for (int i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            if (entry.Day > 0 && entry.Day != day)
                continue;
            if (entry.State != state)
                continue;
            if (!MatchesFlow(entry.FlowIdContains, activeFlowId))
                continue;

            result.Add(entry);
        }

        return result;
    }

    private static ChatConversationTriggerCatalog LoadFromResources()
    {
        var catalog = new ChatConversationTriggerCatalog();

        TextAsset csv = Resources.Load<TextAsset>("ChatConversationTriggers");
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

            string triggerConversationId = SafeGet(c, 0);
            string conversationId = SafeGet(c, 1);
            string roomId = SafeGet(c, 2);
            int day = ParseIntOrDefault(SafeGet(c, 3), 0);
            string stateRaw = SafeGet(c, 4);
            string flowIdContains = SafeGet(c, 5);
            bool notify = ParseBoolIntOrDefault(SafeGet(c, 6), true);

            if (string.IsNullOrEmpty(triggerConversationId) ||
                string.IsNullOrEmpty(conversationId) ||
                string.IsNullOrEmpty(roomId))
            {
                continue;
            }

            if (!TryParseState(stateRaw, out GameState state))
                continue;

            catalog.roomIds.Add(roomId);

            if (!catalog.byTriggerConversation.TryGetValue(triggerConversationId, out var list))
            {
                list = new List<Entry>();
                catalog.byTriggerConversation.Add(triggerConversationId, list);
            }

            bool exists = false;
            for (int j = 0; j < list.Count; j++)
            {
                var existing = list[j];
                if (!string.Equals(existing.ConversationId, conversationId, StringComparison.Ordinal) ||
                    !string.Equals(existing.RoomId, roomId, StringComparison.Ordinal) ||
                    existing.Day != day ||
                    existing.State != state ||
                    !string.Equals(existing.FlowIdContains, flowIdContains, StringComparison.Ordinal))
                {
                    continue;
                }

                existing.Notify |= notify;
                exists = true;
                break;
            }

            if (exists)
                continue;

            list.Add(new Entry
            {
                TriggerConversationId = triggerConversationId,
                ConversationId = conversationId,
                RoomId = roomId,
                Day = day,
                State = state,
                FlowIdContains = flowIdContains,
                Notify = notify
            });
        }

        return catalog;
    }

    private static bool MatchesFlow(string filter, string activeFlowId)
    {
        if (string.IsNullOrEmpty(filter))
            return true;

        if (string.IsNullOrEmpty(activeFlowId))
            return false;

        return activeFlowId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool TryParseState(string raw, out GameState state)
    {
        if (Enum.TryParse(raw, true, out state))
            return true;

        state = default;
        return false;
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

    private static string SafeGet(string[] c, int index)
    {
        if (c == null || index < 0 || index >= c.Length)
            return string.Empty;

        return c[index].Trim();
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
