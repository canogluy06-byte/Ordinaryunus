// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Security.Cryptography;
using System.Text.Json;

namespace Ordinaryunus.Data;

/// <summary>Kasadaki bir betik, kontrol edilmemiş bir değişiklik taşıdığı için çalıştırılmadığında fırlatılır.
/// Mesajı doğrudan kullanıcıya gösterilir.</summary>
public sealed class ScriptNotTrustedException(string message) : InvalidOperationException(message);

/// <summary>Kasadaki bir betiğin git'e göre durumu.</summary>
public enum ScriptStatus
{
    /// <summary>Git deposunda, commit edilmiş hâliyle aynı.</summary>
    Clean,
    /// <summary>Git deposunda ama commit edilmemiş bir değişiklik var.</summary>
    Modified,
    /// <summary>Git deposunda ama hiç commit edilmemiş (izlenmiyor).</summary>
    Untracked,
    /// <summary>Git yok, kasa bir git deposu değil ya da git cevap vermedi: git'e göre karar verilemiyor.</summary>
    Unknown,
}

/// <summary>
/// bu uygulamadan başlatılan Claude/Codex işleri kasaya yazabilir
/// ama kabuk (shell) kullanamaz. Oysa uygulamanın kendisi kasadaki bazı betikleri (iş nöbetçisi, Kestirmeler bakım
/// betikleri) kullanıcının kendi tam yetkisiyle, korumasız çalıştırır. Bir iste gizlenmiş kötü bir talimat, bu
/// betiklerden birini bir sonraki "Claude'a ver"/"Rolleri eşitle" tıklamasından hemen önce değiştirip incelenmemiş
/// sürümün çalışmasını sağlayabilirdi. Bu sınıf ek bir güvenlik katmanıdır (asıl düzeltme değil):
/// <list type="bullet">
/// <item>Kasa bir git deposuysa: git'in "değişmiş" ya da "izlenmiyor" gördüğü betik çalıştırılmaz. Çare: değişikliği
/// gözden geçirip commit etmek.</item>
/// <item>Kasa git deposu değilse (ya da git kurulu değilse): iş tamamen kilitlenmez. Betiğin SHA-256 özeti kasanın
/// DIŞINDA (%LOCALAPPDATA%\Ordinaryunus\guvenlik\betik-izleri.json) saklanır. İlk kullanımda o anki hâline güvenilir;
/// sonradan içerik değişirse çalıştırmadan önce yerel bir Windows onay kutusuyla kullanıcıya sorulur. Onay kutusu
/// olmayan modlarda (selftest, deneme, ekran görüntüsü) değişmiş betik çalıştırılmaz.</item>
/// </list>
/// </summary>
public static class VaultGitGuard
{
    /// <summary>Değişmiş bir betik için kullanıcıya sorulacak yerel onay kutusu (WebHostForm bağlar). Null ise
    /// (başsız modlar) değişmiş betik reddedilir. Sorulan metni alır, "evet" için true döner.</summary>
    public static Func<string, bool>? ConfirmChangedScript { get; set; }

    /// <summary>Betiğin git'e göre durumu. Git yoksa, kasa depo değilse ya da git hata/zaman aşımı verirse
    /// <see cref="ScriptStatus.Unknown"/> döner (asla fırlatmaz).</summary>
    public static ScriptStatus Check(string vault, string relPath)
    {
        try
        {
            var r = ProcessRunner.Run("git", ["-C", vault, "status", "--porcelain", "--", relPath], null, 8000);
            return Classify(r);
        }
        catch { return ScriptStatus.Unknown; }
    }

    /// <summary>"git status --porcelain -- &lt;dosya&gt;" sonucunu yorumlar (saf fonksiyon, selftest'te denenir).</summary>
    public static ScriptStatus Classify(ProcessResult r)
    {
        if (r.TimedOut || r.ExitCode != 0) return ScriptStatus.Unknown;
        string o = r.Output.Trim();
        if (o.Length == 0) return ScriptStatus.Clean;
        return o.StartsWith("??", StringComparison.Ordinal) ? ScriptStatus.Untracked : ScriptStatus.Modified;
    }

    /// <summary>Kasadaki <paramref name="relPath"/> betiği ("/" ayraçlı, kasaya göre) çalıştırılabilir mi; değilse
    /// kullanıcıya gösterilecek açıklamayla <see cref="ScriptNotTrustedException"/> fırlatır.</summary>
    public static void EnsureSafeToRun(string vault, string relPath) =>
        Enforce(vault, relPath, Check(vault, relPath), ConfirmChangedScript);

