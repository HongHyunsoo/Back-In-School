using System;
using System.Collections.Generic;
using UnityEngine;

public enum ChatSender { Other, Me }

[Serializable]
public class ChatLine
{
    public ChatSender sender;
    [TextArea(2, 6)] public string text;

    [Tooltip("Other면 자동, Me면 탭해야 진행")]
    public bool requireTapToSend;

    [Min(0f)] public float delay = 0.6f; // 자동 메시지 전 딜레이
}

[CreateAssetMenu(menuName = "Game/Chat/Chat Session")]
public class ChatSessionData : ScriptableObject
{
    public string sessionId;
    public string roomId;

    public List<ChatLine> lines = new();
}
