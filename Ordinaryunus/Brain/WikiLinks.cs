// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Text;
using System.Text.RegularExpressions;
using Ordinaryunus.Data;

namespace Ordinaryunus.Brain;

/// <summary>One [[wikilink]] or ![[embed]] found in a note, 1-based line number in the original file.</summary>
public sealed record RawLink(string Target, string Label, int Line, bool IsEmbed);

/// <summary>Extraction (code-aware) and Obsidian-style resolution of wikilinks (MIMARI §5.4).</summary>
public static partial class WikiLinks
{
    /// <summary>Blanks fenced code, inline code, %%…%% and HTML comments (keeps line/column count) so link
    /// regexes never fire inside them, then extracts every [[..]] / ![[..]].</summary>
    public static List<RawLink> Extract(string text)
    {
        string masked = Mask(text);
        var result = new List<RawLink>();
        var lines = masked.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            foreach (Match m in WikiRx().Matches(lines[i]))
            {
                bool embed = m.Groups[1].Success;
                string inner = m.Groups[2].Value;
                if (inner.Contains('<') || inner.Contains('>') || inner.Contains("{{")) continue;
                string target = inner;
                string label = inner;
                int pipe = inner.IndexOf('|');
                if (pipe >= 0) { target = inner[..pipe]; label = inner[(pipe + 1)..]; }
                target = StripAnchor(target).Trim();
                label = label.Trim();
                if (target.Length == 0) continue;
                result.Add(new RawLink(target, label.Length == 0 ? target : label, i + 1, embed));
            }
        }
        return result;
    }

    static string StripAnchor(string target)
    {
        int hash = target.IndexOf('#');
        if (hash >= 0) target = target[..hash];
        int caret = target.IndexOf('^');
        if (caret >= 0) target = target[..caret];
        return target;
    }

    /// <summary>Replaces fenced code blocks, inline code spans, %%…%% and HTML comments with spaces (newlines kept).</summary>
    public static string Mask(string text)
    {
        var sb = new StringBuilder(text.Length);
        bool inFence = false;
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal) || line.TrimStart().StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
                sb.Append(new string(' ', line.Length));
            }
            else if (inFence)
            {
                sb.Append(new string(' ', line.Length));
            }
            else
            {
                sb.Append(MaskInlineCode(line));
            }
            if (i < lines.Length - 1) sb.Append('\n');
        }
        string masked = sb.ToString();
        masked = HtmlCommentRx().Replace(masked, m => Blank(m.Value));
        masked = ObsidianCommentRx().Replace(masked, m => Blank(m.Value));
        return masked;
    }

    static string MaskInlineCode(string line)
    {
        var sb = new StringBuilder(line.Length);
        int i = 0;
        while (i < line.Length)
        {
            if (line[i] == '`')
            {
                int start = i;
                int j = i + 1;
                while (j < line.Length && line[j] != '`') j++;
                if (j < line.Length) { sb.Append(new string(' ', j - start + 1)); i = j + 1; continue; }
            }
            sb.Append(line[i]);
            i++;
        }
        return sb.ToString();
    }

    static string Blank(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s) sb.Append(c == '\n' ? '\n' : ' ');
        return sb.ToString();
    }

    [GeneratedRegex(@"(!)?\[\[([^\[\]]*)\]\]")] private static partial Regex WikiRx();
    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)] private static partial Regex HtmlCommentRx();
    [GeneratedRegex(@"%%.*?%%", RegexOptions.Singleline)] private static partial Regex ObsidianCommentRx();
}

/// <summary>Resolves wikilink targets against the note index (Obsidian-like), Turkish-aware fold, 1:1 with the index built once per reload.</summary>
public sealed class WikiLinkResolver
{
    readonly List<string> _allPaths;                                  // every indexed relPath (files + notes), original case
    readonly Dictionary<string, List<string>> _byFoldedPath;          // folded relPath -> [relPath] (for '/'-qualified targets)
    readonly Dictionary<string, List<string>> _byFoldedName;          // folded file name (with ext) -> [relPath] (non-md extension match)
    readonly Dictionary<string, List<string>> _byFoldedBasename;      // folded .md basename (no ext) -> [relPath]
    readonly Dictionary<string, List<string>> _byFoldedAlias;         // folded alias -> [relPath of the note declaring it]

    public WikiLinkResolver(IEnumerable<string> allRelPaths, IReadOnlyDictionary<string, List<string>> aliasesByPath)
    {
        _allPaths = allRelPaths.ToList();
        _byFoldedPath = new(StringComparer.Ordinal);
        _byFoldedName = new(StringComparer.Ordinal);
        _byFoldedBasename = new(StringComparer.Ordinal);
        _byFoldedAlias = new(StringComparer.Ordinal);
        foreach (var p in _allPaths)
        {
            Add(_byFoldedPath, Fold(p), p);
            Add(_byFoldedName, Fold(Path.GetFileName(p)), p);
            if (p.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                Add(_byFoldedBasename, Fold(Path.GetFileNameWithoutExtension(p)), p);
        }
        foreach (var (path, aliases) in aliasesByPath)
            foreach (var a in aliases)
                Add(_byFoldedAlias, Fold(a), path);
    }

    static void Add(Dictionary<string, List<string>> d, string key, string val)
    {
        if (!d.TryGetValue(key, out var list)) d[key] = list = [];
        list.Add(val);
    }

    public static string Fold(string s) => Md.Normalize(s.Replace('\\', '/'));

    /// <summary>Resolves a raw wikilink target; null relPath means broken.</summary>
    public string? Resolve(string target)
    {
        string t = target.Replace('\\', '/').Trim();
        if (t.Length == 0) return null;
        List<string>? matches = null;
        if (t.Contains('/'))
        {
            string folded = Fold(t);
            string foldedMd = folded.EndsWith(".md", StringComparison.Ordinal) ? folded : folded + ".md";
            matches = _allPaths.Where(p => { string fp = Fold(p); return fp == folded || fp == foldedMd || fp.EndsWith("/" + folded, StringComparison.Ordinal) || fp.EndsWith("/" + foldedMd, StringComparison.Ordinal); }).ToList();
        }
        else if (Path.HasExtension(t) && !t.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            _byFoldedName.TryGetValue(Fold(t), out matches);
        }
        else
        {
            string bn = t.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? t[..^3] : t;
            if (!_byFoldedBasename.TryGetValue(Fold(bn), out matches) || matches.Count == 0)
                _byFoldedAlias.TryGetValue(Fold(bn), out matches);
        }
        if (matches is null || matches.Count == 0) return null;
        return matches.OrderBy(p => p.Length).ThenBy(p => p, StringComparer.Ordinal).First();
    }
}
