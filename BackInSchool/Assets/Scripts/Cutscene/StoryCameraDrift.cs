using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class StoryCameraDrift : MonoBehaviour
{
    [Header("Position Drift")]
    [SerializeField] private Vector2 positionAmplitude = new Vector2(0.045f, 0.03f);
    [SerializeField] private Vector2 frequency = new Vector2(0.12f, 0.09f);
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private float smooth = 2.4f;

    private Vector3 baseLocalPosition;
    private float seedX;
    private float seedY;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
        seedX = Random.Range(0f, 1000f);
        seedY = Random.Range(1000f, 2000f);
    }

    private void OnEnable()
    {
        baseLocalPosition = transform.localPosition;
    }

    private void LateUpdate()
    {
        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        float targetX = (Mathf.PerlinNoise(seedX, time * frequency.x) - 0.5f) * 2f * positionAmplitude.x;
        float targetY = (Mathf.PerlinNoise(seedY, time * frequency.y) - 0.5f) * 2f * positionAmplitude.y;

        Vector3 target = baseLocalPosition + new Vector3(targetX, targetY, 0f);
        float lerp = 1f - Mathf.Exp(-Mathf.Max(0.01f, smooth) * Time.unscaledDeltaTime);
        transform.localPosition = Vector3.Lerp(transform.localPosition, target, lerp);
    }
}

internal static class StoryCameraDriftBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !string.Equals(scene.name, "STORY", System.StringComparison.OrdinalIgnoreCase))
            return;

        Camera cam = Camera.main;
        if (cam == null)
            cam = Object.FindAnyObjectByType<Camera>();

        if (cam == null)
            return;

        if (cam.GetComponent<StoryCameraDrift>() == null)
            cam.gameObject.AddComponent<StoryCameraDrift>();
    }
}
