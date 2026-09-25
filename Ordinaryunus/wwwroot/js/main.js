// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request, on, isMock } from "./bridge.js";
import * as store from "./store.js";
import * as router from "./router.js";
import { h, icon, chip, btn, iconBtn, toast, bindTooltips, confirmDialog, fmtTime } from "./ui.js";
import { registerThemes, registerDarkTheme, setChartsTheme, reinitTheme } from "./charts.js";
import { initLogin } from "./overlays/login.js";
import { openGiveWork } from "./overlays/givework.js";
import { openFocus, isFocusActive, closeFocus } from "./overlays/focus.js";
import { openPalette, isPaletteOpen, closePalette } from "./overlays/palette.js";
import { openJobDrawer } from "./overlays/jobdrawer.js";
import { openTamGazDialog } from "./overlays/dialogs.js";
import { initScale, stepScale, resetScale } from "./scale.js";

import masamPage from "./pages/masam.js";
import beyinPage from "./pages/beyin/beyin.js";
import sirketimPage from "./pages/sirketim.js";
import gecmisPage from "./pages/gecmis.js";
import basvurularPage from "./pages/basvurular.js";
import projelerPage from "./pages/projeler.js";
import kestirmelerPage from "./pages/kestirmeler.js";
import ayarlarPage from "./pages/ayarlar.js";

window.__openJobDrawer = openJobDrawer; // small escape hatch for pages that need it without a circular import

const NAV_ITEMS = [
  { id: "masam", label: "Masam", icon: "masam", key: "1" },
  { id: "beyin", label: "Beyin", icon: "beyin", key: "2" },
  { id: "sirketim", label: "Şirketim", icon: "sirketim", key: "3" },
  { id: "gecmis", label: "Geçmiş", icon: "gecmis", key: "4" },
  { id: "basvurular", label: "İş Başvuruları", icon: "basvurular", key: "5" },
  { id: "projeler", label: "Projeler", icon: "projeler", key: "6" },
  { id: "kestirmeler", label: "Kestirmeler", icon: "kestirmeler", key: "7" },
];

function applyTheme(theme) {
  document.documentElement.setAttribute("data-theme", theme);
  store.set("theme", theme);
  registerDarkTheme();
  setChartsTheme(theme === "dark" ? "ordi-dark" : "ordi-light");
  reinitTheme();
}
function applyAnimations(animations) {
  const shot = new URLSearchParams(location.search).get("shot") === "1";
  const reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  document.documentElement.classList.toggle("no-anim", !animations || reduce || shot);
}

function buildShellOnce() {
  const list = document.getElementById("nav-list");
  const pill = h("div", { class: "nav-pill" });
  list.appendChild(pill);
  NAV_ITEMS.forEach((item) => {
    const b = h("button", { class: "nav-item", onclick: () => router.navigate("#/" + item.id) },
      icon(item.icon), h("span", { class: "label" }, item.label), h("span", { class: "kbd" }, "Ctrl+" + item.key), h("span", { class: "badge" }));
    b.dataset.id = item.id;
    list.appendChild(b);
  });
  const bottom = document.getElementById("nav-bottom");
  bottom.appendChild(h("button", { class: "nav-item", onclick: () => router.navigate("#/ayarlar") }, icon("ayarlar"), h("span", { class: "label" }, "Ayarlar")));
  bottom.appendChild(h("button", { class: "nav-item", onclick: doLock }, icon("lock"), h("span", { class: "label" }, "Kilitle"), h("span", { class: "kbd" }, "Ctrl+L")));
}

function updateNavActive(activeId) {
  const list = document.getElementById("nav-list");
  const items = Array.from(list.querySelectorAll(".nav-item"));
  const idx = NAV_ITEMS.findIndex((n) => n.id === activeId);
  const pill = list.querySelector(".nav-pill");
  items.forEach((b) => b.classList.toggle("active", b.dataset.id === activeId));
  if (idx >= 0) { pill.style.opacity = "1"; pill.style.transform = `translateY(${idx * 50}px)`; }
  else pill.style.opacity = "0";
}

function setBadge(pageId, count) {
  const el = document.querySelector(`.nav-item[data-id="${pageId}"] .badge`);
  if (!el) return;
  el.classList.toggle("show", count > 0);
  if (count > 0) el.textContent = count > 99 ? "99+" : String(count);
}

