// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Globalization;
using Ordinaryunus.Bridge;
using Ordinaryunus.Data;

namespace Ordinaryunus.Brain;

/// <summary>Builds the Beyin "Genel bakış" payload (MIMARI §5.9). A functional subset of the full spec: every
/// field is populated from real data, but some of the rarer sources (e.g. "Bitenler.md" free-text list items)
/// are not mined — see the backend report for the exact list of deviations.</summary>
public static class OverviewBuilder
{
    public static BrainOverview Build(VaultSnapshot snap, BrainIndex index, GitActivity git, List<ProblemDto> problems,
        IReadOnlyDictionary<string, List<KayitEntry>> kayitByProject, List<LightDto> lights, UsageDto? usage,
        IReadOnlyList<JobDto> jobs, decimal usdRate, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var (level, score) = ProblemDetector.ComputeHealth(problems);
        string healthNote = ProblemDetector.HealthHint(problems);
        var counts = new CountsDto(problems.Count(p => p.Severity == "kritik"), problems.Count(p => p.Severity == "dikkat"), problems.Count(p => p.Severity == "bilgi"));
        var allEntries = kayitByProject.Values.SelectMany(v => v).ToList();

        var focus = snap.Focus;
        var focusDto = new FocusSummaryDto(focus?.Name, focus is null || focus.TotalCount == 0 ? null : (int)Math.Round(focus.Progress * 100),
            focus?.DoneCount ?? 0, focus?.TotalCount ?? 0);

        var (cl7, cx7) = Insights.RequestsSince(snap, today.AddDays(-6));
        var (cl13, cx13) = Insights.RequestsSince(snap, today.AddDays(-13));
        int commits7 = Insights.CommitsSince(snap, today.AddDays(-6));
        int commits13 = Insights.CommitsSince(snap, today.AddDays(-13));
        var week = new WeekDto(commits7, cl7 + cx7, commits13 - commits7, (cl13 + cx13) - (cl7 + cx7));

        var headline = BuildHeadline(snap, problems, week, today, counts);
        var kpis = BuildKpis(snap, problems, week, usage, today, usdRate);
        var projectHealth = BuildProjectHealth(snap, problems, kayitByProject, today);
        var done = BuildDone(snap, allEntries, jobs, now);
        var notDone = BuildNotDone(snap, allEntries, today);
        var recent = BuildRecent(snap, allEntries, jobs, now);
        var top = problems.Take(8).ToList();
        var charts = BuildCharts(snap, git, allEntries, today);

        var deep = new DeepAnalysisDto(snap.Analysis.Found, snap.Analysis.Date?.ToString("yyyy-MM-dd"),
            snap.Analysis.Found ? snap.Analysis.RelPath : null, snap.Analysis.Lines.Take(25).ToList());
        decimal monthTl = snap.Income.MonthTl(today);
        double monthUsd = usdRate > 0 ? (double)(monthTl / usdRate) : 0;
        int targetUsd = Math.Max(1, snap.Income.TargetUsd);
        var income = new IncomeDto(snap.Income.Found, (double)monthTl, monthUsd, targetUsd, Math.Clamp(monthUsd / targetUsd, 0, 1), (double)usdRate);

        var stats = new BrainStatsDto(index.Notes.Count, index.Files.Count,
            index.Files.Keys.Select(k => System.IO.Path.GetDirectoryName(k) ?? "").Distinct().Count(),
            index.Resolved.Values.Sum(l => l.Count), index.Broken.Values.Sum(l => l.Count),
            index.Notes.Values.Sum(n => n.Words), index.IndexedAt.ToString("o", CultureInfo.InvariantCulture), index.IndexMs);

        return new BrainOverview(now.ToString("o", CultureInfo.InvariantCulture), headline,
            new HealthSummaryDto(score, level, counts, healthNote), focusDto, week, kpis, projectHealth, done, notDone, recent, top,
            charts, Insights.Sentences(snap), deep, income, usage, stats);
    }

