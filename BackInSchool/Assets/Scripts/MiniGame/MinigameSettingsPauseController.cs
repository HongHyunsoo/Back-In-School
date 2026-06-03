using UnityEngine;
using UnityEngine.SceneManagement;

public static class MinigameSettingsPauseController
{
    private static bool paused;
    private static float previousTimeScale = 1f;

    public static bool IsPaused => paused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        paused = false;
        previousTimeScale = 1f;
    }

    public static bool HandleEscapeOrPaused()
    {
        if (!IsMinigameScene())
            return false;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (paused)
                Resume();
            else
                PauseAndOpenSettings();

            return true;
        }

        return paused;
    }

    private static bool IsMinigameScene()
    {
        return SceneManager.GetActiveScene().name == "MINIGAME";
    }

    private static void PauseAndOpenSettings()
    {
        if (paused)
            return;

        previousTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
        Time.timeScale = 0f;
        paused = true;

        if (PhoneSystem.Instance != null)
            PhoneSystem.Instance.OpenSettingsOnlyForMinigamePause();
    }

    public static void Resume()
    {
        if (!paused)
            return;

        paused = false;
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;

        if (PhoneSystem.Instance != null && PhoneSystem.Instance.IsOpen)
            PhoneSystem.Instance.Close();
    }
}
