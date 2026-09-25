// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Ordinaryunus.Brain;
using Ordinaryunus.Data;
using Ordinaryunus.Host;
using Ordinaryunus.Jobs;

namespace Ordinaryunus.Bridge;

/// <summary>Domain objects (VaultSnapshot, BrainIndex, ...) → wire DTOs (MIMARI §3.6 Mapper).</summary>
public static class Mapper
{
    /// <summary>"td-&lt;LineIndex&gt;-&lt;hash8&gt;" / "ap-&lt;LineIndex&gt;-&lt;hash8&gt;" (§3.6).</summary>
    public static string RowKey(string prefix, int lineIndex, string rawLine)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawLine));
        string hex = Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
        return $"{prefix}-{lineIndex}-{hex}";
    }

    /// <summary>8-hex content hash of one pending safety-gate record, sent
    /// to the web as <see cref="SafetyDto.Summary"/> and required back on <c>decideSafety</c>. The card's
    /// Onayla/Reddet is then bound to THIS exact record (id + summary), not just its id: if the queue's record for
    /// that id changed underneath (edited, or removed and a new one appended with the same id) between the
    /// snapshot the person is looking at and their click, the summary no longer matches and the decision is
    /// refused ("stale" — "değişmiş, onaylanamaz") instead of silently approving something else.</summary>
    public static string SafetySummary(SafetyRequest s)
    {
        string canon = string.Join("\u0001", s.Id, s.Arac, s.Islem, s.Hedef, s.Komut, s.Risk, s.Neden);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canon));
        return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
    }

    public static Snapshot BuildSnapshot(AppState app)
    {
        var snap = app.Snapshot;
        var now = DateTime.Now;
        var today = snap.Today;
        var jobs = JobStore.GetAll();
        var install = ClaudeCli.Find();
        var newestClaude = jobs.Where(j => j.Tool == "claude" && j.End is not null)
            .OrderByDescending(j => DateTime.TryParse(j.End, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var e) ? e : DateTime.MinValue)
            .FirstOrDefault();
        string auth = newestClaude is null ? "bilinmiyor" : newestClaude.State == "giris" ? "gerekli" : "tamam";
        var claudeCli = new ClaudeCliDto(install is not null, install?.Version?.ToString(), install?.Exe, auth,
            install is not null ? ClaudeCli.LoginCommand(install.Exe) : null);
        var kasaLight = app.Lights.FirstOrDefault(l => l.Id == "kasa");
        var vaultInfo = new VaultInfoDto(snap.Root, snap.VaultExists, kasaLight?.State == "green");
        var projects = snap.Projects.Select(p => MapProject(p, app.Problems, today)).ToList();
        var allEntries = app.KayitByProject.Values.SelectMany(v => v).ToList();

        return new Snapshot(
            app.Stamp, now.ToString("o", CultureInfo.InvariantCulture), today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            today.ToString("d MMMM yyyy, dddd", Md.Tr), Greeting(now), vaultInfo, snap.Stopped, snap.Streak(), snap.Focus?.Name,
            new LimitDto(snap.ActiveCount, 3, snap.ActiveCount >= 3), MapDayTask(snap),
            new TodosDto(snap.Todos.Found, snap.Todos.Items.Select(t => new TodoItemDto(RowKey("td", t.LineIndex, t.RawLine), t.Text, t.Done)).ToList()),
            MapSafety(snap, jobs), MapApprovals(snap), MapExpected(snap), projects, snap.Tasks.Select(MapTask).ToList(),
            snap.Roles.Select(MapRole).ToList(), MapCompany(snap, jobs, app.Settings.KeepAwakeAcik), MapHistory(snap, allEntries, jobs, now), MapApplications(snap),
            MapLinkedIn(snap), MapShortcuts(snap), MapTools(snap.Tools), app.Lights, app.Usage, claudeCli, snap.Warnings);
    }

    /// <summary>Saate göre selam; kimseye adıyla hitap etmez (uygulama herkesin kendi kasasıyla çalışır).</summary>
    public static string Greeting(DateTime now) => now.Hour switch
    {
        >= 5 and < 12 => "Günaydın", >= 12 and < 18 => "İyi günler", >= 18 and < 23 => "İyi akşamlar", _ => "İyi geceler",
    };

    public static List<LightDto> MapLights(VaultSnapshot snap, List<JobDto> jobs, DateTime now)
    {
        var raw = SystemMonitor.CheckLights(snap.Root, now);
        var list = raw.Select(l => new LightDto(LightId(l.Label), l.Label, StateStr(l.State), l.Detail, l.Tooltip)).ToList();
        list.Add(ClaudeAuthLight(jobs));
        return list;
    }

    static string LightId(string label) => label switch
    {
        "Claude" => "claude", "Codex" => "codex", "Obsidian" => "obsidian", "Kasa" => "kasa",
        "Güvenlik kapısı" => "kapi", "Codex kotası" => "kota", _ => "sistem",
    };

    static string StateStr(LightState s) => s switch { LightState.Green => "green", LightState.Yellow => "yellow", LightState.Red => "red", _ => "off" };

    static LightDto ClaudeAuthLight(List<JobDto> jobs)
    {
        var newest = jobs.Where(j => j.Tool == "claude" && j.End is not null)
            .OrderByDescending(j => DateTime.TryParse(j.End, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var e) ? e : DateTime.MinValue)
            .FirstOrDefault();
        const string tip = "Claude komut satırı en son bir işte oturum açmayı başardı mı?";
        if (newest is null) return new LightDto("giris", "Claude girişi", "off", "denenmedi", tip);
        bool needed = newest.State == "giris";
        return new LightDto("giris", "Claude girişi", needed ? "yellow" : "green", needed ? "gerekli" : "tamam", tip);
    }

    public static UsageDto MapUsage(UsageToday u) => new(u.CodexTokens, u.ClaudeTokens, u.TimedOut,
        "Codex: " + SystemMonitor.FormatTokens(u.CodexTokens), "Claude: " + SystemMonitor.FormatTokens(u.ClaudeTokens));

    public static ProjectDto MapProject(ProjectCard p, List<ProblemDto> allProblems, DateOnly today)
    {
        var own = allProblems.Where(x => x.Project == p.Name).ToList();
        var (level, score) = ProblemDetector.ComputeHealth(own);
        LockInfo? lockInfo = null;
        if (!string.IsNullOrWhiteSpace(p.Kilit))
        {
            var (holder, since) = ProblemDetector.ParseLock(p.Kilit);
            double? hours = since is { } s ? (DateTime.Now - s).TotalHours : null;
            lockInfo = new LockInfo(holder, since?.ToString("o", CultureInfo.InvariantCulture), hours, hours is > 12);
        }
        return new ProjectDto(p.Name, $"20 Projeler/{p.Name}", p.Klasor, p.Durum, p.Odak, p.BittiTanimi, p.SonrakiAdim, p.NextStepShort,
            p.OlumKriteri, p.KillDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), p.DaysToKill(today), p.KararBekliyor, lockInfo,
            p.Baslangic?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), p.SonGuncelleme?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            p.SonGuncelleme is { } g ? today.DayNumber - g.DayNumber : null,
            new BittiInfo(p.DoneCount, p.TotalCount, p.TotalCount == 0 ? 0 : (int)Math.Round(p.Progress * 100),
                p.BittiItems.Select(b => new BittiItemDto(b.done, b.text)).ToList()),
            p.Engel, p.Beklenenler, !string.IsNullOrWhiteSpace(p.Klasor) && Directory.Exists(p.Klasor), new HealthDto(level, score, own.Count));
    }

    public static TaskDto MapTask(TaskItem t) => new(t.Project, t.Kimlik, t.Title, t.Durum, TaskDurumText(t.Durum), t.Atanan,
        t.SonTarih?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), t.RelPath, t.Modified.ToString("o", CultureInfo.InvariantCulture),
        (int)(DateTime.Now - t.Modified).TotalDays);

    static string TaskDurumText(string d) => d switch { "hazir" => "Hazır", "verildi" => "Verildi", "kontrol" => "Kontrol", "tamam" => "Tamam", "iptal" => "İptal", _ => d };

    public static RoleDto MapRole(RoleCard r) => new(r.Title, r.ShortName, r.Cagri, r.Departman, r.Yonetici, r.Arac, r.Model, r.Ozet, r.RelPath);

    static DayTaskDto? MapDayTask(VaultSnapshot snap)
    {
        var firstOpen = snap.Todos.Open.FirstOrDefault();
        if (firstOpen is not null) return new DayTaskDto(firstOpen.Text, "todo", RowKey("td", firstOpen.LineIndex, firstOpen.RawLine));
        var focus = snap.Focus;
        if (focus is not null && !string.IsNullOrWhiteSpace(focus.SonrakiAdim))
            return new DayTaskDto(Md.Plain(Md.AfterArrow(focus.SonrakiAdim)), "focus", null);
        return null;
    }

    static List<SafetyDto> MapSafety(VaultSnapshot snap, List<JobDto> jobs) =>
        snap.SafetyPending.Select(s => new SafetyDto(s.Id, s.Zaman?.ToString("o", CultureInfo.InvariantCulture), s.ToolName, s.Islem, s.Hedef,
            s.Komut, s.Risk, s.Critical, s.Neden, s.Kesildi, jobs.FirstOrDefault(j => j.SafetyIds.Contains(s.Id))?.Id, SafetySummary(s))).ToList();

    static ApprovalsDto MapApprovals(VaultSnapshot snap) =>
        new(snap.Jobs.AwaitingCount, snap.LinkedIn.Drafts.Count, snap.DecisionPending.Select(p => p.Name).ToList(), snap.LinkedIn.Found);

    static List<ExpectedDto> MapExpected(VaultSnapshot snap) =>
        snap.Active.Where(p => p.Beklenenler.Count > 0).Select(p => new ExpectedDto(p.Name, $"20 Projeler/{p.Name}/{p.Name}.md", p.Beklenenler)).ToList();

    static CompanyDto MapCompany(VaultSnapshot snap, List<JobDto> jobs, bool keepAwakeSetting)
    {
        var locks = snap.Projects.Where(p => !string.IsNullOrWhiteSpace(p.Kilit)).Select(p =>
        {
            var (holder, since) = ProblemDetector.ParseLock(p.Kilit);
            double? hours = since is { } s ? (DateTime.Now - s).TotalHours : null;
            return new LockRow(p.Name, holder, since?.ToString("o", CultureInfo.InvariantCulture), hours, hours is > 12);
        }).ToList();
        var activeTasks = snap.Tasks.Where(t => t.Durum is "verildi" or "kontrol").Select(MapTask).ToList();
        var scheduled = snap.Tools.ScheduledTasks.Select(s => new ScheduledDto(s.Name, s.Description, s.NextRun)).ToList();
        var sessions = snap.ClaudeSessions.Select(c => new ClaudeSessionDto(c.SessionId, c.Title, c.ProjectDir,
            c.LastActivityLocal.ToString("o", CultureInfo.InvariantCulture),
            c.State switch { ClaudeSessionState.Aktif => "aktif", ClaudeSessionState.IzinBekliyorOlabilir => "izin", _ => "bosta" }, false)).ToList();
        var tamGaz = new TamGazDto(snap.TamGaz.On, snap.TamGaz.Start?.ToString("o", CultureInfo.InvariantCulture),
            snap.TamGaz.End?.ToString("o", CultureInfo.InvariantCulture), snap.TamGaz.Hours);
        var watch = new CodexWatchDto(snap.Watch.Running, snap.Watch.Task, snap.Watch.WaitingQuota, snap.Watch.LastResult);
        var jobList = jobs.Where(j => j.CanCancel).Concat(jobs.Where(j => !j.CanCancel).Take(20)).ToList();
        var (keepOn, keepReason) = PowerManagement.Compute(keepAwakeSetting, jobs, snap.TamGaz.On);
        return new CompanyDto(jobList, locks, activeTasks, scheduled, sessions, tamGaz, watch, snap.TamGazLog, new KeepAwakeDto(keepOn, keepReason));
    }

    static List<HistoryItem> MapHistory(VaultSnapshot snap, List<KayitEntry> entries, List<JobDto> jobs, DateTime now)
    {
        var cutoff = now.AddDays(-30);
        var list = new List<HistoryItem>();
        foreach (var c in snap.Git.Recent.Where(c => c.When.LocalDateTime >= cutoff))
            list.Add(new HistoryItem(c.When.LocalDateTime.ToString("o", CultureInfo.InvariantCulture), DateOnly.FromDateTime(c.When.LocalDateTime).ToString("yyyy-MM-dd"),
                "commit", c.Tool, c.Text, null, null));
        foreach (var l in snap.Ledger.Where(l => !l.Auto && l.Local >= cutoff))
            list.Add(new HistoryItem(l.Local.ToString("o", CultureInfo.InvariantCulture), DateOnly.FromDateTime(l.Local).ToString("yyyy-MM-dd"), "istek",
                l.Arac == "codex" ? "codex" : "claude", l.Metin, l.Proje.Length > 0 ? l.Proje : null, null));
        foreach (var t in snap.Tasks.Where(t => t.Durum == "tamam" && t.Modified >= cutoff))
            list.Add(new HistoryItem(t.Modified.ToString("o", CultureInfo.InvariantCulture), DateOnly.FromDateTime(t.Modified).ToString("yyyy-MM-dd"), "gorev",
                null, $"{t.Kimlik}: {t.Title}", t.Project, new NoteTarget(t.RelPath, null)));
        foreach (var j in AppJournal.Load().Where(e => e.Zaman >= cutoff))
            list.Add(new HistoryItem(j.Zaman.ToString("o", CultureInfo.InvariantCulture), DateOnly.FromDateTime(j.Zaman).ToString("yyyy-MM-dd"), "isaret", null, j.Metin, null, null));
        foreach (var j in jobs)
            if (j.State == "bitti" && j.End is not null && DateTime.TryParse(j.End, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var end) && end >= cutoff)
                list.Add(new HistoryItem(end.ToString("o", CultureInfo.InvariantCulture), DateOnly.FromDateTime(end).ToString("yyyy-MM-dd"), "is", j.Tool, j.Title, j.Project, new JobTarget(j.Id)));
        foreach (var e in entries)
        {
            var dt = e.Date.ToDateTime(TimeOnly.TryParse(e.Time, out var t2) ? t2 : TimeOnly.MinValue);
            if (dt < cutoff) continue;
            list.Add(new HistoryItem(dt.ToString("o", CultureInfo.InvariantCulture), e.Date.ToString("yyyy-MM-dd"), e.Kind, e.Tool, e.Title, e.Project, new NoteTarget(e.Path, e.Anchor)));
        }
        return list.OrderByDescending(h => h.Time).Take(400).ToList();
    }

    static ApplicationsDto MapApplications(VaultSnapshot snap)
    {
        var sections = snap.Jobs.Sections.Select(sec => new ApplicationSectionDto(sec.Name, sec.Columns, sec.Rows.Select(r =>
            new ApplicationRowDto(RowKey("ap", r.LineIndex, r.RawLine), sec.Name, r.No, r.Firma, r.Pozisyon, r.Tur, r.Kanal, r.Sehir, r.Uygunluk,
                r.EksikBeceri, r.Url, r.Belgeler, r.Durum, DurumKind(r.Durum), r.Not, r.AwaitingApproval)).ToList())).ToList();
        return new ApplicationsDto(snap.Jobs.Found, snap.Jobs.AwaitingCount, Directory.Exists(AppPaths.JobDocsDir), sections);
    }

    static string DurumKind(string durum) => Md.Normalize(durum) switch
    {
        "onay bekliyor" => "onay-bekliyor", "onaylandi" => "onaylandi", "basvuruldu" => "basvuruldu", "mulakat" => "mulakat", "olumsuz" => "olumsuz", _ => "diger",
    };

    static LinkedInDto MapLinkedIn(VaultSnapshot snap) =>
        new(snap.LinkedIn.Found, snap.LinkedIn.Published.Select(p => new LinkedInPublishedDto(p.No, p.Tarih, p.Konu)).ToList(), snap.LinkedIn.Drafts);

    static List<ShortcutDto> MapShortcuts(VaultSnapshot snap)
    {
        var runs = ShortcutRuns.Load();
        string? lr(string id) => ShortcutRuns.IsoOrNull(runs, id);
        // /haftalik ve /devir birer Claude Code becerisidir; depoyla gelmez, kurulu değilse kutucuk soluk görünür.
        ShortcutDto Skill(string id, string icon)
        {
            bool ready = SkillInstalled(snap.Root, id);
            return new(id, "ac", icon, $"\"/{id}\" kopyala + Claude'u aç",
                ready ? $"/{id} becerisini panoya koyar" : "Beceri kurulu değil (KURULUM.md 3.6)", lr(id), ready);
        }
        var list = new List<ShortcutDto>
        {
            new("simdi", "ac", "doc", "01 Şimdi'yi Obsidian'da aç", "", lr("simdi")),
            new("kasa", "ac", "folder", "Kasayı aç", "", lr("kasa")),
            new("claudeUygulama", "ac", "code", "Claude'u aç", "", lr("claudeUygulama")),
            new("codexUygulama", "ac", "code", "Codex'i aç", "", lr("codexUygulama")),
        };
        // LinkedIn kutucuğu yalnızca kasada LinkedIn takvimi (40 Alanlar/LinkedIn Takvimi.md) varsa görünür.
        if (snap.LinkedIn.Found) list.Add(new("linkedin", "ac", "link", "LinkedIn'i aç", "", lr("linkedin")));
        list.Add(new("basvuruKlasoru", "ac", "folder", "İş başvuru klasörü", "", lr("basvuruKlasoru")));
        list.Add(Skill("haftalik", "calendar"));
        list.Add(Skill("devir", "doc"));
        list.Add(new("istekTara", "bakim", "code", "İstek defterini tara", "", lr("istekTara")));
        list.Add(new("kapiTest", "bakim", "shield", "Güvenlik kapısını dene", "Kapının kendi testini çalıştırır", lr("kapiTest")));
        return list;
    }

    /// <summary>Bir Claude Code becerisi (örneğin "devir") bu bilgisayarda kurulu mu? Claude Code'un baktığı yerler:
    /// kullanıcının <c>%USERPROFILE%\.claude\skills\&lt;ad&gt;\SKILL.md</c> ya da <c>.claude\commands\&lt;ad&gt;.md</c>
    /// dosyası, ya da kasanın kendi <c>.claude\skills\&lt;ad&gt;\SKILL.md</c> dosyası. Örnekleri depodaki
    /// <c>kasa-araclari/claude-becerileri/</c> klasöründe (KURULUM.md 3.6).</summary>
    public static bool SkillInstalled(string vault, string name)
    {
        string home = AppPaths.UserProfile;
        return File.Exists(Path.Combine(home, ".claude", "skills", name, "SKILL.md"))
            || File.Exists(Path.Combine(home, ".claude", "commands", name + ".md"))
            || (!string.IsNullOrEmpty(vault) && File.Exists(Path.Combine(vault, ".claude", "skills", name, "SKILL.md")));
    }

    static ToolsDto MapTools(ToolStatus t) => new(t.ClaudeHooks, t.ClaudeAgents,
        t.ScheduledTasks.Select(s => new ScheduledDto(s.Name, s.Description, s.NextRun)).ToList(), t.CodexMcpServers,
        t.CodexPlugins.Select(p => new PluginDto(p.name, p.enabled)).ToList(), t.CodexHooks, t.VaultOk, t.GitOk, t.ClaudeSettingsFound, t.CodexConfigFound);
}
