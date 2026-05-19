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

        ApplyPreDialogueVisibilityOverrides(convoId);
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
        ApplyPreDialogueVisibilityOverrides(temporaryConversationId);
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

    private void ApplyPreDialogueVisibilityOverrides(string conversationId)
    {
        if (string.Equals(conversationId, "DAY1_CLASSOPEN", System.StringComparison.Ordinal) ||
            string.Equals(conversationId, "DAY1_CLASSEND", System.StringComparison.Ordinal))
        {
            SetCharacterVisible("NAME_NUREONG", false);
        }
    }

    private static void SetCharacterVisible(string characterId, bool visible)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return;

        var actors = FindObjectsByType<CharacterActor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < actors.Length; i++)
        {
            CharacterActor actor = actors[i];
            if (actor == null || !string.Equals(actor.characterId, characterId, System.StringComparison.Ordinal))
                continue;

            actor.gameObject.SetActive(visible);

            var renderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                if (renderers[rendererIndex] != null)
                    renderers[rendererIndex].enabled = visible;
            }
        }
    }

    private IEnumerator CoReturnFromTemporaryStory()
    {
        yield return null;
        TemporaryStorySceneFlow.ReturnToStoredScene();
    }
}
