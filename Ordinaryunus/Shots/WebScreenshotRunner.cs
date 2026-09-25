// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Ordinaryunus.Data;
using Ordinaryunus.Host;
using Ordinaryunus.Security;

namespace Ordinaryunus.Shots;

/// <summary>--screenshots: real (read-only) data, via CapturePreviewAsync (MIMARI §9.3).
///
/// WebView2 is a COM/STA component: every call and every async continuation must happen on the same thread that
/// keeps pumping Windows messages. `Application.Run` normally does both (installs a
/// <see cref="System.Windows.Forms.WindowsFormsSynchronizationContext"/> and pumps), but this mode drives the
/// window programmatically instead of showing it interactively, so both are set up by hand: the sync context is
/// installed once on the dedicated STA thread, and every wait below pumps with <see cref="Application.DoEvents"/>
/// in a loop rather than a bare `await Task.Delay(...)` (which — with nothing pumping in between — never lets
/// WebView2's IPC complete, so it silently produces empty PNGs).
/// </summary>
public static class WebScreenshotRunner
{
    static readonly (string name, string page, string? tab, string? open, string? query)[] Shots =
    [
        ("masam", "masam", null, null, null),
        ("beyin-genel", "beyin", "genel", null, null),
        // Keşfet bir not açık hâlde çekilir (okuyucu ve bağlantı paneli dolu görünsün; not aşağıda seçilir).
        ("beyin-kesif", "beyin", "kesif", null, null),
        ("beyin-proje", "beyin", "proje", null, null),
        // Harita: grafik yerleşince sayfa grafik kartına kaydırılır, kart kesilmeden görünür.
        ("beyin-harita", "beyin", "harita", "harita", null),
        ("beyin-sorunlar", "beyin", "sorunlar", null, null),
        ("sirketim", "sirketim", null, null, null),
        ("gecmis", "gecmis", null, null, null),
        ("basvurular", "basvurular", null, null, null),
        ("projeler", "projeler", null, null, null),
        ("kestirmeler", "kestirmeler", null, null, null),
        // Ayarlar sayfanın altına (Bağlantılar + Hakkında kartı: imza, sürüm, lisans) kaydırılarak çekilir.
        ("ayarlar", "ayarlar", null, "hakkinda", null),
        ("is-ver", "masam", null, "givework", null),
        ("odak", "masam", null, "focus", null),
        ("tam-gaz", "sirketim", null, "tamgaz", null),
        ("arama", "masam", null, "palette", "ordinary"),
    ];

