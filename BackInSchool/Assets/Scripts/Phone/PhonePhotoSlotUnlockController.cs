using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PhonePhotoSlotUnlockController : MonoBehaviour
{
    private const string AdultMorningConversationId = "DAY1_MOR_ADULT";
    private const string MinumLunchConversationId = "DAY1_LUNCH_MINUM";

    private const string AdultPhotoResource = "Gallery/Pictures#Pictures_2";
    private const string MinumPhotoResource = "Gallery/Pictures#Pictures_3";

    private const string PhotoNameObjectName = "PhotoName";
    private const string PhotoPageObjectName = "Photo Page";
    private const string PhotoPageImageObjectName = "Image";
    private const string PhotoPageTitleObjectName = "Image_Title";
    private const string PhotoPageOutlineObjectName = "Image_Outline";
    private const string PhotoPageBackObjectName = "Back";

    private const string AdultPhotoTitleKo = "엉인이 레전드 짤";
    private const string AdultPhotoTitleEn = "Adult's Legendary Pic";
    private const string AdultPhotoDescriptionKo = "아침에 ADULT와의 대화를 마친 뒤 저장된 사진입니다.";
    private const string AdultPhotoDescriptionEn = "A photo saved after finishing the morning Adult conversation.";

    private const string MinumPhotoTitleKo = "미눔이 잔다";
    private const string MinumPhotoTitleEn = "Sleeping Minum";
    private const string MinumPhotoDescriptionKo = "점심시간 MINUM과의 대화를 마친 뒤 저장된 사진입니다.";
    private const string MinumPhotoDescriptionEn = "A photo saved after finishing the lunch conversation with Minum.";

    private sealed class SlotInfo
    {
        public Button button;
        public Image image;
        public TMP_Text title;
        public string descriptionKo;
        public string descriptionEn;
    }

    private readonly Dictionary<Button, SlotInfo> slotInfos = new();

    private Image photo01Image;
    private TMP_Text photo01NameText;
    private Sprite defaultPhoto01Sprite;
    private string defaultPhoto01Title;

    private Image photo02Image;
    private TMP_Text photo02NameText;
    private Sprite defaultPhoto02Sprite;
    private string defaultPhoto02Title;

    private bool cachedDefaults;
    private Sprite adultUnlockedSprite;
    private Sprite minumUnlockedSprite;

    private GameObject photoPage;
    private Image photoPageImage;
    private TMP_Text photoPageTitle;
    private TMP_Text photoPageOutline;
    private Button photoPageBackButton;

    private void OnEnable()
    {
        DialogueManager.DialogueConversationCompleted -= HandleDialogueConversationCompleted;
        DialogueManager.DialogueConversationCompleted += HandleDialogueConversationCompleted;

        ResolveDetailPage();
        HookPhotoSlotButtons();
        RefreshPhotoSlots();
    }

    private void OnDisable()
    {
        DialogueManager.DialogueConversationCompleted -= HandleDialogueConversationCompleted;

        if (photoPageBackButton != null)
            photoPageBackButton.onClick.RemoveListener(HidePhotoPage);
    }

    private void HandleDialogueConversationCompleted(string conversationId)
    {
        if (!string.Equals(conversationId, AdultMorningConversationId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(conversationId, MinumLunchConversationId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RefreshPhotoSlots();
    }

    private void RefreshPhotoSlots()
    {
        ResolvePhoto01References();
        ResolvePhoto02References();
        ResolveDetailPage();
        HookPhotoSlotButtons();

        if (DialogueProgressState.HasCompletedConversation(AdultMorningConversationId))
            ApplyAdultMorningPhoto();
        else
            RestoreDefaultPhoto01();

        if (DialogueProgressState.HasCompletedConversation(MinumLunchConversationId))
            ApplyMinumLunchPhoto();
        else
            RestoreDefaultPhoto02();
    }

    private void ResolvePhoto01References()
    {
        if (photo01Image != null && photo01NameText != null)
            return;

        Transform photo01 = FindNamedChildRecursive(transform, "Photo01");
        if (photo01 == null)
            return;

        if (photo01Image == null)
            photo01Image = photo01.GetComponent<Image>();

        if (photo01NameText == null)
        {
            Transform name = FindNamedChildRecursive(photo01, PhotoNameObjectName);
            if (name != null)
                photo01NameText = name.GetComponent<TMP_Text>();
        }

        CacheDefaultsIfNeeded();
    }

    private void ResolvePhoto02References()
    {
        if (photo02Image != null && photo02NameText != null)
            return;

        Transform photo02 = FindNamedChildRecursive(transform, "Photo02");
        if (photo02 == null)
            return;

        if (photo02Image == null)
            photo02Image = photo02.GetComponent<Image>();

        if (photo02NameText == null)
        {
            Transform name = FindNamedChildRecursive(photo02, PhotoNameObjectName);
            if (name != null)
                photo02NameText = name.GetComponent<TMP_Text>();
        }

        CacheDefaultsIfNeeded();
    }

    private void CacheDefaultsIfNeeded()
    {
        if (cachedDefaults)
            return;

        if (photo01Image != null)
            defaultPhoto01Sprite = photo01Image.sprite;
        if (photo01NameText != null)
            defaultPhoto01Title = photo01NameText.text;

        if (photo02Image != null)
            defaultPhoto02Sprite = photo02Image.sprite;
        if (photo02NameText != null)
            defaultPhoto02Title = photo02NameText.text;

        cachedDefaults = true;
    }

    private void ResolveDetailPage()
    {
        if (photoPage == null)
            photoPage = FindNamedChildRecursive(transform, PhotoPageObjectName)?.gameObject;

        if (photoPage != null && photoPageImage == null)
        {
            Transform image = FindNamedChildRecursive(photoPage.transform, PhotoPageImageObjectName);
            if (image != null)
                photoPageImage = image.GetComponent<Image>();
        }

        if (photoPage != null && photoPageTitle == null)
        {
            Transform title = FindNamedChildRecursive(photoPage.transform, PhotoPageTitleObjectName);
            if (title != null)
                photoPageTitle = title.GetComponent<TMP_Text>();
        }

        if (photoPage != null && photoPageOutline == null)
        {
            Transform outline = FindNamedChildRecursive(photoPage.transform, PhotoPageOutlineObjectName);
            if (outline != null)
                photoPageOutline = outline.GetComponent<TMP_Text>();
        }

        if (photoPage != null && photoPageBackButton == null)
        {
            Transform back = FindNamedChildRecursive(photoPage.transform, PhotoPageBackObjectName);
            if (back != null)
                photoPageBackButton = back.GetComponent<Button>();
        }

        if (photoPageBackButton != null)
        {
            photoPageBackButton.onClick.RemoveListener(HidePhotoPage);
            photoPageBackButton.onClick.AddListener(HidePhotoPage);
        }
    }

    private void HookPhotoSlotButtons()
    {
        slotInfos.Clear();
        CachePhotoSlot("Photo01", AdultPhotoDescriptionKo, AdultPhotoDescriptionEn);
        CachePhotoSlot("Photo02", MinumPhotoDescriptionKo, MinumPhotoDescriptionEn);
    }

    private void CachePhotoSlot(string slotName, string descriptionKo, string descriptionEn)
    {
        Transform slot = FindNamedChildRecursive(transform, slotName);
        if (slot == null)
            return;

        Button button = slot.GetComponent<Button>();
        Image image = slot.GetComponent<Image>();
        TMP_Text title = null;

        Transform titleTransform = FindNamedChildRecursive(slot, PhotoNameObjectName);
        if (titleTransform != null)
            title = titleTransform.GetComponent<TMP_Text>();

        if (button == null || image == null)
            return;

        var info = new SlotInfo
        {
            button = button,
            image = image,
            title = title,
            descriptionKo = descriptionKo,
            descriptionEn = descriptionEn,
        };

        slotInfos[button] = info;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => ShowPhotoPage(info));
    }

    private void ShowPhotoPage(SlotInfo info)
    {
        ResolveDetailPage();
        if (photoPage == null || photoPageImage == null || info == null || info.image == null)
            return;

        photoPage.SetActive(true);
        photoPage.transform.SetAsLastSibling();
        photoPageImage.sprite = info.image.sprite;
        photoPageImage.color = info.image.color;
        photoPageImage.preserveAspect = true;

        if (photoPageTitle != null)
            photoPageTitle.text = info.title != null ? info.title.text : string.Empty;

        if (photoPageOutline != null)
            photoPageOutline.text = IsEnglish() ? info.descriptionEn : info.descriptionKo;
    }

    private void HidePhotoPage()
    {
        if (photoPage != null)
            photoPage.SetActive(false);
    }

    private void ApplyAdultMorningPhoto()
    {
        Sprite sprite = LoadSpriteResource(AdultPhotoResource, ref adultUnlockedSprite);
        if (sprite != null && photo01Image != null)
        {
            photo01Image.sprite = sprite;
            photo01Image.preserveAspect = true;
            photo01Image.color = Color.white;
        }

        if (photo01NameText != null)
            photo01NameText.text = IsEnglish() ? AdultPhotoTitleEn : AdultPhotoTitleKo;
    }

    private void ApplyMinumLunchPhoto()
    {
        Sprite sprite = LoadSpriteResource(MinumPhotoResource, ref minumUnlockedSprite);
        if (sprite != null && photo02Image != null)
        {
            photo02Image.sprite = sprite;
            photo02Image.preserveAspect = true;
            photo02Image.color = Color.white;
        }

        if (photo02NameText != null)
            photo02NameText.text = IsEnglish() ? MinumPhotoTitleEn : MinumPhotoTitleKo;
    }

    private void RestoreDefaultPhoto01()
    {
        if (!cachedDefaults)
            return;

        if (photo01Image != null)
        {
            photo01Image.sprite = defaultPhoto01Sprite;
            photo01Image.preserveAspect = true;
        }

        if (photo01NameText != null)
            photo01NameText.text = defaultPhoto01Title;
    }

    private void RestoreDefaultPhoto02()
    {
        if (!cachedDefaults)
            return;

        if (photo02Image != null)
        {
            photo02Image.sprite = defaultPhoto02Sprite;
            photo02Image.preserveAspect = true;
        }

        if (photo02NameText != null)
            photo02NameText.text = defaultPhoto02Title;
    }

    private Sprite LoadSpriteResource(string resourcePath, ref Sprite cache)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        if (cache != null)
            return cache;

        string path = resourcePath.Trim();
        string spriteName = null;
        int split = path.IndexOf('#');
        if (split >= 0)
        {
            spriteName = path.Substring(split + 1).Trim();
            path = path.Substring(0, split).Trim();
        }

        if (string.IsNullOrEmpty(path))
            return null;

        if (!string.IsNullOrEmpty(spriteName))
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(path);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && string.Equals(sprites[i].name, spriteName, StringComparison.OrdinalIgnoreCase))
                {
                    cache = sprites[i];
                    return cache;
                }
            }
        }

        cache = Resources.Load<Sprite>(path);
        return cache;
    }

    private bool IsEnglish()
    {
        return LocalizationManager.Instance != null && LocalizationManager.Instance.currentLanguage == Language.English;
    }

    private Transform FindNamedChildRecursive(Transform root, string nameToFind)
    {
        if (root == null || string.IsNullOrEmpty(nameToFind))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
                continue;

            if (child.name == nameToFind)
                return child;

            Transform nested = FindNamedChildRecursive(child, nameToFind);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
