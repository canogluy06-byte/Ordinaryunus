// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Ordinaryunus.Bridge;

/// <summary>Thrown by a validator or handler; carries the exact wire error code + Turkish message (MIMARI §3.3).</summary>
public sealed class BridgeException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
    public static BridgeException Bad(string msg = "İstek geçersiz.") => new("bad_request", msg);
}

// ---------------- payload records (only the fields MIMARI §3.5 allows; unmapped members are rejected) ----------------

public sealed record LoginPayload(string? Password);
public sealed record SetPasswordPayload(string? Password, string? Repeat);
public sealed record ClientErrorPayload(string? Message, string? Source);
public sealed record ActivityPayload(bool FocusRunning);
public sealed record GetNotePayload(string? Path);
public sealed record GetProjectDetailPayload(string? Name);
public sealed record GetLedgerPayload(string? Day, int Limit);
public sealed record SearchVaultPayload(string? Q, int Limit);
public sealed record GetGraphPayload(string? Project);
public sealed record ToggleTodoPayload(string? Key);
// SÖZLEŞME EKİ (A2, backend'in kendi eklediği): Summary, listede gösterilen kaydın kısa özet hash'i — onay/ret,
// kuyruktaki GERÇEK kayıtla eşleşmiyorsa (istek listelendikten sonra değiştiyse) reddedilir.
public sealed record DecideSafetyPayload(string? Id, bool Approve, bool ConfirmCritical, string? Summary);
public sealed record ApproveJobsPayload(List<string>? Keys);
// SÖZLEŞME EKİ (EK-v2.1, kullanıcının önerdiği ad): Web — Claude işi WebFetch de kullanabilsin mi (varsayılan hayır).
public sealed record RunWorkPayload(string? Text, string? Role, string? Project, bool Web);
public sealed record CopyWorkPayload(string? Tool, string? Text, string? Role, string? Project);
public sealed record JobIdPayload(string? Id);
public sealed record ConfirmPayload(bool Confirm);
public sealed record TamGazOnPayload(int Hours);
public sealed record PathPayload(string? Path);
public sealed record OpenUrlPayload(string? Url);
public sealed record OpenFolderPayload(string? Which, string? Project, string? Sub, string? Path);
public sealed record CopyTextPayload(string? Text);
public sealed record RunShortcutPayload(string? Id);
// SÖZLEŞME EKİ (EK-v2.1 §2b/§2c, kullanıcının önerdiği adlar): KeepAwake ("İş varken bilgisayar uyumasın"),
// UiScale'in kabul ettiği değer kümesi 100/125/150'den 100/110/125/140/150/175'e genişledi.
public sealed record SetSettingsPayload(string? VaultPath, int? UiScale, int? LockMinutes, decimal? UsdRate, string? Theme, bool? Animations, bool? KeepAwake);
public sealed record ChangePasswordPayload(string? Old, string? New, string? Repeat);
public sealed record PageReadyPayload(string? Page, string? Tab);

/// <summary>Per-type payload validation (MIMARI §3.2-3.5): required fields, JSON kinds, lengths, enums, regexes;
/// unknown fields are rejected by <see cref="Options"/> (UnmappedMemberHandling.Disallow) before we even see them.</summary>
public static partial class Payloads
{
    /// <summary>Every request type the bridge accepts, and whether it requires an unlocked session.</summary>
    public static readonly Dictionary<string, bool> RequestTypes = new()
    {
        ["hello"] = false, ["getAuthState"] = false, ["login"] = false, ["setPassword"] = false, ["clientError"] = false,
        ["activity"] = true, ["lock"] = true, ["getSnapshot"] = true, ["refresh"] = true, ["getLights"] = true,
        ["getBrainOverview"] = true, ["getBrainTree"] = true, ["getNote"] = true, ["getProjectDetail"] = true,
        ["getLedger"] = true, ["searchVault"] = true, ["getGraph"] = true, ["getProblems"] = true, ["toggleTodo"] = true,
        ["decideSafety"] = true, ["approveJobs"] = true, ["runClaude"] = true, ["runCodex"] = true, ["copyWork"] = true,
        ["getJobs"] = true, ["getJobDetail"] = true, ["cancelJob"] = true, ["retryJobNow"] = true, ["emergencyStop"] = true, ["resume"] = true,
        ["tamGazOn"] = true, ["tamGazOff"] = true, ["openInObsidian"] = true, ["openUrl"] = true, ["openFolder"] = true,
        ["copyText"] = true, ["runShortcut"] = true, ["getSettings"] = true, ["setSettings"] = true, ["pickVaultFolder"] = true,
        ["changePassword"] = true, ["pageReady"] = true,
    };