    /// <summary><see cref="EnsureSafeToRun"/>'ın asıl kuralı; git durumu dışarıdan verilir ki selftest git'e bağlı
    /// kalmadan deneyebilsin.</summary>
    public static void Enforce(string vault, string relPath, ScriptStatus status, Func<string, bool>? confirm)
    {
        string full = Path.Combine(vault, relPath.Replace('/', Path.DirectorySeparatorChar));
        switch (status)
        {
            case ScriptStatus.Modified:
                throw new ScriptNotTrustedException(
                    $"Kasadaki {relPath} dosyasında commit edilmemiş bir değişiklik var. Değişikliği gözden geçirip commit edene kadar çalıştırılmaz.");
            case ScriptStatus.Untracked:
                throw new ScriptNotTrustedException(
                    $"Kasadaki {relPath} dosyası git'e hiç eklenmemiş. Dosyayı gözden geçirip commit edene kadar çalıştırılmaz " +
                    "(kasanda: git add ve git commit).");
            case ScriptStatus.Clean:
                return;
        }

        // Git ile karar verilemedi: kasanın dışında saklanan özete göre "ilk kullanımda güven, değişince sor".
        if (!File.Exists(full)) return; // dosya yoksa çağıran zaten "bulunamadı" hatası verir
        string? hash = HashFile(full);
        if (hash is null) // var ama okunamıyor: neyin çalışacağı bilinmeden çalıştırma
            throw new ScriptNotTrustedException($"Kasadaki {relPath} betiği okunamadı; biraz sonra tekrar dene.");
        var known = LoadHashes();
        string key = KeyFor(full);
        if (!known.TryGetValue(key, out var old))
        {
            Remember(key, hash, known);
            return;
        }
        if (string.Equals(old, hash, StringComparison.OrdinalIgnoreCase)) return;

        string question = $"Kasadaki {relPath} betiği, bu uygulamanın onu son çalıştırmasından beri değişmiş.\n\n" +
                          "Bu değişikliği sen (ya da güvendiğin biri) yaptıysan \"Evet\" de. Emin değilsen \"Hayır\" de ve " +
                          "dosyayı açıp kontrol et: bir yapay zekâ işi betiği değiştirmiş olabilir.\n\nÇalıştırılsın mı?";
        bool ok = false;
        try { ok = confirm?.Invoke(question) ?? false; } catch { ok = false; }
        if (!ok)
            throw new ScriptNotTrustedException(
                $"Kasadaki {relPath} betiği son çalıştırmadan beri değişmiş ve onaylanmadı; çalıştırılmadı. " +
                "Değişikliği kontrol edip tekrar dene.");
        Remember(key, hash, known);
    }

    // ---------------- betik özetleri (kasanın dışında) ----------------

    /// <summary>%LOCALAPPDATA%\Ordinaryunus\guvenlik\betik-izleri.json (testlerde ORDINARYUNUS_DATA altına düşer).</summary>
    public static string HashesFile => Path.Combine(AppPaths.LocalDir, "guvenlik", "betik-izleri.json");

    static string KeyFor(string full)
    {
        try { return Path.GetFullPath(full).ToLowerInvariant(); }
        catch { return full.ToLowerInvariant(); }
    }

    static string? HashFile(string full)
    {
        try
        {
            if (!File.Exists(full)) return null;
            return Convert.ToHexString(SHA256.HashData(Md.ReadBytesShared(full))).ToLowerInvariant();
        }
        catch { return null; }
    }

    static Dictionary<string, string> LoadHashes()
    {
        try
        {
            string? text = Md.ReadText(HashesFile);
            if (text is null) return new(StringComparer.Ordinal);
            var d = JsonSerializer.Deserialize<Dictionary<string, string>>(text);
            return d is null ? new(StringComparer.Ordinal) : new(d, StringComparer.Ordinal);
        }
        catch { return new(StringComparer.Ordinal); }
    }

    /// <summary>Özeti kaydeder. Salt okunur modlarda (selftest, deneme, ekran görüntüsü) hiçbir şey yazmaz; o
    /// modlarda veri klasörü zaten geçici bir klasördür.</summary>
    static void Remember(string key, string hash, Dictionary<string, string> known)
    {
        if (VaultWriter.ReadOnlyMode) return;
        try
        {
            known[key] = hash;
            Directory.CreateDirectory(Path.GetDirectoryName(HashesFile)!);
            string tmp = HashesFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(known, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, HashesFile, overwrite: true);
        }
        catch { /* en iyi çaba: kaydedilemezse bir dahaki sefere yine "ilk kullanım" sayılır */ }
    }
}
