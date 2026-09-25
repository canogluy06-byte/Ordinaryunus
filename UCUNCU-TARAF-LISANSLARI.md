# Üçüncü taraf lisansları

Ordinaryunus'un kendi kodu [GPL-3.0](LICENSE) ve [ek şartları](EK-SARTLAR.md) ile dağıtılır. Uygulama iki açık
kaynak bileşen kullanır; ikisinin de lisansı aşağıda ve ikisi de GPL-3.0 ile uyumludur (Apache License 2.0, GPL-3.0
ile uyumlu lisanslardandır; WebView2 paketi için EK-SARTLAR.md ayrıca açık bir birleştirme izni verir). Kaynak koddan
ya da hazır sürümden bir kopya dağıtıyorsan bu dosyayı da yanında tut.

## 1. Apache ECharts 5.6.0

- **Ne işe yarar:** Beyin, Geçmiş ve Şirketim sayfalarındaki grafikleri ve Harita sekmesindeki bağlantı grafiğini çizer.
- **Dosya:** `Ordinaryunus/wwwroot/lib/echarts.min.js` (npm `echarts@5.6.0` paketinin `dist/echarts.min.js` dosyası,
  değiştirilmeden). Kaynağı ve SHA-256 özeti `Ordinaryunus/wwwroot/lib/VERSIONS.txt` içinde; `--selftest` bu özeti
  denetler.
- **Lisans:** Apache License 2.0. Tam metin kütüphanenin yanında:
  [`Ordinaryunus/wwwroot/lib/LICENSE-Apache-2.0.txt`](Ordinaryunus/wwwroot/lib/LICENSE-Apache-2.0.txt) (hazır sürümde
  `yayin\wwwroot\lib\LICENSE-Apache-2.0.txt`; <https://www.apache.org/licenses/LICENSE-2.0.txt> ile bayt bayt aynı).
  Dosyanın başındaki lisans başlığı korunmuştur. Dosyanın içinde ECharts'ın kendi paketlediği `tslib` (Microsoft,
  0BSD) başlığı da olduğu gibi durur.
- **Proje sayfası:** <https://echarts.apache.org>
- **NOTICE:**

```text
Apache ECharts
Copyright 2017-2025 The Apache Software Foundation

This product includes software developed at
The Apache Software Foundation (https://www.apache.org/).
```

## 2. Microsoft.Web.WebView2 (NuGet paketi) 1.0.3179.45

- **Ne işe yarar:** Uygulamanın penceresinde arayüzü çizen Microsoft Edge WebView2 denetimini .NET'ten kullanmayı
  sağlar.
- **Nerede:** Kaynak kodda yalnızca paket başvurusu olarak durur (`Ordinaryunus/Ordinaryunus.csproj`); paket derleme
  sırasında NuGet'ten indirilir. Hazır sürümde (ikili dağıtım) paketin .NET kütüphaneleri `Ordinaryunus.exe` içine
  gömülüdür ve yanında `WebView2Loader.dll` dosyası bulunabilir.
- **Lisans:** Microsoft'un aşağıdaki lisansı (BSD tarzı). Paketin kendi `NOTICE.txt` dosyası NuGet paketinin
  içindedir.
- **Not:** WebView2 Runtime'ın kendisi (arayüzü çalıştıran Edge bileşeni) bu depoda ya da hazır sürümde dağıtılmaz;
  Windows ile gelir ya da Microsoft'tan ayrıca kurulur.

```text
Copyright (C) Microsoft Corporation. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

   * Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.
   * Redistributions in binary form must reproduce the above
copyright notice, this list of conditions and the following disclaimer
in the documentation and/or other materials provided with the
distribution.
   * The name of Microsoft Corporation, or the names of its contributors 
may not be used to endorse or promote products derived from this
software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## Kullanılan ama dağıtılmayan araçlar

Şunlar Ordinaryunus'un içinde yer almaz; kullanıcının kendi bilgisayarında kurulu olur ve kendi lisanslarına tabidir:
.NET 10 Çalışma Zamanı, Microsoft Edge WebView2 Runtime, Node.js, Git, Obsidian, Claude Code ve Codex.
