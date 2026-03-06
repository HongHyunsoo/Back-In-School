using System.Collections;
using TMPro;
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
    [System.Serializable]
    public class StairDestination
    {
        [Tooltip("If targetSceneName is empty, teleport in current scene.")]
        public string targetSceneName;
        public Transform sameSceneTarget;
        public string destinationId;
    }

    public enum TransitionMode
    {
        TeleportInSameScene,
        LoadAnotherScene,
        StairChoice
    }

    [Header("Mode")]
    public TransitionMode mode = TransitionMode.TeleportInSameScene;

    [Header("Trigger/Interaction")]
    [Tooltip("If true, player must press interact key (E by default).")]
    public bool requireInteractKey;
    public GameObject interactPrompt;
    [SerializeField] private TMP_Text interactKeyText;
    [SerializeField] private string interactKeyFormat = "[{0}]";
    [SerializeField] private TMP_Text stairUpKeyText;
    [SerializeField] private string stairUpKeyFormat = "[{0}] UP";
    [SerializeField] private TMP_Text stairDownKeyText;
    [SerializeField] private string stairDownKeyFormat = "[{0}] DOWN";
    [SerializeField] private float promptFontSize = 1.4f;
    [SerializeField] private float promptWorldScale = 0.08f;

    [Header("Destination (Same Scene)")]
    [Tooltip("Direct target transform for same-scene teleport.")]
    public Transform sameSceneTarget;
    [Tooltip("If transform is empty, find MapTransitionDestination by this id in current scene.")]
    public string sameSceneDestinationId;

    [Header("Destination (Another Scene)")]
    public string targetSceneName = "FREEROAM";
    [Tooltip("MapTransitionDestination id in target scene.")]
    public string targetSceneDestinationId;

    [Header("Stair Choice")]
    [Tooltip("Move destination when selecting up arrow.")]
    public StairDestination stairUp = new StairDestination();
    [Tooltip("Move destination when selecting down arrow.")]
    public StairDestination stairDown = new StairDestination();

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
    private bool awaitingStairChoice;
    private KeyCode lastInteractKey = KeyCode.None;
    private KeyCode lastStairUpKey = KeyCode.None;
    private KeyCode lastStairDownKey = KeyCode.None;

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

        RefreshPromptTexts(true);
    }

    private void Update()
    {
        if (isTransitioning)
            return;

        RefreshPromptTexts();

        if (mode == TransitionMode.StairChoice)
        {
            if (!playerInRange || Time.time < blockedUntilTime)
            {
                awaitingStairChoice = false;
                if (interactPrompt != null)
                    interactPrompt.SetActive(false);
                return;
            }

            // Stair portals should immediately show key guide and accept W/S without extra E press.
            awaitingStairChoice = true;
            if (interactPrompt != null)
                interactPrompt.SetActive(true);
            HandleStairChoiceInput();
            return;
        }

        if (awaitingStairChoice)
        {
            if (interactPrompt != null)
                interactPrompt.SetActive(true);
            HandleStairChoiceInput();
            return;
        }

        if (!playerInRange || Time.time < blockedUntilTime)
        {
            if (interactPrompt != null)
                interactPrompt.SetActive(false);
            return;
        }

        bool needsInteractKey = requireInteractKey || mode == TransitionMode.StairChoice;
        if (needsInteractKey)
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

        awaitingStairChoice = false;
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

            case TransitionMode.StairChoice:
                BeginStairChoice();
                break;
        }
    }

    private void BeginStairChoice()
    {
        awaitingStairChoice = true;
    }

    private void RefreshPromptTexts(bool force = false)
    {
        KeyCode interactKey = KeyBindingConfig.Get(KeyBindingConfig.InteractKey, KeyCode.E);
        KeyCode stairUpKey = KeyBindingConfig.Get(KeyBindingConfig.StairUpKey, KeyCode.W);
        KeyCode stairDownKey = KeyBindingConfig.Get(KeyBindingConfig.StairDownKey, KeyCode.S);

        if (force || interactKey != lastInteractKey)
        {
            lastInteractKey = interactKey;
            if (interactKeyText != null)
            {
                ApplyPromptTextStyle(interactKeyText);
                interactKeyText.text = string.Format(interactKeyFormat, interactKey.ToString().ToUpperInvariant());
            }
        }

        if (force || stairUpKey != lastStairUpKey)
        {
            lastStairUpKey = stairUpKey;
            if (stairUpKeyText != null)
            {
                ApplyPromptTextStyle(stairUpKeyText);
                stairUpKeyText.text = string.Format(stairUpKeyFormat, stairUpKey.ToString().ToUpperInvariant());
            }
        }

        if (force || stairDownKey != lastStairDownKey)
        {
            lastStairDownKey = stairDownKey;
            if (stairDownKeyText != null)
            {
                ApplyPromptTextStyle(stairDownKeyText);
                stairDownKeyText.text = string.Format(stairDownKeyFormat, stairDownKey.ToString().ToUpperInvariant());
            }
        }

        if (mode == TransitionMode.StairChoice && awaitingStairChoice && interactKeyText != null)
        {
            string upText = stairUpKeyFormat.Replace("{0}", stairUpKey.ToString().ToUpperInvariant());
            string downText = stairDownKeyFormat.Replace("{0}", stairDownKey.ToString().ToUpperInvariant());
            ApplyPromptTextStyle(interactKeyText);
            interactKeyText.text = upText + "\n" + downText;
        }
    }

    private void ApplyPromptTextStyle(TMP_Text text)
    {
        if (text == null)
            return;

        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.fontSize = promptFontSize;

        float worldScale = Mathf.Max(0.01f, promptWorldScale);
        Transform parent = text.transform.parent;
        if (parent == null)
        {
            text.transform.localScale = Vector3.one * worldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        float sx = Mathf.Abs(parentScale.x) > 0.0001f ? worldScale / Mathf.Abs(parentScale.x) : worldScale;
        float sy = Mathf.Abs(parentScale.y) > 0.0001f ? worldScale / Mathf.Abs(parentScale.y) : worldScale;
        text.transform.localScale = new Vector3(sx, sy, 1f);
    }

    private void HandleStairChoiceInput()
    {
        if (!playerInRange)
        {
            awaitingStairChoice = false;
            return;
        }

        KeyCode upKey = KeyBindingConfig.Get(KeyBindingConfig.StairUpKey, KeyCode.W);
        KeyCode downKey = KeyBindingConfig.Get(KeyBindingConfig.StairDownKey, KeyCode.S);
        bool upPressed = Input.GetKeyDown(upKey);
        bool downPressed = Input.GetKeyDown(downKey);
        bool cancelPressed = Input.GetKeyDown(KeyCode.Escape) ||
                             Input.GetKeyDown(KeyBindingConfig.Get(KeyBindingConfig.InteractKey, KeyCode.E));

        if (cancelPressed)
        {
            awaitingStairChoice = false;
            return;
        }

        if (upPressed)
        {
            awaitingStairChoice = false;
            ExecuteStairDestination(stairUp);
            return;
        }

        if (downPressed)
        {
            awaitingStairChoice = false;
            ExecuteStairDestination(stairDown);
        }
    }

    private void ExecuteStairDestination(StairDestination destination)
    {
        if (destination == null)
            return;

        if (string.IsNullOrWhiteSpace(destination.targetSceneName) ||
            destination.targetSceneName == SceneManager.GetActiveScene().name)
        {
            Transform target = ResolveLocalDestination(destination.sameSceneTarget, destination.destinationId);
            if (target == null)
            {
                Debug.LogWarning($"[MapTransitionPortal] Stair destination not found. id='{destination.destinationId}'");
                return;
            }

            TeleportToTarget(target);
            return;
        }

        StartCoroutine(CoLoadScene(destination.targetSceneName, destination.destinationId));
    }

    private void TeleportInSameScene()
    {
        Transform target = ResolveSameSceneTarget();
        if (target == null)
        {
            Debug.LogWarning($"[MapTransitionPortal] Same-scene target not found. id='{sameSceneDestinationId}'");
            return;
        }

        TeleportToTarget(target);
    }

    private Transform ResolveSameSceneTarget()
    {
        return ResolveLocalDestination(sameSceneTarget, sameSceneDestinationId);
    }

    private Transform ResolveLocalDestination(Transform targetTransform, string destinationId)
    {
        if (targetTransform != null)
            return targetTransform;

        if (string.IsNullOrWhiteSpace(destinationId))
            return null;

        MapTransitionDestination[] points =
            FindObjectsByType<MapTransitionDestination>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        var scene = gameObject.scene;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null) continue;
            if (points[i].gameObject.scene != scene) continue;
            if (points[i].destinationId == destinationId)
                return points[i].transform;
        }

        return null;
    }

    private void TeleportToTarget(Transform target)
    {
        Transform player = cachedPlayer != null ? cachedPlayer : FindPlayerTransform();
        if (player == null)
        {
            Debug.LogWarning("[MapTransitionPortal] Player not found for teleport.");
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

    private IEnumerator CoLoadAnotherScene()
    {
        yield return CoLoadScene(targetSceneName, targetSceneDestinationId);
    }

    private IEnumerator CoLoadScene(string sceneName, string destinationId)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[MapTransitionPortal] target scene name is empty.");
            yield break;
        }

        isTransitioning = true;
        bool hasDestination = !string.IsNullOrWhiteSpace(destinationId);
        hasPendingSpawn = hasDestination;
        pendingSceneName = hasDestination ? sceneName : null;
        pendingDestinationId = hasDestination ? destinationId : null;

        if (useFade)
        {
            var fader = SceneTransitionFader.EnsureInstance();
            fader.PrepareFadeInOnNextScene(fadeInDuration);
            yield return fader.FadeOut(fadeOutDuration);
        }

        SceneManager.LoadScene(sceneName);
    }
}

