using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class StorySetSwitcher : MonoBehaviour
{
    [Serializable]
    public class StorySetBinding
    {
        [Tooltip("Exact FLOW_ID match. Example: D1_ASSEMBLY_MOVE")]
        public string flowId;

        [Tooltip("Optional prefix match if exact flowId is empty. Example: D1_ASSEMBLY_")]
        public string flowIdPrefix;

        [Tooltip("Root object that contains background + character placements for this set")]
        public GameObject setRoot;
    }

    [Serializable]
    public class CharacterRevealBinding
    {
        public string characterId;
        public string targetFlowId;
        public string revealLineId;
        public string[] hiddenFlowIds;
        public AudioClip revealSfx;
        [Range(0f, 1f)] public float revealSfxVolume = 0.4f;
    }

    [Header("Sets")]
    [SerializeField] private StorySetBinding[] bindings;
    [SerializeField] private GameObject defaultSetRoot;

    [Header("Options")]
    [SerializeField] private bool runOnlyInStoryFlow = true;
    [SerializeField] private bool rebindDialogueAfterSwitch = true;
    [SerializeField] private bool useSetTagLookup = true;

    [Header("Character Reveals")]
    [SerializeField] private CharacterRevealBinding[] characterRevealBindings;

    private readonly Dictionary<string, GameObject> setById = new Dictionary<string, GameObject>();
    private readonly Dictionary<CharacterRevealBinding, bool> revealTriggeredByBinding = new Dictionary<CharacterRevealBinding, bool>();
    private readonly Dictionary<CharacterRevealBinding, bool> revealSfxPlayedByBinding = new Dictionary<CharacterRevealBinding, bool>();
    private AudioSource audioSource;

    private void Awake()
    {
        RebuildSetLookup();
        EnsureAudioSource();
        ApplySet();
        RefreshCharacterReveals();
    }

    private void OnEnable()
    {
        DialogueManager.DialogueLineShown += HandleDialogueLineShown;
        RefreshCharacterReveals();
    }

    private void OnDisable()
    {
        DialogueManager.DialogueLineShown -= HandleDialogueLineShown;
    }

    private void Update()
    {
        RefreshCharacterReveals();
    }

    public void ApplySet()
    {
        if (runOnlyInStoryFlow && PlayerPrefs.GetString("FLOW_TYPE", "") != "STORY")
            return;

        string flowId = PlayerPrefs.GetString("FLOW_ID", "");
        GameObject target = ResolveTarget(flowId);

        if (bindings != null)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b == null || b.setRoot == null)
                    continue;

                b.setRoot.SetActive(b.setRoot == target);
            }
        }

        if (defaultSetRoot != null)
            defaultSetRoot.SetActive(target == defaultSetRoot);

        if (rebindDialogueAfterSwitch)
        {
            var dm = FindAnyObjectByType<DialogueManager>();
            if (dm != null)
                dm.RebindForScene();
        }

        RefreshCharacterReveals();
    }

    public bool ApplySetById(string setId)
    {
        if (string.IsNullOrEmpty(setId))
            return false;

        if (setById.Count == 0)
            RebuildSetLookup();

        if (!setById.TryGetValue(setId, out var target) || target == null)
            return false;

        if (bindings != null)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b == null || b.setRoot == null)
                    continue;
                b.setRoot.SetActive(b.setRoot == target);
            }
        }

        if (defaultSetRoot != null)
            defaultSetRoot.SetActive(target == defaultSetRoot);

        // Also enforce tag-registered sets.
        foreach (var kv in setById)
        {
            if (kv.Value == null)
                continue;
            kv.Value.SetActive(kv.Value == target);
        }

        if (rebindDialogueAfterSwitch)
        {
            var dm = FindAnyObjectByType<DialogueManager>();
            if (dm != null)
                dm.RebindForScene();
        }

        RefreshCharacterReveals();

        return true;
    }

    private GameObject ResolveTarget(string flowId)
    {
        if (bindings != null)
        {
            // 1) Exact match first
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b == null || b.setRoot == null || string.IsNullOrEmpty(b.flowId))
                    continue;

                if (string.Equals(b.flowId, flowId, StringComparison.Ordinal))
                    return b.setRoot;
            }

            // 2) Prefix match fallback
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b == null || b.setRoot == null || string.IsNullOrEmpty(b.flowIdPrefix))
                    continue;

                if (!string.IsNullOrEmpty(flowId) && flowId.StartsWith(b.flowIdPrefix, StringComparison.Ordinal))
                    return b.setRoot;
            }
        }

        return defaultSetRoot;
    }

    private void RebuildSetLookup()
    {
        setById.Clear();

        if (bindings != null)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b == null || b.setRoot == null || string.IsNullOrEmpty(b.flowId))
                    continue;

                if (!setById.ContainsKey(b.flowId))
                    setById.Add(b.flowId, b.setRoot);
            }
        }

        if (!useSetTagLookup)
            return;

        var tags = FindObjectsOfType<StorySetTag>(true);
        for (int i = 0; i < tags.Length; i++)
        {
            var tag = tags[i];
            if (tag == null || string.IsNullOrEmpty(tag.SetId))
                continue;

            if (!setById.ContainsKey(tag.SetId))
                setById.Add(tag.SetId, tag.gameObject);
        }
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

    private void HandleDialogueLineShown(string conversationId, string lineId)
    {
        if (characterRevealBindings == null || characterRevealBindings.Length == 0)
            return;

        for (int i = 0; i < characterRevealBindings.Length; i++)
        {
            var binding = characterRevealBindings[i];
            if (binding == null)
                continue;

            if (!string.Equals(conversationId, binding.targetFlowId, StringComparison.Ordinal))
                continue;

            if (!string.Equals(lineId, binding.revealLineId, StringComparison.Ordinal))
                continue;

            revealTriggeredByBinding[binding] = true;
            ApplyRevealVisibility(binding, true);
            PlayRevealSfx(binding);
        }
    }

    private void RefreshCharacterReveals()
    {
        if (characterRevealBindings == null || characterRevealBindings.Length == 0)
            return;

        string flowId = FlowContext.CurrentId;
        string flowType = FlowContext.CurrentType;
        bool isStory = string.Equals(flowType, "STORY", StringComparison.OrdinalIgnoreCase);

        var dm = FindAnyObjectByType<DialogueManager>();
        string currentConversationId = dm != null ? dm.CurrentConversationId : string.Empty;
        string currentLineId = dm != null ? dm.CurrentLineId : string.Empty;

        for (int i = 0; i < characterRevealBindings.Length; i++)
        {
            var binding = characterRevealBindings[i];
            if (binding == null || string.IsNullOrWhiteSpace(binding.characterId))
                continue;

            if (!revealTriggeredByBinding.ContainsKey(binding))
                revealTriggeredByBinding[binding] = false;
            if (!revealSfxPlayedByBinding.ContainsKey(binding))
                revealSfxPlayedByBinding[binding] = false;

            bool isTargetConversation = dm != null &&
                string.Equals(currentConversationId, binding.targetFlowId, StringComparison.Ordinal);
            bool isTargetFlow = isStory &&
                (MatchesTargetFlowOrPrelude(flowId, binding.targetFlowId) || isTargetConversation);
            bool isHiddenFlow = MatchesAnyFlow(flowId, binding.hiddenFlowIds);

            if (!isTargetFlow)
            {
                revealTriggeredByBinding[binding] = false;
                revealSfxPlayedByBinding[binding] = false;

                if (isHiddenFlow)
                    ApplyRevealVisibility(binding, false);

                continue;
            }

            bool shouldReveal = revealTriggeredByBinding[binding];
            if (!shouldReveal &&
                string.Equals(currentConversationId, binding.targetFlowId, StringComparison.Ordinal) &&
                string.Equals(currentLineId, binding.revealLineId, StringComparison.Ordinal))
            {
                shouldReveal = true;
                revealTriggeredByBinding[binding] = true;
                PlayRevealSfx(binding);
            }

            ApplyRevealVisibility(binding, shouldReveal);
        }
    }

    private static bool MatchesAnyFlow(string flowId, string[] hiddenFlowIds)
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

    private static bool MatchesTargetFlowOrPrelude(string flowId, string targetFlowId)
    {
        if (string.IsNullOrWhiteSpace(flowId) || string.IsNullOrWhiteSpace(targetFlowId))
            return false;

        return string.Equals(flowId, targetFlowId, StringComparison.Ordinal) ||
               string.Equals(flowId, targetFlowId + "_NS", StringComparison.Ordinal) ||
               string.Equals(flowId, targetFlowId + "_NO_SLIPPERS", StringComparison.Ordinal);
    }

    private static void ApplyRevealVisibility(CharacterRevealBinding binding, bool visible)
    {
        var actors = FindObjectsByType<CharacterActor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < actors.Length; i++)
        {
            CharacterActor actor = actors[i];
            if (actor == null || !string.Equals(actor.characterId, binding.characterId, StringComparison.Ordinal))
                continue;

            if (visible && !IsParentHierarchyActive(actor.transform))
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

    private static bool IsParentHierarchyActive(Transform transform)
    {
        if (transform == null)
            return false;

        Transform parent = transform.parent;
        while (parent != null)
        {
            if (!parent.gameObject.activeSelf)
                return false;

            parent = parent.parent;
        }

        return true;
    }

    private void PlayRevealSfx(CharacterRevealBinding binding)
    {
        if (binding == null || binding.revealSfx == null)
            return;

        if (revealSfxPlayedByBinding.TryGetValue(binding, out bool played) && played)
            return;

        EnsureAudioSource();
        var dialogueManager = FindAnyObjectByType<DialogueManager>();
        if (dialogueManager != null)
            dialogueManager.MuteTypingSfxForSeconds(Mathf.Max(0.05f, binding.revealSfx.length));

        audioSource.PlayOneShot(binding.revealSfx, AudioSettingsService.ScaleSfx(binding.revealSfxVolume));
        revealSfxPlayedByBinding[binding] = true;
    }

}
