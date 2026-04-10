using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PhoneGalleryAppController : MonoBehaviour
{
    [SerializeField] private RectTransform galleryContentRoot;
    [SerializeField] private Button photoTemplate;
    [SerializeField] private TextMeshProUGUI galleryTitleLabel;
    [SerializeField] private TextMeshProUGUI galleryCountLabel;
    [SerializeField] private TextMeshProUGUI galleryEmptyLabel;
    [SerializeField] private GameObject detailPanel;
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
    private readonly List<Button> photoSlots = new();

    private GameObject galleryPanel;
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
    private const string TapToViewKo = "\uB20C\uB7EC\uC11C \uBCF4\uAE30";
    private const string LockedKo = "\uC7A0\uAE40";

    private void Start()
    {
        ResolveTargets();
        HookEvents(true);
        RefreshLabels();
        RefreshEntries();
    }

    private void OnEnable()
    {
        HookEvents(true);
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

    private void ResolveTargets()
    {
        if (galleryPanel == null)
            galleryPanel = FindByName("App_Gallery") ?? FindByName("App_Music");

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
        detailOverlay = detailPanel;
        detailImage = detailPreviewImage;
        detailTitleText = detailTitleLabel;
        detailDescriptionText = detailDescriptionLabel;

        if (photoTemplate != null)
            photoTemplate.gameObject.SetActive(false);

        CollectPhotoSlots();

        sharedFont = ResolveSharedFont();
        EnsureGalleryButtonLabel();
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

        ClearCards();

        var entries = PhoneGalleryService.EnsureExists().GetEntries();
        if (emptyStateText != null)
            emptyStateText.gameObject.SetActive(entries.Count == 0);

        for (int i = 0; i < entries.Count; i++)
        {
            var card = CreateCard(entries[i], i);
            if (card != null)
                cards.Add(card);
        }

        for (int i = entries.Count; i < photoSlots.Count; i++)
            photoSlots[i].gameObject.SetActive(false);
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
            cardRect.localScale = Vector3.one;

        var thumbImage = button.GetComponent<Image>();
        var title = FindText(button.transform, "PhotoName");
        var status = FindSecondaryText(button.transform, title);

        var widgets = new CardWidgets
        {
            entry = entry,
            root = button.gameObject,
            button = button,
            thumbnail = thumbImage,
            title = title,
            status = status
        };

        ApplyCardState(widgets);
        return widgets;
    }

    private void ApplyCardState(CardWidgets widgets)
    {
        bool unlocked = PhoneGalleryService.EnsureExists().IsUnlocked(widgets.entry.entryId);
        Language language = GetLanguage();

        if (widgets.title != null)
            widgets.title.text = widgets.entry.GetTitle(language);
        if (widgets.status != null)
        {
            widgets.status.text = unlocked
                ? (language == Language.English ? "Tap to view" : TapToViewKo)
                : (language == Language.English ? "Locked" : LockedKo);
        }

        widgets.button.onClick.RemoveAllListeners();
        widgets.button.interactable = unlocked;
        widgets.button.onClick.AddListener(() => ShowDetail(widgets.entry));

        Sprite sprite = unlocked ? LoadSprite(widgets.entry.imageResourcePath) : null;
        widgets.thumbnail.sprite = sprite;
        widgets.thumbnail.color = unlocked
            ? new Color(1f, 1f, 1f, sprite == null ? 0.22f : 1f)
            : new Color(0.18f, 0.18f, 0.18f, 0.55f);
    }

    private void ShowDetail(PhoneGalleryEntry entry)
    {
        if (detailOverlay == null || entry == null)
            return;

        Language language = GetLanguage();
        if (detailTitleText != null)
            detailTitleText.text = entry.GetTitle(language);
        if (detailDescriptionText != null)
            detailDescriptionText.text = entry.GetDescription(language);

        Sprite sprite = LoadSprite(entry.imageResourcePath);
        if (detailImage != null)
        {
            detailImage.sprite = sprite;
            detailImage.color = sprite != null ? Color.white : new Color(0.78f, 0.75f, 0.72f, 1f);
        }

        detailOverlay.SetActive(true);
    }

    private Sprite LoadSprite(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        if (spriteCache.TryGetValue(resourcePath, out Sprite cached))
            return cached;

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        spriteCache[resourcePath] = sprite;
        return sprite;
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

    private void CollectPhotoSlots()
    {
        photoSlots.Clear();

        if (contentRoot == null)
            return;

        var buttons = contentRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == photoTemplate)
                continue;

            if (buttons[i].transform.parent != contentRoot)
                continue;

            if (!buttons[i].name.StartsWith("Photo", System.StringComparison.OrdinalIgnoreCase))
                continue;

            photoSlots.Add(buttons[i]);
        }

        photoSlots.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
    }

    private Button GetOrCreatePhotoSlot(int index)
    {
        if (index < photoSlots.Count)
            return photoSlots[index];

        if (photoTemplate == null || contentRoot == null)
            return null;

        Button button = Instantiate(photoTemplate, contentRoot);
        button.gameObject.name = $"Photo_{index + 1}";
        button.gameObject.SetActive(true);
        photoSlots.Add(button);
        return button;
    }

    private RectTransform FindContentRoot()
    {
        if (galleryPanel == null)
            return null;

        var photo = FindPhotoTemplate();
        if (photo != null && photo.transform.parent is RectTransform parent)
            return parent;

        var rects = galleryPanel.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].name == "Content")
                return rects[i];
        }

        return null;
    }

    private Button FindPhotoTemplate()
    {
        if (galleryPanel == null)
            return null;

        var buttons = galleryPanel.GetComponentsInChildren<Button>(true);
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

    private static TextMeshProUGUI FindSecondaryText(Transform root, TextMeshProUGUI primary)
    {
        if (root == null)
            return null;

        var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != primary)
                return texts[i];
        }

        return null;
    }

    private TMP_FontAsset ResolveSharedFont()
    {
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

    private TextMeshProUGUI CreateLabel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        var rect = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = sharedFont;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = new Color(0.14f, 0.14f, 0.16f, 1f);
        text.enableWordWrapping = false;
        return text;
    }
}
