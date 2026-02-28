using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Main menu subway window lighting rig:
/// - Fixed Light2D group with subtle wobble (position/intensity)
/// - Moving shadow caster group that loops horizontally
/// </summary>
public class SubwayWindowLightRig2D : MonoBehaviour
{
    [System.Serializable]
    public class FixedLight
    {
        public Light2D light;
        public bool enablePositionWobble = true;
        public float posWobbleX = 0.04f;
        public float posWobbleY = 0.03f;
        public float posWobbleSpeed = 0.85f;
        public bool enableIntensityWobble = true;
        public float intensityWobble = 0.15f;
        public float intensityWobbleSpeed = 1.2f;
    }

    [System.Serializable]
    public class MovingShadow
    {
        public Transform target;
        public float speed = 3.2f;
        public float speedVariance = 0.5f;
    }

    [Header("Fixed Lights")]
    public List<FixedLight> fixedLights = new List<FixedLight>();
    public bool autoCollectChildLights = true;
    public Transform fixedLightRoot;

    [Header("Moving Shadows")]
    public bool enableMovingShadows = false;
    public List<MovingShadow> movingShadows = new List<MovingShadow>();
    public bool autoCollectShadowChildren = true;
    public Transform shadowRoot;

    [Header("Shadow Loop (Local X)")]
    public bool moveLeft = true;
    public bool useUnscaledTime = true;
    public float minX = -22f;
    public float maxX = 22f;
    public Vector2 respawnGapRange = new Vector2(2.5f, 7.0f);
    public Vector2 shadowYJitter = new Vector2(-0.02f, 0.02f);
    public bool randomizeInitialShadowX = true;
    [Tooltip("Keep renderer enabled for silhouette casting, but hide the sprite itself.")]
    public bool hideShadowVisualKeepSilhouette = true;

    [Header("Fixed Light Wobble Master")]
    public bool enableLightPositionWobble = false;
    public bool enableLightIntensityWobble = true;

    [Header("Light2D Blend Forcing")]
    [Tooltip("In this project Renderer2D style order is: 0=Multiply, 1=Additive.")]
    public bool forceBlendStyleSetup = true;
    public bool forceFixedLightBlendStyle = false;
    public int fixedLightBlendStyleIndex = 1;   // Additive
    public int shadowLightBlendStyleIndex = 0;  // Multiply
    public Color shadowLightColor = Color.black;
    public float shadowLightIntensity = 0.35f;

    private readonly List<Vector3> baseLightPos = new List<Vector3>();
    private readonly List<float> baseLightIntensity = new List<float>();
    private readonly List<float> lightSeed = new List<float>();

    private readonly List<Vector3> baseShadowPos = new List<Vector3>();
    private readonly List<float> shadowRuntimeSpeed = new List<float>();
    private readonly List<SpriteRenderer> shadowRenderers = new List<SpriteRenderer>();
    private readonly List<Light2D> shadowLights = new List<Light2D>();

    private void Awake()
    {
        BuildListsIfNeeded();
        CacheInitialState();
    }

