// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
// Mock bridge: same request/event shape as the real C# bridge, backed by mock-data.js.
// Active only when window.chrome.webview is absent (decided in bridge.js).
import * as D from "./mock-data.js";
import { IMZA } from "./imza.js";

const params = new URLSearchParams(location.search);
const EMPTY = params.get("empty") === "1";
const AUTH_GIRIS = params.get("auth") === "giris";

function clone(x) { return JSON.parse(JSON.stringify(x)); }
function delay(ms) { return new Promise((r) => setTimeout(r, ms)); }
function jitter() { return 80 + Math.random() * 170; }

const listeners = [];
export function mockSubscribe(fn) { listeners.push(fn); }
function emit(type, payload) { for (const fn of listeners) fn(type, clone(payload)); }

// ---------------- mutable state ----------------
const state = {
  hasPassword: true,
  unlocked: false,
  wrongTries: 0,
  waitUntil: 0,
  todos: EMPTY ? [] : clone(D.TODOS),
  safety: EMPTY ? [] : clone(D.SAFETY),
  applications: EMPTY ? { found: false, awaitingCount: 0, docsDirExists: false, sections: [] } : clone(D.APPLICATIONS),
  jobs: EMPTY ? [] : clone(D.JOBS),
  history: EMPTY ? [] : clone(D.HISTORY),
  stopped: false,
  tamGaz: clone(D.COMPANY_EXTRA.tamGaz),
  settings: clone(D.SETTINGS),
  streak: EMPTY ? 0 : 3,
  jobCounter: 0,
};
if (AUTH_GIRIS) {
  const j = state.jobs.find((x) => x.tool === "claude");
  if (j) { j.state = "giris"; j.stateText = "Giriş gerekli"; j.tone = "red"; j.fix = "Claude komut satırında bir kez /login yap."; }
}

function buildSnapshot() {
  const projects = EMPTY ? [] : clone(D.PROJECTS);
  const aktif = projects.filter((p) => p.durum === "aktif").length;
  const focus = projects.find((p) => p.odak) || null;
  return {
    stamp: Date.now(), loadedAt: new Date().toISOString(), today: D.TODAY, todayText: D.TODAY_TEXT,
    greeting: hourGreeting(),
    vault: { path: D.SETTINGS.vaultPath, exists: true, gitOk: true },
    stopped: state.stopped, streak: state.streak, focusProject: focus ? focus.name : null,
    limit: { active: aktif, max: 3, full: aktif >= 3 },
    dayTask: state.todos.find((t) => !t.done)
      ? { text: state.todos.find((t) => !t.done).text, source: "todo", todoKey: state.todos.find((t) => !t.done).key }
      : (focus ? { text: focus.sonrakiAdimKisa, source: "focus", todoKey: null } : null),
    todos: { found: state.todos.length > 0, items: clone(state.todos) },
    safety: clone(state.safety),
    approvals: { jobsAwaiting: state.applications.awaitingCount || 0, linkedinDrafts: D.LINKEDIN.drafts.length, decisionProjects: projects.filter((p) => p.kararBekliyor).map((p) => p.name), linkedinFound: !EMPTY },
    expected: projects.filter((p) => p.beklenenler && p.beklenenler.length).map((p) => ({ project: p.name, path: p.path, items: p.beklenenler })),
    projects, tasks: EMPTY ? [] : clone(D.COMPANY_EXTRA.activeTasks), roles: clone(D.ROLES),
    company: {
      jobs: clone(state.jobs), locks: EMPTY ? [] : clone(D.COMPANY_EXTRA.locks), activeTasks: EMPTY ? [] : clone(D.COMPANY_EXTRA.activeTasks),
      scheduled: clone(D.TOOLS.scheduledTasks), claudeSessions: EMPTY ? [] : clone(D.COMPANY_EXTRA.claudeSessions),
      tamGaz: clone(state.tamGaz), watch: clone(D.COMPANY_EXTRA.watch), tamGazLog: clone(D.COMPANY_EXTRA.tamGazLog),
      keepAwake: computeKeepAwake(),
    },
    history: clone(state.history),
    applications: clone(state.applications),
    linkedin: EMPTY ? { found: false, published: [], drafts: [] } : clone(D.LINKEDIN),
    shortcuts: clone(D.SHORTCUTS),
    tools: clone(D.TOOLS),
    lights: clone(D.LIGHTS), usage: clone(D.USAGE),
    claudeCli: clone(D.CLAUDE_CLI),
    warnings: [],
  };
}
function hourGreeting() {
  const h = new Date().getHours();
  if (h < 6) return "İyi geceler"; if (h < 12) return "Günaydın"; if (h < 18) return "İyi günler"; return "İyi akşamlar";
}
// C: Snapshot.keepAwake {on, reason} — mirrors ES_CONTINUOUS|ES_SYSTEM_REQUIRED being held while a job
// runs/waits on quota or Tam Gaz is on; `reason` is a ready Turkish phrase for the Şirketim status line.
function computeKeepAwake() {
  if (state.settings.keepAwake === false) return { on: false, reason: "" };
  const activeJobs = state.jobs.filter((j) => j.state === "calisiyor" || j.state === "basliyor" || j.state === "kota-bekliyor").length;
  if (state.tamGaz.on) return { on: true, reason: "Tam Gaz açık" };
  if (activeJobs > 0) return { on: true, reason: `${activeJobs} iş sürüyor` };
  return { on: false, reason: "" };
}

