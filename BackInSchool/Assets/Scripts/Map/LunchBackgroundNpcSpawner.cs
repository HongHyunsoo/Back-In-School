using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class LunchBackgroundNpcSpawner : MonoBehaviour
{
    [System.Serializable]
    private sealed class SpawnRoute
    {
        public string floorId = "";
        public Transform start = null;
        public Transform end = null;
        public bool allowReverse = true;
        public float yJitter = 0.02f;
    }

    private sealed class AliveNpc
    {
        public GameObject instance;
        public GameObject prefab;
        public string floorId;
    }

    private enum WalkDirection
    {
        Right,
        Left,
        Random
    }

    [Header("Spawn")]
    [SerializeField] private GameObject[] npcPrefabs;
    [SerializeField] private SpawnRoute[] routes;
    [SerializeField] private Transform[] floorAnchors;
    [SerializeField] private Transform spawnedParent;
    [SerializeField] private int maxAlive = 3;
    [SerializeField] private bool preventSamePrefabOnSameFloor = true;
    [SerializeField] private Vector2 spawnIntervalSeconds = new Vector2(3.5f, 7f);
    [SerializeField] private Vector2 speedRange = new Vector2(0.45f, 0.8f);

    [Header("Path")]
    [SerializeField] private WalkDirection walkDirection = WalkDirection.Right;
    [SerializeField] private float leftSpawnX = -10.5f;
    [SerializeField] private float rightSpawnX = 10.5f;
    [SerializeField] private float floorYJitter = 0.02f;
    [SerializeField] private float spawnZ = 0f;

    [Header("Condition")]
    [SerializeField] private bool onlyDuringLunchFreeTime = true;
    [SerializeField] private bool clearWhenLunchEnds = true;

    private readonly List<AliveNpc> aliveNpcs = new();
    private float nextSpawnAt;
    private string activeFlowId = string.Empty;

    private void OnEnable()
    {
        ResetSchedule();
    }

    private void OnDisable()
    {
        if (clearWhenLunchEnds)
            ClearAliveNpcs();
    }

    private void Update()
    {
        CleanupDeadNpcs();

        if (!IsSpawnAllowed())
        {
            ResetSchedule();
            if (clearWhenLunchEnds && aliveNpcs.Count > 0)
                ClearAliveNpcs();
            return;
        }

        string flowId = FlowContext.CurrentId ?? string.Empty;
        if (!string.Equals(activeFlowId, flowId, System.StringComparison.Ordinal))
        {
            activeFlowId = flowId;
            ResetSchedule();
        }

        if (aliveNpcs.Count >= Mathf.Max(1, maxAlive) || Time.unscaledTime < nextSpawnAt)
            return;

        SpawnNpc();
        ScheduleNextSpawn();
    }

    private void SpawnNpc()
    {
        if (TrySpawnOnRoute())
            return;

        Transform floor = PickFloorAnchor();
        GameObject prefab = PickPrefab(GetFloorId(floor));
        if (prefab == null)
            return;

        float direction = ResolveDirection();
        float spawnX = direction > 0f ? leftSpawnX : rightSpawnX;
        float despawnX = direction > 0f ? rightSpawnX : leftSpawnX;
        float spawnY = floor != null ? floor.position.y : transform.position.y;
        spawnY += Random.Range(-Mathf.Abs(floorYJitter), Mathf.Abs(floorYJitter));

        var position = new Vector3(spawnX, spawnY, spawnZ);
        Transform parent = spawnedParent != null ? spawnedParent : transform;
        GameObject npc = Instantiate(prefab, position, Quaternion.identity, parent);
        npc.name = $"{prefab.name}_Background";
        aliveNpcs.Add(new AliveNpc
        {
            instance = npc,
            prefab = prefab,
            floorId = GetFloorId(floor)
        });

        BackgroundNpcWalker walker = npc.GetComponent<BackgroundNpcWalker>();
        if (walker == null)
            walker = npc.AddComponent<BackgroundNpcWalker>();

        walker.Initialize(direction, Random.Range(speedRange.x, speedRange.y), despawnX);
    }

    private bool TrySpawnOnRoute()
    {
        SpawnRoute route = PickRoute();
        if (route == null || route.start == null || route.end == null)
            return false;

        GameObject prefab = PickPrefab(GetRouteFloorId(route));
        if (prefab == null)
            return false;

        Vector3 start = route.start.position;
        Vector3 end = route.end.position;
        bool reverse = route.allowReverse && walkDirection == WalkDirection.Random && Random.value < 0.5f;
        if (reverse)
            (start, end) = (end, start);

        float direction = end.x >= start.x ? 1f : -1f;
        Vector3 spawnPosition = new Vector3(start.x, start.y + Random.Range(-Mathf.Abs(route.yJitter), Mathf.Abs(route.yJitter)), spawnZ);
        Transform parent = spawnedParent != null ? spawnedParent : transform;
        GameObject npc = Instantiate(prefab, spawnPosition, Quaternion.identity, parent);
        npc.name = $"{prefab.name}_Background";
        aliveNpcs.Add(new AliveNpc
        {
            instance = npc,
            prefab = prefab,
            floorId = GetRouteFloorId(route)
        });

        BackgroundNpcWalker walker = npc.GetComponent<BackgroundNpcWalker>();
        if (walker == null)
            walker = npc.AddComponent<BackgroundNpcWalker>();

        walker.Initialize(direction, Random.Range(speedRange.x, speedRange.y), end.x);
        return true;
    }

    private GameObject PickPrefab(string floorId)
    {
        if (npcPrefabs == null || npcPrefabs.Length == 0)
            return null;

        for (int attempts = 0; attempts < npcPrefabs.Length; attempts++)
        {
            GameObject candidate = npcPrefabs[Random.Range(0, npcPrefabs.Length)];
            if (candidate != null && !HasSamePrefabOnFloor(candidate, floorId))
                return candidate;
        }

        return null;
    }

    private Transform PickFloorAnchor()
    {
        if (floorAnchors == null || floorAnchors.Length == 0)
            return null;

        for (int attempts = 0; attempts < floorAnchors.Length; attempts++)
        {
            Transform candidate = floorAnchors[Random.Range(0, floorAnchors.Length)];
            if (candidate != null)
                return candidate;
        }

        return null;
    }

    private SpawnRoute PickRoute()
    {
        if (routes == null || routes.Length == 0)
            return null;

        for (int attempts = 0; attempts < routes.Length; attempts++)
        {
            SpawnRoute candidate = routes[Random.Range(0, routes.Length)];
            if (candidate != null &&
                candidate.start != null &&
                candidate.end != null &&
                HasAvailablePrefabForFloor(GetRouteFloorId(candidate)))
                return candidate;
        }

        return null;
    }

    private bool HasAvailablePrefabForFloor(string floorId)
    {
        if (npcPrefabs == null || npcPrefabs.Length == 0)
            return false;

        for (int i = 0; i < npcPrefabs.Length; i++)
        {
            GameObject prefab = npcPrefabs[i];
            if (prefab != null && !HasSamePrefabOnFloor(prefab, floorId))
                return true;
        }

        return false;
    }

    private bool HasSamePrefabOnFloor(GameObject prefab, string floorId)
    {
        if (!preventSamePrefabOnSameFloor || prefab == null || string.IsNullOrEmpty(floorId))
            return false;

        for (int i = 0; i < aliveNpcs.Count; i++)
        {
            AliveNpc alive = aliveNpcs[i];
            if (alive == null || alive.instance == null)
                continue;

            if (alive.prefab == prefab && string.Equals(alive.floorId, floorId, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string GetRouteFloorId(SpawnRoute route)
    {
        if (route == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(route.floorId))
            return route.floorId.Trim();

        if (route.start != null && route.start.parent != null)
            return route.start.parent.name;

        return route.start != null ? route.start.name : string.Empty;
    }

    private static string GetFloorId(Transform floor)
    {
        return floor != null ? floor.name : string.Empty;
    }

    private float ResolveDirection()
    {
        if (walkDirection == WalkDirection.Left)
            return -1f;
        if (walkDirection == WalkDirection.Random)
            return Random.value < 0.5f ? -1f : 1f;
        return 1f;
    }

    private bool IsSpawnAllowed()
    {
        if (!onlyDuringLunchFreeTime)
            return true;

        if (SceneManager.GetActiveScene().name != "FREEROAM")
            return false;

        if (!FlowContext.IsLunchFreeRoam())
            return false;

        GameManager gameManager = FindAnyObjectByType<GameManager>();
        return gameManager == null || gameManager.currentState == GameState.Lunch_FreeTime;
    }

    private void ScheduleNextSpawn()
    {
        float min = Mathf.Max(0.1f, Mathf.Min(spawnIntervalSeconds.x, spawnIntervalSeconds.y));
        float max = Mathf.Max(min, Mathf.Max(spawnIntervalSeconds.x, spawnIntervalSeconds.y));
        nextSpawnAt = Time.unscaledTime + Random.Range(min, max);
    }

    private void ResetSchedule()
    {
        ScheduleNextSpawn();
    }

    private void CleanupDeadNpcs()
    {
        for (int i = aliveNpcs.Count - 1; i >= 0; i--)
        {
            if (aliveNpcs[i] == null || aliveNpcs[i].instance == null)
                aliveNpcs.RemoveAt(i);
        }
    }

    private void ClearAliveNpcs()
    {
        for (int i = aliveNpcs.Count - 1; i >= 0; i--)
        {
            if (aliveNpcs[i] != null && aliveNpcs[i].instance != null)
                Destroy(aliveNpcs[i].instance);
        }

        aliveNpcs.Clear();
    }
}
