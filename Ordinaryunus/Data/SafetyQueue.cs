// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Globalization;
using System.Text.Json;

namespace Ordinaryunus.Data;

public sealed record SafetyRequest(string Id, DateTimeOffset? Zaman, string Arac, string Islem, string Hedef, string Komut, string Risk, string Neden,
    bool Kesildi = false)
{
    public string ToolName => Arac.Equals("codex", StringComparison.OrdinalIgnoreCase) ? "Codex" : "Claude";
    public bool Critical => Risk.Equals("kritik", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Security gate files in &lt;vault&gt;\_sistem\guvenlik: bekleyenler.jsonl (read-only), kararlar.jsonl (append-only)
/// and the DURDUR flag (create/delete). These are the only vault writes besides the two in SPEC "Writes".
/// </summary>
public static class SafetyQueue
{
    public const string DirRel = "_sistem/guvenlik";

    public static string Dir(string vault) => Path.Combine(vault, "_sistem", "guvenlik");
    public static string PendingFile(string vault) => Path.Combine(Dir(vault), "bekleyenler.jsonl");
    // Decisions live OUTSIDE the vault (%LOCALAPPDATA%\Ordinaryunus\guvenlik\kararlar.jsonl): an AI working in the vault
    // (Codex's sandbox can only write the vault; Claude is blocked by the gate hook) must not be able to approve its own
    // request. The gate reads approvals only from this file. Selftest points it at a temp folder via DecisionsDirOverride.
    public static string? DecisionsDirOverride;
    public static string DecisionsFile(string vault) => Path.Combine(DecisionsDirOverride ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ordinaryunus", "guvenlik"), "kararlar.jsonl");
    // Old location inside the vault: only its rejections still count (hide the card); approvals there are ignored.
    public static string LegacyDecisionsFile(string vault) => Path.Combine(Dir(vault), "kararlar.jsonl");
    public static string StopFlag(string vault) => Path.Combine(Dir(vault), "DURDUR");

    public static List<SafetyRequest> ReadPending(string vault, List<string>? warnings = null)
    {
        string legacyRejections = string.Join("\n", Md.Lines(Md.ReadText(LegacyDecisionsFile(vault)))
            .Where(l => l.Contains("\"karar\":\"red\"", StringComparison.Ordinal)));
        return ParsePending(Md.ReadText(PendingFile(vault)), (Md.ReadText(DecisionsFile(vault)) ?? "") + "\n" + legacyRejections, warnings);
    }

    public static List<SafetyRequest> ParsePending(string? pendingText, string? decisionsText, List<string>? warnings = null)
    {
        var decided = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in Md.Lines(decisionsText))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var d = JsonDocument.Parse(line);
                var r = d.RootElement;
                if (r.ValueKind == JsonValueKind.Object && Get(r, "karar") is "onay" or "red" &&
                    r.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    decided.Add(id.GetString()!);
            }
            catch (JsonException) { }
        }
        var list = new List<SafetyRequest>();
        int bad = 0;
        foreach (var line in Md.Lines(pendingText))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var d = JsonDocument.Parse(line);
                var r = d.RootElement;
                if (r.ValueKind != JsonValueKind.Object) { bad++; continue; }
                string id = Get(r, "id");
                if (id.Length == 0 || decided.Contains(id) || list.Any(x => x.Id == id)) continue;
                DateTimeOffset? z = DateTimeOffset.TryParse(Get(r, "zaman"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t : null;
                string komut = Get(r, "komut");
                // the gate itself does not write "kesildi" yet, but it DOES save
                // the command truncated at exactly 600 chars while the card's summary hash is computed from the
                // FULL command — approving what looks like a short, complete command on screen could really approve
                // a much longer one the person never saw the end of. Until the gate writes the field itself, treat
                // a command that hits that exact cap as cut short too, so "Onayla" is disabled either way.
                bool kesildi = GetBool(r, "kesildi") || komut.Length >= 600;
                list.Add(new SafetyRequest(id, z, Get(r, "arac"), Get(r, "islem"), Get(r, "hedef"), komut, Get(r, "risk"), Get(r, "neden"),
                    kesildi));
            }
            catch (JsonException) { bad++; }
        }
        if (bad > 0) warnings?.Add($"Güvenlik bekleyenler dosyasında okunamayan {bad} satır");
        return list;
    }

    static string Get(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString() : "";

    static bool GetBool(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    /// <summary>karar satırı artık sadece çıplak kimliği
    /// değil, onaylanan/reddedilen kaydın tam içerik özetini de (<paramref name="ozet"/> = <c>Mapper.SafetySummary</c>)
    /// taşır. Kapı kancası şimdilik yalnızca kimliğe bakıyor; kanca bu alanı da denetleyecek şekilde güncellenene
    /// kadar (aynı bulgunun "kapı tarafı" maddesi) bunu şimdiden yazmak ileriye dönük uyumludur ve bedava; bu
    /// uygulamanın <c>Handlers.DecideSafety</c> içindeki KENDİ yeniden okuması da, aynı kimlikle değiştirilmiş bir
    /// bekleyen kayda onay bağlamayı bu sayede reddeder.</summary>
    public static string DecisionLine(string id, bool approve, DateTimeOffset when, string? ozet = null)
    {
        var fields = new Dictionary<string, string>
        {
            ["id"] = id,
            ["karar"] = approve ? "onay" : "red",
            ["zaman"] = when.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
        };
        if (!string.IsNullOrEmpty(ozet)) fields["ozet"] = ozet;
        return JsonSerializer.Serialize(fields, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>Appends a decision (append-only; never edits bekleyenler.jsonl).</summary>
    public static void Decide(string vault, string id, bool approve, string? ozet = null) =>
        VaultWriter.AppendJsonLine(DecisionsFile(vault), DecisionLine(id, approve, DateTimeOffset.UtcNow, ozet));

    public static bool IsStopped(string vault) => File.Exists(StopFlag(vault));

    public static void CreateStopFlag(string vault)
    {
        if (VaultWriter.ReadOnlyMode) throw new InvalidOperationException("Salt okunur mod");
        Directory.CreateDirectory(Dir(vault));
        File.WriteAllText(StopFlag(vault), DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
    }

    public static void RemoveStopFlag(string vault)
    {
        if (VaultWriter.ReadOnlyMode) throw new InvalidOperationException("Salt okunur mod");
        if (File.Exists(StopFlag(vault))) File.Delete(StopFlag(vault));
    }
}