const HANDLERS = {
  async hello() {
    return { appVersion: IMZA.surum, theme: params.get("theme") === "dark" ? "dark" : "light", animations: params.get("shot") !== "1", uiScale: state.settings.uiScale || 100, screenshotMode: params.get("shot") === "1", dryRun: false, today: D.TODAY, todayText: D.TODAY_TEXT };
  },
  async getAuthState() {
    const waitSeconds = state.waitUntil > Date.now() ? Math.ceil((state.waitUntil - Date.now()) / 1000) : 0;
    return { hasPassword: state.hasPassword, unlocked: state.unlocked, waitSeconds, reason: "start" };
  },
  async login({ password }) {
    if (!state.hasPassword) return err("bad_request", "Henüz şifre belirlenmedi.");
    if (state.waitUntil > Date.now()) return err("auth_wait", `${Math.ceil((state.waitUntil - Date.now()) / 1000)} saniye bekle.`);
    if (password === "test123") { state.unlocked = true; state.wrongTries = 0; return { unlocked: true }; }
    state.wrongTries++;
    if (state.wrongTries >= 5) { state.waitUntil = Date.now() + 30000; return err("auth_wait", "5 yanlış deneme. 30 saniye bekle."); }
    return err("auth_wrong", `Şifre yanlış. ${5 - state.wrongTries} deneme hakkın kaldı.`);
  },
  async setPassword({ password, repeat }) {
    if (state.hasPassword) return err("auth_exists", "Zaten bir şifre var.");
    if (password !== repeat) return err("bad_request", "Şifreler aynı değil.");
    state.hasPassword = true; state.unlocked = true; return { unlocked: true };
  },
  async clientError() { return {}; },
  async activity() { return {}; },
  async lock() { state.unlocked = false; emit("locked", { reason: "manual" }); return {}; },
  async getSnapshot() { return buildSnapshot(); },
  async refresh() { emit("snapshotChanged", buildSnapshot()); return {}; },
  async getLights() { return { lights: clone(D.LIGHTS) }; },
  async getBrainOverview() { return clone(D.OVERVIEW); },
  async getBrainTree() { return { nodes: clone(D.TREE), stats: clone(D.OVERVIEW.stats) }; },
  async getNote({ path }) {
    const n = D.NOTES[path];
    if (!n) return err("not_found", "Not bulunamadı.");
    return clone(n);
  },
  async getProjectDetail({ name }) {
    const p = D.PROJECTS.find((x) => x.name === name);
    if (!p) return err("not_found", "Proje bulunamadı.");
    const health = D.OVERVIEW.projects.find((x) => x.name === name) || null;
    return {
      project: clone(p), health: clone(health),
      handoffs: [{ kind: "devir", date: "2026-09-23", time: "23:14", tool: "codex", title: "devir notu", fields: [{ key: "Yapılan", value: "Kart ve kayıt güncellendi." }, { key: "Açık kalan", value: p.sonrakiAdimKisa || "—" }], path: `20 Projeler/${p.folder}/Kayıt.md`, anchor: "2026-09-23-23-14", project: p.name }],
      decisions: [{ kind: "karar", date: "2026-09-15", time: null, tool: "claude", title: "Kapsam netleştirildi", fields: [{ key: "Karar", value: "Bitti tanımı üç maddeye indirildi." }], path: `20 Projeler/${p.folder}/Kayıt.md`, anchor: "karar-2026-09-15", project: p.name }],
      tasks: (D.COMPANY_EXTRA.activeTasks || []).filter((t) => t.project === name),
      openItems: D.OVERVIEW.notDone.filter((w) => w.project === name),
      problems: D.PROBLEMS.filter((pr) => pr.project === name),
      files: [{ path: p.path, title: "Kart", kind: "proje-karti", modified: "2026-09-23T23:14:00+03:00", size: 2400, isMarkdown: true }],
      activity30: D.OVERVIEW.charts.activity30, links: { incoming: 3, outgoing: 5 },
    };
  },
  async getLedger({ day }) {
    const days = {};
    for (const h of state.history) { if (h.kind !== "istek") continue; (days[h.day] ||= []).push(h); }
    const list = Object.keys(days).sort().reverse().map((d) => ({ day: d, label: d, entries: days[d].map((h) => ({ time: h.time, tool: h.tool || "claude", project: h.project || "—", text: h.text, auto: false })) }));
    return { days: day ? list.filter((x) => x.day === day) : list };
  },
  async searchVault({ q }) {
    const fold = (s) => s.toLowerCase();
    const needle = fold(q || "");
    const results = [];
    for (const [path, n] of Object.entries(D.NOTES)) {
      if (fold(n.title).includes(needle) || fold(path).includes(needle)) {
        results.push({ kind: "note", path, title: n.title, noteKind: n.kind, project: n.project, snippetHtml: n.title.replace(new RegExp(q, "ig"), (m) => `<mark>${m}</mark>`), line: null, score: 10, target: { type: "note", path, anchor: null } });
      }
    }
    return { results, total: results.length, tookMs: 4 };
  },
  async getGraph({ project }) {
    if (!project) return clone(D.GRAPH);
    const g = clone(D.GRAPH);
    const keep = new Set(g.nodes.filter((n) => n.project === project).map((n) => n.id));
    g.links.forEach((l) => { if (keep.has(l.source)) keep.add(l.target); if (keep.has(l.target)) keep.add(l.source); });
    g.nodes = g.nodes.filter((n) => keep.has(n.id));
    g.links = g.links.filter((l) => keep.has(l.source) && keep.has(l.target));
    g.stats = { nodes: g.nodes.length, links: g.links.length, missing: g.nodes.filter((n) => n.missing).length, truncated: false };
    return g;
  },
  async getProblems() {
    const list = clone(D.PROBLEMS);
    const counts = { kritik: 0, dikkat: 0, bilgi: 0 };
    for (const p of list) counts[p.severity]++;
    return { problems: list, counts };
  },
  async toggleTodo({ key }) {
    const t = state.todos.find((x) => x.key === key);
    if (!t) return err("stale", "Dosya az önce değişti, liste yenilendi.");
    t.done = !t.done;
    if (t.done) state.streak = Math.max(state.streak, 3);
    return { done: t.done, text: t.text, celebrate: t.done, streak: state.streak };
  },
  async decideSafety({ id, approve, confirmCritical, summary }) {
    const s = state.safety.find((x) => x.id === id);
    if (!s) return err("not_found", "İstek bulunamadı.");
    // A2 (matches Bridge/Handlers.cs DecideSafety): the card must be voting on the exact record it rendered —
    // a stale `summary` means the queue changed underneath the open card (e.g. someone edited
    // bekleyenler.jsonl), so it must be refused, not silently approved against a different command.
    if (summary && s.summary && summary !== s.summary) return err("stale", "Bu istek değişti; onay/ret yapılamaz, liste yenilendi.");
    if (s.kesildi) return err("forbidden", "Bu istek zaten kesildi.");
    if (approve && s.critical && !confirmCritical) return err("bad_request", "Kritik onay için okuduğunu doğrula.");
    state.safety = state.safety.filter((x) => x.id !== id);
    emit("toast", { text: approve ? "Onaylandı." : "Reddedildi.", tone: "info" });
    return { decided: approve ? "onay" : "red" };
  },
  async approveJobs({ keys }) {
    let count = 0;
    for (const sec of state.applications.sections || []) {
      for (const row of sec.rows) {
        if (keys.includes(row.key) && row.durumKind === "onay-bekliyor") { row.durum = "Onaylandı"; row.durumKind = "onaylandi"; row.awaiting = false; count++; }
      }
    }
    state.applications.awaitingCount = Math.max(0, (state.applications.awaitingCount || 0) - count);
    return { count };
  },
  async runClaude({ text, role, project }) {
    if (state.stopped) return err("stopped", "Acil durdurma açık. Önce \"Devam Et\"e bas.");
    return startFakeJob("claude", text, role, project);
  },
  async runCodex({ text, role, project }) {
    if (state.stopped) return err("stopped", "Acil durdurma açık. Önce \"Devam Et\"e bas.");
    return startFakeJob("codex", text, role, project);
  },
  async copyWork({ tool, text, role }) {
    emit("toast", { text: "Panoya kopyalandı. " + (tool === "claude" ? "Claude'da Ctrl+V ve Enter." : "Codex'te Ctrl+V ve Enter."), tone: "info" });
    return {};
  },
  async getJobs() { return { jobs: clone(state.jobs) }; },
  async getJobDetail({ id }) {
    const job = state.jobs.find((j) => j.id === id);
    if (!job) return err("not_found", "İş bulunamadı.");
    return {
      job: clone(job), steps: [
        { t: job.start, kind: "start", text: "Başladı" },
        { t: job.start, kind: "read", text: "Okuyor: proje kartı" },
        { t: job.start, kind: "edit", text: "Düzenliyor: src/index.html" },
      ], resultHtml: job.resultPreview ? `<p>${job.resultPreview}</p>` : null, logPath: `%APPDATA%\\Ordinaryunus\\claude-isler\\${job.id.split(":")[1]}.jsonl`, sessionId: "sess-a1b2", exe: D.CLAUDE_CLI.path, loginCommand: D.CLAUDE_CLI.loginCommand,
    };
  },
  async retryJobNow({ id }) {
    // B: mirrors touching the nöbetçi's `.simdi` file — ends the kota wait immediately and resumes.
    const job = state.jobs.find((j) => j.id === id);
    if (!job) return err("not_found", "İş bulunamadı.");
    if (job.state !== "kota-bekliyor") return {};
    job.state = "calisiyor"; job.stateText = "Çalışıyor"; job.tone = "teal"; job.now = "Devam ediyor…"; job.resumeAt = null;
    emit("jobUpdated", job);
    runJobLifecycle(job);
    return {};
  },
  async cancelJob({ id }) {
    const job = state.jobs.find((j) => j.id === id);
    if (!job) return err("not_found", "İş bulunamadı.");
    job.state = "durduruldu"; job.stateText = "Durduruldu"; job.tone = "grey"; job.end = new Date().toISOString(); job.canCancel = false;
    emit("jobUpdated", job);
    return { job: clone(job) };
  },
  async emergencyStop() { state.stopped = true; for (const j of state.jobs) if (j.state === "calisiyor" || j.state === "basliyor") { j.state = "durduruldu"; j.stateText = "Durduruldu"; j.tone = "grey"; j.canCancel = false; emit("jobUpdated", j); } return { stopped: state.jobs.length, errors: [] }; },
  async resume() { state.stopped = false; return {}; },
  async tamGazOn({ hours }) {
    if (state.stopped) return err("stopped", "Acil durdurma açık.");
    const start = new Date();
    state.tamGaz = { on: true, start: start.toISOString(), end: new Date(start.getTime() + hours * 3600000).toISOString(), hours };
    return { tamGaz: clone(state.tamGaz) };
  },
  async tamGazOff() { state.tamGaz = { on: false, start: null, end: null, hours: 0 }; return { tamGaz: clone(state.tamGaz) }; },
  async openInObsidian() { emit("toast", { text: "(deneme) Obsidian'da açılıyor…", tone: "info" }); return {}; },
  async openUrl() { emit("toast", { text: "(deneme) Tarayıcıda açılıyor…", tone: "info" }); return {}; },
  async openFolder() { emit("toast", { text: "(deneme) Klasör açılıyor…", tone: "info" }); return {}; },
  async copyText() { emit("toast", { text: "Panoya kopyalandı.", tone: "info" }); return {}; },
  async runShortcut({ id }) {
    const sc = D.SHORTCUTS.find((s) => s.id === id);
    if (!sc) return err("bad_request", "Bilinmeyen kestirme.");
    if (sc.hazir === false) return err("not_found", `/${sc.id} becerisi kurulu değil. Depodaki kasa-araclari\\claude-becerileri\\${sc.id} klasörünü %USERPROFILE%\\.claude\\skills\\ altına kopyala (KURULUM.md 3.6).`);
    if (sc.group === "ac") { emit("toast", { text: "(deneme) " + sc.title, tone: "info" }); return { kind: sc.id.includes("Uygulama") || sc.id === "kasa" || sc.id === "simdi" || sc.id === "linkedin" || sc.id === "basvuruKlasoru" ? "opened" : "copied", output: null, exitCode: null, timedOut: false }; }
    await delay(600);
    return { kind: "script", output: "(deneme) Betik çalıştı.\n2 uyarı, 0 hata.", exitCode: 0, timedOut: false };
  },
  async getSettings() { return clone(state.settings); },
  async setSettings(patch) { Object.assign(state.settings, patch); return clone(state.settings); },
  async pickVaultFolder() { return { path: state.settings.vaultPath }; },
  async changePassword({ old }) { if (old !== "test123") return err("auth_wrong", "Eski şifre yanlış."); return {}; },
  async pageReady() { return {}; },
};