    private void LateUpdate()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        TickFixedLights();
        if (enableMovingShadows)
            TickMovingShadows(dt);
    }

    private void BuildListsIfNeeded()
    {
        if (autoCollectChildLights && fixedLights.Count == 0)
        {
            Transform root = fixedLightRoot != null ? fixedLightRoot : transform;
            var lights = root.GetComponentsInChildren<Light2D>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                fixedLights.Add(new FixedLight { light = lights[i] });
            }
        }

        if (enableMovingShadows && movingShadows.Count == 0)
        {
            Transform root = shadowRoot != null ? shadowRoot : transform;
            for (int i = 0; i < root.childCount; i++)
            {
                var t = root.GetChild(i);
                if (t == transform || (fixedLightRoot != null && t.IsChildOf(fixedLightRoot)))
                    continue;
                movingShadows.Add(new MovingShadow { target = t });
            }
        }
    }

    private void CacheInitialState()
    {
        baseLightPos.Clear();
        baseLightIntensity.Clear();
        lightSeed.Clear();

        for (int i = fixedLights.Count - 1; i >= 0; i--)
        {
            if (fixedLights[i] == null || fixedLights[i].light == null)
                fixedLights.RemoveAt(i);
        }

        for (int i = 0; i < fixedLights.Count; i++)
        {
            var l = fixedLights[i].light;
            baseLightPos.Add(l.transform.localPosition);
            baseLightIntensity.Add(l.intensity);
            lightSeed.Add(Random.Range(10f, 999f));
        }

        baseShadowPos.Clear();
        shadowRuntimeSpeed.Clear();
        shadowRenderers.Clear();
        shadowLights.Clear();
        if (!enableMovingShadows)
            return;

        for (int i = movingShadows.Count - 1; i >= 0; i--)
        {
            if (movingShadows[i] == null || movingShadows[i].target == null)
                movingShadows.RemoveAt(i);
        }

        for (int i = 0; i < movingShadows.Count; i++)
        {
            var tr = movingShadows[i].target;
            Vector3 lp = tr.localPosition;
            if (randomizeInitialShadowX)
                lp.x = Random.Range(minX, maxX);
            tr.localPosition = lp;

            baseShadowPos.Add(lp);
            float s = movingShadows[i].speed + Random.Range(-movingShadows[i].speedVariance, movingShadows[i].speedVariance);
            shadowRuntimeSpeed.Add(Mathf.Max(0.01f, s));

            var sr = tr.GetComponent<SpriteRenderer>();
            shadowRenderers.Add(sr);
            var sl = tr.GetComponent<Light2D>();
            if (sl == null)
                sl = tr.GetComponentInChildren<Light2D>(true);
            shadowLights.Add(sl);
            if (sr != null)
            {
                if (hideShadowVisualKeepSilhouette)
                {
                    sr.enabled = true;
                    Color c = sr.color;
                    c.a = 0f;
                    sr.color = c;
                }
            }

            if (forceBlendStyleSetup)
            {
                var shadowLight = shadowLights[i];
                if (shadowLight != null)
                {
                    shadowLight.blendStyleIndex = Mathf.Max(0, shadowLightBlendStyleIndex);
                    shadowLight.color = shadowLightColor;
                    shadowLight.intensity = shadowLightIntensity;
                }
            }
        }

        if (forceBlendStyleSetup && forceFixedLightBlendStyle)
        {
            for (int i = 0; i < fixedLights.Count; i++)
            {
                var l = fixedLights[i].light;
                if (l == null) continue;
                l.blendStyleIndex = Mathf.Max(0, fixedLightBlendStyleIndex);
            }
        }
    }

    private void TickFixedLights()
    {
        float t = useUnscaledTime ? Time.unscaledTime : Time.time;

        for (int i = 0; i < fixedLights.Count; i++)
        {
            var cfg = fixedLights[i];
            var l = cfg.light;
            if (l == null) continue;

            Vector3 p = baseLightPos[i];
            float seed = lightSeed[i];

            if (enableLightPositionWobble && cfg.enablePositionWobble)
            {
                float nx = Mathf.PerlinNoise(seed, t * cfg.posWobbleSpeed) - 0.5f;
                float ny = Mathf.PerlinNoise(seed + 13.7f, t * cfg.posWobbleSpeed) - 0.5f;
                p.x += nx * cfg.posWobbleX;
                p.y += ny * cfg.posWobbleY;
            }

            l.transform.localPosition = p;

            if (enableLightIntensityWobble && cfg.enableIntensityWobble)
            {
                float ni = Mathf.PerlinNoise(seed + 29.1f, t * cfg.intensityWobbleSpeed) - 0.5f;
                l.intensity = Mathf.Max(0f, baseLightIntensity[i] + (ni * cfg.intensityWobble));
            }
            else
            {
                l.intensity = baseLightIntensity[i];
            }
        }
    }

    private void TickMovingShadows(float dt)
    {
        float dir = moveLeft ? -1f : 1f;

        for (int i = 0; i < movingShadows.Count; i++)
        {
            var shadow = movingShadows[i];
            if (shadow.target == null) continue;

            Vector3 lp = shadow.target.localPosition;
            lp.x += dir * shadowRuntimeSpeed[i] * dt;
            shadow.target.localPosition = lp;

            if (moveLeft)
            {
                if (lp.x < minX) RespawnShadow(i);
            }
            else
            {
                if (lp.x > maxX) RespawnShadow(i);
            }
        }
    }

    private void RespawnShadow(int index)
    {
        if (index < 0 || index >= movingShadows.Count) return;
        var t = movingShadows[index].target;
        if (t == null) return;

        float gap = Random.Range(respawnGapRange.x, respawnGapRange.y);

        Vector3 lp = t.localPosition;
        // Always respawn from opposite edge.
        lp.x = moveLeft ? (maxX + gap) : (minX - gap);
        lp.y = baseShadowPos[index].y + Random.Range(shadowYJitter.x, shadowYJitter.y);
        t.localPosition = lp;

        float s = movingShadows[index].speed + Random.Range(-movingShadows[index].speedVariance, movingShadows[index].speedVariance);
        shadowRuntimeSpeed[index] = Mathf.Max(0.01f, s);
    }

}
