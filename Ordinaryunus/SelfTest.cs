// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ordinaryunus.Data;
using Ordinaryunus.Security;

namespace Ordinaryunus;

/// <summary>Headless check: parses every vault source (read-only) and runs small unit checks on temp copies.</summary>
public static class SelfTest
{
    static int _fail;
    static TextWriter _out = Console.Out;

    public static int Run(string vault)
    {
        AttachConsoleIfAny();
        VaultWriter.ReadOnlyMode = true;   // the real vault is never written
        SettingsStore.ReadOnly = true;
        Launch.Disabled = true;
        var sb = new StringBuilder();
        _out = new StringWriter(sb);
        int code;
        try
        {
            code = RunInner(vault);
        }
        catch (Exception ex)
        {
            P("HATA: " + ex);
            code = 2;
        }
        string text = sb.ToString();
        Console.Out.Write(text);
        Console.Out.Flush();
        try
        {
            string log = Path.Combine(Path.GetTempPath(), "ordinaryunus-selftest.txt");
            File.WriteAllText(log, text, new UTF8Encoding(false));
        }
        catch { }
        return code;
    }

    /// <summary>--surum için: tek satırı --selftest çıktısıyla aynı yoldan (varsa üst konsola bağlanıp, UTF-8) yazar.</summary>
    public static int PrintLine(string line)
    {
        AttachConsoleIfAny();
        Console.Out.WriteLine(line);
        Console.Out.Flush();
        return 0;
    }

    static int RunInner(string vault)
    {
        P(Imza.Satir);
        P($"Ordinaryunus öz-test · kasa: {vault}");
        P(new string('-', 60));
        var s = VaultSnapshot.Load(vault);
        P($"Kasa bulundu: {(s.VaultExists ? "evet" : "HAYIR")}");
        P($"Yapılacaklar: {s.Todos.Items.Count} ({s.Todos.DoneCount} bitti){(s.Todos.Found ? "" : " — bölüm yok")}");
        foreach (var t in s.Todos.Items) P($"   [{(t.Done ? "x" : " ")}] {Md.Clip(t.Text, 90)}");
        var byDurum = s.Projects.GroupBy(p => p.Durum).ToDictionary(g => g.Key, g => g.Count());
        P($"Proje: {byDurum.GetValueOrDefault("aktif")} aktif, {byDurum.GetValueOrDefault("beklemede")} beklemede, " +
          $"{byDurum.GetValueOrDefault("bitti")} bitti, {byDurum.GetValueOrDefault("donduruldu")} donduruldu");
        foreach (var p in s.Projects)
            P($"   {p.Name}: {p.Durum}{(p.Odak ? " (odak)" : "")}, bitti {p.DoneCount}/{p.TotalCount}, beklenen {p.Beklenenler.Count}, " +
              $"bırakma {(p.KillDate?.ToString("yyyy-MM-dd") ?? "yok")}, karar_bekliyor {p.KararBekliyor}, kilit \"{p.Kilit}\"");
        P($"Görev: {s.Tasks.Count} ({string.Join(", ", s.Tasks.GroupBy(t => t.Durum).Select(g => $"{g.Key} {g.Count()}"))})");
        P($"Rol: {s.Roles.Count} ({s.Roles.Select(r => r.Departman).Distinct().Count()} departman)");
        P($"İstek: {s.Ledger.Count(l => !l.Auto)} (Claude {s.Ledger.Count(l => !l.Auto && l.Arac != "codex")}, Codex {s.Ledger.Count(l => !l.Auto && l.Arac == "codex")}; " +
          $"ayrıca {s.Ledger.Count(l => l.Auto)} zamanlanmış görev kaydı)");
        P($"LinkedIn: {(s.LinkedIn.Found ? $"{s.LinkedIn.Published.Count} yayında, {s.LinkedIn.Drafts.Count} taslak" : "dosya yok")}");
        P($"İş ilanı: {(s.Jobs.Found ? $"{s.Jobs.AllRows.Count()} satır, {s.Jobs.AwaitingCount} onay bekliyor, bölümler: {string.Join(" / ", s.Jobs.Sections.Select(x => $"{x.Name} ({x.Rows.Count})"))}" : "henüz yok (dosya bulunamadı)")}");
        P($"Git: {(s.Git.Available ? $"{s.Git.Recent.Count} commit (30 gün), son 14 gün {s.Git.PerDay14.Values.Sum()}" : "yok")}");
        P($"Seri: {s.Streak()} gün");
        P($"Gelir defteri: {(s.Income.Found ? $"{s.Income.Entries.Count} kayıt, bu ay {s.Income.MonthTl(s.Today)} TL" : "henüz yok")}");
        P($"Derin analiz: {(s.Analysis.Found ? $"{s.Analysis.RelPath} ({s.Analysis.Lines.Count} satır)" : "henüz yok")}");
        P($"Güvenlik: {s.SafetyPending.Count} onay bekliyor, acil durdurma {(s.Stopped ? "AÇIK" : "kapalı")}");
        P($"Tam Gaz: {(s.TamGaz.On ? $"açık, bitiş {s.TamGaz.End:HH:mm}" : "kapalı")}; Codex nöbeti: {(s.Watch.Running ? "çalışıyor" : "yok")}; günlük {s.TamGazLog.Count} satır");
        var t0 = s.Tools;
        P($"Araçlar: Claude hook {t0.ClaudeHooks.Count}, alt ajan {t0.ClaudeAgents.Count}, zamanlanmış görev {t0.ScheduledTasks.Count} " +
          $"({string.Join(", ", t0.ScheduledTasks.Select(x => $"{x.Name}: {x.NextRun ?? "?"}"))}), Codex MCP {t0.CodexMcpServers.Count}, " +
          $"eklenti {t0.CodexPlugins.Count}, Codex hook {t0.CodexHooks.Count}");
        var lights = SystemMonitor.CheckLights(vault, DateTime.Now);
        P("Işıklar: " + string.Join(" · ", lights.Select(l => $"{l.Label}={l.State}({l.Detail})")));
        var usage = SystemMonitor.EstimateUsage(DateTime.Now, TimeSpan.FromSeconds(3));
        P($"Bugünkü kullanım: Codex {SystemMonitor.FormatTokens(usage.CodexTokens)}, Claude {SystemMonitor.FormatTokens(usage.ClaudeTokens)}{(usage.TimedOut ? " (süre doldu)" : "")}");
        P("Analiz cümleleri:");
        foreach (var line in Insights.Sentences(s)) P("   • " + line);
        if (s.Warnings.Count > 0)
        {
            P("Uyarılar:");
            foreach (var w in s.Warnings) P("   ! " + w);
        }

        P(new string('-', 60));
        P("Birim kontrolleri (geçici klasörde):");
        UnitChecks(vault);
        P(new string('-', 60));
        P(_fail == 0 ? "SONUÇ: TAMAM" : $"SONUÇ: {_fail} kontrol başarısız");
        return _fail == 0 ? 0 : 1;
    }

