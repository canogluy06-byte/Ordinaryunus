---
name: devir
description: Oturumun devir notunu ("kaldığım yer") kasaya yazar; proje kartını, Kayıt.md'yi ve 01 Şimdi.md'yi günceller. Kullanıcı "/devir" yazınca ya da "devir notu yaz", "bugünlük bu kadar", "kaldığım yeri yaz" deyince kullan.
argument-hint: "[proje adı]"
---
# Devir: kaldığım yeri yaz

Bu, Ordinaryunus deposundaki örnek beceridir (`kasa-araclari/claude-becerileri/devir`). Kendi düzenine göre
değiştirebilirsin.

**Kasa:** bu oturumun çalıştığı klasörden yukarı doğru, içinde `AGENTS.md` ve `01 Şimdi.md` olan ilk klasör. Bulamazsan
`IKINCI_BEYIN_KASA` ortam değişkenindeki klasöre bak; o da yoksa kullanıcıya kasanın yerini sor.
**Kurallar:** kasadaki `AGENTS.md` (özellikle "Kilit kuralı" ve "Devir notu" bölümleri). Her şeyi Türkçe yaz.
**Proje:** $ARGUMENTS (boşsa bu oturumda üzerinde çalışılan projeyi çıkar; emin değilsen sor).

1. **Kartı bul:** `20 Projeler/<Proje>/<Proje>.md`. Kart yoksa ve iş küçükse devir notunu yalnızca `01 Şimdi.md`'ye yaz.
2. **Kilit:** kartın `kilit:` alanını başka bir araç tutuyorsa o proje klasöründe hiçbir şey yazma; kullanıcıya söyle.
3. **Devir notunu hazırla** (`AGENTS.md`'deki biçimle): Yapılan, Çıktı (dosya yolları), Doğrulama, Açık kalan,
   Sıradaki somut adım, Benden beklenen. Bu oturumda alınan önemli kararları ayrıca `### KARAR` kaydı olarak yaz.
4. **Kayıt.md:** notu ve kararları dosyanın **en üstüne** (açıklama satırının altına) ekle; eski kayıtları silme.
   Dosya yoksa oluştur.
5. **Kart:** `## Son Devir (kaldığım yer)` bölümünü yeni notla değiştir; `son_guncelleme`yi bugüne çek; gerekiyorsa
   `sonraki_adim` ("Eğer … → …" biçiminde) ve `karar_bekliyor` alanlarını güncelle; kilidi temizle (`kilit: ""`).
6. **Görev:** bu oturumda bitirdiğin bir görev paketi varsa `durum: kontrol` yap ve "📥 Sonuç" bölümünü doldur.
   `tamam`ı kontrol eden verir.
7. **01 Şimdi.md:** ilgili satırları güncelle (proje durumu, sıradaki adım, kasa sahibinden beklenen).
8. Bir iş bittiyse `20 Projeler/Bitenler.md` dosyasının en üstüne bir satır ekle. Bitti tanımının bütün maddeleri
   işaretliyse `durum: bitti` yapmayı **öner**; kendin yapma.
9. **Commit etme.** Kasa git deposuysa kullanıcıya yalnızca değiştirdiğin dosyalar için komut öner:
   `git add -- "<dosya>" …` ve `git commit -m "[claude] devir: <proje> — <kısa açıklama>"`.
10. Kullanıcıya üç satırlık özet ver: ne kaydedildi, sıradaki adım, ondan beklenen.

Silme, para harcama, bir şey yayınlama ya da gönderme yok. Güvenlik kapısı bir işlemi reddederse tekrar deneme; istek
numarasını devir notuna yaz.
