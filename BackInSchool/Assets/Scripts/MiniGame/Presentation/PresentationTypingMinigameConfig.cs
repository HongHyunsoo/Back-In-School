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

    [Header("Rounds")]
    public int cycleRepeatCount = 5;
    public List<PresentationCycleDefinition> cycles = new List<PresentationCycleDefinition>();

    [Header("Presentation Rules")]
    public float wordFallDuration = 4.5f;
    public float wordSpawnInterval = 1.1f;
    public float sentenceTimeLimit = 7f;
    public int tensionGainOnWordMiss = 10;
    public int tensionGainOnSentenceMiss = 20;
    public int maxTension = 100;
    public float speechBubbleShowSeconds = 1.5f;
    public int penaltyOnFail = 1;

    [Header("UI")]
    public TMP_FontAsset uiFontAsset;
    public Color backgroundColor = new Color(0.97f, 0.97f, 0.95f, 1f);
    public Color frameColor = new Color(0.12f, 0.12f, 0.12f, 1f);
    public Color stageColor = new Color(1f, 1f, 1f, 1f);
    public Color wordColor = new Color(1f, 1f, 1f, 1f);
    public Color wordMissColor = new Color(1f, 0.82f, 0.82f, 1f);
    public Color wordTypedColor = new Color(0.79f, 1f, 0.83f, 1f);
    public Color tensionFillColor = new Color(0.94f, 0.38f, 0.32f, 1f);
    public Color timerFillColor = new Color(0.28f, 0.85f, 0.21f, 1f);
}
