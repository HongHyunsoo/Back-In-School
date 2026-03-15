using UnityEngine;

public class StorySceneEntry : MonoBehaviour
{
    void Start()
    {
        // STORY 이벤트가 아니면 아무것도 안 함
        if (PlayerPrefs.GetString("FLOW_TYPE", "") != "STORY") return;

        string convoId = PlayerPrefs.GetString("FLOW_ID", "");

        // ✅ 아직 대화ID 없으면 개발 중이니까 그냥 스킵해서 다음으로
        if (string.IsNullOrEmpty(convoId))
        {
            Debug.LogWarning("[StorySceneEntry] STORY인데 FLOW_ID가 비어있음 → 자동 스킵");
            FlowManager.Instance?.CompleteCurrentEvent(0);
            return;
        }

        var dm = FindAnyObjectByType<DialogueManager>();
        if (dm == null)
        {
            Debug.LogError("[StorySceneEntry] DialogueManager 없음 → 자동 스킵");
            FlowManager.Instance?.CompleteCurrentEvent(0);
            return;
        }

        // ✅ CSV에 없는 ID면 멈추지 말고 스킵
        var convo = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetConversation(convoId)
            : null;

        if (convo == null || convo.Count == 0)
        {
            Debug.LogWarning($"[StorySceneEntry] '{convoId}' 대화 없음 → 자동 스킵");
            FlowManager.Instance?.CompleteCurrentEvent(0);
            return;
        }

        dm.StartDialogue(convoId, null);
    }
}
