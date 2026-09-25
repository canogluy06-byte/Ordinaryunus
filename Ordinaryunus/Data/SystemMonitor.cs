// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ordinaryunus.Data;

public enum LightState { Green, Yellow, Red, Off }

public sealed record Light(string Label, LightState State, string Detail, string Tooltip);

public sealed record UsageToday(long? CodexTokens, long? ClaudeTokens, bool TimedOut);

/// <summary>System lights (top bar) and today's token usage estimate (Durum). All read-only.</summary>
public static partial class SystemMonitor
{
    public static List<Light> CheckLights(string vault, DateTime now)
    {
        var lights = new List<Light>
        {
            ProcessLight("Claude", ["claude", "Claude"], "Claude uygulaması açık mı? Yeşil: açık. Gri: kapalı."),
            ProcessLight("Codex", ["Codex", "codex"], "Codex uygulaması açık mı? Yeşil: açık. Gri: kapalı."),
            ProcessLight("Obsidian", ["Obsidian"], "Notlarını gösteren Obsidian açık mı? Yeşil: açık. Gri: kapalı."),
        };

        // Git isteğe bağlıdır: git'siz bir kasa sarı yanar (kayıt geçmişi ve betik koruması kısıtlı), kırmızı sadece
        // kasa klasörü yoksa. Böylece git kullanmayan biri kalıcı bir "kritik" sorunla karşılaşmaz.
        bool vaultExists = Directory.Exists(vault);
        bool gitOk = vaultExists &&
            ProcessRunner.Run("git", ["-C", vault, "rev-parse", "--is-inside-work-tree"], null, 8000).ExitCode == 0;
        const string kasaTip = "Notlarının durduğu klasör (kasa) yerinde mi, kayıt geçmişi (git) çalışıyor mu? " +
            "Yeşil: ikisi de tamam. Sarı: kasa yerinde ama git deposu değil (isteğe bağlı). Kırmızı: kasa bulunamadı.";
        lights.Add(!vaultExists ? new Light("Kasa", LightState.Red, "bulunamadı", kasaTip)
            : gitOk ? new Light("Kasa", LightState.Green, "hazır", kasaTip)
            : new Light("Kasa", LightState.Yellow, "git yok", kasaTip));

        string home = AppPaths.UserProfile;
        bool gateFile = File.Exists(Path.Combine(home, ".claude", "hooks", "ikinci-beyin", "guvenlik-kapisi.mjs"));
        string settings = Md.ReadText(Path.Combine(home, ".claude", "settings.json")) ?? "";
        bool gateOn = gateFile && settings.Contains("guvenlik-kapisi", StringComparison.Ordinal);
        const string kapiTip = "Yapay zekâ tehlikeli bir iş yapmadan önce senden izin istiyor mu? Yeşil: evet. " +
            "Sarı: kapı kurulu ama IKINCI_BEYIN_KASA ortam değişkeni bu kasayı göstermiyor; kasanın dışında açılan Claude " +
            "oturumlarında onay istekleri buraya düşmeyebilir, Acil Durdur görülmeyebilir (KURULUM.md 3.2). Kırmızı: kurulu değil.";
        if (!gateOn) lights.Add(new Light("Güvenlik kapısı", LightState.Red, "kurulu değil", kapiTip));
        else
        {
            // Kancanın göreceği kasa yolu: önce Claude Code ayarlarındaki "env" bölümü (oturumlara ve kancalara o geçer),
            // sonra ortam değişkeni. Uygulamadan verilen işler bu değişkeni zaten kendileri ayarlar (iş nöbetçisi).
            string? gateVault = GateVaultSetting(settings);
            bool same = gateVault is not null && SamePath(gateVault, vault);
            lights.Add(same ? new Light("Güvenlik kapısı", LightState.Green, "açık", kapiTip)
                : new Light("Güvenlik kapısı", LightState.Yellow, gateVault is null ? "kasa yolu ayarlı değil" : "başka kasayı gösteriyor", kapiTip));
        }

        var reset = CodexQuotaReset(now);
        lights.Add(reset is { } r && r > now
            ? new Light("Codex kotası", LightState.Yellow, $"açılış {r:HH:mm}", "Codex'in kullanım hakkı bitti; yazan saatte yeniden açılır.")
            : new Light("Codex kotası", LightState.Green, "var", "Codex'in bugünkü kullanım hakkı var mı? Yeşil: var."));
        return lights;
    }

