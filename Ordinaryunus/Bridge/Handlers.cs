// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Globalization;
using Ordinaryunus.Brain;
using Ordinaryunus.Data;
using Ordinaryunus.Host;
using Ordinaryunus.Jobs;
using Ordinaryunus.Security;

namespace Ordinaryunus.Bridge;

/// <summary>One method per request type (MIMARI §3.5). Heavy work already ran on the thread pool by the time
/// BridgeHost calls in (it awaits us); we post the response, the caller serializes it on the UI thread.</summary>
public sealed class Handlers(AppState app, BridgeHost bridge)
{
    static readonly object ClientErrGate = new();
    static readonly List<DateTime> ClientErrTimes = [];
    static readonly object ScriptGate = new();
    static bool _scriptRunning;

    public async Task<object> Dispatch(string type, object payload) => type switch
    {
        "hello" => Hello(),
        "getAuthState" => GetAuthState(),
        "login" => Login((LoginPayload)payload),
        "setPassword" => SetPassword((SetPasswordPayload)payload),
        "clientError" => ClientError((ClientErrorPayload)payload),
        "activity" => Activity((ActivityPayload)payload),
        "lock" => Lock(),
        "getSnapshot" => Mapper.BuildSnapshot(app),
        "refresh" => Refresh(),
        "getLights" => new { lights = app.Lights },
        "getBrainOverview" => GetBrainOverview(),
        "getBrainTree" => GetBrainTree(),
        "getNote" => GetNote((GetNotePayload)payload),
        "getProjectDetail" => GetProjectDetail((GetProjectDetailPayload)payload),
        "getLedger" => GetLedger((GetLedgerPayload)payload),
        "searchVault" => SearchVault((SearchVaultPayload)payload),
        "getGraph" => GraphBuilder.Build(RequireIndex(), ((GetGraphPayload)payload).Project),
        "getProblems" => new { problems = app.Problems, counts = Counts(app.Problems) },
        "toggleTodo" => ToggleTodo((ToggleTodoPayload)payload),
        "decideSafety" => DecideSafety((DecideSafetyPayload)payload),
        "approveJobs" => ApproveJobs((ApproveJobsPayload)payload),
        "runClaude" => RunClaude((RunWorkPayload)payload),
        "runCodex" => RunCodex((RunWorkPayload)payload),
        "copyWork" => CopyWork((CopyWorkPayload)payload),
        "getJobs" => new { jobs = JobStore.GetAll() },
        "getJobDetail" => GetJobDetail((JobIdPayload)payload),
        "cancelJob" => await CancelJob((JobIdPayload)payload),
        "retryJobNow" => RetryJobNow((JobIdPayload)payload),
        "emergencyStop" => EmergencyStop(),
        "resume" => Resume(),
        "tamGazOn" => TamGazOn((TamGazOnPayload)payload),
        "tamGazOff" => TamGazOff(),
        "openInObsidian" => OpenInObsidian((PathPayload)payload),
        "openUrl" => OpenUrl((OpenUrlPayload)payload),
        "openFolder" => OpenFolder((OpenFolderPayload)payload),
        "copyText" => CopyText((CopyTextPayload)payload),
        "runShortcut" => await RunShortcut((RunShortcutPayload)payload),
        "getSettings" => GetSettings(),
        "setSettings" => SetSettings((SetSettingsPayload)payload),
        "pickVaultFolder" => PickVaultFolder(),
        "changePassword" => ChangePassword((ChangePasswordPayload)payload),
        "pageReady" => PageReady((PageReadyPayload)payload),
        _ => throw new BridgeException("unknown_type", "Bilinmeyen istek türü."),
    };

    BrainIndex RequireIndex() => app.Index ?? throw new BridgeException("failed", "Beyin dizini henüz hazır değil.");

    // ---------------- auth / lifecycle ----------------

    static string AppVersion => typeof(Handlers).Assembly.GetName().Version?.ToString(3) ?? Imza.Surum;

    object Hello()
    {
        return new
        {
            appVersion = AppVersion, theme = app.Settings.Tema == "koyu" ? "dark" : "light", animations = app.Settings.Hareket,
            uiScale = app.Settings.YaziBoyutu, screenshotMode = app.ScreenshotMode, dryRun = app.DryRun,
            today = app.Snapshot.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            todayText = app.Snapshot.Today.ToString("d MMMM yyyy, dddd", Md.Tr),
            // SÖZLEŞME EKİ (v2.1): uygulamanın imzası (Imza.cs); arayüz giriş ekranında ve Hakkında kartında gösterir.
            imza = new { yapan = Imza.Yapan, surum = Imza.Surum, github = Imza.GitHub, lisans = Imza.Lisans },
        };
    }

    object GetAuthState()
    {
        var s = app.Settings;
        double wait = s.BeklemeBitis is { } b && b > DateTime.UtcNow ? (b - DateTime.UtcNow).TotalSeconds : 0;
        return new { hasPassword = s.HasPassword, unlocked = app.Unlocked, waitSeconds = (int)Math.Ceiling(wait), reason = app.LockReason };
    }

