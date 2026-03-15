using UnityEngine;

/// <summary>
/// Stable endless loop for two world-space tiles.
/// Uses deterministic offset so tile X cannot jump to unexpected values.
/// </summary>
public class ParallaxLoopScroller : MonoBehaviour
{
    [Header("Tiles (World/SpriteRenderer)")]
    public Transform tileA;
    public Transform tileB;

    [Header("Scroll")]
    [Tooltip("World units per second")]
    public float speed = 1.5f;
    [Tooltip("Move left when true")]
    public bool moveLeft = true;
    [Tooltip("Ignore timeScale")]
    public bool useUnscaledTime = true;

    [Header("Loop Width")]
    [Tooltip("If > 0, use this value. Otherwise use initial |B-A| distance.")]
    public float tileWidthOverride = 18f;

    [Header("Debug")]
    public bool logOnStart = true;

    private float startAx;
    private float startBx;
    private float tileWidth;
    private float travel;
    private bool initialized;

    private void Awake()
    {
        if (tileA == null || tileB == null || tileA == tileB)
        {
            Debug.LogError("[ParallaxLoopScroller] Assign different tileA/tileB.", this);
            enabled = false;
            return;
        }

        startAx = tileA.localPosition.x;
        startBx = tileB.localPosition.x;

        tileWidth = tileWidthOverride > 0f ? tileWidthOverride : Mathf.Abs(startBx - startAx);
        if (tileWidth <= 0.001f)
        {
            Debug.LogError("[ParallaxLoopScroller] Invalid width. Set tileWidthOverride or separate A/B on X.", this);
            enabled = false;
            return;
        }

        travel = 0f;
        ApplyDeterministicPositions();

        if (logOnStart)
            Debug.Log($"[ParallaxLoopScroller] init ax={startAx}, bx={startBx}, width={tileWidth}", this);

        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float dir = moveLeft ? -1f : 1f;

        travel += dir * speed * dt;

        // Keep travel bounded forever.
        if (travel >= tileWidth || travel <= -tileWidth)
            travel %= tileWidth;

        ApplyDeterministicPositions();
    }

    private void ApplyDeterministicPositions()
    {
        SetLocalX(tileA, startAx + travel);
        SetLocalX(tileB, startBx + travel);
    }

    private static void SetLocalX(Transform t, float x)
    {
        Vector3 p = t.localPosition;
        p.x = x;
        t.localPosition = p;
    }
}
