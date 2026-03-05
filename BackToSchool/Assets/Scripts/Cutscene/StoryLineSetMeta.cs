using System.Collections.Generic;
using UnityEngine;

public static class StoryLineSetMeta
{
    private const string ResourceName = "StoryLineSetMeta";
    private static Dictionary<string, string> lineToSet;
    private static bool loaded;

    public static string GetSetIdForLine(string lineId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(lineId) || lineToSet == null)
            return null;

        return lineToSet.TryGetValue(lineId, out var setId) ? setId : null;
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        lineToSet = new Dictionary<string, string>();

        TextAsset csv = Resources.Load<TextAsset>(ResourceName);
        if (csv == null)
            return;

        string[] rows = csv.text.Split('\n');
        for (int i = 1; i < rows.Length; i++)
        {
            string row = rows[i].Trim();
            if (string.IsNullOrEmpty(row))
                continue;

            string[] cols = ParseRow(row);
            if (cols.Length < 2)
                continue;

            string lineId = cols[0].Trim();
            string setId = cols[1].Trim();
            if (string.IsNullOrEmpty(lineId) || string.IsNullOrEmpty(setId))
                continue;

            if (!lineToSet.ContainsKey(lineId))
                lineToSet.Add(lineId, setId);
        }
    }

    private static string[] ParseRow(string row)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var cur = "";

        for (int i = 0; i < row.Length; i++)
        {
            char c = row[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(cur);
                cur = "";
                continue;
            }

            cur += c;
        }

        result.Add(cur);
        return result.ToArray();
    }
}
