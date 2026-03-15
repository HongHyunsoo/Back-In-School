using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

/*
 * 대화 데이터를 Unity Inspector에서 쉽게 작성하고 편집할 수 있는 에디터 도구
 * 
 * 사용 방법:
 * 1. Hierarchy에서 빈 GameObject 생성
 * 2. DialogueDataEditor 스크립트 추가
 * 3. Inspector에서 대화 작성
 * 4. "Save to CSV" 버튼 클릭하여 CSV로 저장
 */
public class DialogueDataEditor : MonoBehaviour
{
    [System.Serializable]
    public class ConversationData
    {
        public string conversationID;
        public List<DialogueLineData> lines = new List<DialogueLineData>();
    }

    [System.Serializable]
    public class DialogueLineData
    {
        public string speakerID;
        public string lineID;
        public string animationTrigger;
        public string soundEffectName;
        public bool hasChoices;
        public List<DialogueChoiceData> choices = new List<DialogueChoiceData>();
    }

    [System.Serializable]
    public class DialogueChoiceData
    {
        public string choiceTextID;
        public string nextConversationID;
        public string sceneToLoad;
        public GameState stateToChange;
    }

    [Header("대화 데이터")]
    public List<ConversationData> conversations = new List<ConversationData>();

    [Header("저장 경로")]
    public string conversationsCSVPath = "Assets/Resources/Conversations.csv";

    // Runtime에 대화 데이터를 실제 DialogueLine으로 변환하여 사용
    public Dictionary<string, List<DialogueLine>> GetConversationDictionary()
    {
        Dictionary<string, List<DialogueLine>> result = new Dictionary<string, List<DialogueLine>>();

        foreach (var convData in conversations)
        {
            List<DialogueLine> lines = new List<DialogueLine>();

            foreach (var lineData in convData.lines)
            {
                DialogueLine line = new DialogueLine
                {
                    speakerID = lineData.speakerID,
                    lineID = lineData.lineID,
                    animationTrigger = lineData.animationTrigger,
                    soundEffectName = lineData.soundEffectName,
                    hasChoices = lineData.hasChoices,
                    choices = new List<DialogueChoice>()
                };

                // 선택지 변환
                if (lineData.hasChoices)
                {
                    foreach (var choiceData in lineData.choices)
                    {
                        DialogueChoice choice = new DialogueChoice
                        {
                            choiceTextID = choiceData.choiceTextID,
                            nextConversationID = choiceData.nextConversationID,
                            sceneToLoad = choiceData.sceneToLoad,
                            stateToChange = choiceData.stateToChange
                        };
                        line.choices.Add(choice);
                    }
                }

                lines.Add(line);
            }

            result[convData.conversationID] = lines;
        }

        return result;
    }

#if UNITY_EDITOR
    [ContextMenu("Save Conversations to CSV")]
    public void SaveToCSV()
    {
        List<string> csvLines = new List<string>();
        csvLines.Add("Conversation_ID,Order,Speaker_ID,Line_ID,AnimationTrigger,SoundEffect,HasChoices,Choices");

        foreach (var conv in conversations)
        {
            for (int i = 0; i < conv.lines.Count; i++)
            {
                var line = conv.lines[i];
                string choicesStr = "";

                if (line.hasChoices && line.choices.Count > 0)
                {
                    List<string> choiceStrs = new List<string>();
                    foreach (var choice in line.choices)
                    {
                        choiceStrs.Add($"{choice.choiceTextID}|{choice.nextConversationID}|{choice.sceneToLoad}|{choice.stateToChange}");
                    }
                    choicesStr = string.Join(";", choiceStrs);
                }

                csvLines.Add($"{conv.conversationID},{i + 1},{line.speakerID},{line.lineID}," +
                            $"{line.animationTrigger},{line.soundEffectName},{line.hasChoices},{choicesStr}");
            }
        }

        File.WriteAllText(conversationsCSVPath, string.Join("\n", csvLines));
        AssetDatabase.Refresh();
        Debug.Log($"대화 데이터를 저장했습니다: {conversationsCSVPath}");
    }

    [ContextMenu("Load Conversations from CSV")]
    public void LoadFromCSV()
    {
        if (!File.Exists(conversationsCSVPath))
        {
            Debug.LogError($"파일을 찾을 수 없습니다: {conversationsCSVPath}");
            return;
        }

        conversations.Clear();
        string[] lines = File.ReadAllLines(conversationsCSVPath);

        Dictionary<string, ConversationData> convDict = new Dictionary<string, ConversationData>();

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i].Trim())) continue;

            string[] columns = lines[i].Split(',');
            if (columns.Length < 4) continue;

            string convID = columns[0].Trim();
            int order = int.Parse(columns[1].Trim());
            string speakerID = columns[2].Trim();
            string lineID = columns[3].Trim();

            if (!convDict.ContainsKey(convID))
            {
                convDict[convID] = new ConversationData { conversationID = convID };
            }

            DialogueLineData lineData = new DialogueLineData
            {
                speakerID = speakerID,
                lineID = lineID
            };

            // 추가 필드 파싱 (있으면)
            if (columns.Length > 4) lineData.animationTrigger = columns[4].Trim();
            if (columns.Length > 5) lineData.soundEffectName = columns[5].Trim();
            if (columns.Length > 6) bool.TryParse(columns[6].Trim(), out lineData.hasChoices);
            if (columns.Length > 7 && !string.IsNullOrEmpty(columns[7]))
            {
                string[] choices = columns[7].Split(';');
                foreach (var choiceStr in choices)
                {
                    string[] choiceParts = choiceStr.Split('|');
                    if (choiceParts.Length >= 4)
                    {
                        lineData.choices.Add(new DialogueChoiceData
                        {
                            choiceTextID = choiceParts[0],
                            nextConversationID = choiceParts[1],
                            sceneToLoad = choiceParts[2],
                            stateToChange = (GameState)System.Enum.Parse(typeof(GameState), choiceParts[3])
                        });
                    }
                }
            }

            convDict[convID].lines.Add(lineData);
        }

        conversations = new List<ConversationData>(convDict.Values);
        Debug.Log($"대화 데이터를 로드했습니다: {conversations.Count}개 대화");
    }
#endif
}




