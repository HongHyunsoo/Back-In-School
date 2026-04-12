using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LayoutRebuilder = UnityEngine.UI.LayoutRebuilder;

[DisallowMultipleComponent]
public class PhoneGalleryAppController : MonoBehaviour
{
    [SerializeField] private RectTransform galleryContentRoot;
    [SerializeField] private Button photoTemplate;
    [SerializeField] private TextMeshProUGUI galleryTitleLabel;
    [SerializeField] private TextMeshProUGUI galleryCountLabel;
    [SerializeField] private TextMeshProUGUI galleryEmptyLabel;
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private Button detailBackButton;
    [SerializeField] private Image detailPreviewImage;
    [SerializeField] private TextMeshProUGUI detailTitleLabel;
    [SerializeField] private TextMeshProUGUI detailDescriptionLabel;

    private sealed class CardWidgets
    {
        public PhoneGalleryEntry entry;
        public GameObject root;
        public Button button;
        public Image thumbnail;
        public TextMeshProUGUI title;
        public TextMeshProUGUI status;
    }

    private readonly List<CardWidgets> cards = new();
    private readonly Dictionary<string, Sprite> spriteCache = new();

    private GameObject galleryPanel;
    private GameObject galleryListPanel;
    private Button galleryButton;
    private RectTransform contentRoot;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI countText;
    private TextMeshProUGUI emptyStateText;
    private GameObject detailOverlay;
    private Image detailImage;
    private TextMeshProUGUI detailTitleText;
    private TextMeshProUGUI detailDescriptionText;
    private TMP_FontAsset sharedFont;

    private const string GalleryLabelKo = "\uAC24\uB7EC\uB9AC";
    private const string EmptyKo = "\uC544\uC9C1 \uB4F1\uB85D\uB41C \uAC24\uB7EC\uB9AC \uD56D\uBAA9\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
    private const string UnlockedKo = "\uD574\uAE08";
    private const string LockedKo = "\uC7A0\uAE40";
    private const string LockedTitleKo = "\uC7A0\uAE34 \uC0AC\uC9C4";
    private const string LockedTitleEn = "Locked Photo";
    private const string LockedDescriptionKo = "\uC544\uC9C1 \uD574\uAE08\uB418\uC9C0 \uC54A\uC740 \uC0AC\uC9C4\uC785\uB2C8\uB2E4.";
    private const string LockedDescriptionEn = "This photo has not been unlocked yet.";
    private const string LockedPlaceholderResource = "Gallery/Pictures#Pictures_0";

    private void Start()
    {
        ResolveTargets();
        HookEvents(true);
        PhoneGalleryService.RefreshUnlocksFromSavedState();
        RefreshLabels();
        RefreshEntries();
    }

    private void OnEnable()
    {
        HookEvents(true);
        PhoneGalleryService.RefreshUnlocksFromSavedState();
        RefreshLabels();
        RefreshEntries();
    }

    private void OnDisable()
    {
        HookEvents(false);
    }

    private void HookEvents(bool subscribe)
    {
        var gallery = subscribe
            ? PhoneGalleryService.EnsureExists()
            : PhoneGalleryService.Instance;

        if (gallery == null)
            return;

        if (subscribe)
        {
            gallery.OnChanged -= HandleGalleryChanged;
            gallery.OnChanged += HandleGalleryChanged;

            if (LocalizationManager.Instance != null)
            {
                LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
                LocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;
            }
        }
        else
        {
            gallery.OnChanged -= HandleGalleryChanged;
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
        }
    }

    private void HandleGalleryChanged()
    {
        RefreshLabels();
        RefreshEntries();
    }

    private void HandleLanguageChanged(Language _)
    {
        RefreshLabels();
        RefreshEntries();
    }

    private void HookDetailBackButton()
    {
        if (detailBackButton == null)
            return;

        detailBackButton.onClick.RemoveListener(ShowGalleryList);
        detailBackButton.onClick.AddListener(ShowGalleryList);
    }

