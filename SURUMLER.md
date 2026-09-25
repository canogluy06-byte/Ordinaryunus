# Sürümler

## 2.1.0 (2026-09-25) — ilk açık kaynak sürüm

Ordinaryunus'un herkesin kendi kasasıyla kullanabileceği ilk yayını.

- **Açık kaynak:** GPL-3.0 + ek şartlar (yazar atfı, logo ve ad korunur; bkz. `EK-SARTLAR.md`); kişisel veriler
  çıkarıldı, yerine uydurma projelerle dolu `ornek-kasa` kondu.
- **Kasa bulma:** Ayarlar'da kayıtlı kasa → `ORDINARYUNUS_KASA` ortam değişkeni → exe'nin yakınındaki `ornek-kasa` →
  `%USERPROFILE%\Documents\IkinciBeyin`. İlk açılış hiçbir ayar yapmadan örnek kasayla çalışır.
- **Arayüz:** WinForms + WebView2 üzerinde yeniden yazılmış tek ekranlı arayüz; Masam, Beyin (Genel bakış, Keşfet,
  Harita, Sorunlar), Şirketim, Geçmiş, İş Başvuruları, Projeler, Kestirmeler, Ayarlar. Açık ve koyu tema.
- **Beyin:** kasanın tamamını dizinler; güvenli Markdown okuyucu, bağlantı haritası, 18 kurallı sorun dedektörü,
  proje sağlık puanı, kural tabanlı durum özeti ve grafikler.
- **Gerçek iş verme:** "Claude'a ver" ve "Codex'e ver" işi iş nöbetçisiyle ayrı bir süreçte başlatır. Kullanım limiti
  dolunca bekler, açılınca aynı oturumu sürdürür ("Şimdi dene", "İptal"). Uygulama kapansa da iş devam eder.
- **Dar yetkiler:** Claude işlerinde kabuk ve MCP kapalı, internetten sayfa okuma yalnızca istenirse açık; Codex
  `workspace-write` korumalı alanında. İzin atlama bayrakları hiçbir yerde kullanılmaz.
- **Güvenlik:** onay kartları (kritikte ek onay, kısaltılmış komutta onay kapalı), kasanın dışında tutulan onay
  kararları, uygulamanın kendi verdiği onaylarla karşılaştırma, Acil Durdur.
- **Rahatlık:** ekran boyutu %100 ile %175 arası (Ctrl + artı / eksi / 0, Ctrl + fare tekerleği), ilk açılışta ekrana
  göre varsayılan; "İş varken bilgisayar uyumasın" ayarı; boşta otomatik kilit.
- **İmza:** Hakkında kartı, giriş ekranı ve exe bilgisinde yapan, sürüm ve lisans; `--surum` komutu.
- **Kurulum kolaylığı:** Claude Code komut satırı masaüstü uygulamasının yanında, yerel kurucunun yerinde ve `PATH`'te
  aranır; `/devir` ve `/haftalik` için örnek beceriler (`kasa-araclari/claude-becerileri`); kurulu olmayan beceri
  kestirmesi soluk görünür. "Güvenlik kapısı" ışığı, kancalara kasa yolu verilmemişse sarı yanar.
- **Geliştirici modları:** `--selftest`, `--screenshots`, `--dump-json`, `--dry-run`.
