// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Diagnostics;

namespace Ordinaryunus.Data;

/// <summary>
/// Emergency-stop process sweep only (EK-v2.1 §1-2 moved per-job starting to <see cref="Jobs.NobetciRunner"/> +
/// <see cref="Jobs.JobStore"/>: both Claude and Codex jobs now run detached through the vault's own job watchman,
/// <c>_sistem/araclar/is-nobetcisi.mjs</c>, not through a Process this app holds). What is left here is the
/// belt-and-suspenders hard kill for Acil Durdur: on top of the DURDUR flag (which every watchman polls every 2 s
/// and the safety-gate hook checks before any new tool call) and JobStore.StopAll()'s per-job ".iptal" signal, this
/// finds and force-kills any matching node/codex process by command line — including one the Tam Gaz scheduled
/// worker started independently of this app, or one whose watchman somehow missed the flag.
/// </summary>
public static class CodexRunner
{
    /// <summary>
    /// True for processes the emergency stop may kill: node.exe running the job watchman (is-nobetcisi.mjs, either
    /// tool) or its retired predecessor (codex-nobet.mjs / codex.mjs), codex.exe running " exec ". Never the Codex
    /// desktop app (WindowsApps package) or anything else.
    /// </summary>
    public static bool ShouldStop(string processName, string? commandLine)
    {
        if (string.IsNullOrEmpty(commandLine)) return false;
        string name = processName.ToLowerInvariant();
        string cmd = commandLine.ToLowerInvariant();
        if (cmd.Contains(@"\windowsapps\")) return false;
        if (name == "node.exe") return cmd.Contains("is-nobetcisi") || cmd.Contains("codex-nobet") || cmd.Contains("codex.mjs");
        if (name == "codex.exe") return (" " + cmd + " ").Contains(" exec ");
        return false;
    }

    public sealed record StopResult(int Stopped, List<string> Errors);

    /// <summary>Hard-kill sweep: node/codex processes matching <see cref="ShouldStop"/>, found via CIM
    /// (Win32_Process). Ordinaryunus itself no longer holds a Process handle to any job (they run detached), so
    /// <see cref="Jobs.JobStore.StopAll"/> (graceful ".iptal" signal) always runs alongside this, not instead of it.</summary>
    public static StopResult EmergencyStop(bool dryRun = false)
    {
        int stopped = 0;
        var errors = new List<string>();
        foreach (var (pid, name, cmd) in ListCandidates())
        {
            if (pid == Environment.ProcessId || !ShouldStop(name, cmd)) continue;
            if (dryRun) { stopped++; continue; }
            try { using var p = Process.GetProcessById(pid); p.Kill(entireProcessTree: true); stopped++; }
            catch (ArgumentException) { /* already gone */ }
            catch (Exception ex) { errors.Add($"{name} ({pid}): {ex.Message}"); }
        }
        return new StopResult(stopped, errors);
    }

    /// <summary>Lists node.exe / codex.exe processes with command lines via PowerShell Get-CimInstance (read-only).</summary>
    public static List<(int pid, string name, string cmd)> ListCandidates()
    {
        var list = new List<(int, string, string)>();
        const string script =
            "Get-CimInstance Win32_Process -Filter \"Name='node.exe' OR Name='codex.exe'\" | " +
            "ForEach-Object { [string]$_.ProcessId + [char]9 + $_.Name + [char]9 + $_.CommandLine }";
        var r = ProcessRunner.Run("powershell", ["-NoProfile", "-NonInteractive", "-Command", script], null, 20000);
        foreach (var line in Md.Lines(r.Output))
        {
            var parts = line.Split('\t', 3);
            if (parts.Length == 3 && int.TryParse(parts[0], out int pid)) list.Add((pid, parts[1], parts[2]));
        }
        return list;
    }
}
