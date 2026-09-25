// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Ordinaryunus.Data;

namespace Ordinaryunus.Brain;

/// <summary>Result of resolving one wikilink target against the brain index.</summary>
public readonly record struct ResolvedLink(bool Found, string? RelPath);

public sealed class RenderInfo
{
    public bool Truncated;
}

/// <summary>
/// Safe markdown → HTML renderer (MIMARI §4). Single pass, line-based. Every text run goes through
/// <see cref="WebUtility.HtmlEncode(string)"/>; only the fixed set of tags below is ever emitted, and no
/// attribute value is ever built from unescaped input. No href/src/style/on* attribute is ever produced.
/// </summary>
public static partial class MarkdownRenderer
{
    public const int CapBytes = 1_000_000;

    static readonly HashSet<string> AllowedCallouts =
    [
        "note", "info", "tip", "success", "question", "warning", "danger", "failure", "bug", "example", "quote", "abstract", "todo", "ai",
    ];

    public static string Render(string? markdown, Func<string, ResolvedLink> resolve, out RenderInfo info)
    {
        info = new RenderInfo();
        string text = markdown ?? "";
        if (text.Length > CapBytes) { text = text[..CapBytes]; info.Truncated = true; }
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int i = Frontmatter.BodyStart(lines);
        var sb = new StringBuilder();
        while (i < lines.Length)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) { i++; continue; }

            string? lang = FenceStart(line);
            if (lang is not null) { i = RenderFence(lines, i, lang, sb); continue; }

            var h = HeadingMatch(line);
            if (h is { } hv) { RenderHeading(hv.level, hv.text, i + 1, resolve, sb); i++; continue; }

            if (IsHr(line)) { sb.Append("<hr>"); i++; continue; }

            if (MarkdownTable.IsTableLine(line) && i + 1 < lines.Length && IsTableSep(lines[i + 1]))
            { i = RenderTable(lines, i, resolve, sb); continue; }

            if (line.TrimStart().StartsWith('>')) { i = RenderBlockquote(lines, i, resolve, sb); continue; }

            if (IsListItem(line)) { i = RenderListLevel(lines, i, -1, resolve, sb); continue; }

