using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(
    fileName = "MathMinigameConfig",
    menuName = "Back in School/Minigame/Math Config",
    order = 14)]
public class MathMinigameConfig : ScriptableObject
{
    [System.Serializable]
    public class EnglishMatchingPairDefinition
    {
        public string word;
        public string meaning;
    }

    [System.Serializable]
    public class EnglishOrderingQuestionDefinition
    {
        [TextArea(2, 5)]
        public string prompt = "다음 단어를 올바른 순서로 배열하시오.";
        [TextArea(2, 6)]
        public string hintText = string.Empty;
        public string[] shuffledWords = new string[0];
        public string[] correctOrder = new string[0];
        [TextArea(2, 4)]
        public string answerSentence = string.Empty;
    }

    [System.Serializable]
    public class EnglishTrueFalseQuestionDefinition
    {
        [TextArea(2, 5)]
        public string prompt = "다음 문장이 맞으면 True, 틀리면 False를 고르시오.";
        [TextArea(2, 4)]
        public string statement = string.Empty;
        public bool correctAnswer = true;
        [TextArea(2, 6)]
        public string hintText = string.Empty;
        [TextArea(2, 4)]
        public string explanation = string.Empty;
    }

    [System.Serializable]
    public class EnglishListeningBlankQuestionDefinition
    {
        [TextArea(2, 5)]
        public string prompt = "음성을 듣고 빈칸에 들어갈 알맞은 단어를 고르시오.";
        [TextArea(2, 8)]
        public string hintText = string.Empty;
        public string sentenceWithBlank = string.Empty;
        public AudioClip voiceClip;
        public string[] choices = new string[0];
        public int correctChoiceIndex;
        [TextArea(2, 4)]
        public string completedSentence = string.Empty;
    }

    [Header("Flow")]
    public string[] supportedFlowIds = new[] { "CLASS1_D2", "AFTERSCHOOL_ENGLISH_D1" };
    public int penaltyOnGiveUp = 1;
    public float correctAnswerDelaySeconds = 0.55f;

    [Header("Questions")]
    public List<MathMinigameController.MathQuestionDefinition> questions = new List<MathMinigameController.MathQuestionDefinition>();

    [Header("AfterSchool English")]
    public string afterSchoolEnglishFlowId = "AFTERSCHOOL_ENGLISH_D1";
    public string englishMatchingTitle = "알맞은 짝을 찾아요";
    [TextArea(2, 5)]
    public string englishMatchingDescription = "영단어와 알맞은 뜻을 찾아 선으로 이어 보세요.";
    [TextArea(4, 16)]
    public string englishMatchingHint = string.Empty;
    public List<EnglishMatchingPairDefinition> englishMatchingPairs = new List<EnglishMatchingPairDefinition>();
    public float englishMatchSuccessDelaySeconds = 0.45f;
    public string englishOrderingTitle = "다음 단어를 올바른 순서로 배열하시오.";
    public EnglishOrderingQuestionDefinition englishOrderingQuestion = new EnglishOrderingQuestionDefinition();
    public string englishTrueFalseTitle = "True or False";
    public EnglishTrueFalseQuestionDefinition englishTrueFalseQuestion = new EnglishTrueFalseQuestionDefinition();
    public List<EnglishTrueFalseQuestionDefinition> englishTrueFalseQuestions = new List<EnglishTrueFalseQuestionDefinition>();
    public string englishListeningTitle = "듣고 알맞은 단어를 고르시오.";
    public EnglishListeningBlankQuestionDefinition englishListeningQuestion = new EnglishListeningBlankQuestionDefinition();
    public List<EnglishListeningBlankQuestionDefinition> englishListeningQuestions = new List<EnglishListeningBlankQuestionDefinition>();

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