    static List<string> BuildHeadline(VaultSnapshot snap, List<ProblemDto> problems, WeekDto week, DateOnly today, CountsDto counts)
    {
        var list = new List<string>();
        string todayText = today.ToString("d MMMM yyyy, dddd", Md.Tr);
        var focus = snap.Focus;
        string line1 = $"{todayText}. {snap.ActiveCount} aktif proje var{(snap.ActiveCount >= 3 ? ", sınır dolu" : "")}; ";
        line1 += focus is null ? "odak projesi seçilmemiş." : $"odak: {focus.Name} (%{(focus.TotalCount == 0 ? 0 : (int)Math.Round(focus.Progress * 100))} bitti).";
        list.Add(line1);

        string trend = week.Requests > week.PrevRequests ? "geçen haftadan fazla" : week.Requests < week.PrevRequests ? "geçen haftadan az" : "geçen haftayla aynı";
        list.Add($"Son 7 günde {week.Commits} kasa kaydı ve {week.Requests} istek oldu ({trend}).");

        list.Add(counts.Kritik + counts.Dikkat == 0
            ? "Ciddi bir sorun yok."
            : $"{counts.Kritik} ciddi sorun, {counts.Dikkat} uyarı var; en önemlisi: {problems.FirstOrDefault()?.Title ?? ""}.");

        int approvals = snap.ApprovalCount;
        if (approvals > 0) list.Add($"{approvals} şey onayını bekliyor; Masam'ın en üstünde.");
        return list;
    }

    static List<KpiDto> BuildKpis(VaultSnapshot snap, List<ProblemDto> problems, WeekDto week, UsageDto? usage, DateOnly today, decimal usdRate)
    {
        var list = new List<KpiDto>();
        list.Add(new KpiDto("aktifProje", "Aktif proje", snap.ActiveCount, $"{snap.ActiveCount}/3", "sınır 3", snap.ActiveCount >= 3 ? "warn" : "normal",
            "Aynı anda en fazla 3 proje aktif olabilir; fazlası dağılmak demek.", null, null, snap.ActiveCount / 3.0, new PageTarget("projeler", null)));
        int todoTotal = snap.Todos.Items.Count, todoDone = snap.Todos.DoneCount;
        list.Add(new KpiDto("bugunYapilan", "Bugün yapılan", todoDone, $"{todoDone}/{todoTotal}", "Masam listesi", "normal",
            "Bugünkü Yapılacaklarım listende işaretlediklerin.", null, null, todoTotal == 0 ? null : (double)todoDone / todoTotal, new PageTarget("masam", null)));
        var commitSpark = Insights.CommitsPerDay(snap, 14).Select(x => (double)x.commits).ToList();
        list.Add(new KpiDto("haftaCommit", "Hafta kaydı", week.Commits, week.Commits.ToString(), "son 7 gün", "normal",
            "Kasada bu hafta kaydedilen (commit) değişiklik sayısı.",
            new KpiTrendDto(week.Commits - week.PrevCommits, week.Commits >= week.PrevCommits ? "arttı" : "azaldı"), commitSpark, null,
            new PageTarget("gecmis", null)));
        var reqSpark = Insights.RequestsPerDay(snap, 14).Select(x => (double)(x.claude + x.codex)).ToList();
        var (cl7, cx7) = Insights.RequestsSince(snap, today.AddDays(-6));
        list.Add(new KpiDto("haftaIstek", "Hafta isteği", cl7 + cx7, (cl7 + cx7).ToString(), $"Claude {cl7} · Codex {cx7}", "normal",
            "Bu hafta Claude'a ya da Codex'e yazdığın mesaj sayısı.", null, reqSpark, null, new PageTarget("gecmis", null)));
        int kritik = problems.Count(p => p.Severity == "kritik"), dikkat = problems.Count(p => p.Severity == "dikkat");
        list.Add(new KpiDto("acikSorun", "Açık sorun", kritik + dikkat, (kritik + dikkat).ToString(), $"{kritik} kritik", kritik > 0 ? "warn" : "normal",
            "Beyin'in otomatik bulduğu kritik ve dikkat gerektiren konular.", null, null, null, new PageTarget("beyin", "sorunlar")));
        list.Add(new KpiDto("onayBekleyen", "Onay bekleyen", snap.ApprovalCount, snap.ApprovalCount.ToString(), "Masam'da", snap.ApprovalCount > 0 ? "warn" : "normal",
            "Senin onayını bekleyen güvenlik istekleri, iş başvuruları ve kararlar.", null, null, null, new PageTarget("masam", null)));
        int streak = snap.Streak();
        list.Add(new KpiDto("seri", "Seri", streak, $"{streak} gün", "art arda", "normal",
            "Art arda en az bir kasa kaydı ya da işaretleme yaptığın gün sayısı.", null, null, null, new PageTarget("masam", null)));
        list.Add(new KpiDto("linkedin", "LinkedIn", snap.LinkedIn.Published.Count, snap.LinkedIn.Published.Count.ToString(),
            $"{snap.LinkedIn.Drafts.Count} taslak", "normal", "Yayınlanan LinkedIn gönderisi sayısı.", null, null, null, null));
        double incomeUsd = snap.Income.Found && usdRate > 0 ? (double)(snap.Income.MonthTl(today) / usdRate) : 0;
        int targetUsd = Math.Max(1, snap.Income.TargetUsd);
        list.Add(new KpiDto("gelir", "Gelir", incomeUsd, incomeUsd.ToString("0", Md.Tr) + " USD", $"hedef {targetUsd} USD", "normal",
            $"Bu ay tahsil edilen tutarın {targetUsd} USD hedefine oranı (hedef: Gelir Defteri'ndeki \"hedef_usd:\").", null, null,
            Math.Clamp(incomeUsd / targetUsd, 0, 1), null));
        list.Add(new KpiDto("kullanim", "Bugünkü kullanım", null,
            usage is null ? "hesaplanamadı" : $"C {SystemMonitor.FormatTokens(usage.ClaudeTokens)} · X {SystemMonitor.FormatTokens(usage.CodexTokens)}",
            "abonelikte ek ücret yok", "normal", "Bugün Claude ve Codex'te kullanılan tahmini jeton (token) sayısı.", null, null, null, null));
        return list;
    }