    object Login(LoginPayload req)
    {
        var s = app.Settings;
        if (!s.HasPassword) throw BridgeException.Bad("Önce bir şifre belirlemelisin.");
        if (s.BeklemeBitis is { } b && b > DateTime.UtcNow)
            throw new BridgeException("auth_wait", $"Çok denendi; {(int)Math.Ceiling((b - DateTime.UtcNow).TotalSeconds)} sn sonra dene.");
        if (!SettingsStore.CheckPassword(s, req.Password!))
        {
            var wait = SettingsStore.RegisterFailure(s);
            if (wait is { } w) throw new BridgeException("auth_wait", $"Çok denendi; {(int)w.TotalSeconds} sn sonra dene.");
            throw new BridgeException("auth_wrong", $"Yanlış şifre. Kalan deneme: {Math.Max(0, 5 - s.HataliDeneme)}");
        }
        SettingsStore.RegisterSuccess(s);
        app.Unlocked = true;
        app.LastActivity = DateTime.Now;
        _ = app.ReloadAsync(true);
        return new { unlocked = true };
    }

    object SetPassword(SetPasswordPayload req)
    {
        if (app.Settings.HasPassword) throw new BridgeException("auth_exists", "Zaten bir şifre var.");
        if (req.Password != req.Repeat) throw BridgeException.Bad("Şifreler aynı değil.");
        SettingsStore.SetPassword(app.Settings, req.Password!);
        SettingsStore.Save(app.Settings);
        app.Unlocked = true;
        app.LastActivity = DateTime.Now;
        _ = app.ReloadAsync(true);
        return new { unlocked = true };
    }

    object ClientError(ClientErrorPayload req)
    {
        lock (ClientErrGate)
        {
            ClientErrTimes.RemoveAll(t => (DateTime.Now - t).TotalMinutes > 1);
            if (ClientErrTimes.Count < 20)
            {
                ClientErrTimes.Add(DateTime.Now);
                try
                {
                    Directory.CreateDirectory(AppPaths.DataDir);
                    File.AppendAllText(AppPaths.WebErrorLog, $"{DateTime.Now:o} [{req.Source}] {req.Message}\n");
                }
                catch { }
            }
        }
        return new { };
    }

    object Activity(ActivityPayload req)
    {
        app.LastActivity = DateTime.Now;
        app.FocusRunning = req.FocusRunning;
        app.FocusHeartbeatAt = req.FocusRunning ? DateTime.Now : null;
        return new { };
    }

    object Lock()
    {
        app.Unlocked = false;
        app.LockReason = "manual";
        bridge.PostEvent("locked", new { reason = "manual" });
        app.OnLockRequested?.Invoke();
        return new { };
    }

    object Refresh()
    {
        _ = Task.Run(async () =>
        {
            await app.ReloadAsync(true);
            var now = DateTime.Now;
            app.RefreshLightsOnly(now);
            bridge.PostEvent("lightsChanged", new { lights = app.Lights });
            app.RefreshUsage(TimeSpan.FromSeconds(3));
            if (app.Usage is not null) bridge.PostEvent("usageChanged", app.Usage);
        });
        return new { };
    }

    // ---------------- brain ----------------

    object GetBrainOverview() => OverviewBuilder.Build(app.Snapshot, RequireIndex(), app.Git, app.Problems, app.KayitByProject, app.Lights,
        app.Usage, JobStore.GetAll(), app.Settings.UsdKuru, DateTime.Now);

    object GetBrainTree()
    {
        var index = RequireIndex();
        var stats = new BrainStatsDto(index.Notes.Count, index.Files.Count,
            index.Files.Keys.Select(k => System.IO.Path.GetDirectoryName(k) ?? "").Distinct().Count(),
            index.Resolved.Values.Sum(l => l.Count), index.Broken.Values.Sum(l => l.Count), index.Notes.Values.Sum(n => n.Words),
            index.IndexedAt.ToString("o", CultureInfo.InvariantCulture), index.IndexMs);
        return new { nodes = app.GetTree(), stats };
    }

