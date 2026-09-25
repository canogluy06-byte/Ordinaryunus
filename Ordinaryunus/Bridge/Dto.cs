// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Text.Json.Serialization;

namespace Ordinaryunus.Bridge;

// ---------------------------------------------------------------------------
// All wire DTOs (MIMARI §3.4, §3.6-3.9). System.Text.Json, JsonSerializerDefaults.Web
// (camelCase) is used by BridgeHost when serializing these; every declared property
// is always present in the JSON (nullable rather than omitted).
// ---------------------------------------------------------------------------

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(NoteTarget), "note")]
[JsonDerivedType(typeof(ProjectTarget), "project")]
[JsonDerivedType(typeof(PageTarget), "page")]
[JsonDerivedType(typeof(JobTarget), "job")]
[JsonDerivedType(typeof(LedgerTarget), "ledger")]
[JsonDerivedType(typeof(UrlTarget), "url")]
public abstract record Target;
public sealed record NoteTarget(string Path, string? Anchor) : Target;
public sealed record ProjectTarget(string Name, string Section) : Target;
public sealed record PageTarget(string Page, string? Tab) : Target;
public sealed record JobTarget(string Id) : Target;
public sealed record LedgerTarget(string? Day) : Target;
public sealed record UrlTarget(string Url) : Target;

public sealed record LightDto(string Id, string Label, string State, string Detail, string Tooltip);
public sealed record UsageDto(long? CodexTokens, long? ClaudeTokens, bool TimedOut, string CodexText, string ClaudeText);

// SÖZLEŞME EKİ (A2, backend'in kendi eklediği): Summary — kaydın {id, islem, hedef, komut, risk, neden} alanlarından
// hesaplanan 8 hex karakterlik özet; decideSafety bunu geri ister ve kuyruktaki GÜNCEL kayıtla eşleşmezse (istek
// listelendikten sonra değiştiyse) reddeder — kart "değişmiş, onaylanamaz" der (A2).
public sealed record SafetyDto(string Id, string? Time, string Tool, string Islem, string Hedef, string Komut,
    string Risk, bool Critical, string Neden, bool Kesildi, string? JobId, string Summary);

public sealed record BittiInfo(int Done, int Total, int Pct, List<BittiItemDto> Items);
public sealed record BittiItemDto(bool Done, string Text);
public sealed record LockInfo(string Holder, string? Since, double? Hours, bool Stale);

public sealed record ProjectDto(string Name, string Path, string Folder, string Durum, bool Odak, string BittiTanimi,
    string SonrakiAdim, string SonrakiAdimKisa, string OlumKriteri, string? KillDate, int? DaysToKill, bool KararBekliyor,
    LockInfo? Kilit, string? Baslangic, string? SonGuncelleme, int? DaysSinceUpdate, BittiInfo Bitti, string Engel,
    List<string> Beklenenler, bool KlasorVar, HealthDto Health);

public sealed record HealthDto(string Level, int Score, int ProblemCount);

public sealed record TaskDto(string Project, string Kimlik, string Title, string Durum, string DurumText, string Atanan,
    string? SonTarih, string Path, string Modified, int AgeDays);

public sealed record RoleDto(string Title, string Short, string Cagri, string Departman, string Yonetici, string Arac,
    string Model, string Ozet, string Path);

public sealed record HistoryItem(string Time, string Day, string Kind, string? Tool, string Text, string? Project, Target? Target);

public sealed record ApplicationRowDto(string Key, string Section, string No, string Firma, string Pozisyon, string Tur,
    string Kanal, string Sehir, string Uygunluk, string EksikBeceri, string? Url, string Belgeler, string Durum,
    string DurumKind, string Not, bool Awaiting);

public sealed record TamGazDto(bool On, string? Start, string? End, int Hours);

public sealed record ScheduledDto(string Name, string Description, string? NextRun);
public sealed record ClaudeSessionDto(string SessionId, string Title, string ProjectDir, string LastActivity, string State, bool AppJob);
public sealed record LockRow(string Project, string Holder, string? Since, double? Hours, bool Stale);
public sealed record CodexWatchDto(bool Running, string Task, bool WaitingQuota, string? LastResult);

