using System.Collections.Generic;
using UnityEngine;

public static class SpeakerAvatarProvider
{
    private static readonly Dictionary<string, Sprite> cache = new();

    public static Sprite GetAvatar(string speakerId)
    {
        if (string.IsNullOrEmpty(speakerId)) return null;

        // 1) NAME_ 붙은 경우/없는 경우 둘 다 시도
        string a = speakerId;
        string b = speakerId.StartsWith("NAME_") ? speakerId.Substring(5) : "NAME_" + speakerId;

        if (cache.TryGetValue(a, out var s) && s != null) return s;
        if (cache.TryGetValue(b, out s) && s != null) return s;

        // Resources/Avatars/{id}
        s = Resources.Load<Sprite>("Avatars/" + a);
        if (s == null) s = Resources.Load<Sprite>("Avatars/" + b);

        cache[a] = s; // null도 캐시해도 됨(중복 로드 방지)
        return s;
    }
}
