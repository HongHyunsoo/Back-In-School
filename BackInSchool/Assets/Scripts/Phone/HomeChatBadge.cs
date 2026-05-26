using TMPro;
using UnityEngine;

public class HomeChatBadge : MonoBehaviour
{
    [SerializeField] private GameObject badgeRoot;
    [SerializeField] private TMP_Text badgeText;

    private ChatService subscribedService;
    private CanvasGroup selfCanvasGroup;

    private void OnEnable()
    {
        SubscribeIfNeeded();
        Refresh();
    }

    private void Update()
    {
        SubscribeIfNeeded();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void SubscribeIfNeeded()
    {
        if (subscribedService == ChatService.Instance)
            return;

        Unsubscribe();
        subscribedService = ChatService.Instance;
        if (subscribedService != null)
            subscribedService.OnChanged += Refresh;
    }

    private void Unsubscribe()
    {
        if (subscribedService != null)
            subscribedService.OnChanged -= Refresh;
        subscribedService = null;
    }

    private void Refresh()
    {
        if (ChatService.Instance == null)
            return;

        int total = ChatService.Instance.GetTotalUnread();
        bool show = total > 0;

        SetBadgeVisible(show);
        if (badgeText != null)
            badgeText.text = total.ToString();
    }

    private void SetBadgeVisible(bool show)
    {
        if (badgeRoot == null)
            return;

        if (badgeRoot != gameObject)
        {
            badgeRoot.SetActive(show);
            return;
        }

        if (selfCanvasGroup == null)
            selfCanvasGroup = badgeRoot.GetComponent<CanvasGroup>();
        if (selfCanvasGroup == null)
            selfCanvasGroup = badgeRoot.AddComponent<CanvasGroup>();

        selfCanvasGroup.alpha = show ? 1f : 0f;
        selfCanvasGroup.interactable = false;
        selfCanvasGroup.blocksRaycasts = false;
    }
}
