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
        if (nameText != null)
        {
            nameText.text = displayName ?? string.Empty;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.gameObject.SetActive(showHeader);
        }

        if (bodyText != null)
        {
            bodyText.text = body ?? string.Empty;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
        }

        if (avatarImage != null)
        {
            avatarImage.sprite = avatar;
            avatarImage.enabled = (avatar != null);
            avatarImage.gameObject.SetActive(showHeader);
        }
    }
}
