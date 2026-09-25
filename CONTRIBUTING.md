# Katkı rehberi

Ordinaryunus'a katkı yapmak istediğin için teşekkürler! Hata düzeltmesi, yeni bir görselleştirme, belge iyileştirmesi,
her şey değerli. Aşağıdaki birkaç kural, katkının hızlıca birleştirilmesini sağlar.

## Başlamadan önce

- **Küçük düzeltme** (yazım hatası, açık bir hata): doğrudan çekme isteği (pull request) gönderebilirsin.
- **Yeni özellik ya da büyük değişiklik:** önce bir [konu (issue)](https://github.com/canogluy06-byte/Ordinaryunus/issues)
  aç ve fikrini kısaca anlat; mümkünse küçük bir ekran taslağı ekle. Böylece kod yazmadan önce nereye oturacağına
  birlikte karar veririz ve emeğin boşa gitmez.
- Harita ve görselleştirme fikirleri özellikle hoş geldin: yol haritasında **Beyin Evreni** (yeni nesil grafik
  haritası) var.

## Geliştirme ortamı

- Windows 10 ya da 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Node.js 20 ya da üstü (iş nöbetçisi ve güvenlik kapısı testleri için)
- Git

Derleme ve yayın (depo kökünden):

```powershell
dotnet publish Ordinaryunus\Ordinaryunus.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o yayin
```

Arayüzde çalışıyorsan C# derlemeden de ilerleyebilirsin: `wwwroot` klasörü yerel bir sunucuda sahte (mock) veriyle
açılır. Ayrıntısı README'nin "Geliştirici notları" bölümünde.

## Göndermeden önce testler

Kendi şifrene ve ayarlarına dokunmamak için testleri ayrı bir veri klasörüyle çalıştır:

```powershell
# 1) Örnek kasanın git'li bir kopyası (iş nöbetçisi testleri kasada git ister)
Copy-Item -Recurse ornek-kasa "$env:TEMP\ornek-kasa-test"
git -C "$env:TEMP\ornek-kasa-test" init -q
git -C "$env:TEMP\ornek-kasa-test" add -A
git -C "$env:TEMP\ornek-kasa-test" -c user.name=Test -c user.email=test@example.com commit -q -m ilk

# 2) Uygulamanın öz testi: sonunda "SONUÇ: TAMAM" yazmalı
$env:ORDINARYUNUS_DATA = "$env:TEMP\ordi-test-veri"
Start-Process .\yayin\Ordinaryunus.exe -ArgumentList '--selftest', "$env:TEMP\ornek-kasa-test" -Wait
Get-Content "$env:TEMP\ordinaryunus-selftest.txt" -Tail 3

# 3) İş nöbetçisi ve güvenlik kapısı testleri: "hepsi geçti" yazmalı
node ornek-kasa\_sistem\araclar\is-nobetcisi-test.mjs
node kasa-araclari\claude-kancalari\guvenlik-kapisi.mjs test
```

Arayüzü değiştirdiysen `--screenshots` ile önce ve sonra görüntülerini al, çekme isteğine ekle.

## Kurallar

1. **Kişisel veri yok.** Gerçek ad, dosya yolu, e-posta, şifre, jeton ya da gerçek not içeriği ekleme. Örneklerde
   `ornek-kasa`'daki uydurma projeleri kullan (Kişisel Blog, Hava Durumu Uygulaması…).
2. **Güvenlik zayıflatılmaz.** İzin atlama bayrakları (`--dangerously-skip-permissions`, `bypassPermissions`,
   `danger-full-access`) hiçbir yerde kullanılmaz. Onay kapısını devre dışı bırakan ya da yapay zekânın onay
   kararlarına yazmasına izin veren bir değişiklik kabul edilmez.
3. **Yazar atfı korunur.** [EK-SARTLAR.md](EK-SARTLAR.md) gereği giriş ekranındaki imza, Ayarlar › Hakkında kartı,
   `Imza.cs` / `imza.js` ve dosya başlarındaki telif satırları silinmez. Yeni bir kaynak dosya eklersen başına aynı
   iki satırlık başlığı koy:

   ```text
   // SPDX-License-Identifier: GPL-3.0-only
   // Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
   ```

4. **Dil Türkçe.** Arayüz metinleri ve belgeler Türkçe; kullanıcıya "sen" diye hitap edilir. Yeni kod yorumlarını da
   Türkçe yaz.
5. **Küçük ve odaklı çekme istekleri.** Bir istek, tek bir işi yapsın. Açıklamada ne değiştiğini ve nasıl test
   ettiğini yaz.

## Lisans

Katkı gönderdiğinde, katkının projenin geri kalanıyla aynı lisansla, yani [GPL-3.0](LICENSE) ve
[ek şartlar](EK-SARTLAR.md) altında dağıtılmasını kabul etmiş olursun. Katkıda bulunanlar sürüm notlarında anılır.
