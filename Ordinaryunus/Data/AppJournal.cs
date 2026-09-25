// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Globalization;
using System.Text.Json;

namespace Ordinaryunus.Data;

/// <summary>Local log of items checked off from the app (%APPDATA%\Ordinaryunus\gunluk.json), used for the streak.</summary>
public static class AppJournal
{
    public sealed record Entry(DateTime Zaman, string Metin);

    public static List<Entry> Load()
    {
        try
        {
            if (!File.Exists(AppPaths.JournalFile)) return [];
            using var doc = JsonDocument.Parse(File.ReadAllText(AppPaths.JournalFile));
            var list = new List<Entry>();
            if (doc.RootElement.TryGetProperty("isaretlemeler", out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var e in arr.EnumerateArray())
                    if (DateTime.TryParse(e.GetProperty("zaman").GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var z))
                        list.Add(new Entry(z, e.TryGetProperty("metin", out var m) ? m.GetString() ?? "" : ""));
            return list;
        }
        catch (Exception) { return []; }
    }

    public static void Add(string text)
    {
        if (VaultWriter.ReadOnlyMode) return;
        var list = Load();
        list.Add(new Entry(DateTime.Now, text));
        if (list.Count > 1000) list = list[^1000..];
        var payload = new Dictionary<string, object>
        {
            ["isaretlemeler"] = list.Select(e => new Dictionary<string, string>
            {
                ["zaman"] = e.Zaman.ToString("o", CultureInfo.InvariantCulture),
                ["metin"] = e.Metin,
            }).ToList(),
        };
        Directory.CreateDirectory(AppPaths.DataDir);
        File.WriteAllText(AppPaths.JournalFile, JsonSerializer.Serialize(payload,
            new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }
}
