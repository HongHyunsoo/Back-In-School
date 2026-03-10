using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Enables only one day layout root (D1~D5) under this object.
/// Child name convention:
/// - D1, D2, D3, D4, D5
/// - DAY1, DAY2... also supported
/// </summary>
public class MapDayLayoutGroup : MonoBehaviour
{
    [Header("Optional Fallback")]
    [Tooltip("Shown when no day-matching child is found. Optional.")]
    [SerializeField] private GameObject fallbackRoot;

    [Header("Runtime")]
    [Tooltip("Use FlowManager.day as the primary day source when available.")]
    [SerializeField] private bool preferFlowManagerDay = true;

    [Tooltip("Re-check day every frame in play mode. Useful with debug day jump.")]
    [SerializeField] private bool autoRefresh = true;

    [Tooltip("Hide this root when no match exists and fallback is empty.")]
    [SerializeField] private bool hideAllWhenNoMatch = false;

    private int _lastAppliedDay = -1;
    private GameManager _cachedGameManager;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshNow();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (!Application.isPlaying || !autoRefresh)
            return;

        int day = ResolveCurrentDay();
        if (day != _lastAppliedDay)
            ApplyDay(day);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _cachedGameManager = null;
        RefreshNow();
    }

    [ContextMenu("Refresh Day Layout")]
    public void RefreshNow()
    {
        ApplyDay(ResolveCurrentDay());
    }

    private int ResolveCurrentDay()
    {
        int flowDay = -1;
        if (Application.isPlaying && preferFlowManagerDay && FlowManager.Instance != null)
            flowDay = Mathf.Clamp(FlowManager.Instance.day, 1, 99);

        GameManager gm = _cachedGameManager;
        if (gm == null || !gm.gameObject.scene.IsValid())
        {
            gm = FindAnyObjectByType<GameManager>();
            _cachedGameManager = gm;
        }

        int gameManagerDay = gm != null ? Mathf.Clamp(gm.currentDay, 1, 99) : -1;

        if (flowDay > 0)
        {
            if (gm != null && gm.currentDay != flowDay)
                gm.currentDay = flowDay;

            return flowDay;
        }

        if (gameManagerDay > 0)
            return gameManagerDay;

        return 1;
    }

    private void ApplyDay(int day)
    {
        _lastAppliedDay = day;

        bool foundMatch = false;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null)
                continue;

            bool isDayNode = TryParseDayNode(child.name, out int nodeDay);

            if (isDayNode && nodeDay == day)
                foundMatch = true;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null)
                continue;

            if (fallbackRoot != null && child.gameObject == fallbackRoot)
            {
                child.gameObject.SetActive(!foundMatch);
                continue;
            }

            bool isDayNode = TryParseDayNode(child.name, out int nodeDay);
            if (isDayNode)
                child.gameObject.SetActive(nodeDay == day);
        }

        if (hideAllWhenNoMatch && !foundMatch && fallbackRoot == null)
            SetChildrenActive(false);
    }

    private static bool TryParseDayNode(string name, out int day)
    {
        day = -1;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        string n = name.Trim().ToUpperInvariant();
        if (n.StartsWith("DAY"))
            n = n.Substring(3);
        else if (n.StartsWith("D"))
            n = n.Substring(1);
        else
            return false;

        if (string.IsNullOrEmpty(n))
            return false;

        for (int i = 0; i < n.Length; i++)
        {
            if (!char.IsDigit(n[i]))
                return false;
        }

        if (!int.TryParse(n, out day))
            return false;

        return day >= 1;
    }

    private void SetChildrenActive(bool active)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null)
                continue;

            child.gameObject.SetActive(active);
        }
    }
}
