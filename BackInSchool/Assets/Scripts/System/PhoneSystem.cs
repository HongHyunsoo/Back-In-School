using UnityEngine;
using UnityEngine.SceneManagement;
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
    [SerializeField] private AudioClip phoneToggleClip;
    [SerializeField] private AudioClip phoneFocusClip;
    [SerializeField] private AudioClip phoneBackClip;
    [SerializeField] private AudioClip phoneApplyClip;
    [SerializeField] private AudioClip phoneBlipClip;
    [SerializeField] [Range(0f, 1f)] private float phoneUiSfxVolume = 0.85f;

    public AudioClip PhoneButtonClickClip => phoneButtonClickClip;
    public float PhoneButtonClickVolume => phoneButtonClickVolume;
    private AudioSource phoneAudioSource;

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

        EnsureDefaultAudioClips();
        EnsurePhoneAudioSource();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        var dm = FindAnyObjectByType<DialogueManager>();
        if (dm != null)
            DontDestroyOnLoad(dm.gameObject);

        EnsureDefaultAudioClips();
    }

    public void Open()
    {
        bool wasOpen = IsOpen;

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

        if (!wasOpen)
            PlayPhoneToggleSfx();
    }

    public void OpenSettingsOnlyForMinigamePause()
    {
        Open();

        if (phoneUIInstance == null)
            return;

        EnsureRuntimeComponents();
        var appManager = phoneUIInstance.GetComponent<PhoneAppManager>();
        if (appManager != null)
            appManager.OpenSettingsForMinigamePause();
    }

    public void Close()
    {
        if (phoneUIInstance != null && phoneUIInstance.activeSelf)
        {
            phoneUIInstance.SetActive(false);
            var appManager = phoneUIInstance.GetComponent<PhoneAppManager>();
            if (appManager != null)
                appManager.ClearMinigameSettingsOnlyMode();
            PlayPhoneToggleSfx();
        }
    }

    public bool IsOpen => phoneUIInstance != null && phoneUIInstance.activeSelf;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (phoneAudioSource != null)
            phoneAudioSource.Stop();

        if (scene.IsValid() && scene.name == "MainMenu")
        {
            CloseSilentlyForSceneChange();
            return;
        }

        ResetPhoneUiScreenState();
    }

    private void CloseSilentlyForSceneChange()
    {
        if (phoneUIInstance == null)
            return;

        phoneUIInstance.SetActive(false);
        var appManager = phoneUIInstance.GetComponent<PhoneAppManager>();
        if (appManager != null)
        {
            appManager.ClearMinigameSettingsOnlyMode();
            appManager.ResetToHomeForSceneChange();
        }
    }

    private void ResetPhoneUiScreenState()
    {
        if (phoneUIInstance == null)
            return;

        EnsureRuntimeComponents();
        var appManager = phoneUIInstance.GetComponent<PhoneAppManager>();
        if (appManager != null)
            appManager.ResetToHomeForSceneChange();
    }

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

    private void EnsureDefaultAudioClips()
    {
        if (phoneButtonClickClip == null)
            phoneButtonClickClip = AudioSettingsService.LoadResourceClip("SFX/UI/UI_confirm");
        if (phoneToggleClip == null)
            phoneToggleClip = AudioSettingsService.LoadResourceClip("SFX/UI/SFX_Beib");
        if (phoneFocusClip == null)
            phoneFocusClip = AudioSettingsService.LoadResourceClip("SFX/UI/UI_focus");
        if (phoneBackClip == null)
            phoneBackClip = AudioSettingsService.LoadResourceClip("SFX/UI/UI_back");
        if (phoneApplyClip == null)
            phoneApplyClip = AudioSettingsService.LoadResourceClip("SFX/UI/UI_apply");
        if (phoneBlipClip == null)
            phoneBlipClip = AudioSettingsService.LoadResourceClip("SFX/UI/Blip");
    }

    private void EnsurePhoneAudioSource()
    {
        if (phoneAudioSource != null)
            return;

        phoneAudioSource = GetComponent<AudioSource>();
        if (phoneAudioSource == null)
            phoneAudioSource = gameObject.AddComponent<AudioSource>();

        phoneAudioSource.playOnAwake = false;
        phoneAudioSource.loop = false;
        phoneAudioSource.spatialBlend = 0f;
        phoneAudioSource.ignoreListenerPause = true;
    }

    private void PlayPhoneUiSfx(AudioClip clip, float volume)
    {
        EnsurePhoneAudioSource();
        if (phoneAudioSource == null || clip == null)
            return;

        phoneAudioSource.PlayOneShot(clip, AudioSettingsService.ScaleSfx(volume));
    }

    public void PlayPhoneButtonClickSfx()
    {
        EnsureDefaultAudioClips();
        PlayPhoneUiSfx(phoneButtonClickClip, phoneButtonClickVolume);
    }

    public void PlayPhoneToggleSfx()
    {
        EnsureDefaultAudioClips();
        PlayPhoneUiSfx(phoneToggleClip, phoneUiSfxVolume);
    }

    public void PlayPhoneFocusSfx()
    {
        EnsureDefaultAudioClips();
        PlayPhoneUiSfx(phoneFocusClip, phoneUiSfxVolume);
    }

    public void PlayPhoneBackSfx()
    {
        EnsureDefaultAudioClips();
        PlayPhoneUiSfx(phoneBackClip, phoneUiSfxVolume);
    }

    public void PlayPhoneApplySfx()
    {
        EnsureDefaultAudioClips();
        PlayPhoneUiSfx(phoneApplyClip, phoneUiSfxVolume);
    }

    public void PlayPhoneBlipSfx()
    {
        EnsureDefaultAudioClips();
        PlayPhoneUiSfx(phoneBlipClip, phoneUiSfxVolume);
    }
}
