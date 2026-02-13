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
    [Tooltip("If FLOW_ID starts with this prefix, we run Pixel Paint.")]
    public string class1Prefix = "CLASS1_";
    [Tooltip("If FLOW_ID starts with this prefix, we run Pixel Paint.")]
    public string class2Prefix = "CLASS2_";

    [Header("Tetris")]
    public TetrisMinigameController tetris;

    [Header("Pixel Paint")]
    public PixelPaintMinigameController pixelPaint;

    private void Awake()
    {
        EnsureControllers();

        string id = PlayerPrefs.GetString("FLOW_ID", "");

        bool shouldRunTetris = !string.IsNullOrEmpty(id) && id.StartsWith(lunchPrefix);
        bool shouldRunPixelPaint =
            !string.IsNullOrEmpty(id) &&
            (id.StartsWith(class1Prefix) || id.StartsWith(class2Prefix));

        tetris.gameObject.SetActive(shouldRunTetris);
        pixelPaint.gameObject.SetActive(shouldRunPixelPaint);

        if (!shouldRunTetris && !shouldRunPixelPaint)
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

        if (pixelPaint == null)
            pixelPaint = FindAnyObjectByType<PixelPaintMinigameController>();
        if (pixelPaint == null)
        {
            var go = new GameObject("PixelPaintMinigame");
            pixelPaint = go.AddComponent<PixelPaintMinigameController>();
        }
    }
}
