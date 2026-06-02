using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PhoneGallerySaveData
{
    public List<string> unlockedEntryIds = new();
}

[DefaultExecutionOrder(-850)]
public class PhoneGalleryService : MonoBehaviour
{
    private const string PrefKey = "PHONE_GALLERY_SAVE_V1";
    private const string SilentUnlockLineId = "DAY1_CLASSOPEN_05";

    public static PhoneGalleryService Instance { get; private set; }
    public static bool IsShuttingDown { get; private set; }

    private readonly HashSet<string> unlockedEntryIds = new(StringComparer.OrdinalIgnoreCase);

    public event Action OnChanged;

    public static PhoneGalleryService EnsureExists()
    {
        if (IsShuttingDown)
            return Instance;

        if (Instance != null)
            return Instance;

        var go = new GameObject("PhoneGalleryService");
        Instance = go.AddComponent<PhoneGalleryService>();
        DontDestroyOnLoad(go);
        return Instance;
    }

    public static void NotifyFlowVisited(string flowId)
    {
        EnsureExists().EvaluateFlow(flowId, playSfx: false);
    }

    public static bool UnlockStatic(string entryId)
    {
        return EnsureExists().Unlock(entryId);
    }

    public static void RefreshUnlocksFromSavedState()
    {
        EnsureExists().RefreshPersistentUnlocksInternal();
    }

    public static void ResetPersistedDataForNewGame()
    {
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();

        if (Instance != null)
            Instance.ResetForNewGame();
    }

    private void Awake()
    {
        IsShuttingDown = false;

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
        UnlockAllImmediateEntries();
        EvaluatePersistentUnlocks();
        DialogueManager.DialogueLineShown += HandleDialogueLineShown;
        DialogueManager.DialogueConversationCompleted += HandleConversationCompleted;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            IsShuttingDown = true;

        if (Instance == this)
            Instance = null;

        DialogueManager.DialogueLineShown -= HandleDialogueLineShown;
        DialogueManager.DialogueConversationCompleted -= HandleConversationCompleted;
    }

    private void OnApplicationQuit()
    {
        IsShuttingDown = true;
    }

    private void Start()
    {
        EvaluateFlow(FlowContext.CurrentId, playSfx: false);
    }

    public IReadOnlyList<PhoneGalleryEntry> GetEntries()
    {
        return PhoneGalleryCatalog.Instance.Entries;
    }

    public bool IsUnlocked(string entryId)
    {
        return !string.IsNullOrEmpty(entryId) && unlockedEntryIds.Contains(entryId);
    }

    public int GetUnlockedCount()
    {
        return unlockedEntryIds.Count;
    }

    public bool Unlock(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
            return false;

        if (!unlockedEntryIds.Add(entryId))
            return false;

        Save();
        OnChanged?.Invoke();
        PlayUnlockSfx();
        return true;
    }

    public void EvaluateFlow(string flowId, bool playSfx = true)
    {
        if (string.IsNullOrEmpty(flowId))
            return;

        bool changed = false;
        var entries = PhoneGalleryCatalog.Instance.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.unlockType != GalleryUnlockType.Flow || string.IsNullOrEmpty(entry.unlockValue))
                continue;

            if (flowId.IndexOf(entry.unlockValue, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            changed |= unlockedEntryIds.Add(entry.entryId);
        }

        if (!changed)
            return;

        Save();
        OnChanged?.Invoke();
        if (playSfx)
            PlayUnlockSfx();
    }

    public void ResetForNewGame()
    {
        unlockedEntryIds.Clear();
        UnlockAllImmediateEntries();
        EvaluatePersistentUnlocks();
        Save();
        OnChanged?.Invoke();
    }

    public void RefreshPersistentUnlocksInternal()
    {
        int beforeCount = unlockedEntryIds.Count;
        EvaluatePersistentUnlocks();
        if (unlockedEntryIds.Count != beforeCount)
            OnChanged?.Invoke();
    }

    private void HandleDialogueLineShown(string _, string lineId)
    {
        if (string.IsNullOrEmpty(lineId))
            return;

        bool changed = false;
        var entries = PhoneGalleryCatalog.Instance.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.unlockType != GalleryUnlockType.Line)
                continue;

            if (!string.Equals(entry.unlockValue, lineId, StringComparison.OrdinalIgnoreCase))
                continue;

            changed |= unlockedEntryIds.Add(entry.entryId);
        }

        if (!changed)
            return;

        Save();
        OnChanged?.Invoke();
        if (!string.Equals(lineId, SilentUnlockLineId, StringComparison.OrdinalIgnoreCase))
            PlayUnlockSfx();
    }

    private void UnlockAllImmediateEntries()
    {
        var entries = PhoneGalleryCatalog.Instance.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].unlockType == GalleryUnlockType.None)
                unlockedEntryIds.Add(entries[i].entryId);
        }
    }

    private void HandleConversationCompleted(string conversationId)
    {
        if (string.IsNullOrEmpty(conversationId))
            return;

        bool changed = false;
        var entries = PhoneGalleryCatalog.Instance.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.unlockType != GalleryUnlockType.Conversation)
                continue;

            if (!string.Equals(entry.unlockValue, conversationId, StringComparison.OrdinalIgnoreCase))
                continue;

            changed |= unlockedEntryIds.Add(entry.entryId);
        }

        if (!changed)
            return;

        Save();
        OnChanged?.Invoke();
        PlayUnlockSfx();
    }

    private void EvaluatePersistentUnlocks()
    {
        bool changed = false;
        var entries = PhoneGalleryCatalog.Instance.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.unlockValue))
                continue;

            switch (entry.unlockType)
            {
                case GalleryUnlockType.Conversation:
                    if (DialogueProgressState.HasCompletedConversation(entry.unlockValue))
                        changed |= unlockedEntryIds.Add(entry.entryId);
                    break;
                case GalleryUnlockType.Flow:
                    if (!string.IsNullOrEmpty(FlowContext.CurrentId) &&
                        FlowContext.CurrentId.IndexOf(entry.unlockValue, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        changed |= unlockedEntryIds.Add(entry.entryId);
                    }
                    break;
            }
        }

        if (changed)
            Save();
    }

    private void Save()
    {
        var saveData = new PhoneGallerySaveData();
        saveData.unlockedEntryIds.AddRange(unlockedEntryIds);
        PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(saveData));
        PlayerPrefs.Save();
    }

    private static void PlayUnlockSfx()
    {
        PhoneSystem.Instance?.PlayPhoneApplySfx();
    }

    private void Load()
    {
        unlockedEntryIds.Clear();

        if (!PlayerPrefs.HasKey(PrefKey))
            return;

        string json = PlayerPrefs.GetString(PrefKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            var saveData = JsonUtility.FromJson<PhoneGallerySaveData>(json);
            if (saveData?.unlockedEntryIds == null)
                return;

            for (int i = 0; i < saveData.unlockedEntryIds.Count; i++)
            {
                string entryId = saveData.unlockedEntryIds[i];
                if (!string.IsNullOrWhiteSpace(entryId))
                    unlockedEntryIds.Add(entryId);
            }
        }
        catch
        {
            unlockedEntryIds.Clear();
        }
    }
}
