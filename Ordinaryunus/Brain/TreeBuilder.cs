// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using Ordinaryunus.Bridge;
using Ordinaryunus.Data;
using TreeNode = Ordinaryunus.Bridge.TreeNode;

namespace Ordinaryunus.Brain;

/// <summary>Curated vault tree for the Keşfet pane (MIMARI §5.10). A pragmatic subset of the full spec: the main
/// groups and project sub-tree are exact; "Tüm dosyalar" is a generic recursive folder tree.</summary>
public static class TreeBuilder
{
    public static List<TreeNode> Build(BrainIndex index, VaultSnapshot snap, IReadOnlyDictionary<string, List<KayitEntry>> kayitByProject,
        IReadOnlyDictionary<string, string> projectHealth)
    {
        var roots = new List<TreeNode>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Başlangıç
        var startNames = new[] { "01 Şimdi.md", "00 Başla Buradan.md", "04 İstek Defteri.md", "AGENTS.md", "CLAUDE.md" };
        var startChildren = new List<TreeNode>();
        foreach (var name in startNames)
            if (index.Notes.TryGetValue(name, out var n)) { startChildren.Add(NoteNode(n)); used.Add(name); }
        if (startChildren.Count > 0) roots.Add(Group("g:baslangic", "Başlangıç", "home", startChildren));

        // 2. Projeler
        var projectChildren = new List<TreeNode>();
        foreach (var p in snap.Projects.OrderByDescending(p => p.Durum == "aktif" && p.Odak).ThenBy(p => p.Durum != "aktif").ThenBy(p => p.Name, StringComparer.Create(Md.Tr, true)))
        {
            var kids = new List<TreeNode>();
            string cardPath = $"20 Projeler/{p.Name}/{p.Name}.md";
            if (index.Notes.TryGetValue(cardPath, out var card)) { kids.Add(NoteNode(card, "Kart")); used.Add(cardPath); }
            string kayitPath = $"20 Projeler/{p.Name}/Kayıt.md";
            var entries = kayitByProject.GetValueOrDefault(p.Name, []);
            if (index.Notes.TryGetValue(kayitPath, out var kayitNote))
            {
                used.Add(kayitPath);
                var entryNodes = entries.OrderByDescending(e => e.Date).ThenByDescending(e => e.Time ?? "")
                    .Where(e => e.Kind == "devir")
                    .Select(e => new TreeNode($"k:{kayitPath}#{e.Anchor}", $"{e.Date:d MMM} · {e.Tool ?? "?"} · {e.Title}", "kayit", "kayit",
                        null, null, null, null, new NoteTarget(kayitPath, e.Anchor), [])).ToList();
                kids.Add(new TreeNode($"n:{kayitPath}", "Kayıt", "note", "kayit", "kayit", null, null, entryNodes.Count,
                    new NoteTarget(kayitPath, null), entryNodes));
            }
            var kararlar = entries.Where(e => e.Kind == "karar").Select(e =>
                new TreeNode($"k:{kayitPath}#{e.Anchor}", $"{e.Date:d MMM} · {e.Title}", "karar", "karar", null, null, null, null,
                    new NoteTarget(kayitPath, e.Anchor), [])).ToList();
            if (kararlar.Count > 0) kids.Add(Group($"g:kararlar:{p.Name}", $"Kararlar ({kararlar.Count})", "karar", kararlar));
            var tasks = snap.Tasks.Where(t => t.Project == p.Name).ToList();
            if (tasks.Count > 0)
            {
                var taskNodes = tasks.Select(t => new TreeNode($"n:{t.RelPath}", t.Kimlik.Length > 0 ? t.Kimlik : t.Title, "note", "task",
                    "gorev", new TreeBadgeDto(t.Durum, DurumTone(t.Durum)), null, null, new NoteTarget(t.RelPath, null), [])).ToList();
                foreach (var t in tasks) used.Add(t.RelPath);
                kids.Add(Group($"g:gorevler:{p.Name}", $"Görevler ({tasks.Count})", "task", taskNodes));
            }
            var latest = KayitParser.LatestDevir(entries);
            string acik = latest?.Get("Açık kalan") ?? "";
            if (acik.Trim().TrimEnd('.').ToLowerInvariant() is not ("" or "yok" or "-"))
                kids.Add(new TreeNode($"g:acik:{p.Name}", "Açık kalanlar", "view", "warning", null, null, null, null,
                    new ProjectTarget(p.Name, "acik"), []));
            var projectPrefix = $"20 Projeler/{p.Name}/";
            var others = index.Files.Keys.Where(k => k.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase) && !used.Contains(k)).ToList();
            foreach (var o in others) used.Add(o);
            if (others.Count > 0) kids.Add(Group($"g:digernot:{p.Name}", $"Diğer notlar ({others.Count})", "folder", FolderTree(others, index)));

            string? health = projectHealth.GetValueOrDefault(p.Name);
            projectChildren.Add(new TreeNode($"p:{p.Name}", p.Name, "project", "project", null,
                new TreeBadgeDto(p.Durum, DurumTone(p.Durum)), health, null, new ProjectTarget(p.Name, "genel"), kids));
        }
        if (index.Notes.TryGetValue("20 Projeler/Bitenler.md", out var bitenler)) { projectChildren.Add(NoteNode(bitenler)); used.Add("20 Projeler/Bitenler.md"); }
        if (index.Notes.TryGetValue("20 Projeler/_Envanter.md", out var envanter)) { projectChildren.Add(NoteNode(envanter)); used.Add("20 Projeler/_Envanter.md"); }
        if (projectChildren.Count > 0) roots.Add(Group("g:projeler", "Projeler", "project", projectChildren));

