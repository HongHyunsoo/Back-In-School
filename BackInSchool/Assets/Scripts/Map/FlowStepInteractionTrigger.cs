using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Completes current Flow step when player interacts inside this trigger.
/// Use this for "go to a specific coordinate and press interact to proceed".
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FlowStepInteractionTrigger : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private TMP_Text interactKeyText;
    [SerializeField] private TMP_FontAsset promptFontAsset;
    [SerializeField] private bool autoCreatePromptTextWhenMissing = true;
    [SerializeField] private string interactKeyFormat = "[{0}]";
    [SerializeField] private bool useUnifiedPromptStyle = true;
    [SerializeField] private float promptFontSize = 3.5f;
    [SerializeField] private float promptWorldScale = 0.08f;

    [Header("Flow Filter")]
    [SerializeField] private bool restrictByFlowType = true;
    [SerializeField] private string requiredFlowType = "FREEROAM";
    [SerializeField] private string requiredFlowIdContains = "";

    [Header("Complete")]
    [SerializeField] private int penaltyDelta = 0;
    [SerializeField] private AudioClip completeSfx;
    [SerializeField] [Range(0f, 1f)] private float completeSfxVolume = 1f;
    [SerializeField] private bool oneShot = true;
    [SerializeField] private bool disableAfterTriggered = true;

    private bool playerInRange;
    private bool consumed;
    private KeyCode lastInteractKey = KeyCode.None;
    private static TMP_FontAsset cachedPromptFont;
    private AudioSource audioSource;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        EnsureAudioSource();
        EnsureDefaultAudio();
        EnsurePromptBinding();
    }

    private void Start()
    {
        EnsurePromptBinding();
        if (interactPrompt != null)
            interactPrompt.SetActive(false);

        RefreshInteractPromptText(KeyBindingConfig.Get(KeyBindingConfig.InteractKey, KeyCode.E));
    }

    private void Update()
    {
        if (consumed && oneShot)
            return;

        KeyCode interactKey = KeyBindingConfig.Get(KeyBindingConfig.InteractKey, KeyCode.E);
        if (interactKey != lastInteractKey)
            RefreshInteractPromptText(interactKey);

        bool canUse = playerInRange && IsFlowAllowed();
        if (interactPrompt != null)
            interactPrompt.SetActive(canUse);

        if (!canUse)
            return;

        if (!Input.GetKeyDown(interactKey))
            return;

        var fm = FlowManager.Instance;
        if (fm == null)
            return;

        PlayCompleteSfx();
        fm.CompleteCurrentEvent(penaltyDelta);
        consumed = true;

        if (disableAfterTriggered)
            gameObject.SetActive(false);
        else if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    private bool IsFlowAllowed()
    {
        if (!restrictByFlowType)
            return true;

        if (!string.Equals(FlowContext.CurrentType, requiredFlowType, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrEmpty(requiredFlowIdContains))
            return true;

        return FlowContext.CurrentId.IndexOf(requiredFlowIdContains, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            playerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = false;
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    private void RefreshInteractPromptText(KeyCode interactKey)
    {
        lastInteractKey = interactKey;
        if (interactKeyText == null)
            return;

        ApplyPromptFont(interactKeyText);
        interactKeyText.enableWordWrapping = false;
        interactKeyText.overflowMode = TextOverflowModes.Overflow;
        float fontSize = useUnifiedPromptStyle ? InteractionPromptStyle.DefaultFontSize : promptFontSize;
        float worldScale = useUnifiedPromptStyle ? InteractionPromptStyle.DefaultWorldScale : promptWorldScale;
        interactKeyText.fontSize = fontSize;
        InteractionPromptStyle.ApplyWorldTextScale(interactKeyText, worldScale);
        interactKeyText.text = string.Format(interactKeyFormat, interactKey.ToString().ToUpperInvariant());
    }

    private void EnsurePromptBinding()
    {
        if (interactPrompt == null)
            return;

        if (interactKeyText == null)
            interactKeyText = interactPrompt.GetComponentInChildren<TMP_Text>(true);

        if (interactKeyText == null && autoCreatePromptTextWhenMissing)
        {
            var go = new GameObject("__AutoInteractKeyText", typeof(TextMeshPro));
            go.transform.SetParent(interactPrompt.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var tmp = go.GetComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            interactKeyText = tmp;
        }

        ApplyPromptFont(interactKeyText);
    }

    private void ApplyPromptFont(TMP_Text text)
    {
        if (text == null)
            return;

        TMP_FontAsset font = ResolvePromptFont(text);
        if (font == null)
            return;

        text.font = font;

        if (text is TextMeshPro worldText && font.material != null)
            worldText.fontSharedMaterial = font.material;
    }

    private TMP_FontAsset ResolvePromptFont(TMP_Text current)
    {
        if (promptFontAsset != null)
            return promptFontAsset;

        if (cachedPromptFont != null)
            return cachedPromptFont;

        TMP_FontAsset[] loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < loaded.Length; i++)
        {
            TMP_FontAsset f = loaded[i];
            if (f == null || string.IsNullOrEmpty(f.name))
                continue;

            if (f.name.Equals("Galmuri11-Bold SDF", StringComparison.OrdinalIgnoreCase) ||
                f.name.IndexOf("Galmuri11-Bold", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                cachedPromptFont = f;
                return f;
            }
        }

        TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text t = texts[i];
            if (t == null || t.font == null || string.IsNullOrEmpty(t.font.name))
                continue;

            string n = t.font.name;
            if (n.Equals("Galmuri11-Bold SDF", StringComparison.OrdinalIgnoreCase) ||
                n.IndexOf("Galmuri11-Bold", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                cachedPromptFont = t.font;
                return cachedPromptFont;
            }
        }

        if (current != null && current.font != null)
            return current.font;

        cachedPromptFont = TMP_Settings.defaultFontAsset;
        return cachedPromptFont;
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
            return;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
    }

    private void PlayCompleteSfx()
    {
        EnsureDefaultAudio();

        if (completeSfx == null)
            return;

        EnsureAudioSource();
        if (audioSource == null)
            return;

        audioSource.PlayOneShot(completeSfx, AudioSettingsService.ScaleSfx(completeSfxVolume));
    }

    private void EnsureDefaultAudio()
    {
        if (completeSfx == null)
            completeSfx = AudioSettingsService.LoadResourceClip("SFX/UI/UI_apply");
    }
}
