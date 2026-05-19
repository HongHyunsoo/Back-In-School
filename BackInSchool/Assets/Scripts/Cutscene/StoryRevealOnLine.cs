using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class StoryRevealOnLine : MonoBehaviour
{
    [SerializeField] private string targetFlowId = "DAY1_CLASSOPEN";
    [SerializeField] private string revealLineId = "DAY1_CLASSOPEN_05";
    [SerializeField] private bool includeChildren = false;
    [SerializeField] private string[] hiddenFlowIds = Array.Empty<string>();
    [SerializeField] private AudioClip revealSfx;
    [SerializeField] [Range(0f, 1f)] private float revealSfxVolume = 0.4f;

    private SpriteRenderer[] cachedRenderers;
    private string lastFlowId = string.Empty;
    private bool revealedThisFlow;
    private bool playedRevealSfxThisFlow;
    private AudioSource audioSource;
    private string linkedCharacterId = string.Empty;

    private void Awake()
    {
        CacheRenderers();
        EnsureAudioSource();
        RefreshVisibility();
    }

    private void Start()
    {
        RefreshVisibility();
    }

    private void Update()
    {
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

        var actor = GetComponent<CharacterActor>();
        linkedCharacterId = actor != null ? actor.characterId ?? string.Empty : string.Empty;
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
            return;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    private void RefreshVisibility()
    {
        string flowId = FlowContext.CurrentId;
        if (!string.Equals(lastFlowId, flowId, StringComparison.Ordinal))
        {
            lastFlowId = flowId;
            revealedThisFlow = false;
            playedRevealSfxThisFlow = false;
        }

        if (!IsTargetStoryFlow(flowId))
        {
            if (ShouldForceHide(flowId))
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            return;
        }

        var dm = FindAnyObjectByType<DialogueManager>();
        if (dm != null &&
            string.Equals(dm.CurrentConversationId, targetFlowId, StringComparison.Ordinal) &&
            string.Equals(dm.CurrentLineId, revealLineId, StringComparison.Ordinal))
        {
            RevealNow();
            return;
        }

        SetVisible(revealedThisFlow);
    }

    private void HandleDialogueLineShown(string conversationId, string lineId)
    {
        string flowId = FlowContext.CurrentId;
        if (!IsTargetStoryFlow(flowId))
        {
            if (ShouldForceHide(flowId))
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            return;
        }

        if (string.Equals(conversationId, targetFlowId, StringComparison.Ordinal) &&
            string.Equals(lineId, revealLineId, StringComparison.Ordinal))
        {
            RevealNow();
            return;
        }

        if (!revealedThisFlow)
            SetVisible(false);
    }

    private bool IsTargetStoryFlow(string flowId)
    {
        if (!string.Equals(SceneManager.GetActiveScene().name, "STORY", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(FlowContext.CurrentType, "STORY", StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(flowId, targetFlowId, StringComparison.Ordinal);
    }

    private bool ShouldForceHide(string flowId)
    {
        if (hiddenFlowIds == null || hiddenFlowIds.Length == 0)
            return false;

        for (int i = 0; i < hiddenFlowIds.Length; i++)
        {
            string hiddenFlowId = hiddenFlowIds[i];
            if (!string.IsNullOrWhiteSpace(hiddenFlowId) &&
                string.Equals(flowId, hiddenFlowId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void RevealNow()
    {
        revealedThisFlow = true;
        SetVisible(true);
        PlayRevealSfxOnce();
    }

    private void PlayRevealSfxOnce()
    {
        if (playedRevealSfxThisFlow || revealSfx == null)
            return;

        EnsureAudioSource();
        audioSource.PlayOneShot(revealSfx, revealSfxVolume);
        playedRevealSfxThisFlow = true;
    }

    private void SetVisible(bool visible)
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
            CacheRenderers();

        if (!string.IsNullOrWhiteSpace(linkedCharacterId))
        {
            var actors = FindObjectsByType<CharacterActor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool anyMatched = false;
            for (int actorIndex = 0; actorIndex < actors.Length; actorIndex++)
            {
                CharacterActor actor = actors[actorIndex];
                if (actor == null || !string.Equals(actor.characterId, linkedCharacterId, StringComparison.Ordinal))
                    continue;

                anyMatched = true;
                actor.gameObject.SetActive(visible);
                var renderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                        renderers[i].enabled = visible;
                }
            }

            if (anyMatched)
                return;
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = visible;
        }
    }
}
