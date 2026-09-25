// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Globalization;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Ordinaryunus.Data;
using Ordinaryunus.Host;
using Ordinaryunus.Jobs;
using Ordinaryunus.Security;
using Ordinaryunus.Shots;

namespace Ordinaryunus;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = Md.Tr;
        CultureInfo.DefaultThreadCurrentUICulture = Md.Tr;
        CultureInfo.CurrentCulture = Md.Tr;
        CultureInfo.CurrentUICulture = Md.Tr;

        // --surum / --version: imza satırını (sürüm, yapan, lisans) konsola yazar; --selftest ile aynı yolla.
        if (args.Any(a => a.Equals("--surum", StringComparison.OrdinalIgnoreCase) || a.Equals("--version", StringComparison.OrdinalIgnoreCase)))
            return SelfTest.PrintLine(Imza.Satir);

        int i = Array.FindIndex(args, a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase));
        if (i >= 0)
        {
            string vault = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : SettingsStore.Load().KasaYolu;
            return SelfTest.Run(vault);
        }

        i = Array.FindIndex(args, a => a.Equals("--dump-json", StringComparison.OrdinalIgnoreCase));
        if (i >= 0)
        {
            string dir = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : Path.Combine(Path.GetTempPath(), "ordinaryunus-dump");
            string? vault = OptionValue(args, "--vault");
            return JsonDump.Run(dir, vault);
        }

        i = Array.FindIndex(args, a => a.Equals("--screenshots", StringComparison.OrdinalIgnoreCase));
        if (i >= 0)
        {
            string dir = i + 1 < args.Length && !args[i + 1].StartsWith("--")
                ? args[i + 1] : Path.Combine(AppContext.BaseDirectory, "test-kanitlari-v2");
            var size = new Size(1600, 1000);
            double scale = 1.0;
            string theme = "acik";
            string? vault = OptionValue(args, "--vault");
            if (OptionValue(args, "--size") is { } sz && sz.Split('x') is [var w, var h] && int.TryParse(w, out int wi) && int.TryParse(h, out int hi)) size = new Size(wi, hi);
            if (OptionValue(args, "--scale") is { } scs && double.TryParse(scs, NumberStyles.Float, CultureInfo.InvariantCulture, out var sc)) scale = sc;
            if (OptionValue(args, "--theme") is { } th) theme = th.Equals("dark", StringComparison.OrdinalIgnoreCase) ? "koyu" : "acik";
            try { return WebScreenshotRunner.Run(dir, size, vault, scale, theme); }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "ordinaryunus-screenshots-error.txt"), ex.ToString(), new UTF8Encoding(false));
                Console.Error.WriteLine(ex);
                return 3;
            }
        }

        bool dryRun = args.Any(a => a.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));
        if (dryRun)
        {
            string? dataDir = Environment.GetEnvironmentVariable("ORDINARYUNUS_DATA");
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\');
            bool underTemp = dataDir is { Length: > 0 } && Path.GetFullPath(dataDir).TrimEnd('\\')
                .StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase);
            if (!underTemp)
            {
                ApplicationConfiguration.Initialize();
                MessageBox.Show("Deneme modu (--dry-run), ORDINARYUNUS_DATA bir %TEMP% klasörünü gösterirken çalışır.",
                    "Ordinaryunus", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 5;
            }
        }

        Mutex? single = null;
        if (!dryRun)
        {
            single = new Mutex(true, @"Local\Ordinaryunus-tek-pencere", out bool first);
            if (!first)
            {
                ApplicationConfiguration.Initialize();
                MessageBox.Show("Ordinaryunus zaten açık. Görev çubuğundaki pencereye bak.", "Ordinaryunus", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception ex) ReportError(ex); };

        try { CoreWebView2Environment.GetAvailableBrowserVersionString(); }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show("Bu uygulama için Microsoft Edge WebView2 bileşeni gerekiyor. Windows Update'i çalıştırıp tekrar dene.",
                "Ordinaryunus", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 4;
        }

        if (dryRun)
        {
            VaultWriter.ReadOnlyMode = true;
            Launch.Disabled = true;
            SafetyQueue.DecisionsDirOverride = Path.Combine(AppPaths.DataDir, "guvenlik");
            JobStore.DryRun = true;
        }

        var settings = SettingsStore.Load();
        // D2c: sadece ilk açılışta (kullanıcı henüz yazı boyutu seçmediyse) her zaman %100 yerine ekrana göre okunaklı
        // bir değerle başla. Bu sadece bellekte uygulanır; kaydedilen, kullanıcının ilk gerçek seçimidir (setSettings,
        // Ctrl+±) ve YaziBoyutuSecildi'yi işaretler.
        if (!settings.YaziBoyutuSecildi)
            settings.YaziBoyutu = Host.ScreenDefaults.DefaultUiScale(Host.ScreenDefaults.CurrentLogicalWorkAreaWidth());
        var app = new Host.AppState(settings, dryRun, screenshotMode: false);
        Application.Run(new WebHostForm(app, dryRun ? RunMode.DryRun : RunMode.Normal));
        single?.Dispose();
        return 0;
    }

    static string? OptionValue(string[] args, string name)
    {
        int idx = Array.IndexOf(args, name);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }

    static void ReportError(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            File.AppendAllText(Path.Combine(AppPaths.DataDir, "hata.log"), $"{DateTime.Now:o}\n{ex}\n\n", new UTF8Encoding(false));
        }
        catch { }
        try
        {
            MessageBox.Show("Beklenmeyen bir sorun oldu, ama uygulama çalışmaya devam ediyor.\n\n" + ex.Message +
                            "\n\nAyrıntı: %APPDATA%\\Ordinaryunus\\hata.log", "Ordinaryunus", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch { }
    }
}
