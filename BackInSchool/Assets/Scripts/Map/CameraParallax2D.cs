using UnityEngine;

[DisallowMultipleComponent]
public class CameraParallax2D : MonoBehaviour
{
    [SerializeField] private Transform driverTransform;
    [SerializeField] private bool usePlayerWhenEmpty = true;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool useMainCameraWhenEmpty = true;
    [SerializeField] private bool affectX = true;
    [SerializeField] private bool affectY = false;
    [SerializeField] [Min(0.1f)] private float activeHalfWidth = 18f;
    [SerializeField] [Min(0.1f)] private float activeHalfHeight = 8f;
    [SerializeField] private Vector2 localOffsetRangeX = new Vector2(-0.65f, 0.65f);
    [SerializeField] private Vector2 localOffsetRangeY = new Vector2(-0.18f, 0.18f);
    [SerializeField] [Min(0f)] private float smooth = 12f;

    private Vector3 startLocalPosition;
    private Vector3 startWorldPosition;
    private bool initialized;

    private void OnEnable()
    {
        InitializeIfNeeded(forceReset: true);
        ApplyParallax();
    }

    private void LateUpdate()
    {
        InitializeIfNeeded(forceReset: false);
        ApplyParallax();
    }

    private void InitializeIfNeeded(bool forceReset)
    {
        ResolveDriver();
        ResolveCamera();
        if (driverTransform == null && targetCamera == null)
            return;

        if (initialized && !forceReset)
            return;

        startLocalPosition = transform.localPosition;
        startWorldPosition = transform.position;
        initialized = true;
    }

    private void ResolveDriver()
    {
        if (driverTransform != null)
            return;

        if (!usePlayerWhenEmpty)
            return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            driverTransform = player.transform;
    }

    private void ResolveCamera()
    {
        if (targetCamera != null)
            return;

        if (!useMainCameraWhenEmpty)
            return;

        targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = FindAnyObjectByType<Camera>();
    }

    private void ApplyParallax()
    {
        if (!initialized)
            return;

        Vector3 driverPosition = ResolveDriverPosition();
        Vector3 next = transform.localPosition;

        if (affectX)
            next.x = startLocalPosition.x + EvaluateRelativeOffset(driverPosition.x - startWorldPosition.x, activeHalfWidth, localOffsetRangeX);

        if (affectY)
            next.y = startLocalPosition.y + EvaluateRelativeOffset(driverPosition.y - startWorldPosition.y, activeHalfHeight, localOffsetRangeY);

        transform.localPosition = Vector3.Lerp(transform.localPosition, next, Time.deltaTime * Mathf.Max(0f, smooth));
    }

    private Vector3 ResolveDriverPosition()
    {
        if (driverTransform != null)
            return driverTransform.position;

        if (targetCamera != null)
            return targetCamera.transform.position;

        return Vector3.zero;
    }

    private static float EvaluateRelativeOffset(float delta, float activeHalfRange, Vector2 offsetRange)
    {
        float half = Mathf.Max(0.0001f, activeHalfRange);
        if (Mathf.Abs(delta) >= half)
            return 0f;

        float t = Mathf.InverseLerp(-half, half, delta);
        return Mathf.Lerp(offsetRange.x, offsetRange.y, t);
    }
}
