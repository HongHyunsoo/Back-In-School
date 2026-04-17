using UnityEngine;

/// <summary>
/// Attach this to any GameObject in the MINIGAME scene.
/// It decides which minigame to start based on FLOW_ID written by FlowManager.
/// </summary>
public class MinigameSceneBootstrap : MonoBehaviour
{
    [Header("Routing")]
    [Tooltip("If FLOW_ID starts with this prefix, we run Tetris.")]
    public string lunchPrefix = "LUNCH_";
    [Tooltip("If FLOW_ID starts with this prefix, we run Croquis doodle.")]
    public string class1Prefix = "CLASS1_";
    [Tooltip("If FLOW_ID starts with this prefix, we run Pixel Paint.")]
    public string class2Prefix = "CLASS2_";
    [Tooltip("If FLOW_ID starts with this prefix, we run the arrival space mash minigame.")]
    public string arrivalSpacePrefix = "ARRIVAL_SPACE_";
    [Tooltip("CLASS1 flow IDs that should run the math quiz instead of Croquis.")]
    public string[] class1MathFlowIds = new[] { "CLASS1_D2" };
    [Tooltip("CLASS2 flow IDs that should run the presentation typing minigame instead of Pixel Paint.")]
    public string[] class2PresentationFlowIds = new[] { "CLASS2_D2" };
    [Tooltip("Auto-create missing minigame controllers at runtime. Disable for strict scene validation.")]
    public bool autoCreateMissingControllers = false;

    [Header("Tetris")]
    public TetrisMinigameController tetris;
    public TetrisMinigameConfig tetrisConfig;

    [Header("Arrival Space Mash")]
    public ArrivalSpaceMashMinigameController arrivalSpaceMash;

    [Header("Croquis Doodle")]
    public CroquisMinigameController croquis;
    public CroquisMinigameConfig croquisConfig;

    [Header("Math Quiz")]
    public MathMinigameController math;

    [Header("Pixel Paint")]
    public PixelPaintMinigameController pixelPaint;

    [Header("Presentation Typing")]
    public PresentationTypingMinigameController presentationTyping;
    public PresentationTypingMinigameConfig presentationTypingConfig;

    private void Awake()
    {
        string id = FlowContext.CurrentId;

        bool shouldRunTetris = FlowContext.CurrentIdStartsWith(lunchPrefix);
        bool shouldRunArrivalSpaceMash = FlowContext.CurrentIdStartsWith(arrivalSpacePrefix);
        bool shouldRunMath = IsMathClass1Flow(id);
        bool shouldRunCroquis = FlowContext.CurrentIdStartsWith(class1Prefix) && !shouldRunMath;
        bool shouldRunPresentationTyping = IsPresentationClass2Flow(id);
        bool shouldRunPixelPaint = FlowContext.CurrentIdStartsWith(class2Prefix) && !shouldRunPresentationTyping;

        EnsureControllers(
            shouldRunTetris,
            shouldRunArrivalSpaceMash,
            shouldRunCroquis,
            shouldRunMath,
            shouldRunPixelPaint,
            shouldRunPresentationTyping);

        ApplyControllerState(tetris, shouldRunTetris, CanToggleHostGameObject(tetris));
        ApplyControllerState(arrivalSpaceMash, shouldRunArrivalSpaceMash, CanToggleHostGameObject(arrivalSpaceMash));
        ApplyControllerState(croquis, shouldRunCroquis, CanToggleHostGameObject(croquis));
        ApplyControllerState(math, shouldRunMath, CanToggleHostGameObject(math));
        ApplyControllerState(pixelPaint, shouldRunPixelPaint, CanToggleHostGameObject(pixelPaint));
        ApplyControllerState(presentationTyping, shouldRunPresentationTyping, CanToggleHostGameObject(presentationTyping));

        if (!shouldRunTetris && !shouldRunArrivalSpaceMash && !shouldRunCroquis && !shouldRunMath && !shouldRunPixelPaint && !shouldRunPresentationTyping)
        {
            Debug.LogError($"[MinigameSceneBootstrap] Unsupported FLOW_ID '{id}'. Check FlowManager timeline and minigame routing prefixes.");
        }
    }

