// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Ordinaryunus.Bridge;
using Ordinaryunus.Data;
using Ordinaryunus.Jobs;

namespace Ordinaryunus.Host;

public enum RunMode { Normal, Screenshots, DryRun }

/// <summary>The only Form (MIMARI §2.2): hosts WebView2, owns the timers, the vault watcher, idle/lock and the
/// close guard. All hardening from §2.3 is applied here, once, in EnsureCoreWebView2Async's continuation.</summary>
public sealed class WebHostForm : Form
{
    readonly AppState _app;
    readonly RunMode _mode;
    readonly WebView2 _web = new();
    BridgeHost? _bridge;
    CoreWebView2Environment? _env;

    System.Windows.Forms.Timer? _reloadTimer, _lightsTimer, _usageTimer, _idleTimer, _watchdogTimer;
    FileSystemWatcher? _watcher;
    string? _watcherPath;
    readonly object _watcherGate = new();
    volatile bool _watchEnabled;
    volatile bool _debouncePending;

    public event Action? Ready;

    public WebHostForm(AppState app, RunMode mode)
    {
        _app = app;
        _mode = mode;
        Text = "Ordinaryunus";
        Icon = AppIcon.Load();
        MinimumSize = new Size(1280, 800);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = ThemeBg();
        _web.DefaultBackgroundColor = ThemeBg();
        _web.Dock = DockStyle.Fill;
        Controls.Add(_web);

        app.ApplyZoom = z => { if (_web.CoreWebView2 is not null) _web.ZoomFactor = z; };
        app.ClipboardSetText = t => { try { RunOnUi(() => Clipboard.SetText(t)); } catch { } };
        app.PickVaultFolder = PickFolderDialog;
        app.OnLockRequested = () => RunOnUi(() => { try { _web.CoreWebView2?.Navigate(WebViewConfig.StartUrl); } catch { } });
        app.ConfirmVaultChange = ConfirmVaultChangeDialog;
        // Git'siz kasada, kasadaki bir betik son çalıştırmadan beri değiştiyse kullanıcıya yerel onay kutusuyla sorulur
        // (bkz. VaultGitGuard). Ekran görüntüsü modunda kimse tıklayamaz: bağlanmaz, değişmiş betik reddedilir.
        if (mode != RunMode.Screenshots) VaultGitGuard.ConfirmChangedScript = ConfirmChangedScriptDialog;

        Load += async (_, _) => await InitWebViewAsync();
        FormClosing += OnFormClosing;
        // Kasa Ayarlar'dan değişince (ya da açılışta olmayan kasa sonradan seçilince) dosya izleyici yeni klasöre geçer.
        _app.SnapshotChanged += () =>
        {
            if (_watchEnabled && !string.Equals(_watcherPath, _app.Vault, StringComparison.OrdinalIgnoreCase)) StartWatcher();
        };

        if (mode == RunMode.Normal || mode == RunMode.DryRun) WindowState = FormWindowState.Maximized;

        _ = _app.ReloadAsync(true); // preload in the background while the login screen shows

        _reloadTimer = MakeTimer(1500, (_, _) => { if (_debouncePending) { _debouncePending = false; _ = _app.ReloadAsync(true); } });
        _lightsTimer = MakeTimer(30_000, (_, _) => RefreshLights());
        _usageTimer = MakeTimer(300_000, (_, _) => RefreshUsage());
        _idleTimer = MakeTimer(15_000, (_, _) => CheckIdle());
        // C (EK-v2.1 §2b): also applies the "don't let Windows sleep" decision every 30 s — cheap and frequent
        // enough that a job finishing (or Tam Gaz turning off) releases the request within half a minute.
        _watchdogTimer = MakeTimer(30_000, (_, _) => { JobStore.CheckWatchdogs(DateTime.Now); ApplyKeepAwake(); });
    }

    Color ThemeBg() => _app.Settings.Tema == "koyu" ? ColorTranslator.FromHtml("#0E1417") : ColorTranslator.FromHtml("#F7F7F5");

