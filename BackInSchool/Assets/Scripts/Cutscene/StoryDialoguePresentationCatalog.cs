using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class StoryDialoguePresentationCatalog : MonoBehaviour
{
    [Serializable]
    public class ConversationPresentationBinding
    {
        [Tooltip("Exact conversation ID / FLOW_ID used in STORY scene.")]
        public string conversationId;

        [Tooltip("Per-line animation/sound overrides for this STORY conversation.")]
        public List<DialogueLinePresentation> linePresentations = new List<DialogueLinePresentation>();
    }

    [SerializeField] private List<ConversationPresentationBinding> bindings = new List<ConversationPresentationBinding>();

    public List<DialogueLinePresentation> GetPresentations(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || bindings == null)
            return null;

        for (int i = 0; i < bindings.Count; i++)
        {
            ConversationPresentationBinding binding = bindings[i];
            if (binding == null || string.IsNullOrWhiteSpace(binding.conversationId))
                continue;

            if (string.Equals(binding.conversationId, conversationId, StringComparison.OrdinalIgnoreCase))
                return binding.linePresentations;
        }

        return null;
    }
}
