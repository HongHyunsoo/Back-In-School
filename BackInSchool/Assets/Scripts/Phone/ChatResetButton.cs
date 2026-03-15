using UnityEngine;

public class ChatResetButton : MonoBehaviour
{
    public void ResetChat()
    {
        ChatService.Instance?.ResetAllChatForTest();

        // 혹시 잠금 남아있으면 강제 해제
        var app = FindAnyObjectByType<PhoneAppManager>();
        if (app) app.SetLocked(false);
    }
}
