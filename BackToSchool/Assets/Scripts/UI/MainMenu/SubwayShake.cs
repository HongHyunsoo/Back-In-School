using UnityEngine;

/// <summary>
/// Subtle subway-like shake for a root transform.
/// Put this on a parent that contains background/interior visuals.
/// </summary>
public class SubwayShake : MonoBehaviour
{
    [Header("Intensity")]
    public float xAmplitude = 0.03f;
    public float yAmplitude = 0.015f;

    [Header("Motion")]
    public float frequency = 1.8f;
    public float jitter = 0.35f;
    public float smooth = 8f;

    private Vector3 originLocalPos;
    private float seedA;
    private float seedB;

    private void Awake()
    {
        originLocalPos = transform.localPosition;
        seedA = Random.Range(0f, 1000f);
        seedB = Random.Range(0f, 1000f);
    }

    private void OnEnable()
    {
        originLocalPos = transform.localPosition;
    }

    private void LateUpdate()
    {
        float t = Time.unscaledTime * frequency;

        float n1 = Mathf.PerlinNoise(seedA, t);
        float n2 = Mathf.PerlinNoise(seedB, t + jitter);

        float x = (n1 - 0.5f) * 2f * xAmplitude;
        float y = (n2 - 0.5f) * 2f * yAmplitude;

        Vector3 target = originLocalPos + new Vector3(x, y, 0f);
        transform.localPosition = Vector3.Lerp(transform.localPosition, target, Time.unscaledDeltaTime * smooth);
    }

    public void ResetToOrigin()
    {
        transform.localPosition = originLocalPos;
    }
}
