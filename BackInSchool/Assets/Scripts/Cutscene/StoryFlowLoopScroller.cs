using UnityEngine;

/// <summary>
/// Enables/disables one or more ParallaxLoopScroller components only for a specific STORY flow.
/// Attach this to the story set root or any child in STORY scene.
/// </summary>
[DisallowMultipleComponent]
public class StoryFlowLoopScroller : MonoBehaviour
{
    [Header("Flow Match")]
    [Tooltip("Exact FLOW_ID match. Example: D1_AfterSchool_E")]
    [SerializeField] private string flowId;

    [Tooltip("Optional prefix match if exact flow is empty.")]
    [SerializeField] private string flowIdPrefix;

    [Header("Targets")]
    [SerializeField] private ParallaxLoopScroller[] scrollers;
    [SerializeField] private bool autoFindInChildren = true;

    private void Awake()
    {
        RebindTargetsIfNeeded();
        Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebindTargetsIfNeeded();
    }
#endif

    public void Apply()
    {
        RebindTargetsIfNeeded();

        bool shouldRun = MatchesCurrentFlow();
        if (scrollers == null)
            return;

        for (int i = 0; i < scrollers.Length; i++)
        {
            if (scrollers[i] == null)
                continue;

            scrollers[i].enabled = shouldRun;
        }
    }

    private void RebindTargetsIfNeeded()
    {
        if (!autoFindInChildren && scrollers != null && scrollers.Length > 0)
            return;

        scrollers = GetComponentsInChildren<ParallaxLoopScroller>(true);
    }

    private bool MatchesCurrentFlow()
    {
        string currentFlowId = PlayerPrefs.GetString("FLOW_ID", string.Empty);
        if (string.IsNullOrEmpty(currentFlowId))
            return false;

        if (!string.IsNullOrEmpty(flowId))
            return string.Equals(currentFlowId, flowId, System.StringComparison.Ordinal);

        if (!string.IsNullOrEmpty(flowIdPrefix))
            return currentFlowId.StartsWith(flowIdPrefix, System.StringComparison.Ordinal);

        return false;
    }
}
