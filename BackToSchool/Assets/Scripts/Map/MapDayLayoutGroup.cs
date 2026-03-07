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
        GameManager gm = _cachedGameManager;
        if (gm == null)
        {
            gm = FindAnyObjectByType<GameManager>();
            _cachedGameManager = gm;
        }

        if (gm == null)
            return 1;

        return Mathf.Clamp(gm.currentDay, 1, 99);
    }

    private void ApplyDay(int day)
    {
        _lastAppliedDay = day;

        bool foundMatch = false;
        string d = "D" + day.ToString();
        string dayText = "DAY" + day.ToString();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null)
                continue;

            string nameUpper = child.name.Trim().ToUpperInvariant();
            bool isDayNode = nameUpper == d || nameUpper == dayText;

            if (isDayNode)
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

            string nameUpper = child.name.Trim().ToUpperInvariant();
            bool isMatch = nameUpper == d || nameUpper == dayText;
            bool isDayNode = nameUpper.StartsWith("D") || nameUpper.StartsWith("DAY");
            if (isDayNode)
                child.gameObject.SetActive(isMatch);
        }

        if (hideAllWhenNoMatch && !foundMatch && fallbackRoot == null)
            SetChildrenActive(false);
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
