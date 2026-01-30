using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChatRoomItemUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;

    [SerializeField] private GameObject badgeRoot;
    [SerializeField] private TMP_Text badgeText;

    private string roomId;
    private string title;
    private ChatRoomDetailUI detailUI; // ✅ 추가

    public void Bind(ChatRoomState room, ChatRoomDetailUI detail)
    {
        roomId = room.roomId;
        title = room.title;
        detailUI = detail;

        if (titleText) titleText.text = title;

        int unread = room.unreadCount;
        bool show = unread > 0;
        if (badgeRoot) badgeRoot.SetActive(show);
        if (badgeText) badgeText.text = unread.ToString();

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }
    }

    private void OnClick()
    {
        Debug.Log($"[ChatRoomItemUI] Click roomId={roomId}");

        if (ChatService.Instance != null)
            ChatService.Instance.MarkRoomRead(roomId);

        if (detailUI != null)
            detailUI.OpenRoom(roomId, title);
        else
            Debug.LogWarning("[ChatRoomItemUI] detailUI가 null (ChatRoomListUI에서 인스펙터 연결 필요)");
    }
}
