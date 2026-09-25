// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ordinaryunus.Data;

/// <summary>Read-only parsers for every vault source. Never throws for missing files; records warnings instead.</summary>
public sealed partial class VaultReader(string vaultRoot)
{
    public string Root { get; } = vaultRoot;
    public List<string> Warnings { get; } = [];

    public const string SimdiRel = "01 Şimdi.md";
    public const string JobsRel = "40 Alanlar/İş Başvuruları.md";
    public const string IncomeRel = "40 Alanlar/Gelir Defteri.md";
    /// <summary>LinkedIn takviminin dosya adı. Önce <see cref="LinkedInRel"/> aranır; yoksa herhangi bir proje
    /// klasöründeki (20 Projeler/&lt;proje&gt;/) aynı adlı dosya kullanılır.</summary>
    public const string LinkedInFileName = "LinkedIn Takvimi.md";
    public const string LinkedInRel = "40 Alanlar/" + LinkedInFileName;
    public const string LedgerRel = "_sistem/istek-defteri/istekler.jsonl";
    public const string TodoHeading = "✅ Yapılacaklarım";
    /// <summary>Gelir defterinde "hedef_usd:" yoksa kullanılan aylık gelir hedefi (USD).</summary>
    public const int DefaultIncomeTargetUsd = 100;

