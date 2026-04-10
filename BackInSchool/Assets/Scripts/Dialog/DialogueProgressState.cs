using UnityEngine;

public static class DialogueProgressState
{
    private const string CompletedPrefix = "DIALOGUE_COMPLETED_";

    public static bool HasCompletedConversation(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return false;

        return PlayerPrefs.GetInt(CompletedPrefix + conversationId, 0) == 1;
    }

    public static void MarkConversationCompleted(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return;

        PlayerPrefs.SetInt(CompletedPrefix + conversationId, 1);
    }
}
