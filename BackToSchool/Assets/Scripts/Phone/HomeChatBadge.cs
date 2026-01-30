using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class HomeChatBadge : MonoBehaviour
{
    [SerializeField] private GameObject badgeRoot;      // 뱃지 동그라미(배경) 오브젝트
    [SerializeField] private TMP_Text badgeText;        // 숫자 텍스트(TMP)

    private void OnEnable()
    {
        Refresh();
        if (ChatService.Instance != null)
            ChatService.Instance.OnChanged += Refresh;
    }

    private void OnDisable()
    {
        if (ChatService.Instance != null)
            ChatService.Instance.OnChanged -= Refresh;
    }

    private void Refresh()
    {
        if (ChatService.Instance == null) return;

        int total = ChatService.Instance.GetTotalUnread();
        bool show = total > 0;

        if (badgeRoot) badgeRoot.SetActive(show);
        if (badgeText) badgeText.text = total.ToString();
    }
}