// SÖZLEŞME EKİ (C, EK-v2.1 §2b, kullanıcının önerdiği ad): Snapshot.company.keepAwake — uygulama şu an bilgisayarı
// uyanık tutuyor mu ve neden ("2 iş sürüyor", "Tam Gaz açık"); reason boş dize = tutmuyor.
public sealed record KeepAwakeDto(bool On, string Reason);

public sealed record CompanyDto(List<JobDto> Jobs, List<LockRow> Locks, List<TaskDto> ActiveTasks, List<ScheduledDto> Scheduled,
    List<ClaudeSessionDto> ClaudeSessions, TamGazDto TamGaz, CodexWatchDto Watch, List<string> TamGazLog, KeepAwakeDto KeepAwake);

public sealed record ApplicationsDto(bool Found, int AwaitingCount, bool DocsDirExists, List<ApplicationSectionDto> Sections);
public sealed record ApplicationSectionDto(string Name, List<string> Columns, List<ApplicationRowDto> Rows);

public sealed record LinkedInPublishedDto(string No, string Tarih, string Konu);
public sealed record LinkedInDto(bool Found, List<LinkedInPublishedDto> Published, List<string> Drafts);

// SÖZLEŞME EKİ (A13, frontend'in önerdiği, backend'de tamamlandı): LastRun — bu kısayolun bu uygulamadan en
// son ne zaman çalıştırıldığı (ISO|null), Data/ShortcutRuns.cs'te kalıcı, kasaya yazmaz.
// Hazir: kestirmenin dayandığı şey (örneğin /devir becerisi) bu bilgisayarda kurulu mu. false ise kutucuk soluk
// görünür, Text nasıl kurulacağını söyler ve tıklamak açıklayıcı bir hata verir.
public sealed record ShortcutDto(string Id, string Group, string Icon, string Title, string Text, string? LastRun, bool Hazir = true);

public sealed record ToolsDto(List<string> ClaudeHooks, List<string> ClaudeAgents, List<ScheduledDto> ScheduledTasks,
    List<string> CodexMcp, List<PluginDto> CodexPlugins, List<string> CodexHooks, bool VaultOk, bool GitOk,
    bool ClaudeSettingsFound, bool CodexConfigFound);
public sealed record PluginDto(string Name, bool Enabled);

public sealed record ClaudeCliDto(bool Found, string? Version, string? Path, string Auth, string? LoginCommand);

public sealed record DayTaskDto(string Text, string Source, string? TodoKey);
public sealed record TodosDto(bool Found, List<TodoItemDto> Items);
public sealed record TodoItemDto(string Key, string Text, bool Done);
// LinkedinFound: kasada LinkedIn takvimi (40 Alanlar/LinkedIn Takvimi.md) var mı; yoksa Masam taslak kutusunu göstermez.
public sealed record ApprovalsDto(int JobsAwaiting, int LinkedinDrafts, List<string> DecisionProjects, bool LinkedinFound = false);
public sealed record ExpectedDto(string Project, string Path, List<string> Items);
public sealed record LimitDto(int Active, int Max, bool Full);
public sealed record VaultInfoDto(string Path, bool Exists, bool GitOk);

public sealed record Snapshot(
    long Stamp, string LoadedAt, string Today, string TodayText, string Greeting,
    VaultInfoDto Vault, bool Stopped, int Streak, string? FocusProject, LimitDto Limit,
    DayTaskDto? DayTask, TodosDto Todos, List<SafetyDto> Safety, ApprovalsDto Approvals, List<ExpectedDto> Expected,
    List<ProjectDto> Projects, List<TaskDto> Tasks, List<RoleDto> Roles, CompanyDto Company, List<HistoryItem> History,
    ApplicationsDto Applications, LinkedInDto LinkedIn, List<ShortcutDto> Shortcuts, ToolsDto Tools,
    List<LightDto> Lights, UsageDto? Usage, ClaudeCliDto ClaudeCli, List<string> Warnings);