            i = RenderParagraph(lines, i, resolve, sb);
        }
        return sb.ToString();
    }

    // ---------------- block helpers ----------------

    static bool IsBlockStart(string[] lines, int idx)
    {
        string line = lines[idx];
        if (string.IsNullOrWhiteSpace(line)) return true;
        if (HeadingMatch(line) is not null) return true;
        if (FenceStart(line) is not null) return true;
        if (IsHr(line)) return true;
        if (line.TrimStart().StartsWith('>')) return true;
        if (IsListItem(line)) return true;
        if (MarkdownTable.IsTableLine(line) && idx + 1 < lines.Length && IsTableSep(lines[idx + 1])) return true;
        return false;
    }

    static (int level, string text)? HeadingMatch(string line)
    {
        var m = HeadingRx().Match(line);
        if (!m.Success) return null;
        return (m.Groups[1].Value.Length, m.Groups[2].Value.TrimEnd().TrimEnd('#').TrimEnd());
    }

    static bool IsHr(string line)
    {
        string t = line.Trim();
        if (t.Length < 3) return false;
        char c = t[0];
        if (c is not ('-' or '*' or '_')) return false;
        foreach (char ch in t) if (ch != c && ch != ' ') return false;
        return t.Count(ch => ch == c) >= 3;
    }

    static string? FenceStart(string line)
    {
        string t = line.TrimStart();
        if (t.StartsWith("```", StringComparison.Ordinal)) return SanitizeLang(t[3..]);
        if (t.StartsWith("~~~", StringComparison.Ordinal)) return SanitizeLang(t[3..]);
        return null;
    }

    static string SanitizeLang(string s)
    {
        var sb = new StringBuilder();
        foreach (char c0 in s.Trim().ToLowerInvariant())
        {
            char c = c0;
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-') sb.Append(c);
            else break;
        }
        return sb.ToString();
    }

    static int RenderFence(string[] lines, int i, string lang, StringBuilder sb)
    {
        string marker = lines[i].TrimStart().StartsWith("~~~", StringComparison.Ordinal) ? "~~~" : "```";
        int j = i + 1;
        var content = new List<string>();
        while (j < lines.Length && !lines[j].TrimStart().StartsWith(marker, StringComparison.Ordinal)) { content.Add(lines[j]); j++; }
        string cls = lang.Length > 0 ? $" class=\"lang-{lang}\"" : "";
        sb.Append("<pre").Append(cls).Append("><code>").Append(WebUtility.HtmlEncode(string.Join('\n', content))).Append("</code></pre>");
        return j < lines.Length ? j + 1 : j;
    }

    static void RenderHeading(int level, string text, int lineNum, Func<string, ResolvedLink> resolve, StringBuilder sb)
    {
        int lvl = Math.Clamp(level, 1, 6);
        sb.Append("<h").Append(lvl).Append(" id=\"h-").Append(lineNum).Append("\">")
          .Append(InlineToHtml(text, resolve, allowTags: false))
          .Append("</h").Append(lvl).Append('>');
    }

    static bool IsTableSep(string line)
    {
        var cells = MarkdownTable.SplitCells(line);
        return cells.Count > 0 && cells.All(c => c.Trim().Length > 0 && c.Trim().All(ch => ch is '-' or ':' or ' '));
    }

    static int RenderTable(string[] lines, int i, Func<string, ResolvedLink> resolve, StringBuilder sb)
    {
        var header = MarkdownTable.SplitCells(lines[i]).Select(c => c.Trim()).ToList();
        var sep = MarkdownTable.SplitCells(lines[i + 1]).Select(c => c.Trim()).ToList();
        var aligns = sep.Select(c => c.StartsWith(':') && c.EndsWith(':') ? "al-c" : c.EndsWith(':') ? "al-r" : c.StartsWith(':') ? "al-l" : null).ToList();
        sb.Append("<table><thead><tr>");
        for (int c = 0; c < header.Count; c++)
        {
            string cls = c < aligns.Count && aligns[c] is { } a ? $" class=\"{a}\"" : "";
            sb.Append("<th").Append(cls).Append('>').Append(InlineToHtml(header[c], resolve, true)).Append("</th>");
        }
        sb.Append("</tr></thead><tbody>");
        int j = i + 2;
        for (; j < lines.Length && MarkdownTable.IsTableLine(lines[j]); j++)
        {
            var cells = MarkdownTable.SplitCells(lines[j]).Select(c => c.Trim()).ToList();
            sb.Append("<tr>");
            for (int c = 0; c < cells.Count; c++)
            {
                string cls = c < aligns.Count && aligns[c] is { } a ? $" class=\"{a}\"" : "";
                sb.Append("<td").Append(cls).Append('>').Append(InlineToHtml(cells[c], resolve, true)).Append("</td>");
            }
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table>");
        return j;
    }

    static int RenderBlockquote(string[] lines, int i, Func<string, ResolvedLink> resolve, StringBuilder sb)
    {
        var content = new List<string>();
        while (i < lines.Length && lines[i].TrimStart().StartsWith('>'))
        {
            string l = lines[i].TrimStart()[1..];
            if (l.StartsWith(' ')) l = l[1..];
            content.Add(l);
            i++;
        }
        if (content.Count == 0) return i;
        var m = CalloutRx().Match(content[0]);
        if (m.Success)
        {
            string type = m.Groups[1].Value.ToLowerInvariant();
            if (!AllowedCallouts.Contains(type)) type = "note";
            string title = m.Groups[2].Value.Trim();
            sb.Append("<div class=\"callout callout-").Append(type).Append("\">");
            sb.Append("<div class=\"callout-title\">").Append(InlineToHtml(title.Length == 0 ? type : title, resolve, true)).Append("</div>");
            var body = content.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (body.Count > 0)
                sb.Append("<p>").Append(string.Join("<br>", body.Select(l => InlineToHtml(l, resolve, true)))).Append("</p>");
            sb.Append("</div>");
        }
        else
        {
            sb.Append("<blockquote><p>").Append(string.Join("<br>", content.Select(l => InlineToHtml(l, resolve, true)))).Append("</p></blockquote>");
        }
        return i;
    }

    static bool IsListItem(string line) => ListItemRx().IsMatch(line);

    static (int indent, bool ordered, int? start, bool? done, string content) ParseListItem(string line)
    {
        var m = ListItemRx().Match(line);
        int indent = 0;
        foreach (char c in m.Groups[1].Value) indent += c == '\t' ? 2 : 1;
        bool ordered = char.IsDigit(m.Groups[2].Value[0]);
        int? start = ordered && int.TryParse(m.Groups[2].Value.TrimEnd('.', ')'), out var n) ? n : null;
        bool? done = m.Groups[3].Success ? m.Groups[3].Value is "x" or "X" : null;
        return (indent, ordered, start, done, m.Groups[4].Value);
    }

    static int RenderListLevel(string[] lines, int i, int parentIndent, Func<string, ResolvedLink> resolve, StringBuilder sb)
    {
        if (i >= lines.Length || !IsListItem(lines[i])) return i;
        var first = ParseListItem(lines[i]);
        if (first.indent <= parentIndent) return i;
        int levelIndent = first.indent;
        bool ordered = first.ordered;
        sb.Append(ordered ? (first.start is int st && st != 1 ? $"<ol start=\"{st}\">" : "<ol>") : "<ul>");
        while (i < lines.Length)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                int peek = i + 1;
                if (peek < lines.Length && IsListItem(lines[peek]) && ParseListItem(lines[peek]).indent >= levelIndent) { i++; continue; }
                break;
            }
            if (!IsListItem(lines[i])) break;
            var item = ParseListItem(lines[i]);
            if (item.indent < levelIndent) break;
            if (item.indent > levelIndent) break; // handled by the recursive call below on the previous iteration
            string liClass = item.done is null ? "" : item.done.Value ? " class=\"task done\"" : " class=\"task\"";
            sb.Append("<li").Append(liClass).Append('>');
            if (item.done is not null) sb.Append("<span class=\"cb").Append(item.done.Value ? " done" : "").Append("\"></span>");
            sb.Append(InlineToHtml(item.content, resolve, true));
            i++;
            if (i < lines.Length && IsListItem(lines[i]) && ParseListItem(lines[i]).indent > levelIndent)
                i = RenderListLevel(lines, i, levelIndent, resolve, sb);
            sb.Append("</li>");
        }
        sb.Append(ordered ? "</ol>" : "</ul>");
        return i;
    }

    static int RenderParagraph(string[] lines, int i, Func<string, ResolvedLink> resolve, StringBuilder sb)
    {
        var buf = new List<string>();
        while (i < lines.Length && !IsBlockStart(lines, i)) { buf.Add(lines[i]); i++; }
        if (buf.Count == 0) return i + 1; // safety: never stall
        sb.Append("<p>").Append(string.Join("<br>", buf.Select(l => InlineToHtml(l, resolve, true)))).Append("</p>");
        return i;
    }

    // ---------------- inline ----------------

    static string InlineToHtml(string raw, Func<string, ResolvedLink> resolve, bool allowTags)
    {
        var sb = new StringBuilder();
        int pos = 0;
        foreach (Match m in InlineRx().Matches(raw))
        {
            if (!allowTags && m.Groups["tag"].Success) continue;
            if (m.Index < pos) continue;
            if (m.Index > pos) sb.Append(WebUtility.HtmlEncode(raw[pos..m.Index]));
            AppendToken(sb, m, resolve);
            pos = m.Index + m.Length;
        }
        if (pos < raw.Length) sb.Append(WebUtility.HtmlEncode(raw[pos..]));
        return sb.ToString();
    }

    static void AppendToken(StringBuilder sb, Match m, Func<string, ResolvedLink> resolve)
    {
        if (m.Groups["code"].Success)
        {
            string content = m.Value[1..^1];
            sb.Append("<code>").Append(WebUtility.HtmlEncode(content)).Append("</code>");
        }
        else if (m.Groups["embed"].Success)
        {
            string inner = m.Value[3..^2];
            int pipe = inner.IndexOf('|');
            if (pipe >= 0) inner = inner[..pipe];
            sb.Append("<span class=\"embed\">").Append(WebUtility.HtmlEncode("📎 " + DisplayTarget(inner))).Append("</span>");
        }
        else if (m.Groups["wikialias"].Success)
        {
            string inner = m.Value[2..^2];
            int pipe = inner.IndexOf('|');
            RenderWikiLink(sb, inner[..pipe], inner[(pipe + 1)..], resolve);
        }
        else if (m.Groups["wiki"].Success)
        {
            string inner = m.Value[2..^2];
            RenderWikiLink(sb, inner, DisplayTarget(inner), resolve);
        }
        else if (m.Groups["mdlink"].Success)
        {
            string linkText = m.Groups["mdtext"].Value;
            linkText = linkText.Length >= 2 ? linkText[1..^1] : linkText;
            string url = m.Groups["mdurl"].Value;
            if (Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
                sb.Append("<a class=\"ext\" data-url=\"").Append(WebUtility.HtmlEncode(url)).Append("\">")
                  .Append(WebUtility.HtmlEncode(linkText.Length == 0 ? url : linkText)).Append("</a>");
            else
                sb.Append(WebUtility.HtmlEncode(m.Value));
        }
        else if (m.Groups["bold"].Success)
        {
            string inner = m.Value[2..^2];
            sb.Append("<strong>").Append(WebUtility.HtmlEncode(inner)).Append("</strong>");
        }
        else if (m.Groups["strike"].Success)
        {
            sb.Append("<del>").Append(WebUtility.HtmlEncode(m.Value[2..^2])).Append("</del>");
        }
        else if (m.Groups["mark"].Success)
        {
            sb.Append("<mark>").Append(WebUtility.HtmlEncode(m.Value[2..^2])).Append("</mark>");
        }
        else if (m.Groups["italic"].Success)
        {
            sb.Append("<em>").Append(WebUtility.HtmlEncode(m.Value[1..^1])).Append("</em>");
        }
        else if (m.Groups["tag"].Success)
        {
            sb.Append("<span class=\"tag\">").Append(WebUtility.HtmlEncode(m.Value)).Append("</span>");
        }
        else if (m.Groups["url"].Success)
        {
            string url = m.Value.TrimEnd('.', ',', ')');
            sb.Append("<a class=\"ext\" data-url=\"").Append(WebUtility.HtmlEncode(url)).Append("\">").Append(WebUtility.HtmlEncode(url)).Append("</a>");
            if (url.Length < m.Value.Length) sb.Append(WebUtility.HtmlEncode(m.Value[url.Length..]));
        }
    }

    static void RenderWikiLink(StringBuilder sb, string targetRaw, string label, Func<string, ResolvedLink> resolve)
    {
        string target = StripAnchor(targetRaw).Trim();
        string labelText = label.Trim();
        if (labelText.Length == 0) labelText = target;
        string labelHtml = WebUtility.HtmlEncode(labelText);
        var resolved = target.Length == 0 ? new ResolvedLink(false, null) : resolve(target);
        if (resolved.Found && resolved.RelPath is not null)
            sb.Append("<a class=\"wl\" data-note=\"").Append(WebUtility.HtmlEncode(resolved.RelPath)).Append("\">").Append(labelHtml).Append("</a>");
        else
            sb.Append("<span class=\"wl broken\" title=\"").Append(WebUtility.HtmlEncode("Not bulunamadı: " + target)).Append("\">").Append(labelHtml).Append("</span>");
    }

    static string DisplayTarget(string target)
    {
        string t = StripAnchor(target);
        int slash = t.LastIndexOf('/');
        return slash >= 0 && slash < t.Length - 1 ? t[(slash + 1)..] : t;
    }

    static string StripAnchor(string target)
    {
        int hash = target.IndexOf('#');
        if (hash >= 0) target = target[..hash];
        int caret = target.IndexOf('^');
        if (caret >= 0) target = target[..caret];
        return target;
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")] private static partial Regex HeadingRx();
    [GeneratedRegex(@"^\[!([A-Za-z]+)\]\s*(.*)$")] private static partial Regex CalloutRx();
    [GeneratedRegex(@"^(\s*)([-*+]|\d+[.)])\s+(?:\[([ xX])\]\s?)?(.*)$")] private static partial Regex ListItemRx();

    // Ordered so a code span always wins over any other construct that might start at the same position.
    [GeneratedRegex(
        @"(?<code>`[^`]*`)" +
        @"|(?<embed>!\[\[[^\[\]]*\]\])" +
        @"|(?<wikialias>\[\[[^\[\]|]*\|[^\[\]]*\]\])" +
        @"|(?<wiki>\[\[[^\[\]]*\]\])" +
        @"|(?<mdlink>(?<mdtext>\[[^\[\]]*\])\((?<mdurl>[^()\s]*)\))" +
        @"|(?<bold>\*\*[^*]+\*\*|__[^_]+__)" +
        @"|(?<strike>~~[^~]+~~)" +
        @"|(?<mark>==[^=]+==)" +
        @"|(?<italic>\*[^*\s][^*]*\*|_[^_\s][^_]*_)" +
        @"|(?<tag>(?<![\w#])#[A-Za-z][A-Za-z0-9_/-]*)" +
        @"|(?<url>https?://[^\s|<>\]\)]+)")]
    private static partial Regex InlineRx();
}
