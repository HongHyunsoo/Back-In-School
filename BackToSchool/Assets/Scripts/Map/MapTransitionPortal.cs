using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Trigger-based map transition.
/// - Teleport inside same scene
/// - Or load another scene and spawn at destinationId
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MapTransitionPortal : MonoBehaviour
{
    public enum TransitionMode
    {
        TeleportInSameScene,
        LoadAnotherScene
    }

    [Header("Mode")]
    public TransitionMode mode = TransitionMode.TeleportInSameScene;

    [Header("Trigger/Interaction")]
    [Tooltip("If true, player must press interact key (E by default).")]
    public bool requireInteractKey;
    public GameObject interactPrompt;

    [Header("Destination (Same Scene)")]
    [Tooltip("Direct target transform for same-scene teleport.")]
    public Transform sameSceneTarget;
    [Tooltip("If transform is empty, find MapTransitionDestination by this id in current scene.")]
    public string sameSceneDestinationId;

    [Header("Destination (Another Scene)")]
    public string targetSceneName = "FREEROAM";
    [Tooltip("MapTransitionDestination id in target scene.")]
    public string targetSceneDestinationId;

    [Header("Transition FX")]
    public bool useFade = true;
    public float fadeOutDuration = 0.2f;
    public float fadeInDuration = 0.25f;
    [Tooltip("Prevents immediate retrigger after in-scene teleport.")]
    public float retriggerBlockSeconds = 0.2f;

    private bool playerInRange;
    private bool isTransitioning;
    private Transform cachedPlayer;
    private Rigidbody2D cachedPlayerRb;
    private float blockedUntilTime;

    private static bool hookInstalled;
    private static bool hasPendingSpawn;
    private static string pendingSceneName;
    private static string pendingDestinationId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallHookBeforeSceneLoad()
    {
        EnsureSceneHook();
    }

    private static void EnsureSceneHook()
    {
        if (hookInstalled)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        hookInstalled = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!hasPendingSpawn)
            return;

        if (!string.IsNullOrEmpty(pendingSceneName) && scene.name != pendingSceneName)
            return;

        ApplyPendingSpawn(scene, pendingDestinationId);
        hasPendingSpawn = false;
        pendingSceneName = null;
        pendingDestinationId = null;
    }

    private static void ApplyPendingSpawn(Scene scene, string destinationId)
    {
        Transform player = FindPlayerTransform();
        if (player == null)
        {
            Debug.LogWarning("[MapTransitionPortal] Player not found when applying pending spawn.");
            return;
        }

        if (string.IsNullOrWhiteSpace(destinationId))
            return;

        MapTransitionDestination[] points =
            Object.FindObjectsByType<MapTransitionDestination>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        MapTransitionDestination target = null;
        for (int i = 0; i < points.Length; i++)
        {
            var p = points[i];
            if (p == null) continue;
            if (p.gameObject.scene != scene) continue;
            if (p.destinationId == destinationId)
            {
                target = p;
                break;
            }
        }

        if (target == null)
        {
            Debug.LogWarning($"[MapTransitionPortal] Destination '{destinationId}' not found in scene '{scene.name}'.");
            return;
        }

        player.position = target.transform.position;
        ResetPlayerVelocity(player);
    }

    private static Transform FindPlayerTransform()
    {
        var pc = Object.FindAnyObjectByType<PlayerController>();
        if (pc != null)
            return pc.transform;

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        return playerGo != null ? playerGo.transform : null;
    }

    private static void ResetPlayerVelocity(Transform player)
    {
        if (player == null)
            return;

        var rb = player.GetComponent<Rigidbody2D>();
        if (rb == null)
            return;

        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    private void Awake()
    {
        EnsureSceneHook();

        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;
    }

    private void Start()
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    private void Update()
    {
        if (isTransitioning)
            return;

        if (!playerInRange || Time.time < blockedUntilTime)
        {
            if (interactPrompt != null)
                interactPrompt.SetActive(false);
            return;
        }

        if (requireInteractKey)
        {
            if (interactPrompt != null)
                interactPrompt.SetActive(true);

            KeyCode interactKey = KeyBindingConfig.Get(KeyBindingConfig.InteractKey, KeyCode.E);
            if (Input.GetKeyDown(interactKey))
                BeginTransition();
        }
        else
        {
            if (interactPrompt != null)
                interactPrompt.SetActive(false);

            BeginTransition();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = true;
        cachedPlayer = other.transform;
        cachedPlayerRb = other.attachedRigidbody;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = false;
        cachedPlayer = null;
        cachedPlayerRb = null;
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    private void BeginTransition()
    {
        if (isTransitioning)
            return;

        switch (mode)
        {
            case TransitionMode.TeleportInSameScene:
                TeleportInSameScene();
                break;

            case TransitionMode.LoadAnotherScene:
                StartCoroutine(CoLoadAnotherScene());
                break;
        }
    }

    private void TeleportInSameScene()
    {
        Transform player = cachedPlayer != null ? cachedPlayer : FindPlayerTransform();
        if (player == null)
        {
            Debug.LogWarning("[MapTransitionPortal] Player not found for same-scene teleport.");
            return;
        }

        Transform target = ResolveSameSceneTarget();
        if (target == null)
        {
            Debug.LogWarning($"[MapTransitionPortal] Same-scene target not found. id='{sameSceneDestinationId}'");
            return;
        }

        player.position = target.position;
        if (cachedPlayerRb != null)
        {
            cachedPlayerRb.velocity = Vector2.zero;
            cachedPlayerRb.angularVelocity = 0f;
        }
        else
        {
            ResetPlayerVelocity(player);
        }

        blockedUntilTime = Time.time + Mathf.Max(0f, retriggerBlockSeconds);
    }

    private Transform ResolveSameSceneTarget()
    {
        if (sameSceneTarget != null)
            return sameSceneTarget;

        if (string.IsNullOrWhiteSpace(sameSceneDestinationId))
            return null;

        MapTransitionDestination[] points =
            FindObjectsByType<MapTransitionDestination>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        var scene = gameObject.scene;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null) continue;
            if (points[i].gameObject.scene != scene) continue;
            if (points[i].destinationId == sameSceneDestinationId)
                return points[i].transform;
        }

        return null;
    }

    private IEnumerator CoLoadAnotherScene()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[MapTransitionPortal] targetSceneName is empty.");
            yield break;
        }

        isTransitioning = true;
        hasPendingSpawn = true;
        pendingSceneName = targetSceneName;
        pendingDestinationId = targetSceneDestinationId;

        if (useFade)
        {
            var fader = SceneTransitionFader.EnsureInstance();
            fader.PrepareFadeInOnNextScene(fadeInDuration);
            yield return fader.FadeOut(fadeOutDuration);
        }

        SceneManager.LoadScene(targetSceneName);
    }
}

