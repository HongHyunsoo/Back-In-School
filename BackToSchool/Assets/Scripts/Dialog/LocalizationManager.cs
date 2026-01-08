using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Language
{
    Korean,
    English
}

/*
 * ===================================================================================
 * LocalizationManager (v4.0 - 언어 전환 기능 추가)
 * ===================================================================================
 * - [v4.0 추가 기능]
 * - 1. 런타임 언어 전환 기능
 * - 2. 한국어/영어 지원
 * - 3. CSV에서 영어 컬럼도 로드
 * ===================================================================================
 */
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    // 언어 설정
    public Language currentLanguage = Language.Korean;
    public System.Action<Language> OnLanguageChanged;

    // 1. '번역' (ID -> 번역 텍스트)
    private Dictionary<string, string> nameTableKOR; // 이름 (NAME_PLAYER -> "로봇")
    private Dictionary<string, string> lineTableKOR; // 대사 (LINE_ROBOT_01 -> "으..징그러")
    private Dictionary<string, string> nameTableENG; // 이름 (영어)
    private Dictionary<string, string> lineTableENG; // 대사 (영어)

    // 현재 사용 중인 테이블
    private Dictionary<string, string> nameTable;
    private Dictionary<string, string> lineTable;

    // 2. '대화' (ID -> 대화 스크립트)
    private Dictionary<string, List<DialogueLine>> conversationScript;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadLocalizationFile("Localization");
            LoadConversationFile("Conversations");
            SetLanguage(currentLanguage);
        }
    }

    // --- 1. 'Localization.csv' (번역) 로드 ---
    private void LoadLocalizationFile(string fileName)
    {
        nameTableKOR = new Dictionary<string, string>();
        lineTableKOR = new Dictionary<string, string>();
        nameTableENG = new Dictionary<string, string>();
        lineTableENG = new Dictionary<string, string>();

        TextAsset csvFile = Resources.Load<TextAsset>(fileName);
        if (csvFile == null)
        {
            UnityEngine.Debug.LogError(fileName + ".csv 파일을 Resources 폴더에서 찾을 수 없습니다!");
            return;
        }

        string[] lines = csvFile.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // CSV 파싱 (쉼표로 구분, 따옴표 처리)
            string[] columns = ParseCSVLine(line);
            if (columns.Length >= 3)
            {
                string id = columns[0].Trim();
                string kor = columns[1].Trim().Trim('"');
                string eng = columns.Length >= 3 ? columns[2].Trim().Trim('"') : "";

                if (id.StartsWith("NAME_"))
                {
                    if (!nameTableKOR.ContainsKey(id)) nameTableKOR.Add(id, kor);
                    if (!string.IsNullOrEmpty(eng) && !nameTableENG.ContainsKey(id)) nameTableENG.Add(id, eng);
                }
                else if (id.StartsWith("LINE_"))
                {
                    if (!lineTableKOR.ContainsKey(id)) lineTableKOR.Add(id, kor);
                    if (!string.IsNullOrEmpty(eng) && !lineTableENG.ContainsKey(id)) lineTableENG.Add(id, eng);
                }
            }
        }
    }

    // CSV 라인 파싱 (따옴표 처리)
    private string[] ParseCSVLine(string line)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        string currentField = "";

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentField);
                currentField = "";
            }
            else
            {
                currentField += c;
            }
        }
        result.Add(currentField);

        return result.ToArray();
    }

    // --- 2. 'Conversations.csv' (대화) 로드 ---
    private void LoadConversationFile(string fileName)
    {
        conversationScript = new Dictionary<string, List<DialogueLine>>();

        TextAsset csvFile = Resources.Load<TextAsset>(fileName);
        if (csvFile == null)
        {
            UnityEngine.Debug.LogError(fileName + ".csv 파일을 Resources 폴더에서 찾을 수 없습니다!");
            return;
        }

        string[] lines = csvFile.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] columns = ParseCSVLine(line);
            if (columns.Length >= 4)
            {
                string conversationID = columns[0].Trim();
                string speakerID = columns[2].Trim();
                string lineID = columns[3].Trim();

                // '대화'에 해당 '대화 스크립트(ID)'가 처리되지 않았다면?
                if (!conversationScript.ContainsKey(conversationID))
                {
                    // 새 '대화 스크립트' 리스트를 생성
                    conversationScript.Add(conversationID, new List<DialogueLine>());
                }

                // 해당 '대화 스크립트' 리스트에 한 줄을 추가
                DialogueLine dialogueLine = new DialogueLine 
                { 
                    speakerID = speakerID, 
                    lineID = lineID 
                };
                conversationScript[conversationID].Add(dialogueLine);
            }
        }
    }

    // --- 3. 언어 전환 ---
    public void SetLanguage(Language language)
    {
        currentLanguage = language;

        if (language == Language.Korean)
        {
            nameTable = nameTableKOR;
            lineTable = lineTableKOR;
        }
        else
        {
            nameTable = nameTableENG;
            lineTable = lineTableENG;
        }

        OnLanguageChanged?.Invoke(language);
        UnityEngine.Debug.Log("언어가 변경되었습니다: " + language.ToString());
    }

    public Language GetCurrentLanguage()
    {
        return currentLanguage;
    }

    public void ToggleLanguage()
    {
        SetLanguage(currentLanguage == Language.Korean ? Language.English : Language.Korean);
    }

    // --- 4. '번역'과 '대화' 조회 함수 ---

    public string GetName(string speakerID)
    {
        if (nameTable != null && nameTable.ContainsKey(speakerID)) return nameTable[speakerID];
        return speakerID; // '번역'이 없으면 ID 그대로 반환
    }

    public string GetLine(string lineID)
    {
        if (lineTable != null && lineTable.ContainsKey(lineID)) return lineTable[lineID];
        UnityEngine.Debug.LogWarning("Localization.csv 파일에 '" + lineID + "'가 없습니다!");
        return lineID;
    }

    public List<DialogueLine> GetConversation(string conversationID)
    {
        if (conversationScript != null && conversationScript.ContainsKey(conversationID))
        {
            return conversationScript[conversationID]; // '대화' 반환
        }

        UnityEngine.Debug.LogError("Conversations.csv 대화에서 '" + conversationID + "'를 찾을 수 없습니다!");
        return new List<DialogueLine>(); // 빈 리스트 반환
    }
}
