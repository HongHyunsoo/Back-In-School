using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class StoryStreetAudioController : MonoBehaviour
{
    private const string StreetObjectName = "Street";
    private const string CarWhooshResource = "SFX/FREEROAM_SFX/Transition_Corrider_Sfx";
    private const string FootstepResourcePrefix = "SFX/Char/FootStep/SFX_FootStep_";

    [SerializeField] [Range(0f, 1f)] private float carWhooshVolume = 0.35f;
    [SerializeField] [Range(0f, 1f)] private float footstepVolume = 0.28f;
    [SerializeField] [Min(0.1f)] private float footstepIntervalSeconds = 0.42f;
    [SerializeField] private float carPassingWorldX = 0f;

    private readonly List<CarTileState> carTiles = new();
    private readonly List<AudioClip> footstepClips = new();
    private AudioSource carSource;
    private AudioSource footstepSource;
    private AudioClip carWhooshClip;
    private float nextFootstepAt;
    private int lastFootstepIndex = -1;
    private bool wasStreetActive;

    private sealed class CarTileState
    {
        public Transform tile;
        public float previousWorldX;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneLoadedHandler()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (!string.Equals(scene.name, "STORY", StringComparison.OrdinalIgnoreCase))
            return;

        AttachToStreet(scene);
    }

    private static void AttachToStreet(Scene scene)
    {
        if (!scene.IsValid())
            return;

        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null ||
                candidate.gameObject.scene != scene ||
                !string.Equals(candidate.name, StreetObjectName, StringComparison.Ordinal))
                continue;

            if (candidate.GetComponent<StoryStreetAudioController>() == null)
                candidate.gameObject.AddComponent<StoryStreetAudioController>();
            return;
        }
    }

    private void Awake()
    {
        carSource = CreateAudioSource("StreetCarSfx");
        footstepSource = CreateAudioSource("StreetFootstepSfx");
        carWhooshClip = AudioSettingsService.LoadResourceClip(CarWhooshResource);
        LoadFootsteps();
        CacheCarTiles();
    }

    private void Update()
    {
        bool streetActive = gameObject.activeInHierarchy;
        if (!streetActive)
        {
            wasStreetActive = false;
            return;
        }

        if (!wasStreetActive)
        {
            RefreshCarTilePositions();
            nextFootstepAt = Time.unscaledTime;
            wasStreetActive = true;
        }

        UpdateCarPassingSfx();
        UpdateFootsteps();
    }

    private AudioSource CreateAudioSource(string objectName)
    {
        var host = new GameObject(objectName);
        host.transform.SetParent(transform, false);

        var source = host.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        return source;
    }

    private void LoadFootsteps()
    {
        footstepClips.Clear();
        for (int i = 1; i <= 10; i++)
        {
            AudioClip clip = AudioSettingsService.LoadResourceClip($"{FootstepResourcePrefix}{i:00}");
            if (clip != null)
                footstepClips.Add(clip);
        }
    }

    private void CacheCarTiles()
    {
        carTiles.Clear();
        var scrollers = GetComponentsInChildren<ParallaxLoopScroller>(true);
        for (int i = 0; i < scrollers.Length; i++)
        {
            ParallaxLoopScroller scroller = scrollers[i];
            if (scroller == null || !scroller.name.StartsWith("Car_", StringComparison.OrdinalIgnoreCase))
                continue;

            AddCarTile(scroller.tileA);
            AddCarTile(scroller.tileB);
        }
    }

    private void AddCarTile(Transform tile)
    {
        if (tile == null)
            return;

        carTiles.Add(new CarTileState
        {
            tile = tile,
            previousWorldX = tile.position.x
        });
    }

    private void RefreshCarTilePositions()
    {
        for (int i = 0; i < carTiles.Count; i++)
        {
            if (carTiles[i].tile != null)
                carTiles[i].previousWorldX = carTiles[i].tile.position.x;
        }
    }

    private void UpdateCarPassingSfx()
    {
        for (int i = 0; i < carTiles.Count; i++)
        {
            CarTileState state = carTiles[i];
            if (state.tile == null)
                continue;

            float currentX = state.tile.position.x;
            bool crossedCenter =
                (state.previousWorldX > carPassingWorldX && currentX <= carPassingWorldX) ||
                (state.previousWorldX < carPassingWorldX && currentX >= carPassingWorldX);

            if (crossedCenter && Mathf.Abs(currentX - state.previousWorldX) < 5f)
                PlayOneShot(carSource, carWhooshClip, carWhooshVolume);

            state.previousWorldX = currentX;
        }
    }

    private void UpdateFootsteps()
    {
        if (footstepClips.Count == 0 || Time.unscaledTime < nextFootstepAt)
            return;

        int index = UnityEngine.Random.Range(0, footstepClips.Count);
        if (footstepClips.Count > 1 && index == lastFootstepIndex)
            index = (index + 1) % footstepClips.Count;

        lastFootstepIndex = index;
        PlayOneShot(footstepSource, footstepClips[index], footstepVolume);
        nextFootstepAt = Time.unscaledTime + Mathf.Max(0.1f, footstepIntervalSeconds);
    }

    private static void PlayOneShot(AudioSource source, AudioClip clip, float volume)
    {
        if (source != null && clip != null)
            source.PlayOneShot(clip, AudioSettingsService.ScaleSfx(volume));
    }
}
