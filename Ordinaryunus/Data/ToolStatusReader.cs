// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ordinaryunus.Data;

/// <summary>
/// Reads tool configuration (read-only) to show names and on/off state. Never reads or shows secret values:
/// only hook event names, agent file names, scheduled task names, MCP/plugin section names.
/// Never opens ~/.codex/auth.json or any .env file.
/// </summary>
public static partial class ToolStatusReader
{
    public static ToolStatus Read(string vaultRoot, bool gitOk, DateTime now)
    {
        string home = AppPaths.UserProfile;
        string claudeSettings = Path.Combine(home, ".claude", "settings.json");
        string codexConfig = Path.Combine(home, ".codex", "config.toml");

        return new ToolStatus
        {
            ClaudeSettingsFound = File.Exists(claudeSettings),
            ClaudeHooks = HookEvents(claudeSettings),
            ClaudeAgents = VaultReader.SafeFiles(Path.Combine(home, ".claude", "agents"), "*.md")
                .Select(Path.GetFileNameWithoutExtension).OfType<string>().ToList(),
            ScheduledTasks = ScheduledTasks(Path.Combine(home, ".claude", "scheduled-tasks"), now),
            CodexConfigFound = File.Exists(codexConfig),
            CodexMcpServers = CodexSections(codexConfig).mcp,
            CodexPlugins = CodexSections(codexConfig).plugins,
            CodexHooks = HookEvents(Path.Combine(home, ".codex", "hooks.json")),
            VaultOk = Directory.Exists(vaultRoot),
            GitOk = gitOk,
        };
    }

    static List<string> HookEvents(string jsonPath)
    {
        try
        {
            string? text = Md.ReadText(jsonPath);
            if (text is null) return [];
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (doc.RootElement.TryGetProperty("hooks", out var hooks) && hooks.ValueKind == JsonValueKind.Object)
                return hooks.EnumerateObject().Select(p => p.Name).ToList();
        }
        catch (Exception) { }
        return [];
    }

    static (List<string> mcp, List<(string, bool)> plugins) CodexSections(string tomlPath)
    {
        var mcp = new List<string>();
        var plugins = new List<(string, bool)>();
        string? text = Md.ReadText(tomlPath);
        if (text is null) return (mcp, plugins);
        string? currentPlugin = null;
        bool currentEnabled = true;
        void Flush()
        {
            if (currentPlugin is not null) plugins.Add((currentPlugin, currentEnabled));
            currentPlugin = null;
            currentEnabled = true;
        }
        foreach (var raw in Md.Lines(text))
        {
            string line = raw.Trim();
            if (line.StartsWith('['))
            {
                Flush();
                var m = McpHeader().Match(line);
                if (m.Success && !mcp.Contains(m.Groups[1].Value)) mcp.Add(m.Groups[1].Value);
                var p = PluginHeader().Match(line);
                if (p.Success) currentPlugin = p.Groups[1].Value;
                continue;
            }
            if (currentPlugin is not null)
            {
                var e = EnabledLine().Match(line);
                if (e.Success) currentEnabled = e.Groups[1].Value == "true";
            }
        }
        Flush();
        return (mcp, plugins);
    }

    [GeneratedRegex(@"^\[mcp_servers\.""?([^""\].]+)""?\]$")] private static partial Regex McpHeader();
    [GeneratedRegex(@"^\[plugins\.""([^""]+)""\]$")] private static partial Regex PluginHeader();
    [GeneratedRegex(@"^enabled\s*=\s*(true|false)")] private static partial Regex EnabledLine();

    // ---------- scheduled tasks ----------
    // Görevin takvimi yalnızca kendi SKILL.md dosyasının başındaki "zaman:" satırından okunur ("pazar 19:00",
    // "her gün 21:30", "cumartesi", "her saat :14"); satır yoksa Şirketim'de takvim boş kalır (KURULUM.md 3.7).