    static List<ProjectHealthDto> BuildProjectHealth(VaultSnapshot snap, List<ProblemDto> problems,
        IReadOnlyDictionary<string, List<KayitEntry>> kayitByProject, DateOnly today)
    {
        return snap.Projects.Where(p => p.Durum is "aktif" or "beklemede")
            .OrderByDescending(p => p.Durum == "aktif").ThenByDescending(p => p.Odak).ThenBy(p => p.Name, StringComparer.Create(Md.Tr, true))
            .Select(p => HealthFor(p, problems, kayitByProject, today)).ToList();
    }

    /// <summary>Public so Handlers.getProjectDetail can build the same health card for a single project.</summary>
    public static ProjectHealthDto HealthFor(ProjectCard p, List<ProblemDto> problems,
        IReadOnlyDictionary<string, List<KayitEntry>> kayitByProject, DateOnly today)
    {
        var own = problems.Where(x => x.Project == p.Name).ToList();
        var (level, score) = ProblemDetector.ComputeHealth(own);
        var reasons = own.Where(x => x.Severity != "bilgi").Take(4).Select(x => x.Title).ToList();
        LockInfo? lockInfo = null;
        if (!string.IsNullOrWhiteSpace(p.Kilit))
        {
            var (holder, since) = ProblemDetector.ParseLock(p.Kilit);
            double? hours = since is { } s ? (DateTime.Now - s).TotalHours : null;
            lockInfo = new LockInfo(holder, since?.ToString("o", CultureInfo.InvariantCulture), hours, hours is > 12);
        }
        var entries = kayitByProject.GetValueOrDefault(p.Name, []);
        var latest = KayitParser.LatestDevir(entries);
        KayitEntryDto? handoffDto = latest is null ? null : ToDto(latest);
        return new ProjectHealthDto(p.Name, p.Durum, p.Odak, level, score, reasons,
            new BittiInfo(p.DoneCount, p.TotalCount, p.TotalCount == 0 ? 0 : (int)Math.Round(p.Progress * 100),
                p.BittiItems.Select(b => new BittiItemDto(b.done, b.text)).ToList()),
            p.DaysToKill(today), p.SonGuncelleme is { } g ? today.DayNumber - g.DayNumber : null, lockInfo,
            p.NextStepShort, handoffDto, own.Select(x => x.Id).ToList());
    }

    public static KayitEntryDto ToDto(KayitEntry e) => new(e.Kind, e.Date.ToString("yyyy-MM-dd"), e.Time, e.Tool, e.Title,
        e.Fields.Select(f => new KeyValueDto(f.Key, f.Value)).ToList(), e.Path, e.Anchor, e.Project);

