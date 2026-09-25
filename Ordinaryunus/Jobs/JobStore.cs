// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Ordinaryunus.Bridge;
using Ordinaryunus.Data;

namespace Ordinaryunus.Jobs;

/// <summary>
/// Unified Claude + Codex job list (EK-v2.1 §1-2, superseding MIMARI §6.7's in-process design): every job — whether
/// started from this app or, independently, by the Tam Gaz scheduled worker calling the same watchman — is a row on
/// disk (<c>&lt;damga&gt;.nobet.json</c> + <c>&lt;damga&gt;.jsonl</c>) under <c>claude-isler</c>/<c>codex-isler</c>.
/// This store polls those two folders (FileSystemWatcher is not used: a 1 s poll already meets the "at most 2
/// pushes/s per job" budget and needs no per-file watcher lifetime management), feeds new <c>.jsonl</c> bytes to
/// <see cref="ClaudeStreamParser"/>/<see cref="CodexStreamParser"/> for the human-readable "now" text and step list,
/// and treats <c>.nobet.json</c>'s own <c>durum</c> as authoritative for the wire state ("durum kaynağı sırası:
/// nobet.json &gt; ayrıştırıcı"). Works headlessly (no Form needed) so --selftest/--dump-json can use it too.
/// </summary>
public static class JobStore
{
    public static bool DryRun { get => NobetciRunner.DryRun; set => NobetciRunner.DryRun = value; }
    public static event Action<JobDto>? Changed;

    sealed class TrackedJob
    {
        public required string Id;                 // "claude:<damga>" | "codex:<damga>"
        public required string Tool;
        public required string Damga;
        public required string JobsDir;
        public string Prompt = "";                  // composed, WITHOUT the AGENTS suffix (title/display source)
        public string? Role;
        public string? Project;
        public DateTime Start;                       // our best guess (Track() time, or nobet.json's "basla", or file time)
        public long LastReadBytes;
        public ClaudeJobState? ClaudeState;
        public CodexJobState? CodexState;
        public NobetStatus? Nobet;
        public DateTime LastNobetWrite = DateTime.MinValue;
        public bool AppStarted;                      // true if this session's Start() created it (vs. discovered on disk)
    }

    static bool _wired;
    static readonly object Gate = new();
    static readonly Dictionary<string, TrackedJob> Tracked = new(StringComparer.Ordinal);
    static System.Threading.Timer? _pollTimer;

    /// <summary>Test-only: drops all in-memory tracking so the next poll rebuilds it purely from disk — used by
    /// SelfTestV2 to simulate "Ordinaryunus yeniden açıldı" without actually restarting the process.</summary>
    internal static void ForgetAllForTest()
    {
        lock (Gate) Tracked.Clear();
        PollOnce(notify: false);
    }

    public static void EnsureWired()
    {
        lock (Gate)
        {
            if (_wired) return;
            _wired = true;
        }
        PollOnce(notify: false); // recovery scan: pick up jobs already on disk (app restart, or a Tam Gaz job)
        _pollTimer = new System.Threading.Timer(_ => { try { PollOnce(notify: true); } catch { } }, null, 1000, 1000);
    }

    // ---------------- starting ----------------

    public static JobDto StartClaude(string vault, string composedPrompt, string? role, string? project, string? addDir, bool web)
    {
        var r = NobetciRunner.Start(vault, "claude", composedPrompt, addDir, web);
        return Track(r, composedPrompt, role, project);
    }

    public static JobDto StartCodex(string vault, string composedPrompt, string? role, string? project)
    {
        var r = NobetciRunner.Start(vault, "codex", composedPrompt, null, false);
        return Track(r, composedPrompt, role, project);
    }

    static JobDto Track(NobetciRunner.LaunchResult r, string prompt, string? role, string? project)
    {
        var job = new TrackedJob
        {
            Id = r.Tool + ":" + r.Damga, Tool = r.Tool, Damga = r.Damga, JobsDir = r.JobsDir, Prompt = prompt,
            Role = role, Project = project, Start = r.Start, AppStarted = true,
            ClaudeState = r.Tool == "claude" ? new ClaudeJobState() : null,
            CodexState = r.Tool == "codex" ? new CodexJobState() : null,
        };
        lock (Gate) Tracked[job.Id] = job;
        return ToDto(job);
    }