// ---------------- Brain ----------------

public sealed record KpiDto(string Id, string Label, double? Value, string Display, string Sub, string Tone, string Tooltip,
    KpiTrendDto? Trend, List<double>? Spark, double? Progress, Target? Target);
public sealed record KpiTrendDto(double Delta, string Text);

public sealed record ProblemDto(string Id, string Rule, string Severity, string Area, string? Project, string Title,
    string Detail, string Fix, Target? Target, string? Since);

public sealed record ProjectHealthDto(string Name, string Durum, bool Odak, string Level, int Score, List<string> Reasons,
    BittiInfo Bitti, int? DaysToKill, int? DaysSinceUpdate, LockInfo? Lock, string NextStep, KayitEntryDto? LastHandoff,
    List<string> ProblemIds);

public sealed record WorkItemDto(string Text, string? Project, string Source, string SourceLabel, string? Time, Target? Target);
public sealed record RecentItemDto(string Time, string Kind, string? Tool, string Title, string? Text, string? Project, Target? Target);

public sealed record KayitEntryDto(string Kind, string Date, string? Time, string? Tool, string Title,
    List<KeyValueDto> Fields, string Path, string Anchor, string? Project);
public sealed record KeyValueDto(string Key, string Value);

// SÖZLEŞME EKİ (A10, backend'in kendi eklediği): Note — puan bekleyen kritik güvenlik onayları yüzünden "dikkat"
// ağırlığıyla hesaplandıysa kısa bir gerekçe (yoksa boş dize).
public sealed record HealthSummaryDto(int Score, string Level, CountsDto Counts, string Note);
public sealed record CountsDto(int Kritik, int Dikkat, int Bilgi);
public sealed record FocusSummaryDto(string? Project, int? Pct, int Done, int Total);
public sealed record WeekDto(int Commits, int Requests, int PrevCommits, int PrevRequests);

public sealed record Requests14Dto(List<string> Days, List<string> Labels, List<int> Claude, List<int> Codex);
public sealed record Commits14Dto(List<string> Days, List<string> Labels, List<int> Values);
public sealed record Activity30Dto(List<string> Days, List<int> Commits, List<int> Requests, List<int> Kayit);
public sealed record StatusCountDto(string Durum, string Label, int Count);
public sealed record ProgressRowDto(string Project, int Done, int Total, int Pct);
public sealed record KillRowDto(string Project, string Date, int Days, string Condition);
public sealed record ChartsDto(Requests14Dto Requests14, Commits14Dto Commits14, Activity30Dto Activity30,
    List<StatusCountDto> ProjectStatus, List<StatusCountDto> TaskFunnel, List<ProgressRowDto> Progress, List<KillRowDto> Kill);

public sealed record DeepAnalysisDto(bool Found, string? Date, string? Path, List<string> Lines);
public sealed record IncomeDto(bool Found, double MonthTl, double MonthUsd, int TargetUsd, double Pct, double Rate);
public sealed record BrainStatsDto(int Notes, int Files, int Folders, int Links, int Broken, int Words, string IndexedAt, long IndexMs);

public sealed record BrainOverview(string GeneratedAt, List<string> Headline, HealthSummaryDto Health, FocusSummaryDto Focus,
    WeekDto Week, List<KpiDto> Kpis, List<ProjectHealthDto> Projects, List<WorkItemDto> Done, List<WorkItemDto> NotDone,
    List<RecentItemDto> Recent, List<ProblemDto> TopProblems, ChartsDto Charts, List<string> Analysis,
    DeepAnalysisDto DeepAnalysis, IncomeDto Income, UsageDto? Usage, BrainStatsDto Stats);

