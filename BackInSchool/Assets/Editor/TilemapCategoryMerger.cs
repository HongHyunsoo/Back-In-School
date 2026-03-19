using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapCategoryMerger : EditorWindow
{
    private bool deleteMergedSources = true;
    private bool renameTargetToCategory = true;
    private string targetName = "MergedTilemap";

    [MenuItem("Tools/Back in School/Merge Selected Tilemaps")]
    public static void Open()
    {
        GetWindow<TilemapCategoryMerger>("Tilemap Merger");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Merge Selected Tilemaps", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        int tilemapSelectionCount = CountSelectedTilemaps();
        EditorGUILayout.LabelField("Selected Tilemaps", tilemapSelectionCount.ToString());

        deleteMergedSources = EditorGUILayout.Toggle("Delete Merged Sources", deleteMergedSources);
        renameTargetToCategory = EditorGUILayout.Toggle("Rename Target", renameTargetToCategory);
        targetName = EditorGUILayout.TextField("Target Name", targetName);

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "Select multiple Tilemap objects in the Hierarchy. The first selected Tilemap is kept as the target, " +
            "and every other selected Tilemap is copied into it.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(tilemapSelectionCount < 2))
        {
            if (GUILayout.Button("Merge Selected Tilemaps"))
                MergeSelectedTilemaps();
        }
    }

    private void MergeSelectedTilemaps()
    {
        var tilemaps = GetSelectedTilemaps();
        if (tilemaps.Length < 2)
        {
            Debug.LogWarning("[TilemapCategoryMerger] Select at least two Tilemap objects.");
            return;
        }

        Tilemap target = tilemaps[0];
        Undo.RegisterCompleteObjectUndo(new Object[] { target, target.gameObject }, "Merge Tilemaps");

        if (renameTargetToCategory && !string.IsNullOrWhiteSpace(targetName))
        {
            Undo.RecordObject(target.gameObject, "Rename merged tilemap");
            target.gameObject.name = targetName.Trim();
        }

        int movedTileCount = 0;
        int mergedTilemapCount = 0;

        for (int i = 1; i < tilemaps.Length; i++)
        {
            Tilemap source = tilemaps[i];
            if (source == null || source == target)
                continue;

            Undo.RegisterCompleteObjectUndo(new Object[] { source, source.gameObject }, "Merge Tilemaps");
            movedTileCount += CopyTiles(source, target);
            mergedTilemapCount++;

            if (deleteMergedSources)
                Undo.DestroyObjectImmediate(source.gameObject);
            else
                source.gameObject.SetActive(false);
        }

        target.CompressBounds();
        EditorUtility.SetDirty(target);
        EditorSceneManager.MarkSceneDirty(target.gameObject.scene);

        Debug.Log(
            $"[TilemapCategoryMerger] Target={target.name}, merged tilemaps={mergedTilemapCount}, copied tiles={movedTileCount}");
    }

    private static int CopyTiles(Tilemap source, Tilemap target)
    {
        int moved = 0;
        BoundsInt bounds = source.cellBounds;

        foreach (Vector3Int sourceCell in bounds.allPositionsWithin)
        {
            if (!source.HasTile(sourceCell))
                continue;

            TileBase tile = source.GetTile(sourceCell);
            if (tile == null)
                continue;

            Vector3 worldCenter = source.GetCellCenterWorld(sourceCell);
            Vector3Int targetCell = target.WorldToCell(worldCenter);

            TileFlags sourceFlags = source.GetTileFlags(sourceCell);
            target.SetTile(targetCell, tile);
            target.SetTileFlags(targetCell, TileFlags.None);
            target.SetColor(targetCell, source.GetColor(sourceCell));
            target.SetTransformMatrix(targetCell, source.GetTransformMatrix(sourceCell));
            target.SetTileFlags(targetCell, sourceFlags);
            moved++;
        }

        return moved;
    }

    private static Tilemap[] GetSelectedTilemaps()
    {
        var objects = Selection.gameObjects;
        var tilemaps = new Tilemap[objects.Length];
        int count = 0;

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] == null)
                continue;

            var tilemap = objects[i].GetComponent<Tilemap>();
            if (tilemap == null)
                continue;

            tilemaps[count++] = tilemap;
        }

        var result = new Tilemap[count];
        for (int i = 0; i < count; i++)
            result[i] = tilemaps[i];

        return result;
    }

    private static int CountSelectedTilemaps()
    {
        return GetSelectedTilemaps().Length;
    }
}