        void AddFolderGroup(string id, string label, string icon, string prefix)
        {
            var files = index.Files.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !used.Contains(k)).ToList();
            foreach (var f in files) used.Add(f);
            if (files.Count > 0) roots.Add(Group(id, label, icon, FolderTree(files, index)));
        }
        AddFolderGroup("g:alanlar", "Alanlar", "area", "40 Alanlar/");
        AddFolderGroup("g:park", "Park Yeri", "idea", "30 Park Yeri/");
        AddFolderGroup("g:gelen", "Gelen Kutusu", "inbox", "10 Gelen Kutusu/");
        AddFolderGroup("g:kaynaklar", "Kaynaklar", "research", "50 Kaynaklar/");

        // 7. Ekip
        var ekipChildren = new List<TreeNode>();
        var roleGroups = snap.Roles.GroupBy(r => r.Departman.Length > 0 ? r.Departman : "Diğer");
        var roleNodes = new List<TreeNode>();
        foreach (var g in roleGroups)
        {
            var kids = g.Select(r => new TreeNode($"n:{r.RelPath}", r.Title, "note", "role", "rol", new TreeBadgeDto(r.Arac, "teal"), null,
                null, new NoteTarget(r.RelPath, null), [])).ToList();
            foreach (var r in g) used.Add(r.RelPath);
            roleNodes.Add(Group($"g:dept:{g.Key}", g.Key, "role", kids));
        }
        if (roleNodes.Count > 0) ekipChildren.Add(Group("g:roller", "Roller", "role", roleNodes));
        var otherEkip = index.Files.Keys.Where(k => k.StartsWith("60 Ekip/", StringComparison.OrdinalIgnoreCase) && !used.Contains(k)).ToList();
        foreach (var f in otherEkip) used.Add(f);
        ekipChildren.AddRange(FolderTree(otherEkip, index));
        if (ekipChildren.Count > 0) roots.Add(Group("g:ekip", "Ekip", "role", ekipChildren));

        AddFolderGroup("g:gunluk", "Günlük", "journal", "70 Günlük/");

        // 9. İstek defteri, by day (last 30 days)
        var byDay = snap.Ledger.Where(e => !e.Auto).GroupBy(e => DateOnly.FromDateTime(e.Local)).OrderByDescending(g => g.Key).Take(30);
        var dayNodes = byDay.Select(g => new TreeNode($"l:{g.Key:yyyy-MM-dd}", $"{g.Key:d MMMM} · {g.Count()} istek", "ledgerDay", "ledger",
            null, null, null, g.Count(), new LedgerTarget(g.Key.ToString("yyyy-MM-dd")), [])).ToList();
        if (dayNodes.Count > 0) roots.Add(Group("g:istek", "İstek defteri", "ledger", dayNodes));

        AddFolderGroup("g:sistem", "Sistem", "system", "_sistem/");
        AddFolderGroup("g:arsiv", "Arşiv", "archive", "90 Arşiv/");

        var rest = index.Files.Keys.Where(k => !used.Contains(k)).ToList();
        roots.Add(Group("g:tum", "Tüm dosyalar", "raw", FolderTree(rest, index)));

        return roots;
    }

    static TreeNode Group(string id, string label, string icon, List<TreeNode> children) =>
        new(id, label, "group", icon, null, null, null, children.Count == 0 ? null : children.Count, null, children);

    static TreeNode NoteNode(NoteInfo n, string? labelOverride = null) =>
        new($"n:{n.RelPath}", labelOverride ?? n.Title, "note", IconFor(n.Kind), n.Kind, null, null, null, new NoteTarget(n.RelPath, null), []);

    static string IconFor(string kind) => kind switch
    {
        "simdi" => "home", "proje-karti" or "proje-notu" or "proje-listesi" => "project", "kayit" => "kayit", "gorev" => "task",
        "rol" => "role", "fikir" => "idea", "alan" => "area", "arastirma" or "ham" or "kaynak" => "research", "gunluk" => "journal",
        "arsiv" => "archive", "sablon" => "template", "gelen" => "inbox", "sistem" => "system", _ => "doc",
    };

    static string DurumTone(string durum) => durum switch { "aktif" => "teal", "beklemede" => "yellow", "bitti" => "green", "donduruldu" => "grey",
        "hazir" => "grey", "verildi" => "yellow", "kontrol" => "yellow", "tamam" => "green", "iptal" => "grey", _ => "grey" };

    /// <summary>Generic recursive folder → file tree for any set of relPaths, used for the catch-all groups. Each
    /// recursion level splits only the still-unconsumed *remainder* of a path (never the full path again — an
    /// earlier version re-split the same first segment forever and stack-overflowed on the real vault).</summary>
    public static List<TreeNode> FolderTree(IEnumerable<string> relPaths, BrainIndex index) =>
        FolderTreeLevel(relPaths.Select(p => (full: p, rest: p)), index);

    static List<TreeNode> FolderTreeLevel(IEnumerable<(string full, string rest)> items, BrainIndex index)
    {
        var leaves = new List<(string full, string rest)>();
        var folders = new Dictionary<string, List<(string full, string rest)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            int slash = item.rest.IndexOf('/');
            if (slash < 0) leaves.Add(item);
            else
            {
                string top = item.rest[..slash];
                if (!folders.TryGetValue(top, out var l)) folders[top] = l = [];
                l.Add((item.full, item.rest[(slash + 1)..]));
            }
        }
        var nodes = new List<TreeNode>();
        foreach (var (folder, children) in folders.OrderBy(f => f.Key, StringComparer.Create(Md.Tr, true)))
            nodes.Add(Group($"f:{folder}:{folder.GetHashCode():x}", folder, "folder", FolderTreeLevel(children, index)));
        foreach (var (full, _) in leaves.OrderBy(l => l.rest, StringComparer.Create(Md.Tr, true)))
        {
            if (index.Notes.TryGetValue(full, out var n)) nodes.Add(NoteNode(n));
            else nodes.Add(new TreeNode($"file:{full}", System.IO.Path.GetFileName(full), "file", "file", null, null, null, null, null, []));
        }
        return nodes;
    }
}