    public const string GateVaultEnvVar = "IKINCI_BEYIN_KASA";

    /// <summary>Kancaların kullanacağı kasa yolu: Claude Code settings.json içindeki <c>"env"</c> bölümü, sonra bu
    /// sürecin, kullanıcının ve makinenin ortam değişkeni (setx sonrası uygulama yeniden açılmasa da kullanıcı
    /// değişkeni kayıt defterinden okunur). Hiçbiri yoksa null.</summary>
    public static string? GateVaultSetting(string claudeSettingsJson)
    {
        try
        {
            if (claudeSettingsJson.Contains(GateVaultEnvVar, StringComparison.Ordinal))
            {
                using var doc = JsonDocument.Parse(claudeSettingsJson, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (doc.RootElement.TryGetProperty("env", out var env) && env.ValueKind == JsonValueKind.Object &&
                    env.TryGetProperty(GateVaultEnvVar, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s && s.Trim().Length > 0)
                    return s.Trim().Trim('"');
            }
        }
        catch (JsonException) { }
        foreach (var target in new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
        {
            try
            {
                if (Environment.GetEnvironmentVariable(GateVaultEnvVar, target) is { } e && e.Trim().Length > 0) return e.Trim().Trim('"');
            }
            catch (Exception) { /* kayıt defteri okunamadı: sıradakine geç */ }
        }
        return null;
    }

    /// <summary>İki klasör yolu aynı yeri mi gösteriyor (büyük/küçük harf ve sondaki bölü önemsiz)?</summary>
    public static bool SamePath(string a, string b)
    {
        try
        {
            static string Norm(string p) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p.Trim()));
            return string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception) { return false; }
    }

    static Light ProcessLight(string label, string[] names, string tip)
    {
        bool running = false;
        foreach (var n in names)
        {
            try
            {
                var ps = Process.GetProcessesByName(n);
                running |= ps.Length > 0;
                foreach (var p in ps) p.Dispose();
            }
            catch { }
            if (running) break;
        }
        return new Light(label, running ? LightState.Green : LightState.Off, running ? "açık" : "kapalı", tip);
    }

    static string TodaySessionsDir(DateTime now) =>
        Path.Combine(AppPaths.UserProfile, ".codex", "sessions", now.ToString("yyyy", CultureInfo.InvariantCulture),
            now.ToString("MM", CultureInfo.InvariantCulture), now.ToString("dd", CultureInfo.InvariantCulture));

    /// <summary>Looks at today's 3 newest Codex rollouts (last ~300 KB each) for "usage limit … try again at H:MM AM/PM".</summary>
    public static DateTime? CodexQuotaReset(DateTime now)
    {
        string dir = TodaySessionsDir(now);
        if (!Directory.Exists(dir)) return null;
        var files = new DirectoryInfo(dir).GetFiles("rollout-*.jsonl").OrderByDescending(f => f.LastWriteTime).Take(3);
        foreach (var f in files)
        {
            string tail = ReadTail(f.FullName, 300 * 1024);
            var found = ParseQuotaReset(tail, now);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>Finds the last "usage limit … try again at 10:05 PM" in text and returns that local time today.</summary>
    public static DateTime? ParseQuotaReset(string text, DateTime now)
    {
        DateTime? result = null;
        foreach (Match m in TryAgain().Matches(text))
        {
            int start = Math.Max(0, m.Index - 600);
            if (!text.AsSpan(start, m.Index - start).ToString().Contains("usage limit", StringComparison.OrdinalIgnoreCase)) continue;
            int h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) % 12;
            int min = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            if (m.Groups[3].Value.Equals("PM", StringComparison.OrdinalIgnoreCase)) h += 12;
            result = now.Date.AddHours(h).AddMinutes(min);
        }
        return result;
    }

    [GeneratedRegex(@"try again at (\d{1,2}):(\d{2})\s*([AP]M)", RegexOptions.IgnoreCase)] private static partial Regex TryAgain();

    static string ReadTail(string path, int bytes)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long start = Math.Max(0, fs.Length - bytes);
            fs.Seek(start, SeekOrigin.Begin);
            var buf = new byte[fs.Length - start];
            int read = 0;
            while (read < buf.Length) { int n = fs.Read(buf, read, buf.Length - read); if (n <= 0) break; read += n; }
            return Encoding.UTF8.GetString(buf, 0, read);
        }
        catch { return ""; }
    }

    // ---------------- token usage (estimate) ----------------

