// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Security.Cryptography;
using System.Text;

namespace Ordinaryunus.Security;

/// <summary>PBKDF2-SHA256 password hashing (200 000 iterations, 16-byte salt, 32-byte key).</summary>
public static class PasswordHasher
{
    public const int Iterations = 200_000;
    public const int SaltSize = 16;
    public const int KeySize = 32;
    public const int MinLength = 6;

    public static (byte[] salt, byte[] hash) Create(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        return (salt, Derive(password, salt, Iterations));
    }

    public static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, KeySize);

    public static bool Verify(string password, byte[] salt, byte[] expected, int iterations)
    {
        if (salt.Length == 0 || expected.Length == 0) return false;
        byte[] actual = Derive(password, salt, iterations);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
