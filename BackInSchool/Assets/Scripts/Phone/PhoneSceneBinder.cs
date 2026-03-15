using UnityEngine;

public enum PhoneSceneMode
{
    None,
    ForceOpen,
    ForceClosed
}

public class PhoneSceneBinder : MonoBehaviour
{
    [SerializeField] private PhoneSceneMode mode = PhoneSceneMode.None;

    private void Start()
    {
        var phone = PhoneSystem.Instance != null
            ? PhoneSystem.Instance
            : FindAnyObjectByType<PhoneSystem>();

        if (phone == null)
        {
            Debug.LogError("[PhoneSceneBinder] PhoneSystem이 없음. Bootstrap부터 시작했는지 확인!");
            return;
        }

        switch (mode)
        {
            case PhoneSceneMode.ForceOpen:
                phone.Open();
                break;
            case PhoneSceneMode.ForceClosed:
                phone.Close();
                break;
            case PhoneSceneMode.None:
            default:
                break;
        }

        // 안전: 혹시 OverlayLock 켜져있던 상태면 풀기
        var app = FindAnyObjectByType<PhoneAppManager>();
        if (app != null) app.SetLocked(false);
    }
}
