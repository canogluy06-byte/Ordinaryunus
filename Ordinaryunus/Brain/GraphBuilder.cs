// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using Ordinaryunus.Bridge;

namespace Ordinaryunus.Brain;

/// <summary>Wikilink force-graph data (MIMARI §5.11).</summary>
public static class GraphBuilder
{
    static readonly string[] KindOrder =
    [
        "simdi", "sistem", "proje-karti", "kayit", "gorev", "proje-notu", "proje-listesi", "gelen", "fikir", "alan",
        "arastirma", "ham", "kaynak", "rol", "ekip", "gunluk", "arsiv", "sablon", "not", "eksik", "dosya",
    ];
    static readonly Dictionary<string, string> Labels = new()
    {
        ["simdi"] = "Şimdi", ["sistem"] = "Sistem", ["proje-karti"] = "Proje kartı", ["kayit"] = "Kayıt", ["gorev"] = "Görev",
        ["proje-notu"] = "Proje notu", ["proje-listesi"] = "Proje listesi", ["gelen"] = "Gelen kutusu", ["fikir"] = "Fikir",
        ["alan"] = "Alan", ["arastirma"] = "Araştırma", ["ham"] = "Ham kaynak", ["kaynak"] = "Kaynak", ["rol"] = "Rol",
        ["ekip"] = "Ekip", ["gunluk"] = "Günlük", ["arsiv"] = "Arşiv", ["sablon"] = "Şablon", ["not"] = "Not",
        ["eksik"] = "Eksik not", ["dosya"] = "Dosya",
    };

    public static GraphData Build(BrainIndex index, string? project)
    {
        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var linkCounts = new Dictionary<(string src, string dst), int>();
        foreach (var (src, list) in index.Resolved)
            foreach (var (target, _) in list)
            {
                nodeIds.Add(src); nodeIds.Add(target);
                var key = (src, target);
                linkCounts[key] = linkCounts.GetValueOrDefault(key) + 1;
            }

        var ghostIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (src, list) in index.Broken)
        {
            foreach (var link in list)
            {
                if (ghostIds.Count >= 100 && !ghostIds.ContainsKey(link.Target)) continue;
                if (!ghostIds.TryGetValue(link.Target, out var gid)) ghostIds[link.Target] = gid = "ghost:" + WikiLinkResolver.Fold(link.Target);
                nodeIds.Add(src); nodeIds.Add(gid);
                var key = (src, gid);
                linkCounts[key] = linkCounts.GetValueOrDefault(key) + 1;
            }
        }

        if (project is not null)
        {
            var own = index.Notes.Values.Where(n => string.Equals(n.Project, project, StringComparison.OrdinalIgnoreCase))
                .Select(n => n.RelPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var neighbours = new HashSet<string>(own, StringComparer.OrdinalIgnoreCase);
            foreach (var (s, t) in linkCounts.Keys)
            {
                if (own.Contains(s)) neighbours.Add(t);
                if (own.Contains(t)) neighbours.Add(s);
            }
            nodeIds.IntersectWith(neighbours);
            linkCounts = linkCounts.Where(kv => nodeIds.Contains(kv.Key.src) && nodeIds.Contains(kv.Key.dst)).ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        var degree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var ((s, t), c) in linkCounts)
        {
            degree[s] = degree.GetValueOrDefault(s) + c;
            degree[t] = degree.GetValueOrDefault(t) + c;
        }

        bool truncated = false;
        var finalIds = nodeIds;
        if (finalIds.Count > 1500)
        {
            finalIds = finalIds.OrderByDescending(id => degree.GetValueOrDefault(id)).Take(800).ToHashSet(StringComparer.OrdinalIgnoreCase);
            truncated = true;
            linkCounts = linkCounts.Where(kv => finalIds.Contains(kv.Key.src) && finalIds.Contains(kv.Key.dst)).ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        var kindsPresent = new HashSet<string>();
        var raw = new List<(string id, string name, string kind, int degree, string? project, bool missing, string? modified)>();
        foreach (var id in finalIds)
        {
            if (id.StartsWith("ghost:", StringComparison.Ordinal))
            {
                string label = ghostIds.FirstOrDefault(kv => kv.Value == id).Key ?? id;
                kindsPresent.Add("eksik");
                raw.Add((id, label, "eksik", degree.GetValueOrDefault(id), null, true, null));
            }
            else if (index.Notes.TryGetValue(id, out var n))
            {
                kindsPresent.Add(n.Kind);
                raw.Add((id, n.Title, n.Kind, degree.GetValueOrDefault(id), n.Project, false, n.Mtime.ToLocalTime().ToString("o")));
            }
            else if (index.Files.TryGetValue(id, out var f))
            {
                kindsPresent.Add("dosya");
                raw.Add((id, System.IO.Path.GetFileName(id), "dosya", degree.GetValueOrDefault(id), null, false, f.Mtime.ToLocalTime().ToString("o")));
            }
        }
        var orderedKinds = KindOrder.Where(kindsPresent.Contains).ToList();
        var catIndex = orderedKinds.Select((k, i) => (k, i)).ToDictionary(x => x.k, x => x.i);
        var nodes = raw.Select(r => new GraphNodeDto(r.id, r.name, r.kind, catIndex[r.kind], r.degree, r.project, r.missing, r.modified)).ToList();
        var links = linkCounts.Select(kv => new GraphLinkDto(kv.Key.src, kv.Key.dst, kv.Value)).ToList();
        var categories = orderedKinds.Select(k => new GraphCategoryDto(Labels.GetValueOrDefault(k, k), k)).ToList();
        return new GraphData(nodes, links, categories, new GraphStatsDto(nodes.Count, links.Count, nodes.Count(n => n.Missing), truncated));
    }
}
