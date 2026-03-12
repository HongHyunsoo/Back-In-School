using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class StoryRevealOnLine : MonoBehaviour
{
    [SerializeField] private string targetFlowId = "DAY1_CLASSOPEN";
    [SerializeField] private string revealLineId = "DAY1_CLASSOPEN_05";
    [SerializeField] private bool includeChildren = false;

    private SpriteRenderer[] cachedRenderers;
    private string lastFlowId = string.Empty;
    private bool revealedThisFlow;

    private void Awake()
    {
        CacheRenderers();
        RefreshVisibility();
    }

    private void OnEnable()
    {
        DialogueManager.DialogueLineShown += HandleDialogueLineShown;
        RefreshVisibility();
    }

    private void OnDisable()
    {
        DialogueManager.DialogueLineShown -= HandleDialogueLineShown;
    }

    private void CacheRenderers()
    {
        cachedRenderers = includeChildren
            ? GetComponentsInChildren<SpriteRenderer>(true)
            : new[] { GetComponent<SpriteRenderer>() };
    }

    private void RefreshVisibility()
    {
        string flowId = PlayerPrefs.GetString("FLOW_ID", "");
        if (!string.Equals(lastFlowId, flowId, StringComparison.Ordinal))
        {
            lastFlowId = flowId;
            revealedThisFlow = false;
        }

        if (!IsTargetStoryFlow(flowId))
        {
            SetVisible(true);
            return;
        }

        var dm = FindAnyObjectByType<DialogueManager>();
        if (dm != null &&
            string.Equals(dm.CurrentConversationId, targetFlowId, StringComparison.Ordinal) &&
            string.Equals(dm.CurrentLineId, revealLineId, StringComparison.Ordinal))
        {
            revealedThisFlow = true;
        }

        SetVisible(revealedThisFlow);
    }

    private void HandleDialogueLineShown(string conversationId, string lineId)
    {
        string flowId = PlayerPrefs.GetString("FLOW_ID", "");
        if (!IsTargetStoryFlow(flowId))
        {
            SetVisible(true);
            return;
        }

        if (string.Equals(conversationId, targetFlowId, StringComparison.Ordinal) &&
            string.Equals(lineId, revealLineId, StringComparison.Ordinal))
        {
            revealedThisFlow = true;
            SetVisible(true);
            return;
        }

        if (!revealedThisFlow)
            SetVisible(false);
    }

    private bool IsTargetStoryFlow(string flowId)
    {
        if (SceneManager.GetActiveScene().name != "STORY")
            return false;

        if (!string.Equals(PlayerPrefs.GetString("FLOW_TYPE", ""), "STORY", StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(flowId, targetFlowId, StringComparison.Ordinal);
    }

    private void SetVisible(bool visible)
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
            CacheRenderers();

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = visible;
        }
    }
}
