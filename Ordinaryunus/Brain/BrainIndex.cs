// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Diagnostics;
using System.Text;
using Ordinaryunus.Data;

namespace Ordinaryunus.Brain;

public sealed record HeadingInfo(int Level, string Text, int Line);

/// <summary>One indexed markdown note (MIMARI §5.2).</summary>
public sealed class NoteInfo
{
    public required string RelPath { get; init; }
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "not";
    public string? Project { get; set; }
    public Dictionary<string, string> Frontmatter { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Aliases { get; set; } = [];
    public List<HeadingInfo> Headings { get; set; } = [];
    public List<RawLink> Links { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public int TasksOpen { get; set; }
    public int TasksDone { get; set; }
    public int Words { get; set; }
    public string PlainText { get; set; } = "";
    public string FoldedText { get; set; } = "";
    public DateTime Mtime { get; set; }
    public long Size { get; set; }
    /// <summary>Raw file text (frontmatter included); kept in memory only for notes, used by getNote/search.</summary>
    public string RawText { get; set; } = "";
}

/// <summary>Any indexed file (note or not), for the tree and graph "linked file" nodes.</summary>
public sealed class FileEntry
{
    public required string RelPath { get; init; }
    public DateTime Mtime { get; set; }
    public long Size { get; set; }
    public bool IsMarkdown { get; set; }
}

/// <summary>
/// Read-only scanner of the vault (MIMARI §5.1): recursive enumeration that never enters excluded directories
/// (so "80 Oturum Arşivi" is never even listed), incremental re-parse by (mtime,size), full link-map rebuild
/// every update (cheap for a few hundred notes).
/// </summary>
public sealed partial class BrainIndex(string root)
{
    public string Root { get; } = root;
    public Dictionary<string, NoteInfo> Notes { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, FileEntry> Files { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public WikiLinkResolver Resolver { get; private set; } = new([], new Dictionary<string, List<string>>());

    /// <summary>Target relPath (or "ghost:&lt;target&gt;" when unresolved) → list of source relPaths that link to it.</summary>
    public Dictionary<string, List<string>> BackLinks { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Source relPath → its unresolved (broken) raw links.</summary>
    public Dictionary<string, List<RawLink>> Broken { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Source relPath → (target relPath, RawLink) for every resolved link.</summary>
    public Dictionary<string, List<(string target, RawLink link)>> Resolved { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool Truncated { get; private set; }
    public DateTime IndexedAt { get; private set; }
    public long IndexMs { get; private set; }
    public List<string> Warnings { get; } = [];

    const string ExcludedTop = "80 Oturum Arşivi";
    static readonly HashSet<string> SkipDirNames = new(StringComparer.OrdinalIgnoreCase) { "node_modules" };
    const long MaxNoteBytes = 5 * 1024 * 1024;
    const int MaxFiles = 5000;

    public void Update()
    {
        var sw = Stopwatch.StartNew();
        var newFiles = new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
        bool cap = false;
        int count = 0;
        var stack = new Stack<string>();
        stack.Push(Root);
        while (stack.Count > 0)
        {
            string dir = stack.Pop();
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(dir); } catch { continue; }
            foreach (var full in entries)
            {
                FileAttributes attr;
                try { attr = File.GetAttributes(full); } catch { continue; }
                if (attr.HasFlag(FileAttributes.ReparsePoint)) continue; // never follow junctions/symlinks
                string name = Path.GetFileName(full).Normalize(NormalizationForm.FormC);
                if ((attr & FileAttributes.Directory) != 0)
                {
                    if (name.StartsWith('.') || SkipDirNames.Contains(name)) continue;
                    string relDir = RelPath(full);
                    if (string.Equals(relDir, ExcludedTop, StringComparison.OrdinalIgnoreCase)) continue;
                    stack.Push(full);
                    continue;
                }
                if (attr.HasFlag(FileAttributes.System)) continue;
                if (count >= MaxFiles) { cap = true; continue; }
                FileInfo fi;
                try { fi = new FileInfo(full); } catch { continue; }
                count++;
                string rel = RelPath(full);
                newFiles[rel] = new FileEntry { RelPath = rel, Mtime = fi.LastWriteTimeUtc, Size = fi.Length, IsMarkdown = rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase) };
            }
        }
        Truncated = cap;
        Warnings.Clear();
        if (cap) Warnings.Add("Kasada 5000'den fazla dosya var; ilk 5000 gösteriliyor.");

        var newNotes = new Dictionary<string, NoteInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var (rel, fe) in newFiles)
        {
            if (!fe.IsMarkdown || fe.Size > MaxNoteBytes) continue;
            if (Notes.TryGetValue(rel, out var old) && old.Mtime == fe.Mtime && old.Size == fe.Size) { newNotes[rel] = old; continue; }
            var note = ParseNote(rel, fe, newFiles);
            if (note is not null) newNotes[rel] = note;
        }
        Files = newFiles;
        Notes = newNotes;

        // Second pass: now that every card path is known, resolve each note's Project (needs the full file set).
        foreach (var n in Notes.Values) n.Project = ProjectOf(n.RelPath, Files);

        RebuildLinks();
        IndexedAt = DateTime.Now;
        IndexMs = sw.ElapsedMilliseconds;
    }

    string RelPath(string full) => Path.GetRelativePath(Root, full).Replace('\\', '/');

    NoteInfo? ParseNote(string rel, FileEntry fe, Dictionary<string, FileEntry> allFiles)
    {
        string full = Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar));
        string? text = Md.ReadText(full);
        if (text is null) return null;
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var fm = Frontmatter.Parse(lines);
        var (kind, _) = Classify(rel);
        var headings = ExtractHeadings(lines);
        string title = headings.Count > 0 ? headings[0].Text : Path.GetFileNameWithoutExtension(rel);
        var links = WikiLinks.Extract(text);
        var aliases = ParseAliases(fm.Get("aliases"));
        var tags = ExtractTags(lines, fm.Get("tags"));
        var (open, done) = CountTasks(lines);
        string plain = string.Join(' ', Md.BodyLines(text).Select(Md.Plain)).Trim();
        int words = plain.Length == 0 ? 0 : plain.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return new NoteInfo
        {
            RelPath = rel,
            Title = title,
            Kind = kind,
            Frontmatter = fm.Values.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase),
            Aliases = aliases,
            Headings = headings,
            Links = links,
            Tags = tags,
            TasksOpen = open,
            TasksDone = done,
            Words = words,
            PlainText = plain,
            FoldedText = Md.Fold1to1(plain + " " + title),
            Mtime = fe.Mtime,
            Size = fe.Size,
            RawText = text,
        };
    }

    static List<HeadingInfo> ExtractHeadings(string[] lines)
    {
        var result = new List<HeadingInfo>();
        bool inFence = false;
        int start = Frontmatter.BodyStart(lines);
        for (int i = start; i < lines.Length; i++)
        {
            string t = lines[i].TrimStart();
            if (t.StartsWith("```", StringComparison.Ordinal) || t.StartsWith("~~~", StringComparison.Ordinal)) { inFence = !inFence; continue; }
            if (inFence) continue;
            int hashes = 0;
            while (hashes < lines[i].Length && lines[i][hashes] == '#') hashes++;
            if (hashes is >= 1 and <= 6 && hashes < lines[i].Length && lines[i][hashes] == ' ')
                result.Add(new HeadingInfo(hashes, Md.Plain(lines[i][(hashes + 1)..].TrimEnd().TrimEnd('#').TrimEnd()), i + 1));
        }
        return result;
    }

    static List<string> ParseAliases(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        string s = raw.Trim();
        if (s.StartsWith('[') && s.EndsWith(']')) s = s[1..^1];
        return s.Split(',').Select(x => x.Trim().Trim('"', '\'')).Where(x => x.Length > 0).ToList();
    }

    static List<string> ExtractTags(string[] lines, string frontmatterTags)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in ParseAliases(frontmatterTags)) set.Add(t.TrimStart('#'));
        string masked = WikiLinks.Mask(string.Join('\n', lines));
        foreach (System.Text.RegularExpressions.Match m in TagRx().Matches(masked)) set.Add(m.Groups[1].Value);
        return set.Where(s => s.Length > 0).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"(?<![\w#])#([A-Za-z][A-Za-z0-9_/-]*)")]
    private static partial System.Text.RegularExpressions.Regex TagRx();

    static (int open, int done) CountTasks(string[] lines)
    {
        int open = 0, done = 0;
        bool inFence = false;
        foreach (var l in lines)
        {
            string t = l.TrimStart();
            if (t.StartsWith("```", StringComparison.Ordinal) || t.StartsWith("~~~", StringComparison.Ordinal)) { inFence = !inFence; continue; }
            if (inFence) continue;
            var cb = Md.Checkbox(l);
            if (cb is null) continue;
            if (cb.Value.done) done++; else open++;
        }
        return (open, done);
    }

    /// <summary>Kind classification by path (MIMARI §5.3), first matching rule wins.</summary>
    public static (string kind, string label) Classify(string relPath)
    {
        string rel = relPath.Replace('\\', '/');
        var parts = rel.Split('/');
        bool isMd = rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

        if (rel.Equals("01 Şimdi.md", StringComparison.OrdinalIgnoreCase)) return ("simdi", "Şimdi");
        if (parts.Length == 1 && isMd) return ("sistem", "Sistem");

        if (parts.Length >= 1 && parts[0] == "20 Projeler")
        {
            if (parts.Length == 2 && isMd) return ("proje-listesi", "Proje listesi");
            if (parts.Length >= 3)
            {
                string proj = parts[1];
                if (parts.Length == 3 && string.Equals(parts[2], proj + ".md", StringComparison.OrdinalIgnoreCase)) return ("proje-karti", "Proje kartı");
                if (parts.Length == 3 && string.Equals(parts[2], "Kayıt.md", StringComparison.OrdinalIgnoreCase)) return ("kayit", "Kayıt");
                if (parts.Length >= 4 && parts[2] == "Görevler" && isMd) return ("gorev", "Görev");
                return ("proje-notu", "Proje notu");
            }
        }
        if (parts.Length >= 1 && parts[0] == "10 Gelen Kutusu") return ("gelen", "Gelen kutusu");
        if (parts.Length >= 1 && parts[0] == "30 Park Yeri") return ("fikir", "Fikir");
        if (parts.Length >= 1 && parts[0] == "40 Alanlar") return ("alan", "Alan");
        if (parts.Length >= 1 && parts[0] == "50 Kaynaklar")
        {
            if (parts.Length >= 2 && parts[1] == "Araştırma") return ("arastirma", "Araştırma");
            if (parts.Length >= 2 && parts[1] == "Ham") return ("ham", "Ham kaynak");
            return ("kaynak", "Kaynak");
        }
        if (parts.Length >= 1 && parts[0] == "60 Ekip")
        {
            if (parts.Length >= 2 && parts[1] == "Roller") return ("rol", "Rol");
            return ("ekip", "Ekip");
        }
        if (parts.Length >= 1 && parts[0] == "70 Günlük") return ("gunluk", "Günlük");
        if (parts.Length >= 1 && parts[0] == "90 Arşiv") return ("arsiv", "Arşiv");
        if (parts.Length >= 1 && parts[0] == "_sistem")
        {
            if (parts.Length >= 2 && parts[1] == "Şablonlar") return ("sablon", "Şablon");
            return ("sistem", "Sistem");
        }
        return ("not", "Not");
    }

    static string? ProjectOf(string relPath, Dictionary<string, FileEntry> files)
    {
        var parts = relPath.Replace('\\', '/').Split('/');
        if (parts.Length < 2 || parts[0] != "20 Projeler") return null;
        string proj = parts[1];
        string card = $"20 Projeler/{proj}/{proj}.md";
        return files.ContainsKey(card) ? proj : null;
    }

    void RebuildLinks()
    {
        var allPaths = Files.Keys.ToList();
        var aliasMap = Notes.Values.Where(n => n.Aliases.Count > 0).ToDictionary(n => n.RelPath, n => n.Aliases);
        Resolver = new WikiLinkResolver(allPaths, aliasMap);
        var back = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var broken = new Dictionary<string, List<RawLink>>(StringComparer.OrdinalIgnoreCase);
        var resolved = new Dictionary<string, List<(string, RawLink)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var note in Notes.Values)
        {
            bool isTemplate = note.Kind == "sablon";
            foreach (var link in note.Links)
            {
                string? target = Resolver.Resolve(link.Target);
                if (target is null)
                {
                    if (!isTemplate)
                    {
                        if (!broken.TryGetValue(note.RelPath, out var l)) broken[note.RelPath] = l = [];
                        l.Add(link);
                    }
                    continue;
                }
                if (!resolved.TryGetValue(note.RelPath, out var rl)) resolved[note.RelPath] = rl = [];
                rl.Add((target, link));
                if (!back.TryGetValue(target, out var bl)) back[target] = bl = [];
                bl.Add(note.RelPath);
            }
        }
        BackLinks = back;
        Broken = broken;
        Resolved = resolved;
    }

    public ResolvedLink ResolveForRender(string target)
    {
        string? p = Resolver.Resolve(target);
        return new ResolvedLink(p is not null, p);
    }

    public NoteInfo? Find(string relPath) => Notes.TryGetValue(relPath, out var n) ? n : null;
}
