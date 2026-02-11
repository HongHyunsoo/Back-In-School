using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime fix for cases where story dialogue has no speaker transform.
/// Ensures the existing DialogueManager speech bubble still appears
/// at a fixed screen position.
/// </summary>
public class DialogueBubbleRuntimeFix : MonoBehaviour
{
    private DialogueManager dm;
    private FieldInfo fiSpeechBubble;
    private FieldInfo fiCurrentSpeaker;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        CacheReflection();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CacheReflection();
        if (dm != null)
            dm.RebindForScene();
    }

    private void LateUpdate()
    {
        if (dm == null) return;
        if (!dm.IsDialogueActive) return;

        var bubble = fiSpeechBubble != null ? fiSpeechBubble.GetValue(dm) as SpeechBubbleUI : null;
        if (bubble == null)
        {
            dm.RebindForScene();
            bubble = fiSpeechBubble != null ? fiSpeechBubble.GetValue(dm) as SpeechBubbleUI : null;
            if (bubble == null) return;
        }

        var speaker = fiCurrentSpeaker != null ? fiCurrentSpeaker.GetValue(dm) as Transform : null;
        if (speaker != null) return;

        var canvas = FindSceneCanvas();
        if (canvas == null) return;

        if (bubble.transform.parent != canvas.transform)
            bubble.transform.SetParent(canvas.transform, false);

        bubble.gameObject.SetActive(true);
        var rt = bubble.transform as RectTransform;
        if (rt == null) return;

        if (speaker != null)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                var canvasRect = canvas.transform as RectTransform;
                Camera uiCam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

                Vector3 screenPos = cam.WorldToScreenPoint(speaker.position + new Vector3(0f, 0.5f, 0f));
                if (screenPos.z >= 0f &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCam, out var localPoint))
                {
                    rt.anchoredPosition = localPoint + new Vector2(0f, -150f);
                    return;
                }
            }
        }

        // speaker가 없거나 화면 밖이면 고정 위치 폴백
        rt.anchoredPosition = new Vector2(0f, -220f);
    }

    private void CacheReflection()
    {
        dm = FindAnyObjectByType<DialogueManager>();
        if (dm == null) return;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        fiSpeechBubble = typeof(DialogueManager).GetField("speechBubble", flags);
        fiCurrentSpeaker = typeof(DialogueManager).GetField("currentSpeaker", flags);
    }

    private static Canvas FindSceneCanvas()
    {
        var active = SceneManager.GetActiveScene();
        var roots = active.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var canvases = roots[i].GetComponentsInChildren<Canvas>(true);
            for (int j = 0; j < canvases.Length; j++)
                return canvases[j];
        }
        return null;
    }
}