    System.Windows.Forms.Timer MakeTimer(int ms, EventHandler handler)
    {
        var t = new System.Windows.Forms.Timer { Interval = ms };
        t.Tick += handler;
        t.Start();
        return t;
    }

    async Task InitWebViewAsync()
    {
        var opts = new CoreWebView2EnvironmentOptions { Language = "tr-TR", AllowSingleSignOnUsingOSPrimaryAccount = false };
        if (_mode == RunMode.Screenshots) opts.AdditionalBrowserArguments = "--disable-features=CalculateNativeWinOcclusion";
        try
        {
            Directory.CreateDirectory(AppPaths.WebViewDataDir);
            _env = await CoreWebView2Environment.CreateAsync(null, AppPaths.WebViewDataDir, opts);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show("Bu uygulama için Microsoft Edge WebView2 bileşeni gerekiyor. Windows Update'i çalıştırıp tekrar dene.",
                "Ordinaryunus", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(4);
            return;
        }
        await _web.EnsureCoreWebView2Async(_env);
        var core = _web.CoreWebView2;
        var s = core.Settings;
        s.IsScriptEnabled = true; s.IsWebMessageEnabled = true; s.AreHostObjectsAllowed = false;
        s.AreDefaultScriptDialogsEnabled = false; s.IsStatusBarEnabled = false; s.IsZoomControlEnabled = false;
        s.IsPinchZoomEnabled = false; s.IsSwipeNavigationEnabled = false; s.IsGeneralAutofillEnabled = false;
        s.IsPasswordAutosaveEnabled = false; s.IsBuiltInErrorPageEnabled = true;
#if DEBUG
        s.AreDevToolsEnabled = true; s.AreDefaultContextMenusEnabled = true; s.AreBrowserAcceleratorKeysEnabled = true;
#else
        s.AreDevToolsEnabled = false; s.AreDefaultContextMenusEnabled = false; s.AreBrowserAcceleratorKeysEnabled = false;
#endif
        core.SetVirtualHostNameToFolderMapping("ordinaryunus.example", AppPaths.WwwRoot, CoreWebView2HostResourceAccessKind.Deny);
        core.NavigationStarting += (_, e) => { if (!WebViewConfig.IsAllowedNavigation(e.Uri)) e.Cancel = true; };
        core.FrameNavigationStarting += (_, e) => e.Cancel = true;
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.DownloadStarting += (_, e) => e.Cancel = true;
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        core.LaunchingExternalUriScheme += (_, e) => e.Cancel = true;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, e) => { if (!WebViewConfig.IsOwnOrigin(e.Request.Uri)) e.Response = _env!.CreateWebResourceResponse(null, 403, "Forbidden", ""); };
        core.WebMessageReceived += OnWebMessage;
        core.ProcessFailed += OnProcessFailed;
        // UiScale zaten 1.0-1.75 aralığında (YaziBoyutu / 100). Bir daha 100'e bölmek her şeyi %25'e küçültüyordu.
        _web.ZoomFactor = _app.Settings.UiScale;

        _bridge = new BridgeHost(_app, PostToWeb);
        core.Navigate(WebViewConfig.StartUrl + (_mode == RunMode.Screenshots ? "?shot=1" : ""));
        if (_mode == RunMode.Normal) { _watchEnabled = true; StartWatcher(); }
        Ready?.Invoke();
    }

    void RunOnUi(Action a) { if (InvokeRequired) BeginInvoke(a); else a(); }

    void PostToWeb(string json) => RunOnUi(() => { try { _web.CoreWebView2?.PostWebMessageAsJson(json); } catch { } });

