using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(
    fileName = "MathMinigameConfig",
    menuName = "Back in School/Minigame/Math Config",
    order = 14)]
public class MathMinigameConfig : ScriptableObject
{
    [Header("Flow")]
    public string[] supportedFlowIds = new[] { "CLASS1_D2" };
    public int penaltyOnGiveUp = 1;
    public float correctAnswerDelaySeconds = 0.55f;

    [Header("Questions")]
    public List<MathMinigameController.MathQuestionDefinition> questions = new List<MathMinigameController.MathQuestionDefinition>();

    [Header("Drawing Pad")]
    public Vector2Int drawingTextureSize = new Vector2Int(1024, 1024);
    [Range(1, 32)] public int brushRadius = 5;
    public Color drawingBackgroundColor = new Color(0.98f, 0.98f, 0.98f, 1f);
    public Color brushColor = new Color(0.14f, 0.19f, 0.28f, 1f);
    public bool clearCanvasOnQuestionChange = true;

    [Header("UI")]
    public TMP_FontAsset uiFontAsset;
    public Color dimColor = new Color(0.08f, 0.08f, 0.12f, 0.82f);
    public Color panelColor = new Color(0.96f, 0.94f, 0.90f, 0.98f);
    public Color accentColor = new Color(0.16f, 0.24f, 0.40f, 1f);
    public Color outlineColor = new Color(0.15f, 0.12f, 0.10f, 0.35f);
    public Color hintPanelColor = new Color(0.91f, 0.96f, 1f, 0.96f);
    public Color feedbackSuccessColor = new Color(0.20f, 0.58f, 0.28f, 1f);
    public Color feedbackErrorColor = new Color(0.78f, 0.23f, 0.18f, 1f);
    public string friendBubbleName = "옆 친구";
    public float friendBubbleShowSeconds = 3.2f;
}