    static void UnitChecks(string vault)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "ordi-test", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tmp);
        // Dışarıdan verilmiş bir ORDINARYUNUS_DATA varsa kontroller bitince ona geri dönülür (boşaltılırsa sonraki
        // her okuma/yazma gerçek %APPDATA%\Ordinaryunus'a düşerdi).
        string? oncekiData = Environment.GetEnvironmentVariable("ORDINARYUNUS_DATA");
        Environment.SetEnvironmentVariable("ORDINARYUNUS_DATA", Path.Combine(tmp, "appdata"));
        try
        {
            ImzaChecks();
            ExpectationChecks();
            DefaultVaultChecks(tmp);
            ScriptGuardChecks(tmp);

            // Frontmatter: quoted / unquoted / CRLF / LF / BOM
            string fmText = "---\ndurum: aktif\nodak: true\nsonraki_adim: \"Eğer yarın → \\\"iş\\\" yaparım\"\nkilit: ''\nolum_kriteri: Eğer 2026-10-07'ye kadar x olmazsa → dondur\n---\n# Başlık\n";
            foreach (var (label, text) in new[] { ("LF", fmText), ("CRLF", fmText.Replace("\n", "\r\n")), ("BOM+CRLF", "\uFEFF" + fmText.Replace("\n", "\r\n")) })
            {
                var fm = Frontmatter.Parse(text);
                Check($"frontmatter {label}", fm.Get("durum") == "aktif" && fm.GetBool("odak") && fm.Get("sonraki_adim") == "Eğer yarın → \"iş\" yaparım"
                    && fm.Get("kilit") == "" && Md.FirstIsoDate(fm.Get("olum_kriteri")) == new DateOnly(2026, 10, 7));
            }
            // BOM on disk read
            string bomFile = Path.Combine(tmp, "bom.md");
            File.WriteAllBytes(bomFile, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("---\r\ndurum: \"beklemede\"\r\n---\r\n")]);
            Check("frontmatter BOM dosyası", Frontmatter.Parse(Md.ReadText(bomFile)).Get("durum") == "beklemede");

            Check("wikilink [[A|B]] → B", Md.Plain("x [[Klasör/Not|Görünen]] y") == "x Görünen y");
            Check("wikilink [[A]] → A", Md.Plain("**[[Kişisel Blog]]** %%gizli%% <!-- yorum -->") == "Kişisel Blog");
            Check("Türkçe ek: 1'i, 6'sı, 10'u", Insights.Poss(1) == "1'i" && Insights.Poss(6) == "6'sı" && Insights.Poss(10) == "10'u");
            Check("Türkçe ek: Defteri'nin, Durumu'nun, Blog'un, Taşıma'nın",
                Insights.Gen("Mobil Not Defteri") == "Mobil Not Defteri'nin" && Insights.Gen("Hava Durumu") == "Hava Durumu'nun" &&
                Insights.Gen("Kişisel Blog") == "Kişisel Blog'un" && Insights.Gen("Eski Blog Taşıma") == "Eski Blog Taşıma'nın");
            Check("saat eki: 22:00'ye, 10:30'a, 09:05'e", Insights.TimeDat(new TimeOnly(22, 0)) == "22:00'ye" &&
                Insights.TimeDat(new TimeOnly(10, 30)) == "10:30'a" && Insights.TimeDat(new TimeOnly(9, 5)) == "09:05'e");

            // Checkbox toggle on a temp copy (LF and CRLF), exactly one line changes
            foreach (var (label, nl, bom) in new[] { ("LF", "\n", false), ("CRLF", "\r\n", true) })
            {
                string doc = string.Join(nl, "---", "tur: simdi", "---", "# Şimdi", "", "## ✅ Yapılacaklarım", "- [ ] **Blog:** taslak yaz",
                    "- [x] Bitmiş iş", "- [ ] Üçüncü", "", "## Başka", "- [ ] Blog dışı") + nl;
                string f = Path.Combine(tmp, $"01 Şimdi {label}.md");
                byte[] bytes = Encoding.UTF8.GetBytes(doc);
                File.WriteAllBytes(f, bom ? [0xEF, 0xBB, 0xBF, .. bytes] : bytes);
                var list = VaultReader.ParseTodos(Md.ReadText(f)!);
                VaultWriter.ReadOnlyMode = false;
                try { VaultWriter.ToggleTodo(f, list.Items[0]); } finally { VaultWriter.ReadOnlyMode = true; }
                byte[] after = File.ReadAllBytes(f);
                string afterText = Encoding.UTF8.GetString(after, bom ? 3 : 0, after.Length - (bom ? 3 : 0));
                var a = afterText.Split(nl);
                var b = doc.Split(nl);
                int diff = a.Length == b.Length ? a.Zip(b).Count(x => x.First != x.Second) : -1;
                bool nlKept = nl == "\n" ? !afterText.Contains('\r') : afterText.Replace("\r\n", "").IndexOf('\n') < 0;
                Check($"kutucuk işaretleme {label}: 1 satır değişti, satır sonu ve BOM korundu",
                    diff == 1 && afterText.Contains("- [x] **Blog:** taslak yaz") && nlKept && Md.HasBom(after) == bom && afterText.Contains("- [ ] Blog dışı"));
                // Stale protection: a changed line must be refused
                VaultWriter.ReadOnlyMode = false;
                bool stale = false;
                try { VaultWriter.ToggleTodo(f, list.Items[0]); } catch (StaleFileException) { stale = true; } finally { VaultWriter.ReadOnlyMode = true; }
                Check($"değişmiş dosya reddedildi {label}", stale);
            }

            // Job table parsing by header names (column order changed, missing columns, markdown links)
            string jobs = "---\ntur: alan\n---\n# İş Başvuruları\n\n## LinkedIn Kolay Başvuru\n| No | Durum | Firma | Pozisyon | İlan linki | Belgeler |\n|---|---|---|---|---|---|\n" +
                          "| 1 | Onay bekliyor | Acme | Junior .NET | [ilan](https://example.com/1) | 2026-09-24_Acme |\n| 2 | Başvuruldu | Beta | Web | https://example.com/2 | |\n\n" +
                          "## Diğer siteler\n| Firma | Pozisyon | Şehir | Durum | Not |\n|:--|:--|:--|:--|:--|\n| Gama | Junior geliştirici | Ankara | Onay bekliyor | uzaktan |\n\n## Takip taslakları\nMetin\n";
            var ji = VaultReader.ParseJobs(jobs);
            var r1 = ji.Sections.FirstOrDefault()?.Rows.FirstOrDefault();
            Check("iş tablosu başlık adlarıyla okundu", ji.Sections.Count == 2 && ji.AllRows.Count() == 3 && ji.AwaitingCount == 2 &&
                r1 is { Firma: "Acme", Pozisyon: "Junior .NET", Belgeler: "2026-09-24_Acme" } && r1.Url == "https://example.com/1" &&
                ji.Sections[1].Rows[0].Sehir == "Ankara");
            string jf = Path.Combine(tmp, "İş Başvuruları.md");
            File.WriteAllText(jf, jobs.Replace("\n", "\r\n"), new UTF8Encoding(false));
            var parsed = VaultReader.ParseJobs(Md.ReadText(jf)!);
            VaultWriter.ReadOnlyMode = false;
            try { VaultWriter.ApproveJobs(jf, parsed.AllRows.Where(r => r.AwaitingApproval).ToList()); } finally { VaultWriter.ReadOnlyMode = true; }
            string jAfter = File.ReadAllText(jf);
            Check("iş onayı: sadece Durum hücresi değişti", jAfter.Contains("| 1 | Onaylandı | Acme |") && jAfter.Contains("| Ankara | Onaylandı | uzaktan |")
                && jAfter.Replace("Onaylandı", "Onay bekliyor") == jobs.Replace("\n", "\r\n"));

            // PBKDF2 round trip
            var st = new AppSettings();
            SettingsStore.SetPassword(st, "gizli-şifre-1");
            Check("şifre: doğru şifre kabul", SettingsStore.CheckPassword(st, "gizli-şifre-1"));
            Check("şifre: yanlış şifre red", !SettingsStore.CheckPassword(st, "gizli-şifre-2") && !SettingsStore.CheckPassword(st, ""));
            Check("şifre: özet 32 bayt, tuz 16 bayt", Convert.FromBase64String(st.SifreOzet!).Length == 32 && Convert.FromBase64String(st.SifreTuz!).Length == 16);
            TimeSpan? wait = null;
            for (int i = 0; i < 5; i++) wait = SettingsStore.RegisterFailure(st);
            TimeSpan? wait2 = null;
            for (int i = 0; i < 5; i++) wait2 = SettingsStore.RegisterFailure(st);
            Check("5 yanlış → 30 sn, sonra 60 sn", wait == TimeSpan.FromSeconds(30) && wait2 == TimeSpan.FromSeconds(60));

            // Emergency-stop process filter (B/EK-v2.1: both tools now run through is-nobetcisi.mjs; the old
            // codex-nobet.mjs/codex.mjs patterns stay recognised too, for a leftover process from before this change).
            Check("acil durdurma süzgeci", CodexRunner.ShouldStop("node.exe", @"node C:\K\_sistem\araclar\is-nobetcisi.mjs --arac claude --is x") &&
                CodexRunner.ShouldStop("node.exe", "node C:\\K\\_sistem\\araclar\\codex.mjs exec -C x") &&
                CodexRunner.ShouldStop("node.exe", "node codex-nobet.mjs") && CodexRunner.ShouldStop("codex.exe", @"C:\a\codex.exe exec -C x -s workspace-write") &&
                !CodexRunner.ShouldStop("codex.exe", @"C:\a\codex.exe app-server") && !CodexRunner.ShouldStop("node.exe", "node server.js") &&
                !CodexRunner.ShouldStop("codex.exe", @"C:\Program Files\WindowsApps\OpenAI.Codex\codex.exe exec x") && !CodexRunner.ShouldStop("Codex.exe", null));

            // Security queue: pending minus decided; append-only decisions
            string vt = Path.Combine(tmp, "kasa");
            Directory.CreateDirectory(SafetyQueue.Dir(vt));
            SafetyQueue.DecisionsDirOverride = Path.Combine(tmp, "yerel-kararlar"); // never touch the real %LOCALAPPDATA% file
            Directory.CreateDirectory(SafetyQueue.DecisionsDirOverride);
            Check("güvenlik: kararlar kasanın dışında", !SafetyQueue.DecisionsFile(vt).StartsWith(vt, StringComparison.OrdinalIgnoreCase));
            string pend = "{\"id\":\"a1\",\"zaman\":\"2026-09-23T18:00:00Z\",\"arac\":\"claude\",\"islem\":\"Dosya silme\",\"hedef\":\"x.md\",\"komut\":\"rm x.md\",\"risk\":\"kritik\",\"neden\":\"test\"}\n" +
                          "{\"id\":\"a2\",\"zaman\":\"2026-09-23T18:01:00Z\",\"arac\":\"codex\",\"islem\":\"Push\",\"hedef\":\"origin\",\"komut\":\"git push\",\"risk\":\"yuksek\",\"neden\":\"\"}\nbozuk satır\n";
            File.WriteAllText(SafetyQueue.PendingFile(vt), pend);
            File.WriteAllText(SafetyQueue.DecisionsFile(vt), "{\"id\":\"a2\",\"karar\":\"red\",\"zaman\":\"2026-09-23T18:02:00Z\"}\n");
            var warn = new List<string>();
            var pending = SafetyQueue.ReadPending(vt, warn);
            Check("güvenlik: karar verilmiş istek gizlendi", pending.Count == 1 && pending[0].Id == "a1" && pending[0].Critical && warn.Count == 1);

            // "kesildi" bayrağı (kapının kesilmiş komutu işaretlemesi için ileriye dönük hazırlık): kart, komutun tamamının
            // görünmediğini bilmeli ki uygulama kullanıcının yarısı gizli bir komutu onaylamasına asla izin vermesin.
            string vtK = Path.Combine(tmp, "kasa-kesildi");
            Directory.CreateDirectory(SafetyQueue.Dir(vtK));
            File.WriteAllText(SafetyQueue.PendingFile(vtK),
                "{\"id\":\"k1\",\"arac\":\"claude\",\"islem\":\"x\",\"hedef\":\"\",\"komut\":\"echo a\",\"risk\":\"kritik\",\"neden\":\"\",\"kesildi\":true}\n" +
                "{\"id\":\"k2\",\"arac\":\"claude\",\"islem\":\"x\",\"hedef\":\"\",\"komut\":\"echo b\",\"risk\":\"kritik\",\"neden\":\"\"}\n");
            var kesikler = SafetyQueue.ReadPending(vtK);
            Check("güvenlik: \"kesildi\" alanı okunuyor", kesikler.Single(x => x.Id == "k1").Kesildi && !kesikler.Single(x => x.Id == "k2").Kesildi);
            VaultWriter.ReadOnlyMode = false;
            try { SafetyQueue.Decide(vt, "a1", approve: true); } finally { VaultWriter.ReadOnlyMode = true; }
            var decisions = File.ReadAllLines(SafetyQueue.DecisionsFile(vt));
            Check("güvenlik: karar dosyaya eklendi (bekleyenler değişmedi)", decisions.Length == 2 && decisions[1].Contains("\"karar\":\"onay\"") &&
                File.ReadAllText(SafetyQueue.PendingFile(vt)) == pend && SafetyQueue.ReadPending(vt).Count == 0);
            VaultWriter.ReadOnlyMode = false;
            try
            {
                SafetyQueue.CreateStopFlag(vt);
                bool on = SafetyQueue.IsStopped(vt);
                SafetyQueue.RemoveStopFlag(vt);
                Check("acil durdurma bayrağı oluştur/sil", on && !SafetyQueue.IsStopped(vt));

                // Tam Gaz switch
                Ordinaryunus.Data.TamGaz.TurnOn(vt, 10);
                var tg = Ordinaryunus.Data.TamGaz.Read(vt, DateTimeOffset.Now);
                Ordinaryunus.Data.TamGaz.TurnOff(vt);
                Check("Tam Gaz aç/kapat (ACIK dosyası)", tg.On && tg.Hours == 10 && tg.End > DateTimeOffset.Now.AddHours(9.9) &&
                    !File.Exists(Ordinaryunus.Data.TamGaz.SwitchFile(vt)));
            }
            finally { VaultWriter.ReadOnlyMode = true; }
            var expired = Ordinaryunus.Data.TamGaz.Parse("{\"baslangic\":\"2026-01-01T00:00:00Z\",\"bitis\":\"2026-01-01T02:00:00Z\",\"saat\":2}", DateTimeOffset.Now);
            Check("Tam Gaz süresi geçmiş = kapalı", !expired.On);
            var watch = Ordinaryunus.Data.TamGaz.ParseWatch("{\"pid\":1234,\"gorev\":\"20 Projeler/X/Görevler/G-001 Demo.md\",\"baslangic\":\"2026-09-23T18:00:00Z\"}", false, _ => true);
            var watchDone = Ordinaryunus.Data.TamGaz.ParseWatch("{\"pid\":0,\"bitis\":\"2026-09-23T19:00:00Z\",\"sonuc\":\"tamam\"}", true, _ => true);
            Check("Codex nöbeti okuma", watch.Running && watch.Task == "G-001 Demo" && !watchDone.Running && watchDone.WaitingQuota);

            // Quota light + usage parsing
            var now = new DateTime(2026, 9, 23, 20, 30, 0);
            var reset = SystemMonitor.ParseQuotaReset("{\"message\":\"You’ve hit your usage limit. ... or try again at 10:05 PM.\"}", now);
            Check("Codex kota saati okundu", reset == new DateTime(2026, 9, 23, 22, 5, 0));
            Check("Codex token sayısı", SystemMonitor.CodexTotal("{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"total_tokens\":667861}}}}") == 667861);
            var cu = SystemMonitor.ClaudeUsage("{\"timestamp\":\"2026-09-23T12:09:34.763Z\",\"message\":{\"id\":\"m1\",\"usage\":{\"input_tokens\":2,\"cache_creation_input_tokens\":100,\"cache_read_input_tokens\":999,\"output_tokens\":10}}}");
            Check("Claude token sayısı (giriş+çıkış+önbellek yazımı)", cu.id == "m1" && cu.tokens == 112 && cu.when is not null);
            Check("zamanlanmış görev sonraki çalışma", ToolStatusReader.NextRun("pazar 19:00", now) == "27 Eyl Paz 19:00" &&
                ToolStatusReader.NextRun("her gün 21:30", now) == "bugün 21:30");

            // ProcessRunner.Resolve: only ".exe" is trusted for a bare name (never ".cmd"/".bat", which run
            // through cmd.exe and would quietly turn "no shell" into a real shell invocation).
            string toolDir = Path.Combine(tmp, "sahte-arac");
            Directory.CreateDirectory(toolDir);
            File.WriteAllText(Path.Combine(toolDir, "sahtearac.cmd"), "@echo hi");
            string oldPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            Environment.SetEnvironmentVariable("PATH", toolDir + Path.PathSeparator + oldPath);
            try
            {
                Check("ProcessRunner.Resolve: .cmd kabul edilmiyor", ProcessRunner.Resolve("sahtearac") is null);
                File.WriteAllBytes(Path.Combine(toolDir, "sahtearac.exe"), "MZ"u8.ToArray());
                Check("ProcessRunner.Resolve: .exe kabul ediliyor", ProcessRunner.Resolve("sahtearac") == Path.Combine(toolDir, "sahtearac.exe"));
            }
            finally { Environment.SetEnvironmentVariable("PATH", oldPath); }

            // Eski v1 WinForms arayüzü (UI\, Charts\) ve onun kontrolleri v2 ile kaldırıldı (MIMARI §1.1 D4). v2'nin bütün
            // kontrolleri (Renderer, Dizin, Kayıt, Sorunlar, Köprü, Claude, Host/web sözleşmesi) aşağıda SelfTestV2.Run'da.

            // Parsers tolerate missing data
            IndependentChecks(tmp);
            Check("eksik dosyalar çökertmez", VaultSnapshot.Load(Path.Combine(tmp, "yok")).Warnings.Count > 0 &&
                VaultReader.ParseTodos("# boş").Found == false && VaultReader.ParseLinkedIn("").Drafts.Count == 0);
            Check("TL ayrıştırma", VaultReader.ParseTl("5.000") == 5000m && VaultReader.ParseTl("1.250,50 TL") == 1250.50m && VaultReader.ParseTl("750") == 750m);

            P(new string('-', 60));
            P("v2 kontrolleri (Renderer, Dizin, Kayıt, Sorunlar, Köprü, Claude, Host/web):");
            SelfTestV2.Run(vault, tmp, P, Check);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ORDINARYUNUS_DATA", oncekiData);
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    static void Check(string name, bool ok)
    {
        if (!ok) _fail++;
        P($"   {(ok ? "GEÇTİ " : "KALDI ")} {name}");
    }

    // ---------------- imza ----------------

    /// <summary>İmza tek kaynaktan mı geliyor: C# (Imza.cs), arayüz (wwwroot/js/imza.js) ve exe sürümü (csproj Version).</summary>
    static void ImzaChecks()
    {
        Check($"imza: exe sürümü (csproj Version) Imza.Surum ({Imza.Surum}) ile aynı",
            typeof(Imza).Assembly.GetName().Version?.ToString(3) == Imza.Surum);
        string? js = FindImzaJs();
        string text = js is null ? "" : Md.ReadText(js) ?? "";
        Check("imza: wwwroot/js/imza.js bulundu" + (js is null ? "" : $" ({js})"), js is not null);
        // Değerler alan atamasında aranır (yapan: '…'), dosya başındaki telif yorum satırında değil; yoksa yorum
        // satırı her zaman eşleşir ve IMZA nesnesindeki bir sapma yakalanmazdı.
        bool Field(string name, string value) => Regex.IsMatch(text, $@"\b{name}\s*:\s*['""]{Regex.Escape(value)}['""]");
        Check("imza: imza.js'teki yapan, sürüm ve GitHub (ayrıca yıl, lisans) Imza.cs ile aynı",
            Field("yapan", Imza.Yapan) && Field("surum", Imza.Surum) && Field("github", Imza.GitHub) && Field("lisans", Imza.Lisans) &&
            Regex.IsMatch(text, $@"\byil\s*:\s*{Imza.Yil}\b"));

        // Lisansın ek şartları (EK-SARTLAR.md madde 1, GPL-3.0 7(b)): giriş ekranı ve Hakkında kartı yazar atfını
        // imza.js'ten göstermeye devam ediyor mu. Atıf koddan çıkarılırsa bu kontrol kalır.
        string? jsDir = js is null ? null : Path.GetDirectoryName(js);
        string login = jsDir is null ? "" : Md.ReadText(Path.Combine(jsDir, "overlays", "login.js")) ?? "";
        string ayarlar = jsDir is null ? "" : Md.ReadText(Path.Combine(jsDir, "pages", "ayarlar.js")) ?? "";
        Check("imza: giriş ekranı yazar atfını gösteriyor (login.js → IMZA.yapan + \"tarafından yapıldı\")",
            login.Contains("IMZA.yapan") && login.Contains("tarafından yapıldı"));
        Check("imza: Ayarlar › Hakkında kartı yazar, depo adresi ve lisans notunu gösteriyor (ayarlar.js)",
            ayarlar.Contains("\"Hakkında\"") && ayarlar.Contains("IMZA.yapan") && ayarlar.Contains("IMZA.github") &&
            ayarlar.Contains("hiçbir garanti olmadan"));
    }

    /// <summary>Çalışan exe'nin yanındaki wwwroot/js/imza.js; yoksa exe klasöründen yukarı doğru (en fazla 6 üst
    /// klasör) kaynak projedeki kopyası (Ordinaryunus.csproj'un yanındaki wwwroot).</summary>
    static string? FindImzaJs()
    {
        string beside = Path.Combine(AppPaths.WwwRoot, "js", "imza.js");
        if (File.Exists(beside)) return beside;
        try
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i <= 6 && d is not null; i++, d = d.Parent)
            {
                foreach (var proj in new[] { d.FullName, Path.Combine(d.FullName, "Ordinaryunus") })
                {
                    string candidate = Path.Combine(proj, "wwwroot", "js", "imza.js");
                    if (File.Exists(Path.Combine(proj, "Ordinaryunus.csproj")) && File.Exists(candidate)) return candidate;
                }
            }
        }
        catch { }
        return null;
    }

    // ---------------- "kullanıcıdan beklenenler" ayrıştırıcısı ----------------

    static void ExpectationChecks()
    {
        foreach (var baslik in new[]
                 {
                     "Benden Beklenenler", "Senden Beklenenler", "Kaan'dan Beklenenler", "Ayşe'den beklenenler", "Mert’ten Beklenenler",
                     "Ekip'ten Beklenenler", "Kullanıcıdan Beklenenler", "⏳ Senden beklenen kararlar",
                 })
            Check($"beklenenler başlığı tanınıyor: \"{baslik}\"", VaultReader.IsExpectationHeading(baslik));
        Check("beklenenler başlığı: ilgisiz başlıklar tanınmıyor (Bitti Tanımı, Beklenen sonuçlar, Engel, Son kararlar)",
            !VaultReader.IsExpectationHeading("Bitti Tanımı") && !VaultReader.IsExpectationHeading("Beklenen sonuçlar") &&
            !VaultReader.IsExpectationHeading("Engel (en zor parça)") && !VaultReader.IsExpectationHeading("Son kararlar"));

        string kart = "---\ndurum: aktif\n---\n# X\n\n## Benden Beklenenler\n- Haftada bir geri bildirim\n- [x] Bitmiş istek\n" +
                      "- [ ] Açık istek\n  - girintili alt madde\n1. Numaralı istek\n\n## Çıktılar ve Kaynaklar\n- sayılmaz\n";
        var yeni = VaultReader.ParseProject("X", "20 Projeler/X/X.md", kart);
        Check("proje kartı: \"Benden Beklenenler\" maddeleri okunuyor (bitmiş ve girintili olan atlanır)",
            yeni.Beklenenler.SequenceEqual(["Haftada bir geri bildirim", "Açık istek", "Numaralı istek"]));
        var eski = VaultReader.ParseProject("X", "20 Projeler/X/X.md", kart.Replace("Benden Beklenenler", "Ufuk'tan Beklenenler"));
        var senden = VaultReader.ParseProject("X", "20 Projeler/X/X.md", kart.Replace("Benden Beklenenler", "Senden Beklenenler"));
        Check("proje kartı: eski \"<ad>'tan Beklenenler\" ve \"Senden Beklenenler\" başlıkları da aynı sonucu verir",
            eski.Beklenenler.SequenceEqual(yeni.Beklenenler) && senden.Beklenenler.SequenceEqual(yeni.Beklenenler));
        var simdi = VaultReader.ParseExpectations(Md.Lines(
            "# Şimdi\n\n## ⏳ Senden beklenen kararlar\n1. Hangi ilana başvurulsun?\n2. Ne zaman dönülsün?\n\n## Son kararlar\n- sayılmaz\n"));
        Check("01 Şimdi: \"Senden beklenen kararlar\" aynı esneklikte okunuyor",
            simdi.SequenceEqual(["Hangi ilana başvurulsun?", "Ne zaman dönülsün?"]));
        Check("beklenenler: kod bloğu içindeki başlık sayılmaz",
            VaultReader.ParseExpectations(Md.Lines("```\n## Benden Beklenenler\n- kod içi\n```\n")).Count == 0);
    }

    // ---------------- varsayılan kasa yolu ----------------

    static void DefaultVaultChecks(string tmp)
    {
        string root = Path.Combine(tmp, "depo");
        string exeDir = Path.Combine(root, "Ordinaryunus", "bin", "Release", "net10.0-windows");
        string yayin = Path.Combine(root, "yayin");
        string demo = Path.Combine(root, AppPaths.DemoVaultName);
        string derin = Path.Combine(tmp, "derin", "a", "b", "c", "d", "e", "f");
        foreach (var d in new[] { exeDir, yayin, demo, derin }) Directory.CreateDirectory(d);
        string profile = Path.Combine(tmp, "profil");

        Check("varsayılan kasa: ORDINARYUNUS_KASA her şeyden önce gelir",
            AppPaths.ResolveDefaultVault(@"D:\Kasam", exeDir, profile) == @"D:\Kasam");
        Check("varsayılan kasa: bin\\Release\\net10.0-windows'tan 4 üstteki ornek-kasa bulunur",
            AppPaths.ResolveDefaultVault(null, exeDir, profile) == demo);
        Check("varsayılan kasa: yayin\\'dan 1 üstteki ornek-kasa bulunur", AppPaths.ResolveDefaultVault("  ", yayin, profile) == demo);
        Check("varsayılan kasa: bulunamazsa %USERPROFILE%\\Documents\\IkinciBeyin",
            AppPaths.ResolveDefaultVault(null, derin, profile) == Path.Combine(profile, "Documents", "IkinciBeyin"));

        // Ayarlarda kayıtlı kasa yolu her zaman önceliklidir; kayıt yoksa ortam değişkeni devreye girer.
        // (UnitChecks, ORDINARYUNUS_DATA'yı geçici bir klasöre çevirdi: gerçek ayar dosyasına dokunulmaz.)
        string? prevEnv = Environment.GetEnvironmentVariable(AppPaths.VaultEnvVar);
        string envVault = Path.Combine(tmp, "ortam-kasasi");
        string saved = Path.Combine(tmp, "kayitli-kasa");
        try
        {
            Environment.SetEnvironmentVariable(AppPaths.VaultEnvVar, envVault);
            Check("varsayılan kasa: ayar kaydı yokken ORDINARYUNUS_KASA kullanılır",
                !File.Exists(AppPaths.SettingsFile) && SettingsStore.Load().KasaYolu == envVault);
            Directory.CreateDirectory(AppPaths.DataDir);
            File.WriteAllText(AppPaths.SettingsFile, "{\"KasaYolu\":" + JsonSerializer.Serialize(saved) + "}");
            Check("varsayılan kasa: ayarlarda kayıtlı kasa yolu her zaman önceliklidir", SettingsStore.Load().KasaYolu == saved);
            File.WriteAllText(AppPaths.SettingsFile, "{\"KasaYolu\":\"\"}");
            Check("varsayılan kasa: kayıtlı yol boşsa varsayılana düşer", SettingsStore.Load().KasaYolu == envVault);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppPaths.VaultEnvVar, prevEnv);
            try { File.Delete(AppPaths.SettingsFile); } catch { }
        }

        Check("boş kasa klasörü çökertmez (uyarı verir)", VaultSnapshot.Load(demo, includeTools: false) is { VaultExists: true } bos && bos.Warnings.Count > 0);
    }

    // ---------------- kasa betiği koruması (VaultGitGuard) ----------------

    static void ScriptGuardChecks(string tmp)
    {
        Check("betik koruması: git çıktısı doğru yorumlanıyor",
            VaultGitGuard.Classify(new ProcessResult(0, "", false)) == ScriptStatus.Clean &&
            VaultGitGuard.Classify(new ProcessResult(0, " M _sistem/araclar/x.mjs\n", false)) == ScriptStatus.Modified &&
            VaultGitGuard.Classify(new ProcessResult(0, "?? _sistem/araclar/x.mjs\n", false)) == ScriptStatus.Untracked &&
            VaultGitGuard.Classify(new ProcessResult(128, "fatal: not a git repository", false)) == ScriptStatus.Unknown &&
            VaultGitGuard.Classify(new ProcessResult(-1, "", true)) == ScriptStatus.Unknown);

        string kasa = Path.Combine(tmp, "gitsiz-kasa");
        const string rel = "_sistem/araclar/deneme.mjs";
        string full = Path.Combine(kasa, "_sistem", "araclar", "deneme.mjs");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "console.log(1);\n");
        static bool Throws(Action a) { try { a(); return false; } catch (ScriptNotTrustedException) { return true; } }

        Check("betik koruması: git'li kasada değişmiş ya da izlenmeyen betik onaylansa bile reddedilir, temiz olan çalışır",
            Throws(() => VaultGitGuard.Enforce(kasa, rel, ScriptStatus.Modified, _ => true)) &&
            Throws(() => VaultGitGuard.Enforce(kasa, rel, ScriptStatus.Untracked, _ => true)) &&
            !Throws(() => VaultGitGuard.Enforce(kasa, rel, ScriptStatus.Clean, null)));

        bool prevRo = VaultWriter.ReadOnlyMode;
        VaultWriter.ReadOnlyMode = false; // özetler sadece geçici ORDINARYUNUS_DATA altına yazılır
        try
        {
            bool first = !Throws(() => VaultGitGuard.Enforce(kasa, rel, ScriptStatus.Unknown, null));
            bool same = !Throws(() => VaultGitGuard.Enforce(kasa, rel, ScriptStatus.Unknown, null));
            File.AppendAllText(full, "// değişti\n");
            bool noDialog = Throws(() => VaultGitGuard.Enforce(kasa, rel, ScriptStatus.Unknown, null));
            bool refused = Throws(() => VaultGitGuard.Enforce(kasa, rel, ScriptStatus.Unknown, _ => false));
            string? asked = null;
            bool approved = !Throws(() => VaultGitGuard.Enforce(kasa, rel, ScriptStatus.Unknown, q => { asked = q; return true; }));
            bool trustedAfter = !Throws(() => VaultGitGuard.Enforce(kasa, rel, ScriptStatus.Unknown, null));
            Check("betik koruması (git'siz kasa): ilk kullanımda çalışır, iş kilitlenmez", first && same);
            Check("betik koruması (git'siz kasa): betik değişince sorar; onay kutusu yoksa ya da \"Hayır\" denirse çalıştırmaz",
                noDialog && refused && asked is { Length: > 0 });
            Check("betik koruması (git'siz kasa): \"Evet\"ten sonra yeni hâline güvenilir", approved && trustedAfter);
            Check("betik koruması: özetler kasanın dışında tutulur",
                !VaultGitGuard.HashesFile.StartsWith(kasa, StringComparison.OrdinalIgnoreCase) && File.Exists(VaultGitGuard.HashesFile));
        }
        finally { VaultWriter.ReadOnlyMode = prevRo; }
    }

    static void IndependentChecks(string tmp)
    {
        void Case(string name, Func<bool> test)
        {
            try { Check(name, test()); }
            catch (Exception ex) { Check(name + " (" + ex.GetType().Name + ")", false); }
        }
        // The two old "independent" job-runner cases here (a Codex process that fails to start; two quick local
        // jobs with separate, complete logs) tested the retired per-invocation CodexRunner.Start API directly.
        // Their replacements — starting a real job through the watchman end-to-end (kota → kota-bekliyor →
        // "Şimdi dene" → bitti), two concurrent jobs' logs staying separate, and app-restart recovery — now live in
        // SelfTestV2.Run's "İş nöbetçisi" section, exercised against the real is-nobetcisi.mjs/-sahte.mjs.
        Case("bağımsız: aynı metinli görevlerden seçilen satır değişir", () =>
        {
            string path = Path.Combine(tmp, "ayni-gorev.md");
            File.WriteAllText(path, "## ✅ Yapılacaklarım\n- [ ] Tekrar\n- [ ] Tekrar\n");
            var todos = VaultReader.ParseTodos(File.ReadAllText(path));
            VaultWriter.ReadOnlyMode = false;
            try { VaultWriter.ToggleTodo(path, todos.Items[1]); }
            finally { VaultWriter.ReadOnlyMode = true; }
            var after = VaultReader.ParseTodos(File.ReadAllText(path));
            return !after.Items[0].Done && after.Items[1].Done;
        });
        Case("bağımsız: tablo başlığı değişince eski onay reddedilir", () =>
        {
            string path = Path.Combine(tmp, "baslik-degisti.md");
            string original = "## İşler\n| Firma | Durum | Not |\n|---|---|---|\n| Örnek | Onay bekliyor | Onay bekliyor |\n";
            var rows = VaultReader.ParseJobs(original).AllRows.ToList();
            string changed = original.Replace("Firma | Durum | Not", "Firma | Not | Durum");
            File.WriteAllText(path, changed);
            bool refused = false;
            VaultWriter.ReadOnlyMode = false;
            try { VaultWriter.ApproveJobs(path, rows); }
            catch (StaleFileException) { refused = true; }
            finally { VaultWriter.ReadOnlyMode = true; }
            return refused && File.ReadAllText(path) == changed;
        });
        Case("bağımsız: güvenlik JSON kökü nesne değilse atlanır", () =>
            SafetyQueue.ParsePending("null\n[]\n42\n{}", "null\n[]").Count == 0);
        Case("bağımsız: geçersiz karar onay kartını gizlemez", () =>
            SafetyQueue.ParsePending("{\"id\":\"a\"}", "{\"id\":\"a\",\"karar\":\"yanlış\"}").Count == 1);
        Case("bağımsız: bozuk Tam Gaz verisi kapalı kalır", () =>
            !TamGaz.Parse("[]", DateTimeOffset.Now).On &&
            !TamGaz.Parse("{\"saat\":1e100}", DateTimeOffset.Now).On);
        Case("bağımsız: bozuk nöbet verisi çalışıyor sayılmaz", () =>
            !TamGaz.ParseWatch("{\"pid\":1e100,\"gorev\":42}", false, _ => true).Running &&
            !TamGaz.ParseWatch("null", false, _ => true).Running);
        Case("bağımsız: bozuk istek satırı sağlam satırı engellemez", () =>
        {
            string root = Path.Combine(tmp, "bozuk-defter");
            string path = Path.Combine(root, VaultReader.LedgerRel);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "null\n[]\n{\"t\":\"2026-09-23T12:00:00Z\",\"n\":1e100}\n{\"t\":\"2026-09-23T12:00:00Z\",\"n\":1,\"metin\":\"sağlam\"}");
            var reader = new VaultReader(root);
            return reader.ReadLedger().Count == 1 && reader.Warnings.Count == 1;
        });
    }

    static void P(string s) => _out.WriteLine(s);

    [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);

    static void AttachConsoleIfAny()
    {
        // WinExe has no console; attach to the parent's so output shows in cmd/PowerShell (redirected output works regardless).
        if (!Console.IsOutputRedirected) AttachConsole(-1);
        try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }
    }
}
