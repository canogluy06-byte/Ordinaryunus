// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
namespace Ordinaryunus.Data;

public static class AppPaths
{
    /// <summary>%APPDATA%\Ordinaryunus (overridable for tests via ORDINARYUNUS_DATA).</summary>
    public static string DataDir =>
        Environment.GetEnvironmentVariable("ORDINARYUNUS_DATA") is { Length: > 0 } d
            ? d
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ordinaryunus");

    public static string SettingsFile => Path.Combine(DataDir, "ayarlar.json");
    public static string JournalFile => Path.Combine(DataDir, "gunluk.json");
    public static string CodexJobsDir => Path.Combine(DataDir, "codex-isler");
    public static string ClaudeJobsDir => Path.Combine(DataDir, "claude-isler");
    public static string WebErrorLog => Path.Combine(DataDir, "hata-web.log");

    /// <summary>%LOCALAPPDATA%\Ordinaryunus, or DataDir\local when ORDINARYUNUS_DATA is set (tests never touch the real one).</summary>
    public static string LocalDir =>
        Environment.GetEnvironmentVariable("ORDINARYUNUS_DATA") is { Length: > 0 }
            ? Path.Combine(DataDir, "local")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ordinaryunus");

    public static string WebViewDataDir => Path.Combine(LocalDir, "WebView2");

    /// <summary>Test-only override (a temp placeholder page) so --screenshots can run before wwwroot has real content.</summary>
    public static string? WwwRootOverride { get; set; }

    /// <summary>The published wwwroot next to the exe (Content, CopyToOutputDirectory).</summary>
    public static string WwwRoot => WwwRootOverride ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");

    /// <summary>Profil klasörü ortam değişkeni: sadece testler ve ekran görüntüleri içindir (bkz. <see cref="UserProfile"/>).</summary>
    public const string ProfileEnvVar = "ORDINARYUNUS_PROFIL";

    /// <summary>
    /// Kullanıcı profil klasörü: .claude ve .codex (hook'lar, alt ajanlar, zamanlanmış görevler, oturumlar) buradan
    /// okunur. ORDINARYUNUS_PROFIL verilirse onun yerine o klasör kullanılır; böylece --screenshots ve testler
    /// makinedeki gerçek araç ayarlarını ve oturum başlıklarını göstermez, uydurma bir profille çalışır. (Windows
    /// USERPROFILE ortam değişkenini dikkate almadığı için ayrı bir değişken gerekir.)
    /// </summary>
    public static string UserProfile =>
        Environment.GetEnvironmentVariable(ProfileEnvVar) is { Length: > 0 } p && p.Trim().Trim('"') is { Length: > 0 } profil
            ? profil
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>Kasa yolu ortam değişkeni: ayarlarda kayıtlı bir kasa yoksa varsayılanı belirler.</summary>
    public const string VaultEnvVar = "ORDINARYUNUS_KASA";
    /// <summary>Depoyla gelen uydurma demo kasanın klasör adı.</summary>
    public const string DemoVaultName = "ornek-kasa";
    /// <summary>İş başvuru klasörü ortam değişkeni.</summary>
    public const string BasvuruEnvVar = "ORDINARYUNUS_BASVURU";

    /// <summary>
    /// Ayarlarda kayıtlı kasa yolu YOKSA kullanılacak varsayılan kasa (kayıtlı yol her zaman önceliklidir, bkz.
    /// SettingsStore.Load). Sıra: (1) ORDINARYUNUS_KASA ortam değişkeni; (2) exe'nin klasöründen başlayıp yukarı doğru
    /// en fazla 5 üst klasörde "ornek-kasa" klasörü (depodan derleyip çalıştıranlar demo kasayla açılsın: yayin\ için 1,
    /// bin\Release\net10.0-windows\ için 4, onun publish\ alt klasörü için 5 üst klasör gerekir); (3) son çare
    /// %USERPROFILE%\Documents\IkinciBeyin. Klasörün var olması şart değil: yoksa uygulama çökmez, "kasa bulunamadı"
    /// uyarısı gösterir ve kasa Ayarlar'dan seçilir.
    /// </summary>
    public static string DefaultVault => ResolveDefaultVault(Environment.GetEnvironmentVariable(VaultEnvVar), AppContext.BaseDirectory, UserProfile);

    /// <summary><see cref="DefaultVault"/>'un saf hâli (selftest'te denenir).</summary>
    public static string ResolveDefaultVault(string? envValue, string? startDir, string userProfile)
    {
        string env = (envValue ?? "").Trim().Trim('"');
        if (env.Length > 0) return env;
        string? demo = FindDemoVault(startDir, 5);
        if (demo is not null) return demo;
        return Path.Combine(userProfile, "Documents", "IkinciBeyin");
    }

    /// <summary><paramref name="startDir"/> ve en fazla <paramref name="maxUp"/> üst klasöründe "ornek-kasa" arar.</summary>
    public static string? FindDemoVault(string? startDir, int maxUp)
    {
        if (string.IsNullOrWhiteSpace(startDir)) return null;
        try
        {
            var dir = new DirectoryInfo(Path.GetFullPath(startDir));
            for (int i = 0; i <= maxUp && dir is not null; i++, dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, DemoVaultName);
                if (Directory.Exists(candidate)) return candidate;
            }
        }
        catch (Exception) { /* erişilemeyen bir üst klasör: aramayı bırak, son çareye düş */ }
        return null;
    }

    /// <summary>İş başvuru klasörü (Kestirmeler ve İş Başvuruları sayfası açar): ORDINARYUNUS_BASVURU ortam değişkeni,
    /// yoksa %USERPROFILE%\Documents\IsBasvuru. Klasör yoksa ilgili düğme "bulunamadı" der.</summary>
    public static string BasvuruKlasoru =>
        Environment.GetEnvironmentVariable(BasvuruEnvVar) is { Length: > 0 } b
            ? b.Trim().Trim('"')
            : Path.Combine(UserProfile, "Documents", "IsBasvuru");

    /// <summary>Başvuru belgelerinin (özgeçmiş, ön yazı çıktıları) durduğu alt klasör.</summary>
    public static string JobDocsDir => Path.Combine(BasvuruKlasoru, "CIKTI");

    /// <summary>Başvuru klasörü bulunamadığında gösterilen ipucu.</summary>
    public const string BasvuruIpucu = "Bu klasörü oluştur ya da ORDINARYUNUS_BASVURU ortam değişkeniyle başka bir klasör göster.";
}
