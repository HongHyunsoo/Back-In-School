using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Global UI safety normalizer.
/// Fixes common runtime drift where Canvas root scale / scaler mode causes UI overflow.
/// </summary>
public class GlobalCanvasSafetyNormalizer : MonoBehaviour
{
    private static GlobalCanvasSafetyNormalizer instance;

    [Header("Defaults")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Range(0f, 1f)] public float matchWidthOrHeight = 0.5f;
    public bool forceScaleWithScreenSize = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        var go = new GameObject("__GlobalCanvasSafetyNormalizer");
        instance = go.AddComponent<GlobalCanvasSafetyNormalizer>();
        DontDestroyOnLoad(go);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        NormalizeSceneCanvases(scene);
    }

    private void NormalizeSceneCanvases(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var canvases = roots[i].GetComponentsInChildren<Canvas>(true);
            for (int j = 0; j < canvases.Length; j++)
            {
                var canvas = canvases[j];
                if (canvas == null) continue;

                if (canvas.gameObject.name == "__SceneTransitionFader")
                    continue;

                if (canvas.renderMode == RenderMode.WorldSpace)
                    continue;

                var rt = canvas.transform as RectTransform;
                if (rt != null && rt.localScale != Vector3.one)
                    rt.localScale = Vector3.one;

                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null) continue;

                if (forceScaleWithScreenSize)
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

                if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    scaler.referenceResolution = referenceResolution;
                    scaler.matchWidthOrHeight = matchWidthOrHeight;
                }
            }
        }
    }
}
