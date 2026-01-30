using System.Collections.Generic;
using UnityEngine;

public class ChatRoomListUI : MonoBehaviour
{
    [SerializeField] private Transform contentRoot;
    [SerializeField] private ChatRoomItemUI itemPrefab;

    // ✅ 추가: 상세 UI를 인스펙터에 꽂아두기
    [SerializeField] private ChatRoomDetailUI detailUI;

    private readonly List<ChatRoomItemUI> spawned = new();

    private void OnEnable()
    {
        Rebuild();
        if (ChatService.Instance != null)
            ChatService.Instance.OnChanged += Rebuild;
    }

    private void OnDisable()
    {
        if (ChatService.Instance != null)
            ChatService.Instance.OnChanged -= Rebuild;
    }

    private void Rebuild()
    {
        if (ChatService.Instance == null)
        {
            Debug.LogWarning("[ChatRoomListUI] ChatService.Instance 가 없습니다.");
            return;
        }

        if (contentRoot == null || itemPrefab == null)
        {
            Debug.LogError("[ChatRoomListUI] contentRoot 또는 itemPrefab 이 인스펙터에 연결되지 않았습니다.");
            return;
        }

        if (detailUI == null)
        {
            Debug.LogWarning("[ChatRoomListUI] detailUI가 인스펙터에 연결되지 않았습니다. 방 아이템 클릭 시 상세 화면이 열리지 않습니다.");
        }

        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i].gameObject);
        spawned.Clear();

        foreach (var room in ChatService.Instance.GetRooms())
        {
            var item = Instantiate(itemPrefab, contentRoot);
            item.Bind(room, detailUI); // ✅ 수정
            spawned.Add(item);
        }
    }
}
