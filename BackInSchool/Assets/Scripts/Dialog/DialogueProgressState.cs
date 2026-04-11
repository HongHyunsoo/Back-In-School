using UnityEngine;
using System.Collections.Generic;

public static class DialogueProgressState
{
    private const string CompletedPrefix = "DIALOGUE_COMPLETED_";
    private const string CompletedRegistryKey = "DIALOGUE_COMPLETED_REGISTRY";
    private const string ConversationsResourceName = "Conversations";

    public static bool HasCompletedConversation(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return false;

        string normalizedId = conversationId.Trim();
        bool completed = PlayerPrefs.GetInt(CompletedPrefix + normalizedId, 0) == 1;
        if (completed)
            RegisterConversation(normalizedId);

        return completed;
    }

    public static void MarkConversationCompleted(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return;

        string normalizedId = conversationId.Trim();
        PlayerPrefs.SetInt(CompletedPrefix + normalizedId, 1);
        RegisterConversation(normalizedId);
        PlayerPrefs.Save();
    }

    public static void ClearCompletedConversation(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return;

        string normalizedId = conversationId.Trim();
        PlayerPrefs.DeleteKey(CompletedPrefix + normalizedId);
        UnregisterConversation(normalizedId);
        PlayerPrefs.Save();
    }

    public static void ClearAllCompletedConversations()
    {
        HashSet<string> idsToClear = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        string registry = PlayerPrefs.GetString(CompletedRegistryKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(registry))
        {
            string[] ids = registry.Split('|');
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i].Trim();
                if (string.IsNullOrEmpty(id))
                    continue;

                idsToClear.Add(id);
            }
        }

        CollectConversationIdsFromResources(idsToClear);

        foreach (string id in idsToClear)
            PlayerPrefs.DeleteKey(CompletedPrefix + id);

        PlayerPrefs.DeleteKey(CompletedRegistryKey);
        PlayerPrefs.Save();
    }

    private static void RegisterConversation(string conversationId)
    {
        string[] existing = PlayerPrefs.GetString(CompletedRegistryKey, string.Empty).Split('|');
        for (int i = 0; i < existing.Length; i++)
        {
            if (string.Equals(existing[i], conversationId, System.StringComparison.OrdinalIgnoreCase))
                return;
        }

        string registry = PlayerPrefs.GetString(CompletedRegistryKey, string.Empty);
        if (string.IsNullOrWhiteSpace(registry))
            PlayerPrefs.SetString(CompletedRegistryKey, conversationId);
        else
            PlayerPrefs.SetString(CompletedRegistryKey, registry + "|" + conversationId);
    }

    private static void UnregisterConversation(string conversationId)
    {
        string registry = PlayerPrefs.GetString(CompletedRegistryKey, string.Empty);
        if (string.IsNullOrWhiteSpace(registry))
            return;

        string[] existing = registry.Split('|');
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < existing.Length; i++)
        {
            string id = existing[i].Trim();
            if (string.IsNullOrEmpty(id) ||
                string.Equals(id, conversationId, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (sb.Length > 0)
                sb.Append('|');

            sb.Append(id);
        }

        if (sb.Length == 0)
            PlayerPrefs.DeleteKey(CompletedRegistryKey);
        else
            PlayerPrefs.SetString(CompletedRegistryKey, sb.ToString());
    }

    private static void CollectConversationIdsFromResources(HashSet<string> ids)
    {
        if (ids == null)
            return;

        TextAsset csv = Resources.Load<TextAsset>(ConversationsResourceName);
        if (csv == null || string.IsNullOrWhiteSpace(csv.text))
            return;

        string[] lines = csv.text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string trimmed = line.Trim();
            if (trimmed.StartsWith("Conversation_ID") || trimmed.StartsWith(",,,"))
                continue;

            int commaIndex = trimmed.IndexOf(',');
            if (commaIndex <= 0)
                continue;

            string conversationId = trimmed.Substring(0, commaIndex).Trim();
            if (string.IsNullOrEmpty(conversationId))
                continue;

            ids.Add(conversationId);
        }
    }
}