public sealed record FileInfoDto(string Path, string Title, string Kind, string Modified, long Size, bool IsMarkdown);
public sealed record ProjectDetail(ProjectDto Project, ProjectHealthDto Health, List<KayitEntryDto> Handoffs,
    List<KayitEntryDto> Decisions, List<TaskDto> Tasks, List<WorkItemDto> OpenItems, List<ProblemDto> Problems,
    List<FileInfoDto> Files, Activity30LiteDto Activity30, LinksCountDto Links);
public sealed record Activity30LiteDto(List<string> Days, List<int> Changes);
public sealed record LinksCountDto(int Incoming, int Outgoing);

public sealed record TreeBadgeDto(string Text, string Tone);
public sealed record TreeNode(string Id, string Label, string Kind, string Icon, string? NoteKind, TreeBadgeDto? Badge,
    string? Health, int? Count, Target? Target, List<TreeNode> Children);

public sealed record HeadingDto(int Level, string Text, string Anchor);
public sealed record OutLinkDto(string Label, string? Path, bool Broken);
public sealed record BackLinkDto(string Path, string Title, string Kind, string Context);
public sealed record TasksCountDto(int Open, int Done);
public sealed record RoleShortDto(string Short);
public sealed record NoteDto(string Path, string Title, string Kind, string? Project, List<string> Breadcrumb, string Html,
    List<KeyValueDto> Frontmatter, List<HeadingDto> Headings, List<OutLinkDto> OutLinks, List<BackLinkDto> BackLinks,
    List<ProblemDto> Problems, TasksCountDto Tasks, int Words, long Size, string Modified, bool IsMarkdown, bool Truncated,
    RoleShortDto? Role);

public sealed record SearchResultDto(string Kind, string? Path, string Title, string? NoteKind, string? Project,
    string SnippetHtml, int? Line, double Score, Target Target);

public sealed record LedgerEntryDto(string Time, string Tool, string Project, string Text, bool Auto);
public sealed record LedgerDay(string Day, string Label, List<LedgerEntryDto> Entries);

public sealed record GraphNodeDto(string Id, string Name, string Kind, int Category, int Degree, string? Project,
    bool Missing, string? Modified);
public sealed record GraphLinkDto(string Source, string Target, int Count);
public sealed record GraphCategoryDto(string Name, string Kind);
public sealed record GraphStatsDto(int Nodes, int Links, int Missing, bool Truncated);
public sealed record GraphData(List<GraphNodeDto> Nodes, List<GraphLinkDto> Links, List<GraphCategoryDto> Categories, GraphStatsDto Stats);

// ---------------- Jobs ----------------

// SÖZLEŞME EKİ (EK-v2.1 §2 item 3, backend tarafı): "state" yeni değer "kota-bekliyor" alır; ResumeAt (nöbetçinin
// .nobet.json'daki bekleUntil'i, ISO, sadece kota-bekliyor'da dolu) ve Attempt (nöbetçinin deneme sayacı) yeni
// alanlar — kullanıcının önerdiği adlarla (Job.resumeAt, Job.attempt).
public sealed record JobDto(string Id, string Tool, string Title, string Prompt, string? Role, string? Project,
    string State, string StateText, string Tone, string Start, string? End, long ElapsedSec, string? LastActivity,
    string Now, int Steps, List<string> FilesChanged, List<string> Denials, List<string> SafetyIds, bool Quiet,
    int? ExitCode, string? ResultPreview, string? Fix, bool CanCancel, string? ResumeAt, int Attempt);

public sealed record JobStepDto(string T, string Kind, string Text);
public sealed record JobDetailDto(JobDto Job, List<JobStepDto> Steps, string? ResultHtml, string LogPath, string? SessionId,
    string? Exe, string? LoginCommand);

// ---------------- Settings ----------------

// SÖZLEŞME EKİ (C, EK-v2.1 §2b, kullanıcının önerdiği ad): KeepAwake — "İş varken bilgisayar uyumasın" anahtarı.
public sealed record SettingsDto(string VaultPath, bool VaultOk, int UiScale, int LockMinutes, decimal UsdRate,
    string Theme, bool Animations, string DataDir, string Version, bool DryRun, bool KeepAwake);
