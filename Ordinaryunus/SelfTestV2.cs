// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Text;
using System.Text.Json;
using Ordinaryunus.Bridge;
using Ordinaryunus.Brain;
using Ordinaryunus.Data;
using Ordinaryunus.Host;
using Ordinaryunus.Jobs;

namespace Ordinaryunus;

/// <summary>v2 checks (MIMARI §9.1 A-H): a representative, headless subset of the full test plan — every rule
/// family is exercised at least once, chosen for the highest chance of catching a real regression given the
/// time available. Never touches the real vault except read-only (section H).</summary>
public static class SelfTestV2
{
    public static void Run(string vault, string tmp, Action<string> log, Action<string, bool> check)
    {
        void P(string s) => log(s);
        void Check(string name, bool ok) => check(name, ok);

        // ---------------- A. Renderer ----------------
        string basicHtml = MarkdownRenderer.Render("# Başlık\n\nBu **kalın** ve `kod`.", NoResolve, out _);
        Check("renderer: başlık id + kalın + kod aralığı", basicHtml.Contains("<h1 id=\"h-1\">Başlık</h1>") &&
            basicHtml.Contains("<strong>kalın</strong>") && basicHtml.Contains("<code>kod</code>"));
        Check("renderer: iç içe liste", MarkdownRenderer.Render("- a\n  - b\n- c", NoResolve, out _) is { } nested &&
            nested.Contains("<ul><li>a<ul><li>b</li></ul></li><li>c</li></ul>"));
        Check("renderer: görev kutucuğu", MarkdownRenderer.Render("- [x] bitti\n- [ ] açık", NoResolve, out _) is { } tasks &&
            tasks.Contains("li class=\"task done\"") && tasks.Contains("li class=\"task\""));
        Check("renderer: tablo hizalama", MarkdownRenderer.Render("|A|B|\n|:--|--:|\n|x|y|", NoResolve, out _) is { } table &&
            table.Contains("class=\"al-l\"") && table.Contains("class=\"al-r\""));
        Check("renderer: bilinmeyen callout tipi \"note\"ya düşer", MarkdownRenderer.Render("> [!weird] T\n> gövde", NoResolve, out _)
            .Contains("callout callout-note"));
        Check("renderer: kod bloğu değişmeden kalır", MarkdownRenderer.Render("```js\nlet x=1;\n```", NoResolve, out _)
            .Contains("<pre class=\"lang-js\"><code>let x=1;</code></pre>"));
        Check("renderer: wikilink çözülür/kırık", MarkdownRenderer.Render("[[Var]] ve [[Yok]]", t => new ResolvedLink(t == "Var", t == "Var" ? "Var.md" : null), out _) is { } wl &&
            wl.Contains("a class=\"wl\" data-note=\"Var.md\"") && wl.Contains("wl broken"));
        Check("renderer: #etiket", MarkdownRenderer.Render("metin #etiket burada", NoResolve, out _).Contains("span class=\"tag\">#etiket</span>"));
        Check("renderer: embed gösterilmez, sadece metin", MarkdownRenderer.Render("![[dosya.png]]", NoResolve, out _) is { } emb &&
            emb.Contains("class=\"embed\"") && !emb.Contains("<img"));

        string[] xss =
        [
            "<script>alert(1)</script>", "<img src=x onerror=alert(1)>", "[x](javascript:alert(1))", "[[a\" onmouseover=\"x]]",
            "`</code><script>`", "> [!x\" onclick=\"y] t", "| <b>x</b> |\n|---|\n| y |", "[x](data:text/html,PHNjcmlwdD4=)",
        ];
        foreach (var v in xss)
        {
            string html = MarkdownRenderer.Render(v, t => new ResolvedLink(false, null), out _);
            // The real invariant: every "<" the renderer emits belongs to one of our own allow-listed tags (so a
            // payload like "<img ...>" can only ever appear as harmless *encoded text*, "&lt;img ...&gt;", never
            // as a live tag) and no live "<script" slipped through unescaped. A raw substring search for
            // "javascript:"/"onerror="/etc. would also flag that same harmless encoded text, so it is not used here.
            bool safe = AllowedTagsOnly(html) && !html.Contains("<script", StringComparison.OrdinalIgnoreCase);
            Check($"renderer XSS güvenli: {Md.Clip(v, 40)}", safe);
        }
        Check("renderer: yalnızca izinli etiketler üretiliyor", AllowedTagsOnly(MarkdownRenderer.Render(
            "# H\n## H2\n**b** *i* ~~s~~ ==m== `c`\n> [!warning] T\nq\n- [ ] a\n- x\n1. y\n[[Var]]\n[u](https://x.com)\n#tag\n---\n|a|b|\n|--|--|\n|1|2|\n```js\nx\n```",
            t => new ResolvedLink(true, "x.md"), out _)));

        // ---------------- B. Indexer ----------------
        string fixtureVault = Path.Combine(tmp, "beyin-kasa");
        BuildFixtureVault(fixtureVault);
        var idx = new BrainIndex(fixtureVault);
        idx.Update();
        Check("dizin: proje kartı türü", idx.Find("20 Projeler/Alfa/Alfa.md")?.Kind == "proje-karti");
        Check("dizin: Kayıt türü", idx.Find("20 Projeler/Alfa/Kayıt.md")?.Kind == "kayit");
        Check("dizin: görev türü", idx.Find("20 Projeler/Alfa/Görevler/G-001.md")?.Kind == "gorev");
        Check("dizin: kod içindeki bağlantı yok sayılır", idx.Find("20 Projeler/Alfa/Not.md") is { } notu && !notu.Links.Any(l => l.Target == "KodIci"));
        Check("dizin: alias ile çözülür", idx.Resolved.TryGetValue("20 Projeler/Alfa/Not.md", out var rl) && rl.Any(r => r.target == "20 Projeler/Alfa/Alfa.md"));
        Check("dizin: kırık bağlantı tespit edilir", idx.Broken.TryGetValue("20 Projeler/Alfa/Not.md", out var br) && br.Any(b => b.Target == "OlmayanNot"));
        Check("dizin: 80 Oturum Arşivi hiç görünmüyor", !idx.Files.Keys.Any(k => k.Contains("80 Oturum Arşivi")) && idx.Find("80 Oturum Arşivi/gizli.md") is null);
        Check("dizin: arama gizli notu bulamıyor", VaultSearch.Search(idx, [], "gizlisirmarkeri", 10).results.Count == 0);

        // A separate index/copy of the fixture vault so this destructive rewrite never disturbs the "idx" checks
        // (Problems, below) that still expect Not.md's original broken link.
        string incVault = Path.Combine(tmp, "beyin-kasa-artimli");
        BuildFixtureVault(incVault);
        var incIdx = new BrainIndex(incVault);
        incIdx.Update();
        var mtimeBefore = incIdx.Notes["20 Projeler/Alfa/Not.md"].Mtime;
        string incNotePath = Path.Combine(incVault, "20 Projeler", "Alfa", "Not.md");
        File.WriteAllText(incNotePath, "# Not\nGüncellendi.\n");
        // Force a distinguishable timestamp: on a fast machine, back-to-back writes can land within the same
        // system-clock tick (Windows' write-time source updates only every ~15 ms), which would make this a flaky
        // test rather than a real bug — BrainIndex itself already reparses on a size change regardless of mtime.
        File.SetLastWriteTimeUtc(incNotePath, mtimeBefore.AddSeconds(2));
        incIdx.Update();
        Check("dizin: artımlı güncelleme değişen dosyayı yeniden okur", incIdx.Notes["20 Projeler/Alfa/Not.md"].Mtime != mtimeBefore &&
            incIdx.Notes["20 Projeler/Alfa/Not.md"].PlainText.Contains("Güncellendi"));

        // ---------------- C. Kayıt parser ----------------
        string kayitFixture = "### 2026-09-23 23:14 — codex — örnek başlık\n- Yapılan: ilk satır\n  ikinci satır devam ediyor\n- Açık kalan: yok\n\n" +
            "### KARAR 2026-09-20 — bir karar\n- Karar: X yapılacak\n";
        var entries = KayitParser.Parse(kayitFixture, "20 Projeler/Alfa/Kayıt.md", "Alfa");
        Check("Kayıt: devir + çok satırlı alan", entries.Any(e => e.Kind == "devir" && e.Get("Yapılan") == "ilk satır ikinci satır devam ediyor"));
        Check("Kayıt: KARAR ayrıştırıldı", entries.Any(e => e.Kind == "karar" && e.Title == "bir karar" && e.Get("Karar") == "X yapılacak"));
        Check("Kayıt: en son devir seçimi", KayitParser.LatestDevir(entries)?.Title == "örnek başlık");

        // ---------------- D. Problems + health ----------------
        var (levelBad, scoreBad) = ProblemDetector.ComputeHealth(new List<ProblemDto>
        {
            new("x", "P", "kritik", "sistem", null, "t", "", "", null, null),
        });
        Check("sağlık: 1 kritik → risk", levelBad == "risk" && scoreBad == 75);
        var (levelGood, scoreGood) = ProblemDetector.ComputeHealth([]);
        Check("sağlık: sorun yok → iyi/100", levelGood == "iyi" && scoreGood == 100);

        // bekleyen kritik güvenlik onayları (P08) TEK BAŞINA puanı sıfıra
        // düşürmemeli — "dikkat" ağırlığıyla girer, gerçek bir kriz (başka bir kritik kural) hâlâ "risk" yapar.
        var pendingSafetyProblems = Enumerable.Range(0, 4)
            .Select(i => new ProblemDto($"P08:{i}", "P08", "kritik", "guvenlik", null, "onay bekliyor", "", "", null, null)).ToList();
        var (levelPending, scorePending) = ProblemDetector.ComputeHealth(pendingSafetyProblems);
        Check("A10: 4 bekleyen kritik onay TEK BAŞINA puanı sıfırlamaz (eskiden 100-25*4=0 olurdu)",
            scorePending == 100 - 6 * 4 && levelPending != "risk");
        Check("A10: HealthHint gerekçe metni dolu", ProblemDetector.HealthHint(pendingSafetyProblems).Contains("4"));
        Check("A10: bekleyen onay yokken HealthHint boş", ProblemDetector.HealthHint([]).Length == 0);
        var realCrisis = pendingSafetyProblems.Append(new ProblemDto("P03:x", "P03", "kritik", "proje", "X", "bırakma geçti", "", "", null, null)).ToList();
        Check("A10: gerçek bir kriz (P08 dışı kritik) hâlâ \"risk\" yapar", ProblemDetector.ComputeHealth(realCrisis).level == "risk");
        var snapFixture = VaultSnapshot.Load(fixtureVault);
        var kayitByProject = new Dictionary<string, List<KayitEntry>> { ["Alfa"] = entries };
        var lightsFixture = new List<LightDto>();
        var problems = ProblemDetector.Run(snapFixture, idx, [], lightsFixture, kayitByProject, new DateTime(2026, 9, 24));
        Check("sorunlar: P01 (kırık bağlantı) üretildi", problems.Any(p => p.Rule == "P01"));
        Check("sorunlar: şablon notundaki kırık bağlantı P01 üretmiyor", !problems.Any(p => p.Id.Contains("Sablon")));

        // ---------------- E. Bridge ----------------
        RunBridgeChecks(fixtureVault, tmp, Check);

        // ---------------- F. Claude runner ----------------
        string claudeBase = Path.Combine(tmp, "claude-code");
        foreach (var v in new[] { "2.1.99", "2.1.274", "2.1.280", "abc" })
        {
            string d = Path.Combine(claudeBase, v);
            Directory.CreateDirectory(d);
            if (v != "abc") File.WriteAllText(Path.Combine(d, "claude.exe"), "x");
        }
        var found = ClaudeCli.Find(claudeBase);
        Check("Claude keşfi: en yüksek sürüm seçilir (2.1.280, string sıralama değil)", found?.Version?.ToString() == "2.1.280");
        string yerelExe = Path.Combine(tmp, "yerel-bin", "claude.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(yerelExe)!);
        File.WriteAllText(yerelExe, "x");
        Check("Claude keşfi: masaüstü uygulaması yoksa yedek yol (.local\\bin, PATH) kullanılır",
            ClaudeCli.Find(Path.Combine(tmp, "claude-code-yok"), [Path.Combine(tmp, "olmayan", "claude.exe"), yerelExe])?.Exe == yerelExe);
        Check("Claude keşfi: sürüm klasörü varsa yedek yollara bakılmaz", ClaudeCli.Find(claudeBase, [yerelExe])?.Version?.ToString() == "2.1.280");

        var argsNoDir = ClaudeCli.BuildArguments("test istemi", null);
        Check("Claude argümanları (dizin yok)", argsNoDir is
            ["-p", "test istemi Follow AGENTS.md (lock, handoff, never commit).", "--output-format", "stream-json", "--verbose",
             "--permission-mode", "acceptEdits", "--permission-prompts", "none", "--allowedTools", ClaudeCli.AllowedTools]);
        var argsDir = ClaudeCli.BuildArguments("test istemi", @"C:\proje");
        Check("Claude argümanları (--add-dir ile)", argsDir is
            ["-p", "test istemi Follow AGENTS.md (lock, handoff, never commit).", "--output-format", "stream-json", "--verbose",
             "--permission-mode", "acceptEdits", "--permission-prompts", "none", "--add-dir", @"C:\proje", "--allowedTools", ClaudeCli.AllowedTools]);
        Check("Claude: \"-\" ile başlayan istem \"Görev: \" ile başlar", ClaudeCli.BuildFinalPrompt("- liste").StartsWith("Görev: - liste"));
        Check("Claude: yasaklı bayraklar yok", !argsDir.Any(a => a.Contains("dangerously", StringComparison.OrdinalIgnoreCase) || a.Contains("bypassPermissions") || a == "--bare" || a == "--safe-mode"));
        var envDict = new Dictionary<string, string?> { ["ANTHROPIC_API_KEY"] = "x", ["ANTHROPIC_AUTH_TOKEN"] = "y", ["CLAUDECODE"] = "1", ["PATH"] = "z" };
        var stripped = ClaudeCli.BuildEnvironment(envDict);
        Check("Claude: ortam değişkenleri temizlendi", !stripped.ContainsKey("ANTHROPIC_API_KEY") && !stripped.ContainsKey("ANTHROPIC_AUTH_TOKEN") &&
            !stripped.ContainsKey("CLAUDECODE") && stripped.ContainsKey("PATH"));

        var state = new ClaudeJobState();
        var t0 = DateTime.Now;
        ClaudeStreamParser.Feed(state, """{"type":"system","subtype":"init","session_id":"s1"}""", t0);
        ClaudeStreamParser.Feed(state, """{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Edit","input":{"file_path":"01 Şimdi.md"}}]}}""", t0);
        ClaudeStreamParser.Feed(state, """{"type":"user","message":{"content":[{"type":"tool_result","is_error":true,"content":[{"type":"text","text":"Güvenlik kapısı: #20260923-1f026c bekleniyor"}]}]}}""", t0);
        ClaudeStreamParser.Feed(state, """{"type":"result","is_error":false,"result":"tamam","permission_denials":[{"tool_name":"Bash","tool_input":{"command":"npm install"}}]}""", t0);
        Check("stream ayrıştırıcı: başarı ucu (sessionId, filesChanged, gate, denials, bitti)", state.SessionId == "s1" &&
            state.FilesChanged.Contains("01 Şimdi.md") && state.SafetyIds.Contains("20260923-1f026c") &&
            state.Denials.Any(d => d.StartsWith("Bash")) && state.State == "bitti");

        var authState = new ClaudeJobState();
        ClaudeStreamParser.Feed(authState, """{"type":"result","is_error":true,"result":"OAuth token has expired. Please run /login"}""", t0);
        Check("stream ayrıştırıcı: giriş gerekli algılanır", authState.State == "giris" && authState.Fix is { Length: > 0 });

        var quotaState = new ClaudeJobState();
        ClaudeStreamParser.Feed(quotaState, """{"type":"result","is_error":true,"result":"5-hour limit reached"}""", t0);
        Check("stream ayrıştırıcı: kota algılanır", quotaState.State == "kota");

        // Bash tamamen yok, WebFetch sadece web:true'da.
        // C (EK-v2.1 §2b): keep-awake kararı — iş sürerken/kota beklerken/Tam Gaz açıkken açık, hiçbiri yokken kapalı,
        // ayar kapalıysa hiç açılmaz.
        var jobRunning = new List<JobDto> { new("claude:1", "claude", "t", "p", null, null, "calisiyor", "Çalışıyor", "teal",
            "2026-01-01T00:00:00+03:00", null, 0, null, "", 0, [], [], [], false, null, null, null, true, null, 0) };
        Check("C: PowerManagement.Compute — çalışan iş varken açık", PowerManagement.Compute(true, jobRunning, false) is (true, { Length: > 0 }));
        Check("C: PowerManagement.Compute — iş/Tam Gaz yokken kapalı", PowerManagement.Compute(true, [], false) is (false, ""));
        Check("C: PowerManagement.Compute — ayar kapalıysa hiç açılmaz", PowerManagement.Compute(false, jobRunning, true) is (false, ""));
        Check("C: PowerManagement.Compute — Tam Gaz tek başına açar", PowerManagement.Compute(true, [], true) is (true, { Length: > 0 }));

        // D2c: ilk açılış varsayılanı — ≤1600 ve arası 125, ≥2200 150.
        Check("D2c: ekran varsayılanı — 1536 (%125 ölçekli 1920 px dizüstü) → 125", Host.ScreenDefaults.DefaultUiScale(1536) == 125);
        Check("D2c: ekran varsayılanı — 1920 (arası) → 125", Host.ScreenDefaults.DefaultUiScale(1920) == 125);
        Check("D2c: ekran varsayılanı — 2560 (geniş) → 150", Host.ScreenDefaults.DefaultUiScale(2560) == 150);

        Check("A1: izinli araç listesinde Bash yok", !ClaudeCli.AllowedToolsBase.Contains("Bash", StringComparison.Ordinal) &&
            !ClaudeCli.AllowedToolsFor(true).Contains("Bash", StringComparison.Ordinal));
        Check("A1: liste tam olarak Read,Grep,Glob,Edit,Write,TodoWrite,WebSearch(+WebFetch)",
            ClaudeCli.AllowedToolsBase == "Read,Grep,Glob,Edit,Write,TodoWrite,WebSearch" &&
            ClaudeCli.AllowedToolsFor(false) == ClaudeCli.AllowedToolsBase &&
            ClaudeCli.AllowedToolsFor(true) == ClaudeCli.AllowedToolsBase + ",WebFetch");
        string nobetSrc = Md.ReadText(Path.Combine(vault, "_sistem", "araclar", "is-nobetcisi.mjs")) ?? "";
        if (nobetSrc.Length > 0)
        {
            Check("A1: C# izinli araç listesi nöbetçininkiyle (CLAUDE_IZINLI) eşleşiyor", nobetSrc.Contains($"'{ClaudeCli.AllowedToolsBase}'"));
            // the check above only looks at the allow-list TEXT and would stay
            // green even if the real shell/MCP guards were deleted — those live in --disallowedTools and
            // --strict-mcp-config, not in --allowedTools (which only picks which allowed tools skip the
            // confirmation prompt; MIMARI §6.2). Check the actual flags directly on the real source
            // instead of trusting ClaudeCli.BuildArguments, which no longer emits them (see its own doc comment).
            Check("A1: nöbetçi --disallowedTools ile Bash'ı her zaman kapatıyor",
                System.Text.RegularExpressions.Regex.IsMatch(nobetSrc, @"--disallowedTools[^\n]*Bash"));
            Check("A1: nöbetçi --strict-mcp-config kullanıyor (harici MCP sunucusu eklenemez)", nobetSrc.Contains("--strict-mcp-config"));
        }
        else P("   (kasadaki is-nobetcisi.mjs bulunamadı — A1 eşleşme kontrolü atlandı.)");
        Check("BuildFinalPrompt: istem+ek metin 4000'i asla geçmez (nöbetçinin kendi sınırı)",
            ClaudeCli.BuildFinalPrompt(new string('a', 5000)).Length == 4000);

        // ---------------- F2. İş nöbetçisi (B, EK-v2.1 §1-2) ----------------
        RunNobetciChecks(vault, tmp, Check, P);

        // ---------------- G. Host/web contract ----------------
        string normalized(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
        string indexPath = Path.Combine(AppPaths.WwwRoot, "index.html");
        if (File.Exists(indexPath))
        {
            string html = File.ReadAllText(indexPath);
            Check("index.html: CSP tam olarak WebViewConfig.Csp ile eşleşiyor",
                normalized(html).Contains(normalized($"content=\"{WebViewConfig.Csp}\"")));
            Check("index.html: mutlak http(s) kaynak yok", !System.Text.RegularExpressions.Regex.IsMatch(html, "(src|href)=\"https?://"));
        }
        else P("   (index.html henüz yok — frontend bu dosyayı ekleyecek; CSP sabiti ve gezinme kuralları aşağıda ayrıca test edildi.)");
        Check("IsAllowedNavigation: izinli", WebViewConfig.IsAllowedNavigation("https://ordinaryunus.example/index.html") &&
            WebViewConfig.IsAllowedNavigation("https://ordinaryunus.example/index.html?shot=1"));
        Check("IsAllowedNavigation: reddedilenler", !WebViewConfig.IsAllowedNavigation("https://baska.example/index.html") &&
            !WebViewConfig.IsAllowedNavigation("http://ordinaryunus.example/index.html") &&
            !WebViewConfig.IsAllowedNavigation("https://ordinaryunus.example/baska.html") &&
            !WebViewConfig.IsAllowedNavigation("about:blank") && !WebViewConfig.IsAllowedNavigation("file:///C:/x") &&
            !WebViewConfig.IsAllowedNavigation("data:text/html,x"));
        string echartsPath = Path.Combine(AppPaths.WwwRoot, "lib", "echarts.min.js");
        string versionsPath = Path.Combine(AppPaths.WwwRoot, "lib", "VERSIONS.txt");
        if (File.Exists(echartsPath) && File.Exists(versionsPath))
        {
            string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(echartsPath))).ToLowerInvariant();
            Check("lib/echarts.min.js: sha256 VERSIONS.txt ile eşleşiyor", File.ReadAllText(versionsPath).Contains(hash, StringComparison.OrdinalIgnoreCase));
        }
        else P("   (lib/echarts.min.js veya VERSIONS.txt henüz yok.)");

        // ---------------- H. Test edilen kasa (salt okunur: demo kasa ya da kullanıcının kendi kasası) ----------------
        try
        {
            var swReal = System.Diagnostics.Stopwatch.StartNew();
            var realIdx = new BrainIndex(vault);
            realIdx.Update();
            var realGit = Directory.Exists(vault) ? GitActivity.Load(vault) : new GitActivity { Available = false };
            var realSnap = VaultSnapshot.Load(vault);
            var realLights = Mapper.MapLights(realSnap, [], DateTime.Now);
            var realKayit = new Dictionary<string, List<KayitEntry>>();
            foreach (var n in realIdx.Notes.Values.Where(n => n.Kind == "kayit" && n.Project is not null))
                realKayit[n.Project!] = KayitParser.Parse(n.RawText, n.RelPath, n.Project);
            var realProblems = ProblemDetector.Run(realSnap, realIdx, [], realLights, realKayit, DateTime.Now);
            swReal.Stop();
            var c = new CountsDto(realProblems.Count(p => p.Severity == "kritik"), realProblems.Count(p => p.Severity == "dikkat"), realProblems.Count(p => p.Severity == "bilgi"));
            P($"Beyin (kasa): {realIdx.Notes.Count} not, {realIdx.Resolved.Values.Sum(l => l.Count)} bağlantı, " +
              $"{realIdx.Broken.Values.Sum(l => l.Count)} kırık, sorunlar {c.Kritik}/{c.Dikkat}/{c.Bilgi}, dizin {realIdx.IndexMs} ms, toplam {swReal.ElapsedMilliseconds} ms");
            foreach (var p in realProblems.Take(5)) P("   ! " + p.Title);
            Check("kasa: 3000 ms bütçesi", swReal.ElapsedMilliseconds < 3000);
        }
        catch (Exception ex) { Check("kasa okunamadı (" + ex.Message + ")", false); }
    }

