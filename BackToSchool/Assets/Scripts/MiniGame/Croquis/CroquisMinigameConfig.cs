using TMPro;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CroquisConfig",
    menuName = "BackToSchool/Minigame/Croquis Config",
    order = 10)]
public class CroquisMinigameConfig : ScriptableObject
{
    [Header("Stage Goals")]
    public int[] successesPerStage = new[] { 10, 15, 20 };

    [Header("Paper Area")]
    public Vector2 paperSize = new Vector2(9.6f, 5.8f);
    public Vector2 paperCenter = Vector2.zero;
    public float paperPadding = 0.45f;
    public Color paperColor = new Color(0.97f, 0.95f, 0.90f, 1f);

    [Header("Stage Sketches (3)")]
    public Sprite[] stageSketchSprites = new Sprite[3];
    [Header("Reveal Frames - Croquis 1 (0~4)")]
    public Sprite[] stage1RevealSprites = new Sprite[5];
    [Header("Reveal Frames - Croquis 2 (5~9)")]
    public Sprite[] stage2RevealSprites = new Sprite[5];
    [Header("Reveal Frames - Croquis 3 (10~14)")]
    public Sprite[] stage3RevealSprites = new Sprite[5];
    [HideInInspector] public Sprite[] stageRevealStepSprites = new Sprite[15]; // legacy
    public Color sketchColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    [Range(0f, 0.25f)] public float stageStartAlpha = 0.02f;

    [Header("Prompt Mix")]
    [Range(0f, 1f)] public float clickPromptChance = 0.55f;

    [Header("Prompt Visual")]
    public GameObject promptVisualPrefab;
    public float markerScale = 0.45f;
    public float markerPulseAmplitude = 0.08f;
    public float markerPulseSpeed = 5f;
    public Color clickColor = new Color(0.20f, 0.85f, 0.95f, 1f);
    public Color dragColor = new Color(0.95f, 0.80f, 0.2f, 1f);
    public float dragArrowLength = 1.0f;

    [Header("Drag Validation")]
    public float requiredDragDistance = 0.65f;
    [Range(0f, 1f)] public float requiredDirectionDot = 0.75f;
    public float maxGrabDistance = 0.45f;

    [Header("Teacher Bubble")]
    public GameObject teacherBubblePrefab;
    [Tooltip("Conversation_ID in Conversations.csv (ex: D1_CLASS1_MINIGAME). If found, this is used first.")]
    public string teacherConversationId = "D1_CLASS1_MINIGAME";
    public string[] teacherLineKeys = new[]
    {
        "MINIGAME_CROQUIS_TEACHER_01","MINIGAME_CROQUIS_TEACHER_02","MINIGAME_CROQUIS_TEACHER_03","MINIGAME_CROQUIS_TEACHER_04","MINIGAME_CROQUIS_TEACHER_05",
        "MINIGAME_CROQUIS_TEACHER_06","MINIGAME_CROQUIS_TEACHER_07","MINIGAME_CROQUIS_TEACHER_08","MINIGAME_CROQUIS_TEACHER_09","MINIGAME_CROQUIS_TEACHER_10",
        "MINIGAME_CROQUIS_TEACHER_11","MINIGAME_CROQUIS_TEACHER_12","MINIGAME_CROQUIS_TEACHER_13","MINIGAME_CROQUIS_TEACHER_14","MINIGAME_CROQUIS_TEACHER_15",
        "MINIGAME_CROQUIS_TEACHER_16","MINIGAME_CROQUIS_TEACHER_17","MINIGAME_CROQUIS_TEACHER_18","MINIGAME_CROQUIS_TEACHER_19","MINIGAME_CROQUIS_TEACHER_20"
    };
    public float bubbleMinInterval = 5f;
    public float bubbleMaxInterval = 9f;
    public float bubbleShowDuration = 2.8f;

    [Header("UI Font")]
    public TMP_FontAsset uiFontAsset;

    [Header("Flow")]
    public int penaltyOnGiveUp = 1;
}