let topbarState = { lightsOpen: false };
function renderTopbar() {
  const snap = store.get("snapshot");
  const bar = document.getElementById("topbar");
  bar.innerHTML = "";
  if (!snap) return;
  bar.appendChild(h("div", { class: "date" }, snap.todayText));
  if (snap.focusProject) bar.appendChild(chip("★ " + snap.focusProject, "teal"));
  if (snap.streak > 0) bar.appendChild(chip(`🔥 ${snap.streak} gün`, "yellow", { noDot: true }));
  if (snap.company?.tamGaz?.on) bar.appendChild(chip("Tam Gaz · " + fmtTime(snap.company.tamGaz.end) + "'a kadar", "teal", { pulse: true }));
  bar.appendChild(h("div", { class: "spacer" }));

  bar.appendChild(btn("Ara · Ctrl+K", { variant: "ghost", icon: "search", onClick: () => openPalette() }));
  bar.appendChild(renderLightsPill(snap.lights || []));
  bar.appendChild(iconBtn("rocket", { tip: "Tam Gaz Modu", onClick: () => openTamGazDialog() }));
  if (snap.stopped) {
    bar.appendChild(btn("Devam Et", { variant: "primary", icon: "play", onClick: async () => { const ok = await confirmDialog("Acil durdurma kaldırılsın ve yapay zekâlar tekrar çalışabilsin mi?", { okText: "Devam Et" }); if (ok) { await request("resume", { confirm: true }); toast("Devam edildi.", "success"); await refreshSnapshot(); } } }));
  } else {
    bar.appendChild(btn("Acil Durdur", { variant: "danger", icon: "stop", onClick: async () => { const ok = await confirmDialog("Bütün yapay zekâ işleri hemen durdurulacak. Emin misin?", { danger: true, okText: "Durdur" }); if (ok) { await request("emergencyStop", { confirm: true }); toast("Acil durduruldu.", "warn"); await refreshSnapshot(); } } }));
  }
  bar.appendChild(h("div", { class: "sep", style: { width: "1px", height: "24px", background: "var(--border)" } }));
  bar.appendChild(btn("Codex'e iş ver", { variant: "secondary", icon: "cpu", onClick: () => openGiveWork({}) }));
  bar.appendChild(btn("Claude'a iş ver", { variant: "primary", icon: "message", onClick: () => openGiveWork({}) }));
  bar.appendChild(iconBtn("refresh", { tip: "Yenile (F5)", onClick: doRefresh }));
  bar.appendChild(iconBtn(store.get("theme") === "dark" ? "sun" : "moon", { tip: "Tema değiştir", onClick: toggleTheme }));

  bindTooltips(bar);

  const bannerSlot = document.getElementById("banner-slot");
  bannerSlot.innerHTML = "";
  if (snap.stopped) bannerSlot.appendChild(h("div", { class: "banner" }, icon("stop"), h("span", {}, "Acil durdurma açık: yapay zekâlar çalışamıyor."), h("div", { class: "spacer" })));
  else if (!snap.vault?.exists) bannerSlot.appendChild(h("div", { class: "banner warn" }, icon("alert"), h("span", {}, "Kasa bulunamadı: " + snap.vault?.path), h("div", { class: "spacer" })));
}

function renderLightsPill(lights) {
  const pill = h("div", { class: "lights-pill" });
  const order = ["kritik-first"]; // critical (red) lights are already prioritised by keeping array order stable
  const sorted = [...lights].sort((a, b) => (sevRank(a.state) - sevRank(b.state)));
  for (const l of sorted) {
    const dot = h("span", { class: `light light-${l.state}` + (l.state === "red" ? " pulse" : ""), "data-tip": l.tooltip, "data-tip-title": l.label });
    pill.appendChild(dot);
  }
  bindTooltips(pill);
  return pill;
}
function sevRank(s) { return { red: 0, yellow: 1, green: 2, off: 3 }[s] ?? 4; }

async function doRefresh() {
  toast("Yenileniyor…", "info");
  try { await request("refresh"); } catch {}
}
async function refreshSnapshot() {
  try { const snap = await request("getSnapshot"); store.set("snapshot", snap); onSnapshot(snap); } catch {}
}
async function toggleTheme() {
  const next = store.get("theme") === "dark" ? "light" : "dark";
  applyTheme(next);
  try { await request("setSettings", { theme: next }); } catch {}
  renderTopbar();
}
async function doLock() {
  try { await request("lock"); } catch {}
}

function onSnapshot(snap) {
  renderTopbar();
  const route = router.currentRoute();
  updateNavActive(route.page === "beyin" ? "beyin" : route.page);
  const runningJobs = (snap.company?.jobs || []).filter((j) => j.state === "calisiyor" || j.state === "basliyor").length;
  setBadge("sirketim", runningJobs);
  setBadge("masam", (snap.safety?.length || 0) + (snap.approvals?.jobsAwaiting || 0));
  store.setJobsFromList(snap.company?.jobs || []);
}

