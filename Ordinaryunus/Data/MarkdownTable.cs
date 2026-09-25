// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
namespace Ordinaryunus.Data;

/// <summary>A pipe table found in markdown lines. Rows keep their original line for safe writes.</summary>
public sealed class MarkdownTable
{
    public List<string> Headers { get; } = [];
    public List<TableRow> Rows { get; } = [];

    /// <summary>Column index for a header by normalized name (Turkish-insensitive), or -1.</summary>
    public int Col(params string[] names)
    {
        for (int i = 0; i < Headers.Count; i++)
        {
            string h = Md.Normalize(Md.Plain(Headers[i]));
            foreach (var n in names)
                if (h == Md.Normalize(n)) return i;
        }
        return -1;
    }

    public static bool IsTableLine(string line)
    {
        string t = line.Trim();
        return t.StartsWith('|') && t.Length > 1;
    }

    static bool IsSeparator(string line)
    {
        var cells = SplitCells(line);
        return cells.Count > 0 && cells.All(c => c.Trim().Length > 0 && c.Trim().All(ch => ch is '-' or ':' or ' '));
    }

    /// <summary>Parses all tables in the given lines. <paramref name="lineOffset"/> maps to file line numbers.</summary>
    public static List<MarkdownTable> ParseAll(IReadOnlyList<string> lines, int lineOffset = 0)
    {
        var tables = new List<MarkdownTable>();
        for (int i = 0; i < lines.Count; i++)
        {
            if (!IsTableLine(lines[i])) continue;
            if (i + 1 >= lines.Count || !IsSeparator(lines[i + 1])) continue;
            var t = new MarkdownTable();
            t.Headers.AddRange(SplitCells(lines[i]).Select(c => c.Trim()));
            int j = i + 2;
            for (; j < lines.Count && IsTableLine(lines[j]); j++)
            {
                var cells = SplitCells(lines[j]).Select(c => c.Trim()).ToList();
                t.Rows.Add(new TableRow(lineOffset + j, lines[j], cells));
            }
            tables.Add(t);
            i = j - 1;
        }
        return tables;
    }

    /// <summary>Splits a table row into raw cell strings (without the outer pipes). Respects "\|".</summary>
    public static List<string> SplitCells(string line)
    {
        var spans = CellSpans(line);
        return spans.Select(s => line.Substring(s.start, s.length)).ToList();
    }

    /// <summary>Start/length of each cell's raw content (between pipes) in the original line.</summary>
    public static List<(int start, int length)> CellSpans(string line)
    {
        var result = new List<(int, int)>();
        int first = line.IndexOf('|');
        if (first < 0) return result;
        int cellStart = first + 1;
        for (int i = first + 1; i < line.Length; i++)
        {
            if (line[i] == '\\' && i + 1 < line.Length) { i++; continue; }
            if (line[i] == '|')
            {
                result.Add((cellStart, i - cellStart));
                cellStart = i + 1;
            }
        }
        // trailing text without closing pipe counts as a cell if non-empty
        if (cellStart < line.Length && line[cellStart..].Trim().Length > 0)
            result.Add((cellStart, line.Length - cellStart));
        return result;
    }

    /// <summary>Returns the line with the trimmed content of cell <paramref name="col"/> replaced, keeping padding.</summary>
    public static string ReplaceCell(string line, int col, string newValue)
    {
        var spans = CellSpans(line);
        if (col < 0 || col >= spans.Count) throw new ArgumentOutOfRangeException(nameof(col));
        var (start, length) = spans[col];
        string raw = line.Substring(start, length);
        int lead = raw.Length - raw.TrimStart().Length;
        int trail = raw.Length - raw.TrimEnd().Length;
        string padL = raw[..lead];
        string padR = raw.Trim().Length == 0 ? " " : raw[(raw.Length - trail)..];
        if (padL.Length == 0) padL = " ";
        if (padR.Length == 0) padR = " ";
        return line[..start] + padL + newValue + padR + line[(start + length)..];
    }
}

public sealed record TableRow(int LineIndex, string RawLine, List<string> Cells)
{
    public string Cell(int col) => col >= 0 && col < Cells.Count ? Cells[col] : "";
}
