// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Text.Json;

namespace Ordinaryunus.Data;

/// <summary>
/// kullanıcıyla aynı Windows hesabında çalışan bir yapay zekâya karşı tam koruma
/// mümkün değil; inceleme, kısa (8.3) bir yol adının güvenlik kapısının "kararlar.jsonl" metin eşleşmesini
/// atlatabildiğini gösterdi. Önleme değil tespit amaçlı bir yedek olarak Ordinaryunus, <c>decideSafety</c> üzerinden
/// KENDİ verdiği her kararın yalnızca eklenen (append-only) bir kaydını tutar (kasanın dışında, kapının karar
/// dosyasının yanında; yani bir yapay zekâ LocalDir'e yazma yetkisi olmadan buraya sahte kayıt ekleyemez — kapının
/// karar dosyasının dayandığı güven sınırıyla aynı). Her yeniden yüklemede bu kaydı kapının kararlar.jsonl dosyasıyla
/// karşılaştırır: kimliği hiç BU uygulamadan geçmemiş her "onay" satırı, kullanıcının burada hiç tıklamadığı bir
/// karardır ve <see cref="VaultSnapshot.Load"/> bunu arayüzün kırmızı gösterdiği bir uyarıya çevirir.
/// </summary>
public static class OwnDecisionLog
{
    public static string FilePath => Path.Combine(AppPaths.LocalDir, "guvenlik", "verdigim-kararlar.jsonl");

    /// <summary>Called right after this app's own <c>decideSafety</c> approves a request.</summary>
    public static void Record(string id)
    {
        if (VaultWriter.ReadOnlyMode || string.IsNullOrEmpty(id)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.AppendAllText(FilePath, JsonSerializer.Serialize(new Dictionary<string, string> { ["id"] = id }) + "\n");
        }
        catch { /* best-effort log; never blocks the real decision */ }
    }

    static HashSet<string> LoadIds()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ReadIds(Md.ReadText(FilePath))) set.Add(id);
        return set;
    }

    static IEnumerable<string> ReadIds(string? text)
    {
        foreach (var line in Md.Lines(text))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string? id = null;
            try
            {
                using var d = JsonDocument.Parse(line);
                if (d.RootElement.ValueKind == JsonValueKind.Object && d.RootElement.TryGetProperty("id", out var v) && v.ValueKind == JsonValueKind.String)
                    id = v.GetString();
            }
            catch (JsonException) { }
            if (id is { Length: > 0 }) yield return id;
        }
    }

    static IEnumerable<string> ReadApprovedIds(string? decisionsText)
    {
        foreach (var line in Md.Lines(decisionsText))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string? id = null;
            try
            {
                using var d = JsonDocument.Parse(line);
                var r = d.RootElement;
                if (r.ValueKind == JsonValueKind.Object && r.TryGetProperty("karar", out var k) && k.ValueKind == JsonValueKind.String &&
                    k.GetString() == "onay" && r.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                    id = idEl.GetString();
            }
            catch (JsonException) { }
            if (id is { Length: > 0 }) yield return id;
        }
    }

    static void Seed(IEnumerable<string> ids)
    {
        if (VaultWriter.ReadOnlyMode) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            using var sw = new StreamWriter(FilePath, append: true);
            foreach (var id in ids) sw.WriteLine(JsonSerializer.Serialize(new Dictionary<string, string> { ["id"] = id }));
        }
        catch { }
    }

    /// <summary>Ids with an "onay" decision in the gate's kararlar.jsonl that this app never recorded making
    /// itself. The very first time this runs (own log file does not exist yet) every currently-existing approval is
    /// grandfathered in silently — otherwise every decision made before this feature shipped would falsely alarm on
    /// the first reload after an update. Only approvals that appear from that point on are ever reported.</summary>
    public static List<string> FindForeignApprovals(string vault)
    {
        try
        {
            var currentOnay = ReadApprovedIds(Md.ReadText(SafetyQueue.DecisionsFile(vault))).ToList();
            if (!File.Exists(FilePath))
            {
                Seed(currentOnay);
                return [];
            }
            var mine = LoadIds();
            return currentOnay.Where(id => !mine.Contains(id)).Distinct(StringComparer.Ordinal).ToList();
        }
        catch { return []; }
    }
}
