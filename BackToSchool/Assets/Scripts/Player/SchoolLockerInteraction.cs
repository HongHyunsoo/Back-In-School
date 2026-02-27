using UnityEngine;

public class SchoolLockerInteraction : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private bool onlyMorningBeforeAssembly = true;
    [SerializeField] private string changedToSlippersConversationId = "SLIPPERS_CHANGED";

    bool isPlayerInRange;

    private void Start()
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    private void Update()
    {
        KeyCode interactKey = KeyBindingConfig.Get(KeyBindingConfig.InteractKey, KeyCode.E);

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

