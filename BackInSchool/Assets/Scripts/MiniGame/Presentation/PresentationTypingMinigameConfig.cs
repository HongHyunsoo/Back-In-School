using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(
    fileName = "PresentationTypingConfig",
    menuName = "Back in School/Minigame/Presentation Typing Config",
    order = 15)]
public class PresentationTypingMinigameConfig : ScriptableObject
{
    [Serializable]
    public class PresentationCycleDefinition
    {
        public string title = "Cycle";
        public string[] keywords = new[]
        {
            "\uC800\uB294",
            "\uB9CC\uC57D",
            "\uC788\uB2E4\uBA74",
            "\uB3C8",
            "1\uC5B5"
        };
        public string completedSentence = "\uC800\uB294 \uB9CC\uC57D \uB3C8 1\uC5B5\uC774 \uC788\uB2E4\uBA74";
    }

    [Header("Flow")]
    public string[] supportedFlowIds = new[] { "CLASS2_D2" };
    public int penaltyOnGiveUp = 1;
    public float successDelaySeconds = 0.6f;

    [Header("Rounds")]
    public int cycleRepeatCount = 5;
    public List<PresentationCycleDefinition> cycles = new List<PresentationCycleDefinition>();

    [Header("UI")]
    public TMP_FontAsset uiFontAsset;
    public Color dimColor = new Color(0.06f, 0.08f, 0.12f, 0.88f);
    public Color panelColor = new Color(0.97f, 0.95f, 0.90f, 0.98f);
    public Color accentColor = new Color(0.18f, 0.28f, 0.48f, 1f);
    public Color chipColor = new Color(0.89f, 0.91f, 0.96f, 1f);
    public Color completedChipColor = new Color(0.58f, 0.82f, 0.66f, 1f);
    public Color outlineColor = new Color(0.14f, 0.12f, 0.10f, 0.30f);
    public Color successColor = new Color(0.19f, 0.55f, 0.28f, 1f);
    public Color errorColor = new Color(0.78f, 0.22f, 0.18f, 1f);
}
