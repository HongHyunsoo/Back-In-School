using System;
using System.Collections.Generic;
using UnityEngine;

public enum GalleryUnlockType
{
    None,
    Flow,
    Line,
    Conversation,
    Manual
}

[Serializable]
public sealed class PhoneGalleryEntry
{
    public string entryId;
    public string titleKo;
    public string titleEn;
    public string descriptionKo;
    public string descriptionEn;
    public string imageResourcePath;
    public GalleryUnlockType unlockType;
    public string unlockValue;
    public int sortOrder;

    public string GetTitle(Language language)
    {
        return language == Language.English && !string.IsNullOrEmpty(titleEn) ? titleEn : titleKo;
    }

    public string GetDescription(Language language)
    {
        return language == Language.English && !string.IsNullOrEmpty(descriptionEn) ? descriptionEn : descriptionKo;
    }
}

public sealed class PhoneGalleryCatalog
{
    private static PhoneGalleryCatalog instance;
    private readonly List<PhoneGalleryEntry> entries = new();

    public static PhoneGalleryCatalog Instance => instance ??= LoadFromResources();

    public IReadOnlyList<PhoneGalleryEntry> Entries => entries;

    public PhoneGalleryEntry GetEntry(string entryId)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].entryId, entryId, StringComparison.OrdinalIgnoreCase))
                return entries[i];
        }

        return null;
    }

    private static PhoneGalleryCatalog LoadFromResources()
    {
        var catalog = new PhoneGalleryCatalog();
        TextAsset csv = Resources.Load<TextAsset>("GalleryEntries");
        if (csv == null)
            return catalog;

        string[] rows = csv.text.Split('\n');
        for (int i = 1; i < rows.Length; i++)
        {
            string row = rows[i].Trim();
            if (string.IsNullOrEmpty(row))
                continue;

            string[] c = ParseCsvLine(row);
            if (c.Length < 9)
                continue;

            string entryId = SafeGet(c, 0);
            if (string.IsNullOrEmpty(entryId))
                continue;

            Enum.TryParse(SafeGet(c, 6), true, out GalleryUnlockType unlockType);
            int.TryParse(SafeGet(c, 8), out int sortOrder);

            catalog.entries.Add(new PhoneGalleryEntry
            {
                entryId = entryId,
                titleKo = SafeGet(c, 1),
                titleEn = SafeGet(c, 2),
                descriptionKo = SafeGet(c, 3),
                descriptionEn = SafeGet(c, 4),
                imageResourcePath = SafeGet(c, 5),
                unlockType = unlockType,
                unlockValue = SafeGet(c, 7),
                sortOrder = sortOrder
            });
        }

        catalog.entries.Sort((a, b) =>
        {
            int sortCompare = a.sortOrder.CompareTo(b.sortOrder);
            if (sortCompare != 0)
                return sortCompare;

            return string.Compare(a.entryId, b.entryId, StringComparison.OrdinalIgnoreCase);
        });

        return catalog;
    }

    private static string SafeGet(string[] values, int index)
    {
        if (values == null || index < 0 || index >= values.Length)
            return string.Empty;

        return values[index].Trim();
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        string current = string.Empty;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(current);
                current = string.Empty;
                continue;
            }

            current += ch;
        }

        result.Add(current);
        return result.ToArray();
    }
}