    object GetNote(GetNotePayload req)
    {
        var index = RequireIndex();
        string path = Payloads.NormalizeVaultPath(req.Path!);
        var note = index.Find(path) ?? throw new BridgeException("not_found", "Not bulunamadı.");
        string html = MarkdownRenderer.Render(note.RawText, index.ResolveForRender, out var info);
        var headings = note.Headings.Select(h => new HeadingDto(h.Level, h.Text, "h-" + h.Line)).ToList();
        var outLinks = note.Links.Where(l => !l.IsEmbed).Select(l => (l.Label, target: index.Resolver.Resolve(l.Target)))
            .DistinctBy(x => x.target ?? x.Label).Select(x => new OutLinkDto(x.Label, x.target, x.target is null)).ToList();
        var backLinks = (index.BackLinks.GetValueOrDefault(path) ?? []).Distinct()
            .Select(p => { var src = index.Find(p); return new BackLinkDto(p, src?.Title ?? p, src?.Kind ?? "not", ""); }).ToList();
        var problems = app.Problems.Where(pr => pr.Target is NoteTarget nt && nt.Path == path).ToList();
        var breadcrumb = path.Contains('/') ? path.Split('/')[..^1].ToList() : [];
        var role = note.Kind == "rol" ? new RoleShortDto(app.Snapshot.Roles.FirstOrDefault(r => r.RelPath == path)?.ShortName ?? "") : null;
        return new NoteDto(path, note.Title, note.Kind, note.Project, breadcrumb, html,
            note.Frontmatter.Select(kv => new KeyValueDto(kv.Key, kv.Value)).ToList(), headings, outLinks, backLinks, problems,
            new TasksCountDto(note.TasksOpen, note.TasksDone), note.Words, note.Size, note.Mtime.ToLocalTime().ToString("o", CultureInfo.InvariantCulture),
            true, info.Truncated, role);
    }