    /// <summary>
    /// B.8 (EK-v2.1): end-to-end against the REAL <c>_sistem/araclar/is-nobetcisi.mjs</c> with
    /// <c>--sahte is-nobetcisi-sahte.mjs</c> (never the real Claude/Codex CLI, never a real quota) — the exact
    /// kota → kota-bekliyor → "Şimdi dene" → bitti flow the acceptance criteria ask for — plus running the vault's
    /// own <c>is-nobetcisi-test.mjs</c> (72 pure-function tests) and requiring it to pass. All job files land under
    /// ORDINARYUNUS_DATA (a %TEMP% folder, set by SelfTest.UnitChecks before calling in here); the watchman's own
    /// script is only READ from the real vault, never written to, and its cwd is set to the vault only because
    /// --dry-run's fake tool never touches the filesystem at all.
    /// </summary>
    static void RunNobetciChecks(string vault, string tmp, Action<string, bool> check, Action<string> log)
    {
        void Check(string name, bool ok) => check(name, ok);
        void P(string s) => log(s);

        var argBuild = NobetciRunner.BuildArguments("is-nobetcisi.mjs", "claude", "20260101-000000-000-abcdef01",
            "x.istem.txt", "isler", @"C:\proje", true, "sahte.mjs", 5);
        Check("nöbetçi argümanları (ekdizin + web + sahte + pay-sn)", argBuild is
            ["is-nobetcisi.mjs", "--arac", "claude", "--is", "20260101-000000-000-abcdef01", "--istem-dosyasi", "x.istem.txt",
             "--klasor", "isler", "--ekdizin", @"C:\proje", "--web", "--sahte", "sahte.mjs", "--pay-sn", "5"]);
        var argCodexNoExtra = NobetciRunner.BuildArguments("is-nobetcisi.mjs", "codex", "d", "x.txt", "isler", "C:\\proje", true, null, null);
        Check("nöbetçi argümanları: --ekdizin/--web sadece Claude içindir", argCodexNoExtra is
            ["is-nobetcisi.mjs", "--arac", "codex", "--is", "d", "--istem-dosyasi", "x.txt", "--klasor", "isler"]);

        var ns = NobetStatus.Parse("""{"is":"x","arac":"claude","pid":123,"durum":"kota-bekliyor","deneme":2,"bekleUntil":"2026-09-24T10:00:00Z"}""");
        Check("nobet.json ayrıştırma", ns is { Durum: "kota-bekliyor", Deneme: 2, Pid: 123 } && ns.BekleUntil is not null && ns.IsNonTerminal);
        Check("nobet.json: bozuk/boş içerik çökertmez", NobetStatus.Parse("null") is null && NobetStatus.Parse("") is null);

        var cs = new CodexJobState();
        var tc = DateTime.Now;
        CodexStreamParser.Feed(cs, """{"type":"thread.started","thread_id":"t1"}""", tc);
        CodexStreamParser.Feed(cs, """{"type":"item.completed","item":{"type":"command_execution","command":"ls -la"}}""", tc);
        CodexStreamParser.Feed(cs, """{"type":"turn.completed"}""", tc);
        Check("Codex akış ayrıştırıcı: thread + komut adımı + bitti", cs.SessionId == "t1" && cs.Steps == 1 && cs.State == "bitti");
        var cf = new CodexJobState();
        CodexStreamParser.Feed(cf, """{"type":"turn.failed","error":{"message":"Not logged in. Please run codex login."}}""", tc);
        Check("Codex akış ayrıştırıcı: giriş gerekli algılanır", cf.State == "giris");
        var cq = new CodexJobState();
        CodexStreamParser.Feed(cq, """{"type":"error","message":"You\u2019ve hit your usage limit. try again in 3 seconds."}""", tc);
        Check("Codex akış ayrıştırıcı: kota algılanır", cq.State == "kota");

        string script = Path.Combine(vault, "_sistem", "araclar", "is-nobetcisi.mjs");
        if (!File.Exists(script))
        {
            P("   (kasadaki iş nöbetçisi bulunamadı — uçtan uca nöbetçi testi atlandı.)");
            return;
        }
        bool prevDry = NobetciRunner.DryRun;

        // Uçtan uca: kota → kota-bekliyor → "Şimdi dene" → devam → bitti, sonra uygulama yeniden açılma senaryosu.
        // Independent try/catch so a hiccup here (or the vault having a stray real Acil Durdur flag) still leaves
        // the cancel scenario and is-nobetcisi-test.mjs below able to run.
        string? id = null;
        try
        {
            NobetciRunner.DryRun = true; // --sahte is-nobetcisi-sahte.mjs kullanılır; gerçek Claude/Codex asla çağrılmaz
            var job = JobStore.StartClaude(vault, "Selftest: sadece AGENTS.md oku, hiçbir şey yazma.", null, null, null, false);
            id = job.Id;
            Check("nöbetçi: iş başlatıldı", job.State is "basliyor" or "calisiyor");
            bool sawKotaBekliyor = SpinWaitJobState(id, s => s == "kota-bekliyor", 12_000);
            var waiting = JobStore.Get(id);
            Check("nöbetçi: kota → kota-bekliyor (sahte modu \"kota-sonra-bitti\")", sawKotaBekliyor);
            Check("nöbetçi: kota-bekliyor iken resumeAt (ISO) dolu", waiting?.ResumeAt is { Length: > 0 });
            Check("nöbetçi: kota-bekliyor iken \"kaldığın yerden\" bekleniyor metni", (waiting?.StateText ?? "").Contains("Kota dolu"));
            var retry = JobStore.RetryNow(id);
            Check("nöbetçi: \"Şimdi dene\" (retryJobNow) kabul edildi", retry == JobStore.RetryResult.Ok);
            Check("nöbetçi: kota-bekliyor değilken \"Şimdi dene\" NotFound döner", JobStore.RetryNow("claude:yok-boyle-bir-is") == JobStore.RetryResult.NotFound);
            bool finished = SpinWaitJobState(id, s => s == "bitti", 15_000);
            Check("nöbetçi: \"Şimdi dene\" sonrası devam edip bitti", finished);

            // App restart recovery: forget in-memory state, force a rescan from disk only.
            JobStore.ForgetAllForTest();
            var rediscovered = JobStore.Get(id);
            Check("nöbetçi: uygulama yeniden açılınca iş diskten yeniden bulunuyor (bitti/history)", rediscovered is { State: "bitti" });
        }
        catch (Exception ex) { Check("nöbetçi uçtan uca akışı (" + ex.GetType().Name + ": " + ex.Message + ")", false); }
        finally
        {
            if (id is not null) JobStore.Cancel(id); // no-op once terminal; harmless if still waiting
            NobetciRunner.DryRun = prevDry;
        }

        // İptal (cancel) senaryosu: ayrı bir iş, hemen iptal edilir.
        try
        {
            NobetciRunner.DryRun = true;
            var job2 = JobStore.StartCodex(vault, "Selftest iptal", null, null);
            Check("nöbetçi: cancelJob bilinmeyen iş için false döner", !JobStore.Cancel("codex:yok-boyle-bir-is"));
            bool cancelled = JobStore.Cancel(job2.Id);
            Check("nöbetçi: iptal sinyali kabul edildi", cancelled);
            bool sawDurduruldu = SpinWaitJobState(job2.Id, s => s is "durduruldu" or "bitti" or "hata", 8_000);
            Check("nöbetçi: iptalden sonra iş sonlanıyor (≤ birkaç sn)", sawDurduruldu);
        }
        catch (Exception ex) { Check("nöbetçi iptal senaryosu (" + ex.GetType().Name + ": " + ex.Message + ")", false); }
        finally { NobetciRunner.DryRun = prevDry; }

        // is-nobetcisi-test.mjs (kasanın kendi testleri) — gerçek kota harcamaz, kendi %TEMP% klasörüne yazar. Testin
        // toplam sayısı burada sabit yazılmaz (sürümden sürüme değişir). Süre sınırı 180 sn: testin içinde gerçek
        // bekleme süreli uçtan uca senaryolar (kota bekleme, iptal, "Şimdi dene", eşzamanlı nöbetçiler) Claude ve Codex
        // için ayrı ayrı koşar ve tek başına yaklaşık 60-70 sn sürer; eski 60 sn sınırı bu yüzden testi başarılıyken
        // "zaman aşımı" diye düşürüyordu.
        string testScript = Path.Combine(vault, "_sistem", "araclar", "is-nobetcisi-test.mjs");
        if (File.Exists(testScript))
        {
            var r = ProcessRunner.Run("node", [testScript], vault, 180_000);
            foreach (var line in Md.Lines(r.Output).TakeLast(3)) P("   is-nobetcisi-test.mjs: " + line);
            Check("is-nobetcisi-test.mjs geçti (kasanın kendi testleri, süre sınırı 180 sn)", !r.TimedOut && r.ExitCode == 0);
        }
        else P("   (is-nobetcisi-test.mjs bulunamadı — atlandı.)");
    }

