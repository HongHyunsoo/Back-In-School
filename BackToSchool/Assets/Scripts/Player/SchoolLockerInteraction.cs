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

    bool isPlayerInRange;
    private KeyCode lastInteractKey = KeyCode.None;

    private void Start()
    {
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
            TryPlayChangedMessageDialogue();
        }
    }

    private bool IsInAllowedFlow()
    {
        if (!onlyMorningBeforeAssembly)
            return true;

        string flowType = PlayerPrefs.GetString("FLOW_TYPE", "");
        if (flowType != "FREEROAM")
            return false;

        string flowId = PlayerPrefs.GetString("FLOW_ID", "");
        return string.IsNullOrEmpty(flowId) || flowId.Contains("BEFORE_ASSEMBLY");
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

}

