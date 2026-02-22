using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PhoneManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject phoneUIPanel;
    public KeyCode phoneKey = KeyCode.Tab;

    [Header("Language UI")]
    public Button languageToggleButton;
    public TextMeshProUGUI languageButtonText;

    private bool isPhoneOpen = false;
    private PlayerController playerController;
    private GameManager gameManager;

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null) Debug.LogError("GameManager not found!");

        playerController = gameManager != null ? gameManager.playerController : null;

        if (phoneUIPanel != null)
            phoneUIPanel.SetActive(false);
        isPhoneOpen = false;

        if (languageToggleButton != null)
            languageToggleButton.onClick.AddListener(ToggleLanguage);

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += UpdateLanguageButtonText;

        UpdateLanguageButtonText(LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetCurrentLanguage()
            : Language.Korean);
    }

    void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= UpdateLanguageButtonText;
    }

    void Update()
    {
        phoneKey = KeyBindingConfig.Get(KeyBindingConfig.PhoneKey, KeyCode.Tab);

        if (!Input.GetKeyDown(phoneKey))
            return;

        if (!isPhoneOpen)
        {
            if (CanOpenPhone())
                OpenPhone();
        }
        else
        {
            ClosePhone();
        }
    }

    private bool CanOpenPhone()
    {
        if (gameManager == null)
            return false;

        GameState currentState = gameManager.currentState;
        return currentState == GameState.Lunch_FreeTime ||
               currentState == GameState.AfterSchool ||
               currentState == GameState.Day5_FreeTime;
    }

    private void OpenPhone()
    {
        isPhoneOpen = true;
        if (phoneUIPanel != null)
            phoneUIPanel.SetActive(true);

        if (playerController != null)
            playerController.enabled = false;
    }

    private void ClosePhone()
    {
        isPhoneOpen = false;
        if (phoneUIPanel != null)
            phoneUIPanel.SetActive(false);

        if (playerController != null)
            playerController.enabled = true;
    }

    private void ToggleLanguage()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.ToggleLanguage();
    }

    private void UpdateLanguageButtonText(Language language)
    {
        if (languageButtonText == null)
            return;

        languageButtonText.text = language == Language.Korean
            ? L("UI_LANGUAGE_ENGLISH", "English")
            : L("UI_LANGUAGE_KOREAN", "한국어");
    }

    private string L(string key, string fallback)
    {
        if (LocalizationManager.Instance == null)
            return fallback;

        string value = LocalizationManager.Instance.GetLine(key);
        return value == key ? fallback : value;
    }
}
