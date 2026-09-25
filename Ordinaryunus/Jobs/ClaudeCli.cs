// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
namespace Ordinaryunus.Jobs;

// Version: yedek yerde (PATH, .local\bin) bulunan exe'nin sürümü bilinmeyebilir; o zaman null.
public sealed record ClaudeInstall(string Exe, Version? Version);

public sealed class ClaudeCliNotFoundException() : InvalidOperationException("Claude komut satırı bulunamadı. Claude uygulamasını bir kez açıp güncelle.");

/// <summary>Discovers the bundled Claude Code CLI. Since EK-v2.1 §1-2, C# no longer invokes claude.exe/codex
/// directly: <see cref="Jobs.NobetciRunner"/> hands the job to the vault's own
/// <c>_sistem/araclar/is-nobetcisi.mjs</c>, which builds its own argument list (and its own copy of
/// <c>--allowedTools</c>) and runs the tool as a detached process. <see cref="Find"/> is still used for the
/// synchronous "no_cli" check and the "/login" hint text; <see cref="BuildArguments"/>/<see cref="AllowedTools"/>
/// stay only as the reference copy selftest checks against the watchman's own list.</summary>
public static class ClaudeCli
{
    /// <summary>the list used to include "Bash(git …:*)",
    /// "Bash(node _sistem/araclar/*:*)", "Bash(dotnet build/test:*)" — real shell access, which opened side doors
    /// around the safety gate (a Bash-run "git diff --output=…" or "node …" can read/write almost anything). No
    /// Bash entry is allowed at all now. WebFetch — which can carry vault content out via a hidden instruction on a
    /// fetched page — is only added when the person giving the work explicitly ticks "İnternetten sayfa da
    /// okuyabilsin" (payload <c>web:boolean</c>, SÖZLEŞME EKİ). Keep this in sync by hand with the identical
    /// constant in <c>is-nobetcisi.mjs</c> (<c>CLAUDE_IZINLI</c>) — that copy is the one actually used at runtime.</summary>
    public const string AllowedToolsBase = "Read,Grep,Glob,Edit,Write,TodoWrite,WebSearch";

    /// <summary>Kept for source compatibility with existing selftest calls that don't pass <c>web</c> (equivalent
    /// to <c>AllowedToolsFor(false)</c>).</summary>
    public const string AllowedTools = AllowedToolsBase;

    public static string AllowedToolsFor(bool web) => web ? AllowedToolsBase + ",WebFetch" : AllowedToolsBase;

    /// <summary>The exact suffix every job prompt ends with (SPEC "Giving work"): reused by both tools.</summary>
    public const string AgentsSuffix = " Follow AGENTS.md (lock, handoff, never commit).";

