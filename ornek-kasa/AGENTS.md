---
tur: sistem
son_guncelleme: 2026-09-24
---
# AGENTS.md — bu kasanın kuralları

Bu dosya, bu kasada çalışan bütün yapay zekâlar (Claude, Codex ve diğerleri) için tek kural kaynağıdır.
Kasa bir Obsidian klasörüdür: her şey düz Markdown dosyasıdır. Ordinaryunus uygulaması bu dosyaları okuyup
Masam, Beyin, Şirketim, Geçmiş, İş Başvuruları, Projeler ve Kestirmeler sayfalarında gösterir. Bu yüzden aşağıdaki
klasör adları, alan adları ve başlıklar **aynen** kullanılmalıdır; değişirse uygulama o bilgiyi göremez.

## 1. Temel kurallar

1. Her şey Türkçe yazılır: notlar, devir notları, görev sonuçları, commit mesajları.
2. Silme yok, para harcama yok, yayınlama/gönderme yok. Bunlar gerekiyorsa "Benden Beklenenler"e yaz ve dur.
3. `_sistem/guvenlik/` ve `_sistem/tamgaz/` klasörlerine yazılmaz. Onayları ve Tam Gaz anahtarını sadece kasa
   sahibi, Ordinaryunus uygulamasından verir.
