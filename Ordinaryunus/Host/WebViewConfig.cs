// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
namespace Ordinaryunus.Host;

/// <summary>Virtual host, allowed navigation and CSP (MIMARI §2.3).</summary>
public static class WebViewConfig
{
    public const string Origin = "https://ordinaryunus.example";
    public const string StartUrl = Origin + "/index.html";

    public const string Csp =
        "default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
        "font-src 'self'; connect-src 'none'; frame-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'";

    /// <summary>https + our host + /index.html, query empty or exactly "shot=1". Everything else is cancelled
    /// (about:blank, file:, data:, other hosts/paths/queries) — the SPA uses #hash routing only.</summary>
    public static bool IsAllowedNavigation(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var u)) return false;
        if (u.Scheme != Uri.UriSchemeHttps) return false;
        if (!string.Equals(u.Host, "ordinaryunus.example", StringComparison.OrdinalIgnoreCase)) return false;
        if (u.AbsolutePath != "/index.html") return false;
        string q = u.Query.TrimStart('?');
        return q.Length == 0 || q == "shot=1";
    }

    public static bool IsOwnOrigin(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps &&
        string.Equals(u.Host, "ordinaryunus.example", StringComparison.OrdinalIgnoreCase);
}
