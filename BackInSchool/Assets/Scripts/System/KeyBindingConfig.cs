using UnityEngine;

public static class KeyBindingConfig
{
    public const string LeftKey = "INPUT_LEFT";
    public const string RightKey = "INPUT_RIGHT";
    public const string JumpKey = "INPUT_JUMP";
    public const string SprintKey = "INPUT_SPRINT";
    public const string DownKey = "INPUT_DOWN";
    public const string UpKey = "INPUT_UP";
    public const string InteractKey = "INPUT_INTERACT";
    public const string PhoneKey = "INPUT_PHONE";
    public const string StairUpKey = "INPUT_STAIR_UP";
    public const string StairDownKey = "INPUT_STAIR_DOWN";

    public static KeyCode Get(string key, KeyCode fallback)
    {
        string raw = PlayerPrefs.GetString(key, fallback.ToString());
        if (System.Enum.TryParse(raw, out KeyCode parsed))
        {
            if (IsAllowedBindingKey(parsed))
                return parsed;

            Set(key, fallback);
            return fallback;
        }
        return fallback;
    }

    public static void Set(string key, KeyCode code)
    {
        if (!IsAllowedBindingKey(code))
            return;

        PlayerPrefs.SetString(key, code.ToString());
        PlayerPrefs.Save();
    }

    public static bool IsAllowedBindingKey(KeyCode code)
    {
        switch (code)
        {
            case KeyCode.None:
            case KeyCode.Mouse0:
            case KeyCode.Mouse1:
            case KeyCode.Mouse2:
            case KeyCode.Mouse3:
            case KeyCode.Mouse4:
            case KeyCode.Mouse5:
            case KeyCode.Mouse6:
                return false;
            default:
                return true;
        }
    }
}