    public static int Run(string outDir, Size size, string? vault, double scale, string theme = "acik")
    {
        int code = 0;
        var thread = new Thread(() =>
        {
            try { code = RunOnStaThread(outDir, size, vault, scale, theme); }
            catch (Exception ex) { Console.Error.WriteLine(ex); code = 3; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        // A WebView2/COM deadlock (observed after a renderer crash on one page) can wedge Application.DoEvents()
        // itself, which no per-shot timeout inside RunOnStaThread can catch. This outer watchdog guarantees the
        // process still exits instead of hanging a CI run forever; whatever PNGs were written up to that point
        // are kept.
        if (!thread.Join(TimeSpan.FromMinutes(8)))
        {
            Console.Error.WriteLine("Zaman aşımı: ekran görüntüsü alma 8 dakikada bitmedi (WebView2 kilitlenmiş olabilir); işlem sonlandırılıyor.");
            Environment.Exit(7);
        }
        return code;
    }

    static int RunOnStaThread(string outDir, Size size, string? vault, double scale, string theme)
    {
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Forms.WindowsFormsSynchronizationContext());

        Directory.CreateDirectory(outDir);
        // Şirketim ve Ayarlar, profil klasöründeki .claude/.codex bilgilerini (oturum başlıkları, zamanlanmış görevler,
        // hook'lar) gösterir. Paylaşılacak görüntülerde gerçek bilgiler çıkmasın diye uydurma bir profil verilmeli.
        if (Environment.GetEnvironmentVariable(AppPaths.ProfileEnvVar) is not { Length: > 0 })
            Console.WriteLine($"Uyarı: {AppPaths.ProfileEnvVar} verilmedi; görüntülerde bu bilgisayarın gerçek Claude/Codex " +
                              "oturumları ve zamanlanmış görevleri görünebilir. Paylaşmadan önce uydurma bir profil klasörü ver.");
        VaultWriter.ReadOnlyMode = true;
        SettingsStore.ReadOnly = true;
        Launch.Disabled = true;

        var settings = SettingsStore.Load();
        if (vault is { Length: > 0 }) settings.KasaYolu = vault;
        settings.Tema = theme is "koyu" ? "koyu" : "acik";

        var app = new AppState(settings, dryRun: false, screenshotMode: true);
        using var form = new WebHostForm(app, RunMode.Screenshots) { Size = size, StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };

        bool ready = false;
        form.Ready += () => ready = true;
        form.Show();
        Pump(() => ready, 20_000);
        try { form.WebControl.ZoomFactor = scale; } catch { }

        string suffix = theme == "koyu" ? "-koyu" : "";
        bool sawAnyPageReady = WaitPageReady(app);
        Capture(form, Path.Combine(outDir, "giris" + suffix + ".png"));

        app.ForceUnlockForScreenshots();
        var reloadTask = app.ReloadAsync(true);
        Pump(() => reloadTask.IsCompleted, 15_000);

        // "beyin-proje" needs a real project segment in the URL (#/beyin/proje/<name>) or the page has nothing to
        // show yet (MIMARI §3.4 Target "project" always names one) — pick whichever project this vault actually has.
        // Önce odaktaki proje (en dolu kart), yoksa ilk proje.
        string? sampleProject = (app.Snapshot.Projects.FirstOrDefault(p => p.Odak) ?? app.Snapshot.Projects.FirstOrDefault())?.Name;

        foreach (var (name, page, tab0, open, query) in Shots)
        {
            string? tab = sampleProject is null ? tab0 : name switch
            {
                "beyin-proje" => "proje/" + Uri.EscapeDataString(sampleProject),
                "beyin-kesif" => "kesif?path=" + Uri.EscapeDataString($"20 Projeler/{sampleProject}/{sampleProject}.md"),
                _ => tab0,
            };
            if (form.IsDisposed)
            {
                Console.WriteLine($"Uyarı: WebView2 kapandı (muhtemelen çöktü); \"{name}\" ve sonrası atlanıyor.");
                break;
            }
            bool gotReady = false;
            app.OnPageReady = (_, _) => gotReady = true;
            var postTask = form.PostShotEventAsync(new { page, tab, open, note = (string?)null, query });
            Pump(() => postTask.IsCompleted, 5_000);
            Pump(() => gotReady, 15_000);
            sawAnyPageReady |= gotReady;
            PumpFor(300); // let the page finish its own paint/animation before capturing
            Capture(form, Path.Combine(outDir, name + suffix + ".png"));
        }

        if (!sawAnyPageReady)
            Console.WriteLine("Uyarı: wwwroot \"pageReady\" göndermedi (frontend hazır değil ya da sayfa yüklenemedi); ekran görüntüleri boş/hata sayfası olabilir.");
        form.Close();
        Pump(() => false, 200); // drain pending Win32 messages before the STA thread exits
        return 0;
    }

    static bool WaitPageReady(AppState app)
    {
        bool got = false;
        app.OnPageReady = (_, _) => got = true;
        Pump(() => got, 15_000);
        PumpFor(300);
        return got;
    }

    static void Capture(WebHostForm form, string path)
    {
        try
        {
            if (form.Core is null) { File.WriteAllBytes(path, []); return; }
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            var task = form.Core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
            Pump(() => task.IsCompleted, 15_000);
            if (task.Exception is not null) throw task.Exception;
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(path + ".hata.txt", ex.ToString()); } catch { }
        }
    }

    /// <summary>Pumps the Windows message queue (required for WebView2's async IPC to ever complete) until
    /// <paramref name="done"/> is true or the timeout elapses.</summary>
    static void Pump(Func<bool> done, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (!done() && sw.ElapsedMilliseconds < timeoutMs)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    static void PumpFor(int ms) => Pump(() => false, ms);
}
