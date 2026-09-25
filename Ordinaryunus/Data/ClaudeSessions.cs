// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ordinaryunus.Data;

public enum ClaudeSessionState { Aktif, IzinBekliyorOlabilir, Bosta }

/// <summary>One Claude Code session transcript active in the last few minutes, for Şirketim's "Çalışan işler" panel.</summary>
public sealed record ClaudeSessionRow(string ProjectDir, string SessionId, string Title, DateTime LastActivityLocal, ClaudeSessionState State);

/// <summary>
/// Reads Claude Code session transcripts (<c>%USERPROFILE%\.claude\projects\*\*.jsonl</c>), read-only, to show
/// "is a Claude session still active / waiting for permission?" on the Şirketim page. Genuinely new data (v1.1
/// §2.3) — not read anywhere else in the app. A transcript is one JSON object per line; a file is never fully
/// loaded: only its last ~256 KB is read (cheap even for a multi-hundred-MB file, since a tail read costs no
/// more than the bytes actually read), which is enough to tell whether the session is active or waiting.
/// </summary>
public static partial class ClaudeSessionReader
{
    const int TailBytes = 256 * 1024;
    const int ActiveMinutes = 3;

    public static string DefaultRoot => Path.Combine(AppPaths.UserProfile, ".claude", "projects");

    /// <summary>Sessions whose transcript file was modified within <paramref name="window"/> of <paramref name="nowLocal"/>,
    /// most recently active first. Never throws: a missing root, an unreadable file or a malformed line is skipped.</summary>
    public static List<ClaudeSessionRow> Recent(DateTime nowLocal, TimeSpan window, string? root = null)
    {
        string dir = root ?? DefaultRoot;
        var list = new List<ClaudeSessionRow>();
        if (!Directory.Exists(dir)) return list;
        string[] projectDirs;
        try { projectDirs = Directory.GetDirectories(dir); } catch { return list; }
        foreach (var proj in projectDirs)
        {
            string[] files;
            try { files = Directory.GetFiles(proj, "*.jsonl"); } catch { continue; }
            foreach (var f in files)
            {
                DateTime mtime;
                try { mtime = File.GetLastWriteTime(f); } catch { continue; }
                var age = nowLocal - mtime;
                if (age < TimeSpan.Zero || age > window) continue;
                var row = ParseFile(f, mtime, nowLocal);
                if (row is not null) list.Add(row);
            }
        }
        return list.OrderByDescending(r => r.LastActivityLocal).ToList();
    }

    static ClaudeSessionRow? ParseFile(string path, DateTime mtimeLocal, DateTime nowLocal)
    {
        try
        {
            var (tail, truncated) = ReadTail(path, TailBytes);
            var lines = Md.Lines(tail);
            if (truncated && lines.Length > 1) lines = lines[1..]; // the seek point can land mid-line

            string? summaryTitle = null, promptTitle = null;
            var pending = new HashSet<string>(); // tool_use ids from the LAST assistant turn, not yet resolved

            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                JsonDocument doc;
                try { doc = JsonDocument.Parse(line); } catch { continue; }
                using (doc)
                {
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object) continue;

                    if (root.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String &&
                        typeEl.GetString() == "summary" && root.TryGetProperty("summary", out var sEl) &&
                        sEl.ValueKind == JsonValueKind.String && sEl.GetString() is { Length: > 0 } sv)
                    {
                        summaryTitle = sv;
                        continue;
                    }

                    if (!root.TryGetProperty("message", out var msg) || msg.ValueKind != JsonValueKind.Object) continue;
                    string role = msg.TryGetProperty("role", out var roleEl) && roleEl.ValueKind == JsonValueKind.String ? roleEl.GetString() ?? "" : "";
                    if (!msg.TryGetProperty("content", out var content)) continue;

                    if (role == "assistant")
                    {
                        // A new assistant turn replaces the pending set: any tool_use of an EARLIER turn that is
                        // still unresolved by the time a later assistant message exists was, by definition, answered.
                        pending.Clear();
                        if (content.ValueKind == JsonValueKind.Array)
                            foreach (var block in content.EnumerateArray())
                                if (block.ValueKind == JsonValueKind.Object &&
                                    block.TryGetProperty("type", out var bt) && bt.ValueKind == JsonValueKind.String && bt.GetString() == "tool_use" &&
                                    block.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String && idEl.GetString() is { Length: > 0 } id)
                                    pending.Add(id);
                    }
                    else if (role == "user")
                    {
                        bool isSidechain = root.TryGetProperty("isSidechain", out var scEl) && scEl.ValueKind == JsonValueKind.True;
                        bool isMeta = root.TryGetProperty("isMeta", out var metaEl) && metaEl.ValueKind == JsonValueKind.True;
                        if (content.ValueKind == JsonValueKind.Array)
                            foreach (var block in content.EnumerateArray())
                                if (block.ValueKind == JsonValueKind.Object &&
                                    block.TryGetProperty("type", out var bt) && bt.ValueKind == JsonValueKind.String && bt.GetString() == "tool_result" &&
                                    block.TryGetProperty("tool_use_id", out var tid) && tid.ValueKind == JsonValueKind.String && tid.GetString() is { } tidv)
                                    pending.Remove(tidv);
                        if (promptTitle is null && !isSidechain && !isMeta)
                        {
                            string? text = ExtractUserText(content);
                            if (text is { Length: > 0 } t && !t.StartsWith('<')) promptTitle = t;
                        }
                    }
                }
            }

