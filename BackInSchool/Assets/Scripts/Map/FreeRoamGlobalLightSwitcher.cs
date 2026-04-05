using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class FreeRoamGlobalLightSwitcher : MonoBehaviour
{
    [Serializable]
    public class LightPreset
    {
        public Color color = Color.white;
        [Min(0f)] public float intensity = 1f;
    }

    [Header("Target")]
    [SerializeField] private Light2D targetGlobalLight;

    [Header("Presets")]
    [SerializeField] private LightPreset morningPreset = new LightPreset
    {
        color = new Color(0.82f, 0.78f, 0.88f, 1f),
        intensity = 0.6f
    };
    [SerializeField] private LightPreset lunchPreset = new LightPreset
    {
        color = new Color(1f, 0.98f, 0.9f, 1f),
        intensity = 0.85f
    };
    [SerializeField] private LightPreset afterSchoolPreset = new LightPreset
    {
        color = new Color(1f, 0.78f, 0.58f, 1f),
        intensity = 0.72f
    };
    [SerializeField] private LightPreset fallbackPreset = new LightPreset
    {
        color = Color.white,
        intensity = 1f
    };

    [Header("Options")]
    [SerializeField] private bool requireFreeRoamType = true;
    [SerializeField] private bool autoRefresh = true;

    private string lastFlowType;
    private string lastFlowId;

    private void Reset()
    {
        if (targetGlobalLight == null)
            targetGlobalLight = GetComponent<Light2D>();
    }

    private void Awake()
    {
        if (targetGlobalLight == null)
            targetGlobalLight = GetComponent<Light2D>();
    }

    private void OnEnable()
    {
        RefreshNow();
    }

    private void Update()
    {
        if (!Application.isPlaying || !autoRefresh)
            return;

        string flowType = FlowContext.CurrentType;
        string flowId = FlowContext.CurrentId;
        if (flowType == lastFlowType && flowId == lastFlowId)
            return;

        RefreshNow();
    }

    [ContextMenu("Refresh Global Light")]
    public void RefreshNow()
    {
        if (targetGlobalLight == null)
            return;

        string flowType = FlowContext.CurrentType;
        string flowId = FlowContext.CurrentId;
        lastFlowType = flowType;
        lastFlowId = flowId;

        if (requireFreeRoamType && !FlowContext.IsFreeRoam())
        {
            ApplyPreset(fallbackPreset);
            return;
        }

        if (FlowContext.IsLunchFreeRoam())
            ApplyPreset(lunchPreset);
        else if (FlowContext.IsAfterSchoolFreeRoam())
            ApplyPreset(afterSchoolPreset);
        else if (FlowContext.IsMorningBeforeAssemblyFreeRoam() || FlowContext.IsDay5FreeRoam())
            ApplyPreset(morningPreset);
        else
            ApplyPreset(fallbackPreset);
    }

    private void ApplyPreset(LightPreset preset)
    {
        if (preset == null || targetGlobalLight == null)
            return;

        targetGlobalLight.color = preset.color;
        targetGlobalLight.intensity = Mathf.Max(0f, preset.intensity);
    }
}
