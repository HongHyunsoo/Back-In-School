using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PlayModeSelectionGuard
{
    static PlayModeSelectionGuard()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
            case PlayModeStateChange.ExitingPlayMode:
            case PlayModeStateChange.EnteredEditMode:
                ClearSelection();
                break;
        }
    }

    private static void ClearSelection()
    {
        Object active = Selection.activeObject;
        if (active == null)
            return;

        Selection.activeObject = null;
        EditorApplication.delayCall += () =>
        {
            if (Selection.activeObject == null)
                return;

            Selection.activeObject = null;
        };
    }
}
