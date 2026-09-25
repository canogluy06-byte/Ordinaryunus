// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Text;

namespace Ordinaryunus.Data;

/// <summary>Thrown when the file changed since it was read; the UI reloads and tells the user.</summary>
public sealed class StaleFileException(string message) : Exception(message);

/// <summary>
/// The only vault writes the app performs. Each write re-reads the file, verifies the exact original line,
/// replaces only that line, keeps newline style and BOM, and writes via a temp file + File.Replace.
/// </summary>
public static class VaultWriter
{
    /// <summary>Set by --selftest / --screenshots: any write attempt throws.</summary>
    public static bool ReadOnlyMode { get; set; }

    public const string StaleMessage = "Dosya az önce değişti, liste yenilendi";

    /// <summary>Flips "- [ ]" / "- [x]" on one line inside the Yapılacaklarım section of 01 Şimdi.md.</summary>
    public static string ToggleTodo(string filePath, TodoItem item)
    {
        GuardWritable();
        var doc = RawDoc.Load(filePath);
        var lines = doc.LogicalLines();
        int h = Md.SectionIndex(lines, VaultReader.TodoHeading);
        if (h < 0) throw new StaleFileException(StaleMessage);
        var matches = new List<int>();
        for (int i = h + 1; i < lines.Count; i++)
        {
            if (Md.IsHeadingAtOrAbove(lines[i], 2)) break;
            if (lines[i] == item.RawLine) matches.Add(i);
        }
        int target = matches.Contains(item.LineIndex) ? item.LineIndex : matches.Count == 1 ? matches[0] : -1;
        if (target < 0) throw new StaleFileException(StaleMessage);
        string newLine = ToggleCheckboxText(lines[target]);
        doc.ReplaceLine(target, newLine);
        doc.Save(filePath);
        return newLine;
    }

    public static string ToggleCheckboxText(string line)
    {
        int open = line.IndexOf('[');
        if (open < 0 || open + 2 >= line.Length || line[open + 2] != ']') throw new StaleFileException(StaleMessage);
        char mark = line[open + 1];
        char next = mark is 'x' or 'X' ? ' ' : 'x';
        return line[..(open + 1)] + next + line[(open + 2)..];
    }

    /// <summary>Changes the Durum cell from "Onay bekliyor" to "Onaylandı" for each row (all-or-nothing).</summary>
    public static int ApproveJobs(string filePath, IReadOnlyList<JobRow> rows)
    {
        GuardWritable();
        var doc = RawDoc.Load(filePath);
        var lines = doc.LogicalLines();
        var changes = new List<(int index, string line)>();
        var currentRows = VaultReader.ParseJobs(doc.ToText()).AllRows.ToList();
        foreach (var row in rows)
        {
            if (row.DurumCol < 0) throw new StaleFileException(StaleMessage);
            // Validate the current table schema and section as well as the row text.
            var matches = currentRows.Where(r => r.RawLine == row.RawLine && r.Section == row.Section &&
                r.DurumCol == row.DurumCol && r.AwaitingApproval).ToList();
            int idx = matches.Any(r => r.LineIndex == row.LineIndex) ? row.LineIndex :
                matches.Count == 1 ? matches[0].LineIndex : -1;
            if (idx < 0) throw new StaleFileException(StaleMessage);
            var cells = MarkdownTable.SplitCells(lines[idx]);
            if (row.DurumCol >= cells.Count || Md.Normalize(Md.Plain(cells[row.DurumCol])) != "onay bekliyor")
                throw new StaleFileException(StaleMessage);
            changes.Add((idx, MarkdownTable.ReplaceCell(lines[idx], row.DurumCol, "Onaylandı")));
        }
        foreach (var (index, line) in changes) doc.ReplaceLine(index, line);
        if (changes.Count > 0) doc.Save(filePath);
        return changes.Count;
    }

    /// <summary>Appends one line to a JSONL file (creates it if missing). Used only for safety decisions.</summary>
    public static void AppendJsonLine(string filePath, string jsonLine)
    {
        GuardWritable();
        string? dir = Path.GetDirectoryName(filePath);
        if (dir is not null) Directory.CreateDirectory(dir);
        string prefix = "";
        string nl = "\n";
        if (File.Exists(filePath))
        {
            var bytes = Md.ReadBytesShared(filePath);
            string text = Encoding.UTF8.GetString(bytes);
            if (text.Contains("\r\n")) nl = "\r\n";
            if (text.Length > 0 && !text.EndsWith('\n')) prefix = nl;
        }
        using var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        var payload = new UTF8Encoding(false).GetBytes(prefix + jsonLine + nl);
        fs.Write(payload);
    }

    static void GuardWritable()
    {
        if (ReadOnlyMode) throw new InvalidOperationException("Salt okunur modda yazma yapılamaz.");
    }

    /// <summary>Byte-faithful line model of a UTF-8 file: keeps BOM and every line's own terminator.</summary>
    public sealed class RawDoc
    {
        public bool Bom { get; private set; }
        readonly List<string> _content = [];   // line text without terminator
        readonly List<string> _terminators = []; // "\r\n", "\n", "\r" or "" (last line)

        public static RawDoc Load(string path)
        {
            byte[] bytes = Md.ReadBytesShared(path);
            var d = new RawDoc { Bom = Md.HasBom(bytes) };
            int off = d.Bom ? 3 : 0;
            string text = new UTF8Encoding(false, true).GetString(bytes, off, bytes.Length - off);
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n' || text[i] == '\r')
                {
                    string term = text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? "\r\n" : text[i].ToString();
                    d._content.Add(text[start..i]);
                    d._terminators.Add(term);
                    i += term.Length - 1;
                    start = i + 1;
                }
            }
            d._content.Add(text[start..]);
            d._terminators.Add("");
            return d;
        }

        public List<string> LogicalLines() => [.. _content];

        public void ReplaceLine(int index, string newContent)
        {
            if (newContent.Contains('\n') || newContent.Contains('\r')) throw new ArgumentException("Tek satır olmalı");
            _content[index] = newContent;
        }

        public string ToText()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _content.Count; i++) sb.Append(_content[i]).Append(_terminators[i]);
            return sb.ToString();
        }

        public void Save(string path)
        {
            var enc = new UTF8Encoding(false);
            byte[] body = enc.GetBytes(ToText());
            byte[] all = Bom ? [0xEF, 0xBB, 0xBF, .. body] : body;
            WriteAtomic(path, all);
        }
    }

    /// <summary>Writes via a temp file then File.Replace (same volume), falling back to a sibling temp file.</summary>
    public static void WriteAtomic(string path, byte[] bytes)
    {
        GuardWritable();
        string tmpDir = Path.Combine(AppPaths.DataDir, "tmp");
        Directory.CreateDirectory(tmpDir);
        string tmp = Path.Combine(tmpDir, Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllBytes(tmp, bytes);
        try
        {
            File.Replace(tmp, path, null, ignoreMetadataErrors: true);
        }
        catch (IOException)
        {
            // Different volume: use a temp file next to the target (not .md, so Obsidian ignores it).
            string sibling = path + ".ordinaryunus-tmp";
            File.Move(tmp, sibling, overwrite: true);
            File.Replace(sibling, path, null, ignoreMetadataErrors: true);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }
}
