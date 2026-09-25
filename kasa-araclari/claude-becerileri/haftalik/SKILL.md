---
name: haftalik
description: Kasanın haftalık gözden geçirmesini yapar; gelen kutusu, aktif ve bekleyen projeler, Park Yeri, bitenler ve gelecek hafta için öneriler hazırlar, kararları kullanıcıya sorar ve onaylananları uygular. Kullanıcı "/haftalik" yazınca ya da "haftalık gözden geçirme", "haftayı kapatalım" deyince kullan.
---
# Haftalık gözden geçirme (yaklaşık 30 dakika)

Bu, Ordinaryunus deposundaki örnek beceridir (`kasa-araclari/claude-becerileri/haftalik`). Kendi düzenine göre
değiştirebilirsin.

**Kasa:** bu oturumun çalıştığı klasörden yukarı doğru, içinde `AGENTS.md` ve `01 Şimdi.md` olan ilk klasör. Bulamazsan
`IKINCI_BEYIN_KASA` ortam değişkenindeki klasöre bak; o da yoksa kullanıcıya sor.
**Kurallar:** kasadaki `AGENTS.md`. Türkçe konuş ve yaz. Karar kullanıcının; sen önerirsin.

## 1. Topla (sessizce)

- `01 Şimdi.md`, `20 Projeler/` altındaki bütün kartların ön bilgisi, `10 Gelen Kutusu/` listesi, `30 Park Yeri/`
  notları, `20 Projeler/Bitenler.md`, varsa geçen haftanın gözden geçirme notu.
- Kanıt: kasa git deposuysa `git log --since="7 days ago" --oneline`.

## 2. Taslağı yaz

`70 Günlük/YYYY-Www Haftalık.md` (örneğin `2026-W40 Haftalık.md`) dosyasını oluştur ve **öneri olarak** doldur:

- Gelen kutusundaki her not için bir öneri: hangi projeye, Park Yeri'ne, kaynaklara ya da arşive.
- Aktif projeler: bu haftanın ilerleme kanıtı (dosya, commit, ekran görüntüsü); `sonraki_adim` "Eğer … → …"
  biçiminde mi; `olum_kriteri` tarihi geldi mi.
- 14 günden uzun güncellenmemiş projeler.
- Park Yeri: 7 günden eski fikirler için öneri (projeye al, beklet, bırak). Aktif proje sınırına uy.
- Bitenler ve gelecek hafta: odak proje, tek bir teslim, en fazla 3 adım.
- Sistem kontrolü: boş `sonraki_adim`, 12 saatten eski kilitler, `kontrol` durumunda bekleyen görevler, kırık
  `[[bağlantılar]]`.
- Kasada `40 Alanlar/LinkedIn Takvimi.md` varsa `## Sıradakiler` altındaki taslakları kısaca göster. Kullanıcının
  açıkça onaylamadığı hiçbir taslağı yayınlama ya da zamanlama.
- Kasada `40 Alanlar/İş Başvuruları.md` varsa durumu "onay bekliyor" olan satırları listele.

## 3. Sor ve uygula

- Kısa sorularla, bir seferde en fazla 4 soru sor.
- Projeye alınan fikir: notuna `karar: terfi` yaz; `20 Projeler/<Proje>/<Proje>.md` kartını `AGENTS.md`'deki
  biçimle oluştur. Bitti tanımı, "Eğer … → …" sonraki adım ve ölüm kriteri birlikte yazılır; üçü yoksa proje aktif olmaz.
- Bırakılan fikir: notuna `karar: birakildi` yaz. Notu silme ya da taşıma işini kullanıcıya bırak.
- Durum değişikliklerini kartlara, kararları ilgili `Kayıt.md` dosyasına yaz; `01 Şimdi.md`'yi güncelle.
- **Commit etme.** Kasa git deposuysa kullanıcıya yalnızca değiştirdiğin dosyalar için komut öner:
  `git add -- "<dosya>" …` ve `git commit -m "[claude] haftalık: YYYY-Www"`.

Suçlayıcı dil kullanma. Bir projeyi dondurmak da bitirmek kadar geçerli bir karardır. 30 dakikayı aşma.
