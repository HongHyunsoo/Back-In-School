using UnityEngine;
using UnityEngine.UI;

public class PhoneSystem : MonoBehaviour
{
    public static PhoneSystem Instance { get; private set; }

    [Header("Assign in Inspector")]
    public GameObject phoneUIPrefab;
    private GameObject phoneUIInstance;

    [Header("Phone UI Scale")]
    public Vector2 phoneUiReferenceResolution = new Vector2(1920f, 1080f);
    [Range(0f, 1f)] public float phoneUiMatchWidthOrHeight = 0.5f;

    [Header("Phone Audio")]
    [SerializeField] private AudioClip phoneButtonClickClip;
    [SerializeField] [Range(0f, 1f)] private float phoneButtonClickVolume = 0.85f;

    public AudioClip PhoneButtonClickClip => phoneButtonClickClip;
    public float PhoneButtonClickVolume => phoneButtonClickVolume;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        PhoneGalleryService.EnsureExists();

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
            NormalizePhoneUICanvas(phoneUIInstance);
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

        if (phoneUIInstance.GetComponent<PhoneUIButtonAudioController>() == null)
            phoneUIInstance.AddComponent<PhoneUIButtonAudioController>();

        if (phoneUIInstance.GetComponent<PhoneSettingsAppController>() == null)
            phoneUIInstance.AddComponent<PhoneSettingsAppController>();

        if (phoneUIInstance.GetComponent<PhoneGalleryAppController>() == null)
            phoneUIInstance.AddComponent<PhoneGalleryAppController>();

        if (phoneUIInstance.GetComponent<PhonePhotoSlotUnlockController>() == null)
            phoneUIInstance.AddComponent<PhonePhotoSlotUnlockController>();
    }

    private void NormalizePhoneUICanvas(GameObject root)
    {
        if (root == null)
            return;

        Canvas postProcessCanvas = root.GetComponent<Canvas>();
        if (postProcessCanvas == null)
            postProcessCanvas = root.GetComponentInChildren<Canvas>(true);

        var canvases = root.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                continue;

            if (canvas == postProcessCanvas)
            {
                var postProcessTarget = canvas.GetComponent<CanvasPostProcessTarget>();
                if (postProcessTarget == null)
                    postProcessTarget = canvas.gameObject.AddComponent<CanvasPostProcessTarget>();

                postProcessTarget.Apply();
            }

            var rt = canvas.transform as RectTransform;
            if (rt != null)
                rt.localScale = Vector3.one;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                continue;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = phoneUiReferenceResolution;
            scaler.matchWidthOrHeight = phoneUiMatchWidthOrHeight;
        }
    }
}
