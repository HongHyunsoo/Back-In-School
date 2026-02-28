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

    [Header("Tetris")]
    public TetrisMinigameController tetris;

    [Header("Croquis Doodle")]
    public CroquisMinigameController croquis;
    public CroquisMinigameConfig croquisConfig;

    [Header("Pixel Paint")]
    public PixelPaintMinigameController pixelPaint;

    private void Awake()
    {
        EnsureControllers();

        string id = PlayerPrefs.GetString("FLOW_ID", "");

        bool shouldRunTetris = !string.IsNullOrEmpty(id) && id.StartsWith(lunchPrefix);
        bool shouldRunCroquis =
            !string.IsNullOrEmpty(id) && id.StartsWith(class1Prefix);
        bool shouldRunPixelPaint =
            !string.IsNullOrEmpty(id) && id.StartsWith(class2Prefix);

        ApplyControllerState(tetris, shouldRunTetris, CanToggleHostGameObject(tetris));
        ApplyControllerState(croquis, shouldRunCroquis, CanToggleHostGameObject(croquis));
        ApplyControllerState(pixelPaint, shouldRunPixelPaint, CanToggleHostGameObject(pixelPaint));

        if (!shouldRunTetris && !shouldRunCroquis && !shouldRunPixelPaint)
        {
            Debug.LogWarning($"[MinigameSceneBootstrap] Unknown FLOW_ID '{id}'.");
            if (FlowManager.Instance != null)
                FlowManager.Instance.CompleteCurrentEvent(0);
        }
    }

    private void EnsureControllers()
    {
        if (tetris == null)
            tetris = FindAnyObjectByType<TetrisMinigameController>();
        if (tetris == null)
        {
            var go = new GameObject("TetrisMinigame");
            tetris = go.AddComponent<TetrisMinigameController>();
        }

        if (croquis == null)
            croquis = FindAnyObjectByType<CroquisMinigameController>();
        if (croquis == null)
        {
            var go = new GameObject("CroquisMinigame");
            croquis = go.AddComponent<CroquisMinigameController>();
        }
        if (croquis != null && croquis.config == null && croquisConfig != null)
            croquis.config = croquisConfig;

        if (pixelPaint == null)
            pixelPaint = FindAnyObjectByType<PixelPaintMinigameController>();
        if (pixelPaint == null)
        {
            var go = new GameObject("PixelPaintMinigame");
            pixelPaint = go.AddComponent<PixelPaintMinigameController>();
        }
    }

    private bool CanToggleHostGameObject(MonoBehaviour controller)
    {
        if (controller == null) return false;
        var host = controller.gameObject;
        if (host == null) return false;

        int count = 0;
        if (tetris != null && tetris.gameObject == host) count++;
        if (croquis != null && croquis.gameObject == host) count++;
        if (pixelPaint != null && pixelPaint.gameObject == host) count++;

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
}
