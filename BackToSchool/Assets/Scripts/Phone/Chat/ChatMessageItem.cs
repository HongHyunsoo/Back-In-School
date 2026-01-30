using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ChatMessageItem : MonoBehaviour
{
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text bodyText;

    public void Set(string displayName, Sprite avatar, string body, bool showHeader)
    {
        if (nameText) nameText.text = displayName ?? "";
        if (bodyText) bodyText.text = body ?? "";

        if (avatarImage)
        {
            avatarImage.sprite = avatar;
            avatarImage.enabled = (avatar != null);
        }

        // 내 말풍선이면 header 숨기는 식으로 사용
        if (nameText) nameText.gameObject.SetActive(showHeader);
        if (avatarImage) avatarImage.gameObject.SetActive(showHeader);
    }
}
