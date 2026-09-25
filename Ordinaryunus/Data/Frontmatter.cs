// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
namespace Ordinaryunus.Data;

/// <summary>Minimal YAML frontmatter reader: top-level "key: value" pairs, quoted or unquoted.</summary>
public sealed class Frontmatter
{
    readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Values => _values;
    public bool Present { get; private set; }

    public string Get(string key, string fallback = "") => _values.TryGetValue(key, out var v) ? v : fallback;

    public bool GetBool(string key) =>
        _values.TryGetValue(key, out var v) && (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "evet" || v == "yes");

    public DateOnly? GetDate(string key) => Md.FirstIsoDate(Get(key));

    public static Frontmatter Parse(string? text) => Parse(Md.Lines(text));

    public static Frontmatter Parse(IReadOnlyList<string> lines)
    {
        var fm = new Frontmatter();
        int end = FindEnd(lines);
        if (end < 0) return fm;
        fm.Present = true;
        for (int i = 1; i < end; i++)
        {
            string line = lines[i];
            if (line.Length == 0 || char.IsWhiteSpace(line[0]) || line[0] == '#' || line[0] == '-') continue; // nested / list / comment
            int colon = line.IndexOf(':');
            if (colon <= 0) continue;
            string key = line[..colon].Trim();
            string raw = line[(colon + 1)..].Trim();
            fm._values[key] = Unquote(raw);
        }
        return fm;
    }

    /// <summary>Index of the closing "---" line, or -1 when there is no frontmatter.</summary>
    static int FindEnd(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0) return -1;
        string first = lines[0].TrimStart('﻿').TrimEnd();
        if (first != "---") return -1;
        for (int i = 1; i < lines.Count; i++)
        {
            string l = lines[i].TrimEnd();
            if (l == "---" || l == "...") return i;
        }
        return -1;
    }

    public static int BodyStart(IReadOnlyList<string> lines)
    {
        int end = FindEnd(lines);
        return end < 0 ? 0 : end + 1;
    }

    public static string Unquote(string raw)
    {
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
        {
            string inner = raw[1..^1];
            var sb = new System.Text.StringBuilder(inner.Length);
            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];
                if (c == '\\' && i + 1 < inner.Length)
                {
                    char n = inner[++i];
                    sb.Append(n switch { 'n' => '\n', 't' => '\t', _ => n });
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }
        if (raw.Length >= 2 && raw[0] == '\'' && raw[^1] == '\'')
            return raw[1..^1].Replace("''", "'");
        // strip trailing YAML comment (" #...") for unquoted scalars
        int hash = raw.IndexOf(" #", StringComparison.Ordinal);
        if (hash >= 0) raw = raw[..hash].TrimEnd();
        return raw;
    }
}
