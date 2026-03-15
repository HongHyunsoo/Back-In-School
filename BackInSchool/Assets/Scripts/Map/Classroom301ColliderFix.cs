using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

/// <summary>
/// Fixes movement snagging in 3-1 classroom by converting tilemap collider
/// to composite collider at runtime.
/// </summary>
public static class Classroom301ColliderFix
{
    private static bool installed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (installed)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        installed = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply(scene);
    }

    private static void Apply(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        bool fixedAny = false;

        for (int i = 0; i < roots.Length; i++)
        {
            var colliders = roots[i].GetComponentsInChildren<TilemapCollider2D>(true);
            for (int j = 0; j < colliders.Length; j++)
            {
                var tile = colliders[j];
                if (tile == null || !tile.enabled || tile.isTrigger)
                    continue;

                if (!IsClassroom301Tilemap(tile.transform))
                    continue;

                var rb = tile.GetComponent<Rigidbody2D>();
                if (rb == null)
                    rb = tile.gameObject.AddComponent<Rigidbody2D>();

                rb.bodyType = RigidbodyType2D.Static;
                rb.simulated = true;
                rb.gravityScale = 0f;

                var composite = tile.GetComponent<CompositeCollider2D>();
                if (composite == null)
                    composite = tile.gameObject.AddComponent<CompositeCollider2D>();

                composite.geometryType = CompositeCollider2D.GeometryType.Outlines;
                tile.usedByComposite = true;
                fixedAny = true;
            }
        }

        if (fixedAny)
            Debug.Log("[Classroom301ColliderFix] Applied composite collider fix.");
    }

    private static bool IsClassroom301Tilemap(Transform t)
    {
        if (t == null)
            return false;

        if (t.name.IndexOf("301_Floor", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        Transform cur = t;
        while (cur != null)
        {
            if (cur.name.IndexOf("3-1 CLASSROOM", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            cur = cur.parent;
        }

        return false;
    }
}