    private void ResolveTargets()
    {
        if (galleryPanel == null)
            galleryPanel = FindByName("App_Gallery") ?? FindByName("App_Music");
        if (galleryListPanel == null && galleryPanel != null)
            galleryListPanel = FindByNameUnder(galleryPanel.transform, "Gallery");

        if (galleryButton == null)
        {
            var buttonGo = FindByName("Btn_AppGallery") ?? FindByName("Btn_AppMusic");
            if (buttonGo != null)
                galleryButton = buttonGo.GetComponent<Button>();
        }

        if (galleryPanel != null)
            galleryPanel.name = "App_Gallery";
        if (galleryButton != null)
            galleryButton.name = "Btn_AppGallery";

        if (galleryContentRoot == null)
            galleryContentRoot = FindContentRoot();
        if (photoTemplate == null)
            photoTemplate = FindPhotoTemplate();

        contentRoot = galleryContentRoot;
        titleText = galleryTitleLabel;
        countText = galleryCountLabel;
        emptyStateText = galleryEmptyLabel;
        if (detailPanel == null && galleryPanel != null)
            detailPanel = FindByNameUnder(galleryPanel.transform, "Photo Page");
        if (detailBackButton == null && detailPanel != null)
        {
            GameObject backGo = FindByNameUnder(detailPanel.transform, "Back");
            if (backGo != null)
                detailBackButton = backGo.GetComponent<Button>();
        }
        if (detailPreviewImage == null && detailPanel != null)
            detailPreviewImage = FindImage(detailPanel.transform, "Image");
        if (detailTitleLabel == null && detailPanel != null)
            detailTitleLabel = FindText(detailPanel.transform, "Image_Title");
        if (detailDescriptionLabel == null && detailPanel != null)
            detailDescriptionLabel = FindText(detailPanel.transform, "Image_Outline");

        detailOverlay = detailPanel;
        detailImage = detailPreviewImage;
        detailTitleText = detailTitleLabel;
        detailDescriptionText = detailDescriptionLabel;

        if (photoTemplate != null)
            photoTemplate.gameObject.SetActive(false);

        sharedFont = ResolveSharedFont();
        EnsureGalleryButtonLabel();
        HookDetailBackButton();

        HideDetailView();
    }

    private void RefreshLabels()
    {
        if (galleryPanel == null)
            return;

        Language language = GetLanguage();
        string galleryLabel = language == Language.English ? "Gallery" : GalleryLabelKo;
        string emptyLabel = language == Language.English ? "No gallery entries are configured yet." : EmptyKo;

        if (titleText != null)
            titleText.text = galleryLabel;

        if (emptyStateText != null)
            emptyStateText.text = emptyLabel;

        if (galleryButton != null)
        {
            var buttonText = galleryButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (buttonText != null)
                buttonText.text = galleryLabel;
        }

        var service = PhoneGalleryService.EnsureExists();
        int total = service.GetEntries().Count;
        int unlocked = service.GetUnlockedCount();
        if (countText != null)
        {
            countText.text = language == Language.English
                ? $"Unlocked {unlocked} / {total}"
                : $"{UnlockedKo} {unlocked} / {total}";
        }
    }

    private void RefreshEntries()
    {
        if (contentRoot == null)
            return;

        ShowGalleryList();

        ClearCards();

        var allEntries = PhoneGalleryService.EnsureExists().GetEntries();
        var visibleEntries = new List<PhoneGalleryEntry>();
        for (int i = 0; i < allEntries.Count; i++)
        {
            if (IsEntryUnlocked(allEntries[i]))
                visibleEntries.Add(allEntries[i]);
        }

        if (emptyStateText != null)
            emptyStateText.gameObject.SetActive(visibleEntries.Count == 0);

        for (int i = 0; i < visibleEntries.Count; i++)
        {
            var card = CreateCard(visibleEntries[i], i);
            if (card != null)
                cards.Add(card);
        }
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }

    private CardWidgets CreateCard(PhoneGalleryEntry entry, int index)
    {
        if (contentRoot == null)
            return null;

        Button button = GetOrCreatePhotoSlot(index);
        if (button == null)
            return null;

        button.gameObject.name = "Photo_" + entry.entryId;
        button.gameObject.SetActive(true);

        var cardRect = button.transform as RectTransform;
        if (cardRect != null)
        {
            cardRect.localScale = Vector3.one;
            cardRect.SetSiblingIndex(Mathf.Min(index + 1, contentRoot.childCount - 1));
        }

        var thumbImage = button.GetComponent<Image>();
        var title = FindText(button.transform, "PhotoName");

        var widgets = new CardWidgets
        {
            entry = entry,
            root = button.gameObject,
            button = button,
            thumbnail = thumbImage,
            title = title,
            status = null
        };

        ApplyCardState(widgets);
        return widgets;
    }