    async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_bridge is null) return;
        // StartsWith(Origin) is a PREFIX check — "https://ordinaryunus.example.evil.com"
        // also starts with "https://ordinaryunus.example" as plain text, so a message whose Source claims that host
        // would have wrongly passed. WebViewConfig.IsOwnOrigin already does a real scheme+host equality check
        // (used elsewhere for WebResourceRequested); this call site just wasn't using it.
        if (!WebViewConfig.IsOwnOrigin(e.Source)) return;
        string raw;
        try { raw = e.TryGetWebMessageAsString(); } catch { return; }
        var response = await _bridge.HandleAsync(raw).ConfigureAwait(true);
        if (response is not null) PostToWeb(response);
    }

    void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
        {
            RunOnUi(() =>
            {
                // A blocking MessageBox here would hang --screenshots/--dry-run forever (nobody can click "Tamam"),
                // so automated modes just log and close; only Normal shows the dialog, since a person is at the keyboard.
                try
                {
                    Directory.CreateDirectory(AppPaths.DataDir);
                    File.AppendAllText(Path.Combine(AppPaths.DataDir, "hata.log"), $"{DateTime.Now:o} WebView2 tarayıcı süreci beklenmedik şekilde kapandı.\n");
                }
                catch { }
                if (_mode == RunMode.Normal)
                    MessageBox.Show("Web bileşeni beklenmedik şekilde kapandı. Uygulama kapatılacak.", "Ordinaryunus", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            });
        }
        else
        {
            RunOnUi(() => { try { _web.CoreWebView2?.Navigate(WebViewConfig.StartUrl); } catch { } });
        }
    }

    // ---------------- vault watcher / timers ----------------

    static readonly string[] IgnoreFragments = [@"\.git\", @"\.obsidian\", @"\.trash\", @"\node_modules\", @"\80 Oturum Arşivi\"];

    /// <summary>Kasayı izleyen FileSystemWatcher'ı (yeniden) kurar. Arka plandaki yeniden yüklemeden de çağrılabilir;
    /// kilit, iki çağrının aynı anda iki izleyici kurmasını engeller. Kasa klasörü yoksa bir şey yapmaz (sonraki
    /// yeniden yüklemede tekrar denenir).</summary>
    void StartWatcher()
    {
        lock (_watcherGate)
        {
            try
            {
                string vault = _app.Vault;
                if (!Directory.Exists(vault)) return;
                _watcher?.Dispose();
                _watcher = new FileSystemWatcher(vault)
                {
                    IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                };
                void OnChange(object s, FileSystemEventArgs e) { if (!IgnoreFragments.Any(f => e.FullPath.Contains(f, StringComparison.OrdinalIgnoreCase))) _debouncePending = true; }
                _watcher.Changed += OnChange; _watcher.Created += OnChange; _watcher.Deleted += OnChange;
                _watcher.Renamed += (s, e) => OnChange(s, e);
                _watcher.EnableRaisingEvents = true;
                _watcherPath = vault;
            }
            catch { }
        }
    }

    void RefreshLights()
    {
        var before = _app.Lights;
        _app.RefreshLightsOnly(DateTime.Now);
        if (!before.SequenceEqual(_app.Lights)) _bridge?.PostEvent("lightsChanged", new { lights = _app.Lights });
    }

    void RefreshUsage()
    {
        Task.Run(() =>
        {
            _app.RefreshUsage(TimeSpan.FromSeconds(3));
            if (_app.Usage is not null) _bridge?.PostEvent("usageChanged", _app.Usage);
        });
    }

    /// <summary>C (EK-v2.1 §2b): raises/releases the "stay awake" request based on running jobs, kota-bekliyor
    /// jobs and Tam Gaz — never touches Windows' own power-plan settings, only this process's own execution-state
    /// requirement for as long as work is pending.</summary>
    void ApplyKeepAwake()
    {
        var (on, _) = PowerManagement.Compute(_app.Settings.KeepAwakeAcik, JobStore.GetAll(), _app.Snapshot.TamGaz.On);
        PowerManagement.Apply(on);
    }

    void CheckIdle()
    {
        // "Odak modu bitince boşta kalma otomatik kilidi yeniden çalışsın".
        // JS is meant to send activity{focusRunning:false} the moment a focus session ends, but if that one message
        // is ever lost (page reload mid-countdown, a JS error in that overlay, …) FocusRunning stays stuck true and
        // idle auto-lock never runs again — a real security regression, not just a UX gap. So a heartbeat older
        // than 3x the 30 s cadence JS is supposed to send while focus runs is treated as "focus actually ended",
        // regardless of what the flag itself still says.
        if (_app.FocusRunning && _app.FocusHeartbeatAt is { } hb && (DateTime.Now - hb).TotalSeconds > 90)
            _app.FocusRunning = false;
        if (!_app.Unlocked || _app.FocusRunning) return;
        if ((DateTime.Now - _app.LastActivity).TotalMinutes < _app.Settings.KilitDakika) return;
        _app.Unlocked = false;
        _app.LockReason = "idle";
        _bridge?.PostEvent("locked", new { reason = "idle" });
        try { _web.CoreWebView2?.Navigate(WebViewConfig.StartUrl); } catch { }
    }

    string? PickFolderDialog()
    {
        string? result = null;
        RunOnUi(() =>
        {
            using var dlg = new FolderBrowserDialog { SelectedPath = _app.Settings.KasaYolu, ShowNewFolderButton = false };
            if (dlg.ShowDialog(this) == DialogResult.OK) result = dlg.SelectedPath;
        });
        return result;
    }

    /// <summary>geçerli (klasör var, AGENTS.md var) bir kasa değişikliği
    /// uygulanmadan önce kullanıcıya yerel Windows onay kutusuyla sorulur.</summary>
    bool ConfirmVaultChangeDialog(string path)
    {
        bool result = false;
        RunOnUi(() => result = MessageBox.Show(this,
            $"Kasa klasörü şuna değiştirilsin mi?\n\n{path}\n\nUygulama artık tüm verileri buradan okuyacak.",
            "Ordinaryunus — kasa değişikliği", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes);
        return result;
    }

    /// <summary>Git'siz kasada değişmiş bir betiği çalıştırmadan önce yerel onay kutusu (VaultGitGuard). UI iş
    /// parçacığı dışından çağrılırsa RunOnUi beklemeden döner ve sonuç "hayır" kalır: güvenli tarafta kalınır.</summary>
    bool ConfirmChangedScriptDialog(string question)
    {
        bool result = false;
        RunOnUi(() => result = MessageBox.Show(this, question, "Ordinaryunus — betik değişmiş",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes);
        return result;
    }

    void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // B (EK-v2.1 §1-2): iş artık bu uygulamanın sürecinde yaşamıyor, kasanın kendi iş nöbetçisiyle ayrı çalışıyor;
        // bu yüzden Ordinaryunus'u kapatmak eski doğrudan çağırma tasarımındaki gibi işi "yarıda" bırakmaz. Burada
        // onaylanacak/iptal edilecek bir şey yok; sadece kullanıcıya işin sürdüğü söylenir.
        if (_mode == RunMode.Normal)
        {
            int running = JobStore.GetAll().Count(j => j.CanCancel);
            if (running > 0)
                // uygulama kapanınca nöbetçi işi sürdürür ama bilgisayarı
                // uyanık tutan bir şey kalmaz (bunun çözümü nöbetçi tarafında, henüz yok); bunu dürüstçe söyle.
                MessageBox.Show(this, $"Arka planda {running} yapay zekâ işi çalışıyor. Uygulamayı kapatsan da iş sürmeye devam edecek; " +
                    "ilerlemeyi görmek için uygulamayı tekrar açabilirsin. Not: uygulama kapalıyken bilgisayar uyursa iş durur.",
                    "Ordinaryunus", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        _watchEnabled = false;
        lock (_watcherGate) { _watcher?.Dispose(); _watcher = null; }
    }

    /// <summary>Used only by --screenshots (Shots\WebScreenshotRunner.cs).</summary>
    public CoreWebView2? Core => _web.CoreWebView2;
    public WebView2 WebControl => _web;
    public async Task PostShotEventAsync(object payload) { await Task.Yield(); _bridge?.PostEvent("shot", payload); }
}
