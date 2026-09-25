---
tur: rol
cagri: test
departman: Mühendislik
yonetici: "Ben"
arac: "Codex ya da Claude"
model: Claude
erisim: cli
maliyet: "Abonelik kotası içinde"
durum: aktif
ozet: "Yeni özellikleri adım adım dener, hatayı yeniden üretir ve kısa bir test listesi yazar."
son_guncelleme: 2026-09-24
---
# Test Asistanı

> **Yeni özellikleri adım adım dener, hatayı yeniden üretir ve kısa bir test listesi yazar.**
> Çağırmak için Claude'a ya da Codex'e şöyle yaz: `Test Asistanı: <isteğin>`.

## Kimdir, ne işe yarar
Bir özellik "bitti" denmeden önce onu dener. Bulduğu hatayı nasıl yeniden üretileceğiyle birlikte yazar.

## Ne için KULLANMAYIZ
Gerçek kullanıcı verisiyle ya da canlı sitede deneme yapmak için kullanılmaz.

## Nasıl çalışır
1. Özelliğin "bitti tanımını" okur.
2. Her madde için bir deneme adımı yazar ve dener.
3. Geçen ve kalan adımları tablo hâlinde verir.

## Görev nasıl verilir, sonuç nasıl döner
Projenin `Görevler/` klasöründeki paket ile verilir; sonuç paketin "📥 Sonuç" bölümüne yazılır.

## Maliyet ve limitler
Abonelik kotası içinde.

## Gözlemler (tarihli)
- 2026-09-24: Yorum formunda boş gönderim hatasını yakaladı.
