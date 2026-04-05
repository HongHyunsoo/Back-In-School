using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class GlobalLight2DLocator
{
    [MenuItem("Tools/Back In School/Select Global Light 2Ds In Active Scene")]
    private static void SelectGlobalLightsInActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[GlobalLight2DLocator] No active loaded scene.");
            return;
        }

        List<GameObject> matches = new List<GameObject>();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"[GlobalLight2DLocator] Scene: {scene.name}");

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Light2D[] lights = roots[i].GetComponentsInChildren<Light2D>(true);
            for (int j = 0; j < lights.Length; j++)
            {
                Light2D light = lights[j];
                if (light == null || light.lightType != Light2D.LightType.Global)
                    continue;

                matches.Add(light.gameObject);
                sb.AppendLine($"- {GetHierarchyPath(light.transform)} | activeInHierarchy={light.gameObject.activeInHierarchy} | enabled={light.enabled}");
            }
        }

        Selection.objects = matches.ToArray();

        if (matches.Count == 0)
        {
            Debug.Log($"[GlobalLight2DLocator] No Global Light 2D found in {scene.name}.");
            return;
        }

        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog(
            "Global Light 2D Locator",
            $"Found {matches.Count} Global Light 2D object(s) in {scene.name}.\nThey are now selected in the Hierarchy.",
            "OK");
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null)
            return string.Empty;

        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        return path;
    }
}