    private void ApplyCardState(CardWidgets widgets)
    {
        Language language = GetLanguage();

        if (widgets.title != null)
        {
            widgets.title.text = widgets.entry.GetTitle(language);
            widgets.title.enableWordWrapping = false;
            widgets.title.overflowMode = TextOverflowModes.Truncate;
            widgets.title.alignment = TextAlignmentOptions.Center;
            widgets.title.raycastTarget = false;
        }

        widgets.button.onClick.RemoveAllListeners();
        widgets.button.onClick.AddListener(() => ShowDetail(widgets.entry));
        widgets.button.interactable = true;
        EnsurePointerOpenTrigger(widgets.button.gameObject, widgets.entry);

        if (widgets.thumbnail != null)
        {
            Sprite sprite = LoadSprite(widgets.entry.imageResourcePath);
            widgets.thumbnail.sprite = sprite;
            widgets.thumbnail.preserveAspect = true;
            widgets.thumbnail.color = new Color(1f, 1f, 1f, sprite == null ? 0.22f : 1f);
        }
    }

    private void ShowDetail(PhoneGalleryEntry entry)
    {
        if (detailOverlay == null || entry == null)
            return;

        Language language = GetLanguage();
        bool unlocked = IsEntryUnlocked(entry);

        if (detailTitleText != null)
        {
            detailTitleText.text = unlocked
                ? entry.GetTitle(language)
                : (language == Language.English ? LockedTitleEn : LockedTitleKo);
        }
        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = unlocked
                ? entry.GetDescription(language)
                : (language == Language.English ? LockedDescriptionEn : LockedDescriptionKo);
        }

        Sprite sprite = LoadSprite(unlocked ? entry.imageResourcePath : LockedPlaceholderResource);
        if (detailImage != null)
        {
            detailImage.sprite = sprite;
            detailImage.preserveAspect = true;
            detailImage.color = sprite != null ? Color.white : new Color(0.78f, 0.75f, 0.72f, 1f);
        }

