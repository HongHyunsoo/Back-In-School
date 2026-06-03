using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class StoryClassroomBgmController : MonoBehaviour
{
    private const string ControllerObjectName = "__StoryClassroomBgm";
    private const string ClassroomBgmResource = "SFX/Ambient/BGM_Classroom";
    private static readonly string[] ExcludedSetNames = { "Street", "Subway" };

    [SerializeField] [Range(0f, 1f)] private float classroomBgmVolume = 1f;

    private Transform[] excludedSets = Array.Empty<Transform>();
    private AudioSource bgmSource;
    private AudioClip classroomBgmClip;
    private bool wasAllowed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneLoadedHandler()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToCurrentStoryScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (string.Equals(scene.name, "STORY", StringComparison.OrdinalIgnoreCase))
            AttachToStoryScene(scene);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (string.Equals(scene.name, "STORY", StringComparison.OrdinalIgnoreCase))
            AttachToStoryScene(scene);
    }

    private static void AttachToStoryScene(Scene scene)
    {
        if (!scene.IsValid())
            return;

        StoryClassroomBgmController[] existing = Resources.FindObjectsOfTypeAll<StoryClassroomBgmController>();
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].gameObject.scene == scene)
                return;
        }

        var host = new GameObject(ControllerObjectName);
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<StoryClassroomBgmController>();
    }

    private void Awake()
    {
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;

        classroomBgmClip = AudioSettingsService.LoadResourceClip(ClassroomBgmResource);
        RefreshExcludedSets();
    }

    private void OnEnable()
    {
        RefreshExcludedSets();
        UpdatePlayback(force: true);
    }

    private void OnDisable()
    {
        if (bgmSource != null)
            bgmSource.Stop();
        wasAllowed = false;
    }

    private void Update()
    {
        UpdatePlayback(force: false);
    }

    private void RefreshExcludedSets()
    {
        var found = new Transform[ExcludedSetNames.Length];
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.gameObject.scene != gameObject.scene)
                continue;

            for (int j = 0; j < ExcludedSetNames.Length; j++)
            {
                if (found[j] == null && string.Equals(candidate.name, ExcludedSetNames[j], StringComparison.Ordinal))
                    found[j] = candidate;
            }
        }

        excludedSets = found;
    }

    private void UpdatePlayback(bool force)
    {
        bool allowed = IsClassroomBgmAllowed();
        if (!force && allowed == wasAllowed)
        {
            if (allowed && bgmSource != null)
                bgmSource.volume = AudioSettingsService.ScaleBgm(classroomBgmVolume);
            return;
        }

        wasAllowed = allowed;
        if (allowed)
            PlayClassroomBgm();
        else
            StopClassroomBgm();
    }

    private bool IsClassroomBgmAllowed()
    {
        if (SceneManager.GetActiveScene().name != "STORY")
            return false;

        if (excludedSets == null || excludedSets.Length == 0)
            RefreshExcludedSets();

        for (int i = 0; i < excludedSets.Length; i++)
        {
            Transform excluded = excludedSets[i];
            if (excluded != null && excluded.gameObject.activeInHierarchy)
                return false;
        }

        return true;
    }

    private void PlayClassroomBgm()
    {
        if (bgmSource == null || classroomBgmClip == null)
            return;

        bgmSource.clip = classroomBgmClip;
        bgmSource.loop = true;
        bgmSource.volume = AudioSettingsService.ScaleBgm(classroomBgmVolume);
        if (!bgmSource.isPlaying)
            bgmSource.Play();
    }

    private void StopClassroomBgm()
    {
        if (bgmSource != null)
            bgmSource.Stop();
    }
}