function err(code, message) { const e = new Error(message); e.code = code; e.message2 = message; throw e; }

function startFakeJob(tool, text, role, project) {
  if (state.jobs.filter((j) => j.tool === "claude" && (j.state === "calisiyor" || j.state === "basliyor")).length >= 2 && tool === "claude") {
    return err("limit", "En fazla 2 Claude işi aynı anda çalışabilir.");
  }
  state.jobCounter++;
  const now = new Date();
  const id = `${tool}:${now.toISOString().replace(/[-:TZ.]/g, "").slice(0, 15)}-${String(state.jobCounter).padStart(3, "0")}-mock${state.jobCounter}`;
  const job = {
    id, tool, title: (role ? role + ": " : "") + text.slice(0, 80), prompt: text.slice(0, 1000),
    role: role || null, project: project || null, state: "basliyor", stateText: "Başlıyor", tone: "teal",
    start: now.toISOString(), end: null, elapsedSec: 0, lastActivity: now.toISOString(), now: "Başlıyor…",
    steps: 0, filesChanged: [], denials: [], safetyIds: [], quiet: false, exitCode: null, resultPreview: null, fix: null, canCancel: true,
  };
  state.jobs.unshift(job);
  emit("jobUpdated", job);
  runJobLifecycle(job);
  return Promise.resolve({ job: clone(job) });
}

