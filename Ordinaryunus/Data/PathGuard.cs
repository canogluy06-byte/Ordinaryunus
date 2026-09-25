// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
namespace Ordinaryunus.Data;

/// <summary>
/// Strict "is this a real subfolder of that root" checks, used wherever a card-supplied or user-picked folder must
/// stay inside a safe boundary (Desktop for --add-dir / "proje klasörü", never the boundary itself).
/// Basit bir <c>string.StartsWith(root)</c> burada GÜVENLİ DEĞİL: "C:\Users\ornek\DesktopEvil\x" de metin olarak
/// "C:\Users\ornek\Desktop" ile başlar; sadece öneki paylaşan kardeş bir klasör yanlışlıkla geçerdi.
/// </summary>
public static class PathGuard
{
    /// <summary>True when <paramref name="full"/>, once fully resolved, is a real subfolder of
    /// <paramref name="root"/> — never the root itself and never a same-prefix sibling.</summary>
    public static bool IsStrictSubfolder(string full, string root)
    {
        string r, f;
        try
        {
            r = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            f = Path.TrimEndingDirectorySeparator(Path.GetFullPath(full));
        }
        catch (Exception) { return false; }
        if (string.Equals(f, r, StringComparison.OrdinalIgnoreCase)) return false;
        return f.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary><see cref="IsStrictSubfolder"/> ile aynı, ama <paramref name="exclude"/> ile aynı klasörü de reddeder
    /// (kasanın kendisi Masaüstü'nde dursa bile "--add-dir"/"proje klasörü" olarak verilmesin diye).</summary>
    public static bool IsStrictSubfolderExcluding(string full, string root, string exclude)
    {
        if (!IsStrictSubfolder(full, root)) return false;
        try
        {
            string f = Path.TrimEndingDirectorySeparator(Path.GetFullPath(full));
            string ex = Path.TrimEndingDirectorySeparator(Path.GetFullPath(exclude));
            return !string.Equals(f, ex, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception) { return false; }
    }
}
