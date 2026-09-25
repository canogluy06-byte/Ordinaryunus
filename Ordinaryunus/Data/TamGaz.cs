// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Ordinaryunus.Data;

public sealed record TamGazState(bool On, DateTimeOffset? Start, DateTimeOffset? End, int Hours);

public sealed record CodexWatch(bool Running, string Task, bool WaitingQuota, string? LastResult);

/// <summary>
/// "Tam Gaz" (full-throttle) switch: &lt;vault&gt;\_sistem\tamgaz\ACIK. The app only creates/deletes ACIK;
/// codex-nobet.json, codex-*.log and gunluk.md are read-only here (written by the scheduled worker).
/// </summary>
public static class TamGaz
{
    public static string Dir(string vault) => Path.Combine(vault, "_sistem", "tamgaz");
    public static string SwitchFile(string vault) => Path.Combine(Dir(vault), "ACIK");

    public static TamGazState Read(string vault, DateTimeOffset now) => Parse(Md.ReadText(SwitchFile(vault)), now);

    public static TamGazState Parse(string? json, DateTimeOffset now)
    {
        if (json is null) return new TamGazState(false, null, null, 0);
        try
        {
            using var d = JsonDocument.Parse(json);
            var r = d.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return new TamGazState(false, null, null, 0);
            DateTimeOffset? Date(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(v.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t : null;
            var start = Date("baslangic");
            var end = Date("bitis");
            int hours = r.TryGetProperty("saat", out var s) && s.ValueKind == JsonValueKind.Number && s.TryGetInt32(out var h) ? h : 0;
            bool on = hours is 2 or 5 or 10 or 12 && end is { } e && e > now; // invalid or expired file = OFF
            return new TamGazState(on, start, end, hours);
        }
        catch (JsonException) { return new TamGazState(false, null, null, 0); }
    }

    public static string SwitchJson(DateTimeOffset now, int hours) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["baslangic"] = now.ToString("o", CultureInfo.InvariantCulture),
            ["bitis"] = now.AddHours(hours).ToString("o", CultureInfo.InvariantCulture),
            ["saat"] = hours,
        });

    public static void TurnOn(string vault, int hours)
    {
        if (VaultWriter.ReadOnlyMode) throw new InvalidOperationException("Salt okunur mod");
        if (hours is not (2 or 5 or 10 or 12)) throw new ArgumentOutOfRangeException(nameof(hours));
        Directory.CreateDirectory(Dir(vault));
        File.WriteAllText(SwitchFile(vault), SwitchJson(DateTimeOffset.Now, hours));
    }

    public static void TurnOff(string vault)
    {
        if (VaultWriter.ReadOnlyMode) throw new InvalidOperationException("Salt okunur mod");
        if (File.Exists(SwitchFile(vault))) File.Delete(SwitchFile(vault));
    }

    public static CodexWatch ReadWatch(string vault, DateTime now)
    {
        string? json = Md.ReadText(Path.Combine(Dir(vault), "codex-nobet.json"));
        string log = Path.Combine(Dir(vault), $"codex-{now:yyyy-MM-dd}.log");
        bool quota = false;
        try
        {
            if (File.Exists(log))
            {
                using var fs = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                fs.Seek(Math.Max(0, fs.Length - 4096), SeekOrigin.Begin);
                using var sr = new StreamReader(fs);
                quota = sr.ReadToEnd().Contains("usage limit", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { }
        return ParseWatch(json, quota, IsAlive);
    }

    public static CodexWatch ParseWatch(string? json, bool quotaInLog, Func<int, bool> alive)
    {
        if (json is null) return new CodexWatch(false, "", quotaInLog, null);
        try
        {
            using var d = JsonDocument.Parse(json);
            var r = d.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return new CodexWatch(false, "", quotaInLog, null);
            int pid = r.TryGetProperty("pid", out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var number) ? number : 0;
            string task = r.TryGetProperty("gorev", out var g) && g.ValueKind == JsonValueKind.String ? Path.GetFileNameWithoutExtension(g.GetString() ?? "") : "";
            string? result = r.TryGetProperty("sonuc", out var s) ? s.ToString() : null;
            return new CodexWatch(pid > 0 && alive(pid), task, quotaInLog, result);
        }
        catch (JsonException) { return new CodexWatch(false, "", quotaInLog, null); }
    }

    static bool IsAlive(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return !p.HasExited; }
        catch { return false; }
    }

    /// <summary>Last <paramref name="n"/> non-empty lines of _sistem\tamgaz\gunluk.md (markdown stripped).</summary>
    public static List<string> RecentLog(string vault, int n = 6)
    {
        string? text = Md.ReadText(Path.Combine(Dir(vault), "gunluk.md"));
        if (text is null) return [];
        return Md.BodyLines(Md.StripComments(text))
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith('#'))
            .Select(l => Md.Plain(l.TrimStart('-', '*', ' ')))
            .Where(l => l.Length > 0)
            .TakeLast(n).ToList();
    }
}