        if (galleryListPanel != null)
            galleryListPanel.SetActive(false);
        detailOverlay.transform.SetAsLastSibling();
        detailOverlay.SetActive(true);
    }

    private Sprite LoadSprite(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        if (spriteCache.TryGetValue(resourcePath, out Sprite cached) && cached != null)
            return cached;

        Sprite sprite = null;
        string normalized = resourcePath.Trim();
        int separatorIndex = normalized.IndexOf('#');
        if (separatorIndex < 0)
            separatorIndex = normalized.IndexOf('@');

        if (separatorIndex > 0 && separatorIndex < normalized.Length - 1)
        {
            string sheetPath = normalized.Substring(0, separatorIndex).Trim();
            string spriteName = normalized.Substring(separatorIndex + 1).Trim();
            Sprite[] sprites = Resources.LoadAll<Sprite>(sheetPath);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && string.Equals(sprites[i].name, spriteName, System.StringComparison.OrdinalIgnoreCase))
                {
                    sprite = sprites[i];
                    break;
                }
            }
        }

        if (sprite == null)
            sprite = Resources.Load<Sprite>(normalized);

        if (sprite != null)
            spriteCache[resourcePath] = sprite;
        return sprite;
    }

    private bool IsEntryUnlocked(PhoneGalleryEntry entry)
    {
        if (entry == null)
            return false;

        if (PhoneGalleryService.EnsureExists().IsUnlocked(entry.entryId))
            return true;

        if (string.IsNullOrWhiteSpace(entry.unlockValue))
            return entry.unlockType == GalleryUnlockType.None;

        switch (entry.unlockType)
        {
            case GalleryUnlockType.None:
                return true;
            case GalleryUnlockType.Conversation:
                return DialogueProgressState.HasCompletedConversation(entry.unlockValue);
            case GalleryUnlockType.Flow:
                return !string.IsNullOrEmpty(FlowContext.CurrentId) &&
                       FlowContext.CurrentId.IndexOf(entry.unlockValue, System.StringComparison.OrdinalIgnoreCase) >= 0;
            default:
                return false;
        }
    }

    private void ClearCards()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].root != null)
                Destroy(cards[i].root);
        }

        cards.Clear();
    }

    private GameObject FindByName(string targetName)
    {
        if (string.IsNullOrEmpty(targetName))
            return null;

        var all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == targetName)
                return all[i].gameObject;
        }

        return null;
    }

    private Button GetOrCreatePhotoSlot(int index)
    {
        if (contentRoot == null)
            return null;

        if (photoTemplate == null)
            return null;

        Button button = Instantiate(photoTemplate, contentRoot);
        button.gameObject.name = $"Photo_{index + 1}";
        button.gameObject.SetActive(true);
        return button;
    }

    private RectTransform FindContentRoot()
    {
        Transform searchRoot = galleryListPanel != null ? galleryListPanel.transform : galleryPanel != null ? galleryPanel.transform : null;
        if (searchRoot == null)
            return null;

        var scrollRects = searchRoot.GetComponentsInChildren<ScrollRect>(true);
        for (int i = 0; i < scrollRects.Length; i++)
        {
            if (scrollRects[i] != null && scrollRects[i].content != null)
                return scrollRects[i].content;
        }

        var rects = searchRoot.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].name == "Content")
                return rects[i];
        }

        return null;
    }

    private Button FindPhotoTemplate()
    {
        Transform searchRoot = contentRoot != null ? contentRoot : galleryPanel != null ? galleryPanel.transform : null;
        if (searchRoot == null)
            return null;

        var buttons = searchRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name == "Photo")
                return buttons[i];
        }

        return null;
    }

    private static TextMeshProUGUI FindText(Transform root, string childName)
    {
        if (root == null)
            return null;

        var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == childName)
                return texts[i];
        }

        return texts.Length > 0 ? texts[0] : null;
    }

    private static GameObject FindByNameUnder(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == targetName)
                return all[i].gameObject;
        }

        return null;
    }

    private static Image FindImage(Transform root, string childName)
    {
        if (root == null)
            return null;

        var images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].name == childName)
                return images[i];
        }

        return null;
    }

    private TMP_FontAsset ResolveSharedFont()
    {
        if (detailTitleLabel != null && detailTitleLabel.font != null)
            return detailTitleLabel.font;

        if (galleryTitleLabel != null && galleryTitleLabel.font != null)
            return galleryTitleLabel.font;

        var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].font != null)
                return texts[i].font;
        }

        return TMP_Settings.defaultFontAsset;
    }

    private void EnsureGalleryButtonLabel()
    {
        if (galleryButton == null)
            return;

        var existing = galleryButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (existing != null)
            return;

        var buttonRect = galleryButton.transform as RectTransform;
        if (buttonRect == null)
            return;

        var labelRect = CreateRect("GalleryLabel", buttonRect, Vector2.zero, Vector2.one, new Vector2(6f, 48f), new Vector2(-6f, -8f));
        var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = sharedFont;
        label.fontSize = 16f;
        label.alignment = TextAlignmentOptions.Bottom;
        label.color = new Color(1f, 1f, 1f, 0.92f);
        label.enableWordWrapping = false;
    }

    private Language GetLanguage()
    {
        return LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetCurrentLanguage()
            : Language.Korean;
    }

    private RectTransform CreateRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
        return rect;
    }

    private void ShowGalleryList()
    {
        if (galleryListPanel != null)
            galleryListPanel.SetActive(true);

        HideDetailView();
    }

    private void HideDetailView()
    {
        if (detailOverlay != null)
            detailOverlay.SetActive(false);
    }

    private void EnsurePointerOpenTrigger(GameObject target, PhoneGalleryEntry entry)
    {
        if (target == null || entry == null)
            return;

        var trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = target.AddComponent<EventTrigger>();

        trigger.triggers ??= new List<EventTrigger.Entry>();
        trigger.triggers.Clear();

        AddPointerEvent(trigger, EventTriggerType.PointerClick, () => ShowDetail(entry));
        AddPointerEvent(trigger, EventTriggerType.PointerUp, () => ShowDetail(entry));
    }

    private static void AddPointerEvent(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction action)
    {
        var entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }
}
