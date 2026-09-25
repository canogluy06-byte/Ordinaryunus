// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ordinaryunus.Jobs;

/// <summary>Mutable progress of one Codex job, updated line by line by <see cref="CodexStreamParser"/>. Same
/// shape as <see cref="ClaudeJobState"/> so both map onto the one <c>Job</c> DTO (MIMARI §3.9) the same way; kept as
/// a separate type instead of sharing one class because the two tools' terminal-state vocabularies differ slightly
/// and unifying them would only add indirection for no real benefit here.</summary>
public sealed class CodexJobState
{
    public string State = "basliyor";
    public string? SessionId;          // Codex calls it thread_id
    public string Now = "Başlıyor";
    public int Steps;
    public List<string> FilesChanged { get; } = [];
    public List<string> Denials { get; } = [];
    public List<string> SafetyIds { get; } = [];
    public string? ResultText;
    public string? Fix;
    public List<JobStepEntry> StepLog { get; } = [];
    public bool Terminal => State is "bitti" or "hata" or "giris" or "kota" or "durduruldu" or "yarida";
}

/// <summary>
/// Pure parser for <c>codex exec --json</c> event lines (EK-v2.1 §1-2), unit-tested against fixture lines. Never
/// throws: an unparseable or unrecognised line is ignored. The watchman's own <c>&lt;damga&gt;.nobet.json</c> is the
/// authoritative source for the job's final state (MIMARI/EK "durum kaynağı sırası: nobet.json &gt; ayrıştırıcı");
/// this parser only builds the human-readable "now" text, the step list and files/denials/gate detection from the
/// tool's own output, plus a best-effort early state guess for the brief window before nobet.json catches up.
/// </summary>
public static partial class CodexStreamParser
{
    const string GirisFix = "Codex uygulamasında bir kez giriş yap.";
    const string KotaFix = "Codex'in kullanım hakkı doldu; biraz sonra yeniden dene.";

