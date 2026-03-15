using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PhoneGalleryAppController : MonoBehaviour
{
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
    private Button galleryButton;
    private RectTransform runtimeRoot;
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
        BuildUiIfNeeded();
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
        var gallery = PhoneGalleryService.EnsureExists();

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

        sharedFont = ResolveSharedFont();
        EnsureGalleryButtonLabel();
    }

    private void BuildUiIfNeeded()
    {
        if (galleryPanel == null)
            return;

        if (runtimeRoot == null)
        {
            Transform existing = galleryPanel.transform.Find("__GalleryRuntimeRoot");
            runtimeRoot = existing as RectTransform;
        }

        if (runtimeRoot != null)
            return;

        runtimeRoot = CreateRect("__GalleryRuntimeRoot", galleryPanel.transform as RectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        titleText = CreateLabel("Title", runtimeRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -24f), new Vector2(-24f, -76f), 34, FontStyles.Bold, TextAlignmentOptions.Left);
        countText = CreateLabel("Count", runtimeRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -76f), new Vector2(-24f, -118f), 20, FontStyles.Normal, TextAlignmentOptions.Left);

        var scrollRoot = CreateRect("ScrollRoot", runtimeRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 24f), new Vector2(-24f, -132f));
        var viewport = CreateRect("Viewport", scrollRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0.96f, 0.95f, 0.92f, 0.08f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        contentRoot = CreateRect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));
        contentRoot.pivot = new Vector2(0.5f, 1f);
        var grid = contentRoot.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(230f, 220f);
        grid.spacing = new Vector2(16f, 16f);
        grid.padding = new RectOffset(0, 0, 0, 16);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;

        var fitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = contentRoot;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        emptyStateText = CreateLabel("EmptyState", runtimeRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(48f, 180f), new Vector2(-48f, -180f), 24, FontStyles.Italic, TextAlignmentOptions.Center);
        emptyStateText.alpha = 0.75f;

        BuildDetailOverlay();
    }

    private void BuildDetailOverlay()
    {
        if (runtimeRoot == null || detailOverlay != null)
            return;

        var overlayRect = CreateRect("DetailOverlay", runtimeRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        detailOverlay = overlayRect.gameObject;
        var overlayBg = detailOverlay.AddComponent<Image>();
        overlayBg.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);

        var closeButtonRect = CreateRect("CloseButton", overlayRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-84f, -84f), new Vector2(-24f, -24f));
        var closeImage = closeButtonRect.gameObject.AddComponent<Image>();
        closeImage.color = new Color(0.95f, 0.35f, 0.35f, 1f);
        var closeButton = closeButtonRect.gameObject.AddComponent<Button>();
        closeButton.onClick.AddListener(() => detailOverlay.SetActive(false));
        var closeLabel = CreateLabel("CloseLabel", closeButtonRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 26, FontStyles.Bold, TextAlignmentOptions.Center);
        closeLabel.text = "X";

        var frame = CreateRect("DetailFrame", overlayRect, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.9f), Vector2.zero, Vector2.zero);
        var frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.color = new Color(0.97f, 0.95f, 0.9f, 1f);

        var imageRect = CreateRect("DetailImage", frame, new Vector2(0f, 0.43f), new Vector2(1f, 1f), new Vector2(22f, -22f), new Vector2(-22f, -22f));
        detailImage = imageRect.gameObject.AddComponent<Image>();
        detailImage.color = new Color(0.76f, 0.73f, 0.68f, 1f);
        detailImage.preserveAspect = true;

        detailTitleText = CreateLabel("DetailTitle", frame, new Vector2(0f, 0.22f), new Vector2(1f, 0.38f), new Vector2(24f, 0f), new Vector2(-24f, 0f), 30, FontStyles.Bold, TextAlignmentOptions.Left);
        detailDescriptionText = CreateLabel("DetailDescription", frame, new Vector2(0f, 0f), new Vector2(1f, 0.22f), new Vector2(24f, 18f), new Vector2(-24f, -18f), 22, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        detailDescriptionText.enableWordWrapping = true;

        detailOverlay.SetActive(false);
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
            cards.Add(CreateCard(entries[i]));
    }

    private CardWidgets CreateCard(PhoneGalleryEntry entry)
    {
        var cardRect = CreateRect("Card_" + entry.entryId, contentRoot, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        cardRect.sizeDelta = new Vector2(230f, 220f);

        var bg = cardRect.gameObject.AddComponent<Image>();
        bg.color = new Color(0.94f, 0.92f, 0.88f, 1f);

        var button = cardRect.gameObject.AddComponent<Button>();

        var thumbRect = CreateRect("Thumb", cardRect, new Vector2(0f, 0.36f), new Vector2(1f, 1f), new Vector2(12f, -12f), new Vector2(-12f, -12f));
        var thumbImage = thumbRect.gameObject.AddComponent<Image>();
        thumbImage.color = new Color(0.78f, 0.75f, 0.72f, 1f);
        thumbImage.preserveAspect = true;

        var title = CreateLabel("CardTitle", cardRect, new Vector2(0f, 0.16f), new Vector2(1f, 0.32f), new Vector2(14f, 0f), new Vector2(-14f, 0f), 20, FontStyles.Bold, TextAlignmentOptions.Left);
        var status = CreateLabel("CardStatus", cardRect, new Vector2(0f, 0f), new Vector2(1f, 0.14f), new Vector2(14f, 10f), new Vector2(-14f, -8f), 18, FontStyles.Normal, TextAlignmentOptions.Left);
        status.alpha = 0.7f;

        var widgets = new CardWidgets
        {
            entry = entry,
            root = cardRect.gameObject,
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

        widgets.title.text = widgets.entry.GetTitle(language);
        widgets.status.text = unlocked
            ? (language == Language.English ? "Tap to view" : TapToViewKo)
            : (language == Language.English ? "Locked" : LockedKo);

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
        detailTitleText.text = entry.GetTitle(language);
        detailDescriptionText.text = entry.GetDescription(language);

        Sprite sprite = LoadSprite(entry.imageResourcePath);
        detailImage.sprite = sprite;
        detailImage.color = sprite != null ? Color.white : new Color(0.78f, 0.75f, 0.72f, 1f);

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
