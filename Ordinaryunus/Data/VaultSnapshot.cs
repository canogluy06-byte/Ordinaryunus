// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
namespace Ordinaryunus.Data;

/// <summary>Everything the UI shows, parsed once per refresh (on a background thread).</summary>
public sealed class VaultSnapshot
{
    public required string Root { get; init; }
    public DateTime LoadedAt { get; init; } = DateTime.Now;
    public DateOnly Today => DateOnly.FromDateTime(LoadedAt);
    public bool VaultExists { get; init; }

    public TodoList Todos { get; init; } = new();
    public List<ProjectCard> Projects { get; init; } = [];
    public List<TaskItem> Tasks { get; init; } = [];
    public List<RoleCard> Roles { get; init; } = [];
    public List<LedgerEntry> Ledger { get; init; } = [];
    public LinkedInInfo LinkedIn { get; init; } = new();
    public JobsInfo Jobs { get; init; } = new();
    public GitInfo Git { get; init; } = new();
    public IncomeInfo Income { get; init; } = new();
    public AnalysisNote Analysis { get; init; } = new();
    public ToolStatus Tools { get; init; } = new();
    public List<SafetyRequest> SafetyPending { get; init; } = [];
    public bool Stopped { get; init; }
    public List<AppJournal.Entry> Journal { get; init; } = [];
    public TamGazState TamGaz { get; init; } = new(false, null, null, 0);
    public CodexWatch Watch { get; init; } = new(false, "", false, null);
    public List<string> TamGazLog { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    /// <summary>Claude Code sessions active in the last 15 minutes (v2, MIMARI §1.3), for Şirketim + problem P09.</summary>
    public List<ClaudeSessionRow> ClaudeSessions { get; init; } = [];

    public List<ProjectCard> Active => Projects.Where(p => p.IsActive).OrderByDescending(p => p.Odak).ThenBy(p => p.Name, StringComparer.Create(Md.Tr, true)).ToList();
    public ProjectCard? Focus => Projects.FirstOrDefault(p => p.Odak && p.IsActive) ?? Projects.FirstOrDefault(p => p.Odak);
    public int ActiveCount => Projects.Count(p => p.IsActive);
    public List<ProjectCard> DecisionPending => Projects.Where(p => p.KararBekliyor && p.Durum is not ("bitti" or "donduruldu")).ToList();
    public int ApprovalCount => SafetyPending.Count + Jobs.AwaitingCount + LinkedIn.Drafts.Count + DecisionPending.Count;

    public static VaultSnapshot Load(string root, bool includeTools = true)
    {
        var reader = new VaultReader(root);
        bool exists = Directory.Exists(root);
        if (!exists)
        {
            return new VaultSnapshot
            {
                Root = root, VaultExists = false,
                Warnings = [$"Kasa klasörü bulunamadı: {root}. Ayarlar'dan doğru klasörü seç."],
                Journal = AppJournal.Load(),
            };
        }
        var git = reader.ReadGit();
        var snap = new VaultSnapshot
        {
            Root = root,
            VaultExists = true,
            Todos = reader.ReadTodos(),
            Projects = reader.ReadProjects(),
            Tasks = reader.ReadTasks(),
            Roles = reader.ReadRoles(),
            Ledger = reader.ReadLedger(),
            LinkedIn = reader.ReadLinkedIn(),
            Jobs = reader.ReadJobs(),
            Git = git,
            Income = reader.ReadIncome(),
            Analysis = reader.ReadAnalysis(),
            Tools = includeTools ? ToolStatusReader.Read(root, git.Available, DateTime.Now) : new ToolStatus(),
            SafetyPending = SafetyQueue.ReadPending(root, reader.Warnings),
            Stopped = SafetyQueue.IsStopped(root),
            Journal = AppJournal.Load(),
            TamGaz = Ordinaryunus.Data.TamGaz.Read(root, DateTimeOffset.Now),
            Watch = Ordinaryunus.Data.TamGaz.ReadWatch(root, DateTime.Now),
            TamGazLog = Ordinaryunus.Data.TamGaz.RecentLog(root),
            Warnings = reader.Warnings,
            ClaudeSessions = ClaudeSessionReader.Recent(DateTime.Now, TimeSpan.FromMinutes(15)),
        };
        if (!snap.Todos.Found) snap.Warnings.Add("01 Şimdi.md içinde \"Yapılacaklarım\" bölümü bulunamadı");
        // a red flag when the gate's kararlar.jsonl contains an "onay" this app
        // never made itself (see OwnDecisionLog for why this is only a detection backstop, not full prevention).
        var foreignApprovals = OwnDecisionLog.FindForeignApprovals(root);
        if (foreignApprovals.Count > 0)
            snap.Warnings.Add("Senin vermediğin bir onay bulundu. Güvenlik kapısı kayıtlarını (kararlar.jsonl) kontrol et.");
        return snap;
    }

    /// <summary>Consecutive days (ending today or yesterday) with a [claude]/[codex] commit or an app check-off.</summary>
    public int Streak()
    {
        var days = new HashSet<DateOnly>();
        foreach (var c in Git.Recent) if (c.Tool is not null) days.Add(DateOnly.FromDateTime(c.When.LocalDateTime));
        foreach (var j in Journal) days.Add(DateOnly.FromDateTime(j.Zaman));
        return Insights.StreakFrom(days, Today);
    }
}
