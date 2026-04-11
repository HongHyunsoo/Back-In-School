using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Back In School/VFX/Ambient Dust Effect")]
public class AmbientDustEffect : MonoBehaviour
{
    private static Material cachedDustMaterial;

    [Header("References")]
    [SerializeField] private ParticleSystem particleSystemRef;

    [Header("Area")]
    [SerializeField] private Vector2 areaSize = new Vector2(2.4f, 1.1f);

    [Header("Look")]
    [SerializeField] private Color tint = new Color(0.82f, 0.8f, 0.74f, 0.16f);
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 8;

    [Header("Motion")]
    [SerializeField] private float emissionRate = 5f;
    [SerializeField] private int maxParticles = 30;
    [SerializeField] private Vector2 lifetimeRange = new Vector2(5f, 8f);
    [SerializeField] private Vector2 sizeRange = new Vector2(0.05f, 0.09f);
    [SerializeField] private Vector2 speedXRange = new Vector2(-0.03f, 0.04f);
    [SerializeField] private Vector2 speedYRange = new Vector2(0.01f, 0.035f);
    [SerializeField] private float noiseStrength = 0.04f;
    [SerializeField] private bool playOnAwake = true;

    private void Reset()
    {
        EnsureConfigured();
    }

    private void Awake()
    {
        EnsureConfigured();
    }

    private void OnEnable()
    {
        EnsureConfigured();

        if (Application.isPlaying && particleSystemRef != null && playOnAwake)
            particleSystemRef.Play(true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureConfigured();
    }
#endif

    private void EnsureConfigured()
    {
        if (particleSystemRef == null)
            particleSystemRef = GetComponent<ParticleSystem>();

        if (particleSystemRef == null)
        {
            particleSystemRef = gameObject.AddComponent<ParticleSystem>();
            if (GetComponent<ParticleSystemRenderer>() == null)
                gameObject.AddComponent<ParticleSystemRenderer>();
        }

        ConfigureParticleSystem(particleSystemRef);
    }

    private void ConfigureParticleSystem(ParticleSystem ps)
    {
        if (ps == null)
            return;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Vector2 safeArea = new Vector2(Mathf.Max(0.1f, Mathf.Abs(areaSize.x)), Mathf.Max(0.1f, Mathf.Abs(areaSize.y)));
        Vector2 safeLifetime = NormalizePositiveRange(lifetimeRange, 0.1f);
        Vector2 safeSize = NormalizePositiveRange(sizeRange, 0.001f);
        Vector2 safeSpeedX = NormalizeRange(speedXRange);
        Vector2 safeSpeedY = NormalizeRange(speedYRange);

        var main = ps.main;
        main.duration = 9f;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = playOnAwake;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startLifetime = new ParticleSystem.MinMaxCurve(safeLifetime.x, safeLifetime.y);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(safeSize.x, safeSize.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startColor = Color.white;
        main.maxParticles = Mathf.Max(1, maxParticles);

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Max(0f, emissionRate);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(safeArea.x, safeArea.y, 0.1f);
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(safeSpeedX.x, safeSpeedX.y);
        velocity.y = new ParticleSystem.MinMaxCurve(safeSpeedY.x, safeSpeedY.y);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var noise = ps.noise;
        noise.enabled = noiseStrength > 0.0001f;
        noise.separateAxes = true;
        noise.frequency = 0.18f;
        noise.scrollSpeedMultiplier = 0.12f;
        noise.damping = true;
        noise.strengthXMultiplier = noiseStrength;
        noise.strengthYMultiplier = noiseStrength * 0.65f;
        noise.strengthZMultiplier = 0f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(BuildDustGradient());

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.65f, 1f, 1f));

        var rotationOverLifetime = ps.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.enabled = true;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingLayerName = string.IsNullOrWhiteSpace(sortingLayerName) ? "Default" : sortingLayerName;
        renderer.sortingOrder = sortingOrder;
        renderer.minParticleSize = 0.0001f;
        renderer.maxParticleSize = 0.5f;
        renderer.sharedMaterial = GetOrCreateDustMaterial();

        if (Application.isPlaying && isActiveAndEnabled && playOnAwake)
            ps.Play(true);
    }

    private static Material GetOrCreateDustMaterial()
    {
        if (cachedDustMaterial != null)
            return cachedDustMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");

        if (shader == null)
            return null;

        cachedDustMaterial = new Material(shader)
        {
            name = "AmbientDust_RuntimeMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };

        return cachedDustMaterial;
    }

    private Gradient BuildDustGradient()
    {
        float alpha = Mathf.Clamp01(tint.a);
        Color rgb = new Color(tint.r, tint.g, tint.b, 1f);

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(rgb, 0f),
                new GradientColorKey(rgb, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(alpha, 0.18f),
                new GradientAlphaKey(alpha * 0.9f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });

        return gradient;
    }

    private static Vector2 NormalizeRange(Vector2 range)
    {
        return range.x <= range.y ? range : new Vector2(range.y, range.x);
    }

    private static Vector2 NormalizePositiveRange(Vector2 range, float minValue)
    {
        Vector2 ordered = NormalizeRange(range);
        return new Vector2(Mathf.Max(minValue, ordered.x), Mathf.Max(minValue, ordered.y));
    }
}
