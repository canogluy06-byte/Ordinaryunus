# Kurulum: Ordinaryunus'u kendi kasanla kullanmak

Bu belge, Ordinaryunus'u uydurma `ornek-kasa` yerine **kendi notlarınla** kullanmak için gereken her şeyi adım adım
anlatır. Uygulamanın kendisini nasıl açacağın depo kökündeki `README.md` dosyasında ("Hızlı başlangıç").

Toplam süre: yaklaşık 30-45 dakika. Adımlar birbirinden bağımsız; sadece notlarını görmek istiyorsan 1. adım yeter.

| Adım | Ne kazandırır | Gerekli mi? |
|---|---|---|
| [1. Kendi kasanı bağla](#1-kendi-kasanı-bağla) | Masam, Beyin, Projeler, İş Başvuruları kendi notlarınla dolar. | Evet |
| [2. İş nöbetçisini kasana koy](#2-iş-nöbetçisini-kasana-koy) | "Claude'a iş ver" ve "Codex'e iş ver" düğmeleri çalışır. | Yapay zekâya iş vereceksen |
| [3. Claude Code kancalarını kur](#3-claude-code-kancalarını-kur) | Güvenlik kapısı (onay kartları, Acil Durdur) ve istek defteri çalışır. | Yapay zekâya iş vereceksen **mutlaka** |
| [4. Claude ve Codex'e bir kez giriş yap](#4-claude-ve-codexe-bir-kez-giriş-yap) | İşler senin aboneliğinle çalışır. | Yapay zekâya iş vereceksen |
| [5. Güvenlik uyarılarını oku](#5-güvenlik-uyarıları) | Neyi asla yapmaman gerektiğini öğrenirsin. | Evet |

Aşağıdaki örneklerde hazır sürümün zip dosyasını `C:\Araclar` klasörüne çıkardığını (zip'in içinden `Ordinaryunus`
klasörü çıkar, yani uygulama `C:\Araclar\Ordinaryunus` altındadır), kasanı da `Belgeler\IkinciBeyin` klasöründe
tutacağını varsayıyoruz. Depoyu klonladıysan `C:\Araclar\Ordinaryunus` yerine klonun yolunu yaz. Senin yolların farklıysa komutlardaki yolları değiştir.
Komutları **PowerShell**'e yapıştır (Başlat menüsüne "PowerShell" yaz ve aç).

---

## 1. Kendi kasanı bağla

### 1.1 Kasa nedir?

Kasa, içinde Markdown (`.md`) notları olan sıradan bir klasördür. Obsidian bu klasörü not uygulaması olarak açar,
Ordinaryunus da aynı klasörü okur. Uygulama belli klasör ve dosya adlarına bakar:

```text
IkinciBeyin/
├── AGENTS.md                 kasanın kuralları (ŞART: uygulama kasayı bununla tanır)
├── CLAUDE.md                 "kurallar AGENTS.md'de" diyen kısa yönlendirme
├── 00 Başla Buradan.md       giriş notu (isteğe bağlı)
├── 01 Şimdi.md               bugünün özeti; "## ✅ Yapılacaklarım" listesi Masam'da görünür
├── 04 İstek Defteri.md       istek defteri kancası yazar (elle düzenleme)
├── 10 Gelen Kutusu/          hızlı notlar
├── 20 Projeler/
│   └── Projem/
│       ├── Projem.md         proje kartı (klasör adıyla aynı ad)
│       ├── Kayıt.md          devir notları ve kararlar
│       └── Görevler/         G-001 ….md görev paketleri
├── 30 Park Yeri/             şimdilik yapılmayacak fikirler
├── 40 Alanlar/               İş Başvuruları.md, Gelir Defteri.md, LinkedIn Takvimi.md …
├── 50 Kaynaklar/
├── 60 Ekip/Roller/           yapay zekâ rolleri
├── 70 Günlük/                günlük notlar, Analiz/ altında akşam analizleri
├── 80 Oturum Arşivi/         (isteğe bağlı) oturum dökümleri; uygulama bu klasörü bilerek hiç okumaz ve göstermez
└── _sistem/                  araçlar ve uygulama dosyaları
```

Proje kartının alanları, devir notunun biçimi, kilit kuralı gibi bütün ayrıntılar örnek kasadaki
`ornek-kasa/AGENTS.md` dosyasında. Kendi kasana da aynı dosyayı koyacaksın.

### 1.2 Örnek kasayı şablon olarak kopyala

En kolay yol, hazır düzeni kopyalayıp içini kendine göre değiştirmek:

```powershell
Copy-Item -Recurse "C:\Araclar\Ordinaryunus\ornek-kasa" "$env:USERPROFILE\Documents\IkinciBeyin"
```

Sonra kopyada (orijinal `ornek-kasa`'ya dokunma) şunları yap:

1. **Koru:** `AGENTS.md`, `CLAUDE.md`, `_sistem\araclar\` klasörü, klasör adları ve başlıklar
   (`## ✅ Yapılacaklarım`, `## Bitti Tanımı`, `## Benden Beklenenler` …). Adı değişen başlığı uygulama bulamaz.
2. **Değiştir:** `20 Projeler\` altındaki örnek projeleri kendi projelerine çevir (klasör adı = kart adı).
   `01 Şimdi.md`, `60 Ekip\Roller\`, `40 Alanlar\` içindeki uydurma bilgileri kendi bilgilerinle değiştir.
   İstemediğin örnek notları ve klasörleri Dosya Gezgini'nden kendin sil.
3. **Boşalt:** Örnek kasadaki kayıtlar senin değil. Not Defteri ile açıp içini tamamen sil ve kaydet:
   - `_sistem\istek-defteri\istekler.jsonl` (boş dosya kalsın)
   - `_sistem\istek-defteri\durum.json` (boş dosya kalsın ya da dosyayı sil; kanca yenisini yazar)
   - `_sistem\guvenlik\bekleyenler.jsonl` (örnek onay isteği; boş dosya kalsın)
   - `04 İstek Defteri.md` (dosyayı silebilirsin; kanca ilk istekte yeniden yazar)

### 1.3 Obsidian'da aç (isteğe bağlı)

Obsidian'ı aç → **Open folder as vault** (Klasörü kasa olarak aç) → `Belgeler\IkinciBeyin` klasörünü seç.
Artık notlarını Obsidian'da yazarsın, Ordinaryunus da aynı dosyaları okur.

### 1.4 Ordinaryunus'ta kasanı seç

1. Ordinaryunus'u aç ve şifrenle gir.
2. **Ayarlar › Kasa › Klasör seç** ile `Belgeler\IkinciBeyin` klasörünü seç.
3. Windows bir onay kutusu açar ("Kasa klasörü şuna değiştirilsin mi?"). **Evet** de.

Klasörde `AGENTS.md` yoksa uygulama "Bu klasörde AGENTS.md yok; kasa klasörü olamaz." der ve kabul etmez.
Seçtiğin yol `%APPDATA%\Ordinaryunus\ayarlar.json` dosyasına kaydedilir; sonraki açılışlarda hep bu yol kullanılır.

### 1.5 Kasayı git deposu yap (önerilir)

[Git](https://git-scm.com) kuruluysa kasanı bir git deposu yapman iki şey kazandırır: Geçmiş sayfası ve grafikler dolar,
ayrıca uygulama kasadaki betiklerin gizlice değiştirilip değiştirilmediğini git'ten anlar.

Önce kasanın köküne `.gitignore` adında bir dosya oluştur ve içine şunu yaz (özel ve sık değişen kayıtlar git'e girmesin):

```text
.obsidian/workspace.json
.obsidian/workspace-mobile.json
.obsidian/cache
.trash/
04 İstek Defteri.md
_sistem/istek-defteri/
_sistem/guvenlik/
_sistem/tamgaz/
```

Sonra:

```powershell
cd "$env:USERPROFILE\Documents\IkinciBeyin"
git init
git add -A
git commit -m "Kasa kuruldu"
```

Yapay zekâların yaptığı işleri commit ederken mesajı `[claude] …` ya da `[codex] …` ile başlat; Geçmiş sayfası işi
kimin yaptığını buradan anlar. Ordinaryunus'tan verilen işler kendileri commit etmez, commit'i sen yaparsın.

> **Dikkat:** Kasa bir git deposuysa ve `_sistem\araclar\is-nobetcisi.mjs` commit edilmemiş (yeni ya da değişmiş)
> hâldeyse uygulama güvenlik için iş başlatmaz: "İş nöbetçisinde henüz kontrol edilmemiş (commit edilmemiş) bir
> değişiklik var". Çözüm: dosyaya bakıp değişikliği onaylıyorsan commit et.

---

## 2. İş nöbetçisini kasana koy

"Claude'a iş ver" dediğinde uygulama işi kendisi çalıştırmaz; kasandaki **iş nöbetçisini** ayrı bir süreç olarak
başlatır. Nöbetçi, kullanım hakkın (kota) dolarsa bekler ve hak açılınca aynı oturumu kaldığı yerden sürdürür.

### 2.1 Node.js kur

Nöbetçi ve kancalar [Node.js](https://nodejs.org) ile çalışır. **LTS** sürümünü indir ve kur (kurulumda
"Add to PATH" seçili kalsın). Sonra PowerShell'i kapatıp yeniden aç ve dene:

```powershell
node -v
```

`v20` ya da daha yeni bir sürüm yazmalı. "node tanınmıyor" dersen bilgisayarı yeniden başlat ya da kurulumu tekrarla.

### 2.2 Dosyaları kasana koy

Kasanın `_sistem\araclar\` klasöründe şu dosyalar olmalı:

| Dosya | Ne işe yarar |
|---|---|
| `is-nobetcisi.mjs` | İş nöbetçisi (uygulama bunu çalıştırır). |
| `codex.mjs` | Codex köprüsü: en yeni `codex.exe`'yi bulup çalıştırır. Codex işleri bunun üzerinden başlar. |
| `is-nobetcisi-sahte.mjs` | Gerçek kota harcamayan sahte Claude/Codex. Deneme modu (`--dry-run`) ve testler kullanır. |
| `is-nobetcisi-test.mjs` | Nöbetçinin testleri. |

1.2'deki gibi örnek kasayı kopyaladıysan bunlar zaten yerinde. Kendi kasan başka bir yerdeyse klasörü kopyala:

```powershell
New-Item -ItemType Directory -Force "$env:USERPROFILE\Documents\IkinciBeyin\_sistem\araclar"
Copy-Item "C:\Araclar\Ordinaryunus\ornek-kasa\_sistem\araclar\*.mjs" "$env:USERPROFILE\Documents\IkinciBeyin\_sistem\araclar\"
```

### 2.3 Dene

```powershell
cd "$env:USERPROFILE\Documents\IkinciBeyin"
node _sistem\araclar\is-nobetcisi-test.mjs
```

Bir iki dakika sürer, gerçek kota harcamaz ve en sonda `hepsi geçti (89)` gibi bir satır yazmalı
(sayı, testler eklendikçe değişebilir). `KALDI` yazan satır varsa o satırı olduğu gibi bir hata kaydına (issue) ekle. Kasan git deposuysa şimdi
commit et (1.5'teki uyarı).

İşlerin kayıtları kasada değil, `%APPDATA%\Ordinaryunus\claude-isler\` ve `...\codex-isler\` klasörlerinde durur.

---

## 3. Claude Code kancalarını kur

Kanca (hook), Claude Code'un belli anlarda kendiliğinden çalıştırdığı küçük bir programdır. Depodaki iki kanca:

| Dosya | Olay | Ne yapar |
|---|---|---|
| `guvenlik-kapisi.mjs` | `PreToolUse` (yapay zekâ bir araç kullanmadan hemen önce) | Silme, geri dönüşü zor git işlemi, program kurma, kayıt defteri değişikliği gibi kritik işlemleri durdurur ve Masam'a onay kartı olarak düşürür. Acil Durdur açıksa okuma dışındaki her şeyi reddeder. Onay dosyalarına, kasadaki onay kuyruğuna (`_sistem\guvenlik\`) ve Tam Gaz anahtarına yapay zekânın yazmasını engeller. |
| `istek-defteri.mjs` | `UserPromptSubmit` (sen bir mesaj gönderdiğinde) | Claude'a ve Codex'e yazdıklarını kasadaki istek defterine kaydeder (şifre, anahtar, kart numarası gibi bilgileri gizleyerek) ve her birine öbürüne ne dediğini gösterir. |

Uygulamadan verilen işlerde de bu kancalar çalışır; güvenlik kapısı olmadan yapay zekâya iş verme.

### 3.1 Dosyaları kopyala

Klasör adı **tam olarak** `ikinci-beyin` olmalı: Ordinaryunus "Güvenlik kapısı" ışığını yakarken ve Kestirmeler'deki
"İstek defterini tara" düğmesinde kancaları bu klasörde arar.

```powershell
$hedef = "$env:USERPROFILE\.claude\hooks\ikinci-beyin"
New-Item -ItemType Directory -Force $hedef
Copy-Item "C:\Araclar\Ordinaryunus\kasa-araclari\claude-kancalari\*.mjs" $hedef
Get-ChildItem $hedef
```

`guvenlik-kapisi.mjs` ve `istek-defteri.mjs` listelenmeli. Kapının kendi testini çalıştır:

```powershell
node "$env:USERPROFILE\.claude\hooks\ikinci-beyin\guvenlik-kapisi.mjs" test
```

En sonda `hepsi geçti` yazmalı.

### 3.2 Kancalara kasanın yerini söyle

Kancalar kasayı şu sırayla arar:

1. `IKINCI_BEYIN_KASA` ortam değişkeni,
2. Claude'un o an çalıştığı klasörden yukarı doğru, içinde `01 Şimdi.md` olan ilk klasör,
3. son çare `%USERPROFILE%\Documents\IkinciBeyin`.

Claude'u bazen kasanın dışındaki proje klasörlerinde de çalıştıracaksan ortam değişkenini mutlaka ayarla. Yoksa Acil
Durdur o klasörlerde görülmeyebilir ve onay istekleri uygulamanın okumadığı bir yere yazılabilir:

```powershell
setx IKINCI_BEYIN_KASA "$env:USERPROFILE\Documents\IkinciBeyin"
```

Bu yol, Ordinaryunus Ayarlar'da seçtiğin kasayla **aynı** olmalı. `setx` yeni açılan programlarda geçerli olur:
Claude'u, Codex'i ve PowerShell'i kapatıp yeniden aç.

Ortam değişkeni yerine Claude Code ayarlarına da yazabilirsin: `%USERPROFILE%\.claude\settings.json` içindeki
`"env"` bölümüne `"IKINCI_BEYIN_KASA": "C:/Users/KULLANICI_ADIN/Documents/IkinciBeyin"` satırı (3.3'teki gibi düz
bölüyle). Ordinaryunus önce bu satıra, sonra ortam değişkenine bakar.

Ordinaryunus'taki **Güvenlik kapısı** ışığı bu değişkeni denetler: kapı kurulu ama değişken yoksa ya da başka bir
klasörü gösteriyorsa ışık **sarı** yanar (Beyin › Sorunlar'da da "Güvenlik kapısı bu kasayı kesin bilmiyor" görünür).
Uygulamadan verilen işler bundan etkilenmez: iş nöbetçisi her işe bu değişkeni kendisi, işin kasasıyla verir.

(Uygulamanın kendisi için ayrı bir değişken var: `ORDINARYUNUS_KASA`. Ayarlar'dan kasa seçtiysen gerekmez.)

### 3.3 Kancaları Claude Code'a tanıt (settings.json)

Claude Code ayarları `%USERPROFILE%\.claude\settings.json` dosyasındadır. Önce kanca klasörünün yolunu, JSON'a
uygun biçimde (ters bölü yerine düz bölü) öğren:

```powershell
"$env:USERPROFILE\.claude\hooks\ikinci-beyin" -replace '\\', '/'
```

Çıkan yol örneğin `C:/Users/ayse/.claude/hooks/ikinci-beyin` olur. Aşağıdaki parçada `KULLANICI_ADIN` yazan yerlere
kendi kullanıcı adını koy:

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "node \"C:/Users/KULLANICI_ADIN/.claude/hooks/ikinci-beyin/guvenlik-kapisi.mjs\" claude",
            "timeout": 5
          }
        ]
      }
    ],
    "UserPromptSubmit": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "node \"C:/Users/KULLANICI_ADIN/.claude/hooks/ikinci-beyin/istek-defteri.mjs\" claude",
            "timeout": 10
          }
        ]
      }
    ]
  }
}
```

Nasıl eklenir:

- **`settings.json` yoksa:** Not Defteri'ni aç, parçanın tamamını yapıştır, `%USERPROFILE%\.claude\settings.json`
  olarak kaydet (Kayıt türü: "Tüm dosyalar", Kodlama: "UTF-8").
- **`settings.json` varsa ve içinde `"hooks"` yoksa:** dosyanın en dıştaki `{` işaretinden hemen sonra
  `"hooks": { … },` bölümünü ekle (sondaki virgülü unutma).
- **`"hooks"` zaten varsa:** içine sadece `"PreToolUse": [ … ]` ve `"UserPromptSubmit": [ … ]` girdilerini ekle.
  Bu olaylar zaten varsa, listelerine `{ "hooks": [ … ] }` nesnesini virgülle ayırarak ekle.

Dikkat edilecekler:
- `PreToolUse` girdisine `"matcher"` **yazma**. Kapı her aracı görmeli; yoksa Acil Durdur bazı araçları kaçırır.
- Komutun sonundaki `claude` kelimesi gerekli: kancaya Claude'dan çağrıldığını söyler.

Dosyanın bozulmadığını (geçerli JSON olduğunu) kontrol et:

```powershell
node -e "JSON.parse(require('fs').readFileSync(process.env.USERPROFILE + '/.claude/settings.json', 'utf8')); console.log('settings.json geçerli')"
```

Claude Code'u yeniden başlat. İçinde `/hooks` yazarak iki kancanın göründüğünü kontrol edebilirsin. Ordinaryunus'ta
üst çubuktaki **Güvenlik kapısı** ışığı yeşile döner (uygulama kanca dosyasının yerinde olduğuna ve `settings.json`
içinde `guvenlik-kapisi` geçtiğine bakar).

### 3.4 Kapıyı dene

1. Kasana `deneme.md` adında boş bir not koy.
2. Claude Code'u kasa klasöründe aç ve "deneme.md dosyasını sil" yaz.
3. Claude'un silme komutu reddedilmeli ve Ordinaryunus'ta Masam'da bir onay kartı çıkmalı.
4. **Reddet**'e bas. Sonra `deneme.md`'yi kendin sil.

### 3.5 Codex için de kur (isteğe bağlı)

Codex sürümün kancaları (hooks) destekliyorsa aynı iki kancayı Codex'e de bağlayabilirsin. `%USERPROFILE%\.codex\hooks.json`
dosyası (yoksa oluştur):

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "node \"C:/Users/KULLANICI_ADIN/.claude/hooks/ikinci-beyin/guvenlik-kapisi.mjs\" codex",
            "timeout": 5
          }
        ]
      }
    ],
    "UserPromptSubmit": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "node \"C:/Users/KULLANICI_ADIN/.claude/hooks/ikinci-beyin/istek-defteri.mjs\" codex",
            "timeout": 10
          }
        ]
      }
    ]
  }
}
```

Buradaki fark sadece sondaki `codex` kelimesi. Codex kanca desteklemiyorsa bu adımı atla; Codex işleri yine her zaman
`workspace-write` korumalı alanında çalışır.

Codex, dışarıdan eklenen kancaları ilk açılışta **güvenilir olarak onaylamanı** isteyebilir (onay, `config.toml`
dosyasına kaydedilir). Onaylamazsan kanca çalışmaz ve kapı Codex'te devre dışı kalır. Kurduktan sonra Codex'i bir kez
aç, kanca sorusu gelirse onayla. Bu davranış Codex sürümüne göre değişebilir; kapının çalıştığını 3.4'teki denemeyle
Codex'te de kontrol et.

### 3.6 /devir ve /haftalik becerileri (isteğe bağlı)

Kestirmeler sayfasındaki **"/devir" kopyala** ve **"/haftalik" kopyala** kutucukları ile Masam'daki **Günü kapat**
düğmesi, komutu panoya koyup Claude'u açar. Bu komutlar Claude Code'un kendisinde yoktur; birer **beceridir (skill)**
ve senin kurman gerekir. Kurulu değilse kutucuk soluk görünür ve tıklayınca bu bölümü gösterir.

Depoda iki örnek var: `kasa-araclari\claude-becerileri\devir\SKILL.md` (oturum sonunda devir notunu kasaya yazar) ve
`...\haftalik\SKILL.md` (haftalık gözden geçirme). Kullanıcı becerisi olarak kur:

```powershell
$hedef = "$env:USERPROFILE\.claude\skills"
New-Item -ItemType Directory -Force $hedef
Copy-Item -Recurse "C:\Araclar\Ordinaryunus\kasa-araclari\claude-becerileri\*" $hedef
Get-ChildItem $hedef
```

Aynı adda (`devir`, `haftalik`) bir becerin zaten varsa önce onu yedekle; komut üzerine yazar. İstersen becerileri
yalnızca kasana da koyabilirsin: `<kasa>\.claude\skills\devir\SKILL.md` (Claude kasa klasöründe açıldığında görünür).
Ordinaryunus iki yere de bakar. Becerilerin içi düz Türkçe talimattır; kendi düzenine göre değiştir.

Claude Code'u yeniden başlat ve `/devir` yazarak dene.

### 3.7 Zamanlanmış görevlerin takvimi (isteğe bağlı)

Şirketim ve Ayarlar sayfaları `%USERPROFILE%\.claude\scheduled-tasks\<ad>\SKILL.md` altındaki zamanlanmış görevleri
listeler. Bir görevin "sonraki çalışma" zamanını görmek için o dosyanın başındaki ön bilgiye bir `zaman:` satırı ekle:

```yaml
---
name: aksam-analizi
description: Her akşam günün kısa analizini kasaya yazar
zaman: her gün 21:30
---
```

Anlaşılan biçimler: `her gün 21:30`, `pazar 19:00`, `cumartesi`, `her saat :14`. Satır yoksa görev listede görünür
ama takvimi boş kalır. Beyin › Genel bakış'taki "derin analiz" kartı, `70 Günlük/Analiz/` altındaki en yeni notu
gösterir; o notu yazan görev depoyla gelmez.

---

## 4. Claude ve Codex'e bir kez giriş yap

Uygulamadan verilen işler **senin aboneliğinle** çalışır. İş nöbetçisi `ANTHROPIC_API_KEY` gibi ortam değişkenlerini
işlere geçirmez; bu yüzden komut satırında bir kez giriş yapmış olman gerekir.

### Claude

Ordinaryunus ve iş nöbetçisi Claude Code komut satırını şu sırayla arar (ilk bulunan kullanılır):

1. **Claude masaüstü uygulamasının** kurduğu yer: `%APPDATA%\Claude\claude-code\<sürüm>\claude.exe` (en yeni sürüm),
2. Claude Code'un yerel kurucusunun koyduğu yer: `%USERPROFILE%\.local\bin\claude.exe`,
3. `PATH` içindeki ilk `claude.exe`.

Yalnızca `.exe` dosyası kabul edilir; npm'in kurduğu `claude.cmd` sarmalayıcısı kullanılmaz (kabuk olmadan
başlatılamaz). Hangisinin bulunduğunu Şirketim sayfasının başındaki "Claude komut satırı" satırında görürsün.
Kontrol etmek için:

```powershell
Get-ChildItem "$env:APPDATA\Claude\claude-code" -ErrorAction SilentlyContinue
Test-Path "$env:USERPROFILE\.local\bin\claude.exe"
(Get-Command claude.exe -ErrorAction SilentlyContinue).Source
```

Üçü de boşsa Claude masaüstü uygulamasını kurup içindeki Claude Code'u bir kez aç ya da Claude Code'u yerel kurucusuyla
kur.

Giriş yap. Masaüstü uygulamasının kurduğu sürümü kullanıyorsan bu iki satır en yenisini bulur ve açar (diğer iki
durumda doğrudan `claude` yazman yeter):

```powershell
$surum = Get-ChildItem "$env:APPDATA\Claude\claude-code" -Directory | Where-Object { $_.Name -match '^\d+(\.\d+)+$' } | Sort-Object { [version]$_.Name } | Select-Object -Last 1
& "$($surum.FullName)\claude.exe"
```

Açılan Claude'da `/login` yaz, tarayıcıda hesabınla onayla, sonra `/exit` ile çık. Bir iş "Giriş gerekli" durumuna
düşerse Şirketim'deki **Komutu kopyala** düğmesi aynı komutu panoya koyar.

### Codex

İki yoldan biri yeter:

- **Codex masaüstü uygulaması:** kur, aç ve hesabınla giriş yap. `codex.exe` kendiliğinden
  `%LOCALAPPDATA%\OpenAI\Codex\bin\<kod>\` altına gelir.
- **Codex komut satırı:** `npm i -g @openai/codex` ile kur, sonra `codex login` ile giriş yap.

Köprünün Codex'i bulduğunu kontrol et:

```powershell
cd "$env:USERPROFILE\Documents\IkinciBeyin"
node _sistem\araclar\codex.mjs --version
```

Bir sürüm numarası yazmalı. "Codex bulunamadı" derse kurulumu kontrol et.

---

## 5. Güvenlik uyarıları

### İzin atlama bayraklarını asla kullanma

Claude Code ve Codex'in "bana hiçbir şey sorma" anlamına gelen seçenekleri var:

| Araç | Asla kullanma |
|---|---|
| Claude Code | `--dangerously-skip-permissions`, `--permission-mode bypassPermissions` |
| Codex | `--sandbox danger-full-access` (kısaca `-s danger-full-access`), `--dangerously-bypass-approvals-and-sandbox` (kısaca `--yolo`) |

İş nöbetçisi bunlara ek olarak onay adımlarını azaltan `--full-auto` gibi bayrakları da kabul etmez.

Neden?
- Bu seçenekler izin sistemini ve Codex'in korumalı alanını kapatır. Yapay zekâ o andan sonra bilgisayarında her komutu
  sormadan çalıştırabilir: dosya silebilir, program kurabilir, bilgilerini internete gönderebilir.
- Yapay zekâ okuduğu bir web sayfasındaki ya da nottaki gizli bir talimata kanabilir (istem enjeksiyonu). İzin sistemi,
  böyle bir durumda senden onay istenen son kapıdır.
- Güvenlik kapısı tek başına her şeyi yakalayamaz: komut metnine bakar. Örneğin bir betiğin içinden yapılan silmeyi
  göremez. Bu yüzden izin sistemi, korumalı alan ve kapı **birlikte** çalışmalı. Uygulamadan verilen Claude işlerine bu
  nedenle kabuk (Bash) hiç açılmaz.
- İş nöbetçisi bu bayraklardan birini görürse işi hiç başlatmaz.

### Diğer kurallar

- `%USERPROFILE%\.claude\settings.json` içine `"Bash(*)"` gibi her komuta izin veren geniş kurallar ekleme.
- Onay kararları `%LOCALAPPDATA%\Ordinaryunus\guvenlik\` altında durur. Bu klasöre ya da kasadaki `_sistem\guvenlik\`
  ve `_sistem\tamgaz\` klasörlerine yapay zekânın yazmasına izin verme; kapı bunu zaten engeller, sen de kural ekleyip
  açma.
- Bir şey ters giderse üst çubuktaki **Acil Durdur**'a bas. Kapı okuma dışındaki her işlemi reddeder, çalışan işler
  durur. Sorun geçince **Devam Et**.
- Kasaya şifre, kimlik numarası, banka bilgisi yazma. İstek defteri bilinen gizli bilgi biçimlerini gizler ama her şeyi
  yakalayamaz.
- Kapı kendi içinde bir hata olursa Claude'u durdurmaz (işini engellemesin diye); hatayı kanca klasöründeki `hata.log`
  dosyasına yazar. Işık yeşil ama kapı tepki vermiyorsa önce bu dosyaya ve 3.1'deki teste bak.
- Kancaları güncellerken yeni dosyaları eskilerinin üzerine kopyala ve `guvenlik-kapisi.mjs test` ile yeniden dene.

---

## Sorun giderme

| Gördüğün | Ne yapmalı |
|---|---|
| "Bu klasörde AGENTS.md yok; kasa klasörü olamaz." | Kasanın köküne `AGENTS.md` koy (örnek kasadakini kopyala). |
| Güvenlik kapısı ışığı kırmızı | `%USERPROFILE%\.claude\hooks\ikinci-beyin\guvenlik-kapisi.mjs` yerinde mi, `settings.json` içinde `guvenlik-kapisi` geçiyor mu? (3.1, 3.3) |
| "Node.js (node.exe) bulunamadı" | Node.js'i kur, bilgisayarı yeniden başlat (2.1). |
| Güvenlik kapısı ışığı sarı | Kapı kurulu ama `IKINCI_BEYIN_KASA` yok ya da başka bir klasörü gösteriyor (3.2). |
| "Claude komut satırı bulunamadı" | Claude masaüstü uygulamasını kur ve Claude Code'u bir kez aç, ya da Claude Code'u yerel kurucusuyla kur (4). |
| İş "Giriş gerekli" durumunda | Şirketim › Komutu kopyala → PowerShell'e yapıştır → `/login` (4). |
| "İş nöbetçisinde henüz kontrol edilmemiş (commit edilmemiş) bir değişiklik var" | `_sistem\araclar` içindeki değişikliğe bak, onaylıyorsan commit et (1.5). |
| Onay kartı hiç gelmiyor | `IKINCI_BEYIN_KASA` ile Ayarlar'daki kasa aynı klasör mü? `setx`'ten sonra Claude'u yeniden açtın mı? (3.2) |
| Kestirmeler'de "/devir" ya da "/haftalik" kutucuğu soluk, "Günü kapat" hata veriyor | Beceriler kurulu değil (3.6). |
| Kestirmeler'de "Güvenlik kapısını dene" ya da "İstek defterini tara" "betik bulunamadı" diyor | Kancalar `%USERPROFILE%\.claude\hooks\ikinci-beyin\` altında değil (3.1). |
| Geçmiş sayfası boş, "Kasa bir git deposu değil" uyarısı | Kasayı git deposu yap (1.5). |
