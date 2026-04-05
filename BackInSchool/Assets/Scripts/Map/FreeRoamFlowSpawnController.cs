using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class FreeRoamFlowSpawnController : MonoBehaviour
{
    public enum SpawnMatchMode
    {
        Any,
        FlowIdContains,
        FlowIdExact,
        FreeRoamPeriod
    }

    public enum FreeRoamPeriod
    {
        Any,
        MorningBeforeAssembly,
        Lunch,
        AfterSchool
    }

    [Serializable]
    public class SpawnEntry
    {
        [Tooltip("Inspector label only.")]
        public string label;

        [Tooltip("How this entry decides whether the current FREEROAM flow should use this spawn point.")]
        public SpawnMatchMode matchMode = SpawnMatchMode.FlowIdContains;

        [FormerlySerializedAs("flowIdContains")]
        [Tooltip("Used when Match Mode is FlowIdContains.")]
        public string flowIdContains;

        [Tooltip("Used when Match Mode is FlowIdExact.")]
        public string flowIdExact;

        [Tooltip("Used when Match Mode is FreeRoamPeriod.")]
        public FreeRoamPeriod freeRoamPeriod = FreeRoamPeriod.Any;

        [Tooltip("Optional day filter. 0 means any day, 1~5 means only that day.")]
        [Min(0)]
        public int day = 0;

        public Transform spawnPoint;
    }

    [Header("Flow Spawn Points")]
    [SerializeField] private List<SpawnEntry> spawnEntries = new();
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool onlyInFreeRoamScene = true;

    private void Start()
    {
        if (!applyOnStart)
            return;

        StartCoroutine(CoApplySpawnNextFrame());
    }

    private IEnumerator CoApplySpawnNextFrame()
    {
        yield return null;
        ApplySpawnNow();
    }

    [ContextMenu("Apply FREEROAM Spawn Now")]
    public void ApplySpawnNow()
    {
        if (onlyInFreeRoamScene && SceneManager.GetActiveScene().name != "FREEROAM")
            return;

        if (!FlowContext.IsFreeRoam())
            return;

        Transform player = ResolvePlayerTransform();
        if (player == null)
        {
            Debug.LogWarning("[FreeRoamFlowSpawnController] Player not found.");
            return;
        }

        SpawnEntry match = FindMatchingEntry(FlowContext.CurrentId);
        if (match == null || match.spawnPoint == null)
            return;

        player.position = match.spawnPoint.position;
        player.rotation = match.spawnPoint.rotation;

        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private SpawnEntry FindMatchingEntry(string flowId)
    {
        SpawnEntry fallback = null;
        for (int i = 0; i < spawnEntries.Count; i++)
        {
            var entry = spawnEntries[i];
            if (entry == null || entry.spawnPoint == null)
                continue;

            if (entry.matchMode == SpawnMatchMode.Any)
            {
                fallback = entry;
                continue;
            }

            if (MatchesEntry(entry, flowId))
                return entry;
        }

        return fallback;
    }

    private bool MatchesEntry(SpawnEntry entry, string flowId)
    {
        if (entry == null)
            return false;

        if (!MatchesDay(entry.day))
            return false;

        switch (entry.matchMode)
        {
            case SpawnMatchMode.Any:
                return true;

            case SpawnMatchMode.FlowIdContains:
                return !string.IsNullOrWhiteSpace(entry.flowIdContains) &&
                       !string.IsNullOrEmpty(flowId) &&
                       flowId.IndexOf(entry.flowIdContains, StringComparison.OrdinalIgnoreCase) >= 0;

            case SpawnMatchMode.FlowIdExact:
                return !string.IsNullOrWhiteSpace(entry.flowIdExact) &&
                       string.Equals(flowId, entry.flowIdExact, StringComparison.OrdinalIgnoreCase);

            case SpawnMatchMode.FreeRoamPeriod:
                return MatchesFreeRoamPeriod(entry.freeRoamPeriod);
        }

        return false;
    }

    private static bool MatchesFreeRoamPeriod(FreeRoamPeriod period)
    {
        switch (period)
        {
            case FreeRoamPeriod.Any:
                return FlowContext.IsFreeRoam();
            case FreeRoamPeriod.MorningBeforeAssembly:
                return FlowContext.IsMorningBeforeAssemblyFreeRoam();
            case FreeRoamPeriod.Lunch:
                return FlowContext.IsLunchFreeRoam();
            case FreeRoamPeriod.AfterSchool:
                return FlowContext.IsAfterSchoolFreeRoam();
        }

        return false;
    }

    private static bool MatchesDay(int requiredDay)
    {
        if (requiredDay <= 0)
            return true;

        int currentDay = ResolveCurrentDay();
        return currentDay == requiredDay;
    }

    private static int ResolveCurrentDay()
    {
        if (FlowManager.Instance != null)
            return Mathf.Clamp(FlowManager.Instance.day, 1, 99);

        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            return Mathf.Clamp(gm.currentDay, 1, 99);

        return -1;
    }

    private static Transform ResolvePlayerTransform()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
            return pc.transform;

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        return playerGo != null ? playerGo.transform : null;
    }
}
