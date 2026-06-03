using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class MainMenuBgmController : MonoBehaviour
{
    private const string ControllerObjectName = "__MainMenuBgm";
    private const string MainMenuBgmResource = "SFX/MainMenu";

    [SerializeField] [Range(0f, 1f)] private float bgmVolume = 0.45f;

    private AudioSource audioSource;
    private AudioClip bgmClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneLoadedHandler()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToCurrentMainMenuScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (IsMainMenuScene(scene))
            AttachToMainMenuScene(scene);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (IsMainMenuScene(scene))
            AttachToMainMenuScene(scene);
    }

    private static bool IsMainMenuScene(Scene scene)
    {
        return scene.IsValid() && string.Equals(scene.name, "MainMenu", StringComparison.OrdinalIgnoreCase);
    }

    private static void AttachToMainMenuScene(Scene scene)
    {
        MainMenuBgmController[] existing = Resources.FindObjectsOfTypeAll<MainMenuBgmController>();
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].gameObject.scene == scene)
                return;
        }

        var host = new GameObject(ControllerObjectName);
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<MainMenuBgmController>();
    }

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        bgmClip = AudioSettingsService.LoadResourceClip(MainMenuBgmResource);
    }

    private void OnEnable()
    {
        PlayBgm();
    }

    private void OnDisable()
    {
        if (audioSource != null)
            audioSource.Stop();
    }

    private void Update()
    {
        if (audioSource != null)
            audioSource.volume = AudioSettingsService.ScaleBgm(bgmVolume);
    }

    private void PlayBgm()
    {
        if (audioSource == null || bgmClip == null)
            return;

        audioSource.clip = bgmClip;
        audioSource.volume = AudioSettingsService.ScaleBgm(bgmVolume);
        if (!audioSource.isPlaying)
            audioSource.Play();
    }
}
