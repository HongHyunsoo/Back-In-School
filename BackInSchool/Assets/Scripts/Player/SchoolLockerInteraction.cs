using UnityEngine;
using TMPro;

public class SchoolLockerInteraction : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private TMP_Text interactKeyText;
    [SerializeField] private string interactKeyFormat = "[{0}]";
    [SerializeField] private bool useUnifiedPromptStyle = true;
    [SerializeField] private float promptFontSize = 4f;
    [SerializeField] private float promptWorldScale = 0.08f;
    [SerializeField] private bool onlyMorningBeforeAssembly = true;
    [SerializeField] private string changedToSlippersConversationId = "SLIPPERS_CHANGED";
    [Header("Audio")]
    [SerializeField] private AudioClip changedSfx;
    [SerializeField] [Range(0f, 1f)] private float changedSfxVolume = 0.9f;

    bool isPlayerInRange;
    private KeyCode lastInteractKey = KeyCode.None;
    private AudioSource audioSource;

    private void Start()
    {
        EnsureAudioSource();
        EnsureDefaultAudio();

        if (interactPrompt != null)
            interactPrompt.SetActive(false);

        RefreshInteractPromptText(KeyBindingConfig.Get(KeyBindingConfig.InteractKey, KeyCode.E));
    }

    private void Update()
    {
        KeyCode interactKey = KeyBindingConfig.Get(KeyBindingConfig.InteractKey, KeyCode.E);
        if (interactKey != lastInteractKey)
            RefreshInteractPromptText(interactKey);

        bool canShowPrompt = isPlayerInRange && IsInAllowedFlow();
        if (interactPrompt != null)
            interactPrompt.SetActive(canShowPrompt);

        if (!canShowPrompt)
            return;

        if (!Input.GetKeyDown(interactKey))
            return;

        FlowManager fm = FlowManager.Instance;
        if (fm == null)
            return;

        bool changed = fm.TryChangeToSlippers();
        if (changed)
        {
            Debug.Log("[SchoolLockerInteraction] Changed shoes to slippers.");
            var shoeVisual = FindAnyObjectByType<PlayerShoeVisual>();
            if (shoeVisual != null)
                shoeVisual.ForceRefresh();
            PlayChangedSfx();
            TryPlayChangedMessageDialogue();
        }
    }

    private bool IsInAllowedFlow()
    {
        if (!onlyMorningBeforeAssembly)
            return true;

        return FlowContext.IsMorningBeforeAssemblyFreeRoam();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            isPlayerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            isPlayerInRange = false;

        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    private void RefreshInteractPromptText(KeyCode interactKey)
    {
        lastInteractKey = interactKey;
        if (interactKeyText == null)
            return;

        interactKeyText.enableWordWrapping = false;
        interactKeyText.overflowMode = TextOverflowModes.Overflow;
        float fontSize = useUnifiedPromptStyle ? InteractionPromptStyle.DefaultFontSize : promptFontSize;
        float worldScale = useUnifiedPromptStyle ? InteractionPromptStyle.DefaultWorldScale : promptWorldScale;
        interactKeyText.fontSize = fontSize;
        InteractionPromptStyle.ApplyWorldTextScale(interactKeyText, worldScale);
        interactKeyText.text = string.Format(interactKeyFormat, interactKey.ToString().ToUpperInvariant());
    }

    private void TryPlayChangedMessageDialogue()
    {
        if (string.IsNullOrEmpty(changedToSlippersConversationId))
            return;

        if (LocalizationManager.Instance == null)
            return;

        var lines = LocalizationManager.Instance.GetConversation(changedToSlippersConversationId);
        if (lines == null || lines.Count == 0)
            return;

        var dm = FindAnyObjectByType<DialogueManager>();
        if (dm == null || dm.IsDialogueActive)
            return;

        dm.StartDialogue(changedToSlippersConversationId, null);
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

    private void EnsureDefaultAudio()
    {
        if (changedSfx == null)
            changedSfx = AudioSettingsService.LoadResourceClip("SFX/UI/UI_apply");
    }

    private void PlayChangedSfx()
    {
        EnsureAudioSource();
        EnsureDefaultAudio();

        if (audioSource == null || changedSfx == null)
            return;

        audioSource.PlayOneShot(changedSfx, AudioSettingsService.ScaleSfx(changedSfxVolume));
    }

}

