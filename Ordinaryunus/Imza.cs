// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
namespace Ordinaryunus;

/// <summary>
/// Uygulamanın imzası: kimin yaptığı, sürüm ve lisans. Tek kaynak burasıdır; arayüzdeki karşılığı
/// <c>wwwroot/js/imza.js</c> dosyasıdır ve --selftest ikisinin aynı olduğunu denetler.
/// Lisansın ek şartları (EK-SARTLAR.md, GPL-3.0 madde 7) gereği bu dosyadaki yazar bilgisi silinemez ve
/// değiştirilemez; giriş ekranındaki ve Hakkında kartındaki yazar atfı da korunmalıdır.
/// </summary>
public static class Imza
{
    public const string Yapan = "Yunus Emre Canoğlu";
    public const int Yil = 2026;
    public const string Surum = "2.1.0";
    public const string GitHub = "https://github.com/canogluy06-byte/Ordinaryunus";
    public const string Lisans = "GPL-3.0";

    /// <summary>Tek satırlık imza: "Ordinaryunus 2.1.0 · © 2026 Yunus Emre Canoğlu · GPL-3.0 lisansı".</summary>
    public static string Satir => $"Ordinaryunus {Surum} · © {Yil} {Yapan} · {Lisans} lisansı";
}