    /// <summary>Non-terminal jobs of one tool, EXCLUDING "kota-bekliyor" ("Aynı anda en fazla 2 çalışan Claude işi
    /// kuralı sürer; kota-bekliyor durumundakiler bu sayıya girmez").</summary>
    public static int CountRunning(string tool)
    {
        lock (Gate)
        {
            return Tracked.Values.Count(j => j.Tool == tool && MapState(j) is "basliyor" or "calisiyor");
        }
    }

    // ---------------- signals ----------------

    public static bool Cancel(string id)
    {
        TrackedJob? job;
        lock (Gate) Tracked.TryGetValue(id, out job);
        if (job is null) return false;
        string state = MapState(job);
        if (state is not ("basliyor" or "calisiyor" or "kota-bekliyor")) return false;
        NobetciRunner.SignalCancel(job.JobsDir, job.Damga);
        return true;
    }

    public enum RetryResult { NotFound, NotWaiting, Ok }

    public static RetryResult RetryNow(string id)
    {
        TrackedJob? job;
        lock (Gate) Tracked.TryGetValue(id, out job);
        if (job is null) return RetryResult.NotFound;
        if (MapState(job) != "kota-bekliyor") return RetryResult.NotWaiting;
        NobetciRunner.SignalRetryNow(job.JobsDir, job.Damga);
        return RetryResult.Ok;
    }

    /// <summary>StopAll (EmergencyStop, EK §2 item 5): "her işin .iptal dosyasını oluşturur" — on top of the
    /// DURDUR flag every watchman already polls every 2 s.</summary>
    public static void StopAll()
    {
        List<TrackedJob> running;
        lock (Gate) running = Tracked.Values.Where(j => MapState(j) is "basliyor" or "calisiyor" or "kota-bekliyor").ToList();
        foreach (var j in running) NobetciRunner.SignalCancel(j.JobsDir, j.Damga);
    }

    // ---------------- reading ----------------

    public static List<JobDto> GetAll(int maxTotal = 50)
    {
        PollIfStale();
        List<TrackedJob> all;
        lock (Gate) all = Tracked.Values.ToList();
        var running = all.Where(j => MapState(j) is "basliyor" or "calisiyor" or "kota-bekliyor")
            .OrderByDescending(j => j.Start).Select(ToDto).ToList();
        var runningIds = running.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var history = all.Where(j => !runningIds.Contains(j.Id)).OrderByDescending(j => j.Start)
            .Take(Math.Max(0, maxTotal - running.Count)).Select(ToDto).ToList();
        return running.Concat(history).ToList();
    }

    public static JobDto? Get(string id)
    {
        PollIfStale();
        lock (Gate) return Tracked.TryGetValue(id, out var j) ? ToDto(j) : null;
    }

    public static (JobDto job, List<JobStepEntry> steps, string logPath)? GetDetail(string id)
    {
        PollIfStale();
        TrackedJob? job;
        lock (Gate) Tracked.TryGetValue(id, out job);
        if (job is null) return null;
        var steps = job.ClaudeState?.StepLog ?? job.CodexState?.StepLog ?? [];
        return (ToDto(job), steps.ToList(), Path.Combine(job.JobsDir, job.Damga + ".jsonl"));
    }

    /// <summary>App-startup / periodic recovery: kills nothing, only re-reads what's already on disk (MIMARI §6.6
    /// restart rule, adapted to EK's file-based model). Called with a 30-day window like the old history reader.</summary>
    public static void CheckWatchdogs(DateTime now) { /* the watchman enforces its own --sure-dk cap now; nothing to do here */ }

    static DateTime _lastPoll = DateTime.MinValue;
    static void PollIfStale()
    {
        if ((DateTime.Now - _lastPoll).TotalMilliseconds < 400) return;
        PollOnce(notify: false);
    }

