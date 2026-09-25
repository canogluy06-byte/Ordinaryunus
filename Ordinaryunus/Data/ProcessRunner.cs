// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Diagnostics;
using System.Text;

namespace Ordinaryunus.Data;

public sealed record ProcessResult(int ExitCode, string Output, bool TimedOut);

/// <summary>Runs command-line tools without a shell (ArgumentList only).</summary>
public static class ProcessRunner
{
    /// <summary>Resolves an executable name (e.g. "git", "node") to a full path using PATH only. Only ".exe" is
    /// accepted for a bare name (never ".cmd"/".bat"): those run through cmd.exe, which would quietly turn the
    /// "no shell, ArgumentList only" guarantee the callers rely on into a real shell invocation.</summary>
    public static string? Resolve(string exe)
    {
        if (Path.IsPathRooted(exe)) return File.Exists(exe) ? exe : null;
        string[] exts = Path.HasExtension(exe) ? [""] : [".exe"];
        foreach (var dir0 in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            string dir = dir0.Trim().Trim('"');
            if (dir.Length == 0) continue;
            foreach (var ext in exts)
            {
                try
                {
                    string p = Path.Combine(dir, exe + ext);
                    if (File.Exists(p)) return p;
                }
                catch (ArgumentException) { }
            }
        }
        return null;
    }

    public static ProcessStartInfo StartInfo(string exe, IEnumerable<string> args, string? workingDir)
    {
        string path = Resolve(exe) ?? throw new FileNotFoundException($"'{exe}' bulunamadı (PATH).");
        var psi = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (workingDir is not null) psi.WorkingDirectory = workingDir;
        return psi;
    }

    public static ProcessResult Run(string exe, IEnumerable<string> args, string? workingDir, int timeoutMs)
    {
        try
        {
            using var p = new Process { StartInfo = StartInfo(exe, args, workingDir) };
            var sb = new StringBuilder();
            object gate = new();
            p.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (gate) sb.AppendLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (gate) sb.AppendLine(e.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                lock (gate) sb.AppendLine($"(Zaman aşımı: {timeoutMs / 1000} sn doldu, işlem durduruldu.)");
                return new ProcessResult(-1, sb.ToString(), true);
            }
            p.WaitForExit();
            lock (gate) return new ProcessResult(p.ExitCode, sb.ToString(), false);
        }
        catch (Exception ex)
        {
            return new ProcessResult(-1, ex.Message, false);
        }
    }

    public static Task<ProcessResult> RunAsync(string exe, IEnumerable<string> args, string? workingDir, int timeoutMs) =>
        Task.Run(() => Run(exe, args, workingDir, timeoutMs));
}
