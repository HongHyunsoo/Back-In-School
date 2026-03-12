using System;
using UnityEngine;

public static class FlowContext
{
    public const string FlowIdKey = "FLOW_ID";
    public const string FlowTypeKey = "FLOW_TYPE";

    public const string TypeChat = "CHAT";
    public const string TypeFreeRoam = "FREEROAM";
    public const string TypeStory = "STORY";
    public const string TypeMinigame = "MINIGAME";

    public static string CurrentId => PlayerPrefs.GetString(FlowIdKey, string.Empty);
    public static string CurrentType => PlayerPrefs.GetString(FlowTypeKey, string.Empty);

    public static bool IsType(string flowType)
    {
        return string.Equals(CurrentType, flowType, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsChat()
    {
        return IsType(TypeChat);
    }

    public static bool IsFreeRoam()
    {
        return IsType(TypeFreeRoam);
    }

    public static bool IsStory()
    {
        return IsType(TypeStory);
    }

    public static bool IsMinigame()
    {
        return IsType(TypeMinigame);
    }

    public static bool CurrentIdStartsWith(string prefix)
    {
        return !string.IsNullOrEmpty(prefix) &&
               CurrentId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CurrentIdContains(string value)
    {
        return !string.IsNullOrEmpty(value) &&
               CurrentId.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsMorningBeforeAssemblyFreeRoam()
    {
        return IsFreeRoam() && (string.IsNullOrEmpty(CurrentId) || CurrentIdContains("BEFORE_ASSEMBLY"));
    }

    public static bool IsLunchFreeRoam()
    {
        return IsFreeRoam() && CurrentIdContains("LUNCH");
    }

    public static bool IsAfterSchoolFreeRoam()
    {
        return IsFreeRoam() && CurrentIdContains("AFTERSCHOOL");
    }

    public static bool IsDay5FreeRoam()
    {
        return IsFreeRoam() && CurrentIdContains("DAY5") && CurrentIdContains("FREEROAM");
    }

    public static bool IsHealthCheckAllowed()
    {
        return IsChat() || IsMorningBeforeAssemblyFreeRoam();
    }

    public static void Set(string flowId, FlowEventType flowType)
    {
        PlayerPrefs.SetString(FlowIdKey, flowId ?? string.Empty);
        PlayerPrefs.SetString(FlowTypeKey, flowType.ToString());
    }
}
