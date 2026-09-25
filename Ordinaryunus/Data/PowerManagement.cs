// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Runtime.InteropServices;

namespace Ordinaryunus.Data;

/// <summary>
/// Keeps Windows from sleeping while a Claude/Codex job is running or waiting out a quota, or while Tam Gaz is on
/// (EK-v2.1 §2b): "gece 02:21'de bilgisayar uyku moduna geçti, çalışan ekipler 7,5 saat durdu". Only
/// <see cref="Apply"/> touches Win32 (called from the Host layer); <see cref="Compute"/> is pure so it can be
/// unit-tested headlessly. Windows' own power-plan settings are never changed — this only raises the process's own
/// execution-state requirement for as long as work is pending, then releases it.
/// </summary>
public static class PowerManagement
{
    [DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint esFlags);

    const uint ES_CONTINUOUS = 0x80000000;
    const uint ES_SYSTEM_REQUIRED = 0x00000001;

    static bool? _lastApplied;

    /// <summary>Decides whether the app should currently be holding the "stay awake" request, and the one-line
    /// Turkish reason shown in Şirketim ("Bilgisayar uyanık tutuluyor: …"). Pure: no Win32, no clock reads besides
    /// what the caller already computed into <paramref name="jobs"/>.</summary>
    public static (bool on, string reason) Compute(bool settingEnabled, IReadOnlyList<Bridge.JobDto> jobs, bool tamGazOn)
    {
        if (!settingEnabled) return (false, "");
        int running = jobs.Count(j => j.State is "basliyor" or "calisiyor" or "kota-bekliyor");
        var parts = new List<string>();
        if (running > 0) parts.Add(running == 1 ? "1 iş sürüyor" : $"{running} iş sürüyor");
        if (tamGazOn) parts.Add("Tam Gaz açık");
        return (parts.Count > 0, string.Join(", ", parts));
    }

    /// <summary>Applies (or releases) the execution-state request. Safe to call repeatedly; skips the Win32 call
    /// when nothing changed. Never throws (a headless/selftest process may not even have a message loop, but
    /// SetThreadExecutionState is safe to call there too).</summary>
    public static void Apply(bool keepAwake)
    {
        if (_lastApplied == keepAwake) return;
        try { SetThreadExecutionState(ES_CONTINUOUS | (keepAwake ? ES_SYSTEM_REQUIRED : 0)); }
        catch (Exception) { /* best-effort only: never let a power-state call take the app down */ }
        _lastApplied = keepAwake;
    }
}
