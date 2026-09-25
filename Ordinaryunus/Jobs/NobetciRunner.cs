// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Diagnostics;
using System.Text;
using Ordinaryunus.Data;

namespace Ordinaryunus.Jobs;

public sealed class NodeNotFoundException() : InvalidOperationException("Node.js (node.exe) bulunamadı; PATH'e ekleyip tekrar dene.");

/// <summary>Kasada iş nöbetçisi betiği yok (kurulum eksik). Mesajı doğrudan kullanıcıya gösterilir.</summary>
public sealed class NobetciNotFoundException() : InvalidOperationException(
    "Bu kasada iş nöbetçisi yok (" + NobetciRunner.ScriptRel + "). Kurulum belgesindeki adımlarla betiği kasana kopyala.");

/// <summary>
/// Starts a Claude/Codex job as an ARM'S-LENGTH process (EK-v2.1 §1-2): hands the composed prompt to the vault's own
/// job watchman (<c>_sistem/araclar/is-nobetcisi.mjs</c>) over a temp file and lets it run detached — no stdout/
/// stderr redirection, no association with this app's process tree, so the job keeps running (including through a
/// quota wait, resumed with <c>--resume</c>/<c>resume</c> on the SAME session) even if Ordinaryunus is closed.
/// <see cref="JobStore"/> is the one that reads the watchman's own <c>&lt;damga&gt;.jsonl</c>/<c>.nobet.json</c>
/// files back; this class only starts jobs and posts the three file signals the watchman listens for
/// (<c>.iptal</c> cancel, <c>.simdi</c> "retry now", and — for a full stop — one <c>.iptal</c> per tracked job).
/// Nöbetçi betiğinin kendisi burada asla değiştirilmez (kasanın kendi dosyasıdır).
/// </summary>
public static class NobetciRunner
{
    /// <summary>İş nöbetçisinin kasaya göre yolu ("/" ayraçlı).</summary>
    public const string ScriptRel = "_sistem/araclar/is-nobetcisi.mjs";

    /// <summary>Set only by --dry-run: passes <c>--sahte is-nobetcisi-sahte.mjs</c> so no real quota is spent and
    /// the whole kota → kota-bekliyor → Şimdi dene → bitti flow can be clicked through safely (§9.5, EK §4).</summary>
    public static bool DryRun { get; set; }

    public sealed record LaunchResult(string Tool, string Damga, string JobsDir, DateTime Start);

    /// <summary>Builds the watchman's argument list (pure — unit-tested). <paramref name="jobsDir"/> and
    /// <paramref name="istemPath"/> are always passed explicitly (never the watchman's own %APPDATA% default) so
    /// --selftest/--dry-run/--screenshots, which redirect ORDINARYUNUS_DATA to a temp folder, never touch the real
    /// job folders.</summary>
    public static List<string> BuildArguments(string scriptPath, string tool, string damga, string istemPath, string jobsDir,
        string? addDir, bool web, string? sahte, int? paySeconds)
    {
        var args = new List<string> { scriptPath, "--arac", tool, "--is", damga, "--istem-dosyasi", istemPath, "--klasor", jobsDir };
        if (tool == "claude" && addDir is not null) { args.Add("--ekdizin"); args.Add(addDir); }
        if (tool == "claude" && web) args.Add("--web");
        if (sahte is not null) { args.Add("--sahte"); args.Add(sahte); }
        if (paySeconds is { } ps) { args.Add("--pay-sn"); args.Add(ps.ToString()); }
        return args;
    }