    static List<WorkItemDto> BuildDone(VaultSnapshot snap, List<KayitEntry> entries, IReadOnlyList<JobDto> jobs, DateTime now)
    {
        var list = new List<WorkItemDto>();
        var cutoff = now.AddDays(-14);
        foreach (var e in entries.Where(e => e.Kind == "devir" && e.Date.ToDateTime(TimeOnly.MinValue) >= cutoff).OrderByDescending(e => e.Date))
        {
            string? yapilan = e.Get("Yapılan");
            if (string.IsNullOrWhiteSpace(yapilan)) continue;
            list.Add(new WorkItemDto(Md.Clip(yapilan, 160), e.Project, "kayit", "Devir notu",
                e.Date.ToDateTime(TimeOnly.MinValue).ToString("o", CultureInfo.InvariantCulture), new NoteTarget(e.Path, e.Anchor)));
        }
        foreach (var t in snap.Tasks.Where(t => t.Durum == "tamam" && t.Modified >= cutoff).OrderByDescending(t => t.Modified))
            list.Add(new WorkItemDto($"{t.Kimlik}: {t.Title}", t.Project, "gorev", "Görev tamamlandı",
                t.Modified.ToString("o", CultureInfo.InvariantCulture), new NoteTarget(t.RelPath, null)));
        foreach (var j in AppJournal.Load().Where(e => e.Zaman >= cutoff).OrderByDescending(e => e.Zaman))
            list.Add(new WorkItemDto(j.Metin, null, "isaret", "İşaretlendi", j.Zaman.ToString("o", CultureInfo.InvariantCulture), null));
        foreach (var j in jobs.Where(j => j.State == "bitti" && DateTime.TryParse(j.End, out var end) && end >= cutoff))
            list.Add(new WorkItemDto(j.Title, j.Project, "is", $"{(j.Tool == "codex" ? "Codex" : "Claude")} işi", j.End, new JobTarget(j.Id)));
        return list.OrderByDescending(w => w.Time).Take(12).ToList();
    }

    /// <summary>Open items for exactly one project (bitti tanımı, latest "Açık kalan", beklenenler, tasks). Used by getProjectDetail.</summary>
    public static List<WorkItemDto> OpenItemsFor(ProjectCard p, VaultSnapshot snap, List<KayitEntry> projectEntries)
    {
        var list = new List<WorkItemDto>();
        foreach (var b in p.BittiItems.Where(b => !b.done))
            list.Add(new WorkItemDto(b.text, p.Name, "bitti", "Bitti tanımı", null, new ProjectTarget(p.Name, "genel")));
        var latest = KayitParser.LatestDevir(projectEntries);
        string? acik = latest?.Get("Açık kalan");
        if (!string.IsNullOrWhiteSpace(acik) && acik.Trim().TrimEnd('.').ToLowerInvariant() is not ("yok" or "-"))
            list.Add(new WorkItemDto(acik, p.Name, "acik", "Açık kalan", null, new NoteTarget(latest!.Path, latest.Anchor)));
        foreach (var b in p.Beklenenler)
            list.Add(new WorkItemDto(b, p.Name, "beklenen", "Senden beklenen", null, new ProjectTarget(p.Name, "genel")));
        foreach (var t in snap.Tasks.Where(t => t.Project == p.Name && t.Durum is "hazir" or "verildi" or "kontrol"))
            list.Add(new WorkItemDto($"{t.Kimlik}: {t.Title}", p.Name, "gorev", "Görev " + t.Durum, null, new NoteTarget(t.RelPath, null)));
        return list;
    }

    static List<WorkItemDto> BuildNotDone(VaultSnapshot snap, List<KayitEntry> entries, DateOnly today)
    {
        var list = new List<WorkItemDto>();
        foreach (var t in snap.Todos.Open)
            list.Add(new WorkItemDto(t.Text, null, "todo", "Yapılacaklarım", null, null));
        foreach (var p in snap.Active)
            foreach (var b in p.BittiItems.Where(b => !b.done))
                list.Add(new WorkItemDto(b.text, p.Name, "bitti", "Bitti tanımı", null, new ProjectTarget(p.Name, "genel")));
        foreach (var p in snap.Active)
        {
            var latest = KayitParser.LatestDevir(entries.Where(e => e.Project == p.Name));
            string? acik = latest?.Get("Açık kalan");
            if (!string.IsNullOrWhiteSpace(acik) && acik.Trim().TrimEnd('.').ToLowerInvariant() is not ("yok" or "-"))
                list.Add(new WorkItemDto(acik, p.Name, "acik", "Açık kalan", null, new NoteTarget(latest!.Path, latest.Anchor)));
        }
        foreach (var p in snap.Active)
            foreach (var b in p.Beklenenler)
                list.Add(new WorkItemDto(b, p.Name, "beklenen", "Senden beklenen", null, new ProjectTarget(p.Name, "genel")));
        foreach (var t in snap.Tasks.Where(t => t.Durum is "hazir" or "verildi" or "kontrol"))
            list.Add(new WorkItemDto($"{t.Kimlik}: {t.Title}", t.Project, "gorev", "Görev " + t.Durum, null, new NoteTarget(t.RelPath, null)));
        return list.Take(15).ToList();
    }

