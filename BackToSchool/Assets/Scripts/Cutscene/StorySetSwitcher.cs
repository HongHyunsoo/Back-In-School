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

    [Header("Sets")]
    [SerializeField] private StorySetBinding[] bindings;
    [SerializeField] private GameObject defaultSetRoot;

    [Header("Options")]
    [SerializeField] private bool runOnlyInStoryFlow = true;
    [SerializeField] private bool rebindDialogueAfterSwitch = true;
    [SerializeField] private bool useSetTagLookup = true;

    private readonly Dictionary<string, GameObject> setById = new Dictionary<string, GameObject>();

    private void Awake()
    {
        RebuildSetLookup();
        ApplySet();
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
}