4. "Güvenlik kapısı" bir işlemi reddederse tekrar deneme, etrafından dolaşma. İstek numarasını (#…) devir notuna yaz.
5. `_sistem/guvenlik/DURDUR` dosyası varsa (acil durdurma) hiçbir iş yapma; kasa sahibi "Devam Et" diyene kadar bekle.
6. Aynı anda en fazla **3 aktif proje** olur. Sınır doluysa yeni proje açma; hangisinin bitirileceğini,
   bekletileceğini ya da dondurulacağını kasa sahibine sor.
7. Bir mesaj bir rol adıyla başlıyorsa (`Kod Gözden Geçirici: …`) önce `60 Ekip/Roller/` içindeki o rolün kartını
   oku ve o rol gibi çalış.
8. Ordinaryunus'tan verilen işler **commit etmez**. Commit'i kasa sahibi yapar; mesaj `[claude] …` ya da
   `[codex] …` ile başlar (Geçmiş sayfası işi kimin yaptığını buradan anlar).

## 2. Klasör düzeni

| Klasör / dosya | Ne için |
|---|---|
| `01 Şimdi.md` | Bugünün özeti. `## ✅ Yapılacaklarım` altındaki `- [ ]` maddeleri Masam'da görünür ve oradan işaretlenir. |
| `04 İstek Defteri.md` | Claude'a ve Codex'e verilen istekler. İstek defteri kancası otomatik yazar; elle düzenlenmez. |
| `10 Gelen Kutusu/` | Hızlı notlar. 3 günden uzun bekleyen not "işle ya da taşı" uyarısı verir. |
| `20 Projeler/<Proje>/<Proje>.md` | Proje kartı (klasör adı ile kart adı aynı olmalı). |
| `20 Projeler/<Proje>/Kayıt.md` | Projenin defteri: devir notları ve kararlar, en yenisi en üstte. |
| `20 Projeler/<Proje>/Görevler/` | Görev paketleri: `G-001 <kısa ad>.md`, `G-002 …` |
| `20 Projeler/Bitenler.md`, `_Envanter.md` | Biten projeler listesi ve bütün projelerin kısa tablosu. |
| `30 Park Yeri/` | Şimdilik yapılmayacak fikirler. 7 günden eski fikir "değerlendir" uyarısı verir. |
| `40 Alanlar/` | Süregelen konular. `İş Başvuruları.md` ve `Gelir Defteri.md` uygulamada ayrıca gösterilir. |
| `50 Kaynaklar/` | Bağlantılar, kitap notları, araştırmalar. |
| `60 Ekip/` | `Şirket Şeması.md` ve `Roller/` (yapay zekâ rolleri). |
| `70 Günlük/` | Günlük notlar (`YYYY-AA-GG.md`); `Analiz/` altında akşam analizleri. |
| `80 Oturum Arşivi/` | (İsteğe bağlı) yapay zekâ oturumlarının tam dökümleri. Ordinaryunus bu klasörü bilerek hiç okumaz, aramaz ve göstermez; oraya koyduğun notlar uygulamada görünmez. |
| `_sistem/` | Araçlar ve uygulama dosyaları (aşağıya bak). Elle karıştırılmaz. |

`_sistem/` içi:
- `araclar/is-nobetcisi.mjs`: Ordinaryunus'un "Claude'a ver / Codex'e ver" düğmeleri işi bununla başlatır.
  Kota dolarsa bekler, açılınca aynı oturumdan devam eder. `codex.mjs` Codex'i bulup çalıştırır.
- `guvenlik/`: onay kuyruğu (`bekleyenler.jsonl`) ve acil durdurma (`DURDUR`). Yapay zekâ buraya yazmaz.
- `istek-defteri/`: istek defteri kancasının kayıtları.
- `tamgaz/`: Tam Gaz anahtarı. Sadece uygulama yazar.

## 3. Proje kartı

Kartın başında YAML ön bilgisi (frontmatter) vardır. Uygulamanın okuduğu alanlar:

```yaml
---
tur: proje
durum: aktif            # aktif | beklemede | bitti | donduruldu
odak: true              # aktif projelerden SADECE biri true olur
bitti_tanimi: "Madde 1; Madde 2; Madde 3"
sonraki_adim: "Eğer bu akşam 30 dk ayırırsam → yorum bölümünü test ederim"
olum_kriteri: "Eğer 2026-10-31'e kadar 10 yazı yayınlanmazsa → tempo düşürülür"
karar_bekliyor: false   # true ise proje Masam'da "senin kararını bekliyor" olarak görünür
kilit: ""               # boş ya da "claude 2026-09-24 21:30"
baslangic: "2026-08-20"
son_guncelleme: "2026-09-24"
klasor: ""              # projenin diskteki klasörü (tam yol), yoksa boş
aliases: []
---
```

- `sonraki_adim` "Eğer … → …" biçimindedir; uygulama okun (→) sağındaki kısmı kısa adım olarak gösterir.
- `olum_kriteri` içinde mutlaka `YYYY-AA-GG` biçiminde bir tarih olsun. Uygulama geri sayımı bu tarihten yapar;
  tarih yoksa ya da geçtiyse uyarı verir.
- `son_guncelleme` her devirde bugünün tarihiyle güncellenir. Aktif bir proje 14 günden uzun güncellenmezse uyarı çıkar.

Kartın gövdesindeki bölümler (başlıklar aynen böyle, `## ` ile):

| Bölüm | Ne yazılır |
|---|---|
| `## Amaç` | Tek cümle: bu proje neden var. |
| `## Bitti Tanımı` | `- [ ]` / `- [x]` maddeleri. İlerleme yüzdesi buradan hesaplanır. |
| `## Engel (en zor parça)` | İlk paragraf okunur: en zor kısım tek cümleyle. |
| `## Şu An` | Durumun kısa özeti. |
| `## Son Devir (kaldığım yer)` | En son devir notu (biçimi aşağıda). |
| `## Benden Beklenenler` | Kasa sahibinin yapması gereken işler (aşağıya bak). |
| `## Çıktılar ve Kaynaklar` | Bağlantılar, dosyalar. |
| `## v2 fikirleri` | Sonraya kalan fikirler. |

### Benden Beklenenler

Bu bölüm, yapay zekânın **kasa sahibinden** beklediği işlerin listesidir: bir karar, bir onay, bir deneme,
bir seçim. Kasa sahibi kendi ağzından okur ("benden beklenen").
- Her madde `- [ ] …` biçiminde, kısa ve yapılabilir olsun: "Üç ikon setinden birini seç", "Formu telefondan dene".
- Aktif projelerdeki işaretsiz maddeler Masam'da "Senden beklenenler" olarak görünür. Kasa sahibi işi yapınca `- [x]` yapar ve madde kaybolur.
- Silme, para harcama, yayınlama gibi yapay zekânın yapmadığı her şey buraya yazılır.

## 4. Kilit kuralı

Aynı projede iki yapay zekâ aynı anda dosya değiştirmesin diye:
1. Bir projede dosya değiştirmeden önce kartın `kilit:` alanına bak.
2. Başka bir araç tutuyorsa (`kilit: codex 2026-09-24 21:30`) o projede dosya değiştirme; kasa sahibine söyle.
3. Boşsa kendin al: `kilit: claude 2026-09-24 21:30` (araç adı + boşluk + `YYYY-AA-GG SS:DD`).
4. İş bitince kilidi temizle: `kilit: ""`.

12 saatten eski bir kilit uygulamada "unutulmuş kilit" uyarısı verir.

## 5. Devir notu (iş bitince)

Her çalışmanın sonunda:
1. `Kayıt.md` dosyasının **en üstüne** (açıklama satırının altına) yeni kayıt ekle. Eski kayıtlar silinmez.
2. Aynı notu kartın `## Son Devir (kaldığım yer)` bölümüne koy (öncekinin yerine).
3. Kartta `son_guncelleme`yi bugüne çek, gerekiyorsa `sonraki_adim`ı güncelle, kilidi temizle.
4. Gerekiyorsa `01 Şimdi.md`yi güncelle.

Devir kaydının biçimi (uygulama bu başlığı ve `- Alan:` satırlarını okur):

```markdown
### 2026-09-24 21:30 — claude — Yorum formu eklendi
- Yapılan: Ne yapıldı, bir iki cümle.
- Çıktı: Hangi dosya ya da sonuç çıktı.
- Doğrulama: Nasıl kontrol edildi.
- Açık kalan: Bitmeyen ne var (yoksa "yok").
- Sıradaki somut adım: Bir sonraki oturumda ilk yapılacak iş.
- Benden beklenen: Kasa sahibinin yapması gereken (yoksa "yok").
```

Karar kaydının biçimi:

```markdown
### KARAR 2026-09-15 — Kapsam üç maddeye indirildi
- Karar: Ne kararlaştırıldı.
- Neden: Neden.
- Elenen seçenekler: Neler denenip bırakıldı.
- Kim verdi: Ben
```

"Açık kalan" alanı "yok" değilse uygulama bunu Beyin sayfasında "açık kalanlar" olarak gösterir.

## 6. Görev paketi

`20 Projeler/<Proje>/Görevler/G-001 <kısa ad>.md`:

```yaml
---
tur: gorev
kimlik: G-001
proje: "Kişisel Blog"
atanan: "claude"        # claude | codex | başka bir rol
tasima: "dogrudan"      # dogrudan | cli | elle
durum: hazir            # hazir → verildi → kontrol → tamam (ya da iptal)
olusturma: "2026-09-11"
son_tarih: ""           # varsa YYYY-AA-GG
maliyet: yok
sonuc: ""
---
```

Gövde bölümleri: `## 1. Amaç`, `## 2. Bağlam`, `## 3. Kaynaklar`, `## 4. Yapılacak iş`, `## 5. Çıktı biçimi`,
`## 6. Tamamlanma ölçütü`, `## 7. Sınırlar`, sonra `## 📥 Sonuç` ve `## ✅ Claude kontrolü`.
Görevi yapan, bitirince `durum: kontrol` yapar ve "📥 Sonuç" bölümünü doldurur; `tamam`ı kontrol eden verir.
2 günden uzun `verildi` ya da `kontrol` durumunda kalan görev uyarı verir.

## 7. Diğer dosyaların biçimi

- `40 Alanlar/İş Başvuruları.md`: her `## ` bölümünde şu sütunlarla bir tablo:
  `No | Firma | Pozisyon | Tür | Kanal | Şehir | Uygunluk | Eksik beceri | İlan linki | Belgeler | Durum | Not`.
  Durum: `onay bekliyor` (Masam'dan onaylanır), `onaylandı`, `başvuruldu`, `mülakat`, `olumsuz`.
- `40 Alanlar/Gelir Defteri.md`: `Tarih | Tutar TL | Açıklama` tablosu (tarih `YYYY-AA-GG`); ön bilgide
  isteğe bağlı `hedef_usd: 100` aylık hedef.
- `40 Alanlar/LinkedIn Takvimi.md`: `## Yayınlananlar` altında `No | Tarih | Konu` tablosu, `## Sıradakiler`
  altında her taslak bir `### başlık`. Taslaklar Masam'da onay bekleyen iş olarak sayılır.
- `60 Ekip/Roller/<Rol>.md`: ön bilgide `cagri`, `departman`, `yonetici`, `arac`, `model`, `ozet` alanları.
  Şirketim sayfası rolleri `departman`a göre gruplar, kartta `ozet`i gösterir.
- `70 Günlük/Analiz/YYYY-AA-GG … .md`: ön bilgide `tarih:`; en yenisi Beyin sayfasında "derin analiz" olarak görünür.
