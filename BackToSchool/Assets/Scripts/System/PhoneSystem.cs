using UnityEngine;

public class PhoneSystem : MonoBehaviour
{
    public static PhoneSystem Instance { get; private set; }

    [Header("Assign in Inspector")]
    public GameObject phoneUIPrefab;
    private GameObject phoneUIInstance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (GetComponent<DialogueBubbleRuntimeFix>() == null)
            gameObject.AddComponent<DialogueBubbleRuntimeFix>();

        // Bootstrap에 있는 DialogueManager를 씬 전환 후에도 유지해서
        // STORY/Health 쪽에서 동일 DialogBox 말풍선을 재사용 가능하게 한다.
        var dm = FindAnyObjectByType<DialogueManager>();
        if (dm != null)
            DontDestroyOnLoad(dm.gameObject);
    }

    private void Start()
    {
        var dm = FindAnyObjectByType<DialogueManager>();
        if (dm != null)
            DontDestroyOnLoad(dm.gameObject);
    }

    public void Open()
    {
        if (phoneUIInstance == null)
        {
            if (phoneUIPrefab == null)
            {
                Debug.LogError("[PhoneSystem] phoneUIPrefab is not assigned.");
                return;
            }

            phoneUIInstance = Instantiate(phoneUIPrefab);
            DontDestroyOnLoad(phoneUIInstance);
        }

        EnsureRuntimeComponents();
        phoneUIInstance.SetActive(true);
    }

    public void Close()
    {
        if (phoneUIInstance != null)
            phoneUIInstance.SetActive(false);
    }

    public bool IsOpen => phoneUIInstance != null && phoneUIInstance.activeSelf;

    private void EnsureRuntimeComponents()
    {
        if (phoneUIInstance == null) return;

        if (phoneUIInstance.GetComponent<PhoneSubwayFlowGate>() == null)
            phoneUIInstance.AddComponent<PhoneSubwayFlowGate>();

        if (phoneUIInstance.GetComponent<PhoneHealthSurveyController>() == null)
            phoneUIInstance.AddComponent<PhoneHealthSurveyController>();

        if (phoneUIInstance.GetComponent<PhoneUiHotfixes>() == null)
            phoneUIInstance.AddComponent<PhoneUiHotfixes>();
    }
}