    object GetProjectDetail(GetProjectDetailPayload req)
    {
        var p = app.Snapshot.Projects.FirstOrDefault(x => x.Name == req.Name) ?? throw new BridgeException("not_found", "Proje bulunamadı.");
        var today = app.Snapshot.Today;
        var entries = app.KayitByProject.GetValueOrDefault(p.Name, []);
        var health = OverviewBuilder.HealthFor(p, app.Problems, app.KayitByProject, today);
        var handoffs = entries.Where(e => e.Kind == "devir").OrderByDescending(e => e.Date).ThenByDescending(e => e.Time ?? "").Select(OverviewBuilder.ToDto).ToList();
        var decisions = entries.Where(e => e.Kind == "karar").OrderByDescending(e => e.Date).Select(OverviewBuilder.ToDto).ToList();
        var tasks = app.Snapshot.Tasks.Where(t => t.Project == p.Name).Select(Mapper.MapTask).ToList();
        var openItems = OverviewBuilder.OpenItemsFor(p, app.Snapshot, entries);
        var problems = app.Problems.Where(x => x.Project == p.Name).ToList();
        var index = RequireIndex();
        string prefix = $"20 Projeler/{p.Name}/";
        var files = index.Files.Where(f => f.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(f => new FileInfoDto(f.Key, index.Notes.TryGetValue(f.Key, out var n) ? n.Title : System.IO.Path.GetFileName(f.Key),
                index.Notes.TryGetValue(f.Key, out var n2) ? n2.Kind : "file", f.Value.Mtime.ToLocalTime().ToString("o", CultureInfo.InvariantCulture),
                f.Value.Size, f.Value.IsMarkdown)).ToList();
        var days = Enumerable.Range(0, 30).Select(i => today.AddDays(-(29 - i))).ToList();
        var changes = days.Select(d => app.Git.LastChangeByFile.Count(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            DateOnly.FromDateTime(kv.Value.LocalDateTime) == d)).ToList();
        int incoming = index.BackLinks.Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Sum(kv => kv.Value.Count(s => !s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
        int outgoing = index.Resolved.Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Sum(kv => kv.Value.Count(t => !t.target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
        return new ProjectDetail(Mapper.MapProject(p, app.Problems, today), health, handoffs, decisions, tasks, openItems, problems, files,
            new Activity30LiteDto(days.Select(d => d.ToString("yyyy-MM-dd")).ToList(), changes), new LinksCountDto(incoming, outgoing));
    }

    object GetLedger(GetLedgerPayload req)
    {
        var today = app.Snapshot.Today;
        var q = app.Snapshot.Ledger.Where(l => !l.Auto);
        if (req.Day is { } d) { var day = DateOnly.ParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture); q = q.Where(l => DateOnly.FromDateTime(l.Local) == day); }
        else { var cutoff = today.AddDays(-29); q = q.Where(l => DateOnly.FromDateTime(l.Local) >= cutoff); }
        var days = q.GroupBy(l => DateOnly.FromDateTime(l.Local)).OrderByDescending(g => g.Key)
            .Select(g => new LedgerDay(g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), g.Key.ToString("d MMMM", Md.Tr),
                g.OrderByDescending(x => x.Local).Take(req.Limit)
                 .Select(x => new LedgerEntryDto(x.Local.ToString("o", CultureInfo.InvariantCulture), x.Arac == "codex" ? "codex" : "claude", x.Proje, x.Metin, x.Auto))
                 .ToList()))
            .ToList();
        return new { days };
    }

    object SearchVault(SearchVaultPayload req)
    {
        var (results, total, tookMs) = VaultSearch.Search(RequireIndex(), app.Snapshot.Ledger, req.Q!, req.Limit);
        return new { results, total, tookMs };
    }

    static CountsDto Counts(List<ProblemDto> problems) => new(problems.Count(p => p.Severity == "kritik"),
        problems.Count(p => p.Severity == "dikkat"), problems.Count(p => p.Severity == "bilgi"));

    // ---------------- writes ----------------

    object ToggleTodo(ToggleTodoPayload req)
    {
        if (!app.TodoKeys.TryGetValue(req.Key!, out var item)) throw new BridgeException("stale", VaultWriter.StaleMessage);
        string filePath = System.IO.Path.Combine(app.Vault, "01 Şimdi.md");
        try { VaultWriter.ToggleTodo(filePath, item); }
        catch (StaleFileException) { _ = app.ReloadAsync(true); throw new BridgeException("stale", VaultWriter.StaleMessage); }
        catch (InvalidOperationException) { throw new BridgeException("readonly", "Deneme modu: hiçbir şey yazılmadı."); }
        bool doneNow = !item.Done;
        if (doneNow) AppJournal.Add(item.Text);
        int streak = app.Snapshot.Streak();
        _ = app.ReloadAsync(true);
        return new { done = doneNow, text = item.Text, celebrate = doneNow, streak };
    }

    object DecideSafety(DecideSafetyPayload req)
    {
        // read bekleyenler.jsonl/kararlar.jsonl FRESH
        // from disk right here instead of trusting app.Snapshot — that snapshot can be up to the ~1.5 s reload
        // debounce old, so a record an AI swapped out (or a decision an AI appended) right before this click could
        // still be invisible to a cached read. A fresh read shrinks that window to this one request.
        var freshPending = SafetyQueue.ReadPending(app.Vault);
        var pending = freshPending.FirstOrDefault(s => s.Id == req.Id) ?? throw new BridgeException("not_found", "İstek bulunamadı.");
        // the decision is bound to the EXACT record the person is looking at
        // (id + content hash), not just its id — if the queue's record for this id changed underneath since it was
        // listed (edited, or removed and re-appended with the same id), the summary no longer matches and the
        // decision is refused instead of silently approving/rejecting a different command than the one shown.
        string ozet = Mapper.SafetySummary(pending);
        if (!string.Equals(ozet, req.Summary, StringComparison.Ordinal))
            throw new BridgeException("stale", "Bu istek az önce değişti; onaylanamaz. Liste yenilendi.");
        if (req.Approve && pending.Kesildi) throw new BridgeException("forbidden", "Komutun tamamı görünmüyor, onaylanamaz.");
        if (req.Approve && pending.Critical && !req.ConfirmCritical) throw BridgeException.Bad("Kritik onay için kutucuğu işaretlemelisin.");
        try { SafetyQueue.Decide(app.Vault, req.Id!, req.Approve, ozet); }
        catch (InvalidOperationException) { throw new BridgeException("readonly", "Deneme modu: hiçbir şey yazılmadı."); }
        // remember every decision THIS app made
        // itself, outside the vault, so a future reload can flag any "onay" line in kararlar.jsonl that did not
        // come from a click here (see OwnDecisionLog / VaultSnapshot.Load).
        if (req.Approve) OwnDecisionLog.Record(req.Id!);
        _ = app.ReloadAsync(true);
        return new { decided = req.Approve ? "onay" : "red" };
    }

    object ApproveJobs(ApproveJobsPayload req)
    {
        var rows = new List<JobRow>();
        foreach (var k in req.Keys!)
        {
            if (!app.AppRowKeys.TryGetValue(k, out var row)) throw new BridgeException("stale", VaultWriter.StaleMessage);
            rows.Add(row);
        }
        string filePath = System.IO.Path.Combine(app.Vault, "40 Alanlar", "İş Başvuruları.md");
        int count;
        try { count = VaultWriter.ApproveJobs(filePath, rows); }
        catch (StaleFileException) { _ = app.ReloadAsync(true); throw new BridgeException("stale", VaultWriter.StaleMessage); }
        catch (InvalidOperationException) { throw new BridgeException("readonly", "Deneme modu: hiçbir şey yazılmadı."); }
        _ = app.ReloadAsync(true);
        return new { count };
    }

    // ---------------- AI jobs ----------------

    object RunClaude(RunWorkPayload req)
    {
        GuardCanRun();
        string composed = ClaudeCli.ComposePrompt(req.Text!, req.Role, req.Project);
        string? addDir = ResolveAddDir(req.Project);
        try
        {
            var job = JobStore.StartClaude(app.Vault, composed, req.Role, req.Project, addDir, req.Web);
            return new { job };
        }
        catch (ClaudeCliNotFoundException ex) { throw new BridgeException("no_cli", ex.Message); }
        catch (NodeNotFoundException ex) { throw new BridgeException("no_cli", ex.Message); }
        catch (NobetciNotFoundException ex) { throw new BridgeException("not_found", ex.Message); }
        catch (ScriptNotTrustedException ex) { throw new BridgeException("forbidden", ex.Message); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("durdurma", StringComparison.Ordinal)) { throw new BridgeException("stopped", ex.Message); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("2 Claude", StringComparison.Ordinal)) { throw new BridgeException("limit", ex.Message); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Salt okunur", StringComparison.Ordinal)) { throw new BridgeException("readonly", "Deneme modu: hiçbir şey yazılmadı."); }
    }

    object RunCodex(RunWorkPayload req)
    {
        GuardCanRun();
        string composed = ClaudeCli.ComposePrompt(req.Text!, req.Role, req.Project);
        try
        {
            var job = JobStore.StartCodex(app.Vault, composed, req.Role, req.Project);
            return new { job };
        }
        catch (NodeNotFoundException ex) { throw new BridgeException("no_cli", ex.Message); }
        catch (NobetciNotFoundException ex) { throw new BridgeException("not_found", ex.Message); }
        catch (ScriptNotTrustedException ex) { throw new BridgeException("forbidden", ex.Message); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("durdurma", StringComparison.Ordinal)) { throw new BridgeException("stopped", ex.Message); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Salt okunur", StringComparison.Ordinal)) { throw new BridgeException("readonly", "Deneme modu: hiçbir şey yazılmadı."); }
    }

    void GuardCanRun()
    {
        RequireVault();
        if (SafetyQueue.IsStopped(app.Vault)) throw new BridgeException("stopped", "Acil durdurma açık. Önce \"Devam Et\"e bas.");
    }

    /// <summary>Güvenli boş durum: kasa klasörü yokken (ilk açılış, taşınmış kasa) iş başlatılmaz ve kasa yolunda
    /// hiçbir klasör/dosya oluşturulmaz; kullanıcı Ayarlar'a yönlendirilir.</summary>
    void RequireVault()
    {
        if (!Directory.Exists(app.Vault))
            throw new BridgeException("not_found", "Kasa klasörü bulunamadı. Önce Ayarlar'dan kasanı seç.");
    }

    /// <summary>eskiden basit bir <c>string.StartsWith(desktop)</c> idi; aynı
    /// metinle başlayan kardeş bir klasör (ör. "C:\Users\ornek\DesktopEvil\x") yanlışlıkla geçerdi. Artık gerçek bir
    /// alt klasör kontrolü (<see cref="PathGuard"/>); Masaüstü'nün kendisini ve kasayı da reddeder.</summary>
    string? ResolveAddDir(string? project)
    {
        if (string.IsNullOrWhiteSpace(project)) return null;
        var card = app.Snapshot.Projects.FirstOrDefault(p => p.Name == project);
        if (card is null || string.IsNullOrWhiteSpace(card.Klasor) || !Directory.Exists(card.Klasor)) return null;
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!PathGuard.IsStrictSubfolderExcluding(card.Klasor, desktop, app.Vault)) return null;
        return Path.GetFullPath(card.Klasor);
    }

    object CopyWork(CopyWorkPayload req)
    {
        string composed = ClaudeCli.ComposePrompt(req.Text!, req.Role, req.Project);
        app.ClipboardSetText?.Invoke(composed);
        if (req.Tool == "claude") Launch.Claude(); else Launch.Codex();
        bridge.PostEvent("toast", new { text = "Panoya kopyalandı. " + (req.Tool == "claude" ? "Claude'da" : "Codex'te") + " Ctrl+V ve Enter.", tone = "info" });
        return new { };
    }

    object GetJobDetail(JobIdPayload req)
    {
        var detail = JobStore.GetDetail(req.Id!) ?? throw new BridgeException("not_found", "İş bulunamadı.");
        var (job, steps, logPath) = detail;
        var stepDtos = steps.Select(s => new JobStepDto(s.T.ToString("o", CultureInfo.InvariantCulture), s.Kind, s.Text)).ToList();
        string? resultHtml = job.ResultPreview is { Length: > 0 } rp ? MarkdownRenderer.Render(rp, _ => new ResolvedLink(false, null), out _) : null;
        var install = job.Tool == "claude" ? ClaudeCli.Find() : null;
        return new JobDetailDto(job, stepDtos, resultHtml, logPath, null, install?.Exe, job.State == "giris" && install is not null ? ClaudeCli.LoginCommand(install.Exe) : null);
    }

    async Task<object> CancelJob(JobIdPayload req)
    {
        if (!JobStore.Cancel(req.Id!)) throw new BridgeException("not_found", "İş çalışmıyor ya da bulunamadı.");
        // The watchman notices ".iptal" within ≤4 s (EK §2 item 4); a short wait so the response already shows
        // "durduruldu" for the common fast case instead of forcing the caller to re-poll immediately.
        await Task.Delay(150).ConfigureAwait(false);
        var job = JobStore.Get(req.Id!) ?? throw new BridgeException("not_found", "İş bulunamadı.");
        return new { job };
    }

    /// <summary>"Şimdi dene" (EK §2 item 3, SÖZLEŞME EKİ: <c>retryJobNow</c>). Never throws for the common case;
    /// <c>bad_request</c> only when the job isn't actually waiting on a quota (button should be hidden then anyway).</summary>
    object RetryJobNow(JobIdPayload req)
    {
        var r = JobStore.RetryNow(req.Id!);
        if (r == JobStore.RetryResult.NotFound) throw new BridgeException("not_found", "İş bulunamadı.");
        if (r == JobStore.RetryResult.NotWaiting) throw BridgeException.Bad("İş şu anda kota beklemiyor.");
        return new { };
    }

    object EmergencyStop()
    {
        // §2.1: dry-run never creates the real DURDUR flag or touches Tam Gaz; it only simulates stopping processes.
        // Kasa klasörü yoksa bayrak yazılmaz (olmayan kasa yolunda klasör oluşturulmasın); süreçler yine durdurulur.
        if (!app.DryRun && Directory.Exists(app.Vault))
        {
            SafetyQueue.CreateStopFlag(app.Vault);
            try { TamGaz.TurnOff(app.Vault); } catch { }
        }
        // Belt and suspenders (EK §2 item 5): the DURDUR flag alone already stops every watchman within ~2 s, and
        // every watchman also checks its own job's ".iptal" — StopAll() writes those directly too, and the CIM
        // sweep below hard-kills anything that somehow missed both (including a Tam Gaz job this app never started).
        JobStore.StopAll();
        var r = CodexRunner.EmergencyStop(app.DryRun);
        _ = app.ReloadAsync(true);
        return new { stopped = r.Stopped, errors = r.Errors };
    }

    object Resume()
    {
        if (!app.DryRun) SafetyQueue.RemoveStopFlag(app.Vault);
        _ = app.ReloadAsync(true);
        return new { };
    }

    object TamGazOn(TamGazOnPayload req)
    {
        GuardCanRun();
        if (app.DryRun) throw new BridgeException("readonly", "Deneme modu: hiçbir şey yazılmadı.");
        TamGaz.TurnOn(app.Vault, req.Hours);
        _ = app.ReloadAsync(true);
        return new { tamGaz = MapTamGaz() };
    }

    object TamGazOff()
    {
        if (app.DryRun) throw new BridgeException("readonly", "Deneme modu: hiçbir şey yazılmadı.");
        RequireVault();
        TamGaz.TurnOff(app.Vault);
        _ = app.ReloadAsync(true);
        return new { tamGaz = MapTamGaz() };
    }

    TamGazDto MapTamGaz()
    {
        var t = app.Snapshot.TamGaz;
        return new TamGazDto(t.On, t.Start?.ToString("o", CultureInfo.InvariantCulture), t.End?.ToString("o", CultureInfo.InvariantCulture), t.Hours);
    }

    // ---------------- launching ----------------

    object OpenInObsidian(PathPayload req)
    {
        string path = Payloads.NormalizeVaultPath(req.Path!);
        if (!RequireIndex().Files.ContainsKey(path)) throw new BridgeException("not_found", "Not bulunamadı.");
        Launch.Obsidian(app.Vault, path);
        return new { };
    }

    object OpenUrl(OpenUrlPayload req)
    {
        Launch.Open(req.Url!);
        return new { };
    }

    object OpenFolder(OpenFolderPayload req)
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        switch (req.Which)
        {
            case "kasa":
                if (!Launch.OpenFolder(app.Vault)) throw new BridgeException("not_found", "Kasa klasörü bulunamadı. Ayarlar'dan kasanı seç.");
                break;
            case "basvuruKlasoru": OpenBasvuruKlasoru(); break;
            case "basvuruBelgeleri":
            {
                string baseDir = AppPaths.JobDocsDir;
                string full = req.Sub is { Length: > 0 } sub ? System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, sub)) : baseDir;
                if (!full.StartsWith(System.IO.Path.GetFullPath(baseDir), StringComparison.OrdinalIgnoreCase)) throw new BridgeException("forbidden", "Klasör kapsam dışında.");
                if (!Launch.OpenFolder(Directory.Exists(full) ? full : baseDir))
                    throw new BridgeException("not_found", $"Başvuru belgeleri klasörü yok: {baseDir}. {AppPaths.BasvuruIpucu}");
                break;
            }
            case "claudeIsleri": Launch.OpenFolder(AppPaths.ClaudeJobsDir); break;
            case "codexIsleri": Launch.OpenFolder(AppPaths.CodexJobsDir); break;
            case "projeKlasoru":
            {
                // Burada da tam-yol alt-klasör kontrolü (PathGuard), önek karşılaştırması değil.
                var card = app.Snapshot.Projects.FirstOrDefault(p => p.Name == req.Project) ?? throw new BridgeException("not_found", "Proje bulunamadı.");
                if (!PathGuard.IsStrictSubfolderExcluding(card.Klasor, desktop, app.Vault))
                    throw new BridgeException("forbidden", "Proje klasörü izinli konumda değil.");
                string full = System.IO.Path.GetFullPath(card.Klasor);
                if (!Directory.Exists(full)) throw new BridgeException("not_found", "Klasör bulunamadı.");
                Launch.OpenFolder(full);
                break;
            }
            case "notKonumu":
            {
                string path = Payloads.NormalizeVaultPath(req.Path ?? throw BridgeException.Bad("path gerekli."));
                if (!RequireIndex().Files.ContainsKey(path)) throw new BridgeException("not_found", "Not bulunamadı.");
                Launch.Reveal(System.IO.Path.Combine(app.Vault, path.Replace('/', System.IO.Path.DirectorySeparatorChar)));
                break;
            }
        }
        return new { };
    }

