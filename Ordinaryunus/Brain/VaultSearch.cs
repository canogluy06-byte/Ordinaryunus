// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Net;
using System.Text;
using Ordinaryunus.Bridge;
using Ordinaryunus.Data;

namespace Ordinaryunus.Brain;

/// <summary>Fold + score + snippet vault search (MIMARI §5.7). Folded text is 1:1 with the original, so a hit's
/// index in the folded text is also its index in the plain text (needed for the snippet window).</summary>
public static class VaultSearch
{
    public static (List<SearchResultDto> results, int total, long tookMs) Search(BrainIndex index, IReadOnlyList<LedgerEntry> ledger, string q, int limit)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var terms = Md.Fold1to1(q).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var scored = new List<(double score, SearchResultDto dto)>();
        if (terms.Length > 0)
        {
            foreach (var note in index.Notes.Values)
            {
                string titleFold = Md.Fold1to1(note.Title);
                string pathFold = Md.Fold1to1(note.RelPath);
                bool all = terms.All(t => titleFold.Contains(t, StringComparison.Ordinal) || pathFold.Contains(t, StringComparison.Ordinal) ||
                    note.FoldedText.Contains(t, StringComparison.Ordinal));
                if (!all) continue;
                double score = 0;
                foreach (var t in terms)
                {
                    if (titleFold.Contains(t, StringComparison.Ordinal)) score += 10;
                    if (note.Headings.Any(h => Md.Fold1to1(h.Text).Contains(t, StringComparison.Ordinal))) score += 4;
                    if (pathFold.Contains(t, StringComparison.Ordinal)) score += 3;
                    score += Math.Min(CountOccurrences(note.FoldedText, t), 10);
                }
                if ((DateTime.Now - note.Mtime.ToLocalTime()).TotalDays <= 7) score += 2;
                string snippet = BuildSnippet(note.PlainText, note.FoldedText, terms);
                scored.Add((score, new SearchResultDto("note", note.RelPath, note.Title, note.Kind, note.Project, snippet, null, score,
                    new NoteTarget(note.RelPath, null))));
            }
            int ledgerCount = 0;
            foreach (var e in ledger.Where(e => !e.Auto).OrderByDescending(e => e.T))
            {
                if (ledgerCount >= 10) break;
                string folded = Md.Fold1to1(e.Metin);
                if (!terms.All(t => folded.Contains(t, StringComparison.Ordinal))) continue;
                ledgerCount++;
                string snippet = BuildSnippet(e.Metin, folded, terms);
                string day = DateOnly.FromDateTime(e.Local).ToString("yyyy-MM-dd");
                scored.Add((5, new SearchResultDto("istek", null, e.Proje.Length > 0 ? e.Proje : "İstek", null,
                    e.Proje.Length > 0 ? e.Proje : null, snippet, null, 5, new LedgerTarget(day))));
            }
        }
        var ordered = scored.OrderByDescending(s => s.score).Take(Math.Clamp(limit, 1, 50)).Select(s => s.dto).ToList();
        return (ordered, scored.Count, sw.ElapsedMilliseconds);
    }

    static int CountOccurrences(string haystack, string needle)
    {
        if (needle.Length == 0) return 0;
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }

    static string BuildSnippet(string plain, string folded, string[] terms)
    {
        int hitPos = -1, hitLen = 0;
        foreach (var t in terms)
        {
            int idx = folded.IndexOf(t, StringComparison.Ordinal);
            if (idx >= 0 && (hitPos < 0 || idx < hitPos)) { hitPos = idx; hitLen = t.Length; }
        }
        if (hitPos < 0) return WebUtility.HtmlEncode(Md.Clip(plain, 120));
        int start = Math.Max(0, hitPos - 60);
        int end = Math.Min(plain.Length, hitPos + hitLen + 60);
        string seg = plain[start..end];
        string foldedSeg = folded[start..end];
        var hits = new List<(int start, int len)>();
        foreach (var t in terms)
        {
            int from = 0;
            while (true)
            {
                int idx = foldedSeg.IndexOf(t, from, StringComparison.Ordinal);
                if (idx < 0) break;
                hits.Add((idx, t.Length));
                from = idx + t.Length;
            }
        }
        hits.Sort((a, b) => a.start.CompareTo(b.start));
        var sb = new StringBuilder();
        int cur = 0;
        foreach (var (s, len) in hits)
        {
            if (s < cur) continue;
            if (s > cur) sb.Append(WebUtility.HtmlEncode(seg[cur..s]));
            sb.Append("<mark>").Append(WebUtility.HtmlEncode(seg[s..(s + len)])).Append("</mark>");
            cur = s + len;
        }
        if (cur < seg.Length) sb.Append(WebUtility.HtmlEncode(seg[cur..]));
        return (start > 0 ? "… " : "") + sb + (end < plain.Length ? " …" : "");
    }
}
