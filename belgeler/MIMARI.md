# Ordinaryunus mimarisi

Bu belge Ordinaryunus'un kodunu okumak, değiştirmek ya da katkı vermek isteyen geliştiriciler içindir. Uygulamanın
katmanlarını, arayüz ile C# arasındaki mesaj köprüsünü, dosya yollarını, yapay zekâ işlerinin nasıl yürüdüğünü ve
güvenlik modelini anlatır. Kullanım için [README](../README.md), kasa kurulumu için
[kasa-araclari/KURULUM.md](../kasa-araclari/KURULUM.md) dosyasına bak.

**Bölüm numaraları hakkında:** Kod yorumlarında `MIMARI §3.5`, `§9.3` gibi göndermeler var. Bu belgenin bölüm numaraları
bilerek o göndermelerle aynı tutuldu. Koddaki `EK-v2.1 §…` ve `EK §…` göndermeleri bu belgenin sonundaki
[Ek: v2.1](#ek-v21-iş-nöbetçisi-uyanık-tutma-ekran-boyutu) bölümünü gösterir.

**Doğruluk kaynağı:** Bir alanın tam adı ya da tipi konusunda bu belge ile kod ayrışırsa kod geçerlidir:
veri şekilleri `Bridge/Dto.cs`, istek doğrulayıcıları `Bridge/Payloads.cs`, arayüzdeki sahte veri `wwwroot/js/mock-data.js`.

## Bir bakışta

```text
┌──────────────────── Ordinaryunus.exe (tek pencere, WinForms) ────────────────────┐
│  WebHostForm ── WebView2 ── https://ordinaryunus.example/index.html (wwwroot)    │
│        │  JS → C#: postMessage(metin JSON)      ▲  C# → JS: PostWebMessageAsJson   │
│        ▼                                        │                                 │
│  BridgeHost → Payloads (doğrula) → kilit kapısı → Handlers                        │
│        │                                                                          │
│  AppState: ayarlar · kilit · VaultSnapshot · BrainIndex · sorunlar · ışıklar       │
│        │                     │                          │                         │
│   Data/* (kasayı okur,   Brain/* (dizin, bağlantı,   Jobs/* (nöbetçiyi başlatır,   │
│   4 izinli yazma)        sorun, Markdown, harita)    iş dosyalarını izler)         │
└────────┼──────────────────────────────────────────────────┼──────────────────────┘
         ▼                                                  ▼
   Kasa (Markdown klasörü)                node is-nobetcisi.mjs (ayrı süreç) → claude.exe / codex
         ▲                                                  │
         └──── Claude Code kancaları: güvenlik kapısı, istek defteri ◄──────────────┘
```

- **Bütün mantık C#'ta.** Dosya okuma, yazma, süreç başlatma, doğrulama, Markdown'ı HTML'e çevirme C# tarafındadır.
- **Arayüz sadece çizer.** `wwwroot` içindeki düz ES modülleri veriyi köprüden ister ve ekrana basar; dosyaya, ağa,
  tarayıcı depolamasına erişmez.
- **Yapay zekâ işleri uygulamanın dışında yaşar.** Uygulama işi kasadaki iş nöbetçisine verir ve sonra yalnızca
  nöbetçinin yazdığı dosyaları okur. Bu yüzden uygulama kapansa da iş sürer.

---

## 0. Temel ilkeler

### 0.1 Kurallar

1. **Mantık C#'ta, arayüz sadece çizer.** JS tarafında `fetch` yok; `localStorage`, `sessionStorage`, IndexedDB,
   çerez yok; `eval`, `new Function`, metinle zamanlayıcı yok; satır içi olay işleyici (`onclick="…"`) yok.
2. **Kasaya yazma dört şeyle sınırlı** (bkz. §10): Yapılacaklarım kutucuğu, iş başvurusu onayı, `DURDUR` bayrağı,
   Tam Gaz anahtarı. Beyin dizinleyicisi salt okunurdur.
3. **`80 Oturum Arşivi` asla okunmaz.** Bu klasör yapay zekâ oturum kayıtları gibi özel içerik için ayrılmıştır;
   dizinleyici klasöre girmez, listelemez bile.
4. **Test modları iz bırakmaz.** `--selftest`, `--screenshots`, `--dump-json`, `--dry-run` gerçek kasaya yazmaz, gerçek
   Claude ya da Codex başlatmaz, `DURDUR` ya da `ACIK` oluşturmaz.
5. **İzin atlama bayrakları yok.** Claude ya da Codex hiçbir zaman `--dangerously-skip-permissions`,
   `bypassPermissions`, `--full-auto`, `--yolo`, `danger-full-access` gibi bayraklarla çalıştırılmaz; kancaların
   (güvenlik kapısı, istek defteri) her işte çalışması şarttır.
6. **Dış bağımlılık en az.** Tek NuGet paketi `Microsoft.Web.WebView2`; arayüzde tek kütüphane ECharts (dosya olarak
   `wwwroot/lib` içinde, çalışırken CDN yok). npm paketi yok.

### 0.2 Tasarım kararları

| # | Karar | Neden |
|---|---|---|
| D1 | Sanal adres `https://ordinaryunus.example/` | `.example` alan adı ayrılmıştır, gerçek bir siteyle çakışmaz; `.local` adlar WebView2'de DNS gecikmesi yaşatır. |
| D2 | Çerçevesiz, paketleyicisiz düz ES modülleri | İndirme yok, derleme adımı yok, WebView2 güncel Chromium. |
| D3 | JS → C# **metin** gönderir; C# → JS `PostWebMessageAsJson`; sıkı izin listesiyle doğrulama | En küçük saldırı yüzeyi; ana makine nesnesi (host object) yok. |
| D4 | Eski WinForms arayüzü (`UI/`, `Charts/`) derlemeden çıkarıldı | v2 arayüzü onun yerini aldı. Bu klasörler depoda yok; csproj'daki `Compile Remove` satırı zararsız bir kalıntıdır. |
| D5 | Claude başsız modda `--permission-prompts none` ile çalışır | Başsız bir iş, kimsenin cevaplayamayacağı bir izin sorusunda asılı kalmamalı. |
| D6 | Claude'un ortamından `ANTHROPIC_API_KEY`, `ANTHROPIC_AUTH_TOKEN`, `CLAUDECODE` çıkarılır | Ücretli API'ye yanlışlıkla fatura yazılmasın; uygulama bir Claude terminalinden açıldıysa "iç içe oturum" hatası olmasın. |
| D7 | Ekran boyutu = WebView2 `ZoomFactor` | Tek ayar; grafikler de birlikte ölçeklenir. |
| D8 | Beyin dizini yalnızca bellekte, (değişme zamanı, boyut) ile artımlı | Soğuk dizinleme bütçeye sığıyor; diskte bayat önbellek hatası olmaz. |
| D9 | `--dry-run` modu (geçici veri klasörü, sahte nöbetçi) | Her düğmeye gerçek veriyle, sıfır risk ve sıfır kotayla basılabilir. |
| D10 | Grafik ve harita kategorileri deniz yeşili ailesi + arduvaz; yeşil/sarı/kırmızı yalnızca durum için | Tutarlı görsel kimlik; renk bir anlam taşıyorsa sadece durumu anlatır. |

---

## 1. Klasör ve dosya düzeni

### 1.1 Depo düzeni

```text
Ordinaryunus/                     (depo kökü)
  BASLAT.bat                      yayin\Ordinaryunus.exe'yi açar
  belgeler/MIMARI.md              bu belge
  belgeler/ekran-goruntuleri/     --screenshots çıktıları (README'de; ana görsel, galeri ve "Diğer ekranlar")
  kasa-araclari/                  kasaya ve Claude Code'a kurulacak araçlar + KURULUM.md
    claude-kancalari/guvenlik-kapisi.mjs   PreToolUse kancası (§10.2)
    claude-kancalari/istek-defteri.mjs     UserPromptSubmit kancası (ortak istek defteri)
    claude-becerileri/devir, haftalik      /devir ve /haftalik için örnek Claude Code becerileri (SKILL.md, isteğe bağlı)
  ornek-kasa/                     uydurma verili örnek kasa (iş nöbetçisi dahil)
  Ordinaryunus/                   (PRJ) uygulama
    Ordinaryunus.csproj
    Program.cs                    modlar, WebView2 kontrolü, WebHostForm'u açar (§2.1)
    Imza.cs                       yapan, yıl, sürüm, lisans, GitHub adresi (tek kaynak; wwwroot/js/imza.js ile eş)
    SelfTest.cs, SelfTestV2.cs    --selftest (§9.1)
    Host/WebHostForm.cs           tek Form: WebView2, zamanlayıcılar, kasa izleyici, boşta kilit, kapanış (§2.2)
    Host/WebViewConfig.cs         köken, başlangıç adresi, CSP sabiti, gezinme izni (§2.3)
    Host/AppState.cs              ayarlar, kilit, anlık görüntü, Beyin durumu, ışıklar, satır anahtarları (§5.6)
    Host/AppIcon.cs, ScreenDefaults.cs     pencere simgesi; ilk açılış ekran boyutu (EK §2c)
    Bridge/BridgeHost.cs          ayrıştır → doğrula → kilit kapısı → işleyici → cevap; olay gönderme (§3)
    Bridge/Payloads.cs            her istek türü için sıkı doğrulayıcı (§3.2, §3.5)
    Bridge/Dto.cs                 bütün veri şekilleri (System.Text.Json, camelCase)
    Bridge/Mapper.cs              alan nesneleri → DTO, satır anahtarları (td-/ap-)
    Bridge/Handlers.cs            her istek türü için bir yöntem
    Brain/*.cs                    dizinleyici, wikilink, Kayıt ayrıştırıcı, git etkinliği, sorunlar, genel bakış,
                                  ağaç, harita, arama, güvenli Markdown çevirici (§4, §5)
    Jobs/*.cs                     Claude bulucu, nöbetçi başlatıcı, iş deposu, akış ayrıştırıcıları (§6)
    Data/*.cs                     kasa okuyucuları, izinli yazmalar, güvenlik kuyruğu, Tam Gaz, ışıklar, süreçler
    Security/*.cs                 PBKDF2 şifre özeti, ayarlar dosyası
    Shots/*.cs                    --screenshots (§9.3), --dump-json (§9.2)
    Assets/                       logo.ico ve logolar (ico-yap.mjs .ico üretir)
    wwwroot/                      arayüz (§7)
      index.html                  CSP meta, simge sprite'ı, kök kaplar, betik etiketleri
      css/tokens.css, app.css     tema belirteçleri; yerleşim, bileşenler, hareket
      js/main.js                  açılış, kabuk, kısayollar
      js/bridge.js                istek/cevap ve olaylar; gerçek köprü ya da sahte köprü
      js/mock.js, mock-data.js    sahte köprü ve sahte veri (§8)
      js/store.js, ui.js, charts.js, router.js, scale.js, imza.js
      js/pages/…                  masam, beyin/{beyin,genel,kesif,harita,sorunlar,proje}, sirketim, gecmis,
                                  basvurular, projeler, kestirmeler, ayarlar
      js/overlays/…               login, focus, givework, palette, jobdrawer, dialogs
      lib/echarts.min.js          ECharts 5.6.0; lib/VERSIONS.txt sürüm ve SHA-256; lib/LICENSE-Apache-2.0.txt lisans metni
      img/                        logo.svg, logo-64.png, logo-256.png, wordmark.svg
```

### 1.2 Proje dosyası (csproj)

- `net10.0-windows`, `WinExe`, WinForms, `Nullable` ve `ImplicitUsings` açık, `PerMonitorV2` DPI.
- `Microsoft.Web.WebView2` **1.0.3179.45**.
- `Assets\*.png;*.ico` gömülü kaynak; `Assets\logo.ico` varsa uygulama simgesi.
- `wwwroot\**\*` içerik olarak çıktıya kopyalanır ve **tek dosyanın dışında** kalır (`ExcludeFromSingleFile`).
- Yayın (depo kökünden):
  `dotnet publish Ordinaryunus\Ordinaryunus.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o yayin`
  → `yayin\Ordinaryunus.exe` + `yayin\wwwroot\` + `yayin\WebView2Loader.dll`. Çalıştırmak için .NET 10 Masaüstü
  Çalışma Zamanı gerekir (çerçeveye bağımlı, küçük tek dosya).

### 1.3 Yollar

**Uygulama verisi** (`Data/AppPaths.cs`):

| Yol | İçerik |
|---|---|
| `%APPDATA%\Ordinaryunus\` (`DataDir`) | `ayarlar.json`, `gunluk.json` (uygulamadan işaretlenenler), `kisayollar.json` (kestirme çalıştırma kaydı), `hata.log`, `hata-web.log`, `tmp\` |
| `DataDir\claude-isler\`, `DataDir\codex-isler\` | iş dosyaları (EK §1) |
| `%LOCALAPPDATA%\Ordinaryunus\` (`LocalDir`) | `WebView2\` (tarayıcı verisi), `guvenlik\kararlar.jsonl` (onay kararları), `guvenlik\verdigim-kararlar.jsonl` (uygulamanın kendi onay kaydı), `guvenlik\betik-izleri.json` (kasa betiklerinin özetleri, §10.3), `yedekler\` (güvenlik kapısının yedekleri) |

`ORDINARYUNUS_DATA` ortam değişkeni verilirse `DataDir` o klasör olur ve `LocalDir` onun altındaki `local\` olur;
testler gerçek klasörlere hiç dokunmaz.

**Kasa yolu** şu sırayla belirlenir: `ayarlar.json` içinde kayıtlı `KasaYolu` → `ORDINARYUNUS_KASA` ortam değişkeni →
`Ordinaryunus.exe` klasöründen başlayıp en fazla 5 üst klasöre kadar bir `ornek-kasa` (`yayin\` için 1,
`bin\Release\net10.0-windows\` için 4 üst klasör) → `%USERPROFILE%\Documents\IkinciBeyin` (`AppPaths.ResolveDefaultVault`,
saf fonksiyon, selftest'te denenir). Klasörün var olması şart değildir; yoksa uygulama "kasa bulunamadı" uyarısı gösterir.
İş başvuru belgeleri klasörü: `ORDINARYUNUS_BASVURU` ortam değişkeni, yoksa `%USERPROFILE%\Documents\IsBasvuru`
(belgeler `CIKTI\` alt klasöründe).

Claude Code kancaları kasayı kendileri bulur: `IKINCI_BEYIN_KASA` ortam değişkeni → kancanın çalışma klasöründen yukarı
doğru `01 Şimdi.md` içeren ilk klasör → `%USERPROFILE%\Documents\IkinciBeyin`. Acil durdurma ve koruma için bulunan
bütün kasa adaylarına bakılır; daha yakına konmuş sahte bir `01 Şimdi.md` gerçek kasadaki `DURDUR`'u gizleyemez.
Ayarlar'dan kasa değiştirmek için klasörün var olması, kökünde `AGENTS.md` bulunması ve Windows'un yerel onay
kutusunda "Evet" denmesi gerekir (bozuk ya da ele geçirilmiş bir sayfa uygulamayı sessizce başka veriye yöneltemesin).

**Kasa içinde okunanlar** (`Data/VaultReader.cs` ve ilgili sınıflar):

| Kaynak | Ne için |
|---|---|
| `01 Şimdi.md` › `## ✅ Yapılacaklarım` | Yapılacaklarım listesi ve günün tek işi |
| `20 Projeler/<P>/<P>.md` | proje kartı: ön bilgi (`durum`, `odak`, `bitti_tanimi`, `sonraki_adim`, `olum_kriteri`, `karar_bekliyor`, `kilit`, `klasor`, `baslangic`, `son_guncelleme`), `## Bitti Tanımı` kutucukları, `## Engel`, `## Benden Beklenenler` |
| `20 Projeler/<P>/Kayıt.md`, kartın `## Son Devir` bölümü | devir ve KARAR kayıtları (§5.5) |
| `20 Projeler/<P>/Görevler/*.md` | görev paketleri (durum, atanan, son tarih) |
| `20 Projeler/Bitenler.md` | biten işler listesi |
| `60 Ekip/Roller/*.md` | yapay zekâ rolleri (Şirketim, "İş ver") |
| `40 Alanlar/İş Başvuruları.md` | başvuru tabloları |
| `40 Alanlar/Gelir Defteri.md` | isteğe bağlı gelir göstergesi (`hedef_usd:` ile aylık hedef) |
| `40 Alanlar/LinkedIn Takvimi.md` (yoksa herhangi bir `20 Projeler/<P>/` altındaki aynı adlı dosya) | isteğe bağlı yayın sayısı ve taslaklar |
| `70 Günlük/Analiz/*.md` | isteğe bağlı "derin analiz" notları |
| `_sistem/istek-defteri/istekler.jsonl` | Claude ve Codex'e verilen istekler (istek defteri kancası yazar) |
| `_sistem/guvenlik/bekleyenler.jsonl`, `DURDUR` | güvenlik kuyruğu ve acil durdurma (§10.2) |
| `_sistem/tamgaz/ACIK`, `codex-nobet.json`, `codex-<gün>.log`, `gunluk.md` | Tam Gaz anahtarı ve zamanlanmış işçinin durumu |
| `_sistem/araclar/is-nobetcisi.mjs` | iş nöbetçisi (çalıştırılır, okunmaz) |
| git deposu (`git rev-parse`; kasa bir üst deponun alt klasörü de olabilir) | kayıt geçmişi, grafikler, "Kasa" ışığı (`git log -- .`, `git status`); yalnızca kasa klasörünü etkileyen kayıtlar sayılır |
| bütün `.md` dosyaları (`80 Oturum Arşivi` hariç: oturum dökümleri bilerek hiç dizinlenmez ve açılamaz) | Beyin dizini (§5) |

**Kasa dışında salt okunanlar:** `%USERPROFILE%\.claude\` (ayarlar, kancalar, ajanlar, zamanlanmış görevler, son
oturumlar) ve `%USERPROFILE%\.codex\` (yapılandırma, kancalar, oturumlar; kota tahmini). Bunlar yalnızca Ayarlar ›
Bağlantılar, sistem ışıkları ve Şirketim için okunur; hiçbir zaman yazılmaz. `ORDINARYUNUS_PROFIL` ortam değişkeni
verilirse `%USERPROFILE%` yerine o klasöre bakılır (`AppPaths.UserProfile`; testler ve ekran görüntüleri için, §9.3).
"Güvenlik kapısı" ışığı kanca dosyasına ve `settings.json` kaydına bakar (yoksa kırmızı); ayrıca kancaların
göreceği `IKINCI_BEYIN_KASA` değerini (önce Claude Code `settings.json` içindeki `env` bölümü, sonra süreç, kullanıcı
ve makine ortam değişkeni) uygulamanın kasasıyla karşılaştırır; yoksa ya da başka bir klasörü gösteriyorsa sarı yanar
(`SystemMonitor.GateVaultSetting`). Uygulamadan verilen işlerde bu değişkeni iş nöbetçisi kendisi koyar.

---

## 2. Ana makine (Host, WebView2)

### 2.1 Program.Main

1. Kültür `tr-TR`.
2. Komut satırı modları: `--selftest [kasa]` (§9.1), `--dump-json <klasör> [--vault <kasa>]` (§9.2),
   `--screenshots <klasör> [--size GxY] [--scale 1.25] [--theme dark] [--vault <kasa>]` (§9.3), `--dry-run`, ve
   `--surum` ya da `--version` (imza satırını, `Imza.Satir`, konsola yazar). Selftest WebView2 oluşturmaz.
3. Tek pencere kilidi (`Local\Ordinaryunus-tek-pencere` mutex) **yalnızca normal modda**; ikinci kopya "zaten açık"
   der ve çıkar.
4. `--dry-run`, `ORDINARYUNUS_DATA` bir `%TEMP%` klasörünü göstermiyorsa açılmaz (çıkış 5). Deneme modunda:
   `VaultWriter.ReadOnlyMode`, `Launch.Disabled`, güvenlik kararları geçici klasöre, `JobStore.DryRun` (gerçek araç
   yerine sahte nöbetçi betiği), Acil Durdur yalnızca benzetim yapar, Tam Gaz hiçbir şey yazmaz.
5. WebView2 Runtime kontrolü: yoksa "Bu uygulama için Microsoft Edge WebView2 bileşeni gerekiyor…" (çıkış 4).
6. İlk açılışta ekran boyutu hiç seçilmemişse ekrana göre varsayılan (EK §2c), sonra `WebHostForm`. Yakalanmamış
   hatalar `hata.log`'a yazılır ve kullanıcıya kısa bir mesaj gösterilir; uygulama çalışmaya devam eder.

### 2.2 WebHostForm

- Başlık "Ordinaryunus", simge `logo.ico`, en küçük boyut 1280×800, normal modda tam ekran. Tek `WebView2` denetimi,
  arka planı temanın zemin rengi (beyaz parlama olmasın).
- Açılır açılmaz arka planda `AppState.ReloadAsync(full:true)` başlar: veri giriş ekranı gösterilirken hazırlanır,
  ama kilit açılmadan JS'e hiçbir veri gönderilmez.
- Zamanlayıcılar (UI iş parçacığı): yeniden yükleme gecikmesi 1,5 sn, ışıklar 30 sn, kullanım tahmini 5 dk, boşta kalma
  kontrolü 15 sn, iş bekçisi ve uyanık tutma 30 sn.
- `FileSystemWatcher` kasayı alt klasörleriyle izler; `\.git\`, `\.obsidian\`, `\.trash\`, `\node_modules\`,
  `\80 Oturum Arşivi\` yollarını yok sayar; diğer her değişiklik yeniden yüklemeyi tetikler. Yalnızca normal modda.
- Kapanırken çalışan iş varsa kullanıcıya bilgi verir: işler nöbetçide sürdüğü için kapanmak işi kesmez (EK §2).

### 2.3 WebView2 sertleştirmesi

- Ortam: dil `tr-TR`, işletim sistemi hesabıyla tek oturum açma kapalı, veri klasörü `LocalDir\WebView2`.
- Ayarlar: betik ve web mesajı açık; ana makine nesneleri, varsayılan betik pencereleri, durum çubuğu, yakınlaştırma
  denetimi, iki parmakla yakınlaştırma, kaydırma gezinmesi, otomatik doldurma ve şifre kaydetme **kapalı**.
  Geliştirici araçları, sağ tık menüsü ve tarayıcı kısayolları yalnızca Debug derlemesinde açık.
- `SetVirtualHostNameToFolderMapping("ordinaryunus.example", wwwroot, Deny)`: arayüz dosyaları bu sanal adresten sunulur.
- Gezinme: yalnızca `https://ordinaryunus.example/index.html` (sorgu boş ya da tam olarak `shot=1`). `about:blank`,
  `file:`, `data:`, başka yol ya da alan adı iptal edilir. Tek sayfa uygulaması yalnızca `#` ile yönlenir.
- Çerçeve (iframe) gezinmesi, yeni pencere, indirme, izin istekleri (kamera, konum…) ve harici protokol açma
  **her zaman reddedilir**. Kendi kökenine ait olmayan her kaynak isteği 403 ile cevaplanır.
- Web mesajı yalnızca kaynağı gerçekten `https://ordinaryunus.example` olan (şema + alan adı eşitliği, önek değil)
  sayfalardan kabul edilir.
- İşleyici süreci çökerse sayfa yeniden yüklenir; tarayıcı süreci çökerse günlüğe yazılır ve pencere kapanır.
- **İçerik güvenlik politikası** (`WebViewConfig.Csp`; `index.html` içindeki meta etiketi bununla birebir aynı olmalı,
  selftest boşlukları normalleştirip karşılaştırır):

```text
default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self';
connect-src 'none'; frame-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'
```

  `'unsafe-inline'` yalnızca stiller için (ECharts ipuçları satır içi stil kullanır). Satır içi betik ve `eval` yok.

### 2.4 Açılış, giriş, kilit, boşta kalma

```text
C#: pencere → WebView2 hazır → Navigate(StartUrl)                     (AppState.Unlocked = false)
JS: açılış ekranı → hello → getAuthState → "Şifre belirle" (ilk açılış) ya da "Giriş"
JS: login{password} → C#: bekleme kontrolü → PBKDF2 doğrulama (iş parçacığı havuzunda) → başarı/başarısızlık kaydı
    başarılı → Unlocked = true; cevap; ışık ve kullanım olayları
JS: getSnapshot (+ Beyin açılınca getBrainOverview) → Masam
Kilit (Ctrl+L | boşta kalma):
    C#: Unlocked = false → "locked" olayı → sayfa yeniden yüklenir (JS belleği ve DOM temizlenir)
```

- Kilitliyken köprü yalnızca `hello`, `getAuthState`, `login`, `setPassword`, `clientError` ve (ekran görüntüsü
  modunda giriş sayfası için) `pageReady` isteklerini cevaplar; diğerleri `locked` hatası alır. Kasa verisi taşıyan
  olaylar (`snapshotChanged`, `brainChanged`, `jobUpdated`) yalnızca kilit açıkken gönderilir.
- Şifre: JS gönderdikten hemen sonra kutuyu temizler, kopya tutmaz. C#, `login`, `setPassword`, `changePassword`
  yüklerini hiçbir zaman günlüğe yazmaz. 5 yanlış denemede 30 sn bekleme, her yeni beklemede süre ikiye katlanır.
- Boşta kalma: WebView2 içindeki fare ve klavye olayları WinForms'a ulaşmadığı için **etkinliği JS bildirir**
  (`activity{focusRunning}`, en fazla 30 sn'de bir). C#, son etkinlikten bu yana `KilitDakika` geçtiyse ve odak sayacı
  çalışmıyorsa kilitler. Odak sayacı 90 sn'den uzun süre kalp atışı göndermezse "bitti" sayılır; tek bir kayıp mesaj
  otomatik kilidi kalıcı olarak kapatamaz.
- Ekran görüntüsü modu kilitli başlar (`giris.png`), sonra şifresiz ve salt okunur olarak içeriden açılır.

### 2.5 Simge ve logo

Görev çubuğu ve pencere simgesi `logo.ico` (gömülü kaynak). Arayüzde üst çubukta ve giriş ekranında `img/logo.svg`;
PNG'ler yedek.

---

## 3. Köprü protokolü

### 3.1 Zarf

- JS → C#: `window.chrome.webview.postMessage(JSON.stringify({ id, type, payload }))`. `id` sayfa yüklemesi başına
  artan bir tam sayı (1 … 2.147.483.647); `type` §3.5'teki listeden; `payload` bir nesne (boşsa `{}`).
- C# → JS cevap: `{"id":17,"type":"getNote","ok":true,"payload":{…}}` ya da
  `{"id":17,"type":"getNote","ok":false,"error":{"code":"not_found","message":"Not bulunamadı."}}`.
- C# → JS olay: `{"id":0,"type":"jobUpdated","payload":{…}}` (`id` 0 = olay).
- Ayrıştırılamayan mesaj ya da geçersiz `id` → `protocolError` olayı (saniyede en fazla 1); başka hiçbir şey olmaz.
- JSON: camelCase, `UnsafeRelaxedJsonEscaping`. **Tanımlı her alan her zaman vardır**; bilinmeyen değer `null`,
  diziler asla `null` değil. Metinler düz metindir; yalnızca adı `Html` ile biten alanlar HTML taşır ve onları C#
  çeviricisi üretir (§4).

### 3.2 Doğrulama (C#, iş başlamadan önce)

Ham metin en fazla 65.536 karakter; `JsonDocument` derinliği en fazla 16; kök nesnede tam olarak `id`, `type`,
`payload` anahtarları; `type` izin listesinde; `payload` bir nesne; türe özel doğrulayıcı zorunlu alanları, JSON
türlerini, uzunlukları, seçenek kümelerini ve düzenli ifadeleri denetler ve **bilinmeyen alanları reddeder**. Sonra
kilit kapısı (§2.4), sonra işleyici. Ağır işler iş parçacığı havuzunda yapılır, cevap UI iş parçacığından gönderilir.

### 3.3 Hata kodları

Mesajlar Türkçedir ve olduğu gibi gösterilebilir: `bad_request`, `unknown_type`, `locked`, `not_found`, `stale`
("Dosya az önce değişti, liste yenilendi"), `busy`, `stopped` (acil durdurma açık), `readonly` (deneme modu),
`forbidden`, `failed`, `auth_wrong`, `auth_wait`, `auth_exists`, `no_cli` (Claude komut satırı ya da Node.js yok),
`limit` (aynı anda 2 Claude işi çalışıyor).

### 3.4 Ortak tipler

```ts
type ISO = string;   // yerel saat ve fark, "2026-09-24T10:15:02+03:00"
type Day = string;   // "2026-09-24"
type Tone = "teal" | "green" | "yellow" | "red" | "grey";
type Severity = "kritik" | "dikkat" | "bilgi";
type PageId = "masam" | "beyin" | "sirketim" | "gecmis" | "basvurular" | "projeler" | "kestirmeler" | "ayarlar";
type Target =                                                     // bir tıklamanın nereye gideceği
  | { type: "note"; path: string; anchor: string | null }         // path kasaya göreli, "/" ayraçlı
  | { type: "project"; name: string; section: "genel" | "kayit" | "kararlar" | "gorevler" | "acik" | "dosyalar" }
  | { type: "page"; page: PageId; tab: string | null }
  | { type: "job"; id: string }
  | { type: "ledger"; day: Day | null }
  | { type: "url"; url: string };                                 // yalnızca http/https
```

### 3.5 İstekler

"Kilit" sütunu: isteğin kilit açıkken gerekip gerekmediği. Yüklerin tam şekli `Bridge/Payloads.cs` içindedir.

| Tür | Kilit | Ne yapar |
|---|---|---|
| `hello` | hayır | sürüm, tema, hareket, ekran boyutu, ekran görüntüsü/deneme modu, bugünün tarihi |
| `getAuthState` | hayır | şifre var mı, kilit açık mı, bekleme süresi, kilit nedeni |
| `login`, `setPassword` | hayır | giriş; ilk açılışta şifre belirleme (6–128 karakter, iki kez) |
| `clientError` | hayır | arayüz hatasını `hata-web.log`'a yazar (dakikada en fazla 20) |
| `activity`, `lock` | evet | etkinlik bildirimi; elle kilitleme |
| `getSnapshot`, `refresh`, `getLights` | evet | Beyin dışındaki her şey (§3.6); tam yenileme; sistem ışıkları |
| `getBrainOverview`, `getBrainTree`, `getProblems` | evet | Beyin genel bakış, ağaç, sorunlar (§3.7, §3.8) |
| `getNote`, `getProjectDetail` | evet | tek not (güvenli HTML ile); tek proje ayrıntısı |
| `getLedger`, `searchVault`, `getGraph` | evet | istek defteri günleri; kasa araması; bağlantı grafiği |
| `toggleTodo` | evet | Yapılacaklarım'da bir kutucuk (anahtar `td-…`) |
| `approveJobs` | evet | seçilen başvuru satırlarını "Onaylandı" yapar (anahtar `ap-…`, ya hep ya hiç) |
| `decideSafety` | evet | güvenlik isteğini onayla ya da reddet (kritikte `confirmCritical`, kısaltılmış komutta onay yasak) |
| `runClaude`, `runCodex` | evet | iş başlatır (metin 1–4000, rol, proje, `web`) |
| `copyWork` | evet | işi panoya kopyalar ve Claude ya da Codex uygulamasını açar (yedek yol) |
| `getJobs`, `getJobDetail`, `cancelJob`, `retryJobNow` | evet | iş listesi, adım adım ayrıntı, iptal, kota beklerken "Şimdi dene" |
| `emergencyStop`, `resume` | evet | Acil Durdur ve Devam Et (ikisi de `confirm:true` ister) |
| `tamGazOn`, `tamGazOff` | evet | Tam Gaz 2, 5, 10 ya da 12 saat; kapat |
| `openInObsidian`, `openUrl`, `openFolder`, `copyText` | evet | notu Obsidian'da aç; http/https bağlantı aç; izinli klasör aç; panoya kopyala |
| `runShortcut` | evet | sabit kimlikli bir kestirme (§3.11) |
| `getSettings`, `setSettings`, `pickVaultFolder`, `changePassword` | evet | ayarlar; kasa klasörü seçme penceresi; şifre değiştirme |
| `pageReady` | evet* | yalnızca ekran görüntüsü modunda kullanılır (*giriş sayfası için kilitliyken de) |

İş kimliği biçimi (`getJobDetail`, `cancelJob`, `retryJobNow`):
`^(claude|codex):[0-9]{8}-[0-9]{6}(-[0-9]{3})?(-[0-9a-f]{8})?$`

`openUrl` yalnızca mutlak, ana makine adı olan, kullanıcı bilgisi ve boşluk içermeyen http/https adreslerini kabul
eder. `openFolder` yalnızca sabit bir listeden klasör açar (kasa, iş klasörleri, proje klasörü, bir notun konumu);
kullanıcı metniyle keyfi yol açılamaz.

### 3.6 Anlık görüntü (Snapshot)

`getSnapshot` cevabı ve `snapshotChanged` olayı: bugünün tarihi ve selamlama, kasa durumu (`exists`, `gitOk`),
acil durdurma, seri, odak projesi, aktif proje sınırı, günün tek işi, Yapılacaklarım, bekleyen güvenlik istekleri,
onay bekleyenler, "senden beklenenler", projeler, görevler, roller, Şirketim verisi (işler, kilitler, zamanlanmış
görevler, son Claude oturumları, Tam Gaz, işçi durumu), 30 günlük geçmiş, başvurular, kestirmeler, araç durumu, ışıklar,
kullanım tahmini, Claude komut satırı bilgisi ve uyarılar. Tam şekil: `Bridge/Dto.cs` (`SnapshotDto` ve bağlı kayıtlar).

**Satır anahtarları** (`Bridge/Mapper.cs`): Yapılacaklar için `td-<satır>-<ham satırın SHA-256'sının ilk 8 onaltılığı>`,
başvuru satırları için `ap-<satır>-<özet8>`. C# anahtarı her zaman **en son** anlık görüntüye göre çözer; bulunamazsa
`stale` döner ve taze bir `snapshotChanged` gönderir. Böylece arayüz, arada değişmiş bir dosyada yanlış satırı
değiştiremez.

### 3.7 Beyin: genel bakış, proje ayrıntısı, sorun

- `BrainOverview`: 2–4 cümlelik kural tabanlı özet, sağlık puanı ve seviyesi, odak ilerlemesi, haftalık sayılar,
  göstergeler (KPI), proje sağlık kartları, "neler yaptık" ve "neler kaldı" listeleri, son olaylar, en önemli sorunlar,
  grafik verileri, kural tabanlı analiz, isteğe bağlı derin analiz ve gelir göstergesi, dizin istatistikleri.
- `ProjectDetail`: proje, sağlık, devirler, kararlar, görevler, açık kalanlar, sorunlar, dosyalar, 30 günlük değişiklik.
- `Problem`: `id` (`"<kural>:<anahtar>"`, yeniden yüklemeler arasında sabit), kural, önem, alan, proje, başlık, ayrıntı,
  tek cümlelik "Ne yapmalı" (`fix`) ve hedef.

### 3.8 Ağaç, not, arama, istek defteri, harita

- `TreeNode`: kimlik, etiket, tür, simge, rozet, sağlık, sayı, hedef, çocuklar. Bütün ağaç tek cevapta gelir.
- `Note`: yol, başlık, tür, proje, kırıntı yolu, **güvenli** `html`, ön bilgi, başlıklar, giden ve gelen bağlantılar,
  sorunlar, görev sayıları, kelime, boyut, değişme zamanı. 1 MB'tan büyük notlarda `truncated`.
- `SearchResult`: `snippetHtml` yalnızca kodlanmış metin ve `<mark>` içerir.
- `GraphData`: düğümler (tür, kategori, derece, proje, eksik mi), bağlantılar (sayılı), kategoriler, istatistik.

### 3.9 İşler (Claude ve Codex tek şekilde)

```ts
interface Job { id: string; tool: "claude" | "codex"; title: string; prompt: string; role: string | null; project: string | null;
  state: "basliyor" | "calisiyor" | "kota-bekliyor" | "bitti" | "hata" | "giris" | "kota" | "durduruldu" | "yarida";
  stateText: string; tone: Tone; start: ISO; end: ISO | null; elapsedSec: number; lastActivity: ISO | null;
  now: string;                 // şu anki eylem, ör. "Düzenliyor: 20 Projeler/Kişisel Blog/Kayıt.md" ya da geri sayım
  steps: number; filesChanged: string[]; denials: string[]; safetyIds: string[];
  quiet: boolean;              // çalışıyor ama 10 dakikadır çıktı yok
  exitCode: number | null; resultPreview: string | null; fix: string | null; canCancel: boolean;
  resumeAt: ISO | null; attempt: number }   // kota bekleniyorsa devam saati; nöbetçinin deneme sayısı
```

Durumların anlamı ve renkleri için §6.6.

### 3.10 Yol kuralı

`path` taşıyan her istekte: `\` → `/`; kök yol, `..` parçası, `:`, baştaki `/`, denetim karakteri ve 400'den uzun yol
reddedilir; yol **o anki Beyin dizininde bir anahtar olmalıdır** (köprü için yalnızca dizinlenmiş dosyalar vardır).
Dizin dışı klasörler hiç dizinlenmediği için hiçbir zaman açılamaz. Obsidian'da açma ve klasörde gösterme ayrıca tam
yolu kurup kasanın içinde kaldığını yeniden denetler.

### 3.11 Kestirme kimlikleri

Başlıklar ve eylemler C#'ta sabittir, `snapshot.shortcuts` ile gönderilir. `ac` grubu: `simdi` (Obsidian'da
`01 Şimdi.md`), `kasa`, `claudeUygulama`, `codexUygulama`, `linkedin` (yalnızca kasada LinkedIn takvimi varsa),
`basvuruKlasoru`, `haftalik` ve `devir` (`/haftalik` ya da `/devir` komutunu kopyalar ve Claude'u açar). Son ikisi
birer Claude Code becerisidir ve depoyla gelmez (örnekleri `kasa-araclari/claude-becerileri/`, KURULUM.md 3.6):
`%USERPROFILE%\.claude\skills\<ad>\SKILL.md`, `.claude\commands\<ad>.md` ya da `<kasa>\.claude\skills\<ad>\SKILL.md`
yoksa kutucuk `hazir: false` ile soluk gönderilir ve tıklamak `not_found` hatası verir (`Mapper.SkillInstalled`).
`bakim` grubu: `istekTara` (istek defteri kancasının `tara` modu) ve `kapiTest` (güvenlik kapısının kendi testi);
ikisi de `%USERPROFILE%\.claude\hooks\ikinci-beyin\` altındaki bir Node betiğini çalıştırır. Bakım betikleri isteğe
bağlıdır; betik yoksa kestirme açık bir mesajla hiçbir şey yapmaz. Betikler kasa klasöründe, 60 sn zaman aşımıyla, çıktısı
50.000 karakterle sınırlı ve aynı anda yalnızca bir tane çalışır. Kasadaki bir betik git'e göre değiştirilmiş ya da
takip edilmiyorsa çalıştırılmaz (§10.3).

### 3.12 Olaylar (C# → JS, `id` 0)

| Tür | Yük | Ne zaman |
|---|---|---|
| `snapshotChanged` | Snapshot | her yeniden yüklemeden sonra (izleyici, F5, bir yazmadan sonra) |
| `brainChanged` | `{stamp}` | dizin ya da sorunlar değişince; JS ekrandakini yeniden ister |
| `jobUpdated` | Job | durum değişince ya da ilerleyince (iş başına saniyede en fazla 2) |
| `lightsChanged` | `{lights}` | 30 sn'lik kontrolde değişiklik varsa |
| `usageChanged` | Usage | 5 dakikada bir |
| `toast` | `{text, tone}` | iş ya da betik bitti, arka plan hatası |
| `locked` | `{reason}` | sayfa yeniden yüklenmeden hemen önce |
| `shot` | sayfa, sekme, açılacak pencere, sorgu | yalnızca ekran görüntüsü modunda |
| `protocolError` | `{code, message}` | bozuk zarf |

---

## 4. Güvenli Markdown → HTML çevirici (`Brain/MarkdownRenderer.cs`)

Tek geçişli, satır tabanlı; girdi 1 MB ile sınırlı (fazlası kesilir, `truncated`). **Her metin parçası
`WebUtility.HtmlEncode`'dan geçer.** Çıktıda yalnızca şu etiketler ve öznitelikler olabilir (selftest bir etiket
tarayıcısıyla denetler):

`h1–h6[id]`, `p`, `br`, `strong`, `em`, `del`, `mark`, `code`, `pre[class="lang-…"]`, `blockquote`,
`div.callout.callout-<tür>`, `div.callout-title`, `ul`, `ol[start]`, `li.task` / `li.task.done`, `span.cb` / `span.cb.done`,
`table`, `thead`, `tbody`, `tr`, `th` ve `td` (hizalama sınıfı), `hr`, `a.wl[data-note]`, `span.wl.broken[title]`,
`a.ext[data-url]`, `span.tag`, `span.embed`.

`href`, `src`, `style` ve olay öznitelikleri **hiçbir zaman** üretilmez. Desteklenenler: YAML ön bilgisi (ayrı döner),
`%%…%%` ve `<!-- -->` yorumlarının silinmesi, başlıklar (`id="h-<satır>"`), paragraflar, iç içe listeler, görev
kutucukları (salt okunur; işaretleme yalnızca Masam'da), kalın, eğik, üstü çizili, vurgu, satır içi kod, çitli kod
blokları, alıntılar, Obsidian uyarı kutuları (`> [!tür] Başlık`, izin listeli türler), GFM tabloları, yatay çizgi,
`#etiket`, wikilink'ler (`[[A]]`, `[[A|B]]`, `[[A#H]]`, `[[klasör/A]]`; çözülemeyen → kırık bağlantı), gömmeler
(`![[x]]` yalnızca adıyla gösterilir), `[metin](adres)` ve çıplak adresler (yalnızca http/https bağlantıya dönüşür;
`javascript:`, `file:`, `data:`, `obsidian:` düz metin kalır). Kaynaktaki ham HTML kodlanmış metin olarak görünür.

---

## 5. Beyin dizinleyicisi (`Brain/*`)

### 5.1 Tarama kuralları

- Kök kasadır. Klasörler elle gezilir, böylece dışlanan klasörlere **hiç girilmez**: adı `.` ile başlayanlar (`.git`,
  `.obsidian`, `.trash`), `node_modules` ve en üst düzeydeki `80 Oturum Arşivi`. Bağlantı noktaları (junction,
  sembolik bağ) izlenmez.
- Bütün dosyalar (ad, boyut, değişme zamanı, tür) dizinlenir; içerik yalnızca 5 MB'tan küçük `.md` dosyalarından
  okunur. En fazla 5.000 dosya; fazlası için uyarı.
- `_sistem/istek-defteri/istekler.jsonl` dizinleyici tarafından değil, istek defteri ayrıştırıcısı tarafından okunur.

### 5.2 Not bilgisi

Her `.md` için: göreli yol, başlık (ilk `# ` başlığı ya da dosya adı), tür (§5.3), proje, ön bilgi, takma adlar
(`aliases`), başlıklar, bağlantılar, etiketler, görev sayıları, kelime sayısı, arama için düz metin, değişme zamanı, boyut.

### 5.3 Yola göre tür (ilk eşleşen)

| Yol | Tür | Etiket |
|---|---|---|
| `01 Şimdi.md` | `simdi` | Şimdi |
| kökteki diğer `.md` dosyaları (ör. `AGENTS.md`) | `sistem` | Sistem |
| `20 Projeler/<P>/<P>.md` | `proje-karti` | Proje kartı |
| `20 Projeler/<P>/Kayıt.md` | `kayit` | Kayıt |
| `20 Projeler/<P>/Görevler/*.md` | `gorev` | Görev |
| `20 Projeler/<P>/**` | `proje-notu` | Proje notu |
| `20 Projeler/*.md` | `proje-listesi` | Proje listesi |
| `10 Gelen Kutusu/**` | `gelen` | Gelen kutusu |
| `30 Park Yeri/**` | `fikir` | Fikir |
| `40 Alanlar/**` | `alan` | Alan |
| `50 Kaynaklar/Araştırma/**`, `Ham/**`, diğer | `arastirma`, `ham`, `kaynak` | Araştırma, Ham kaynak, Kaynak |
| `60 Ekip/Roller/**`, diğer | `rol`, `ekip` | Rol, Ekip |
| `70 Günlük/**` | `gunluk` | Günlük |
| `90 Arşiv/**` | `arsiv` | Arşiv |
| `_sistem/Şablonlar/**`, diğer `_sistem/**` | `sablon`, `sistem` | Şablon, Sistem |
| diğer her şey | `not` | Not |

Haritada kırık bağlantı hedefleri için hayalet düğüm: tür `eksik`, etiket "Eksik not". `Proje`, not
`20 Projeler/<P>/` altındaysa ve o klasörde kart varsa `<P>` olur.

### 5.4 Wikilink'ler (`WikiLinks.cs`)

Çıkarım çitli kodu, satır içi kodu, `%%…%%` ve HTML yorumlarını, `<`, `>`, `{{` içeren hedefleri yok sayar. Hedef =
`|` öncesi, sonra `#…` ve `^…` atılır. Çözüm (Obsidian'a benzer, Türkçe harfleri bilen büyük/küçük harf duyarsız
karşılaştırma): hedef `/` içeriyorsa göreli yolla eşleşme; `.md` dışı uzantı varsa dosya adıyla; yoksa `.md` dosya
adı, sonra takma adlar. Birden çok eşleşmede en kısa yol kazanır; hiç yoksa kırık. Şablon notlarındaki kırık
bağlantılar sorun sayılmaz.

### 5.5 Kayıt ayrıştırıcı (`KayitParser.cs`)

Başlık biçimi: `### [KARAR ]YYYY-AA-GG[ SS:DD] — başlık`. Devir kayıtlarında başlığın geri kalanı ` — ` ile araç ve
başlığa ayrılır. Alanlar bir sonraki `###`/`##` başlığına kadar `- Anahtar: değer` satırlarıdır (devam satırları eklenir);
anahtarlar yazıldığı gibi tutulur (`Yapılan`, `Çıktı`, `Doğrulama`, `Açık kalan`, `Sıradaki somut adım`, `Karar`,
`Neden`…). Her projenin `Kayıt.md` dosyasına ve kartın `## Son Devir` bölümüne uygulanır; en son devir tarih ve saate
göre seçilir.

### 5.6 Yeniden yükleme hattı (`AppState.ReloadAsync`)

1. `VaultSnapshot.Load(kasa)` (arka plan iş parçacığı).
2. `BrainIndex.Update()`: (değişme zamanı, boyut) değişen ya da yeni `.md` dosyaları yeniden okunur, silinenler atılır,
   bağlantı haritaları (giden, gelen, kırık) her seferinde baştan kurulur.
3. `GitActivity.Load()`: tek bir `git log --since="30 days ago"` çağrısı → günlük sayılar ve dosya başına son değişiklik
   (15 sn zaman aşımı; hata olursa boş + uyarı).
4. Sorun dedektörü; genel bakış ve ağaç (önbellekli). Harita ve arama istek gelince dizinden hesaplanır.
5. `snapshotChanged` ve `brainChanged`. Aynı anda tek yeniden yükleme çalışır; bu sırada gelen istek tam olarak bir
   yeniden yükleme daha sıraya koyar.

### 5.7 Arama (`VaultSearch.cs`)

Katlama: `tr-TR` küçük harf + `ı→i ş→s ğ→g ü→u ö→o ç→c â→a î→i û→u`, **karakter başına 1:1** (katlanmış metnin
konumları özgün metinle aynı). Terimler boşlukla ayrılır, hepsi eşleşmeli (VE). Puan: başlık ×10, başlık satırı ×4,
yol ×3, gövdedeki geçiş ×1 (en fazla 10), son 7 günde değiştiyse +2. Parça: ilk eşleşmenin ±60 karakteri, eşleşmeler
`<mark>` içinde. İstek defteri de aranır (en fazla 10 sonuç). 500 notta bütçe < 50 ms.

### 5.8 Sorun kuralları (`ProblemDetector.cs`)

`id = "<kural>:<anahtar>"`. Sıralama: kritik > dikkat > bilgi, sonra proje, sonra başlık. Her sorunun tek cümlelik bir
"Ne yapmalı" metni vardır.

| Kural | Koşul | Önem |
|---|---|---|
| P01 | Notta kırık wikilink var (not başına bir sorun) | dikkat |
| P02 | Aktif kartta `bitti_tanimi`, `sonraki_adim` ya da `olum_kriteri` boş (beklemedekinde bilgi) | dikkat |
| P02b | Aktif kartın `## Bitti Tanımı` bölümünde kutucuk yok ya da `## Engel` yok | dikkat / bilgi |
| P03 | Bırakma tarihi geçti (kritik), 0–7 gün kaldı ya da `olum_kriteri` içinde tarih yok (dikkat) | kritik / dikkat |
| P04 | Aktif proje 14 günden uzun süredir güncellenmedi (beklemede 30 gün: bilgi) | dikkat / bilgi |
| P05 | `kilit` 12 saatten eski (okunamıyorsa bilgi) | dikkat |
| P06 | Görev 2 günden uzun süredir "verildi" ya da "kontrol" durumunda, ya da son tarihi geçti | dikkat |
| P07 | Son devirde "Açık kalan" var | bilgi |
| P08 | Bekleyen güvenlik isteği (kritik istek → kritik) | dikkat / kritik |
| P09 | Bir Claude oturumu izin bekliyor olabilir (uygulamanın kendi işi değil) | dikkat |
| P10 | İş "giriş gerekli" (kritik), kota, son 24 saatte hata, reddedilen araç izni (bilgi) | kritik / dikkat / bilgi |
| P11 | Codex kotası kapalı | bilgi |
| P12 | 3'ten fazla aktif proje (kritik) ya da odak projesi yok veya birden çok | kritik / dikkat |
| P13 | Proje kararını bekliyor (`karar_bekliyor: true`) | dikkat |
| P14 | `DURDUR` var | kritik |
| P15 | Kasa yok ya da git çalışmıyor (kritik); güvenlik kapısı kurulu değil (hiç yapay zekâ işi verilmediyse dikkat, verildiyse kritik); kapı kurulu ama `IKINCI_BEYIN_KASA` bu kasayı göstermiyor (bilgi); ayrıştırıcı uyarıları (bilgi) | kritik / dikkat / bilgi |
| P16 | Park Yeri'ndeki fikir 7 günden eski | bilgi |
| P17 | Gelen Kutusu'ndaki dosya 3 günden eski | bilgi |

Sağlık puanı: `clamp(100 − 25·kritik − 6·dikkat − 1·bilgi, 0, 100)`; proje puanı yalnızca kendi sorunlarını kullanır.
Seviye: ≥ 80 `iyi`, 50–79 `dikkat`, < 50 `risk`; tek bir kritik sorun seviyeyi `risk` yapar.

### 5.9 Genel bakış kuralları (`OverviewBuilder.cs`)

- **Özet cümleleri** (verisi yoksa cümle atlanır): bugünün tarihi, aktif proje sayısı ve odak ilerlemesi; son 7 günün
  kayıt ve istek sayısı ve geçen haftayla karşılaştırma; kritik ve dikkat sayısı ve en önemli sorun; onay bekleyen sayısı.
- **Göstergeler**: aktif proje (x/3), bugün yapılan, bu haftaki kayıtlar, bu haftaki istekler (Claude ve Codex ayrı),
  açık sorunlar, onay bekleyenler, seri, yayın sayısı, gelir, bugünkü kullanım. Her birinin ipucu düz Türkçeyle ne
  olduğunu ve nasıl hesaplandığını anlatır.
- **Neler yaptık** (14 gün, en fazla 12), **neler kaldı** (en fazla 15), **son olanlar** (72 saat, en fazla 30).
- Arayüz, analiz bölümünün "kural tabanlı, yapay zekâ değil" olduğunu açıkça yazar.

### 5.10 Ağaç (`TreeBuilder.cs`)

Kök gruplar bu sırayla: Başlangıç (`01 Şimdi` ve kökteki başlangıç notları), Projeler (her proje: Kart, Kayıt ve
kayıtları, Kararlar, Görevler, Açık kalanlar, Diğer notlar), Alanlar, Park Yeri, Gelen Kutusu, Kaynaklar, Ekip (Roller
departmanlara göre), Günlük, İstek defteri (son 30 gün), Sistem, Arşiv, Tüm dosyalar. Düğüm kimlikleri kararlıdır
(`n:<yol>`, `p:<ad>`, `k:<yol>#<satır>` …).

### 5.11 Harita (`GraphBuilder.cs`)

Düğümler: dizinlenmiş `.md` notları, bağlantı verilmiş diğer dosyalar ve kırık hedefler için hayalet düğümler (en fazla
100). Bağlantılar sıralı çift başına tekilleştirilir ve sayılır. `degree` = gelen + giden. Proje süzgeci: o projenin
notları ve bir adım komşuları. 1.500'den fazla düğümde en yüksek dereceli 800 düğüm tutulur (`truncated`).

### 5.12 Performans bütçesi (selftest ölçer ve yazar)

| Adım | Bütçe |
|---|---|
| 500 sentetik notta soğuk dizinleme (bağlantılar, sorunlar, genel bakış dahil) | < 1.500 ms |
| tek not değişince artımlı güncelleme | < 150 ms |
| 50 KB notun çevrilmesi | < 30 ms |
| 500 notta arama | < 50 ms |
| 500 notta harita | < 100 ms |
| gerçek kasada tam yeniden yükleme (git dahil) | yazılır; yalnızca > 3.000 ms ise başarısız |

---

## 6. Claude ve Codex işleri (`Jobs/*`)

v2.1'den beri uygulama Claude'u ya da Codex'i **doğrudan çalıştırmaz**. İşi kasadaki iş nöbetçisine
(`_sistem/araclar/is-nobetcisi.mjs`) verir; araç bayraklarını nöbetçi kurar. Ayrıntılar EK §1 ve EK §2'de; bu bölüm
uygulama tarafını anlatır.

### 6.1 Claude'u bulma (`ClaudeCli.Find()`)

Önce `%APPDATA%\Claude\claude-code\` (Claude masaüstü uygulamasının kurduğu yer) altında adı `System.Version`
olarak ayrışan ve içinde `claude.exe` bulunan klasörler aranır; **sayısal olarak** en yüksek sürüm seçilir (metin
sıralaması yanlıştır: 2.1.99 < 2.1.280). Yoksa sırayla `%USERPROFILE%\.local\bin\claude.exe` (yerel kurucu) ve `PATH`
içindeki ilk `claude.exe` denenir; bu yedek yerlerde sürüm, dosyanın sürüm bilgisinden okunur (yoksa boş). Yalnızca
`.exe` kabul edilir (`.cmd`/`.ps1` kabuksuz başlatılamaz). Bulunamazsa `no_cli`. Nöbetçi (`claudeBul()`) de aynı
sırayla arar. `loginCommand` = `& "<claude.exe tam yolu>"` (Terminal'e yapıştırılıp
`/login` yapılır).

### 6.2 İstem ve araç bayrakları

İstem kuralları (`ClaudeCli.BuildFinalPrompt`): rol seçildiyse `"<rol>: <metin>"`, proje seçildiyse sonuna
`" (Proje: <ad>)"`; NUL silinir, satır sonları LF yapılır; `-` ile başlıyorsa başına `Görev: ` eklenir; sonuna kasa
kurallarına uyma eki eklenir: `" Follow AGENTS.md (lock, handoff, never commit)."`. Ek dahil **toplam** en fazla 4.000
karakter (nöbetçinin sınırıyla aynı).

Nöbetçinin Claude için kurduğu komut (EK §1): `-p` (istem standart girişten), `--output-format stream-json`,
`--verbose`, `--permission-mode acceptEdits`, `--permission-prompts none`, varsa `--resume <oturum>`, izinli proje
klasörü için `--add-dir`, `--tools` ve `--allowedTools` = `Read,Grep,Glob,Edit,Write,TodoWrite,WebSearch` (işte
"İnternetten sayfa da okuyabilsin" işaretliyse `+WebFetch`), `--disallowedTools Bash` (ve web kapalıysa `WebFetch`),
`--strict-mcp-config` (bağlı hesap eklentileri kapalı). `ClaudeCli.BuildArguments` yalnızca selftest için bir referans
kopyadır; çalışırken geçerli olan nöbetçinin listesidir ve selftest ikisini karşılaştırır.

`--add-dir`: yalnızca proje seçildiyse, kartında `klasor:` yazıyorsa, klasör varsa, **Masaüstü'nün gerçek bir alt
klasörüyse** (aynı önekle başlayan kardeş klasör geçmez) ve kasanın kendisi değilse eklenir.

### 6.3 Süreç

`node.exe` (`PATH`'ten; yoksa `no_cli`) ile nöbetçi başlatılır: `UseShellExecute=false`, `CreateNoWindow=true`,
çalışma klasörü kasa, standart çıktı **yönlendirilmez**, uygulamanın iş nesnesine bağlanmaz; süreç tutamacı hemen
bırakılır. Başlatmadan önce: acil durdurma açıksa `stopped`, salt okunur modda `readonly` (deneme modu hariç), 2 Claude
işi çalışıyorsa `limit`, nöbetçi betiği yoksa `not_found`, nöbetçi betiği git'e göre değiştirilmiş ya da takip
edilmiyorsa `forbidden` (§10.3). Claude'un ortamından `ANTHROPIC_API_KEY`, `ANTHROPIC_AUTH_TOKEN`, `CLAUDECODE`
çıkarılır (D6).

### 6.4 Dosyalar

Her iş `DataDir\claude-isler\` ya da `DataDir\codex-isler\` altında `<damga>` adıyla dosyalar bırakır
(`<damga>` = `yyyyMMdd-HHmmss-fff-<8 onaltılık>`): `.istem.txt` (uygulama yazar), `.jsonl` (aracın her çıktı satırı +
nöbetçinin kendi olay satırları), `.nobet.json` (nöbetçinin durumu), `.kilit.<n>` (nöbetçinin sahiplik kilidi),
`.iptal` ve `.simdi` (uygulamanın sinyalleri). Dosya biçimleri: EK §1.

### 6.5 Akış ayrıştırıcıları (`ClaudeStreamParser`, `CodexStreamParser`)

Saf fonksiyonlar, birim testli. Görevleri insan tarafından okunur "şu an" metnini ve adım listesini üretmektir; işin
**durumu için yetkili kaynak `nobet.json`'dur** (§6.7).

| Claude satırı | Etki |
|---|---|
| `system/init` | oturum kimliği, "Başladı" |
| `assistant` + `tool_use` | adım sayısı artar; `now` = "Okuyor: …", "Arıyor: …", "Düzenliyor: …", "Yazıyor: …", "Web'de arıyor: …", "Sayfa okuyor: …", "Plan güncelliyor", "Komut: …", "Yardımcı ajan çalışıyor"; düzenlenen dosyalar `filesChanged`'e (en fazla 50) |
| `assistant` metin / düşünme | "Yazıyor…" / "Düşünüyor…" |
| `tool_result` hatası içinde `Güvenlik kapısı` | `#<kimlik>` → `safetyIds`; adım "Onayını bekliyor (#kimlik)" |
| aynısı `ACİL DURDUR` ile | adım "Acil durdurma yüzünden durdu" |
| `result` | sonuç metni; `permission_denials` → `denials`; başarı ya da hata |
| giriş ya da kota kalıbı | `giris` ya da `kota` ve tek satırlık Türkçe çözüm |

Codex satırları (`codex exec --json`): `thread.started` → oturum ve "çalışıyor"; `item.*` → komut "Komut: …", dosya
değişikliği "Düzenliyor: …", ajan mesajı "Yazıyor…"; `turn.completed` → bitti; `turn.failed` ya da `error` → hata,
kota ya da giriş.

**"İzin bekliyor" nasıl görünür?** Başsız bir iş hiçbir zaman soru sorup donmaz; bekleme üç somut biçimde görünür:
(1) güvenlik kapısı bir işlemi kuyruğa aldı → `safetyIds` → Masam'daki onay kartına giden etiket; (2) izinli liste
dışındaki bir araç reddedildi → `denials` → "İzin verilmedi: …"; (3) çalışırken 10 dakikadır çıktı yok → `quiet`.
Uygulama dışında açılmış etkileşimli Claude oturumları ayrıca `ClaudeSessionReader` ile okunur (P09).

### 6.6 Durum makinesi

```text
basliyor ──ilk durum──▶ calisiyor ──başarılı──▶ bitti
                           ├──hata / çıkış≠0──▶ hata
                           ├──giriş kalıbı──▶ giris                          (son durum)
                           ├──kota──▶ kota-bekliyor ──sıfırlanma / "Şimdi dene"──▶ calisiyor (aynı oturum)
                           │                └──deneme hakkı bitti──▶ kota   (son durum)
                           ├──İptal / Acil Durdur──▶ durduruldu
                           └──süre sınırı (varsayılan 120 dk)──▶ hata
uygulama yeniden açıldı: nobet.json "calisiyor"/"kota-bekliyor" ve nöbetçi süreci yaşıyor → izlemeye devam
                         süreç ölü → yarida ("Uygulama kapanınca yarıda kaldı")
```

Renkler: `basliyor`/`calisiyor` deniz yeşili, `bitti` yeşil, `hata`/`giris` kırmızı, `kota`/`kota-bekliyor` sarı,
`durduruldu`/`yarida` gri. "Süreç yaşıyor mu?" kontrolü pid'e ek olarak sürecin adının `node` olmasına ve başlama
zamanının `nobet.json`'daki `basla` ile 10 sn içinde tutmasına bakar; Windows'un aynı pid'i başka bir süreçte yeniden
kullanması işi sonsuza dek "çalışıyor" göstermez.

### 6.7 JobStore

İki iş klasörünü saniyede bir yoklar (dosya izleyici yerine basit yoklama; iş başına saniyede en fazla 2 bildirim
bütçesine zaten uyar). Uygulamanın başlatmadığı işleri de (uygulama yeniden açıldıysa ya da Tam Gaz işçisi başlattıysa)
diskten bulur. Her işin `.jsonl` dosyasını kaldığı bayttan okur ve ayrıştırıcıya besler. **Durum sırası: `nobet.json` >
ayrıştırıcı.** Aynı anda en fazla 2 çalışan Claude işi kuralında `kota-bekliyor` işler sayılmaz. `StopAll()` her
çalışan ya da bekleyen işe `.iptal` sinyali yazar. Liste: önce çalışanlar, sonra geçmiş (toplam en fazla 50; 500'ü
aşınca 30 günden eski bitmiş işler bellekten atılır).

### 6.8 Acil Durdur ve Tam Gaz

- **Acil Durdur** (`emergencyStop`): kasaya `_sistem/guvenlik/DURDUR` yazılır; Tam Gaz kapatılır; `JobStore.StopAll()`
  her işe `.iptal` yazar; son olarak `CodexRunner.EmergencyStop` komut satırına bakarak kalan süreçleri zorla kapatır:
  yalnızca iş nöbetçisini çalıştıran `node.exe` ve `exec` çalıştıran `codex.exe` (Codex masaüstü uygulamasına asla
  dokunulmaz). Güvenlik kapısı `DURDUR` varken okuma dışındaki her araç çağrısını reddeder; her nöbetçi 2 sn'de bir
  `DURDUR`'a bakar. **Devam Et** (`resume`) arayüzde onay ister, sonra `DURDUR`'u siler.
- **Tam Gaz**: `_sistem/tamgaz/ACIK` içine `{baslangic, bitis, saat}` yazılır (2, 5, 10 ya da 12 saat); geçersiz ya
  da süresi dolmuş dosya kapalı sayılır. Uygulama yalnızca bu anahtarı yazar ve siler; anahtarı okuyup görev işleyen
  zamanlanmış işçi uygulamanın parçası değildir (kullanıcı kendi zamanlanmış görevini kurar). İşçinin kasaya yazdığı
  `codex-nobet.json`, `codex-<gün>.log` ve `gunluk.md` yalnızca okunur ve Şirketim'de gösterilir.

---

## 7. Arayüz (`wwwroot`)

### 7.1 Açılış

`index.html`: önce CSP meta, `referrer` = `no-referrer`, `css/tokens.css`, `css/app.css`, gizli bir SVG simge sprite'ı,
kök kaplar (`#splash`, `#login`, `#app`, `#overlay-root`, `#toast-root`, `#tip`), sonra `lib/echarts.min.js` ve
`<script type="module" src="js/main.js">`. Satır içi betik ya da dış adres yok. Modüller `.js` uzantılıdır (hem WebView2
klasör eşlemesi hem basit bir test sunucusu doğru MIME türünü versin diye).

`main.js`: köprüyü seç (`window.chrome.webview` varsa gerçek, yoksa sahte) → `hello` → tema ve hareket → `getAuthState`
→ giriş penceresi ya da uygulama kabuğu → `getSnapshot` → adres çubuğundaki `#` rotası (varsayılan `#/masam`).

### 7.2 Modüller

- `bridge.js`: `request(type, payload, {timeout})` → yükün sözü (Promise) ya da `{code, message}` reddi;
  `on(type, fn)` → abonelikten çıkma fonksiyonu; `isMock`. Zaman aşımı varsayılan 30 sn, `runShortcut` 70 sn, `login` 15 sn.
- `store.js`: basit durum deposu (`get/set/patch/on`). `snapshotChanged` anlık görüntüyü değiştirir, `jobUpdated` işi
  günceller, `brainChanged` Beyin önbelleklerini atar ve ekrandakini yeniden ister.
- `router.js`: `#/masam`, `#/beyin/genel`, `#/beyin/kesif?path=…&a=…`, `#/beyin/harita[?project=]`, `#/beyin/sorunlar`,
  `#/beyin/proje/<ad>[?s=<bölüm>]`, `#/sirketim`, `#/gecmis`, `#/basvurular`, `#/projeler`, `#/kestirmeler`, `#/ayarlar`.
  Sayfa modülü = `{ id, mount(el, ctx), update(ctx, key), unmount() }`. `openTarget(target)` her `Target`'ı bir rotaya
  ya da köprü çağrısına çevirir.
- `ui.js`: `h(etiket, özellikler, ...çocuklar)` (yalnızca `textContent`), simge, etiket, düğme, sayaç animasyonu,
  halka, kıvılcım grafiği, boş durum, iskelet, bildirim, pencere (odak tuzağı, Esc), ipucu, konfeti, biçimlendiriciler.
- `charts.js`: ECharts temaları CSS değişkenlerinden kurulur; grafikler boyut değişimini izler, sayfa kapanınca atılır,
  tema değişince yeniden kurulur; ipucu biçimlendiricisi **her değeri kaçışlar**.
- `scale.js`: ekran boyutu adımları (EK §2c). `imza.js`: yapan, sürüm, lisans (C# `Imza.cs` ile eş; selftest denetler).

**`innerHTML` yalnızca şunlarda serbesttir:** `note.html`, `jobDetail.resultHtml`, `searchResult.snippetHtml` (üçü de
C#'ta temizlenmiştir) ve veri içermeyen sabit işaretleme. İşlenmiş HTML içindeki `a[data-note]` ve `a[data-url]`
tıklamaları yakalanır ve `openTarget` ya da `openUrl`'e yönlendirilir; diğer `<a>` etiketleri hiçbir yere gitmez.

### 7.3 Sayfalar

- **Kabuk:** üst çubuk (logo, tarih, odak etiketi, seri, Tam Gaz etiketi; sağda arama, ışıklar, Tam Gaz, Acil Durdur /
  Devam Et, "Codex'e iş ver", "Claude'a iş ver", Yenile, tema), acil durdurma açıkken kırmızı şerit, kasa yoksa sarı
  şerit. Sol menü: Masam, Beyin, Şirketim, Geçmiş, İş Başvuruları, Projeler, Kestirmeler; altta Ayarlar ve Kilitle.
  1.180 CSS pikselinin altında menü simge şeridine döner.
- **Masam:** selamlama, günün tek işi ve "Başla" (odak), 3/3 sınır uyarısı, onay bekleyenler (güvenlik kartları;
  kritikte "Komutu okudum, bir kez yapılsın" kutulu pencere; kısaltılmış komutta Onayla kapalı), küçük sayılar,
  Yapılacaklarım (bitince satır aşağı kayar, kutlama), projelerden senden beklenenler, "Günü kapat".
- **Beyin:** Genel bakış, Keşfet (ağaç · okuyucu · bilgi paneli), Harita (ECharts kuvvet grafiği; tür süzgeci, proje
  seçimi, arama, yakınlaştırma), Sorunlar (önem süzgeci, "Ne yapmalı"), Proje ayrıntısı (sağlık, ilerleme, bırakma
  tarihi, Kayıt zaman çizelgesi, Kararlar, Görevler kanban, Açık kalanlar, Dosyalar).
- **Şirketim:** çalışan işler (Claude, Codex, son 15 dakikanın Claude oturumları, zamanlanmış görevler, Tam Gaz ve
  işçi), iş satırında canlı süre, adım, etiketler, Detay (adım listesi, sonuç, "Günlüğü aç") ve Durdur; `giris`
  satırında çözüm cümlesi ve "Komutu kopyala"; `kota-bekliyor` satırında geri sayım, "Şimdi dene" ve "İptal";
  roller departmanlara göre, her birinde "İş ver".
- **Geçmiş:** 30 günlük etkinlik grafiği, süzgeçler, arama, gün başlıklı zaman çizelgesi.
- **İş Başvuruları:** bölüm başına tablo, bekleyen satırlarda kutucuk, "Seçilenleri onayla", çift tık ilanı açar.
- **Projeler:** iki sütunlu proje kartları, "Beyin'de aç", "Obsidian'da aç", "Klasörü aç".
- **Kestirmeler:** kutucuklar; bakım betikleri çıktı penceresiyle.
- **Ayarlar:** Kasa, Görünüm (tema, ekran boyutu, hareketler), Güvenlik (kilit süresi, şifre), "İş varken bilgisayar
  uyumasın", Bağlantılar (araç durumu), Hakkında (imza, sürüm, lisans).
- **Pencereler:** Giriş (ilk açılışta iki alan), İş ver (0/4000 sayaç, rol ve proje seçimi, istem önizlemesi,
  "İnternetten sayfa da okuyabilsin", "Claude'a ver — hemen başlasın", "Codex'e ver", "Sadece kopyala"), Odak (25/50
  dakika), Tam Gaz (2/5/10/12 saat), Arama paleti (Ctrl+K).

### 7.4 Grafikler (ECharts 5, canvas)

Ortak: 900 ms `cubicOut` animasyon, yuvarlak sütunlar, deniz yeşili geçişli dolgular, `tooltip.confine`. Claude =
deniz yeşili, Codex = arduvaz. Her grafiğin ipucunda tek satırlık bir "Bu ne?" açıklaması vardır (ör. "İstek =
Claude'a ya da Codex'e yazdığın bir mesaj."). Ekran görüntüsü ve hareketsiz modda animasyon kapalı.

### 7.5 Hareket

Yalnızca CSS geçişleri ve `requestAnimationFrame`; `html.no-anim` hepsini kapatır (`hello.animations` false,
`prefers-reduced-motion: reduce` ya da `?shot=1`). Sayfa girişi, sekme alt çizgisi, kart yükselmesi, sayaç, ilerleme
halkaları, yapılacak bitince kutlama (konfeti), çalışan iş için nefes alan nokta, kritik ışıkta yavaş nabız.

### 7.6 Tema belirteçleri

`css/tokens.css`, `html[data-theme="light"|"dark"]`. Açık zemin `#F7F7F5`, koyu zemin `#0E1417`; ana renk deniz yeşili
(`--teal` açıkta `#0F766E`, koyuda `#2DB5A5`), ikincil arduvaz; yeşil, sarı ve kırmızı yalnızca durum için. Yazı tipleri
sistemden (Segoe UI Variable, Cascadia Mono). Metin karşıtlığı iki temada da en az WCAG AA.

### 7.7 Klavye ve erişilebilirlik

Genel: **Ctrl+1…7** sayfalar, **F5** yenile, **Ctrl+L** kilitle, **Ctrl+K** arama paleti, **Ctrl + artı / eksi / 0** ve
Ctrl + fare tekerleği ekran boyutu (EK §2c). Sayfaya özel: **Ctrl+F** (Geçmiş, Keşfet ağacı), **Alt+← / Alt+→** (Keşfet
not geçmişi), **Ctrl+Enter** (İş ver → Claude'a ver), **Esc** en üstteki pencereyi kapatır. Odak modu açıkken yalnızca
Esc çalışır. Anlamsal yapı: `nav` + `aria-current`, sekmeler `role="tablist"`, pencereler `role="dialog"` + odak
tuzağı + odağın geri dönmesi, bildirimler `aria-live="polite"`, grafikler `role="img"` + özet etiketi, görünür odak
halkası, yalnızca simgeli her düğmede `aria-label` ve ipucu. Bütün arayüz metni Türkçe ve doğru harflerle.

---

## 8. Sahte köprü (`js/mock.js` + `js/mock-data.js`)

Yalnızca `window.chrome.webview` yokken (sıradan bir tarayıcıda) çalışır; aynı `request/on` arayüzü, 80–250 ms
gecikmeli cevaplar, derin kopyalar. Adres parametreleri (yalnızca sahte modda): `unlocked=1` girişi atla,
`theme=dark`, `auth=giris` (bir Claude işi giriş bekliyor), `empty=1` (boş kasa: her boş durumu dener),
`open=givework|tamgaz|focus|palette`, `selfcheck=1`. `shot=1` iki modda da animasyonları kapatır. Sahte girişin şifresi
`test123`. `runClaude` her 1,5 sn'de ilerleyen bir iş üretir; `openUrl`, `openFolder`, `copyText`, `runShortcut`
"(deneme)" bildirimi gösterir.

`mock-data.js`, DOM kullanmayan düz bir ES modülüdür ve §3'teki şekillerle **birebir** aynı nesneleri dışa aktarır.
Verisi uydurmadır (örnek kasadaki projeler ve roller). `selfcheck=1`: her sayfa bir kez çizildikten sonra
`document.body.dataset.selfcheck` `"ok"` ya da `"fail:<neden>"` olur.

Arayüzü C# olmadan çalıştırmak için `wwwroot` klasörünü yerel bir statik sunucuyla aç (ör.
`python -m http.server 8080 -d Ordinaryunus\wwwroot`) ve `http://localhost:8080/index.html?unlocked=1` adresine git.

---

## 9. Testler

### 9.1 `Ordinaryunus.exe --selftest [kasa]`

Başsız; kasa verilmezse ayarlardaki kasa. Gerçek kasa salt okunur taranır; birim testleri `%TEMP%` altındaki geçici
kasalarda ve `ORDINARYUNUS_DATA` geçici klasörüyle çalışır. Çıktı ayrıca `%TEMP%\ordinaryunus-selftest.txt`; çıkış kodu
0 = hepsi geçti. Kapsam:

- **A. Çevirici:** başlıklar, listeler, görevler, tablolar, uyarı kutuları, kod, wikilink, gömme, etiket; XSS
  örnekleri (`<script>`, `onerror`, `javascript:` bağlantı, tırnak kaçırma, `data:` adres…) hiçbir `<script`,
  `javascript:`, `on…=`, `style=`, `href=`, `src=` üretmemeli; etiket tarayıcısı izin listesini denetler.
- **B. Dizinleyici:** türler, kod içindeki bağlantının yok sayılması, takma ad ve yol çözümü, kırık bağlantı,
  `80 Oturum Arşivi` içindeki benzersiz bir işaretin hiçbir yerde görünmemesi, artımlı güncellemenin tek dosyayı
  yeniden okuması, §5.12 bütçeleri.
- **C. Kayıt ayrıştırıcı**, **D. Sorun kuralları** (her kural kimliği, sağlık formülü).
- **E. Köprü:** bozuk JSON, 70.000 karakterlik mesaj, eksik ya da metin `id`, dizi `payload`, bilinmeyen tür ve alan,
  kilitliyken her tür → `locked`, yol saldırıları (`../../Windows/win.ini`, `C:\Windows\win.ini`, `80 Oturum Arşivi/…`),
  `openUrl` ile `javascript:`, `file:`, kullanıcı bilgili adres, `openFolder` kaçışları, bilinmeyen kestirme, bayat
  anahtar, kritik onay kutusu, `DURDUR` varken iş başlatma, 5 yanlış şifre → `auth_wait`.
- **F. İşler:** Claude sürüm seçimi, istem kuralları, yasak bayrakların yokluğu, ortam değişkenlerinin çıkarılması,
  ayrıştırıcı örnekleri (başarı, süresi dolmuş giriş, kota, bilinmeyen seçenek), sahte nöbetçiyle uçtan uca iş, kota →
  bekleme → "Şimdi dene" → devam, uygulamayı "yeniden açma" benzetimi; nöbetçinin kendi test betiği
  (`is-nobetcisi-test.mjs`).
- **G. Ana makine ve arayüz sözleşmesi:** CSP eşitliği, `index.html`'de dış adres olmaması, `echarts.min.js`'in SHA-256'sı
  `VERSIONS.txt` ile aynı, gezinme izin tablosu, `Imza.cs` ile `imza.js` eşitliği.
- **H. Gerçek kasa (salt okunur):** not, bağlantı, kırık bağlantı ve sorun sayıları, dizinleme süresi, ilk 5 sorun.

### 9.2 `Ordinaryunus.exe --dump-json <klasör> [--vault <kasa>]`

Salt okunur. Hedef `%TEMP%` altında olmalı (gerçek kasa içeriği yanlış yere kopyalanmasın), değilse çıkış 6. Gerçek
işleyicileri kullanarak `snapshot.json`, `overview.json`, `tree.json`, `note.json`, `project.json`, `graph.json`,
`problems.json`, `jobs.json`, `settings.json` yazar. Sahte verinin şekillerini gerçek cevaplarla karşılaştırmak için
kullanılır.

### 9.3 `Ordinaryunus.exe --screenshots <klasör> [--size 1600x1000] [--scale 1.0] [--theme dark] [--vault <kasa>]`

Gerçek veri, salt okunur, `Launch.Disabled`, mutex yok. Pencere istenen boyutta ekranda gösterilir (WebView2 gizli
pencereyi çizmez). Önce `giris.png` (kilitli), sonra her görüntü için `shot` olayı → `pageReady` beklenir (15 sn) →
300 ms → `CapturePreviewAsync(Png)`. Adlar: `giris`, `masam`, `beyin-genel`, `beyin-kesif`, `beyin-proje`,
`beyin-harita`, `beyin-sorunlar`, `sirketim`, `gecmis`, `basvurular`, `projeler`, `kestirmeler`, `ayarlar`, `is-ver`,
`odak`, `tam-gaz`, `arama`; `--theme dark` ile `-koyu` eki. Varsayılan klasör exe'nin yanında `test-kanitlari-v2`.
Bütün çalışma 8 dakikayı geçerse süreç kendini sonlandırır (çıkış 7). `beyin-proje` odaktaki projeyi (yoksa ilk projeyi)
açar; `beyin-kesif` aynı projenin kartını okuyucuda açık gösterir; `beyin-harita` grafik yerleşince grafik kartına
kaydırılır; `ayarlar` sayfanın altına, Bağlantılar ve Hakkında kartına kaydırılarak çekilir. Şirketim ve Ayarlar profil
klasöründeki `.claude`/`.codex` bilgilerini gösterdiği için paylaşılacak görüntülerde `ORDINARYUNUS_PROFIL` uydurma bir
profil klasörünü göstermeli; verilmezse konsola uyarı yazılır. Depodaki `belgeler/ekran-goruntuleri/` görüntüleri
`ornek-kasa`'nın git'li bir kopyası, uydurma bir profil ve boş bir `ORDINARYUNUS_DATA` ile alındı.

### 9.4 Arayüzün sahte veriyle denenmesi

§8'deki gibi yerel sunucuyla; açık ve koyu temada, 1600×1000 ve 1280×800 boyutlarında, `empty=1` ile boş durumlar ve
`selfcheck=1` ile otomatik kontrol. Tarayıcı konsolunda CSP ihlali (`Refused to …`) ya da yakalanmamış hata olmamalı.

### 9.5 Deneme modu ile elle kabul

`ORDINARYUNUS_DATA=%TEMP%\ordi-deneme` ile `--dry-run`: şifre belirle, kilitle ve aç, 1 dakikalık boşta kilit, her
sayfa, Ctrl+1…7, Ctrl+K, F5, tema, %150 ekran boyutu, iş ver → sahte iş `bitti`'ye kadar ilerler, ikinci işi iptal et,
sahte kota → `kota-bekliyor` → "Şimdi dene", Acil Durdur akışı, Tam Gaz penceresi, yapılacak kutlaması (deneme modunda
yazma yok: "Deneme modu" bildirimi). Gerçek Claude, Codex, Acil Durdur ve Tam Gaz'a yalnızca kullanıcı kendi elle basar.

---

## 10. Güvenlik modeli

### 10.1 Neye karşı?

Ordinaryunus'ta yapay zekâ araçları kullanıcının kasasında, kullanıcının Windows hesabıyla çalışır. Asıl riskler:
(a) bir yapay zekânın yanlış ya da kandırılmış bir talimatla geri dönüşü zor bir şey yapması (silme, kurma, zorla git
işlemi), (b) kasadaki içeriğin gizli bir talimatla dışarı taşınması, (c) arayüzün (web içeriğinin) bir not ya da
ilandaki kötü niyetli HTML ile ele geçirilmesi, (d) uygulamanın başkası tarafından açılması.

### 10.2 Katmanlar

| Katman | Nerede | Ne yapar |
|---|---|---|
| Şifre ve kilit | `Security/*`, §2.4 | PBKDF2-SHA256 (200.000 tur, 16 bayt tuz, 32 bayt anahtar), sabit zamanlı karşılaştırma, artan bekleme, boşta kilit. Kilitliyken köprü veri vermez. |
| Web içeriği sertleştirmesi | §2.3, §4, §7.2 | Sıkı CSP, dış istek yok, gezinme ve pencere açma yasak, HTML yalnızca C#'ın izin listeli çeviricisinden, `innerHTML` üç yerde. |
| Köprü doğrulaması | §3.2, §3.10 | Her istek izin listesinde, bilinmeyen alan reddedilir, yollar dizinle sınırlı, adresler http/https, klasörler sabit listeden. |
| Dar yazma alanı | §10.4 | Kasaya yalnızca dört yazma; satır doğrulamalı ve atomik. |
| Dar araç yetkisi | §6.2 | Claude'da kabuk ve MCP kapalı, WebFetch isteğe bağlı; Codex `workspace-write` korumalı alanında; izin atlama bayrağı yok. |
| Güvenlik kapısı | `kasa-araclari/claude-kancalari/guvenlik-kapisi.mjs` | Araç kullanımından önce çalışır; aşağıda. |
| Onayların kasa dışında tutulması | `SafetyQueue.cs` | Kararlar `%LOCALAPPDATA%\Ordinaryunus\guvenlik\kararlar.jsonl`'da. |
| Kendi onay kaydı | `OwnDecisionLog.cs` | Uygulama verdiği her onayı ayrıca kaydeder; kapının dosyasında uygulamadan geçmemiş bir "onay" görürse kırmızı uyarı. |
| Acil Durdur | §6.8 | Tek düğmeyle bütün yapay zekâ işlerini durdurur. |

**Güvenlik kapısı** (Claude Code ve Codex için `PreToolUse` kancası):

1. `_sistem/guvenlik/DURDUR` varsa okuma dışındaki her araç reddedilir.
2. Korunan köklerde (kasa, Masaüstü, Belgeler, OneDrive, Resimler, Videolar, `.claude`, `.codex`) kritik bir işlem
   (dosya silme, `git reset --hard`/`push`/`clean -f` gibi geri dönüşü zor git işlemleri, program kurma ve kaldırma,
   kayıt defteri, kapatma ve disk araçları, internetten betik çalıştırma, zamanlanmış görev değişikliği, veritabanı
   silme) reddedilir ve `_sistem/guvenlik/bekleyenler.jsonl` kuyruğuna yazılır. Derleme, bağımlılık ve geçici klasörler
   (`bin`, `obj`, `node_modules`, `publish`, `dist`, `build`, `out`, `tmp`, `temp`…) serbesttir. PowerShell'in dosya
   olmayan sürücüleri (`Env:`, `Variable:`, `Function:`, `Alias:`) silme hedefi sayılmaz; kayıt defteri sürücüleri sayılır.
3. Uygulamada onaylanan istek, yapay zekâ aynı işlemi tekrar denediğinde **bir kez** geçer; reddedilen istek için
   yapay zekâya "yapma, başka yol bul" denir. Onay yalnızca **bütün** bir kayda bağlanır: kapı, kaydın özetini
   kaydın kendi görünen alanlarından (araç, işlem, hedef, komut) yeniden hesaplar; kartta başka, özette başka komut
   taşıyan bir kayıttaki onay sayılmaz.
4. Onay dosyasına, Tam Gaz anahtarına ve kasadaki `_sistem/guvenlik/` klasörüne (kuyruk, `DURDUR`) yapay zekânın
   yazması (araçla ya da kabukla) her zaman reddedilir; okuma serbesttir.
5. Git dışındaki korunan bir dosya değiştirilmeden önce `%LOCALAPPDATA%\Ordinaryunus\yedekler\<tarih>\` altına kopyalanır.
6. Kancanın kendi iç hatası ana aracı durdurmaz (hata günlüğe yazılır); `DURDUR` kontrolü bundan önce yapılır.

Uygulama tarafında onay kartı: kritik istek için ayrıca "Komutu okudum, bir kez yapılsın" onayı gerekir; kapı komutu
600 karakterde kestiyse (`kesildi` ya da uzunluk tam 600) Onayla kapalıdır, çünkü ekranda sonunu görmediğin bir komutu
onaylamış olmazsın. Karar satırı, kartın içerik özetini (`ozet`) de taşır; aynı kimlikle arkadan değiştirilmiş bir
kayda onay bağlanmaz.

### 10.3 Kasadaki betiklere güven

Uygulamanın başlattığı işler kasaya yazabilir, ama uygulama da kasadaki bazı betikleri (iş nöbetçisi, bakım betikleri)
kullanıcının tam yetkisiyle çalıştırır. Bir işe gizlenmiş kötü bir talimat bu betiklerden birini bir sonraki tıklamadan
hemen önce değiştirebilirdi. Bu yüzden (`Data/VaultGitGuard.cs`):

- **Kasa bir git deposuysa:** git'in "değişmiş" ya da "izlenmiyor" gördüğü betik çalıştırılmaz. Çare: değişikliği
  gözden geçirip commit etmek.
- **Kasa git deposu değilse ya da git yoksa:** betiğin SHA-256 özeti kasanın dışında
  (`%LOCALAPPDATA%\Ordinaryunus\guvenlik\betik-izleri.json`) saklanır. İlk kullanımda o anki hâline güvenilir; içerik
  sonradan değişirse çalıştırmadan önce Windows'un yerel onay kutusuyla sorulur. Onay kutusu olmayan modlarda
  (selftest, deneme, ekran görüntüsü) değişmiş betik çalıştırılmaz.

Bu bir derinlemesine savunma katmanıdır, tek başına çözüm değildir.

### 10.4 Uygulamanın kasaya yazdıkları

1. `01 Şimdi.md` › Yapılacaklarım: tek satırdaki `- [ ]` ↔ `- [x]`.
2. `40 Alanlar/İş Başvuruları.md`: seçilen satırlarda Durum hücresi "Onay bekliyor" → "Onaylandı" (ya hep ya hiç).
3. `_sistem/guvenlik/DURDUR` (oluştur ve sil).
4. `_sistem/tamgaz/ACIK` (oluştur ve sil).

Her satır yazmasında dosya yeniden okunur, özgün satırın aynı olduğu doğrulanır, yalnızca o satır değiştirilir, satır
sonları ve BOM korunur, geçici dosya + `File.Replace` ile atomik yazılır (farklı sürücüdeyse hedefin yanında
`.ordinaryunus-tmp` uzantılı geçici dosya). Dosya arada değiştiyse `stale`. Kasa dışında yazılanlar için §1.3.

### 10.5 Bilinen sınırlar

1. Seninle aynı Windows kullanıcısıyla çalışan bir yapay zekâya karşı kusursuz koruma mümkün değildir (ör. kısa 8.3
   dosya adlarıyla metin eşleşmesinden kaçmak). Kapı, kasa dışı onaylar ve kendi onay kaydı birlikte bir engel ve
   alarm sistemidir.
2. Şifre uygulamayı korur, dosyaları değil. Notlar düz metindir; disk şifrelemesi kullanıcının işidir.
3. Claude işleri kasaya (ve izinli proje klasörüne) yazabilir; kurma, silme, gönderme gibi işler güvenlik kapısının
   arkasında kalır. Bu yüzden bazı işler "İzin verilmedi" etiketleriyle `bitti` olarak biter; bu bilerek böyledir.
4. Başsız işler aboneliği harcar: en fazla 2 Claude işi, 120 dakika sınırı, her işten önce onay.
5. Uygulama kapalıyken bilgisayar uyursa iş durur (uyanık tutmayı yalnızca açık uygulama yapar; EK §2b).
6. Claude komut satırı yalnızca Claude masaüstü uygulamasının kurduğu yerde aranır (§6.1).
7. `style-src 'unsafe-inline'` ECharts ipuçları için gereklidir; betikler sıkı kalır.

---

## Ek: v2.1 (iş nöbetçisi, uyanık tutma, ekran boyutu)

Koddaki `EK-v2.1 §…` ve `EK §…` göndermeleri bu bölümün numaralarını gösterir.

### EK §1 İş nöbetçisi

Dosya: kasada `_sistem/araclar/is-nobetcisi.mjs` (+ `is-nobetcisi-sahte.mjs` deneme aracı, `is-nobetcisi-test.mjs`
testleri). Node.js ile çalışır, dışa bağımlılığı yoktur.

#### EK §1.1 Komut satırı

```text
node <kasa>\_sistem\araclar\is-nobetcisi.mjs --arac claude|codex --is <damga>
     (--istem-dosyasi <yol> | --gorev "<kasaya göre görev paketi yolu>")
     [--ekdizin <proje klasörü, claude>] [--cd <kasa dışı çalışma klasörü, codex>] [--klasor <iş klasörü>]
     [--bekle 20] [--deneme 12] [--pay-sn 120] [--sure-dk 120] [--web] [--exe <claude.exe>] [--sahte <deneme betiği>]
```

- İş klasörü varsayılanı `%APPDATA%\Ordinaryunus\claude-isler\` ya da `codex-isler\`; uygulama her zaman `--klasor`'u
  açıkça verir (testler gerçek klasörlere dokunmasın).
- İstem araca komut satırından değil **standart girişten** verilir: süreç listesinde görünmez, uzunluk sınırına takılmaz.
- Codex, kasadaki `_sistem/araclar/codex.mjs` köprüsüyle `exec --json --skip-git-repo-check -C <klasör> -s workspace-write`
  olarak çalıştırılır; köprü Codex masaüstü uygulamasının getirdiği en yeni `codex.exe`'yi
  (`%LOCALAPPDATA%\OpenAI\Codex\bin\<kod>\`) bulur, yoksa `PATH`'teki komut satırını kullanır.
- Çıkış kodları: 0 bitti, 1 hata, 2 yanlış kullanım ya da iş zaten çalışıyor, 3 durduruldu, 4 giriş gerekli, 5 kota
  deneme hakkı bitti.

#### EK §1.2 Dosyalar ve sinyaller

- `<damga>.jsonl`: aracın her çıktı satırı olduğu gibi +
  `{"type":"ordinaryunus","event":"start|devam|stderr|exit|kota-bekliyor|simdi-dene|cancel|durdur|timeout","kaynak":"nobetci",…}`.
- `<damga>.nobet.json` (atomik yazılır): `{is, arac, pid, durum, deneme, sessionId, bekleUntil, exitCode, not, basla, guncel}`;
  `durum` ∈ `calisiyor | kota-bekliyor | bitti | hata | giris | durduruldu`; `guncel` dakikada bir tazelenir (nöbetçi canlı).
- `<damga>.kilit.<n>`: nesilli sahiplik kilidi; iki süreç aynı işi aynı anda yürütemez; kilitler silinmez.
- Uygulamadan nöbetçiye: `<damga>.iptal` oluştur → en geç 4 sn'de durur. `<damga>.simdi` dosyasına **yeni bir değer**
  yaz → kota beklemesi hemen biter ve yeniden denenir ("Şimdi dene").
- `_sistem/guvenlik/DURDUR` varsa her nöbetçi 2 sn içinde durur.

#### EK §1.3 Kota beklemesi

Araç çıktısında kota kalıbı görülürse nöbetçi mesajdan sıfırlanma zamanını çıkarmaya çalışır (Unix zamanı, "resets
3am", "try again at 3:09 AM", "try again in 12 minutes"); çıkaramazsa ya da saat dilimi uyuşmazsa `--bekle` dakika
aralıkla dener. `bekleUntil` + `--pay-sn` saniye sonra **aynı oturumu** sürdürür (Claude `--resume <oturum>`, Codex
`exec resume <oturum>`). Arayüzde satır sarıdır: "Kota dolu — 03:09'da kendiliğinden devam edecek", altında geri sayım
("2 sa 14 dk kaldı" → "14 dk kaldı" → "Devam ediyor…"), düğmeler **Şimdi dene** ve **İptal**. `--deneme` hakkı biterse
son durum `kota` olur.

### EK §2 Uygulamaya bağlama

1. **Başlatma:** `runClaude` ve `runCodex` nöbetçiyi başlatır (§6.3); uygulama kapansa da iş sürer. Aynı anda en fazla
   2 çalışan Claude işi; `kota-bekliyor` olanlar sayılmaz.
2. **İzleme:** `JobStore` her işin `.jsonl` dosyasını kaldığı bayttan okur ve ayrıştırıcılara besler; durum kaynağı
   sırası `nobet.json` > ayrıştırıcı (§6.7).
3. **`kota-bekliyor` durumu:** EK §1.3'teki gösterim; eski son durum `kota` yalnızca deneme hakkı bitince (çıkış 5).
4. **Uygulama yeniden açılınca:** `nobet.json` içindeki süreç yaşıyorsa iş izlenmeye devam eder; `yarida` yalnızca süreç
   ölü **ve** durum bitmemişse (§6.6).
5. **Acil Durdur:** `DURDUR` nöbetçiyi de durdurur; `StopAll()` ayrıca her işin `.iptal` dosyasını oluşturur (§6.8).
6. **Tam Gaz işçisi:** zamanlanmış işçi de görevleri aynı nöbetçiyle (`--gorev`) çalıştırır; uygulama bu işleri de
   diskten bulup listeler.
7. **Masam ve Şirketim:** İş ver penceresinde birincil düğme "Claude'a ver — hemen başlasın"; "Sadece kopyala" yedek
   yoldur. Şirketim çalışan ve kota bekleyen iş sayısını ve en yakın devam saatini gösterir.
8. **Selftest:** sahte nöbetçiyle uçtan uca: başlat → kota → `kota-bekliyor` → `.simdi` → devam → bitti; uygulamayı
   kapatıp açma benzetimi. Gerçek Claude ya da Codex asla çağrılmaz.

### EK §2b İş varken bilgisayar uyumasın

Bir iş `basliyor`, `calisiyor` ya da `kota-bekliyor` iken ya da Tam Gaz açıkken uygulama
`SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED)` ile uykuyu engeller (ekran yine kapanabilir); iş
kalmayınca bırakır. Karar 30 saniyede bir yeniden verilir. Windows güç ayarları **değiştirilmez**. Ayarlar'da tek
anahtar: "İş varken bilgisayar uyumasın" (varsayılan açık); Şirketim'de "Bilgisayar uyanık tutuluyor: 2 iş sürüyor"
satırı. Sınır: bu isteği yalnızca açık uygulama tutar; uygulama kapalıyken bilgisayar uyursa iş durur.

### EK §2c Ekran boyutu

- Adımlar: %100, 110, 125, 140, 150, 175 (WebView2 `ZoomFactor`; WebView2'nin kendi yakınlaştırması kapalı).
- İlk açılış varsayılanı ekrana göre: ayar dosyasında boyut hiç seçilmemişse (`YaziBoyutuSecildi: false`) ana ekranın
  mantıksal çalışma alanı genişliği 2.200 ve üstündeyse %150, değilse %125. Kullanıcı bir kez seçince onun seçimi kalır.
- Ctrl + artı / Ctrl + eksi / Ctrl+0 (%100) ve Ctrl + fare tekerleği anında büyütür ya da küçültür; seçim kaydedilir,
  köşede kısa bir "Ekran boyutu: %125" bildirimi çıkar. Ayarlar'da "Ekran boyutu" düğme grubu.
- Düzen %175'te de taşmadan çalışır: dar menü (simge şeridi) ve tek sütun kartlar.

### EK §3 Beyin Evreni (yol haritası)

Harita sekmesindeki ECharts grafiğinin yerini alacak yeni nesil harita. Planlanan bağlantı: `evrenKur(kap, GraphData,
{tema, azHareket, ac})`; bir düğüme tıklamak notu Keşfet'te açar; tema değişince yeniden boyanır; sayfa kapanınca
kaynaklarını bırakır; `?shot=1` ve `html.no-anim` durumunda hareket azaltılır. CSP değişmez. O güne kadar mevcut
ECharts haritası okunaklı kalmalıdır.

### EK §4 Kabul ölçütleri

- Release derleme 0 hata, selftest tamamen geçer, ekran görüntüleri yenilenir ve her biri gözle kontrol edilir.
- "Claude'a ver" gerçek iş başlatır (deneme modunda sahte nöbetçiyle gösterilir); uygulama kapatılıp açılınca iş kaybolmaz.
- Kota senaryosu sahte nöbetçiyle ekranda görünür: sarı "Kota dolu — …'de devam edecek" satırı, "Şimdi dene" ve
  "İptal" çalışır.
