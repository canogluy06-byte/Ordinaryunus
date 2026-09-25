// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
namespace Ordinaryunus.Data;

public sealed record TodoItem(int LineIndex, string RawLine, bool Done, string Text);

public sealed class TodoList
{
    public bool Found { get; init; }
    public List<TodoItem> Items { get; init; } = [];
    public int DoneCount => Items.Count(i => i.Done);
    public IEnumerable<TodoItem> Open => Items.Where(i => !i.Done);
}

public sealed class ProjectCard
{
    public required string Name { get; init; }
    public required string RelPath { get; init; }
    public string Durum { get; init; } = "";
    public bool Odak { get; init; }
    public string BittiTanimi { get; init; } = "";
    public string SonrakiAdim { get; init; } = "";
    public string OlumKriteri { get; init; } = "";
    public bool KararBekliyor { get; init; }
    public string Kilit { get; init; } = "";
    /// <summary>Frontmatter "klasor:" (absolute filesystem folder), used by openFolder{which:"projeKlasoru"} (v2).</summary>
    public string Klasor { get; init; } = "";
    public DateOnly? Baslangic { get; init; }
    public DateOnly? SonGuncelleme { get; init; }
    public List<(bool done, string text)> BittiItems { get; init; } = [];
    public List<string> Beklenenler { get; init; } = [];
    public string Engel { get; init; } = "";
    public DateOnly? KillDate { get; init; }

    public int DoneCount => BittiItems.Count(b => b.done);
    public int TotalCount => BittiItems.Count;
    public bool IsActive => Durum == "aktif";
    public double Progress => TotalCount == 0 ? 0 : (double)DoneCount / TotalCount;
    public int? DaysToKill(DateOnly today) => KillDate is { } d ? d.DayNumber - today.DayNumber : null;
    public string NextStepShort => Md.Plain(Md.AfterArrow(SonrakiAdim));
}

public sealed record TaskItem(string Project, string Kimlik, string Title, string Durum, string Atanan,
    DateOnly? SonTarih, string RelPath, DateTime Modified);

public sealed record RoleCard(string Title, string ShortName, string Cagri, string Departman, string Yonetici,
    string Arac, string Model, string Ozet, string RelPath);

public sealed record LedgerEntry(int N, DateTimeOffset T, string Arac, string Proje, string Oturum, string Metin, bool Auto = false)
{
    public DateTime Local => T.LocalDateTime;
}

public sealed record LinkedInPost(string No, string Tarih, string Konu);

public sealed class LinkedInInfo
{
    public bool Found { get; init; }
    public List<LinkedInPost> Published { get; init; } = [];
    public List<string> Drafts { get; init; } = [];
}

public sealed class JobRow
{
    public required string Section { get; init; }
    public required int LineIndex { get; init; }
    public required string RawLine { get; init; }
    public required int DurumCol { get; init; }
    public string No { get; init; } = "";
    public string Firma { get; init; } = "";
    public string Pozisyon { get; init; } = "";
    public string Tur { get; init; } = "";
    public string Kanal { get; init; } = "";
    public string Sehir { get; init; } = "";
    public string Uygunluk { get; init; } = "";
    public string EksikBeceri { get; init; } = "";
    public string IlanLinkiRaw { get; init; } = "";
    public string Belgeler { get; init; } = "";
    public string Durum { get; init; } = "";
    public string Not { get; init; } = "";
    public string? Url => Md.FirstUrl(IlanLinkiRaw);
    public bool AwaitingApproval => Md.Normalize(Durum) == "onay bekliyor";
}

public sealed class JobSection
{
    public required string Name { get; init; }
    public List<JobRow> Rows { get; init; } = [];
    public List<string> Columns { get; init; } = [];
}

public sealed class JobsInfo
{
    public bool Found { get; init; }
    public List<JobSection> Sections { get; init; } = [];
    public IEnumerable<JobRow> AllRows => Sections.SelectMany(s => s.Rows);
    public int AwaitingCount => AllRows.Count(r => r.AwaitingApproval);
}

public sealed record Commit(DateTimeOffset When, string Subject)
{
    public string? Tool => Subject.StartsWith("[claude]", StringComparison.OrdinalIgnoreCase) ? "claude"
        : Subject.StartsWith("[codex]", StringComparison.OrdinalIgnoreCase) ? "codex" : null;
    public string Text => Tool is null ? Subject : Subject[(Subject.IndexOf(']') + 1)..].Trim();
}

public sealed class GitInfo
{
    public bool Available { get; init; }
    public List<Commit> Recent { get; init; } = [];              // 30 days, max 200 (Geçmiş)
    public Dictionary<DateOnly, int> PerDay14 { get; init; } = []; // 14 days (Durum)
}

public sealed record IncomeEntry(DateOnly Date, decimal AmountTl, string Note);

public sealed class IncomeInfo
{
    public bool Found { get; init; }
    public List<IncomeEntry> Entries { get; init; } = [];
    /// <summary>Aylık gelir hedefi (USD); gelir defterinin "hedef_usd:" satırından, yoksa varsayılan.</summary>
    public int TargetUsd { get; init; } = VaultReader.DefaultIncomeTargetUsd;
    public decimal MonthTl(DateOnly today) =>
        Entries.Where(e => e.Date.Year == today.Year && e.Date.Month == today.Month).Sum(e => e.AmountTl);
}

public sealed class AnalysisNote
{
    public bool Found { get; init; }
    public string RelPath { get; init; } = "";
    public DateOnly? Date { get; init; }
    public List<string> Lines { get; init; } = [];
}

public sealed record ScheduledTaskInfo(string Name, string Description, string? NextRun);

// CodexJobRecord (finished-Codex-job history from *.json meta files) removed with the old per-invocation CodexRunner
// (EK-v2.1 §1-2): job history now comes from Jobs/JobStore.cs reading the watchman's own .nobet.json files.

public sealed class ToolStatus
{
    public List<string> ClaudeHooks { get; init; } = [];
    public List<string> ClaudeAgents { get; init; } = [];
    public List<ScheduledTaskInfo> ScheduledTasks { get; init; } = [];
    public List<string> CodexMcpServers { get; init; } = [];
    public List<(string name, bool enabled)> CodexPlugins { get; init; } = [];
    public List<string> CodexHooks { get; init; } = [];
    public bool VaultOk { get; init; }
    public bool GitOk { get; init; }
    public bool ClaudeSettingsFound { get; init; }
    public bool CodexConfigFound { get; init; }
}
