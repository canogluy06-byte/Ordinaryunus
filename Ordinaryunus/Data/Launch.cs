// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Diagnostics;

namespace Ordinaryunus.Data;

/// <summary>Opens other apps, files, folders and links. Never passes user text through a shell.</summary>
public static class Launch
{
    public const string ClaudeAppId = @"shell:AppsFolder\Claude_pzs8sxrjxfjjc!Claude";
    public const string CodexAppId = @"shell:AppsFolder\OpenAI.Codex_2p2nqsd0c76g0!App";

    /// <summary>Set in screenshot/selftest mode: nothing is launched.</summary>
    public static bool Disabled { get; set; }

    public static string ObsidianUri(string vaultRoot, string relPath)
    {
        string vaultName = Path.GetFileName(vaultRoot.TrimEnd('\\', '/'));
        string rel = relPath.Replace('\\', '/');
        if (rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) rel = rel[..^3];
        return $"obsidian://open?vault={Uri.EscapeDataString(vaultName)}&file={Uri.EscapeDataString(rel)}";
    }

    public static void Obsidian(string vaultRoot, string relPath) => Open(ObsidianUri(vaultRoot, relPath));

    public static void Claude() => Explorer(ClaudeAppId);
    public static void Codex() => Explorer(CodexAppId);

    static void Explorer(string arg)
    {
        if (Disabled) return;
        var psi = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        psi.ArgumentList.Add(arg);
        Process.Start(psi)?.Dispose();
    }

    /// <summary>URLs, obsidian:// links and folders via the shell's default handler.</summary>
    public static void Open(string target)
    {
        if (Disabled) return;
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
    }

    public static bool OpenFolder(string folder)
    {
        if (!Directory.Exists(folder)) return false;
        Open(folder);
        return true;
    }

    /// <summary>Opens Explorer with one file selected (path must already be validated by the caller).</summary>
    public static bool Reveal(string fullPath)
    {
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath)) return false;
        if (Disabled) return true;
        var psi = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        psi.ArgumentList.Add("/select,\"" + fullPath + "\"");
        Process.Start(psi)?.Dispose();
        return true;
    }
}