    static List<RecentItemDto> BuildRecent(VaultSnapshot snap, List<KayitEntry> entries, IReadOnlyList<JobDto> jobs, DateTime now)
    {
        var list = new List<RecentItemDto>();
        var cutoff = now.AddHours(-72);
        foreach (var e in entries.Where(e => e.Date.ToDateTime(TimeOnly.MinValue) >= cutoff))
            list.Add(new RecentItemDto(e.Date.ToDateTime(TimeOnly.MinValue).ToString("o", CultureInfo.InvariantCulture), e.Kind, e.Tool,
                e.Title, e.Get("Yapılan") ?? e.Get("Karar"), e.Project, new NoteTarget(e.Path, e.Anchor)));
        foreach (var c in snap.Git.Recent.Where(c => c.When.LocalDateTime >= cutoff))
            list.Add(new RecentItemDto(c.When.LocalDateTime.ToString("o", CultureInfo.InvariantCulture), "commit", c.Tool, c.Text, null, null, null));
        foreach (var l in snap.Ledger.Where(l => !l.Auto && l.Local >= cutoff))
            list.Add(new RecentItemDto(l.Local.ToString("o", CultureInfo.InvariantCulture), "istek", l.Arac == "codex" ? "codex" : "claude",
                Md.Clip(l.Metin, 100), null, l.Proje.Length > 0 ? l.Proje : null, null));
        foreach (var j in jobs.Where(j => DateTime.TryParse(j.Start, out var st) && st >= cutoff))
            list.Add(new RecentItemDto(j.Start, "is", j.Tool, j.Title, null, j.Project, new JobTarget(j.Id)));
        foreach (var s in snap.SafetyPending.Where(s => s.Zaman is { } z && z.LocalDateTime >= cutoff))
            list.Add(new RecentItemDto(s.Zaman!.Value.LocalDateTime.ToString("o", CultureInfo.InvariantCulture), "onay", s.Arac == "codex" ? "codex" : "claude",
                s.Islem, s.Hedef, null, new PageTarget("masam", null)));
        return list.OrderByDescending(r => r.Time).Take(30).ToList();
    }

    static ChartsDto BuildCharts(VaultSnapshot snap, GitActivity git, List<KayitEntry> entries, DateOnly today)
    {
        var req14 = Insights.RequestsPerDay(snap, 14);
        var requests14 = new Requests14Dto(req14.Select(x => x.day.ToString("yyyy-MM-dd")).ToList(), req14.Select(x => x.day.ToString("d MMM", Md.Tr)).ToList(),
            req14.Select(x => x.claude).ToList(), req14.Select(x => x.codex).ToList());
        var com14 = Insights.CommitsPerDay(snap, 14);
        var commits14 = new Commits14Dto(com14.Select(x => x.day.ToString("yyyy-MM-dd")).ToList(), com14.Select(x => x.day.ToString("d MMM", Md.Tr)).ToList(),
            com14.Select(x => x.commits).ToList());
        var days30 = Enumerable.Range(0, 30).Select(i => today.AddDays(-(29 - i))).ToList();
        var activity30 = new Activity30Dto(days30.Select(d => d.ToString("yyyy-MM-dd")).ToList(),
            days30.Select(d => git.CommitsOn(d)).ToList(),
            days30.Select(d => snap.Ledger.Count(l => !l.Auto && DateOnly.FromDateTime(l.Local) == d)).ToList(),
            days30.Select(d => entries.Count(e => e.Date == d)).ToList());
        var statusOrder = new[] { ("aktif", "Aktif"), ("beklemede", "Beklemede"), ("bitti", "Bitti"), ("donduruldu", "Donduruldu") };
        var projectStatus = statusOrder.Select(s => new StatusCountDto(s.Item1, s.Item2, snap.Projects.Count(p => p.Durum == s.Item1))).ToList();
        var taskOrder = new[] { ("hazir", "Hazır"), ("verildi", "Verildi"), ("kontrol", "Kontrol"), ("tamam", "Tamam"), ("iptal", "İptal") };
        var taskFunnel = taskOrder.Select(s => new StatusCountDto(s.Item1, s.Item2, snap.Tasks.Count(t => t.Durum == s.Item1))).ToList();
        var progress = snap.Active.Select(p => new ProgressRowDto(p.Name, p.DoneCount, p.TotalCount, p.TotalCount == 0 ? 0 : (int)Math.Round(p.Progress * 100))).ToList();
        var kill = snap.Active.Where(p => p.KillDate is not null).Select(p => new KillRowDto(p.Name, p.KillDate!.Value.ToString("yyyy-MM-dd"),
            p.DaysToKill(today) ?? 0, Insights.KillCondition(p.OlumKriteri))).ToList();
        return new ChartsDto(requests14, commits14, activity30, projectStatus, taskFunnel, progress, kill);
    }
}
