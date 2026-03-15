using UnityEngine;

public class PhoneInputOpener : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] private bool allowClose = true;
    [SerializeField] private bool forceCloseOnSceneStart = true;

    private void Start()
    {
        if (forceCloseOnSceneStart && PhoneSystem.Instance != null)
            PhoneSystem.Instance.Close();
    }

    private void Update()
    {
        if (PhoneSystem.Instance == null) return;

        if (Input.GetKeyDown(toggleKey))
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
