using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class FreeRoamFlowSpawnController : MonoBehaviour
{
    [Serializable]
    public class SpawnEntry
    {
        [Tooltip("Inspector label only.")]
        public string label;
        [Tooltip("Leave empty to match any FREEROAM flow id.")]
        public string flowIdContains;
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

            if (string.IsNullOrWhiteSpace(entry.flowIdContains))
            {
                fallback = entry;
                continue;
            }

            if (!string.IsNullOrEmpty(flowId) &&
                flowId.IndexOf(entry.flowIdContains, StringComparison.OrdinalIgnoreCase) >= 0)
                return entry;
        }

        return fallback;
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