    /// <summary>İş başvuru klasörünü açar; yoksa nasıl ayarlanacağını söyleyen bir hata verir.</summary>
    static void OpenBasvuruKlasoru()
    {
        if (!Launch.OpenFolder(AppPaths.BasvuruKlasoru))
            throw new BridgeException("not_found", $"İş başvuru klasörü yok: {AppPaths.BasvuruKlasoru}. {AppPaths.BasvuruIpucu}");
    }

    object CopyText(CopyTextPayload req)
    {
        app.ClipboardSetText?.Invoke(req.Text!);
        return new { };
    }

    async Task<object> RunShortcut(RunShortcutPayload req)
    {
        string vault = app.Vault;
        // A13: record "son çalışma" for this tile regardless of kind (opened/copied/script); no-op under
        // --dry-run/--selftest/--screenshots (ShortcutRuns.MarkRun checks VaultWriter.ReadOnlyMode itself).
        ShortcutRuns.MarkRun(req.Id!);
        switch (req.Id)
        {
            case "simdi": Launch.Obsidian(vault, "01 Şimdi.md"); return new { kind = "opened", output = (string?)null, exitCode = (int?)null, timedOut = false };
            case "kasa": Launch.OpenFolder(vault); return new { kind = "opened", output = (string?)null, exitCode = (int?)null, timedOut = false };
            case "claudeUygulama": Launch.Claude(); return new { kind = "opened", output = (string?)null, exitCode = (int?)null, timedOut = false };
            case "codexUygulama": Launch.Codex(); return new { kind = "opened", output = (string?)null, exitCode = (int?)null, timedOut = false };
            case "linkedin": Launch.Open("https://www.linkedin.com/feed/"); return new { kind = "opened", output = (string?)null, exitCode = (int?)null, timedOut = false };
            case "basvuruKlasoru": OpenBasvuruKlasoru(); return new { kind = "opened", output = (string?)null, exitCode = (int?)null, timedOut = false };
            case "haftalik": case "devir":
            {
                // Beceri kurulu değilse Claude'a tanımadığı bir komut yapıştırtma; nasıl kurulacağını söyle.
                if (!Mapper.SkillInstalled(vault, req.Id!))
                    throw new BridgeException("not_found", $"/{req.Id} becerisi kurulu değil. Depodaki kasa-araclari\\claude-becerileri\\{req.Id} " +
                        "klasörünü %USERPROFILE%\\.claude\\skills\\ altına kopyala (KURULUM.md 3.6).");
                string text = "/" + req.Id;
                app.ClipboardSetText?.Invoke(text);
                Launch.Claude();
                bridge.PostEvent("toast", new { text = "Panoya kopyalandı. Claude'da Ctrl+V ve Enter.", tone = "info" });
                return new { kind = "copied", output = (string?)null, exitCode = (int?)null, timedOut = false };
            }
            case "istekTara": return await RunScript(vault, "node", [System.IO.Path.Combine(AppPaths.UserProfile, ".claude", "hooks", "ikinci-beyin", "istek-defteri.mjs"), "tara"]);
            // Kapının kendi testi: sadece geçici klasöre yazar (KURULUM.md 3.1'deki komutun aynısı).
            case "kapiTest": return await RunScript(vault, "node", [System.IO.Path.Combine(AppPaths.UserProfile, ".claude", "hooks", "ikinci-beyin", "guvenlik-kapisi.mjs"), "test"]);
            default: throw BridgeException.Bad();
        }
    }

