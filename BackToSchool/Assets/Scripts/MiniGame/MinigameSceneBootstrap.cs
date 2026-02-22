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

        bool sharedHost =
            tetris.gameObject == croquis.gameObject ||
            tetris.gameObject == pixelPaint.gameObject ||
            croquis.gameObject == pixelPaint.gameObject;

        if (sharedHost)
        {
            // If all controllers are attached to one host object, avoid SetActive on the whole object.
            tetris.enabled = shouldRunTetris;
            croquis.enabled = shouldRunCroquis;
            pixelPaint.enabled = shouldRunPixelPaint;

            Debug.LogWarning("[MinigameSceneBootstrap] Controllers share same GameObject. Using component enable/disable mode.");
        }
        else
        {
            tetris.gameObject.SetActive(shouldRunTetris);
            croquis.gameObject.SetActive(shouldRunCroquis);
            pixelPaint.gameObject.SetActive(shouldRunPixelPaint);

            tetris.enabled = shouldRunTetris;
            croquis.enabled = shouldRunCroquis;
            pixelPaint.enabled = shouldRunPixelPaint;
        }

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

        if (pixelPaint == null)
            pixelPaint = FindAnyObjectByType<PixelPaintMinigameController>();
        if (pixelPaint == null)
        {
            var go = new GameObject("PixelPaintMinigame");
            pixelPaint = go.AddComponent<PixelPaintMinigameController>();
        }
    }
}
