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
    private float nextScanTime;

    [Header("Defaults")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Range(0f, 1f)] public float matchWidthOrHeight = 0.5f;
    public bool forceScaleWithScreenSize = true;
    public bool convertOverlayToScreenSpaceCamera = false;
    public float screenSpaceCameraPlaneDistance = 100f;

    [Header("Late Spawn Scan")]
    public bool normalizeLateSpawnedCanvases = false;
    [Min(0.1f)] public float lateScanInterval = 0.5f;

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
        NormalizeAllCanvases();
        nextScanTime = Time.unscaledTime + lateScanInterval;
    }

    private void Update()
    {
        if (!normalizeLateSpawnedCanvases)
            return;

        if (Time.unscaledTime < nextScanTime)
            return;

        nextScanTime = Time.unscaledTime + lateScanInterval;
        NormalizeAllCanvases();
    }

    private void NormalizeSceneCanvases(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var canvases = roots[i].GetComponentsInChildren<Canvas>(true);
            for (int j = 0; j < canvases.Length; j++)
                NormalizeCanvas(canvases[j]);
        }
    }

    private void NormalizeAllCanvases()
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
            NormalizeCanvas(canvases[i]);
    }

    private void NormalizeCanvas(Canvas canvas)
    {
        if (canvas == null)
            return;

        if (canvas.gameObject.name == "__SceneTransitionFader")
            return;

        if (canvas.renderMode == RenderMode.WorldSpace)
            return;

        Camera mainCam = Camera.main;
        if (convertOverlayToScreenSpaceCamera &&
            canvas.renderMode == RenderMode.ScreenSpaceOverlay &&
            mainCam != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = mainCam;
            canvas.planeDistance = screenSpaceCameraPlaneDistance;
        }

        var rt = canvas.transform as RectTransform;
        if (rt != null && rt.localScale != Vector3.one)
            rt.localScale = Vector3.one;

        // Missing worldCamera in ScreenSpaceCamera can break layout in build.
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            if (canvas.worldCamera == null)
                canvas.worldCamera = mainCam;

            if (canvas.planeDistance <= 0f)
                canvas.planeDistance = screenSpaceCameraPlaneDistance;
        }

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            return;

        if (forceScaleWithScreenSize)
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = matchWidthOrHeight;
        }
    }
}