    async Task<object> RunScript(string vault, string exe, List<string> args)
    {
        // --dry-run hiçbir GERÇEK betiği çalıştırmamalı. Açma ve başlatma işleri zaten Launch.Disabled'dan geçiyor;
        // Kestirmeler betikleri ise doğrudan ProcessRunner ile çalıştığı için eskiden deneme modunda bile geçici veri
        // klasörünün dışında bir şeyler değiştirebiliyordu.
        if (app.DryRun)
            return new { kind = "script", output = "Deneme modu: betik çalıştırılmadı (sahte çıktı).", exitCode = (int?)0, timedOut = false };
        lock (ScriptGate)
        {
            if (_scriptRunning) throw new BridgeException("busy", "Başka bir betik çalışıyor; bitmesini bekle.");
            _scriptRunning = true;
        }
        try
        {
            // Betik bu kasada (ya da kullanıcı klasöründe) yoksa node'un anlaşılmaz hatası yerine açık bir mesaj ver:
            // bu bakım betikleri isteğe bağlıdır, her kasada bulunmaz.
            string scriptFull = System.IO.Path.IsPathRooted(args[0]) ? args[0] : System.IO.Path.Combine(vault, args[0]);
            if (!File.Exists(scriptFull))
                return new { kind = "script", output = $"Bu betik bulunamadı: {args[0]}\nBu kısayol isteğe bağlıdır; betik kasanda yoksa bir şey yapmaz.",
                    exitCode = (int?)null, timedOut = false };
            // iş nöbetçisiyle aynı "incelenmemiş kasa betiği korumasız çalışmaz"
            // kuralı. Sadece kasadaki betiklere uygulanır (tam yollu olanlar, ör. %USERPROFILE%\.claude altındaki
            // istek-defteri.mjs, kasanın git deposunda değildir ve burada denetlenmez).
            if (!System.IO.Path.IsPathRooted(args[0]))
            {
                try { VaultGitGuard.EnsureSafeToRun(vault, args[0]); }
                catch (ScriptNotTrustedException ex) { throw new BridgeException("forbidden", ex.Message); }
            }
            // Only the first argument (the script path) is vault-relative; keep any following literal args (e.g. "tara") as is.
            var absArgs = new List<string> { System.IO.Path.IsPathRooted(args[0]) ? args[0] : System.IO.Path.Combine(vault, args[0]) };
            absArgs.AddRange(args.Skip(1));
            var r = await ProcessRunner.RunAsync(exe, absArgs, vault, 60_000).ConfigureAwait(false);
            string output = r.Output.Length > 50_000 ? r.Output[..50_000] : r.Output;
            return new { kind = "script", output, exitCode = (int?)r.ExitCode, timedOut = r.TimedOut };
        }
        finally { lock (ScriptGate) _scriptRunning = false; }
    }

