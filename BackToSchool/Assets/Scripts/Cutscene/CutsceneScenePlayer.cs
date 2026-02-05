using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class CutsceneScenePlayer : MonoBehaviour
{
    [Header("Conversation")]
    public string conversationId;
    public GameState nextState = GameState.Lunch_FreeTime;


    [Header("UI")]
    public TMP_Text nameText;
    public TMP_Text bodyText;

    [Header("Systems")]
    public CutsceneCommandRunner runner;      // 씬에 있는 러너 드래그
    public CharacterRegistry registry;        // 씬에 있는 레지스트리 드래그 (runner가 필요로 할 수 있음)

    Queue<DialogueLine> lines = new Queue<DialogueLine>();
    bool canAdvance = true;

    void Start()
    {
        if (LocalizationManager.Instance == null)
        {
            Debug.LogError("[CutsceneScenePlayer] LocalizationManager 없음");
            return;
        }

        if (string.IsNullOrEmpty(conversationId))
        {
            Debug.LogError("[CutsceneScenePlayer] conversationId 비어있음");
            return;
        }

        var convo = LocalizationManager.Instance.GetConversation(conversationId);
        lines.Clear();
        foreach (var l in convo) lines.Enqueue(l);

        Advance();
    }

    void Update()
    {
        if (!canAdvance) return;
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            Advance();
        }
    }

    void Advance()
    {
        if (lines.Count == 0)
        {
            EndCutscene();
            return;
        }

        var line = lines.Dequeue();
        nameText.text = LocalizationManager.Instance.GetName(line.speakerID);

        // lineID -> 실제 텍스트 (태그 포함 가능)
        string raw = LocalizationManager.Instance.GetLine(line.lineID);

        // 태그 실행 + 텍스트 출력
        StartCoroutine(PlayLine(raw));
    }

    IEnumerator PlayLine(string raw)
    {
        canAdvance = false;

        if (runner != null)
        {
            // runner가 내부에서 TagParser/registry를 쓰는 구조면, runner 쪽이 참조를 갖게 세팅해둬야 함
            yield return runner.Execute(raw);
        }

        // 태그 제거하고 텍스트만 표시
        bodyText.text = TagParser.Strip(raw);

        canAdvance = true;
    }

    void EndCutscene()
    {
        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            gm.ChangeState(nextState);


        SceneManager.LoadScene(nextSceneName);
    }
}
