// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Text.Json;
using Ordinaryunus.Bridge;
using Ordinaryunus.Data;
using Ordinaryunus.Host;
using Ordinaryunus.Jobs;
using Ordinaryunus.Security;

namespace Ordinaryunus.Shots;

/// <summary>--dump-json &lt;dir&gt;: read-only, real vault content, must be under %TEMP% (privacy). Reuses the real
/// Handlers so the dump is exactly what the bridge would answer (MIMARI §9.2).</summary>
public static class JsonDump
{
    public static int Run(string dir, string? vault)
    {
        string full = Path.GetFullPath(dir);
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\');
        if (!full.StartsWith(tempRoot + "\\", StringComparison.OrdinalIgnoreCase) && !string.Equals(full, tempRoot, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Hedef klasör %TEMP% altında olmalı (gerçek kasa içeriği taşınmasın diye).");
            return 6;
        }
        Directory.CreateDirectory(full);
        VaultWriter.ReadOnlyMode = true;
        SettingsStore.ReadOnly = true;
        Launch.Disabled = true;

        var settings = SettingsStore.Load();
        if (vault is { Length: > 0 }) settings.KasaYolu = vault;
        var app = new AppState(settings, dryRun: false, screenshotMode: false) { Unlocked = true };
        app.ReloadAsync(true).GetAwaiter().GetResult();
        app.RefreshLightsOnly(DateTime.Now);
        app.RefreshUsage(TimeSpan.FromSeconds(3));

        var bridge = new BridgeHost(app, _ => { });
        var handlers = new Handlers(app, bridge);
        var writeOpts = new JsonSerializerOptions(BridgeHost.JsonOptions) { WriteIndented = true };

        void Write(string name, object value) => File.WriteAllText(Path.Combine(full, name), JsonSerializer.Serialize(value, value.GetType(), writeOpts));

        Write("snapshot.json", Mapper.BuildSnapshot(app));
        Write("overview.json", handlers.Dispatch("getBrainOverview", new object()).GetAwaiter().GetResult());
        Write("tree.json", handlers.Dispatch("getBrainTree", new object()).GetAwaiter().GetResult());
        Write("problems.json", handlers.Dispatch("getProblems", new object()).GetAwaiter().GetResult());
        Write("jobs.json", handlers.Dispatch("getJobs", new object()).GetAwaiter().GetResult());
        Write("settings.json", handlers.Dispatch("getSettings", new object()).GetAwaiter().GetResult());
        Write("graph.json", handlers.Dispatch("getGraph", new GetGraphPayload(null)).GetAwaiter().GetResult());

        var firstActive = app.Snapshot.Projects.FirstOrDefault(p => p.Durum == "aktif") ?? app.Snapshot.Projects.FirstOrDefault();
        if (firstActive is not null)
        {
            Write("project.json", handlers.Dispatch("getProjectDetail", new GetProjectDetailPayload(firstActive.Name)).GetAwaiter().GetResult());
            string cardPath = $"20 Projeler/{firstActive.Name}/{firstActive.Name}.md";
            if (app.Index?.Find(cardPath) is not null)
                Write("note.json", handlers.Dispatch("getNote", new GetNotePayload(cardPath)).GetAwaiter().GetResult());
        }

        Console.WriteLine($"Yazıldı: {full} (snapshot, overview, tree, note, project, graph, problems, jobs, settings)");
        return 0;
    }
}
