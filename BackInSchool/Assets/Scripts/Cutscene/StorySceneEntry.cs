using System.Collections;
using UnityEngine;

public class StorySceneEntry : MonoBehaviour
{
    private string temporaryConversationId = string.Empty;
    private StoryDialoguePresentationCatalog presentationCatalog;

    private void OnDisable()
    {
        DialogueManager.DialogueConversationCompleted -= HandleTemporaryConversationCompleted;
    }

    private void Start()
    {
        presentationCatalog = GetComponent<StoryDialoguePresentationCatalog>();

        if (TryStartTemporaryStoryConversation())
            return;

        if (PlayerPrefs.GetString("FLOW_TYPE", "") != "STORY")
            return;

        string convoId = PlayerPrefs.GetString("FLOW_ID", "");
        if (string.IsNullOrEmpty(convoId))
        {
            Debug.LogWarning("[StorySceneEntry] STORY but FLOW_ID is empty, auto-skipping.");
            FlowManager.Instance?.CompleteCurrentEvent(0);
            return;
        }

        var dm = FindAnyObjectByType<DialogueManager>();
        if (dm == null)
        {
            Debug.LogError("[StorySceneEntry] DialogueManager missing, auto-skipping.");
            FlowManager.Instance?.CompleteCurrentEvent(0);
            return;
        }

        var convo = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetConversation(convoId)
            : null;

        if (convo == null || convo.Count == 0)
        {
            Debug.LogWarning($"[StorySceneEntry] Conversation '{convoId}' missing, auto-skipping.");
            FlowManager.Instance?.CompleteCurrentEvent(0);
            return;
        }

        ApplyStoryPresentations(dm, convoId);
        dm.StartDialogue(convoId, null);
    }

    private bool TryStartTemporaryStoryConversation()
    {
        if (!TemporaryStorySceneFlow.HasPendingStory())
            return false;

        temporaryConversationId = TemporaryStorySceneFlow.GetPendingConversationId();
        if (string.IsNullOrEmpty(temporaryConversationId))
        {
            TemporaryStorySceneFlow.ReturnToStoredScene();
            return true;
        }

        var dm = FindAnyObjectByType<DialogueManager>();
        if (dm == null)
        {
            Debug.LogError("[StorySceneEntry] Temporary story requested but DialogueManager is missing.");
            TemporaryStorySceneFlow.ReturnToStoredScene();
            return true;
        }

        var convo = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetConversation(temporaryConversationId)
            : null;
        if (convo == null || convo.Count == 0)
        {
            Debug.LogWarning($"[StorySceneEntry] Temporary story '{temporaryConversationId}' is missing.");
            TemporaryStorySceneFlow.ReturnToStoredScene();
            return true;
        }

        DialogueManager.DialogueConversationCompleted -= HandleTemporaryConversationCompleted;
        DialogueManager.DialogueConversationCompleted += HandleTemporaryConversationCompleted;
        ApplyStoryPresentations(dm, temporaryConversationId);
        dm.StartDialogue(temporaryConversationId, null);
        return true;
    }

    private void ApplyStoryPresentations(DialogueManager dm, string conversationId)
    {
        if (dm == null || presentationCatalog == null)
            return;

        dm.SetUpcomingLinePresentations(presentationCatalog.GetPresentations(conversationId));
    }

    private void HandleTemporaryConversationCompleted(string conversationId)
    {
        if (!string.Equals(conversationId, temporaryConversationId, System.StringComparison.OrdinalIgnoreCase))
            return;

        DialogueManager.DialogueConversationCompleted -= HandleTemporaryConversationCompleted;
        StartCoroutine(CoReturnFromTemporaryStory());
    }

    private IEnumerator CoReturnFromTemporaryStory()
    {
        yield return null;
        TemporaryStorySceneFlow.ReturnToStoredScene();
    }
}
