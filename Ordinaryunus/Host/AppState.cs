// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using Ordinaryunus.Bridge;
using Ordinaryunus.Brain;
using Ordinaryunus.Data;
using Ordinaryunus.Jobs;
using Ordinaryunus.Security;
using TreeNode = Ordinaryunus.Bridge.TreeNode;

namespace Ordinaryunus.Host;

/// <summary>
/// Everything the bridge answers from (MIMARI §2.2, §5.6): settings, unlock state, the current VaultSnapshot,
/// the brain index and its derived state (problems, Kayıt entries), lights, usage, and the "current row keys"
/// registry (td-/ap-) a toggle/approve request is checked against. Pure C#: no WinForms dependency, so it also
/// runs headless under --selftest / --dump-json / --screenshots.
/// </summary>
public sealed class AppState
{
    public AppSettings Settings { get; set; }
    public bool Unlocked { get; set; }
    public bool DryRun { get; init; }
    public bool ScreenshotMode { get; init; }
    public DateTime LastActivity { get; set; } = DateTime.Now;
    public bool FocusRunning { get; set; }
    /// <summary>last time JS confirmed "odak modu still running". JS is
    /// supposed to send this every 30 s while a focus countdown runs, but if it ever stops (a bug, the tab losing
    /// focus, a crash in that overlay…) <see cref="FocusRunning"/> would otherwise stay stuck true forever and the
    /// idle auto-lock would never run again. The Host layer clears FocusRunning once this goes stale (see
    /// WebHostForm.CheckIdle) instead of trusting the flag blindly.</summary>
    public DateTime? FocusHeartbeatAt { get; set; }
    public string LockReason { get; set; } = "start";

    // UI-thread bridges the Host wires up (WebView2/Clipboard/dialogs need the STA UI thread).
    public Action<string>? ClipboardSetText { get; set; }
    public Func<string?>? PickVaultFolder { get; set; }
    public Action<double>? ApplyZoom { get; set; }
    public Action? OnLockRequested { get; set; }
    public Action<string?, string?>? OnPageReady { get; set; }
    /// <summary>native Windows onay kutusu before a vaultPath change actually
    /// takes effect. Null in headless modes (selftest/dry-run/screenshots have no Form) — SetSettings then
    /// auto-confirms, matching how PickVaultFolder/ClipboardSetText already behave there.</summary>
    public Func<string, bool>? ConfirmVaultChange { get; set; }

    public string Vault => Settings.KasaYolu;

    public VaultSnapshot Snapshot { get; private set; }
    public BrainIndex? Index { get; private set; }
    public GitActivity Git { get; private set; } = new() { Available = false };
    public List<ProblemDto> Problems { get; private set; } = [];
    public Dictionary<string, List<KayitEntry>> KayitByProject { get; private set; } = [];
    public List<LightDto> Lights { get; private set; } = [];
    public UsageDto? Usage { get; private set; }
    public long Stamp { get; private set; }
    public List<TreeNode>? TreeCache { get; private set; }

    public Dictionary<string, TodoItem> TodoKeys { get; private set; } = [];
    public Dictionary<string, JobRow> AppRowKeys { get; private set; } = [];

    public event Action? SnapshotChanged;
    public event Action? BrainChanged;

    bool _reloading;
    bool _reloadQueued;
    readonly object _reloadGate = new();

    public AppState(AppSettings settings, bool dryRun, bool screenshotMode)
    {
        Settings = settings;
        DryRun = dryRun;
        ScreenshotMode = screenshotMode;
        Snapshot = new VaultSnapshot { Root = settings.KasaYolu, VaultExists = false };
        JobStore.DryRun = dryRun;
        JobStore.EnsureWired();
    }

    public Task ReloadAsync(bool full)
    {
        lock (_reloadGate)
        {
            if (_reloading) { _reloadQueued = true; return Task.CompletedTask; }
            _reloading = true;
        }
        return Task.Run(() =>
        {
            try { DoReload(full); }
            finally
            {
                bool again;
                lock (_reloadGate) { _reloading = false; again = _reloadQueued; _reloadQueued = false; }
                if (again) _ = ReloadAsync(true);
            }
        });
    }

    void DoReload(bool full)
    {
        var now = DateTime.Now;
        var snap = VaultSnapshot.Load(Vault);
        if (Index is null || !string.Equals(Index.Root, Vault, StringComparison.OrdinalIgnoreCase)) Index = new BrainIndex(Vault);
        Index.Update();
        Git = snap.VaultExists ? GitActivity.Load(Vault) : new GitActivity { Available = false };
        var kayitByProject = BuildKayitByProject(Index);
        var jobs = JobStore.GetAll();
        var lights = Mapper.MapLights(snap, jobs, now);
        var problems = ProblemDetector.Run(snap, Index, jobs, lights, kayitByProject, now);

        Snapshot = snap;
        KayitByProject = kayitByProject;
        Problems = problems;
        Lights = lights;
        TreeCache = null;
        RebuildKeys(snap);
        Stamp++;
        SnapshotChanged?.Invoke();
        BrainChanged?.Invoke();
    }

    /// <summary>Screenshot mode only: after "giris.png" is captured, C# unlocks internally — no password (§9.3).</summary>
    public void ForceUnlockForScreenshots()
    {
        if (ScreenshotMode) Unlocked = true;
    }

    public void RefreshLightsOnly(DateTime now)
    {
        Lights = Mapper.MapLights(Snapshot, JobStore.GetAll(), now);
    }

    public void RefreshUsage(TimeSpan budget)
    {
        var u = SystemMonitor.EstimateUsage(DateTime.Now, budget);
        Usage = Mapper.MapUsage(u);
    }

    static Dictionary<string, List<KayitEntry>> BuildKayitByProject(BrainIndex index)
    {
        var result = new Dictionary<string, List<KayitEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in index.Notes.Values.Where(n => n.Kind == "kayit" && n.Project is not null))
        {
            var entries = KayitParser.Parse(n.RawText, n.RelPath, n.Project);
            result[n.Project!] = entries;
        }
        return result;
    }

    void RebuildKeys(VaultSnapshot snap)
    {
        var todoKeys = new Dictionary<string, TodoItem>(StringComparer.Ordinal);
        foreach (var t in snap.Todos.Items) todoKeys[Mapper.RowKey("td", t.LineIndex, t.RawLine)] = t;
        TodoKeys = todoKeys;
        var rowKeys = new Dictionary<string, JobRow>(StringComparer.Ordinal);
        foreach (var r in snap.Jobs.AllRows) rowKeys[Mapper.RowKey("ap", r.LineIndex, r.RawLine)] = r;
        AppRowKeys = rowKeys;
    }

    public List<TreeNode> GetTree()
    {
        if (TreeCache is not null) return TreeCache;
        if (Index is null) return [];
        var health = Problems.GroupBy(p => p.Project).Where(g => g.Key is not null)
            .ToDictionary(g => g.Key!, g => ProblemDetector.ComputeHealth(g).level);
        TreeCache = TreeBuilder.Build(Index, Snapshot, KayitByProject, health);
        return TreeCache;
    }
}
