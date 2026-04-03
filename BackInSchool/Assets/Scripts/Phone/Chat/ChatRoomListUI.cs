using System.Collections.Generic;
using UnityEngine;

public class ChatRoomListUI : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private float itemHeight = 84f;
    [SerializeField] private float itemSpacing = 6f;

    [SerializeField] private Transform contentRoot;
    [SerializeField] private ChatRoomItemUI itemPrefab;
    [SerializeField] private ChatRoomDetailUI detailUI;

    private readonly List<ChatRoomItemUI> spawned = new();

    private void Awake()
    {
        SanitizeContentRoot();
    }

    private void OnEnable()
    {
        SanitizeContentRoot();
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
            Debug.LogWarning("[ChatRoomListUI] ChatService.Instance is missing.");
            return;
        }

        if (contentRoot == null || itemPrefab == null)
        {
            Debug.LogError("[ChatRoomListUI] contentRoot or itemPrefab is not assigned.");
            return;
        }

        if (detailUI == null)
            Debug.LogWarning("[ChatRoomListUI] detailUI is not assigned.");

        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
                Destroy(spawned[i].gameObject);
        }

        spawned.Clear();
        SanitizeContentRoot();

        foreach (var room in ChatService.Instance.GetRooms())
        {
            var item = Instantiate(itemPrefab, contentRoot);
            PrepareItemRect(item.transform as RectTransform, spawned.Count);
            item.Bind(room, detailUI);
            spawned.Add(item);
        }

        UpdateContentHeight();
    }

    private void SanitizeContentRoot()
    {
        if (contentRoot is not RectTransform rect)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, rect.sizeDelta.y);
        rect.localScale = Vector3.one;
    }

    private void PrepareItemRect(RectTransform rect, int index)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -index * (itemHeight + itemSpacing));
        rect.sizeDelta = new Vector2(0f, itemHeight);
        rect.localScale = Vector3.one;
    }

    private void UpdateContentHeight()
    {
        if (contentRoot is not RectTransform rect)
            return;

        float totalHeight = spawned.Count <= 0
            ? 0f
            : spawned.Count * itemHeight + Mathf.Max(0, spawned.Count - 1) * itemSpacing;

        rect.sizeDelta = new Vector2(0f, totalHeight);
    }
}
