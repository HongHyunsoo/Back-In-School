using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public class CanvasPostProcessTarget : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float planeDistance = 100f;
    [SerializeField] private bool autoBindMainCamera = true;
    [SerializeField] private bool forceOverrideSorting = true;
    [SerializeField] private int sortingOrder = 5000;

    private Canvas canvas;

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void LateUpdate()
    {
        if (canvas == null)
            canvas = GetComponent<Canvas>();

        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceCamera || canvas.worldCamera != null)
            return;

        Apply();
    }

    [ContextMenu("Apply Canvas Post Processing")]
    public void Apply()
    {
        if (canvas == null)
            canvas = GetComponent<Canvas>();

        if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
            return;

        Camera cam = ResolveTargetCamera();
        if (cam == null)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = Mathf.Max(1f, planeDistance);

        if (forceOverrideSorting)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }
    }

    private Camera ResolveTargetCamera()
    {
        if (targetCamera != null)
            return targetCamera;

        if (autoBindMainCamera)
            return Camera.main;

        return null;
    }
}