    /// <summary>Accepted even while locked (§2.4), plus pageReady only for the "giris" page (checked by the handler).</summary>
    public static readonly HashSet<string> AllowedWhileLocked = ["hello", "getAuthState", "login", "setPassword", "clientError", "pageReady"];

    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    static T Parse<T>(JsonElement payload) where T : notnull
    {
        try
        {
            var v = payload.Deserialize<T>(Options);
            if (v is null) throw BridgeException.Bad();
            return v;
        }
        catch (JsonException) { throw BridgeException.Bad(); }
    }

    /// <summary>Validates payload shape for one request type and returns the typed, checked record (or throws).</summary>
    public static object Validate(string type, JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) throw BridgeException.Bad();
        switch (type)
        {
            case "hello": case "getAuthState": case "lock": case "getSnapshot": case "refresh": case "getLights":
            case "getBrainOverview": case "getBrainTree": case "getProblems": case "getJobs": case "tamGazOff":
            case "getSettings": case "pickVaultFolder":
                RequireEmpty(payload);
                return new object();

            case "login": { var p = Parse<LoginPayload>(payload); Len(p.Password, 1, 128, "password"); return p; }
            case "setPassword":
            {
                var p = Parse<SetPasswordPayload>(payload);
                Len(p.Password, 6, 128, "password"); Len(p.Repeat, 6, 128, "repeat");
                return p;
            }
            case "clientError":
            {
                var p = Parse<ClientErrorPayload>(payload);
                LenMax(p.Message, 500, "message"); LenMax(p.Source, 200, "source");
                return p;
            }
            case "activity": return Parse<ActivityPayload>(payload);

            case "getNote": { var p = Parse<GetNotePayload>(payload); LenRange(p.Path, 1, 400, "path"); return p; }
            case "getProjectDetail": { var p = Parse<GetProjectDetailPayload>(payload); LenRange(p.Name, 1, 120, "name"); return p; }
            case "getLedger":
            {
                var p = Parse<GetLedgerPayload>(payload);
                if (p.Day is not null && !DayRx().IsMatch(p.Day)) throw BridgeException.Bad("Gün biçimi YYYY-MM-DD olmalı.");
                if (p.Limit is < 1 or > 500) throw BridgeException.Bad("limit 1-500 arası olmalı.");
                return p;
            }
            case "searchVault":
            {
                var p = Parse<SearchVaultPayload>(payload);
                LenRange(p.Q, 1, 200, "q");
                if (p.Limit is < 1 or > 50) throw BridgeException.Bad("limit 1-50 arası olmalı.");
                return p;
            }
            case "getGraph": { var p = Parse<GetGraphPayload>(payload); LenMax(p.Project, 120, "project"); return p; }

            case "toggleTodo": { var p = Parse<ToggleTodoPayload>(payload); if (p.Key is null || !TodoKeyRx().IsMatch(p.Key)) throw BridgeException.Bad(); return p; }
            case "decideSafety":
            {
                var p = Parse<DecideSafetyPayload>(payload);
                if (p.Id is null || !SafetyIdRx().IsMatch(p.Id)) throw BridgeException.Bad();
                if (p.Summary is null || !SummaryRx().IsMatch(p.Summary)) throw BridgeException.Bad("summary geçersiz.");
                return p;
            }
            case "approveJobs":
            {
                var p = Parse<ApproveJobsPayload>(payload);
                if (p.Keys is null || p.Keys.Count is < 1 or > 100 || p.Keys.Any(k => !ApKeyRx().IsMatch(k))) throw BridgeException.Bad();
                return p;
            }

            case "runClaude": case "runCodex":
            {
                var p = Parse<RunWorkPayload>(payload);
                LenRange(p.Text, 1, 4000, "text"); LenMax(p.Role, 80, "role"); LenMax(p.Project, 120, "project");
                return p;
            }
            case "copyWork":
            {
                var p = Parse<CopyWorkPayload>(payload);
                if (p.Tool is not ("claude" or "codex")) throw BridgeException.Bad("tool \"claude\" ya da \"codex\" olmalı.");
                LenRange(p.Text, 1, 4000, "text"); LenMax(p.Role, 80, "role"); LenMax(p.Project, 120, "project");
                return p;
            }

            case "getJobDetail": case "cancelJob": case "retryJobNow":
            {
                var p = Parse<JobIdPayload>(payload);
                if (p.Id is null || !JobIdRx().IsMatch(p.Id)) throw BridgeException.Bad();
                return p;
            }
            case "emergencyStop": case "resume":
            {
                var p = Parse<ConfirmPayload>(payload);
                if (!p.Confirm) throw BridgeException.Bad("confirm true olmalı.");
                return p;
            }
            case "tamGazOn":
            {
                var p = Parse<TamGazOnPayload>(payload);
                if (p.Hours is not (2 or 5 or 10 or 12)) throw BridgeException.Bad("Süre 2, 5, 10 ya da 12 saat olmalı.");
                return p;
            }
            case "openInObsidian": { var p = Parse<PathPayload>(payload); LenRange(p.Path, 1, 400, "path"); return p; }
            case "openUrl":
            {
                var p = Parse<OpenUrlPayload>(payload);
                LenRange(p.Url, 1, 2048, "url");
                ValidateHttpUrl(p.Url!);
                return p;
            }
            case "openFolder":
            {
                var p = Parse<OpenFolderPayload>(payload);
                if (p.Which is not ("kasa" or "basvuruKlasoru" or "basvuruBelgeleri" or "claudeIsleri" or "codexIsleri" or "projeKlasoru" or "notKonumu"))
                    throw BridgeException.Bad("which geçersiz.");
                LenMax(p.Project, 120, "project"); LenMax(p.Sub, 400, "sub"); LenMax(p.Path, 400, "path");
                if (p.Sub is { } sub && (sub.Contains("..", StringComparison.Ordinal) || sub.Contains(':'))) throw BridgeException.Bad("sub geçersiz.");
                return p;
            }
            case "copyText": { var p = Parse<CopyTextPayload>(payload); LenRange(p.Text, 1, 20000, "text"); return p; }
            case "runShortcut": { var p = Parse<RunShortcutPayload>(payload); if (p.Id is null || !ShortcutIds.Contains(p.Id)) throw BridgeException.Bad(); return p; }

            case "setSettings":
            {
                var p = Parse<SetSettingsPayload>(payload);
                // D2c: 1536x864 mantıksal ekranda (%125 ölçekli 1920 px) %100 küçük kalıyor; adımlar 100-110-125-140-150-175.
                if (p.UiScale is not (null or 100 or 110 or 125 or 140 or 150 or 175)) throw BridgeException.Bad("uiScale 100/110/125/140/150/175 olmalı.");
                if (p.LockMinutes is { } lm && lm is < 1 or > 240) throw BridgeException.Bad("lockMinutes 1-240 arası olmalı.");
                if (p.UsdRate is { } ur && ur is < 1 or > 1000) throw BridgeException.Bad("usdRate 1-1000 arası olmalı.");
                if (p.Theme is not (null or "light" or "dark")) throw BridgeException.Bad("theme \"light\" ya da \"dark\" olmalı.");
                LenMax(p.VaultPath, 260, "vaultPath");
                return p;
            }
            case "changePassword":
            {
                var p = Parse<ChangePasswordPayload>(payload);
                Len(p.Old, 1, 128, "old"); Len(p.New, 6, 128, "new"); Len(p.Repeat, 6, 128, "repeat");
                return p;
            }
            case "pageReady":
            {
                var p = Parse<PageReadyPayload>(payload);
                LenMax(p.Page, 40, "page"); LenMax(p.Tab, 40, "tab");
                return p;
            }
            default: throw new BridgeException("unknown_type", "Bilinmeyen istek türü.");
        }
    }

    static readonly HashSet<string> ShortcutIds =
    [
        "simdi", "kasa", "claudeUygulama", "codexUygulama", "linkedin", "basvuruKlasoru", "haftalik", "devir",
        "istekTara", "kapiTest",
    ];

    static void RequireEmpty(JsonElement payload)
    {
        foreach (var _ in payload.EnumerateObject()) throw BridgeException.Bad();
    }

    static void Len(string? s, int min, int max, string field)
    {
        if (s is null || s.Length < min || s.Length > max) throw BridgeException.Bad($"{field} {min}-{max} karakter olmalı.");
        if (ContainsBadControlChars(s)) throw BridgeException.Bad($"{field} geçersiz karakter içeriyor.");
    }
    static void LenRange(string? s, int min, int max, string field) => Len(s, min, max, field);
    static void LenMax(string? s, int max, string field)
    {
        if (s is null) return;
        if (s.Length > max) throw BridgeException.Bad($"{field} en fazla {max} karakter olmalı.");
        if (ContainsBadControlChars(s)) throw BridgeException.Bad($"{field} geçersiz karakter içeriyor.");
    }

    public static bool ContainsBadControlChars(string s) => s.Any(c => c < 0x20 && c != '\n' && c != '\t');

    public static void ValidateHttpUrl(string url)
    {
        if (url.Any(char.IsWhiteSpace)) throw BridgeException.Bad("url boşluk içeremez.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) throw BridgeException.Bad("url geçersiz.");
        if (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps) throw BridgeException.Bad("Sadece http/https bağlantıları açılabilir.");
        if (string.IsNullOrEmpty(u.Host)) throw BridgeException.Bad("url geçersiz.");
        if (!string.IsNullOrEmpty(u.UserInfo)) throw BridgeException.Bad("url kullanıcı bilgisi içeremez.");
    }

    /// <summary>Path rule (MIMARI §3.10): forward slashes only, no rooted/`..`/`:`/control chars, ≤400 chars.
    /// The caller still checks membership in the current BrainIndex.</summary>
    public static string NormalizeVaultPath(string raw)
    {
        if (raw.Length == 0 || raw.Length > 400) throw BridgeException.Bad();
        if (ContainsBadControlChars(raw)) throw BridgeException.Bad();
        string p = raw.Replace('\\', '/');
        if (p.StartsWith('/') || p.Contains(':') || p.Split('/').Any(seg => seg == "..")) throw BridgeException.Bad();
        return p;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")] private static partial Regex DayRx();
    [GeneratedRegex(@"^td-\d{1,6}-[0-9a-f]{8}$")] private static partial Regex TodoKeyRx();
    [GeneratedRegex(@"^ap-\d{1,6}-[0-9a-f]{8}$")] private static partial Regex ApKeyRx();
    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,64}$")] private static partial Regex SafetyIdRx();
    [GeneratedRegex(@"^[0-9a-f]{8}$")] private static partial Regex SummaryRx();
    // the original pattern only matched the watchman's own default damga shape
    // and rejected any job whose stamp came from elsewhere (e.g. a future Tam Gaz stamp), so getJobDetail/cancelJob
    // returned bad_request for a job the list already showed. Widened to the watchman's own id rule
    // (is-nobetcisi.mjs: /^[0-9A-Za-z-]{6,60}$/) with the tool prefix kept — no path-traversal risk, the value is
    // only ever used to look the job up in the in-memory tracked-jobs map, never to build a file path directly.
    [GeneratedRegex(@"^(claude|codex):[0-9A-Za-z-]{6,60}$")] public static partial Regex JobIdRx();
}