async function refreshProblemBadge() {
  try { const res = await request("getProblems"); store.set("problemCounts", res.counts); setBadge("beyin", res.counts.kritik); } catch {}
}

function wireGlobalEvents() {
  on("snapshotChanged", (snap) => { store.set("snapshot", snap); onSnapshot(snap); });
  on("jobUpdated", (job) => {
    // Update snapshot.company.jobs (read by sirketim.js) BEFORE store.patchJob() below, whose "jobs"
    // subscribers (e.g. sirketim.js's live re-render, so "Şimdi dene"/"İptal"/"Durdur" show their effect
    // without leaving the page) read store.get("snapshot") synchronously — patching first would fire them
    // against the still-stale job list.
    const snap = store.get("snapshot");
    if (snap) {
      const jobs = snap.company.jobs.filter((j) => j.id !== job.id);
      jobs.unshift(job);
      snap.company.jobs = jobs;
      const running = jobs.filter((j) => j.state === "calisiyor" || j.state === "basliyor").length;
      setBadge("sirketim", running);
    }
    store.patchJob(job);
    renderTopbar();
  });
  on("lightsChanged", (p) => { const snap = store.get("snapshot"); if (snap) { snap.lights = p.lights; renderTopbar(); } });
  on("usageChanged", (u) => { const snap = store.get("snapshot"); if (snap) snap.usage = u; });
  on("toast", (p) => toast(p.text, p.tone));
  on("locked", () => location.reload());
  on("brainChanged", () => { store.set("brainStamp", Date.now()); refreshProblemBadge(); });
}

function wireKeyboard() {
  document.addEventListener("keydown", (e) => {
    if (isFocusActive()) { if (e.key === "Escape") {} return; }
    const ctrl = e.ctrlKey || e.metaKey;
    if (ctrl && /^[1-7]$/.test(e.key)) { e.preventDefault(); router.navigate("#/" + NAV_ITEMS[+e.key - 1].id); }
    else if (e.key === "F5") { e.preventDefault(); doRefresh(); }
    else if (ctrl && e.key.toLowerCase() === "l") { e.preventDefault(); doLock(); }
    else if (ctrl && e.key.toLowerCase() === "k") { e.preventDefault(); if (!isPaletteOpen()) openPalette(); }
    else if (ctrl && (e.key === "+" || e.key === "=")) { e.preventDefault(); stepScale(1); }
    else if (ctrl && (e.key === "-" || e.key === "_")) { e.preventDefault(); stepScale(-1); }
    else if (ctrl && e.key === "0") { e.preventDefault(); resetScale(); }
  });
  // Ctrl+wheel (also how trackpad pinch-zoom arrives in Chromium): step through the same 6 sizes.
  // WebView2's own pinch/zoom is disabled host-side, so this never fights a native browser zoom.
  window.addEventListener("wheel", (e) => {
    if (!e.ctrlKey || isFocusActive()) return; // Odak modu: only Esc/Ctrl+L work (MIMARI §7.7)
    e.preventDefault();
    stepScale(e.deltaY < 0 ? 1 : -1);
  }, { passive: false });
}

function applyRailBreakpoint() {
  document.documentElement.classList.toggle("rail", window.innerWidth < 1180);
}
function wireRailBreakpoint() {
  applyRailBreakpoint();
  let t = null;
  window.addEventListener("resize", () => { clearTimeout(t); t = setTimeout(applyRailBreakpoint, 120); });
}

let lastActivityPing = 0;
function wireActivity() {
  const ping = () => {
    const now = Date.now();
    if (now - lastActivityPing < 29000) return;
    lastActivityPing = now;
    request("activity", { focusRunning: isFocusActive() }).catch(() => {});
  };
  ["pointermove", "pointerdown", "keydown", "wheel"].forEach((ev) => window.addEventListener(ev, ping, { passive: true }));
}

function registerPages() {
  router.register(masamPage(helpers()));
  router.register(beyinPage(helpers()));
  router.register(sirketimPage(helpers()));
  router.register(gecmisPage(helpers()));
  router.register(basvurularPage(helpers()));
  router.register(projelerPage(helpers()));
  router.register(kestirmelerPage(helpers()));
  router.register(ayarlarPage(helpers()));
}
function helpers() {
  return { openGiveWork, openFocus, openJobDrawer, openTamGazDialog, toast, confirmDialog, openTarget: (t) => router.openTarget(t, { openJobDrawer, openUrl: (url) => request("openUrl", { url }) }) };
}

