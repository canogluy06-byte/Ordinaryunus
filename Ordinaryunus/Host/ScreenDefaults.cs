// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
namespace Ordinaryunus.Host;

/// <summary>
/// D2c: yaygın bir dizüstü ekranı 1536x864 MANTIKSAL pikseldir (1920 px, Windows %125 ölçek, DPI 120) ve orada %100
/// yakınlaştırma her şeyi çok küçük gösteriyordu. İlk açılışta (henüz yazı boyutu seçilmemişken) her zaman %100 yerine
/// ekrana uygun bir değerle başlanır; kullanıcı kendisi bir değer seçince o seçim kalıcı olur
/// (AppSettings.YaziBoyutuSecildi).
/// </summary>
public static class ScreenDefaults
{
    /// <summary>≤1600 logical px → 125, ≥2200 → 150, in between → 125 (EK-v2.1 §2c, verbatim rule). Pure: takes the
    /// logical width as an int so it's unit-testable without a real screen.</summary>
    public static int DefaultUiScale(int logicalWorkAreaWidth) => logicalWorkAreaWidth >= 2200 ? 150 : 125;

    /// <summary>Birincil ekranın FİZİKSEL çalışma alanı genişliğini ve sistem DPI'ını okuyup mantıksal (DIP) genişliğe
    /// çevirir: ör. %125 ölçekte (DPI 120) 1920 px fiziksel genişlik 1536 mantıksal px'tir, yani kullanıcının gerçekte
    /// gördüğü genişlik. Henüz hiçbir Form yokken de çağrılabilir (masaüstü aygıt bağlamını kullanır).</summary>
    public static int CurrentLogicalWorkAreaWidth()
    {
        try
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen is null) return 1536; // ekran bulunamadı (bazı CI/RDP oturumları): yaygın dizüstü ekranını varsay
            int physicalWidth = screen.WorkingArea.Width;
            using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            double scale = g.DpiX / 96.0;
            return scale > 0 ? (int)Math.Round(physicalWidth / scale) : physicalWidth;
        }
        catch (Exception) { return 1536; }
    }
}
