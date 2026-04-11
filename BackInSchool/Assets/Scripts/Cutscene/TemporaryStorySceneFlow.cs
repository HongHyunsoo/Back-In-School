using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TemporaryStorySceneFlow
{
    private const string PendingKey = "TEMP_STORY_PENDING";
    private const string ConversationIdKey = "TEMP_STORY_CONVERSATION_ID";
    private const string ReturnSceneKey = "TEMP_STORY_RETURN_SCENE";
    private const string ReturnFlowIdKey = "TEMP_STORY_RETURN_FLOW_ID";
    private const string ReturnFlowTypeKey = "TEMP_STORY_RETURN_FLOW_TYPE";
    private const string ReturnSpawnPendingKey = "TEMP_STORY_RETURN_SPAWN_PENDING";
    private const string ReturnSpawnSceneKey = "TEMP_STORY_RETURN_SPAWN_SCENE";
    private const string ReturnSpawnPosXKey = "TEMP_STORY_RETURN_SPAWN_POS_X";
    private const string ReturnSpawnPosYKey = "TEMP_STORY_RETURN_SPAWN_POS_Y";
    private const string ReturnSpawnPosZKey = "TEMP_STORY_RETURN_SPAWN_POS_Z";
    private const string ReturnSpawnRotZKey = "TEMP_STORY_RETURN_SPAWN_ROT_Z";

    private const float FadeOutSeconds = 0.22f;
    private const float FadeInSeconds = 0.22f;

    private static TemporaryStorySceneFlowRunner runner;

    public static bool HasPendingStory()
    {
        return PlayerPrefs.GetInt(PendingKey, 0) == 1;
    }

    public static string GetPendingConversationId()
    {
        return PlayerPrefs.GetString(ConversationIdKey, string.Empty);
    }

    public static void Begin(string conversationId, string returnSceneName, bool preserveReturnPosition, bool preserveLunchClock)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return;

        string resolvedReturnScene = string.IsNullOrWhiteSpace(returnSceneName)
            ? SceneManager.GetActiveScene().name
            : returnSceneName;

        PlayerPrefs.SetInt(PendingKey, 1);
        PlayerPrefs.SetString(ConversationIdKey, conversationId);
        PlayerPrefs.SetString(ReturnSceneKey, resolvedReturnScene);
        PlayerPrefs.SetString(ReturnFlowIdKey, FlowContext.CurrentId ?? string.Empty);
        PlayerPrefs.SetString(ReturnFlowTypeKey, FlowContext.CurrentType ?? string.Empty);

        CacheReturnSpawn(resolvedReturnScene, preserveReturnPosition);
        CacheLunchClock(preserveLunchClock);

        FlowContext.Set(conversationId, FlowEventType.STORY);
        EnsureRunner().StartCoroutine(CoFadeAndLoad("STORY"));
    }

    public static void ReturnToStoredScene()
    {
        string returnScene = PlayerPrefs.GetString(ReturnSceneKey, "FREEROAM");
        string returnFlowId = PlayerPrefs.GetString(ReturnFlowIdKey, string.Empty);
        string returnFlowType = PlayerPrefs.GetString(ReturnFlowTypeKey, FlowContext.TypeFreeRoam);

        PlayerPrefs.SetString(FlowContext.FlowIdKey, returnFlowId);
        PlayerPrefs.SetString(FlowContext.FlowTypeKey, returnFlowType);

        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.DeleteKey(ConversationIdKey);
        PlayerPrefs.DeleteKey(ReturnSceneKey);
        PlayerPrefs.DeleteKey(ReturnFlowIdKey);
        PlayerPrefs.DeleteKey(ReturnFlowTypeKey);

        EnsureRunner().StartCoroutine(CoFadeAndLoad(returnScene));
    }

    public static bool TryConsumeReturnSpawnOverride(string sceneName, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (PlayerPrefs.GetInt(ReturnSpawnPendingKey, 0) != 1)
            return false;

        string targetScene = PlayerPrefs.GetString(ReturnSpawnSceneKey, string.Empty);
        if (!string.Equals(targetScene, sceneName, System.StringComparison.OrdinalIgnoreCase))
            return false;

        position = new Vector3(
            PlayerPrefs.GetFloat(ReturnSpawnPosXKey, 0f),
            PlayerPrefs.GetFloat(ReturnSpawnPosYKey, 0f),
            PlayerPrefs.GetFloat(ReturnSpawnPosZKey, 0f));
        rotation = Quaternion.Euler(0f, 0f, PlayerPrefs.GetFloat(ReturnSpawnRotZKey, 0f));

        PlayerPrefs.DeleteKey(ReturnSpawnPendingKey);
        PlayerPrefs.DeleteKey(ReturnSpawnSceneKey);
        PlayerPrefs.DeleteKey(ReturnSpawnPosXKey);
        PlayerPrefs.DeleteKey(ReturnSpawnPosYKey);
        PlayerPrefs.DeleteKey(ReturnSpawnPosZKey);
        PlayerPrefs.DeleteKey(ReturnSpawnRotZKey);
        return true;
    }

    private static IEnumerator CoFadeAndLoad(string sceneName)
    {
        var fader = SceneTransitionFader.EnsureInstance();
        fader.PrepareFadeInOnNextScene(FadeInSeconds);
        yield return fader.FadeOut(FadeOutSeconds);
        SceneManager.LoadScene(sceneName);
    }

    private static TemporaryStorySceneFlowRunner EnsureRunner()
    {
        if (runner != null)
            return runner;

        var go = new GameObject("__TemporaryStorySceneFlow");
        Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<TemporaryStorySceneFlowRunner>();
        return runner;
    }

    private static void CacheReturnSpawn(string returnSceneName, bool preserveReturnPosition)
    {
        if (!preserveReturnPosition)
        {
            ClearReturnSpawnOverride();
            return;
        }

        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            ClearReturnSpawnOverride();
            return;
        }

        Transform playerTransform = player.transform;
        PlayerPrefs.SetInt(ReturnSpawnPendingKey, 1);
        PlayerPrefs.SetString(ReturnSpawnSceneKey, returnSceneName);
        PlayerPrefs.SetFloat(ReturnSpawnPosXKey, playerTransform.position.x);
        PlayerPrefs.SetFloat(ReturnSpawnPosYKey, playerTransform.position.y);
        PlayerPrefs.SetFloat(ReturnSpawnPosZKey, playerTransform.position.z);
        PlayerPrefs.SetFloat(ReturnSpawnRotZKey, playerTransform.eulerAngles.z);
    }

    private static void CacheLunchClock(bool preserveLunchClock)
    {
        if (!preserveLunchClock || !FlowContext.IsLunchFreeRoam() || FlowManager.Instance == null)
            return;

        LunchFreeTimeTimerController timer = Object.FindAnyObjectByType<LunchFreeTimeTimerController>();
        if (timer == null)
            return;

        FlowManager.Instance.SetLunchFreeTimeStartMinuteForCurrentDay(timer.GetCurrentClockMinuteComponent());
    }

    private static void ClearReturnSpawnOverride()
    {
        PlayerPrefs.DeleteKey(ReturnSpawnPendingKey);
        PlayerPrefs.DeleteKey(ReturnSpawnSceneKey);
        PlayerPrefs.DeleteKey(ReturnSpawnPosXKey);
        PlayerPrefs.DeleteKey(ReturnSpawnPosYKey);
        PlayerPrefs.DeleteKey(ReturnSpawnPosZKey);
        PlayerPrefs.DeleteKey(ReturnSpawnRotZKey);
    }

    private sealed class TemporaryStorySceneFlowRunner : MonoBehaviour { }
}
