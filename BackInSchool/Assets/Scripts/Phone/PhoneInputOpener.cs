using UnityEngine;

public class PhoneInputOpener : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] private bool allowClose = true;
    [SerializeField] private bool forceCloseOnSceneStart = true;

    public KeyCode ToggleKey => KeyBindingConfig.Get(KeyBindingConfig.PhoneKey, toggleKey);

    private void Start()
    {
        if (forceCloseOnSceneStart && PhoneSystem.Instance != null)
            PhoneSystem.Instance.Close();
    }

    private void Update()
    {
        if (PhoneSystem.Instance == null) return;
        if (MinigameSettingsPauseController.IsPaused) return;

        KeyCode runtimeToggleKey = ToggleKey;
        if (Input.GetKeyDown(runtimeToggleKey))
        {
            if (PhoneSystem.Instance.IsOpen)
            {
                if (allowClose)
                    PhoneSystem.Instance.Close();
            }
            else
            {
                PhoneSystem.Instance.Open();
            }
        }
    }

}
