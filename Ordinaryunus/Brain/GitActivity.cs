// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Globalization;
using Ordinaryunus.Data;

namespace Ordinaryunus.Brain;

/// <summary>Per-day commit counts and per-file last-change time, from one `git log` call (MIMARI §5.1 step 3).</summary>
public sealed class GitActivity
{
    public bool Available { get; init; }
    public Dictionary<DateOnly, int> PerDay30 { get; init; } = [];
    public Dictionary<string, DateTimeOffset> LastChangeByFile { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Warnings { get; init; } = [];

    public static GitActivity Load(string vault)
    {
        // --relative ve "-- .": kasa bir üst deponun alt klasörüyse yalnızca kasadaki değişiklikler, kasaya göre yollarla.
        // core.quotepath=false: Türkçe harfli dosya adları "\305\237" gibi kaçışlı değil, olduğu gibi gelsin (dizinle eşleşsin).
        var r = ProcessRunner.Run("git",
            ["-c", "core.quotepath=false", "-C", vault, "log", "--since=30 days ago", "--date=iso-strict", "--name-only", "--relative",
                "--format=%x1e%ad%x1f%s", "--", "."], null, 15000);
        if (r.ExitCode != 0)
            return new GitActivity { Available = false, Warnings = ["Git etkinliği okunamadı (30 günlük geçmiş)."] };
        return Parse(r.Output, true);
    }

    public static GitActivity Parse(string output, bool available = true)
    {
        var perDay = new Dictionary<DateOnly, int>();
        var lastChange = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in output.Split('\u001e'))
        {
            if (chunk.Trim().Length == 0) continue;
            var lines = chunk.Replace("\r\n", "\n").Split('\n');
            int sep = lines[0].IndexOf('\u001f');
            if (sep < 0) continue;
            if (!DateTimeOffset.TryParse(lines[0][..sep], CultureInfo.InvariantCulture, DateTimeStyles.None, out var when)) continue;
            var day = DateOnly.FromDateTime(when.LocalDateTime);
            perDay[day] = perDay.GetValueOrDefault(day) + 1;
            for (int i = 1; i < lines.Length; i++)
            {
                string f = lines[i].Trim().Replace('\\', '/');
                if (f.Length == 0) continue;
                if (!lastChange.TryGetValue(f, out var existing) || when > existing) lastChange[f] = when;
            }
        }
        return new GitActivity { Available = available, PerDay30 = perDay, LastChangeByFile = lastChange };
    }

    public int CommitsOn(DateOnly day) => PerDay30.GetValueOrDefault(day);
}
