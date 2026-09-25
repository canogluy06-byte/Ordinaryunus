// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Globalization;
using System.Text.Json;

namespace Ordinaryunus.Jobs;

/// <summary>
/// One <c>&lt;damga&gt;.nobet.json</c> file, written atomically by the vault's job watchman
/// (<c>_sistem/araclar/is-nobetcisi.mjs</c>, EK-v2.1 §1). This is the AUTHORITATIVE source for a job's terminal/
/// waiting state ("durum kaynağı sırası: nobet.json > ayrıştırıcı") — the stream parser only fills in the
/// human-readable "now" text and step list from the tool's own output lines.
/// </summary>
public sealed record NobetStatus(
    string? Is, string? Arac, int? Pid, string? Durum, int Deneme, string? SessionId,
    DateTimeOffset? BekleUntil, int? ExitCode, string? Not, DateTimeOffset? Basla, DateTimeOffset? Guncel)
{
    public static NobetStatus? TryRead(string path)
    {
        string text;
        try { text = File.ReadAllText(path); } catch (IOException) { return null; } catch (UnauthorizedAccessException) { return null; }
        return Parse(text);
    }

    public static NobetStatus? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return null;
            string? Str(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            int? Int(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
            DateTimeOffset? Date(string n) => Str(n) is { Length: > 0 } s &&
                DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : null;
            int deneme = r.TryGetProperty("deneme", out var dv) && dv.ValueKind == JsonValueKind.Number && dv.TryGetInt32(out var di) ? di : 0;
            return new NobetStatus(Str("is"), Str("arac"), Int("pid"), Str("durum"), deneme, Str("sessionId"),
                Date("bekleUntil"), Int("exitCode"), Str("not"), Date("basla"), Date("guncel"));
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Non-terminal durum values (the watchman itself defines these): still trying, or waiting out a
    /// quota. "bitti"/"hata"/"giris"/"durduruldu" are terminal — the watchman process has already exited.</summary>
    public bool IsNonTerminal => Durum is "calisiyor" or "kota-bekliyor" or null;
}