    public string Full(string rel) => Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar));

    public string Rel(string full)
    {
        string r = Path.GetRelativePath(Root, full).Replace('\\', '/');
        return r;
    }

    // ---------- 1. Yapılacaklarım ----------
    public TodoList ReadTodos()
    {
        string? text = Md.ReadText(Full(SimdiRel));
        if (text is null) { Warnings.Add("01 Şimdi.md bulunamadı"); return new TodoList(); }
        return ParseTodos(text);
    }

    public static TodoList ParseTodos(string text)
    {
        var lines = Md.Lines(text);
        int h = Md.SectionIndex(lines, TodoHeading);
        if (h < 0) return new TodoList { Found = false };
        var items = new List<TodoItem>();
        for (int i = h + 1; i < lines.Length; i++)
        {
            if (Md.IsHeadingAtOrAbove(lines[i], 2)) break;
            var cb = Md.Checkbox(lines[i]);
            if (cb is null) continue;
            string plain = Md.Plain(cb.Value.text);
            if (plain.Length == 0) continue;
            items.Add(new TodoItem(i, lines[i], cb.Value.done, plain));
        }
        return new TodoList { Found = true, Items = items };
    }

    // ---------- 2. Project cards ----------
    public List<ProjectCard> ReadProjects()
    {
        var list = new List<ProjectCard>();
        string dir = Full("20 Projeler");
        if (!Directory.Exists(dir)) { Warnings.Add("20 Projeler klasörü yok"); return list; }
        foreach (var sub in SafeDirs(dir))
        {
            string name = Path.GetFileName(sub);
            string card = Path.Combine(sub, name + ".md");
            if (!File.Exists(card)) continue;
            string? text = Md.ReadText(card);
            if (text is null) continue;
            try { list.Add(ParseProject(name, Rel(card), text)); }
            catch (Exception ex) { Warnings.Add($"Proje kartı okunamadı: {name} ({ex.Message})"); }
        }
        return list;
    }

    public static ProjectCard ParseProject(string name, string relPath, string text)
    {
        var lines = Md.Lines(text);
        var fm = Frontmatter.Parse(lines);
        var body = lines[Frontmatter.BodyStart(lines)..];

        var bitti = new List<(bool, string)>();
        foreach (var l in Md.Section(body, "Bitti Tanımı") ?? [])
        {
            var cb = Md.Checkbox(l);
            if (cb is not null && !char.IsWhiteSpace(l.Length > 0 ? l[0] : 'x')) bitti.Add((cb.Value.done, Md.Plain(cb.Value.text)));
        }

        var beklenen = ParseExpectations(body);

        string engel = "";
        var engelLines = Md.Section(body, "Engel");
        if (engelLines is not null)
        {
            var para = new List<string>();
            foreach (var l in engelLines)
            {
                if (string.IsNullOrWhiteSpace(l)) { if (para.Count > 0) break; continue; }
                if (l.TrimStart().StartsWith("%%", StringComparison.Ordinal)) continue;
                para.Add(l.Trim().TrimStart('>', ' '));
            }
            engel = Md.Plain(string.Join(" ", para));
        }

        string durum = Md.Normalize(fm.Get("durum"));
        return new ProjectCard
        {
            Name = name,
            RelPath = relPath,
            Durum = durum,
            Odak = fm.GetBool("odak"),
            BittiTanimi = Md.Plain(fm.Get("bitti_tanimi")),
            SonrakiAdim = fm.Get("sonraki_adim"),
            OlumKriteri = Md.Plain(fm.Get("olum_kriteri")),
            KararBekliyor = fm.GetBool("karar_bekliyor"),
            Kilit = fm.Get("kilit").Trim(),
            Klasor = fm.Get("klasor").Trim(),
            Baslangic = fm.GetDate("baslangic"),
            SonGuncelleme = fm.GetDate("son_guncelleme"),
            BittiItems = bitti,
            Beklenenler = beklenen,
            Engel = engel,
            KillDate = Md.FirstIsoDate(fm.Get("olum_kriteri")),
        };
    }

    /// <summary>
    /// "Kullanıcıdan beklenenler" bölüm başlığı mı? Kabul edilen biçimler (büyük/küçük harf, Türkçe harf ve emoji
    /// farkı gözetmeden): "Benden Beklenenler" (demo kasanın biçimi), "Senden Beklenenler", eski kasalardaki
    /// "&lt;ad&gt;'tan Beklenenler" ve genel olarak "&lt;herhangi bir ad&gt;'dan/'den/'tan/'ten Beklenenler"
    /// (kesme işareti isteğe bağlı: "Kullanıcıdan Beklenenler" da olur). 01 Şimdi.md'deki
    /// "⏳ Senden beklenen kararlar" gibi başlıklar da aynı kuralla tanınır. "Beklenen sonuçlar" gibi kimden
    /// beklendiğini söylemeyen başlıklar tanınmaz.
    /// </summary>
    public static bool IsExpectationHeading(string headingText)
    {
        string t = headingText.Replace('’', '\'').Replace('‘', '\'').Replace('ʼ', '\'').Replace('`', '\'');
        return ExpectationHeading().IsMatch(Md.Normalize(t));
    }

    /// <summary>
    /// Belgedeki bütün "kullanıcıdan beklenenler" bölümlerinin (<see cref="IsExpectationHeading"/>, "## " düzeyi)
    /// açık maddeleri: "- x" / "* x" / "1. x" / "1) x" satırları; işaretlenmiş kutucuklar ("- [x]") atlanır, girintili
    /// alt maddeler sayılmaz. Proje kartı ve 01 Şimdi.md için aynı kural.
    /// </summary>
    public static List<string> ParseExpectations(IReadOnlyList<string> lines)
    {
        var items = new List<string>();
        foreach (var section in Md.Sections(lines, IsExpectationHeading))
        {
            foreach (var l in section)
            {
                var t = l.TrimEnd();
                string? raw = null;
                if (t.StartsWith("- ", StringComparison.Ordinal) || t.StartsWith("* ", StringComparison.Ordinal))
                {
                    var cb = Md.Checkbox(t);
                    if (cb is { done: true }) continue;
                    raw = cb is null ? t[2..] : cb.Value.text;
                }
                else if (NumberedItem().Match(t) is { Success: true } m) raw = m.Groups[1].Value;
                if (raw is null) continue;
                string p = Md.Plain(raw);
                if (p.Length > 0) items.Add(p);
            }
        }
        return items;
    }

    // Normalize edilmiş başlık: "benden beklenenler", "kaan'dan beklenenler", "senden beklenen kararlar" …
    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N} '-]{0,60}?(?:dan|den|tan|ten) beklen(?:enler|en|ilenler|ilen)(?: |$)")]
    private static partial Regex ExpectationHeading();
    [GeneratedRegex(@"^\d{1,3}[.)]\s+(.+)$")] private static partial Regex NumberedItem();

    // ---------- 3. Tasks ----------
    public List<TaskItem> ReadTasks()
    {
        var list = new List<TaskItem>();
        string dir = Full("20 Projeler");
        if (!Directory.Exists(dir)) return list;
        foreach (var sub in SafeDirs(dir))
        {
            string tasksDir = Path.Combine(sub, "Görevler");
            if (!Directory.Exists(tasksDir)) continue;
            foreach (var f in SafeFiles(tasksDir, "*.md"))
            {
                string? text = Md.ReadText(f);
                if (text is null) continue;
                var lines = Md.Lines(text);
                var fm = Frontmatter.Parse(lines);
                string title = lines.Skip(Frontmatter.BodyStart(lines)).FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal)) is { } h
                    ? Md.Plain(h[2..]) : Path.GetFileNameWithoutExtension(f);
                DateTime mod = SafeMtime(f);
                list.Add(new TaskItem(Path.GetFileName(sub), fm.Get("kimlik", Path.GetFileNameWithoutExtension(f)), title,
                    Md.Normalize(fm.Get("durum")), fm.Get("atanan"), fm.GetDate("son_tarih"), Rel(f), mod));
            }
        }
        return list;
    }

    // ---------- 4. Roles ----------
    public List<RoleCard> ReadRoles()
    {
        var list = new List<RoleCard>();
        string dir = Full("60 Ekip/Roller");
        if (!Directory.Exists(dir)) { Warnings.Add("60 Ekip/Roller klasörü yok"); return list; }
        foreach (var f in SafeFiles(dir, "*.md"))
        {
            string? text = Md.ReadText(f);
            if (text is null) continue;
            var lines = Md.Lines(text);
            var fm = Frontmatter.Parse(lines);
            string title = lines.Skip(Frontmatter.BodyStart(lines)).FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal)) is { } h
                ? Md.Plain(h[2..]) : Path.GetFileNameWithoutExtension(f);
            string shortName = title.Contains(':') ? title[..title.IndexOf(':')].Trim() : title;
            list.Add(new RoleCard(title, shortName, fm.Get("cagri"), Md.Plain(fm.Get("departman", "Diğer")),
                Md.Plain(fm.Get("yonetici")), Md.Plain(fm.Get("arac")), fm.Get("model"), Md.Plain(fm.Get("ozet")), Rel(f)));
        }
        return list;
    }

    // ---------- 5. Request ledger ----------
    public List<LedgerEntry> ReadLedger()
    {
        var list = new List<LedgerEntry>();
        string? text = Md.ReadText(Full(LedgerRel));
        if (text is null) { Warnings.Add("İstek defteri (istekler.jsonl) bulunamadı"); return list; }
        int bad = 0;
        foreach (var line in Md.Lines(text))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var r = doc.RootElement;
                if (r.ValueKind != JsonValueKind.Object) { bad++; continue; }
                string ts = Str(r, "t");
                if (!DateTimeOffset.TryParse(ts, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t)) { bad++; continue; }
                int n = list.Count + 1;
                if (r.TryGetProperty("n", out var nEl) &&
                    (nEl.ValueKind != JsonValueKind.Number || !nEl.TryGetInt32(out n))) { bad++; continue; }
                string metin = Str(r, "metin");
                if (metin.TrimStart().StartsWith("<task-notification", StringComparison.Ordinal)) continue; // tool chatter, not a request
                bool auto = false;
                var sched = ScheduledTaskName().Match(metin);
                if (sched.Success) { metin = "Zamanlanmış görev çalıştı: " + sched.Groups[1].Value; auto = true; }
                list.Add(new LedgerEntry(n, t, Str(r, "arac").ToLowerInvariant(), Str(r, "proje"), Str(r, "oturum"), metin, auto));
            }
            catch (JsonException) { bad++; }
        }
        if (bad > 0) Warnings.Add($"İstek defterinde okunamayan {bad} satır atlandı");
        return list;
    }

    static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString() : "";

    // ---------- 6. LinkedIn ----------
    public LinkedInInfo ReadLinkedIn()
    {
        string? path = FindLinkedInFile();
        string? text = path is null ? null : Md.ReadText(path);
        if (text is null) return new LinkedInInfo();
        return ParseLinkedIn(text);
    }

    /// <summary>Önce 40 Alanlar/LinkedIn Takvimi.md; yoksa 20 Projeler altındaki ilk (ada göre sıralı) proje
    /// klasöründe aynı adlı dosya. Hiçbiri yoksa null (LinkedIn bölümü "dosya yok" gösterir).</summary>
    public string? FindLinkedInFile()
    {
        string primary = Full(LinkedInRel);
        if (File.Exists(primary)) return primary;
        string projects = Full("20 Projeler");
        if (!Directory.Exists(projects)) return null;
        foreach (var sub in SafeDirs(projects))
        {
            string candidate = Path.Combine(sub, LinkedInFileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public static LinkedInInfo ParseLinkedIn(string text)
    {
        var body = Md.BodyLines(Md.StripComments(text));
        var published = new List<LinkedInPost>();
        var pub = Md.Section(body, "Yayınlananlar");
        if (pub is not null)
        {
            foreach (var t in MarkdownTable.ParseAll(pub))
            {
                int cNo = t.Col("No"), cTarih = t.Col("Tarih"), cKonu = t.Col("Konu");
                foreach (var r in t.Rows)
                    if (r.Cells.Any(c => c.Length > 0))
                        published.Add(new LinkedInPost(r.Cell(cNo), r.Cell(cTarih), Md.Plain(r.Cell(cKonu))));
            }
        }
        var drafts = new List<string>();
        var sira = Md.Section(body, "Sıradakiler");
        if (sira is not null)
        {
            bool inFence = false;
            foreach (var l in sira)
            {
                if (l.TrimStart().StartsWith("```", StringComparison.Ordinal)) inFence = !inFence;
                if (!inFence && l.StartsWith("### ", StringComparison.Ordinal)) drafts.Add(Md.Plain(l[4..]));
            }
        }
        return new LinkedInInfo { Found = true, Published = published, Drafts = drafts };
    }

    // ---------- 7. Jobs ----------
    public JobsInfo ReadJobs()
    {
        string? text = Md.ReadText(Full(JobsRel));
        if (text is null) return new JobsInfo();
        return ParseJobs(text);
    }

    public static JobsInfo ParseJobs(string text)
    {
        var lines = Md.Lines(text);
        var sections = new List<JobSection>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].StartsWith("## ", StringComparison.Ordinal)) continue;
            string name = Md.Plain(lines[i][3..]);
            int j = i + 1;
            while (j < lines.Length && !Md.IsHeadingAtOrAbove(lines[j], 2)) j++;
            var secLines = lines[(i + 1)..j];
            var rows = new List<JobRow>();
            var columns = new List<string>();
            foreach (var t in MarkdownTable.ParseAll(secLines, i + 1))
            {
                int cFirma = t.Col("Firma"), cPoz = t.Col("Pozisyon");
                if (cFirma < 0 && cPoz < 0) continue; // not a job table
                if (columns.Count == 0) columns.AddRange(t.Headers.Select(Md.Plain));
                int cNo = t.Col("No", "#"), cTur = t.Col("Tür", "Tur"), cKanal = t.Col("Kanal"), cSehir = t.Col("Şehir", "Sehir", "Konum");
                int cUyg = t.Col("Uygunluk"), cEksik = t.Col("Eksik beceri", "Eksik beceriler"), cLink = t.Col("İlan linki", "Ilan linki", "Link", "İlan");
                int cBelge = t.Col("Belgeler", "Belge"), cDurum = t.Col("Durum"), cNot = t.Col("Not", "Notlar");
                foreach (var r in t.Rows)
                {
                    if (r.Cells.All(c => c.Length == 0)) continue;
                    rows.Add(new JobRow
                    {
                        Section = name, LineIndex = r.LineIndex, RawLine = r.RawLine, DurumCol = cDurum,
                        No = Md.Plain(r.Cell(cNo)), Firma = Md.Plain(r.Cell(cFirma)), Pozisyon = Md.Plain(r.Cell(cPoz)),
                        Tur = Md.Plain(r.Cell(cTur)), Kanal = Md.Plain(r.Cell(cKanal)), Sehir = Md.Plain(r.Cell(cSehir)),
                        Uygunluk = Md.Plain(r.Cell(cUyg)), EksikBeceri = Md.Plain(r.Cell(cEksik)), IlanLinkiRaw = r.Cell(cLink),
                        Belgeler = Md.Plain(r.Cell(cBelge)), Durum = Md.Plain(r.Cell(cDurum)), Not = Md.Plain(r.Cell(cNot)),
                    });
                }
            }
            if (columns.Count > 0) sections.Add(new JobSection { Name = name, Rows = rows, Columns = columns });
            i = j - 1;
        }
        return new JobsInfo { Found = true, Sections = sections };
    }

    // ---------- 8. Git ----------
    public GitInfo ReadGit()
    {
        // Kasa ışığı (SystemMonitor) ve betik koruması (VaultGitGuard) gibi git'e sor: kasa kendi deposu olabileceği gibi
        // bir üst deponun alt klasörü (örneğin klonlanan depodaki ornek-kasa) ya da .git'i dosya olan bir worktree olabilir.
        var inside = Directory.Exists(Root)
            ? ProcessRunner.Run("git", ["-C", Root, "rev-parse", "--is-inside-work-tree"], null, 8000)
            : new ProcessResult(1, "", false);
        if (inside.ExitCode != 0 || !inside.Output.Contains("true", StringComparison.Ordinal))
        {
            bool gitMissing = inside.ExitCode == -1 && Path.Exists(Path.Combine(Root, ".git"));
            Warnings.Add(gitMissing ? "git çalışmadı: " + Md.Clip(inside.Output.Trim(), 120)
                : "Kasa bir git deposu değil (isteğe bağlı; kasada \"git init\" ile kayıt geçmişi ve betik koruması açılır)");
            return new GitInfo();
        }
        var recent = new List<Commit>();
        // "-- ." : yalnızca kasa klasörünü etkileyen kayıtlar (kasa bir üst deponun içindeyse depo geneli sayılmaz)
        var r1 = ProcessRunner.Run("git", ["-C", Root, "log", "-n", "200", "--since=30 days ago", "--date=iso", "--format=%ad|%s", "--", "."], null, 15000);
        if (r1.ExitCode != 0) { Warnings.Add("git log çalışmadı: " + Md.Clip(r1.Output.Trim(), 120)); return new GitInfo(); }
        foreach (var line in Md.Lines(r1.Output))
        {
            int bar = line.IndexOf('|');
            if (bar <= 0) continue;
            if (TryParseGitIso(line[..bar], out var when)) recent.Add(new Commit(when, line[(bar + 1)..].Trim()));
        }
        var perDay = new Dictionary<DateOnly, int>();
        var r2 = ProcessRunner.Run("git", ["-C", Root, "log", "--since=14 days ago", "--date=short", "--format=%ad", "--", "."], null, 15000);
        if (r2.ExitCode == 0)
            foreach (var line in Md.Lines(r2.Output))
                if (DateOnly.TryParseExact(line.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    perDay[d] = perDay.GetValueOrDefault(d) + 1;
        return new GitInfo { Available = true, Recent = recent, PerDay14 = perDay };
    }

    /// <summary>Parses git's "--date=iso" format: "2026-09-23 17:52:35 +0300".</summary>
    public static bool TryParseGitIso(string s, out DateTimeOffset when)
    {
        s = s.Trim();
        var m = GitIso().Match(s);
        if (m.Success)
            s = $"{m.Groups[1].Value}T{m.Groups[2].Value}{m.Groups[3].Value}:{m.Groups[4].Value}";
        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out when);
    }

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2}) (\d{2}:\d{2}:\d{2}) ([+-]\d{2})(\d{2})$")] private static partial Regex GitIso();
    [GeneratedRegex(@"^\s*<scheduled-task name=""([^""]+)""")] private static partial Regex ScheduledTaskName();

    // ---------- gelir defteri + derin analiz ----------
    public IncomeInfo ReadIncome()
    {
        string? text = Md.ReadText(Full(IncomeRel));
        if (text is null) return new IncomeInfo();
        return ParseIncome(text);
    }

    /// <summary>Gelir defteri tablolarını okur. Aylık hedef, dosyanın başındaki "hedef_usd: 150" satırından gelir;
    /// yoksa <see cref="DefaultIncomeTargetUsd"/>.</summary>
    public static IncomeInfo ParseIncome(string text)
    {
        var fm = Frontmatter.Parse(text);
        int target = int.TryParse(fm.Get("hedef_usd").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int hedef) && hedef > 0
            ? hedef : DefaultIncomeTargetUsd;
        var entries = new List<IncomeEntry>();
        foreach (var t in MarkdownTable.ParseAll(Md.BodyLines(Md.StripComments(text))))
        {
            int cDate = t.Col("Tarih"), cAmt = t.Col("Tutar TL", "Tutar (TL)", "Tutar"), cNote = t.Col("Açıklama", "Aciklama");
            if (cDate < 0 || cAmt < 0) continue;
            foreach (var r in t.Rows)
            {
                var d = Md.FirstIsoDate(r.Cell(cDate));
                var amt = ParseTl(r.Cell(cAmt));
                if (d is null || amt is null) continue;
                entries.Add(new IncomeEntry(d.Value, amt.Value, Md.Plain(r.Cell(cNote))));
            }
        }
        return new IncomeInfo { Found = true, Entries = entries, TargetUsd = target };
    }

    /// <summary>Parses "5.000", "5.000,50", "5000 TL", "₺1.250" into decimal TL.</summary>
    public static decimal? ParseTl(string s)
    {
        string t = new string(s.Where(c => char.IsDigit(c) || c is '.' or ',' or '-').ToArray());
        if (t.Length == 0) return null;
        if (t.Contains(',')) t = t.Replace(".", "").Replace(',', '.');
        else if (t.Count(c => c == '.') >= 1 && t.Length - t.LastIndexOf('.') == 4) t = t.Replace(".", ""); // thousands
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    public AnalysisNote ReadAnalysis()
    {
        string dir = Full("70 Günlük/Analiz");
        if (!Directory.Exists(dir)) return new AnalysisNote();
        var files = SafeFiles(dir, "*.md")
            .Select(f => (f, date: Md.FirstIsoDate(Path.GetFileName(f)), mtime: SafeMtime(f)))
            .OrderByDescending(x => x.date?.ToDateTime(TimeOnly.MinValue) ?? x.mtime)
            .ThenByDescending(x => x.mtime)
            .ToList();
        if (files.Count == 0) return new AnalysisNote();
        var (path, date, mtime) = files[0];
        string? text = Md.ReadText(path);
        if (text is null) return new AnalysisNote();
        var fm = Frontmatter.Parse(text);
        var outLines = new List<string>();
        bool inFence = false;
        foreach (var raw in Md.BodyLines(Md.StripComments(text)))
        {
            if (raw.TrimStart().StartsWith("```", StringComparison.Ordinal)) { inFence = !inFence; continue; }
            string l = raw.Trim();
            if (l.StartsWith("> [!", StringComparison.Ordinal)) continue;
            l = l.TrimStart('#', '>', ' ');
            if (l.StartsWith("- [ ] ", StringComparison.Ordinal) || l.StartsWith("- [x] ", StringComparison.Ordinal)) l = "• " + l[6..];
            else if (l.StartsWith("- ", StringComparison.Ordinal) || l.StartsWith("* ", StringComparison.Ordinal)) l = "• " + l[2..];
            l = Md.Plain(l).Replace("*", "");
            if (l.Length == 0 && (outLines.Count == 0 || outLines[^1].Length == 0)) continue;
            if (l == "---") continue;
            outLines.Add(l);
            if (outLines.Count >= 25) break;
        }
        while (outLines.Count > 0 && outLines[^1].Length == 0) outLines.RemoveAt(outLines.Count - 1);
        return new AnalysisNote
        {
            Found = true, RelPath = Rel(path), Lines = outLines,
            Date = fm.GetDate("tarih") ?? date ?? DateOnly.FromDateTime(mtime),
        };
    }

    // ---------- helpers ----------
    static IEnumerable<string> SafeDirs(string dir)
    {
        try { return Directory.GetDirectories(dir).OrderBy(d => d, StringComparer.Create(Md.Tr, true)); }
        catch (Exception) { return []; }
    }

    public static IEnumerable<string> SafeFiles(string dir, string pattern)
    {
        try { return Directory.GetFiles(dir, pattern).OrderBy(d => d, StringComparer.Create(Md.Tr, true)); }
        catch (Exception) { return []; }
    }

    static DateTime SafeMtime(string f)
    {
        try { return File.GetLastWriteTime(f); } catch { return DateTime.MinValue; }
    }
}