    public static void Feed(CodexJobState s, string line, DateTime now, string? vaultRoot = null)
    {
        line = line.Trim();
        if (line.Length == 0) return;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(line); } catch { return; }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            string? type = Str(root, "type");
            switch (type)
            {
                case "thread.started":
                    s.SessionId = Str(root, "thread_id") ?? Str(root, "threadId");
                    if (s.State == "basliyor") s.State = "calisiyor";
                    s.Now = "Başladı";
                    s.StepLog.Add(new JobStepEntry(now, "start", "Başladı"));
                    break;
                case "turn.started":
                    if (s.State == "basliyor") s.State = "calisiyor";
                    break;
                case "item.started":
                case "item.completed":
                    HandleItem(s, root, now, vaultRoot, type == "item.completed");
                    break;
                case "turn.completed":
                    s.State = "bitti";
                    s.StepLog.Add(new JobStepEntry(now, "result", "Bitti"));
                    break;
                case "turn.failed":
                    HandleTurnFailed(s, root, now);
                    break;
                case "error":
                    string msg = Str(root, "message") ?? "hata";
                    if (AuthRx().IsMatch(msg)) { s.State = "giris"; s.Fix = GirisFix; }
                    else if (QuotaRx().IsMatch(msg)) { s.State = "kota"; s.Fix = KotaFix; }
                    s.StepLog.Add(new JobStepEntry(now, "error", Clip(msg, 300)));
                    break;
            }
        }
    }

    static void HandleItem(CodexJobState s, JsonElement root, DateTime now, string? vaultRoot, bool completed)
    {
        if (!root.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object) return;
        string itemType = Str(item, "type") ?? "";
        string desc;
        bool countsAsStep = false;
        switch (itemType)
        {
            case "command_execution":
                desc = $"Komut: {Clip(TryGetStr(item, "command") ?? "", 60)}";
                countsAsStep = true;
                CheckGateText(s, now, TryGetStr(item, "aggregated_output") ?? TryGetStr(item, "output") ?? "");
                break;
            case "file_change":
            {
                string path = TryGetStr(item, "path") ?? "";
                if (path.Length > 0) AddChanged(s.FilesChanged, Rel(path, vaultRoot));
                desc = path.Length > 0 ? $"Düzenliyor: {Rel(path, vaultRoot)}" : "Dosya değişikliği";
                countsAsStep = true;
                break;
            }
            case "mcp_tool_call":
                desc = $"MCP aracı: {TryGetStr(item, "tool") ?? TryGetStr(item, "server") ?? ""}".TrimEnd();
                countsAsStep = true;
                break;
            case "web_search":
                desc = $"Web'de arıyor: {TryGetStr(item, "query") ?? ""}";
                countsAsStep = true;
                break;
            case "patch_apply":
                desc = "Yama uygulanıyor";
                countsAsStep = true;
                break;
            case "agent_message":
                desc = "Yazıyor…";
                if (completed) s.StepLog.Add(new JobStepEntry(now, "text", Clip(TryGetStr(item, "text") ?? "", 160)));
                break;
            case "reasoning":
                desc = "Düşünüyor…";
                break;
            default:
                desc = itemType.Length > 0 ? $"Adım: {itemType}" : "Çalışıyor…";
                break;
        }
        s.Now = desc;
        if (completed)
        {
            if (countsAsStep) s.Steps++;
            s.StepLog.Add(new JobStepEntry(now, "tool", desc));
        }
    }

    static void HandleTurnFailed(CodexJobState s, JsonElement root, DateTime now)
    {
        string msg = root.TryGetProperty("error", out var err) ? Str(err, "message") ?? "hata" : "hata";
        s.ResultText = msg;
        if (AuthRx().IsMatch(msg)) { s.State = "giris"; s.Fix = GirisFix; }
        else if (QuotaRx().IsMatch(msg)) { s.State = "kota"; s.Fix = KotaFix; }
        else s.State = "hata";
        s.StepLog.Add(new JobStepEntry(now, "error", Clip(msg, 300)));
    }

    static void CheckGateText(CodexJobState s, DateTime now, string text)
    {
        if (text.Length == 0) return;
        if (text.Contains("Güvenlik kapısı", StringComparison.Ordinal))
        {
            var m = IdRx().Match(text);
            if (m.Success)
            {
                s.SafetyIds.Add(m.Groups[1].Value);
                s.StepLog.Add(new JobStepEntry(now, "gate", $"Onayını bekliyor (#{m.Groups[1].Value})"));
            }
        }
        else if (text.Contains("ACİL DURDUR", StringComparison.Ordinal))
        {
            s.StepLog.Add(new JobStepEntry(now, "gate", "Acil durdurma yüzünden durdu"));
        }
    }

    static void AddChanged(List<string> filesChanged, string rel)
    {
        if (rel.Length > 0 && filesChanged.Count < 50 && !filesChanged.Contains(rel)) filesChanged.Add(rel);
    }

    static string Rel(string full, string? vaultRoot)
    {
        if (vaultRoot is null || full.Length == 0) return full;
        try
        {
            string rel = Path.GetRelativePath(vaultRoot, full).Replace('\\', '/');
            return rel.StartsWith("..", StringComparison.Ordinal) ? full : rel;
        }
        catch { return full; }
    }

    static string? Str(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    static string? TryGetStr(JsonElement e, string name) => Str(e, name);
    static string Clip(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    [GeneratedRegex(@"(?i)(not logged in|codex login|unauthori[sz]ed|authentication_error)")]
    private static partial Regex AuthRx();
    [GeneratedRegex(@"(?i)(usage limit|hit your limit|rate limit|try again (in|at)|\b429\b)")]
    private static partial Regex QuotaRx();
    [GeneratedRegex(@"#([A-Za-z0-9-]+)")] private static partial Regex IdRx();
}