async function runJobLifecycle(job) {
  const stepsText = ["Okuyor: proje kartı", "Plan güncelliyor", "Düzenliyor: src/components/ProjeKarti.js", "Yazıyor: src/css/site.css", "Komut: npm test"];
  await delay(700);
  if (job.state === "durduruldu") return;
  job.state = "calisiyor"; job.stateText = "Çalışıyor"; job.now = "Başladı"; emit("jobUpdated", job);
  for (let i = 0; i < stepsText.length; i++) {
    await delay(900);
    if (job.state !== "calisiyor") return;
    job.steps++; job.now = stepsText[i]; job.lastActivity = new Date().toISOString();
    emit("jobUpdated", job);
  }
  await delay(900);
  if (job.state !== "calisiyor") return;
  job.state = "bitti"; job.stateText = "Bitti"; job.tone = "green"; job.exitCode = 0; job.canCancel = false;
  job.end = new Date().toISOString(); job.resultPreview = "İstenen değişiklikler uygulandı, derleme temiz.";
  emit("jobUpdated", job);
  emit("toast", { text: (job.tool === "claude" ? "Claude" : "Codex") + " işi bitti.", tone: "success" });
}

export function mockRequest(type, payload, timeout) {
  const fn = HANDLERS[type];
  if (!fn) return Promise.reject({ code: "unknown_type", message: "Bilinmeyen istek türü." });
  const AUTH_FREE = new Set(["hello", "getAuthState", "login", "setPassword", "clientError", "pageReady"]);
  if (!state.unlocked && !AUTH_FREE.has(type)) return Promise.reject({ code: "locked", message: "Kilitli." });
  return delay(jitter()).then(() => fn(payload || {})).catch((e) => { throw { code: e.code || "failed", message: e.message2 || e.message || "Hata." }; });
}

if (params.get("unlocked") === "1") { state.unlocked = true; }
