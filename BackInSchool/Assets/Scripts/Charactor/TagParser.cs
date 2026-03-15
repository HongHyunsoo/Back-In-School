using System.Collections.Generic;
using System.Text.RegularExpressions;

public struct CutTag
{
    public string cmd;
    public string[] args;
    public CutTag(string c, string[] a) { cmd = c; args = a; }
}

public static class TagParser
{
    // [cmd:a,b,c] 형태를 전부 추출
    static readonly Regex TagRegex = new Regex(@"\[(?<cmd>[a-zA-Z_]+)\:(?<args>[^\]]*)\]");

    public static List<CutTag> Extract(string raw)
    {
        var list = new List<CutTag>();
        var matches = TagRegex.Matches(raw);
        foreach (Match m in matches)
        {
            var cmd = m.Groups["cmd"].Value.Trim();
            var argsRaw = m.Groups["args"].Value.Trim();
            var args = argsRaw.Length == 0 ? new string[0] : SplitArgs(argsRaw);
            list.Add(new CutTag(cmd, args));
        }
        return list;
    }

    public static string Strip(string raw)
    {
        return TagRegex.Replace(raw, "").Trim();
    }

    static string[] SplitArgs(string argsRaw)
    {
        // 간단 버전: 콤마로 분리
        // (나중에 따옴표 같은 거 필요하면 확장)
        var parts = argsRaw.Split(',');
        for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
        return parts;
    }
}
