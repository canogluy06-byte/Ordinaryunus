// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Text.Json;
using System.Text.Json.Serialization;
using Ordinaryunus.Data;

namespace Ordinaryunus.Security;

/// <summary>Contents of %APPDATA%\Ordinaryunus\ayarlar.json. Only the PBKDF2 salt+hash are stored, never the password.</summary>
public sealed class AppSettings
{
    public string? SifreTuz { get; set; }
    public string? SifreOzet { get; set; }
    public int SifreIterasyon { get; set; } = PasswordHasher.Iterations;
    /// <summary>Kayıtlı kasa yolu her zaman önceliklidir; boşsa AppPaths.DefaultVault kullanılır.</summary>
    public string KasaYolu { get; set; } = AppPaths.DefaultVault;
    public int YaziBoyutu { get; set; } = 100;          // 100 / 110 / 125 / 140 / 150 / 175 % (D2c)
    /// <summary>D2c: kullanıcı (ya da setSettings) yazı boyutunu açıkça seçene kadar false; o zamana dek ilk açılış
    /// her zaman %100 yerine ekrana uygun bir varsayılanla başlar.</summary>
    public bool YaziBoyutuSecildi { get; set; }
    public int KilitDakika { get; set; } = 10;          // auto-lock idle minutes
    public decimal UsdKuru { get; set; } = 41m;         // TL per USD for the income gauge
    public string Tema { get; set; } = "acik";          // "acik" | "koyu"
    public bool Hareket { get; set; } = true;           // animations on/off
    /// <summary>C (EK-v2.1 §2b): "İş varken bilgisayar uyumasın" — default açık.</summary>
    public bool KeepAwakeAcik { get; set; } = true;
    public int HataliDeneme { get; set; }               // consecutive wrong passwords
    public int KilitSayisi { get; set; }                // how many lockouts so far (for doubling)
    public DateTime? BeklemeBitis { get; set; }         // UTC time until login is allowed again

    [JsonIgnore] public bool HasPassword => !string.IsNullOrEmpty(SifreTuz) && !string.IsNullOrEmpty(SifreOzet);
    [JsonIgnore] public double UiScale => Math.Clamp(YaziBoyutu, 100, 175) / 100.0;
}

public static class SettingsStore
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>When true (selftest/screenshots) nothing is ever written.</summary>
    public static bool ReadOnly { get; set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), Options);
                if (s is not null)
                {
                    if (string.IsNullOrWhiteSpace(s.KasaYolu)) s.KasaYolu = AppPaths.DefaultVault;
                    if (s.YaziBoyutu is not (100 or 110 or 125 or 140 or 150 or 175)) { s.YaziBoyutu = 100; s.YaziBoyutuSecildi = false; }
                    if (s.KilitDakika < 1) s.KilitDakika = 10;
                    if (s.UsdKuru <= 0) s.UsdKuru = 41m;
                    if (s.Tema is not ("acik" or "koyu")) s.Tema = "acik";
                    return s;
                }
            }
        }
        catch (Exception) { /* corrupt file: start with defaults, keep the old file untouched until next save */ }
        return new AppSettings();
    }

    public static void Save(AppSettings s)
    {
        if (ReadOnly) return;
        Directory.CreateDirectory(AppPaths.DataDir);
        string tmp = AppPaths.SettingsFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(s, Options));
        File.Move(tmp, AppPaths.SettingsFile, overwrite: true);
    }

    public static void SetPassword(AppSettings s, string password)
    {
        var (salt, hash) = PasswordHasher.Create(password);
        s.SifreTuz = Convert.ToBase64String(salt);
        s.SifreOzet = Convert.ToBase64String(hash);
        s.SifreIterasyon = PasswordHasher.Iterations;
        s.HataliDeneme = 0;
        s.KilitSayisi = 0;
        s.BeklemeBitis = null;
    }

    public static bool CheckPassword(AppSettings s, string password)
    {
        if (!s.HasPassword) return false;
        try
        {
            return PasswordHasher.Verify(password, Convert.FromBase64String(s.SifreTuz!), Convert.FromBase64String(s.SifreOzet!),
                s.SifreIterasyon > 0 ? s.SifreIterasyon : PasswordHasher.Iterations);
        }
        catch (FormatException) { return false; }
    }

    /// <summary>Registers a failed login; returns the wait (if a lockout starts now). 5 tries → 30 s, doubling.</summary>
    public static TimeSpan? RegisterFailure(AppSettings s)
    {
        s.HataliDeneme++;
        TimeSpan? wait = null;
        if (s.HataliDeneme >= 5)
        {
            wait = TimeSpan.FromSeconds(30 * Math.Pow(2, Math.Min(s.KilitSayisi, 10)));
            s.KilitSayisi++;
            s.HataliDeneme = 0;
            s.BeklemeBitis = DateTime.UtcNow + wait;
        }
        Save(s);
        return wait;
    }

    public static void RegisterSuccess(AppSettings s)
    {
        if (s.HataliDeneme == 0 && s.KilitSayisi == 0 && s.BeklemeBitis is null) return;
        s.HataliDeneme = 0;
        s.KilitSayisi = 0;
        s.BeklemeBitis = null;
        Save(s);
    }
}