    // ---------------- settings ----------------

    object GetSettings() => BuildSettingsDto();

    SettingsDto BuildSettingsDto() => new(app.Settings.KasaYolu, Directory.Exists(app.Settings.KasaYolu), app.Settings.YaziBoyutu,
        app.Settings.KilitDakika, app.Settings.UsdKuru, app.Settings.Tema == "koyu" ? "dark" : "light", app.Settings.Hareket,
        AppPaths.DataDir, AppVersion, app.DryRun, app.Settings.KeepAwakeAcik);

    object SetSettings(SetSettingsPayload req)
    {
        bool vaultChanged = false;
        if (req.VaultPath is { Length: > 0 } vp)
        {
            // kasa klasörü değişikliği için (1) klasör gerçekten bir kasa olmalı
            // (AGENTS.md var — kurallar orada durur; rastgele bir klasör geçerli bir "kasa" değildir) ve (2) sadece web
            // sayfasındaki bir tık değil, kullanıcının yerel Windows onay kutusundaki kendi evet/hayır cevabı gerekir;
            // yoksa bozuk ya da ele geçirilmiş bir sayfa uygulamayı sessizce saldırganın verisine yöneltebilirdi.
            if (!Directory.Exists(vp)) throw BridgeException.Bad("Klasör bulunamadı.");
            if (!File.Exists(Path.Combine(vp, "AGENTS.md"))) throw BridgeException.Bad("Bu klasörde AGENTS.md yok; kasa klasörü olamaz.");
            vaultChanged = !string.Equals(app.Settings.KasaYolu, vp, StringComparison.OrdinalIgnoreCase);
            if (vaultChanged)
            {
                bool confirmed = app.ConfirmVaultChange?.Invoke(vp) ?? true; // headless (selftest/dry-run/screenshots): auto-confirm, no Form exists
                if (!confirmed) throw new BridgeException("forbidden", "Kasa klasörü değişikliği onaylanmadı.");
            }
            app.Settings.KasaYolu = vp;
        }
        if (req.UiScale is { } scale) { app.Settings.YaziBoyutu = scale; app.Settings.YaziBoyutuSecildi = true; app.ApplyZoom?.Invoke(scale / 100.0); }
        if (req.LockMinutes is { } lm) app.Settings.KilitDakika = lm;
        if (req.UsdRate is { } ur) app.Settings.UsdKuru = ur;
        if (req.Theme is { } theme) app.Settings.Tema = theme == "dark" ? "koyu" : "acik";
        if (req.Animations is { } anim) app.Settings.Hareket = anim;
        if (req.KeepAwake is { } ka) app.Settings.KeepAwakeAcik = ka;
        try { SettingsStore.Save(app.Settings); } catch (InvalidOperationException) { /* selftest/screenshots: read-only, ignore */ }
        _ = app.ReloadAsync(vaultChanged);
        return BuildSettingsDto();
    }

    object PickVaultFolder() => new { path = app.PickVaultFolder?.Invoke() };

    object PageReady(PageReadyPayload req)
    {
        app.OnPageReady?.Invoke(req.Page, req.Tab);
        return new { };
    }

    object ChangePassword(ChangePasswordPayload req)
    {
        if (!SettingsStore.CheckPassword(app.Settings, req.Old!)) throw new BridgeException("auth_wrong", "Eski şifre yanlış.");
        if (req.New != req.Repeat) throw BridgeException.Bad("Yeni şifreler aynı değil.");
        SettingsStore.SetPassword(app.Settings, req.New!);
        SettingsStore.Save(app.Settings);
        return new { };
    }
}
