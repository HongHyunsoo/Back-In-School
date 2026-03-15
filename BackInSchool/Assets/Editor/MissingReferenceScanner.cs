using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissingReferenceScanner
{
    [MenuItem("Tools/Project/Scan Missing Scripts In Open Scenes")]
    public static void ScanOpenScenes()
    {
        var sb = new StringBuilder();
        int totalObjects = 0;
        int totalMissing = 0;

        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var trs = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < trs.Length; j++)
                {
                    var go = trs[j].gameObject;
                    totalObjects++;
                    int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    if (missing <= 0) continue;

                    totalMissing += missing;
                    sb.AppendLine($"[Scene:{scene.name}] {GetPath(go)} -> missing scripts: {missing}");
                }
            }
        }

        if (totalMissing == 0)
        {
            Debug.Log($"[MissingReferenceScanner] No missing scripts found in open scenes. scanned objects={totalObjects}");
        }
        else
        {
            Debug.LogWarning($"[MissingReferenceScanner] Found missing scripts: {totalMissing}\n{sb}");
        }
    }

    [MenuItem("Tools/Project/Scan Missing Scripts In Project Prefabs")]
    public static void ScanProjectPrefabs()
    {
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        var sb = new StringBuilder();
        int totalMissing = 0;

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;

            var trs = root.GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < trs.Length; j++)
            {
                var go = trs[j].gameObject;
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missing <= 0) continue;

                totalMissing += missing;
                sb.AppendLine($"[Prefab:{path}] {GetPath(go)} -> missing scripts: {missing}");
            }
        }

        if (totalMissing == 0)
        {
            Debug.Log($"[MissingReferenceScanner] No missing scripts found in project prefabs. scanned prefabs={prefabGuids.Length}");
        }
        else
        {
            Debug.LogWarning($"[MissingReferenceScanner] Found missing scripts: {totalMissing}\n{sb}");
        }
    }

    private static string GetPath(GameObject go)
    {
        var t = go.transform;
        string path = go.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
