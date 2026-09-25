// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ordinaryunus.Data;

/// <summary>Markdown / text helpers shared by all parsers.</summary>
public static partial class Md
{
    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    static readonly UTF8Encoding Utf8NoBom = new(false, false);

    /// <summary>Reads a text file as UTF-8 (BOM optional). Returns null if missing or unreadable.</summary>
    public static string? ReadText(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            byte[] bytes = ReadBytesShared(path);
            int offset = HasBom(bytes) ? 3 : 0;
            return Utf8NoBom.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public static byte[] ReadBytesShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var ms = new MemoryStream();
        fs.CopyTo(ms);
        return ms.ToArray();
    }

    public static bool HasBom(byte[] b) => b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF;

    /// <summary>Splits into lines without line terminators (handles LF and CRLF).</summary>
    public static string[] Lines(string? text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    /// <summary>Returns the body lines (after the YAML frontmatter, if any).</summary>
    public static string[] BodyLines(string? text)
    {
        var lines = Lines(text);
        int start = Frontmatter.BodyStart(lines);
        return lines[start..];
    }

    /// <summary>
    /// Lines of the section whose "## " heading starts with <paramref name="headingPrefix"/>
    /// (case-insensitive), until the next heading of the same or higher level. Null if missing.
    /// </summary>
    public static List<string>? Section(IReadOnlyList<string> lines, string headingPrefix, int level = 2)
    {
        string prefix = Normalize(headingPrefix);
        return Sections(lines, h => Normalize(h).StartsWith(prefix, StringComparison.Ordinal), level).FirstOrDefault();
    }

    /// <summary>
    /// Başlık metni (işaretsiz, ör. "Benden Beklenenler") <paramref name="headingMatches"/> koşulunu sağlayan HER
    /// <paramref name="level"/> düzeyindeki bölümün satırları, belge sırasıyla. Her bölüm aynı ya da daha üst düzeydeki
    /// bir sonraki başlıkta biter; kod blokları (```) içindeki başlıklar sayılmaz.
    /// </summary>
    public static IEnumerable<List<string>> Sections(IReadOnlyList<string> lines, Func<string, bool> headingMatches, int level = 2)
    {
        string marker = new string('#', level) + " ";
        bool fenceOutside = false;
        for (int i = 0; i < lines.Count; i++)
        {
            string l = lines[i].TrimEnd();
            if (l.TrimStart().StartsWith("```", StringComparison.Ordinal)) { fenceOutside = !fenceOutside; continue; }
            if (fenceOutside || !l.StartsWith(marker, StringComparison.Ordinal) || !headingMatches(l[marker.Length..])) continue;
            var result = new List<string>();
            bool inFence = false;
            int j = i + 1;
            for (; j < lines.Count; j++)
            {
                string s = lines[j];
                if (s.TrimStart().StartsWith("```", StringComparison.Ordinal)) inFence = !inFence;
                if (!inFence && IsHeadingAtOrAbove(s, level)) break;
                result.Add(s);
            }
            yield return result;
            i = j - 1; // bir sonraki arama bu bölümün bittiği başlıktan devam eder
        }
    }

    /// <summary>Index of the heading line for a section (or -1).</summary>
    public static int SectionIndex(IReadOnlyList<string> lines, string headingPrefix, int level = 2)
    {
        string marker = new string('#', level) + " ";
        for (int i = 0; i < lines.Count; i++)
        {
            string l = lines[i].TrimEnd();
            if (l.StartsWith(marker, StringComparison.Ordinal) &&
                Normalize(l[marker.Length..]).StartsWith(Normalize(headingPrefix), StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    public static bool IsHeadingAtOrAbove(string line, int level)
    {
        int hashes = 0;
        while (hashes < line.Length && line[hashes] == '#') hashes++;
        return hashes >= 1 && hashes <= level && hashes < line.Length && line[hashes] == ' ';
    }

    /// <summary>Search fold (MIMARI §5.7): lowercase tr-TR + Turkish letter → ASCII, exactly 1 char per char so
    /// indices of the folded text always match indices of the original (needed for snippet highlighting).</summary>
    public static string Fold1to1(string s)
    {
        var arr = s.ToCharArray();
        for (int i = 0; i < arr.Length; i++)
        {
            char c = char.ToLower(arr[i], Tr);
            arr[i] = c switch
            {
                'ı' => 'i', 'ş' => 's', 'ğ' => 'g', 'ü' => 'u', 'ö' => 'o', 'ç' => 'c', 'â' => 'a', 'î' => 'i', 'û' => 'u',
                _ => c,
            };
        }
        return new string(arr);
    }

    /// <summary>Lowercases (Turkish-aware), folds Turkish letters and emoji so headings compare robustly.</summary>
    public static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c0 in s.Trim().ToLower(Tr))
        {
            char c = c0 switch
            {
                'ı' => 'i', 'ş' => 's', 'ğ' => 'g', 'ü' => 'u', 'ö' => 'o', 'ç' => 'c', 'â' => 'a', 'î' => 'i', 'û' => 'u',
                _ => c0
            };
            if (char.IsLetterOrDigit(c) || c == ' ' || c == '\'' || c == '-') sb.Append(c);
        }
        return MultiSpace().Replace(sb.ToString(), " ").Trim();
    }

    /// <summary>Converts markdown-ish text to plain display text.</summary>
    public static string Plain(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        string t = s;
        t = HtmlComment().Replace(t, "");
        t = ObsidianComment().Replace(t, "");
        t = Embed().Replace(t, "");                              // ![[x]] embeds disappear
        t = WikiAlias().Replace(t, m => m.Groups[2].Value);        // [[A|B]] -> B
        t = Wiki().Replace(t, m => StripHeadingRef(m.Groups[1].Value)); // [[A]] -> A
        t = MdLink().Replace(t, m => m.Groups[1].Value);           // [x](url) -> x
        t = t.Replace("**", "").Replace("__", "");
        t = t.Replace("`", "");
        t = t.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
        t = MultiSpace().Replace(t, " ");
        return t.Trim();
    }

    static string StripHeadingRef(string target)
    {
        int hash = target.IndexOf('#');
        string t = hash > 0 ? target[..hash] : target;
        // show only the file name of a path link
        int slash = t.LastIndexOf('/');
        if (slash >= 0 && slash < t.Length - 1) t = t[(slash + 1)..];
        return t;
    }

    /// <summary>Removes multi-line %%...%% and HTML comments from a whole document.</summary>
    public static string StripComments(string text)
    {
        text = HtmlComment().Replace(text, "");
        text = ObsidianComment().Replace(text, "");
        return text;
    }

    /// <summary>Extracts the first URL from a markdown link or bare URL.</summary>
    public static string? FirstUrl(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var m = MdLink().Match(s);
        if (m.Success && IsHttp(m.Groups[2].Value)) return m.Groups[2].Value.Trim();
        var b = BareUrl().Match(s);
        return b.Success ? b.Value.TrimEnd(')', '.', ',') : null;
    }

    public static bool IsHttp(string url) =>
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

    public static DateOnly? FirstIsoDate(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        var m = IsoDate().Match(s);
        if (!m.Success) return null;
        return DateOnly.TryParseExact(m.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    public static string AfterArrow(string s)
    {
        int i = s.IndexOf('→');
        return i >= 0 ? s[(i + 1)..].Trim() : s.Trim();
    }

    public static string Clip(string s, int max) => s.Length <= max ? s : s[..(max - 1)].TrimEnd() + "…";

    /// <summary>Checkbox line "- [ ] text" / "- [x] text". Returns null if not a checkbox.</summary>
    public static (bool done, string text)? Checkbox(string line)
    {
        var m = CheckboxRx().Match(line);
        if (!m.Success) return null;
        bool done = m.Groups[1].Value is "x" or "X";
        return (done, m.Groups[2].Value.Trim());
    }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)] private static partial Regex HtmlComment();
    [GeneratedRegex(@"%%.*?%%", RegexOptions.Singleline)] private static partial Regex ObsidianComment();
    [GeneratedRegex(@"!\[\[[^\]]*\]\]")] private static partial Regex Embed();
    [GeneratedRegex(@"\[\[([^\]|]*)\|([^\]]*)\]\]")] private static partial Regex WikiAlias();
    [GeneratedRegex(@"\[\[([^\]]*)\]\]")] private static partial Regex Wiki();
    [GeneratedRegex(@"\[([^\]]*)\]\(([^)\s]*)\)")] private static partial Regex MdLink();
    [GeneratedRegex(@"https?://[^\s|<>\]]+")] private static partial Regex BareUrl();
    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b")] private static partial Regex IsoDate();
    [GeneratedRegex(@"[ \t]{2,}")] private static partial Regex MultiSpace();
    [GeneratedRegex(@"^\s*[-*+]\s+\[([ xX])\]\s?(.*)$")] private static partial Regex CheckboxRx();
}