    private void EnsureControllers(
        bool shouldRunTetris,
        bool shouldRunArrivalSpaceMash,
        bool shouldRunCroquis,
        bool shouldRunMath,
        bool shouldRunPixelPaint,
        bool shouldRunPresentationTyping)
    {
        if (tetris == null)
            tetris = FindController<TetrisMinigameController>();
        if (tetris == null)
        {
            if (autoCreateMissingControllers || shouldRunTetris)
            {
                var go = new GameObject("TetrisMinigame");
                tetris = go.AddComponent<TetrisMinigameController>();
            }
        }
        if (tetris != null && tetris.config == null && tetrisConfig != null)
            tetris.config = tetrisConfig;

        if (arrivalSpaceMash == null)
            arrivalSpaceMash = FindController<ArrivalSpaceMashMinigameController>();
        if (arrivalSpaceMash == null)
        {
            if (autoCreateMissingControllers || shouldRunArrivalSpaceMash)
            {
                var go = new GameObject("ArrivalSpaceMashMinigame");
                arrivalSpaceMash = go.AddComponent<ArrivalSpaceMashMinigameController>();
            }
        }

        if (croquis == null)
            croquis = FindController<CroquisMinigameController>();
        if (croquis == null)
        {
            if (autoCreateMissingControllers || shouldRunCroquis)
            {
                var go = new GameObject("CroquisMinigame");
                croquis = go.AddComponent<CroquisMinigameController>();
            }
        }
        if (croquis != null && croquis.config == null && croquisConfig != null)
            croquis.config = croquisConfig;

        if (math == null)
            math = FindController<MathMinigameController>();
        if (math == null)
        {
            if (autoCreateMissingControllers || shouldRunMath)
            {
                var go = new GameObject("MathMinigame");
                math = go.AddComponent<MathMinigameController>();
            }
        }

        if (pixelPaint == null)
            pixelPaint = FindController<PixelPaintMinigameController>();
        if (pixelPaint == null)
        {
            if (autoCreateMissingControllers || shouldRunPixelPaint)
            {
                var go = new GameObject("PixelPaintMinigame");
                pixelPaint = go.AddComponent<PixelPaintMinigameController>();
            }
        }

        if (presentationTyping == null)
            presentationTyping = FindController<PresentationTypingMinigameController>();
        if (presentationTyping == null)
        {
            if (autoCreateMissingControllers || shouldRunPresentationTyping)
            {
                var go = new GameObject("PresentationTypingMinigame");
                presentationTyping = go.AddComponent<PresentationTypingMinigameController>();
            }
        }
        if (presentationTyping != null && presentationTyping.config == null && presentationTypingConfig != null)
            presentationTyping.config = presentationTypingConfig;
    }

    private static T FindController<T>() where T : MonoBehaviour
    {
        var found = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (found != null && found.Length > 0)
            return found[0];

        // Build-safe fallback for inactive scene objects that may not be surfaced
        // reliably through FindObjectsByType during early bootstrap timing.
        var all = Resources.FindObjectsOfTypeAll<T>();
        if (all == null || all.Length == 0)
            return null;

        for (int i = 0; i < all.Length; i++)
        {
            T candidate = all[i];
            if (candidate == null)
                continue;

            if (candidate.gameObject == null)
                continue;

            if (!candidate.gameObject.scene.IsValid())
                continue;

            return candidate;
        }

        return null;
    }

    private bool CanToggleHostGameObject(MonoBehaviour controller)
    {
        if (controller == null) return false;
        var host = controller.gameObject;
        if (host == null) return false;

        int count = 0;
        if (tetris != null && tetris.gameObject == host) count++;
        if (arrivalSpaceMash != null && arrivalSpaceMash.gameObject == host) count++;
        if (croquis != null && croquis.gameObject == host) count++;
        if (math != null && math.gameObject == host) count++;
        if (pixelPaint != null && pixelPaint.gameObject == host) count++;
        if (presentationTyping != null && presentationTyping.gameObject == host) count++;

        // Only safe to toggle whole GameObject when this host is not shared.
        return count <= 1;
    }

    private static void ApplyControllerState(MonoBehaviour controller, bool shouldRun, bool canToggleHost)
    {
        if (controller == null) return;

        if (canToggleHost && controller.gameObject != null)
            controller.gameObject.SetActive(shouldRun);

        controller.enabled = shouldRun;
    }

    private bool IsMathClass1Flow(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        if (class1MathFlowIds == null || class1MathFlowIds.Length == 0)
            return string.Equals(id, "CLASS1_D2", System.StringComparison.OrdinalIgnoreCase);

        for (int i = 0; i < class1MathFlowIds.Length; i++)
        {
            if (string.Equals(id, class1MathFlowIds[i], System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool IsPresentationClass2Flow(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        if (class2PresentationFlowIds == null || class2PresentationFlowIds.Length == 0)
            return string.Equals(id, "CLASS2_D2", System.StringComparison.OrdinalIgnoreCase);

        for (int i = 0; i < class2PresentationFlowIds.Length; i++)
        {
            if (string.Equals(id, class2PresentationFlowIds[i], System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
