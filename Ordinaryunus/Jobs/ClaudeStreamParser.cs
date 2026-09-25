// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ordinaryunus.Jobs;

public sealed record JobStepEntry(DateTime T, string Kind, string Text);

/// <summary>Mutable progress of one Claude job, updated line by line by <see cref="ClaudeStreamParser"/>.</summary>
public sealed class ClaudeJobState
{
    public string State = "basliyor";
    public string? SessionId;
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

/// <summary>Pure stream-json line parser (MIMARI §6.5), unit-tested against fixture lines. Never throws: an
/// unparseable or unrecognised line is ignored.</summary>
public static partial class ClaudeStreamParser
{
    const string GirisFix = "Claude komut satırında bir kez /login yap.";
    const string KotaFix = "Claude'un kullanım hakkı doldu; biraz sonra yeniden dene.";

    public static void Feed(ClaudeJobState s, string line, DateTime now, string? vaultRoot = null)
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
                case "system" when Str(root, "subtype") == "init":
                    s.SessionId = Str(root, "session_id") ?? Str(root, "sessionId");
                    if (s.State == "basliyor") s.State = "calisiyor";
                    s.Now = "Başladı";
                    s.StepLog.Add(new JobStepEntry(now, "start", "Başladı"));
                    break;
                case "assistant":
                    HandleAssistant(s, root, now, vaultRoot);
                    break;
                case "user":
                    HandleUser(s, root, now);
                    break;
                case "result":
                    HandleResult(s, root, now);
                    break;
            }
        }
    }

    /// <summary>stderr lines: an auth/quota pattern here also ends the job (checked outside the 10 s window elsewhere).</summary>
    public static void FeedStderr(ClaudeJobState s, string text, TimeSpan elapsedSinceStart)
    {
        if (AuthRx().IsMatch(text)) { s.State = "giris"; s.Fix = GirisFix; }
        else if (QuotaRx().IsMatch(text)) { s.State = "kota"; s.Fix = KotaFix; }
        else if (elapsedSinceStart < TimeSpan.FromSeconds(10) && text.Contains("error: unknown option", StringComparison.OrdinalIgnoreCase))
        {
            s.State = "hata";
            s.Fix = "Claude sürümün bu ayarı tanımıyor; Claude'u güncelle. Şimdilik 'Sadece kopyala'yı kullan.";
        }
    }

    static void HandleAssistant(ClaudeJobState s, JsonElement root, DateTime now, string? vaultRoot)
    {
        if (!root.TryGetProperty("message", out var msg) || !msg.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) return;
        var textParts = new List<string>();
        bool any = false;
        foreach (var block in content.EnumerateArray())
        {
            any = true;
            string? bt = Str(block, "type");
            if (bt == "tool_use")
            {
                s.Steps++;
                string name = Str(block, "name") ?? "";
                var input = block.TryGetProperty("input", out var inp) ? inp : default;
                string desc = DescribeTool(name, input, s.FilesChanged, vaultRoot);
                s.Now = desc;
                s.StepLog.Add(new JobStepEntry(now, "tool", desc));
            }
            else if (bt == "text")
            {
                string t = Str(block, "text") ?? "";
                textParts.Add(t);
                s.Now = "Yazıyor…";
                s.StepLog.Add(new JobStepEntry(now, "text", Clip(t, 160)));
            }
            else if (bt == "thinking")
            {
                s.Now = "Düşünüyor…";
                s.StepLog.Add(new JobStepEntry(now, "think", ""));
            }
        }
        if (any && s.State == "basliyor") s.State = "calisiyor";
        string joined = string.Concat(textParts);
        if (joined.StartsWith("API Error", StringComparison.Ordinal) || joined.Contains("Invalid API key", StringComparison.OrdinalIgnoreCase))
        {
            if (AuthRx().IsMatch(joined)) { s.State = "giris"; s.Fix = GirisFix; }
            else if (QuotaRx().IsMatch(joined)) { s.State = "kota"; s.Fix = KotaFix; }
        }
    }

    static void HandleUser(ClaudeJobState s, JsonElement root, DateTime now)
    {
        if (!root.TryGetProperty("message", out var msg) || !msg.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) return;
        foreach (var block in content.EnumerateArray())
        {
            if (Str(block, "type") != "tool_result") continue;
            bool isError = block.TryGetProperty("is_error", out var ie) && ie.ValueKind == JsonValueKind.True;
            if (!isError) continue;
            string text = ExtractResultText(block);
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
    }

    static void HandleResult(ClaudeJobState s, JsonElement root, DateTime now)
    {
        s.ResultText = Str(root, "result");
        bool isError = root.TryGetProperty("is_error", out var ie) && ie.ValueKind == JsonValueKind.True;
        if (root.TryGetProperty("permission_denials", out var pd) && pd.ValueKind == JsonValueKind.Array)
        {
            foreach (var d in pd.EnumerateArray())
            {
                string tool = Str(d, "tool_name") ?? "";
                string extra = "";
                if (tool == "Bash" && d.TryGetProperty("tool_input", out var ti) && ti.TryGetProperty("command", out var cmd) && cmd.ValueKind == JsonValueKind.String)
                    extra = ": " + Clip(cmd.GetString() ?? "", 60);
                s.Denials.Add(tool + extra);
            }
        }
        string combined = s.ResultText ?? "";
        if (AuthRx().IsMatch(combined)) { s.State = "giris"; s.Fix = GirisFix; }
        else if (QuotaRx().IsMatch(combined)) { s.State = "kota"; s.Fix = KotaFix; }
        else s.State = isError ? "hata" : "bitti";
        s.StepLog.Add(new JobStepEntry(now, "result", Clip(combined, 300)));
    }

    static string DescribeTool(string name, JsonElement input, List<string> filesChanged, string? vaultRoot)
    {
        string FilePath() => TryGetStr(input, "file_path") ?? TryGetStr(input, "path") ?? "";
        string Rel(string full)
        {
            if (vaultRoot is null || full.Length == 0) return full;
            try
            {
                string rel = Path.GetRelativePath(vaultRoot, full).Replace('\\', '/');
                return rel.StartsWith("..", StringComparison.Ordinal) ? full : rel;
            }
            catch { return full; }
        }
        void AddChanged(string full)
        {
            if (full.Length == 0) return;
            string rel = Rel(full);
            if (filesChanged.Count < 50 && !filesChanged.Contains(rel)) filesChanged.Add(rel);
        }
        switch (name)
        {
            case "Read": return $"Okuyor: {Rel(FilePath())}";
            case "Grep": return $"Arıyor: “{TryGetStr(input, "pattern") ?? ""}”";
            case "Glob": return "Dosya arıyor";
            case "Edit": case "MultiEdit": AddChanged(FilePath()); return $"Düzenliyor: {Rel(FilePath())}";
            case "Write": AddChanged(FilePath()); return $"Yazıyor: {Rel(FilePath())}";
            case "NotebookEdit": AddChanged(FilePath()); return $"Düzenliyor: {Rel(FilePath())}";
            case "WebSearch": return $"Web'de arıyor: {TryGetStr(input, "query") ?? ""}";
            case "WebFetch":
                string url = TryGetStr(input, "url") ?? "";
                string host = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : url;
                return $"Sayfa okuyor: {host}";
            case "TodoWrite": return "Plan güncelliyor";
            case "Bash": return $"Komut: {Clip(TryGetStr(input, "command") ?? "", 60)}";
            case "Task": case "Agent": return "Yardımcı ajan çalışıyor";
            default: return $"Araç: {name}";
        }
    }

    static string ExtractResultText(JsonElement toolResultBlock)
    {
        if (!toolResultBlock.TryGetProperty("content", out var c)) return "";
        if (c.ValueKind == JsonValueKind.String) return c.GetString() ?? "";
        if (c.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var b in c.EnumerateArray())
                if (Str(b, "type") == "text") parts.Add(Str(b, "text") ?? "");
            return string.Concat(parts);
        }
        return "";
    }

    static string? Str(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    static string? TryGetStr(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    static string Clip(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    [GeneratedRegex(@"(?i)(oauth token has expired|authentication_error|invalid api key|please run /login|run /login|not logged in|\b401\b.*(unauthori[sz]ed|authentication))")]
    public static partial Regex AuthRx();
    [GeneratedRegex(@"(?i)(usage limit|limit reached|rate_limit_error|5-hour limit|weekly limit)")]
    public static partial Regex QuotaRx();
    [GeneratedRegex(@"#([A-Za-z0-9-]+)")] private static partial Regex IdRx();
}