    public static string DefaultBaseDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "claude-code");

    /// <summary>Claude komut satırını iş nöbetçisindeki <c>claudeBul()</c> ile aynı sırayla arar (biri değişirse öbürü de
    /// değişmeli): 1) Claude masaüstü uygulamasının kurduğu <c>&lt;baseDir&gt;\&lt;sürüm&gt;\claude.exe</c> (System.Version
    /// olarak en yüksek sürüm), 2) yerel kurucunun koyduğu <c>%USERPROFILE%\.local\bin\claude.exe</c>, 3) PATH'teki ilk
    /// <c>claude.exe</c>. Sadece .exe kabul edilir (.cmd/.ps1 kabuksuz başlatılamaz). <paramref name="baseDir"/> açıkça
    /// verilirse (öz-test) yalnızca o klasöre bakılır. Hiçbiri yoksa null (no_cli).</summary>
    public static ClaudeInstall? Find(string? baseDir = null) =>
        baseDir is not null ? FindInVersionDir(baseDir) : Find(DefaultBaseDir, FallbackCandidates());

    /// <summary>Öz-test için: sürüm klasörü, sonra verilen yedek yollar (sırayla).</summary>
    public static ClaudeInstall? Find(string baseDir, IEnumerable<string> fallbacks)
    {
        var best = FindInVersionDir(baseDir);
        if (best is not null) return best;
        foreach (var exe in fallbacks)
        {
            try { if (File.Exists(exe)) return new ClaudeInstall(exe, FileVersionOf(exe)); }
            catch (Exception) { /* geçersiz PATH parçası: sıradakine geç */ }
        }
        return null;
    }

    static ClaudeInstall? FindInVersionDir(string dir)
    {
        if (!Directory.Exists(dir)) return null;
        ClaudeInstall? best = null;
        foreach (var sub in Directory.EnumerateDirectories(dir))
        {
            if (!Version.TryParse(Path.GetFileName(sub), out var v)) continue;
            string exe = Path.Combine(sub, "claude.exe");
            if (!File.Exists(exe)) continue;
            if (best?.Version is null || v > best.Version) best = new ClaudeInstall(exe, v);
        }
        return best;
    }

    static IEnumerable<string> FallbackCandidates()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude.exe");
        foreach (var part in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            string dir = part.Trim().Trim('"');
            if (dir.Length == 0 || !Path.IsPathFullyQualified(dir)) continue;
            yield return Path.Combine(dir, "claude.exe");
        }
    }

    /// <summary>Yedek yerde bulunan exe'nin sürümü (klasör adında sürüm yok): dosya sürüm bilgisinden, yoksa null.</summary>
    static Version? FileVersionOf(string exe)
    {
        try
        {
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);
            string raw = info.ProductVersion ?? info.FileVersion ?? "";
            var m = System.Text.RegularExpressions.Regex.Match(raw, @"^\d+(\.\d+){1,3}");
            return m.Success && Version.TryParse(m.Value, out var v) ? v : null;
        }
        catch (Exception) { return null; }
    }

    public static string ComposePrompt(string text, string? role, string? project)
    {
        string body = string.IsNullOrWhiteSpace(role) ? text : $"{role}: {text}";
        if (!string.IsNullOrWhiteSpace(project)) body += $" (Proje: {project})";
        return body;
    }

    /// <summary>Applies the prompt rules (MIMARI §6.2) and appends the AGENTS.md suffix, in that order — the
    /// TOTAL length (composed text + suffix) is capped at 4000 chars, not just the composed text: the watchman's
    /// own istem dosyası limit (<c>_sistem/araclar/is-nobetcisi.mjs</c>, ISTEM_SINIRI = 4000) counts the suffix too,
    /// and refuses (kullanımHatası, exit 2, job never starts) a prompt that only fits without it.</summary>
    public static string BuildFinalPrompt(string composed)
    {
        string p = composed.Replace("\0", "").Replace("\r\n", "\n").Replace('\r', '\n');
        int room = Math.Max(0, 4000 - AgentsSuffix.Length);
        if (p.Length > room) p = p[..room];
        if (p.StartsWith('-')) p = "Görev: " + p;
        string result = p + AgentsSuffix;
        if (result.Length > 4000) result = result[..Math.Max(0, 4000 - AgentsSuffix.Length)] + AgentsSuffix;
        return result;
    }

    /// <summary>Partial reference copy (MIMARI §6.2) — selftest-only, see the type-level remark.
    /// This does NOT include <c>--tools</c>, <c>--disallowedTools</c> or
    /// <c>--strict-mcp-config</c> any more — those now live only in the watchman's own arg builder
    /// (<c>is-nobetcisi.mjs</c>, <c>CLAUDE_IZINLI</c>/<c>WEB</c>) and are checked directly against ITS source in
    /// <see cref="SelfTestV2"/> instead of being duplicated (and silently drifting) here. Do not read this list as
    /// "the shell/MCP guard is enforced by these arguments" — it is enforced by the two flags this function omits.
    /// addDir is the chosen project's folder, or null.</summary>
    public static List<string> BuildArguments(string composedPrompt, string? addDir, bool web = false)
    {
        var args = new List<string> { "-p", BuildFinalPrompt(composedPrompt), "--output-format", "stream-json", "--verbose",
            "--permission-mode", "acceptEdits", "--permission-prompts", "none" };
        if (addDir is not null) { args.Add("--add-dir"); args.Add(addDir); }
        args.Add("--allowedTools");
        args.Add(AllowedToolsFor(web));
        return args;
    }

    /// <summary>Child environment for a headless job (D6): inherited minus the paid-API/nested-session variables.</summary>
    public static Dictionary<string, string?> BuildEnvironment(System.Collections.IDictionary parentEnv)
    {
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry e in parentEnv) env[(string)e.Key] = e.Value as string;
        env.Remove("ANTHROPIC_API_KEY");
        env.Remove("ANTHROPIC_AUTH_TOKEN");
        env.Remove("CLAUDECODE");
        return env;
    }

    public static string LoginCommand(string exe) => $"& \"{exe}\"";
}
