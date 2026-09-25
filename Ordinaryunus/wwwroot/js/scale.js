// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
// Screen-scale control (EK-v2.1 §2c): Ctrl +/-/0 and Ctrl+wheel step through 6 sizes, a 1s corner notice
// shows the new value, and the Ayarlar "Ekran boyutu" control shares this same state. The actual zoom is
// applied by C# (WebView2 ZoomFactor); this module only tracks the value, persists it and shows the notice.
import { request } from "./bridge.js";
import * as store from "./store.js";
import { h } from "./ui.js";

export const SCALE_STEPS = [100, 110, 125, 140, 150, 175];

function closest(v) {
  return SCALE_STEPS.reduce((best, s) => (Math.abs(s - v) < Math.abs(best - v) ? s : best), SCALE_STEPS[0]);
}

export function getScale() { return store.get("uiScale") || 100; }

// Called once at boot with hello.uiScale (or the fallback default) — never shows the notice.
export function initScale(value) {
  store.set("uiScale", SCALE_STEPS.includes(value) ? value : closest(value || 100));
}

export async function setScale(next) {
  if (!SCALE_STEPS.includes(next)) next = closest(next);
  if (next === getScale()) { showNotice(next); return; }
  store.set("uiScale", next);
  showNotice(next);
  try { await request("setSettings", { uiScale: next }); } catch { /* best effort: UI already reflects the choice */ }
}

export function stepScale(dir) {
  const cur = getScale();
  const idx = SCALE_STEPS.indexOf(cur);
  const from = idx === -1 ? SCALE_STEPS.indexOf(closest(cur)) : idx;
  const nextIdx = Math.max(0, Math.min(SCALE_STEPS.length - 1, from + dir));
  setScale(SCALE_STEPS[nextIdx]);
}

export function resetScale() { setScale(100); }

// ---- 1 saniyelik köşe bildirimi ("Ekran boyutu: %125") ----
let noticeEl = null, noticeTimer = null;
function ensureNotice() {
  if (noticeEl) return noticeEl;
  noticeEl = h("div", { class: "scale-notice" });
  document.body.appendChild(noticeEl);
  return noticeEl;
}
function showNotice(scale) {
  const el = ensureNotice();
  el.textContent = `Ekran boyutu: %${scale}`;
  el.classList.add("show");
  clearTimeout(noticeTimer);
  noticeTimer = setTimeout(() => el.classList.remove("show"), 1000);
}