    /// <summary>Uygulamanın kendi kavramlarına ait görev adları için elle yazılmış Türkçe başlıklar (akşam analizi:
    /// Beyin'deki "derin analiz" kartı; tam-gaz: Tam Gaz anahtarını işleyen işçi). Başka her adın tireleri boşluğa
    /// çevrilir ve sadece ilk harfi büyütülür, böylece ham ad (slug) Şirketim sayfasına olduğu gibi düşmez.</summary>
    static readonly Dictionary<string, string> TrTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aksam-analizi"] = "Akşam analizi",
        ["tam-gaz"] = "Tam Gaz işçisi",
    };

    public static string TitleFor(string slug)
    {
        if (TrTitles.TryGetValue(slug, out var t)) return t;
        string s = slug.Replace('-', ' ').Replace('_', ' ').Trim();
        return s.Length == 0 ? slug : char.ToUpper(s[0], Md.Tr) + s[1..];
    }

    static List<ScheduledTaskInfo> ScheduledTasks(string dir, DateTime now)
    {
        var list = new List<ScheduledTaskInfo>();
        if (!Directory.Exists(dir)) return list;
        string[] subs;
        try { subs = Directory.GetDirectories(dir); } catch { return list; }
        foreach (var sub in subs.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            string skill = Path.Combine(sub, "SKILL.md");
            string? text = Md.ReadText(skill);
            if (text is null) continue;
            var fm = Frontmatter.Parse(text);
            string name = fm.Get("name", Path.GetFileName(sub));
            string sched = fm.Get("zaman", "");
            list.Add(new ScheduledTaskInfo(TitleFor(name), fm.Get("description"), NextRun(sched, now)));
        }
        return list;
    }

    static readonly string[] TrDays = ["pazar", "pazartesi", "salı", "çarşamba", "perşembe", "cuma", "cumartesi"];

    /// <summary>Turns "pazar 19:00" / "her gün 21:30" / "cumartesi" / "haftalık" into a short Turkish next-run text.</summary>
    public static string? NextRun(string sched, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(sched)) return null;
        string s = sched.Trim().ToLower(Md.Tr);
        var tm = Regex.Match(s, @"(\d{1,2}):(\d{2})");
        TimeSpan? time = tm.Success ? new TimeSpan(int.Parse(tm.Groups[1].Value), int.Parse(tm.Groups[2].Value), 0) : null;
        if (s.Contains("her saat"))
        {
            var mm = Regex.Match(s, @":(\d{2})");
            int minute = mm.Success ? int.Parse(mm.Groups[1].Value) : 0;
            var next = now.Date.AddHours(now.Hour).AddMinutes(minute);
            if (next <= now) next = next.AddHours(1);
            return "her saat (Tam Gaz açıkken) · sonraki " + next.ToString("HH:mm");
        }
        if (s.Contains("her gün"))
        {
            if (time is null) return "her gün";
            var next = now.Date + time.Value;
            if (next <= now) next = next.AddDays(1);
            return (next.Date == now.Date ? "bugün " : "yarın ") + next.ToString("HH:mm");
        }
        // longest day name first so "pazartesi" wins over "pazar"
        int dow = -1;
        foreach (var (d, i) in TrDays.Select((d, i) => (d, i)).OrderByDescending(x => x.d.Length))
            if (Regex.IsMatch(s, $@"(^|\s){d}($|\s)")) { dow = i; break; }
        if (dow < 0) return s; // e.g. "haftalık"
        int delta = ((dow - (int)now.DayOfWeek) + 7) % 7;
        var day = now.Date.AddDays(delta);
        if (delta == 0 && time is not null && now.TimeOfDay >= time.Value) day = day.AddDays(7);
        string when = day == now.Date ? "bugün" : day == now.Date.AddDays(1) ? "yarın" : day.ToString("d MMM ddd", Md.Tr);
        return time is null ? when : $"{when} {time.Value:hh\\:mm}";
    }
}
