using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ChatMessageItem : MonoBehaviour
{
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text bodyText;

    private LayoutElement layoutElement;
    private RectTransform rectTransform;

    private void Awake()
    {
        EnsureLayoutRefs();
    }

    public void Set(string displayName, Sprite avatar, string body, bool showHeader)
    {
        if (nameText) nameText.text = displayName ?? "";
        if (bodyText) bodyText.text = body ?? "";

        if (avatarImage)
        {
            avatarImage.sprite = avatar;
            avatarImage.enabled = (avatar != null);
        }

        if (nameText) nameText.gameObject.SetActive(showHeader);
        if (avatarImage) avatarImage.gameObject.SetActive(showHeader);

        if (nameText) nameText.ForceMeshUpdate();
        if (bodyText) bodyText.ForceMeshUpdate();

        RefreshLayoutHeight();
    }

    private void RefreshLayoutHeight()
    {
        EnsureLayoutRefs();
        if (rectTransform == null || layoutElement == null)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

        float preferred = ComputeVisualHeight();
        if (preferred <= 0f)
            preferred = 1f;

        layoutElement.minHeight = preferred;
        layoutElement.preferredHeight = preferred;
        layoutElement.flexibleHeight = 0f;
    }

    private float ComputeVisualHeight()
    {
        if (rectTransform == null)
            return 0f;

        bool found = false;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        var allRects = GetComponentsInChildren<RectTransform>(false);
        for (int i = 0; i < allRects.Length; i++)
        {
            var rt = allRects[i];
            if (rt == null || rt == rectTransform)
                continue;

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            for (int c = 0; c < 4; c++)
            {
                Vector3 local = rectTransform.InverseTransformPoint(corners[c]);
                minY = Mathf.Min(minY, local.y);
                maxY = Mathf.Max(maxY, local.y);
                found = true;
            }
        }

        if (!found)
            return rectTransform.rect.height;

        return maxY - minY;
    }

    private void EnsureLayoutRefs()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;
        if (layoutElement == null)
        {
            layoutElement = GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = gameObject.AddComponent<LayoutElement>();
        }
    }
}

