// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Text;
using System.Text.RegularExpressions;
using Ordinaryunus.Data;

namespace Ordinaryunus.Brain;

public sealed record KayitField(string Key, string Value);

/// <summary>One "### 2026-09-23 23:14 — codex — title" (devir) or "### KARAR 2026-09-23 — title" heading and its fields.</summary>
public sealed record KayitEntry(string Kind, DateOnly Date, string? Time, string? Tool, string Title,
    List<KayitField> Fields, string Path, string Anchor, string? Project)
{
    public string? Get(string key) => Fields.FirstOrDefault(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
}

/// <summary>Kayıt.md / card "## Son Devir" entry parser (MIMARI §5.5).</summary>
public static partial class KayitParser
{
    public static List<KayitEntry> Parse(string text, string relPath, string? project)
    {
        var result = new List<KayitEntry>();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var m = HeadingRx().Match(lines[i].TrimEnd());
            if (!m.Success) continue;
            bool isKarar = m.Groups[1].Success;
            if (!DateOnly.TryParseExact(m.Groups[2].Value, "yyyy-MM-dd", out var date)) continue;
            string? time = m.Groups[3].Success ? m.Groups[3].Value : null;
            string rest = m.Groups[4].Value.Trim();
            string? tool = null;
            string title = rest;
            if (!isKarar)
            {
                int dash = rest.IndexOf(" — ", StringComparison.Ordinal);
                if (dash >= 0) { tool = rest[..dash].Trim(); title = rest[(dash + 3)..].Trim(); }
            }
            var fields = new List<KayitField>();
            string? curKey = null;
            var curVal = new StringBuilder();
            int j = i + 1;
            for (; j < lines.Length; j++)
            {
                if (Md.IsHeadingAtOrAbove(lines[j], 3)) break;
                var fm = FieldRx().Match(lines[j]);
                if (fm.Success)
                {
                    if (curKey is not null) fields.Add(new KayitField(curKey, curVal.ToString().Trim()));
                    curKey = fm.Groups[1].Value.Trim();
                    curVal = new StringBuilder(fm.Groups[2].Value.Trim());
                }
                else if (curKey is not null && !string.IsNullOrWhiteSpace(lines[j]))
                {
                    curVal.Append(' ').Append(lines[j].Trim());
                }
            }
            if (curKey is not null) fields.Add(new KayitField(curKey, curVal.ToString().Trim()));
            result.Add(new KayitEntry(isKarar ? "karar" : "devir", date, time, tool, title, fields, relPath, "h-" + (i + 1), project));
        }
        return result;
    }

    /// <summary>Newest devir entry by date+time, from a set of entries already merged (card + Kayıt.md).</summary>
    public static KayitEntry? LatestDevir(IEnumerable<KayitEntry> entries) =>
        entries.Where(e => e.Kind == "devir")
               .OrderByDescending(e => e.Date).ThenByDescending(e => e.Time ?? "")
               .FirstOrDefault();

    [GeneratedRegex(@"^### (KARAR )?(\d{4}-\d{2}-\d{2})(?: (\d{2}:\d{2}))?\s+[—–-]+\s+(.+)$")] private static partial Regex HeadingRx();
    [GeneratedRegex(@"^-\s*([^:]+):\s*(.*)$")] private static partial Regex FieldRx();
}
