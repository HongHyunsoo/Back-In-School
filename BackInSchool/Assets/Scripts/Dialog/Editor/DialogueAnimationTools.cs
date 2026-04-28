#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DialogueAnimationTools
{
    private const string CatalogAssetPath = "Assets/Resources/DialogueAnimationClipCatalog.asset";
    private const string ConversationsCsvPath = "Assets/Resources/Conversations.csv";
    private static readonly string[] AnimatorRoots = { "Assets/Animator" };

    [InitializeOnLoadMethod]
    private static void EnsureCatalogOnEditorLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (AssetDatabase.LoadAssetAtPath<DialogueAnimationClipCatalogAsset>(CatalogAssetPath) == null)
                RebuildAnimationCatalog(logSummary: false);
        };
    }

    [MenuItem("Tools/Back In School/Dialogue/Rebuild Animation Catalog")]
    public static void RebuildAnimationCatalogMenu()
    {
        RebuildAnimationCatalog(logSummary: true);
    }

    [MenuItem("Tools/Back In School/Dialogue/Validate Conversations CSV")]
    public static void ValidateConversationsCsvMenu()
    {
        RebuildAnimationCatalog(logSummary: false);

        List<string> issues = new List<string>();
        Dictionary<string, int> clipNames = BuildAnimationClipNameSet();

        if (!File.Exists(ConversationsCsvPath))
        {
            Debug.LogError($"[DialogueCSV] Conversations.csv not found at '{ConversationsCsvPath}'.");
            return;
        }

        string csvText = File.ReadAllText(ConversationsCsvPath, Encoding.UTF8).Replace("\r\n", "\n").Replace('\r', '\n');
        List<string> records = BuildCsvRecords(csvText.Split('\n'));
        if (records.Count == 0)
        {
            Debug.LogWarning("[DialogueCSV] Conversations.csv is empty.");
            return;
        }

        string[] headers = ParseCsvLine(records[0]);
        Dictionary<string, int> headerMap = BuildHeaderIndexMap(headers);

        ValidateRequiredHeader(headerMap, "Conversation_ID", issues);
        ValidateRequiredHeader(headerMap, "Order", issues);
        ValidateRequiredHeader(headerMap, "Line_ID", issues);
        ValidateRequiredHeader(headerMap, "AnimationClip", issues);
        ValidateRequiredHeader(headerMap, "SneakersAnimationClip", issues);
        ValidateRequiredHeader(headerMap, "TargetCharacter_ID", issues);

        for (int i = 1; i < records.Count; i++)
        {
            string row = records[i];
            if (string.IsNullOrWhiteSpace(row))
                continue;

            string[] columns = ParseCsvLine(row);
            string conversationId = GetCsvValue(columns, headerMap, "Conversation_ID");
            string order = GetCsvValue(columns, headerMap, "Order");
            string lineId = GetCsvValue(columns, headerMap, "Line_ID");

            string targetRaw = GetCsvValue(columns, headerMap, "TargetCharacter_ID");
            string clipRaw = GetCsvValue(columns, headerMap, "AnimationClip");
            string sneakersRaw = GetCsvValue(columns, headerMap, "SneakersAnimationClip");
            string triggerRaw = GetCsvValue(columns, headerMap, "AnimationTrigger");
            string soundRaw = GetCsvValue(columns, headerMap, "SoundEffect");
            string delayRaw = GetCsvValue(columns, headerMap, "BeforeTextDelaySeconds");

            string context = $"{conversationId}/{order}/{lineId}";

            string[] targetParts = SplitPresentationField(targetRaw);
            string[] clipParts = SplitPresentationField(clipRaw);
            string[] sneakersParts = SplitPresentationField(sneakersRaw);
            string[] triggerParts = SplitPresentationField(triggerRaw);
            string[] soundParts = SplitPresentationField(soundRaw);
            string[] delayParts = SplitPresentationField(delayRaw);

            int activeFieldCount = 0;
            int expectedCount = 0;
            CheckFieldCount(targetParts, "TargetCharacter_ID", context, ref activeFieldCount, ref expectedCount, issues);
            CheckFieldCount(clipParts, "AnimationClip", context, ref activeFieldCount, ref expectedCount, issues);
            CheckFieldCount(sneakersParts, "SneakersAnimationClip", context, ref activeFieldCount, ref expectedCount, issues);
            CheckFieldCount(triggerParts, "AnimationTrigger", context, ref activeFieldCount, ref expectedCount, issues);
            CheckFieldCount(soundParts, "SoundEffect", context, ref activeFieldCount, ref expectedCount, issues);
            CheckFieldCount(delayParts, "BeforeTextDelaySeconds", context, ref activeFieldCount, ref expectedCount, issues);

            ValidateClipNames(clipParts, "AnimationClip", context, clipNames, issues);
            ValidateClipNames(sneakersParts, "SneakersAnimationClip", context, clipNames, issues);
        }

        if (issues.Count == 0)
        {
            Debug.Log($"[DialogueCSV] Validation passed. Checked {Mathf.Max(0, records.Count - 1)} rows.");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"[DialogueCSV] Validation found {issues.Count} issue(s):");
        for (int i = 0; i < issues.Count; i++)
            sb.AppendLine($"- {issues[i]}");
        Debug.LogWarning(sb.ToString());
    }

    private static void RebuildAnimationCatalog(bool logSummary)
    {
        List<DialogueAnimationClipCatalogAsset.Entry> entries = new List<DialogueAnimationClipCatalogAsset.Entry>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string[] guids = AssetDatabase.FindAssets("t:AnimationClip", AnimatorRoots);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
                continue;

            string key = clip.name.Trim();
            if (string.IsNullOrEmpty(key) || !seen.Add(key))
                continue;

            entries.Add(new DialogueAnimationClipCatalogAsset.Entry
            {
                key = key,
                clip = clip
            });
        }

        entries.Sort((a, b) => string.Compare(a.key, b.key, StringComparison.OrdinalIgnoreCase));

        DialogueAnimationClipCatalogAsset asset = AssetDatabase.LoadAssetAtPath<DialogueAnimationClipCatalogAsset>(CatalogAssetPath);
        if (asset == null)
        {
            string directory = Path.GetDirectoryName(CatalogAssetPath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                string parent = Path.GetDirectoryName(directory)?.Replace("\\", "/");
                string folderName = Path.GetFileName(directory);
                if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
                    AssetDatabase.CreateFolder(parent, folderName);
            }

            asset = ScriptableObject.CreateInstance<DialogueAnimationClipCatalogAsset>();
            AssetDatabase.CreateAsset(asset, CatalogAssetPath);
        }

        asset.ReplaceEntries(entries);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (logSummary)
            Debug.Log($"[DialogueAnimationCatalog] Rebuilt '{CatalogAssetPath}' with {entries.Count} clip(s).");
    }

    private static Dictionary<string, int> BuildAnimationClipNameSet()
    {
        Dictionary<string, int> names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip", AnimatorRoots);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null || string.IsNullOrWhiteSpace(clip.name))
                continue;

            names[clip.name.Trim()] = 1;
        }

        return names;
    }

    private static void ValidateRequiredHeader(Dictionary<string, int> headerMap, string header, List<string> issues)
    {
        if (!headerMap.ContainsKey(header))
            issues.Add($"Missing required header '{header}'.");
    }

    private static void CheckFieldCount(string[] parts, string fieldName, string context, ref int activeFieldCount, ref int expectedCount, List<string> issues)
    {
        int length = GetMeaningfulPartCount(parts);
        if (length <= 1)
            return;

        if (activeFieldCount == 0)
        {
            activeFieldCount = 1;
            expectedCount = length;
            return;
        }

        if (length != expectedCount)
            issues.Add($"{context}: '{fieldName}' count {length} does not match other multi-target fields count {expectedCount}.");
    }

    private static int GetMeaningfulPartCount(string[] parts)
    {
        if (parts == null || parts.Length == 0)
            return 0;

        int lastMeaningfulIndex = -1;
        for (int i = 0; i < parts.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(parts[i]))
                lastMeaningfulIndex = i;
        }

        return lastMeaningfulIndex + 1;
    }

    private static void ValidateClipNames(string[] parts, string fieldName, string context, Dictionary<string, int> clipNames, List<string> issues)
    {
        if (parts == null)
            return;

        for (int i = 0; i < parts.Length; i++)
        {
            string clipName = parts[i];
            if (string.IsNullOrWhiteSpace(clipName))
                continue;

            if (!clipNames.ContainsKey(clipName.Trim()))
                issues.Add($"{context}: '{fieldName}' references missing clip '{clipName}'.");
        }
    }

    private static Dictionary<string, int> BuildHeaderIndexMap(string[] headers)
    {
        Dictionary<string, int> map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (headers == null)
            return map;

        for (int i = 0; i < headers.Length; i++)
        {
            string header = headers[i] != null ? headers[i].Trim() : string.Empty;
            if (string.IsNullOrEmpty(header) || map.ContainsKey(header))
                continue;

            map[header] = i;
        }

        return map;
    }

    private static string GetCsvValue(string[] columns, Dictionary<string, int> headerMap, string header)
    {
        if (columns == null || headerMap == null)
            return string.Empty;

        if (!headerMap.TryGetValue(header, out int index))
            return string.Empty;

        if (index < 0 || index >= columns.Length)
            return string.Empty;

        return columns[index].Trim();
    }

    private static string[] SplitPresentationField(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw.Split('|').Select(part => part.Trim()).ToArray();
    }

    private static string[] ParseCsvLine(string line)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        StringBuilder currentField = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '\"')
            {
                bool isEscapedQuote = inQuotes && i + 1 < line.Length && line[i + 1] == '\"';
                if (isEscapedQuote)
                {
                    currentField.Append('\"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(currentField.ToString());
                currentField.Length = 0;
                continue;
            }

            currentField.Append(c);
        }

        result.Add(currentField.ToString());
        return result.ToArray();
    }

    private static List<string> BuildCsvRecords(string[] rawLines)
    {
        List<string> records = new List<string>();
        StringBuilder current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < rawLines.Length; i++)
        {
            string line = rawLines[i];

            if (current.Length > 0)
                current.Append('\n');

            current.Append(line);

            for (int j = 0; j < line.Length; j++)
            {
                if (line[j] != '\"')
                    continue;

                bool isEscaped = j + 1 < line.Length && line[j + 1] == '\"';
                if (isEscaped)
                {
                    j++;
                    continue;
                }

                inQuotes = !inQuotes;
            }

            if (inQuotes)
                continue;

            records.Add(current.ToString());
            current.Length = 0;
        }

        if (current.Length > 0)
            records.Add(current.ToString());

        return records;
    }
}
#endif