    static bool SpinWaitJobState(string id, Func<string, bool> predicate, int timeoutMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var j = JobStore.Get(id);
            if (j is not null && predicate(j.State)) return true;
            Thread.Sleep(250);
        }
        return false;
    }

    static ResolvedLink NoResolve(string _) => new(false, null);

    static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "h1", "h2", "h3", "h4", "h5", "h6", "p", "br", "strong", "em", "del", "mark", "code", "pre", "blockquote", "div",
        "ul", "ol", "li", "span", "table", "thead", "tbody", "tr", "th", "td", "hr", "a",
    };

    static bool AllowedTagsOnly(string html)
    {
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(html, "</?([a-zA-Z0-9]+)"))
            if (!AllowedTags.Contains(m.Groups[1].Value)) return false;
        return true;
    }

    static void BuildFixtureVault(string root)
    {
        Directory.CreateDirectory(root);
        void W(string rel, string content)
        {
            string full = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content, new UTF8Encoding(false));
        }
        W("01 Şimdi.md", "# Şimdi\n\n## ✅ Yapılacaklarım\n- [ ] test\n");
        W("20 Projeler/Alfa/Alfa.md", "---\ndurum: aktif\nodak: true\naliases: [\"Proje Alfa\"]\n---\n# Alfa\n## Bitti Tanımı\n- [ ] madde\n");
        W("20 Projeler/Alfa/Kayıt.md", "# Kayıt\n### 2026-09-23 10:00 — claude — ilk\n- Yapılan: test\n");
        W("20 Projeler/Alfa/Görevler/G-001.md", "---\ndurum: hazir\n---\n# G-001 Görev\n");
        W("20 Projeler/Alfa/Not.md", "# Not\nBağlantı: [[Proje Alfa]] ve [[OlmayanNot]].\nKod içi: `[[KodIci]]`\n```\n[[KodIciBlok]]\n```\n");
        W("30 Park Yeri/Fikir.md", "---\ntarih: 2020-01-01\n---\n# Fikir\nEski bir fikir.\n");
        W("_sistem/Şablonlar/Sablon.md", "# Şablon\n[[BuHicBirZamanOlmayacak]]\n");
        W("80 Oturum Arşivi/gizli.md", "gizlisirmarkeri\n");
    }

    static void RunBridgeChecks(string vault, string tmp, Action<string, bool> check)
    {
        void Check(string name, bool ok) => check(name, ok);
        var settings = new Security.AppSettings { KasaYolu = vault };
        var app = new Ordinaryunus.Host.AppState(settings, dryRun: false, screenshotMode: false);
        app.ReloadAsync(true).GetAwaiter().GetResult();
        var bridge = new BridgeHost(app, _ => { });

        string Send(string raw) => bridge.HandleAsync(raw).GetAwaiter().GetResult() ?? "";

        Check("köprü: JSON olmayan mesaj yanıt üretmiyor", Send("değil-json") == "");
        Check("köprü: 70000 karakter reddedilir", Send(new string('a', 70_000)) == "");
        Check("köprü: id eksik", JsonOk(Send("""{"type":"hello","payload":{}}""")) is null);
        Check("köprü: id metin", JsonOk(Send("""{"id":"1","type":"hello","payload":{}}""")) is null);
        Check("köprü: payload dizi", JsonOk(Send("""{"id":1,"type":"hello","payload":[]}""")) is null);
        Check("köprü: bilinmeyen tür", ErrCode(Send("""{"id":1,"type":"uydurma","payload":{}}""")) == "unknown_type");
        Check("köprü: bilinmeyen alan reddedilir", ErrCode(Send("""{"id":1,"type":"login","payload":{"password":"x","fazla":1}}""")) == "bad_request");

        Check("köprü: kilitliyken getSnapshot reddedilir", ErrCode(Send("""{"id":1,"type":"getSnapshot","payload":{}}""")) == "locked");
        var helloPayload = JsonOk(Send("""{"id":1,"type":"hello","payload":{}}"""));
        Check("köprü: kilitliyken hello çalışır", helloPayload is not null);
        Check("köprü: hello imza alanı Imza.cs'le aynı (yapan, surum, github, lisans) ve appVersion korunuyor",
            helloPayload is { } hp && hp.TryGetProperty("appVersion", out _) && hp.TryGetProperty("imza", out var im) &&
            im.ValueKind == JsonValueKind.Object && Str(im, "yapan") == Imza.Yapan && Str(im, "surum") == Imza.Surum &&
            Str(im, "github") == Imza.GitHub && Str(im, "lisans") == Imza.Lisans);

        string hasPasswordJson = Send("""{"id":2,"type":"getAuthState","payload":{}}""");
        bool hasPassword = JsonOk(hasPasswordJson)!.Value.GetProperty("hasPassword").GetBoolean();
        if (!hasPassword) Send("""{"id":3,"type":"setPassword","payload":{"password":"test12345","repeat":"test12345"}}""");
        else app.Unlocked = true;

        Check("köprü: getNote path traversal reddedilir", ErrCode(Send("""{"id":4,"type":"getNote","payload":{"path":"../../Windows/win.ini"}}""")) == "bad_request");
        Check("köprü: getNote 80 Oturum Arşivi asla bulunamaz", ErrCode(Send("""{"id":5,"type":"getNote","payload":{"path":"80 Oturum Arşivi/gizli.md"}}""")) == "not_found");
        Check("köprü: getNote indekslenmemiş yol", ErrCode(Send("""{"id":6,"type":"getNote","payload":{"path":"yok.md"}}""")) == "not_found");
        Check("köprü: openUrl javascript: reddedilir", ErrCode(Send("""{"id":7,"type":"openUrl","payload":{"url":"javascript:alert(1)"}}""")) == "bad_request");
        Check("köprü: openUrl file: reddedilir", ErrCode(Send("""{"id":8,"type":"openUrl","payload":{"url":"file:///C:/"}}""")) == "bad_request");
        Check("köprü: openUrl kullanıcı bilgisi reddedilir", ErrCode(Send("""{"id":9,"type":"openUrl","payload":{"url":"http://user:pw@x.com"}}""")) == "bad_request");
        Check("köprü: openFolder which=C:\\\\ reddedilir", ErrCode(Send("""{"id":10,"type":"openFolder","payload":{"which":"C:\\","project":null,"sub":null,"path":null}}""")) == "bad_request");
        Check("köprü: runShortcut bilinmeyen id reddedilir", ErrCode(Send("""{"id":11,"type":"runShortcut","payload":{"id":"rm"}}""")) == "bad_request");
        Check("köprü: toggleTodo bilinmeyen anahtar stale döner", ErrCode(Send("""{"id":12,"type":"toggleTodo","payload":{"key":"td-999-deadbeef"}}""")) == "stale");
        Check("köprü: decideSafety kritik onayda confirmCritical gerekir",
            ErrCode(Send("""{"id":13,"type":"decideSafety","payload":{"id":"x","approve":true,"confirmCritical":false}}""")) is "bad_request" or "not_found");

        Directory.CreateDirectory(SafetyQueue.Dir(vault));
        File.WriteAllText(SafetyQueue.StopFlag(vault), "test");
        Check("köprü: DURDUR varken runClaude \"stopped\" döner", ErrCode(Send("""{"id":14,"type":"runClaude","payload":{"text":"deneme","role":null,"project":null}}""")) == "stopped");
        try { File.Delete(SafetyQueue.StopFlag(vault)); } catch { }

        var validSnapshot = Send("""{"id":15,"type":"getSnapshot","payload":{}}""");
        var okPayload = JsonOk(validSnapshot);
        Check("köprü: getSnapshot alan adları (stamp, todos, projects)", okPayload is not null &&
            okPayload.Value.TryGetProperty("stamp", out _) && okPayload.Value.TryGetProperty("todos", out _) && okPayload.Value.TryGetProperty("projects", out _));
        Check("köprü: company.keepAwake alanı var (SÖZLEŞME EKİ)", okPayload!.Value.TryGetProperty("company", out var comp) && comp.TryGetProperty("keepAwake", out _));

        // decideSafety artık listede gösterilen ÖZETİ (summary) geri ister;
        // kuyruktaki kayıt değişmese bile YANLIŞ bir özetle onay/ret reddedilir ("değişmiş, onaylanamaz").
        File.WriteAllText(SafetyQueue.PendingFile(vault),
            "{\"id\":\"a2test\",\"zaman\":\"2026-09-24T10:00:00Z\",\"arac\":\"claude\",\"islem\":\"Dosya silme\",\"hedef\":\"eski.md\",\"komut\":\"rm eski.md\",\"risk\":\"kritik\",\"neden\":\"gereksiz\"}\n");
        // AppState.ReloadAsync coalesces overlapping reloads (an earlier setPassword/login already queued one in
        // the background, fire-and-forget) — its returned Task can complete without the NEW file actually being
        // picked up yet, so poll briefly instead of trusting a single await.
        JsonElement a2Row = default;
        for (int i = 0; i < 40 && a2Row.ValueKind != JsonValueKind.Object; i++)
        {
            app.ReloadAsync(true).GetAwaiter().GetResult();
            var snapNow = JsonOk(Send("""{"id":16,"type":"getSnapshot","payload":{}}"""));
            if (snapNow is { } sv && sv.TryGetProperty("safety", out var arr))
                a2Row = arr.EnumerateArray().FirstOrDefault(s => s.GetProperty("id").GetString() == "a2test");
            if (a2Row.ValueKind != JsonValueKind.Object) Thread.Sleep(50);
        }
        string? realSummary = a2Row.ValueKind == JsonValueKind.Object ? a2Row.GetProperty("summary").GetString() : null;
        Check("köprü: SafetyDto.summary 8 hex karakter", realSummary is { Length: 8 });
        Check("köprü: decideSafety yanlış özetle reddedilir (stale, \"değişmiş\")",
            ErrCode(Send("""{"id":17,"type":"decideSafety","payload":{"id":"a2test","approve":false,"confirmCritical":false,"summary":"deadbeef"}}""")) == "stale");
        // Doğru özetle "stale" ARTIK dönmemeli — selftest'in kendi salt-okunur korumasına (VaultWriter.ReadOnlyMode)
        // takılıp "readonly" dönmesi, tam da özet kontrolünü GEÇTİĞİNİN kanıtı (gerçek kasaya hiçbir şey yazılmaz).
        Check("köprü: decideSafety doğru özetle özet kontrolünü geçer (yalnızca selftest'in salt-okunur korumasına takılır)",
            ErrCode(Send("{\"id\":18,\"type\":\"decideSafety\",\"payload\":{\"id\":\"a2test\",\"approve\":true,\"confirmCritical\":true,\"summary\":\"" + realSummary + "\"}}")) == "readonly");

        // the gate saves a command truncated at exactly 600 chars but computes
        // the card's summary hash from the FULL command — a command that hits that cap must be treated as
        // "kesildi" even though the gate itself does not write that field yet, or "Onaylıyorum" would silently
        // approve more than what the card shows.
        string longKomut = new string('a', 600);
        File.WriteAllText(SafetyQueue.PendingFile(vault),
            "{\"id\":\"kesiktest\",\"zaman\":\"2026-09-24T10:00:00Z\",\"arac\":\"claude\",\"islem\":\"Komut\",\"hedef\":\"x\",\"komut\":\"" + longKomut + "\",\"risk\":\"dikkat\",\"neden\":\"test\"}\n");
        var directParsed = SafetyQueue.ParsePending(Md.ReadText(SafetyQueue.PendingFile(vault)), null);
        Check("#6: SafetyQueue.ParsePending — tam 600 karakterlik komut \"kesildi\" sayılır (gate alanı yazmasa bile)",
            directParsed.FirstOrDefault(s => s.Id == "kesiktest") is { Kesildi: true });

        JsonElement kesikRow = default;
        for (int i = 0; i < 40 && kesikRow.ValueKind != JsonValueKind.Object; i++)
        {
            app.ReloadAsync(true).GetAwaiter().GetResult();
            var snapNow = JsonOk(Send("""{"id":21,"type":"getSnapshot","payload":{}}"""));
            if (snapNow is { } sv && sv.TryGetProperty("safety", out var arr))
                kesikRow = arr.EnumerateArray().FirstOrDefault(s => s.GetProperty("id").GetString() == "kesiktest");
            if (kesikRow.ValueKind != JsonValueKind.Object) Thread.Sleep(50);
        }
        Check("#6: SafetyDto.kesildi köprüden true olarak geçiyor", kesikRow.ValueKind == JsonValueKind.Object && kesikRow.GetProperty("kesildi").GetBoolean());
        string? kesikSummary = kesikRow.ValueKind == JsonValueKind.Object ? kesikRow.GetProperty("summary").GetString() : null;
        Check("#6: decideSafety — kesildi bir isteği onaylamak \"forbidden\" döner (Reddet hâlâ açık kalabilir)",
            ErrCode(Send("{\"id\":22,\"type\":\"decideSafety\",\"payload\":{\"id\":\"kesiktest\",\"approve\":true,\"confirmCritical\":true,\"summary\":\"" + kesikSummary + "\"}}")) == "forbidden");

        // the decision line now carries the content
        // hash it was decided for, forward-compatible with a future gate-side check (SafetyQueue.DecisionLine doc).
        string ozetForLine = Mapper.SafetySummary(directParsed.First(s => s.Id == "kesiktest"));
        string decisionLine = SafetyQueue.DecisionLine("kesiktest", true, DateTimeOffset.UtcNow, ozetForLine);
        Check("#7: karar satırına \"ozet\" alanı yazılıyor", decisionLine.Contains("\"ozet\":\"" + ozetForLine + "\"", StringComparison.Ordinal));

        // OwnDecisionLog grandfathers whatever is already in kararlar.jsonl the
        // first time it runs (no false alarm on history already on disk before this feature shipped), then flags
        // any FUTURE "onay" this app itself never recorded via decideSafety. Both Seed and Record honour
        // VaultWriter.ReadOnlyMode (like AppJournal/ShortcutRuns — never write anything in test modes), so this one
        // spot briefly turns it off, the same way SelfTest.cs already does around SafetyQueue.Decide itself: only
        // the TEMP fixture paths (ORDINARYUNUS_DATA / DecisionsDirOverride) are touched, never anything real.
        bool prevRo = VaultWriter.ReadOnlyMode;
        VaultWriter.ReadOnlyMode = false;
        try
        {
            try { if (File.Exists(OwnDecisionLog.FilePath)) File.Delete(OwnDecisionLog.FilePath); } catch { }
            File.WriteAllText(SafetyQueue.DecisionsFile(vault), "{\"id\":\"eski-onay\",\"karar\":\"onay\",\"zaman\":\"2026-09-01T00:00:00Z\"}\n");
            Check("#9: ilk çalıştırmada var olan onaylar sessizce devralınır (geçmiş yanlış alarm vermez)",
                OwnDecisionLog.FindForeignApprovals(vault).Count == 0);
            File.AppendAllText(SafetyQueue.DecisionsFile(vault), "{\"id\":\"yabanci-onay\",\"karar\":\"onay\",\"zaman\":\"2026-09-24T12:00:00Z\"}\n");
            Check("#9: uygulamanın kendi vermediği yeni bir onay yakalanır", OwnDecisionLog.FindForeignApprovals(vault).Contains("yabanci-onay"));
            OwnDecisionLog.Record("yabanci-onay");
            Check("#9: OwnDecisionLog.Record'dan sonra artık yabancı sayılmıyor", !OwnDecisionLog.FindForeignApprovals(vault).Contains("yabanci-onay"));
        }
        finally { VaultWriter.ReadOnlyMode = prevRo; }

        // runClaude bir "proje" seçildiğinde --add-dir olarak sadece
        // Masaüstü'nün GERÇEK alt klasörünü kabul eder — aynı-önekli bir kardeş klasörü (…\DesktopEvil\x) değil.
        Check("A6: PathGuard bir kardeş klasörü (aynı önek) reddeder",
            !PathGuard.IsStrictSubfolder(@"C:\Users\ornek\DesktopEvil\x", @"C:\Users\ornek\Desktop"));
        Check("A6: PathGuard kökün kendisini reddeder", !PathGuard.IsStrictSubfolder(@"C:\Users\ornek\Desktop", @"C:\Users\ornek\Desktop"));
        Check("A6: PathGuard gerçek bir alt klasörü kabul eder",
            PathGuard.IsStrictSubfolder(@"C:\Users\ornek\Desktop\Projeler\X", @"C:\Users\ornek\Desktop"));
        Check("A6: PathGuard hariç tutulan klasörü (kasa) reddeder",
            !PathGuard.IsStrictSubfolderExcluding(@"C:\Users\ornek\Desktop\Kasa", @"C:\Users\ornek\Desktop", @"C:\Users\ornek\Desktop\Kasa") &&
            PathGuard.IsStrictSubfolderExcluding(@"C:\Users\ornek\Desktop\Proje", @"C:\Users\ornek\Desktop", @"C:\Users\ornek\Desktop\Kasa"));

        // setSettings ile kasa yolu değişimi, AGENTS.md içermeyen bir klasörü reddeder.
        string notAVault = Path.Combine(tmp, "kasa-degil");
        Directory.CreateDirectory(notAVault);
        Check("A7: setSettings AGENTS.md olmayan klasörü reddeder",
            ErrCode(Send("{\"id\":19,\"type\":\"setSettings\",\"payload\":{\"vaultPath\":\"" + notAVault.Replace("\\", "\\\\") + "\"}}")) == "bad_request");
        string looksLikeVault = Path.Combine(tmp, "kasa-gibi");
        Directory.CreateDirectory(looksLikeVault);
        File.WriteAllText(Path.Combine(looksLikeVault, "AGENTS.md"), "# kural yok, sadece varlık testi\n");
        Check("A7: setSettings AGENTS.md olan klasörü kabul eder (headless: otomatik onay)",
            JsonOk(Send("{\"id\":20,\"type\":\"setSettings\",\"payload\":{\"vaultPath\":\"" + looksLikeVault.Replace("\\", "\\\\") + "\"}}")) is not null);
        // vaultı geri al ki bu fonksiyonun kalanı hâlâ fixtureVault üzerinde çalışsın
        app.Settings.KasaYolu = vault;
        app.ReloadAsync(true).GetAwaiter().GetResult();

        // D2c (SÖZLEŞME EKİ): setSettings uiScale artık 6 değeri kabul ediyor (100/110/125/140/150/175), aradaki
        // (105 gibi) bir değeri reddediyor.
        foreach (int v in new[] { 100, 110, 125, 140, 150, 175 })
            Check($"D2c: setSettings uiScale={v} kabul edilir", JsonOk(Send("{\"id\":21,\"type\":\"setSettings\",\"payload\":{\"uiScale\":" + v + "}}")) is not null);
        Check("D2c: setSettings uiScale=105 (adımlar arası) reddedilir",
            ErrCode(Send("""{"id":22,"type":"setSettings","payload":{"uiScale":105}}""")) == "bad_request");

        // E (zamanlanmış görev Türkçe başlıkları)
        Check("E: uygulamanın kendi görev adları Türkçeleşiyor", ToolStatusReader.TitleFor("aksam-analizi") == "Akşam analizi" &&
            ToolStatusReader.TitleFor("tam-gaz") == "Tam Gaz işçisi");
        Check("E: bilinmeyen görev adı — tireler boşluk, ilk harf büyük", ToolStatusReader.TitleFor("yeni-gorev-adi") == "Yeni gorev adi");

        // Güvenlik kapısı ışığı: kancanın göreceği kasa yolu (settings.json "env" bölümü önce gelir) ve yol karşılaştırması
        Check("kapı ışığı: settings.json env bölümündeki IKINCI_BEYIN_KASA okunur",
            SystemMonitor.GateVaultSetting("""{"env":{"IKINCI_BEYIN_KASA":"C:\\Kasalar\\ornek"}}""") == @"C:\Kasalar\ornek");
        Check("kapı ışığı: yol karşılaştırması büyük/küçük harfi ve sondaki bölüyü önemsemez",
            SystemMonitor.SamePath(@"C:\Kasalar\Ornek\", @"c:\kasalar\ornek") && !SystemMonitor.SamePath(@"C:\Kasalar\ornek", @"C:\Kasalar\ornek2"));

        // Kestirmeler: /haftalik ve /devir yalnızca beceri kuruluysa hazır (kasanın kendi .claude\skills klasörü de sayılır)
        string beceriKasa = Path.Combine(tmp, "beceri-kasa");
        Directory.CreateDirectory(Path.Combine(beceriKasa, ".claude", "skills", "ornek-beceri"));
        File.WriteAllText(Path.Combine(beceriKasa, ".claude", "skills", "ornek-beceri", "SKILL.md"), "---\nname: ornek-beceri\n---\n");
        Check("kestirme: kasadaki .claude\\skills\\<ad>\\SKILL.md beceriyi hazır sayar", Mapper.SkillInstalled(beceriKasa, "ornek-beceri"));
        Check("kestirme: olmayan beceri hazır sayılmaz", !Mapper.SkillInstalled(beceriKasa, "olmayan-beceri-7f3a"));

        var authSettings = new Security.AppSettings { KasaYolu = vault };
        Security.SettingsStore.SetPassword(authSettings, "correcthorse");
        var app2 = new Ordinaryunus.Host.AppState(authSettings, dryRun: false, screenshotMode: false);
        var bridge2 = new BridgeHost(app2, _ => { });
        string Send2(string raw) => bridge2.HandleAsync(raw).GetAwaiter().GetResult() ?? "";
        for (int i = 0; i < 5; i++) Send2("{\"id\":" + (20 + i) + ",\"type\":\"login\",\"payload\":{\"password\":\"wrong\"}}");
        Check("köprü: 5 yanlış şifre sonrası auth_wait", ErrCode(Send2("""{"id":30,"type":"login","payload":{"password":"wrong"}}""")) == "auth_wait");
    }

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static JsonElement? JsonOk(string s)
    {
        if (s.Length == 0) return null;
        try
        {
            using var doc = JsonDocument.Parse(s);
            if (!doc.RootElement.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True) return null;
            return doc.RootElement.TryGetProperty("payload", out var p) ? p.Clone() : null;
        }
        catch { return null; }
    }

    static string? ErrCode(string s)
    {
        if (s.Length == 0) return null;
        try
        {
            using var doc = JsonDocument.Parse(s);
            return doc.RootElement.TryGetProperty("error", out var err) && err.TryGetProperty("code", out var c) ? c.GetString() : null;
        }
        catch { return null; }
    }
}
