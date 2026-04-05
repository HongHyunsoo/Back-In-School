using System;
using UnityEngine;

public static class AudioSettingsService
{
    public const string MasterVolumePrefKey = "AUDIO_MASTER_VOLUME";
    public const string BgmVolumePrefKey = "AUDIO_BGM_VOLUME";
    public const string SfxVolumePrefKey = "AUDIO_SFX_VOLUME";

    private const float DefaultVolume = 1f;

    public static event Action<float> MasterVolumeChanged;
    public static event Action<float> BgmVolumeChanged;
    public static event Action<float> SfxVolumeChanged;

    public static float MasterVolume => PlayerPrefs.GetFloat(MasterVolumePrefKey, DefaultVolume);
    public static float BgmVolume => PlayerPrefs.GetFloat(BgmVolumePrefKey, DefaultVolume);
    public static float SfxVolume => PlayerPrefs.GetFloat(SfxVolumePrefKey, DefaultVolume);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        ApplyMasterVolume();
    }

    public static void ApplyMasterVolume()
    {
        AudioListener.volume = Mathf.Clamp01(MasterVolume);
    }

    public static void SetMasterVolume(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MasterVolumePrefKey, value);
        PlayerPrefs.Save();
        AudioListener.volume = value;
        MasterVolumeChanged?.Invoke(value);
    }

    public static void SetBgmVolume(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(BgmVolumePrefKey, value);
        PlayerPrefs.Save();
        BgmVolumeChanged?.Invoke(value);
    }

    public static void SetSfxVolume(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumePrefKey, value);
        PlayerPrefs.Save();
        SfxVolumeChanged?.Invoke(value);
    }

    public static float GetCategoryVolume(bool isBgm)
    {
        return isBgm ? BgmVolume : SfxVolume;
    }

    public static float ScaleBgm(float baseVolume)
    {
        return Mathf.Clamp01(baseVolume) * BgmVolume;
    }

    public static float ScaleSfx(float baseVolume)
    {
        return Mathf.Clamp01(baseVolume) * SfxVolume;
    }
}