    /// <summary>Starts one job. Returns immediately (does not wait for the watchman); the caller
    /// (<see cref="JobStore.StartClaude"/>/<see cref="JobStore.StartCodex"/>) tracks it from that point on by
    /// polling its files. Throws for every "cannot even try" case (readonly, stopped, limit, no_cli, node missing).</summary>
    public static LaunchResult Start(string vault, string tool, string composedPrompt, string? addDir, bool web)
    {
        if (VaultWriter.ReadOnlyMode && !DryRun) throw new InvalidOperationException("Salt okunur modda iş başlatılmaz.");
        if (SafetyQueue.IsStopped(vault)) throw new InvalidOperationException("Acil durdurma açık. Önce \"Devam Et\"e bas.");
        if (tool == "claude")
        {
            if (JobStore.CountRunning("claude") >= 2) throw new InvalidOperationException("En fazla 2 Claude işi aynı anda çalışabilir.");
            if (!DryRun && ClaudeCli.Find() is null) throw new ClaudeCliNotFoundException();
        }

        string? nodeExe = ProcessRunner.Resolve("node");
        if (nodeExe is null) throw new NodeNotFoundException();
        string scriptPath = Path.Combine(vault, "_sistem", "araclar", "is-nobetcisi.mjs");
        if (!File.Exists(scriptPath)) throw new NobetciNotFoundException();
        // kasada kontrol edilmemiş bir değişiklik taşıyan nöbetçi çalıştırılmaz.
        // Git'li kasada commit edilmemiş değişiklik reddedilir; git'siz kasada içerik özeti değiştiyse kullanıcıya
        // sorulur (bkz. VaultGitGuard). Git'siz kasa bu yüzden tamamen kilitlenmez.
        VaultGitGuard.EnsureSafeToRun(vault, ScriptRel);

        string jobsDir = tool == "claude" ? AppPaths.ClaudeJobsDir : AppPaths.CodexJobsDir;
        Directory.CreateDirectory(jobsDir);
        string damga = NewDamga();
        string finalPrompt = ClaudeCli.BuildFinalPrompt(composedPrompt);
        string istemPath = Path.Combine(jobsDir, damga + ".istem.txt");
        File.WriteAllText(istemPath, finalPrompt, new UTF8Encoding(false));

        string? sahte = null; int? paySeconds = null;
        if (DryRun)
        {
            sahte = Path.Combine(vault, "_sistem", "araclar", "is-nobetcisi-sahte.mjs");
            paySeconds = 5; // shortens the kota-bekliyor demo wait from the watchman's 120 s default to a few seconds
        }
        var args = BuildArguments(scriptPath, tool, damga, istemPath, jobsDir, addDir, web, sahte, paySeconds);

        var psi = new ProcessStartInfo(nodeExe) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = vault };
        foreach (var a in args) psi.ArgumentList.Add(a);
        // No redirection (EK §2.1: "stdout/stderr yönlendirilMEZ, nöbetçi kendi dosyasına yazar") and no job-object
        // link: this Process handle is discarded right after Start, so closing Ordinaryunus never touches the child.
        var proc = new Process { StartInfo = psi };
        try { proc.Start(); }
        catch (Exception ex) { throw new InvalidOperationException("İş nöbetçisi başlatılamadı: " + ex.Message, ex); }
        finally { try { proc.Dispose(); } catch { } }

        return new LaunchResult(tool, damga, jobsDir, DateTime.Now);
    }

    static string NewDamga() => DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..8];

    /// <summary>"İptal" (EK §2 item 4): the watchman notices within ≤4 s, stops the tool and exits. Never kills any
    /// process directly — the watchman owns that (it can also wait out an unkillable child, which a hard kill
    /// from here could orphan).</summary>
    public static void SignalCancel(string jobsDir, string damga)
    {
        try { File.WriteAllText(Path.Combine(jobsDir, damga + ".iptal"), DateTime.Now.ToString("o")); } catch { }
    }

    /// <summary>"Şimdi dene" (EK §2 item 3): must be a NEW value on every click, or the watchman (which only reacts
    /// to the file's content changing, so a click during an already-running wait is never missed) would ignore a
    /// second click that wrote the same bytes as the first.</summary>
    public static void SignalRetryNow(string jobsDir, string damga)
    {
        try { File.WriteAllText(Path.Combine(jobsDir, damga + ".simdi"), DateTime.Now.Ticks.ToString()); } catch { }
    }
}
