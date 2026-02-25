using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PenaltyReasonEntry
{
    public string reasonId;
    public int amount;
    public int day;
}

[Serializable]
internal class PenaltyReasonEntryList
{
    public List<PenaltyReasonEntry> items = new List<PenaltyReasonEntry>();
}

public static class PenaltyReasonLog
{
    public const string ReasonHealthSurveyMissing = "PENALTY_HEALTH_SURVEY_MISSING";
    public const string ReasonNoSlippers = "PENALTY_NO_SLIPPERS";

    private const string PrefKey = "PENALTY_REASON_LOG_V1";
    private static readonly List<PenaltyReasonEntry> cache = new List<PenaltyReasonEntry>();
    private static bool loaded;

    public static event Action OnChanged;

    public static IReadOnlyList<PenaltyReasonEntry> GetAll()
    {
        EnsureLoaded();
        return cache;
    }

    public static void Add(string reasonId, int amount, int day)
    {
        if (string.IsNullOrEmpty(reasonId) || amount <= 0)
            return;

        EnsureLoaded();
        cache.Add(new PenaltyReasonEntry
        {
            reasonId = reasonId,
            amount = amount,
            day = day
        });
        Save();
        OnChanged?.Invoke();
    }

    public static void Clear()
    {
        EnsureLoaded();
        cache.Clear();
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        cache.Clear();

        string raw = PlayerPrefs.GetString(PrefKey, string.Empty);
        if (string.IsNullOrEmpty(raw))
            return;

        try
        {
            var list = JsonUtility.FromJson<PenaltyReasonEntryList>(raw);
            if (list != null && list.items != null)
                cache.AddRange(list.items);
        }
        catch
        {
            // Ignore broken old data and start fresh.
            cache.Clear();
        }
    }

    private static void Save()
    {
        var list = new PenaltyReasonEntryList { items = cache };
        string json = JsonUtility.ToJson(list);
        PlayerPrefs.SetString(PrefKey, json);
        PlayerPrefs.Save();
    }
}

