using UnityEngine;

/// <summary>
/// Activates one map root for FREEROAM by FLOW_ID context.
/// - Morning: contains "BEFORE_ASSEMBLY" (or empty)
/// - Lunch: contains "LUNCH"
/// - AfterSchool: contains "AFTERSCHOOL"
/// </summary>
public class FreeTimeMapVariantSwitcher : MonoBehaviour
{
    [Header("Map Roots")]
    [SerializeField] private GameObject morningMapRoot;
    [SerializeField] private GameObject lunchMapRoot;
    [SerializeField] private GameObject afterSchoolMapRoot;
    [SerializeField] private GameObject fallbackMapRoot;

    [Header("Options")]
    [SerializeField] private bool requireFreeRoamType = true;
    [SerializeField] private bool useFallbackWhenTypeMismatch = false;
    [SerializeField] private bool autoRefresh = true;

    private string lastFlowType;
    private string lastFlowId;

    private void OnEnable()
    {
        RefreshNow();
    }

    private void Update()
    {
        if (!autoRefresh)
            return;

        string flowType = FlowContext.CurrentType;
        string flowId = FlowContext.CurrentId;
        if (flowType == lastFlowType && flowId == lastFlowId)
            return;

        RefreshNow();
    }

    [ContextMenu("Refresh FreeTime Map")]
    public void RefreshNow()
    {
        string flowType = FlowContext.CurrentType;
        string flowId = FlowContext.CurrentId;

        lastFlowType = flowType;
        lastFlowId = flowId;

        if (requireFreeRoamType && !FlowContext.IsFreeRoam())
        {
            if (useFallbackWhenTypeMismatch)
                SetActiveOnly(fallbackMapRoot);
            return;
        }

        bool isLunch = FlowContext.IsLunchFreeRoam();
        bool isAfterSchool = FlowContext.IsAfterSchoolFreeRoam();
        bool isMorning = FlowContext.IsMorningBeforeAssemblyFreeRoam() || (!isLunch && !isAfterSchool);

        if (isLunch)
            SetActiveOnly(lunchMapRoot);
        else if (isAfterSchool)
            SetActiveOnly(afterSchoolMapRoot);
        else if (isMorning)
            SetActiveOnly(morningMapRoot);
        else
            SetActiveOnly(fallbackMapRoot);
    }

    private void SetActiveOnly(GameObject target)
    {
        SetRootActive(morningMapRoot, target == morningMapRoot);
        SetRootActive(lunchMapRoot, target == lunchMapRoot);
        SetRootActive(afterSchoolMapRoot, target == afterSchoolMapRoot);

        bool anyMapped = target == morningMapRoot || target == lunchMapRoot || target == afterSchoolMapRoot;
        SetRootActive(fallbackMapRoot, target == fallbackMapRoot || (!anyMapped && fallbackMapRoot != null));
    }

    private static void SetRootActive(GameObject go, bool active)
    {
        if (go == null)
            return;
        if (go.activeSelf != active)
            go.SetActive(active);
    }
}