            string sessionId = Path.GetFileNameWithoutExtension(path);
            string rawTitle = summaryTitle ?? promptTitle ?? ("Oturum " + Short(sessionId));
            string title = Clip60(MaskSecrets(rawTitle));
            bool aktif = nowLocal - mtimeLocal < TimeSpan.FromMinutes(ActiveMinutes);
            var state = aktif ? ClaudeSessionState.Aktif
                : pending.Count > 0 ? ClaudeSessionState.IzinBekliyorOlabilir
                : ClaudeSessionState.Bosta;
            return new ClaudeSessionRow(Path.GetFileName(Path.GetDirectoryName(path)) ?? "", sessionId, title, mtimeLocal, state);
        }
        catch { return null; }
    }

    static string? ExtractUserText(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String) return content.GetString();
        if (content.ValueKind != JsonValueKind.Array) return null;
        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind == JsonValueKind.Object &&
                block.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "text" &&
                block.TryGetProperty("text", out var txt) && txt.ValueKind == JsonValueKind.String && txt.GetString() is { Length: > 0 } s)
                return s;
        }
        return null;
    }

    static (string text, bool truncated) ReadTail(string path, int maxBytes)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long start = Math.Max(0, fs.Length - maxBytes);
        fs.Seek(start, SeekOrigin.Begin);
        using var sr = new StreamReader(fs, Encoding.UTF8);
        return (sr.ReadToEnd(), start > 0);
    }

    static string Short(string id) => id.Length <= 8 ? id : id[..8];
    static string Clip60(string s) => Md.Clip(s.Replace('\r', ' ').Replace('\n', ' ').Trim(), 60);

    /// <summary>Masks anything that looks like a password/token/card number/TC kimlik no. Never shows more than
    /// the masked form — applied before clipping, so truncation can never leak a partial secret.</summary>
    public static string MaskSecrets(string text)
    {
        string t = text;
        t = TcNoRx().Replace(t, "•••••••••••");
        t = CardNoRx().Replace(t, m => new string('•', m.Value.Length));
        t = SecretKvRx().Replace(t, m => m.Groups[1].Value + ": •••gizli•••");
        t = OpaqueTokenRx().Replace(t, "•••gizli•••");
        return t;
    }

    [GeneratedRegex(@"\b\d{11}\b")] private static partial Regex TcNoRx();
    [GeneratedRegex(@"\b\d(?:[-\s]?\d){12,18}\b")] private static partial Regex CardNoRx();
    [GeneratedRegex(@"(?i)\b(api[_-]?key|access[_-]?token|secret|password|şifre|sifre|pwd|pass|token)\b\s*[:=]\s*\S+")] private static partial Regex SecretKvRx();
    [GeneratedRegex(@"(?i)\b(sk-[a-z0-9]{10,}|gh[a-z]_[a-z0-9]{20,}|xox[baprs]-[a-z0-9-]{6,})\b")] private static partial Regex OpaqueTokenRx();
}