let appEntered = false;
async function enterApp() {
  if (appEntered) return; // idempotent: the first "shot" event in screenshot mode may race a real login
  appEntered = true;
  document.getElementById("login").classList.add("hidden");
  const appEl = document.getElementById("app");
  appEl.classList.remove("hidden");
  buildShellOnce();
  wireGlobalEvents(); wireKeyboard(); wireActivity(); wireRailBreakpoint();
  registerPages();
  router.init(document.getElementById("content"));
  const snap = await request("getSnapshot");
  store.set("snapshot", snap);
  onSnapshot(snap);
  refreshProblemBadge();
  requestAnimationFrame(() => appEl.classList.add("ready"));
  if (!location.hash) location.hash = "#/masam";
  router.render();
  document.addEventListener("routechange", (e) => updateNavActive(e.detail.page === "beyin" ? "beyin" : e.detail.page));

  const params = new URLSearchParams(location.search);
  const openParam = params.get("open");
  if (openParam === "givework") openGiveWork({});
  if (openParam === "tamgaz") openTamGazDialog();
  if (openParam === "focus") openFocus({ text: "Portfolyo Yenileme'de proje kartları bölümünü bitir", todoKey: null });
  if (openParam === "palette") openPalette();

  if (params.get("selfcheck") === "1") runSelfcheck();
}

async function runSelfcheck() {
  try {
    const routes = ["masam", "beyin/genel", "beyin/kesif", "beyin/harita", "beyin/sorunlar", "sirketim", "gecmis", "basvurular", "projeler", "kestirmeler", "ayarlar"];
    for (const r of routes) { router.navigate("#/" + r); await new Promise((res) => setTimeout(res, 120)); }
    document.body.dataset.selfcheck = "ok";
  } catch (e) {
    document.body.dataset.selfcheck = "fail:" + e.message;
  }
}

async function boot() {
  const hello = await request("hello");
  store.set("hello", hello);
  applyTheme(hello.theme);
  applyAnimations(hello.animations);
  initScale(hello.uiScale || 100);
  registerThemes();

  // Screenshot mode (MIMARI §2.4/§3.12): C# captures "giris.png" first, then unlocks internally
  // (no password) and starts pushing "shot" events — which the bridge delivers even while the JS
  // side still thinks it is locked (locked/toast/shot are exempt from the lock gate). So this
  // listener must be live before login, not registered inside enterApp() as a post-login step,
  // or every shot after the first would be dropped and every capture would show the login screen.
  if (hello.screenshotMode) {
    on("shot", async (p) => {
      await enterApp(); // no-op if already entered
      // Each shot is a fresh scene: close whatever the previous shot opened first, or e.g. Focus Mode (a
      // full-viewport overlay outside the router's page content) would still cover every shot that follows it.
      closeFocus(); closePalette();
      const overlayRoot = document.getElementById("overlay-root");
      if (overlayRoot) { overlayRoot.innerHTML = ""; overlayRoot.style.pointerEvents = "none"; }
      if (p.page && p.page !== "giris") router.navigate("#/" + p.page + (p.tab ? "/" + p.tab : ""));
      if (p.open === "givework") openGiveWork({});
      if (p.open === "tamgaz") openTamGazDialog();
      if (p.open === "focus") openFocus({ text: "Portfolyo Yenileme'de proje kartları bölümünü bitir" });
      if (p.open === "palette") openPalette(p.query || "");
      await new Promise((r) => setTimeout(r, 350));
      // Hakkında kartı Ayarlar'ın en altında: görüntüde görünsün diye sayfa oraya kaydırılır (ayarlar sonradan
      // getSettings ile yeniden çizildiği için bekleme bittikten sonra).
      if (p.open === "hakkinda") document.querySelector(".about-card")?.scrollIntoView({ block: "end", behavior: "instant" });
      // Harita: grafik yerleşsin diye biraz daha beklenir, sonra grafik kartı kesilmeden görünsün diye oraya kaydırılır.
      if (p.open === "harita") {
        await new Promise((r) => setTimeout(r, 1500));
        document.getElementById("graph-canvas")?.scrollIntoView({ block: "end", behavior: "instant" });
      }
      // Sekmedeki adres parametresi (kesif?path=…) pageReady'ye gönderilmez: köprü sekme adını 40 karakterle sınırlar.
      request("pageReady", { page: p.page || "masam", tab: p.tab ? p.tab.split("?")[0] : null }).catch(() => {});
    });
  }

  const auth = await request("getAuthState");
  document.getElementById("splash").classList.add("hidden");
  if (!auth.unlocked) {
    document.getElementById("login").classList.remove("hidden");
    initLogin(auth, enterApp);
    if (hello.screenshotMode) request("pageReady", { page: "giris", tab: null }).catch(() => {});
  } else {
    await enterApp();
  }
}

boot().catch((e) => {
  console.error("boot failed", e);
  document.getElementById("splash").classList.add("hidden");
  toast("Başlatma hatası: " + (e.message || e), "error");
});