    static void PollOnce(bool notify)
    {
        _lastPoll = DateTime.Now;
        try { ScanDir(AppPaths.ClaudeJobsDir, "claude"); } catch { }
        try { ScanDir(AppPaths.CodexJobsDir, "codex"); } catch { }
        List<TrackedJob> snapshot;
        lock (Gate) snapshot = Tracked.Values.ToList();
        foreach (var job in snapshot)
        {
            bool changed;
            try { changed = RefreshOne(job); } catch { changed = false; }
            if (changed && notify) { try { Changed?.Invoke(ToDto(job)); } catch { } }
        }
        PruneOld();
    }

    static void ScanDir(string dir, string tool)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.EnumerateFiles(dir, "*.nobet.json"))
        {
            string name = Path.GetFileName(f);
            string damga = name[..^".nobet.json".Length];
            string id = tool + ":" + damga;
            lock (Gate) { if (Tracked.ContainsKey(id)) continue; }
            var built = BuildFromDisk(dir, tool, damga);
            if (built is null) continue;
            lock (Gate) Tracked.TryAdd(id, built);
        }
    }

    static TrackedJob? BuildFromDisk(string dir, string tool, string damga)
    {
        string metaPath = Path.Combine(dir, damga + ".nobet.json");
        var nobet = NobetStatus.TryRead(metaPath);
        DateTime start = nobet?.Basla?.LocalDateTime ?? SafeFileTime(Path.Combine(dir, damga + ".jsonl")) ?? DateTime.Now;
        string prompt = ReadIstemBestEffort(dir, damga);
        return new TrackedJob
        {
            Id = tool + ":" + damga, Tool = tool, Damga = damga, JobsDir = dir, Prompt = prompt, Start = start, AppStarted = false,
            ClaudeState = tool == "claude" ? new ClaudeJobState() : null,
            CodexState = tool == "codex" ? new CodexJobState() : null,
            Nobet = nobet,
        };
    }

    static string ReadIstemBestEffort(string dir, string damga)
    {
        try
        {
            string p = Path.Combine(dir, damga + ".istem.txt");
            if (!File.Exists(p)) return "(önceki oturumdan iş)";
            string text = File.ReadAllText(p);
            if (text.EndsWith(ClaudeCli.AgentsSuffix, StringComparison.Ordinal)) text = text[..^ClaudeCli.AgentsSuffix.Length];
            return text.Trim();
        }
        catch { return "(önceki oturumdan iş)"; }
    }

    static DateTime? SafeFileTime(string path) { try { return File.Exists(path) ? File.GetCreationTime(path) : null; } catch { return null; } }

    /// <summary>Re-reads a tracked job's files if they changed. Returns true when anything visible changed.</summary>
    static bool RefreshOne(TrackedJob job)
    {
        bool changed = false;
        string metaPath = Path.Combine(job.JobsDir, job.Damga + ".nobet.json");
        DateTime metaWrite = SafeFileTime2(metaPath);
        if (metaWrite != job.LastNobetWrite || (job.Nobet is null && File.Exists(metaPath)))
        {
            var n = NobetStatus.TryRead(metaPath);
            if (n is not null) { job.Nobet = n; job.LastNobetWrite = metaWrite; changed = true; }
        }

        string jsonlPath = Path.Combine(job.JobsDir, job.Damga + ".jsonl");
        long len = SafeLength(jsonlPath);
        if (len > job.LastReadBytes)
        {
            string tail = ReadRange(jsonlPath, job.LastReadBytes, len);
            job.LastReadBytes = len;
            if (tail.Length > 0)
            {
                foreach (var line in tail.Split('\n'))
                {
                    string l = line.TrimEnd('\r');
                    if (l.Length == 0) continue;
                    if (job.Tool == "claude") ClaudeStreamParser.Feed(job.ClaudeState!, l, DateTime.Now);
                    else CodexStreamParser.Feed(job.CodexState!, l, DateTime.Now);
                }
                changed = true;
            }
        }
        return changed;
    }

    static DateTime SafeFileTime2(string path) { try { return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue; } catch { return DateTime.MinValue; } }
    static long SafeLength(string path) { try { return new FileInfo(path).Exists ? new FileInfo(path).Length : 0; } catch { return 0; } }

    static string ReadRange(string path, long from, long to)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            fs.Seek(from, SeekOrigin.Begin);
            var buf = new byte[to - from];
            int read = 0;
            while (read < buf.Length) { int n = fs.Read(buf, read, buf.Length - read); if (n <= 0) break; read += n; }
            return Encoding.UTF8.GetString(buf, 0, read);
        }
        catch { return ""; }
    }

    static void PruneOld()
    {
        lock (Gate)
        {
            if (Tracked.Count <= 500) return;
            var cutoff = DateTime.Now.AddDays(-30);
            foreach (var id in Tracked.Where(kv => kv.Value.Start < cutoff && MapState(kv.Value) is not ("basliyor" or "calisiyor" or "kota-bekliyor"))
                         .Select(kv => kv.Key).ToList())
                Tracked.Remove(id);
        }
    }

    // ---------------- state mapping (nobet.json is authoritative; see class remark) ----------------

    /// <summary>a bare "pid alive + named node" check is fooled once Windows
    /// reuses the pid for a completely unrelated node.exe (a hook, an MCP server, ...) after the real watchman
    /// died — the job then shows "Çalışıyor"/"kota-bekliyor" forever and keeps blocking the 2-job limit and the
    /// keep-awake guard. <paramref name="expectedStart"/> is nobet.json's own "basla" stamp; the watchman already
    /// guards itself against the same reuse by comparing the OS process start time to it (35ca98a), so this mirrors
    /// that check on the C# side instead of trusting the pid alone.</summary>
    static bool IsAliveNode(int pid, DateTimeOffset? expectedStart)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (p.HasExited || !p.ProcessName.Equals("node", StringComparison.OrdinalIgnoreCase)) return false;
            if (expectedStart is { } es)
            {
                try
                {
                    var actualStart = new DateTimeOffset(p.StartTime.ToUniversalTime(), TimeSpan.Zero);
                    if (Math.Abs((actualStart - es).TotalSeconds) > 10) return false; // different process, reused pid
                }
                catch { /* StartTime can throw (access denied, already exited) — fall back to the name check alone */ }
            }
            return true;
        }
        catch { return false; }
    }

    static string MapState(TrackedJob job)
    {
        var n = job.Nobet;
        if (n?.Durum is null) return "basliyor";
        bool pidAlive = n.Pid is { } pid && IsAliveNode(pid, n.Basla);
        switch (n.Durum)
        {
            case "calisiyor": return pidAlive || n.Pid is null ? "calisiyor" : "yarida";
            case "kota-bekliyor": return pidAlive || n.Pid is null ? "kota-bekliyor" : "yarida";
            case "bitti": return "bitti";
            case "durduruldu": return "durduruldu";
            case "giris": return "giris";
            case "hata":
                // Terminal "kota" (EK §2 item 3): "eski terminal kota durumu artık sadece nöbetçinin deneme hakkı
                // biterse (çıkış 5) kullanılır" — the watchman's own note text says so; keep that distinct from a
                // plain failure so the tone stays yellow, not red.
                return n.Not is { Length: > 0 } note && note.Contains("denemede açılmadı", StringComparison.OrdinalIgnoreCase) ? "kota" : "hata";
            default: return "basliyor";
        }
    }

    public static string StateText(string state) => state switch
    {
        "basliyor" => "Başlıyor", "calisiyor" => "Çalışıyor", "bitti" => "Bitti", "hata" => "Hata", "giris" => "Giriş gerekli",
        "kota" => "Kota dolu (deneme hakkı bitti)", "kota-bekliyor" => "Kota dolu", "durduruldu" => "Durduruldu", "yarida" => "Yarıda kaldı", _ => state,
    };

    public static string ToneFor(string state) => state switch
    {
        "basliyor" or "calisiyor" => "teal", "bitti" => "green", "hata" or "giris" => "red", "kota" or "kota-bekliyor" => "yellow",
        "durduruldu" or "yarida" => "grey", _ => "grey",
    };

    static string FormatCountdown(TimeSpan left)
    {
        if (left <= TimeSpan.Zero) return "Devam ediyor…";
        if (left.TotalHours >= 1) return $"{(int)left.TotalHours} sa {left.Minutes} dk kaldı";
        if (left.TotalMinutes >= 1) return $"{(int)left.TotalMinutes} dk kaldı";
        return $"{Math.Max(1, (int)left.TotalSeconds)} sn kaldı";
    }

    static string Clip(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    static JobDto ToDto(TrackedJob job)
    {
        string state = MapState(job);
        string tone = ToneFor(state);
        var n = job.Nobet;
        int attempt = n?.Deneme ?? 0;
        string? resumeAt = state == "kota-bekliyor" ? n?.BekleUntil?.ToLocalTime().ToString("o", CultureInfo.InvariantCulture) : null;

        string parserNow = job.ClaudeState?.Now ?? job.CodexState?.Now ?? "";
        int steps = job.ClaudeState?.Steps ?? job.CodexState?.Steps ?? 0;
        var filesChanged = job.ClaudeState?.FilesChanged ?? job.CodexState?.FilesChanged ?? [];
        var denials = job.ClaudeState?.Denials ?? job.CodexState?.Denials ?? [];
        var safetyIds = job.ClaudeState?.SafetyIds ?? job.CodexState?.SafetyIds ?? [];
        string? resultText = job.ClaudeState?.ResultText ?? job.CodexState?.ResultText;
        string? parserFix = job.ClaudeState?.Fix ?? job.CodexState?.Fix;

        string stateText = state == "kota-bekliyor" && n?.BekleUntil is { } b
            ? $"Kota dolu — {b.ToLocalTime():HH:mm}'de kendiliğinden devam edecek" : StateText(state);
        string now = state == "kota-bekliyor" && n?.BekleUntil is { } b2
            ? FormatCountdown(b2.ToLocalTime() - DateTimeOffset.Now)
            : state switch
            {
                "yarida" => "Uygulama kapanınca yarıda kaldı",
                "giris" or "hata" or "kota" when n?.Not is { Length: > 0 } => n!.Not!,
                _ => parserNow.Length > 0 ? parserNow : (state is "basliyor" ? "Başlıyor" : ""),
            };
        string? fix = state switch
        {
            "giris" => n?.Not ?? (job.Tool == "codex" ? "Codex uygulamasında bir kez giriş yap." : "Claude komut satırında bir kez /login yap."),
            "kota" or "kota-bekliyor" => job.Tool == "codex" ? "Codex'in kullanım hakkı doldu; biraz sonra yeniden dene." : "Claude'un kullanım hakkı doldu; biraz sonra yeniden dene.",
            "hata" => n?.Not ?? parserFix,
            _ => parserFix,
        };

        DateTime end = job.Start;
        bool running = state is "basliyor" or "calisiyor" or "kota-bekliyor";
        DateTime last = n?.Guncel?.LocalDateTime ?? DateTime.Now;
        bool quiet = running && state != "kota-bekliyor" && (DateTime.Now - last) > TimeSpan.FromMinutes(10);
        long elapsedSec = (long)((running ? DateTime.Now : last) - job.Start).TotalSeconds;

        return new JobDto(job.Id, job.Tool, Clip(TitleOf(job), 80), Clip(job.Prompt, 1000), job.Role, job.Project,
            state, stateText, tone, job.Start.ToString("o", CultureInfo.InvariantCulture), running ? null : last.ToString("o", CultureInfo.InvariantCulture),
            Math.Max(0, elapsedSec), last.ToString("o", CultureInfo.InvariantCulture), now, steps,
            filesChanged.ToList(), denials.ToList(), safetyIds.ToList(), quiet, n?.ExitCode,
            resultText is null ? null : Clip(resultText, 300), fix, running,
            resumeAt, attempt);
    }

    static string TitleOf(TrackedJob job)
    {
        string t = job.Prompt;
        if (job.Project is { Length: > 0 } p)
        {
            int idx = t.LastIndexOf($" (Proje: {p})", StringComparison.Ordinal);
            if (idx >= 0) t = t[..idx];
        }
        return t;
    }
}
