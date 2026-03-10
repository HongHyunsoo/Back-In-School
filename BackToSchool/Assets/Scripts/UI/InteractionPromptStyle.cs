using TMPro;
using UnityEngine;

/// <summary>
/// Shared visual defaults for world-space interaction key prompts.
/// </summary>
public static class InteractionPromptStyle
{
    public const float DefaultFontSize = 24f;
    public const float DefaultWorldScale = 0.08f;

    public static void ApplyWorldTextScale(TMP_Text text, float worldScale)
    {
        if (text == null)
            return;

        if (text is not TextMeshPro)
            return;

        float target = Mathf.Max(0.01f, worldScale);
        Transform parent = text.transform.parent;
        if (parent == null)
        {
            text.transform.localScale = Vector3.one * target;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        float sx = Mathf.Abs(parentScale.x) > 0.0001f ? target / Mathf.Abs(parentScale.x) : target;
        float sy = Mathf.Abs(parentScale.y) > 0.0001f ? target / Mathf.Abs(parentScale.y) : target;
        text.transform.localScale = new Vector3(sx, sy, 1f);
    }
}
