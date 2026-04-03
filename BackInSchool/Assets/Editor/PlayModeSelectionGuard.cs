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
        if (state != PlayModeStateChange.ExitingPlayMode)
            return;

        Object active = Selection.activeObject;
        if (active == null)
            return;

        Selection.activeObject = null;
    }
}