    public static UsageToday EstimateUsage(DateTime now, TimeSpan budget)
    {
        var sw = Stopwatch.StartNew();
        long? codex = null, claude = null;
        bool timedOut = false;
        try { codex = CodexTokensToday(now, sw, budget); } catch (TimeoutException) { timedOut = true; } catch { }
        if (!timedOut)
        {
            try { claude = ClaudeTokensToday(now, sw, budget); } catch (TimeoutException) { timedOut = true; } catch { }
        }
        return new UsageToday(codex, timedOut ? null : claude, timedOut);
    }

    static long CodexTokensToday(DateTime now, Stopwatch sw, TimeSpan budget)
    {
        string dir = TodaySessionsDir(now);
        if (!Directory.Exists(dir)) return 0;
        long total = 0;
        foreach (var f in Directory.GetFiles(dir, "rollout-*.jsonl"))
        {
            long last = 0;
            foreach (var line in ReadLinesShared(f))
            {
                if (sw.Elapsed > budget) throw new TimeoutException();
                if (!line.Contains("\"token_count\"", StringComparison.Ordinal) || !line.Contains("total_token_usage", StringComparison.Ordinal)) continue;
                var v = CodexTotal(line);
                if (v is not null) last = v.Value;
            }
            total += last;
        }
        return total;
    }

    public static long? CodexTotal(string line)
    {
        try
        {
            using var d = JsonDocument.Parse(line);
            if (d.RootElement.TryGetProperty("payload", out var p) && p.TryGetProperty("info", out var info) &&
                info.ValueKind == JsonValueKind.Object && info.TryGetProperty("total_token_usage", out var tu) &&
                tu.TryGetProperty("total_tokens", out var tt) && tt.ValueKind == JsonValueKind.Number)
                return tt.GetInt64();
        }
        catch (JsonException) { }
        return null;
    }

    static long ClaudeTokensToday(DateTime now, Stopwatch sw, TimeSpan budget)
    {
        string dir = Path.Combine(AppPaths.UserProfile, ".claude", "projects");
        if (!Directory.Exists(dir)) return 0;
        var perMessage = new Dictionary<string, long>();
        long anonymous = 0;
        foreach (var f in Directory.EnumerateFiles(dir, "*.jsonl", SearchOption.AllDirectories))
        {
            if (sw.Elapsed > budget) throw new TimeoutException();
            DateTime mt;
            try { mt = File.GetLastWriteTime(f); } catch { continue; }
            if (mt.Date != now.Date) continue;
            foreach (var line in ReadLinesShared(f))
            {
                if (sw.Elapsed > budget) throw new TimeoutException();
                if (!line.Contains("\"usage\"", StringComparison.Ordinal)) continue;
                var (id, tokens, when) = ClaudeUsage(line);
                if (when is null || when.Value.LocalDateTime.Date != now.Date) continue;
                if (id is null) anonymous += tokens;
                else perMessage[id] = tokens; // streamed messages repeat; keep the last usage per message id
            }
        }
        return perMessage.Values.Sum() + anonymous;
    }

    public static (string? id, long tokens, DateTimeOffset? when) ClaudeUsage(string line)
    {
        try
        {
            using var d = JsonDocument.Parse(line);
            var r = d.RootElement;
            DateTimeOffset? when = r.TryGetProperty("timestamp", out var ts) && DateTimeOffset.TryParse(ts.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var t) ? t : null;
            if (!r.TryGetProperty("message", out var m) || m.ValueKind != JsonValueKind.Object || !m.TryGetProperty("usage", out var u)) return (null, 0, when);
            long Get(string n) => u.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
            string? id = m.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            return (id, Get("input_tokens") + Get("output_tokens") + Get("cache_creation_input_tokens"), when);
        }
        catch (JsonException) { return (null, 0, null); }
    }

    static IEnumerable<string> ReadLinesShared(string path)
    {
        FileStream fs;
        try { fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
        catch { yield break; }
        using var sr = new StreamReader(fs, Encoding.UTF8);
        string? line;
        while ((line = sr.ReadLine()) is not null) yield return line;
    }

    public static string FormatTokens(long? n)
    {
        if (n is null) return "hesaplanamadı";
        double v = n.Value;
        if (v >= 1_000_000) return (v / 1_000_000).ToString("0.#", Md.Tr) + " milyon";
        if (v >= 1_000) return (v / 1_000).ToString("0", Md.Tr) + " bin";
        return v.ToString("0", Md.Tr);
    }
}
