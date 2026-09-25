// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Globalization;
using System.Text.Json;

namespace Ordinaryunus.Data;

/// <summary>
/// A13 (frontend's own addition, confirmed here): "Son çalışma" (last-run time) shown on each Kestirmeler tile.
/// The bridge had no field for this (MIMARI §3.6/§3.11 shortcuts have no timestamp) — it needs somewhere to live,
/// so this is a tiny local log next to gunluk.json/ayarlar.json (%APPDATA%\Ordinaryunus\kisayollar.json,
/// {id: ISO}), same pattern as <see cref="AppJournal"/>. Never a vault write; guarded by
/// <see cref="VaultWriter.ReadOnlyMode"/> exactly like AppJournal.Add so selftest/dry-run/screenshots
/// (all redirected to a temp ORDINARYUNUS_DATA) never touch the real file.
/// </summary>
public static class ShortcutRuns
{
    static string File_() => Path.Combine(AppPaths.DataDir, "kisayollar.json");

    public static Dictionary<string, DateTime> Load()
    {
        var result = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        try
        {
            string path = File_();
            if (!File.Exists(path)) return result;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;
            foreach (var prop in doc.RootElement.EnumerateObject())
                if (DateTime.TryParse(prop.Value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t))
                    result[prop.Name] = t;
        }
        catch (Exception) { /* corrupt/missing file → treat as "never run" for everything */ }
        return result;
    }

    /// <summary>Records "now" for one shortcut id. No-op in read-only mode (selftest/dry-run/screenshots).</summary>
    public static void MarkRun(string id)
    {
        if (VaultWriter.ReadOnlyMode) return;
        try
        {
            var all = Load();
            all[id] = DateTime.Now;
            Directory.CreateDirectory(AppPaths.DataDir);
            var payload = all.ToDictionary(kv => kv.Key, kv => kv.Value.ToString("o", CultureInfo.InvariantCulture));
            File.WriteAllText(File_(), JsonSerializer.Serialize(payload,
                new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        }
        catch (Exception) { /* best-effort; a missed timestamp is cosmetic */ }
    }

    public static string? IsoOrNull(Dictionary<string, DateTime> runs, string id) =>
        runs.TryGetValue(id, out var t) ? t.ToString("o", CultureInfo.InvariantCulture) : null;
}
