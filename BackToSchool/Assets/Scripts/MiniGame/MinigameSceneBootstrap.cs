using UnityEngine;

/// <summary>
/// Attach this to any GameObject in the MINIGAME scene.
/// It decides which minigame to start based on PlayerPrefs written by FlowManager.
/// Currently: FLOW_ID starting with "LUNCH_" -> Tetris.
/// </summary>
public class MinigameSceneBootstrap : MonoBehaviour
{
    [Header("Routing")]
    [Tooltip("If FLOW_ID starts with this prefix, we run Tetris.")]
    public string lunchPrefix = "LUNCH_";

    [Header("Tetris")]
    public TetrisMinigameController tetris;

    private void Awake()
    {
        if (tetris == null)
        {
            tetris = FindAnyObjectByType<TetrisMinigameController>();
        }

        // If not present in scene, create one.
        if (tetris == null)
        {
            var go = new GameObject("TetrisMinigame");
            tetris = go.AddComponent<TetrisMinigameController>();
        }

        // Decide based on FLOW_ID.
        string id = PlayerPrefs.GetString("FLOW_ID", "");

        bool shouldRunTetris = !string.IsNullOrEmpty(id) && id.StartsWith(lunchPrefix);
        tetris.gameObject.SetActive(shouldRunTetris);

        if (!shouldRunTetris)
        {
            Debug.LogWarning($"[MinigameSceneBootstrap] Unknown FLOW_ID '{id}'. Tetris disabled.");
        }
    }
}
