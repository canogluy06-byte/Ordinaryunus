// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
// ECharts wiring: themes from CSS variables, tracked instances, tooltip helper with "Bu ne?" line.
import { escapeHtml } from "./ui.js";

const instances = new Set();
let themesRegistered = false;
let currentThemeName = "ordi-light";

function cssVar(name) { return getComputedStyle(document.documentElement).getPropertyValue(name).trim(); }

function buildTheme() {
  return {
    color: [cssVar("--teal"), cssVar("--slate"), cssVar("--teal-2"), cssVar("--slate-2"), cssVar("--teal-mid")],
    backgroundColor: "transparent",
    textStyle: { fontFamily: "Segoe UI, sans-serif", color: cssVar("--text") },
    title: { textStyle: { color: cssVar("--text") } },
    legend: { textStyle: { color: cssVar("--muted") } },
    grid: { left: 8, right: 8, top: 28, bottom: 8, containLabel: true },
    categoryAxis: { axisLine: { lineStyle: { color: cssVar("--border") } }, axisLabel: { color: cssVar("--muted"), fontSize: 11 }, splitLine: { show: false } },
    valueAxis: { axisLine: { show: false }, axisLabel: { color: cssVar("--muted"), fontSize: 11 }, splitLine: { lineStyle: { color: cssVar("--border-soft") } } },
  };
}

export function registerThemes() {
  const light = buildTheme();
  echarts.registerTheme("ordi-light", light);
  // dark theme computed after html[data-theme=dark] is applied by caller
}
export function registerDarkTheme() {
  echarts.registerTheme("ordi-dark", buildTheme());
}

export function setChartsTheme(name) { currentThemeName = name; }

const noAnim = () => document.documentElement.classList.contains("no-anim");

// Canvas 2D (ECharts' canvas renderer) cannot resolve CSS custom properties the way DOM/SVG
// elements can — `ctx.fillStyle = "var(--teal)"` silently falls back to black. Every option object
// passed to `chart()` may freely use var(--token) strings (consistent with the rest of the app); we
// resolve them against :root here, once, right before handing the option to ECharts.
const VAR_RE = /var\((--[a-zA-Z0-9-]+)\)/g;
function resolveVarString(s) {
  if (s.indexOf("var(--") === -1) return s;
  return s.replace(VAR_RE, (m, name) => cssVar(name) || m);
}
function resolveVarsDeep(value) {
  if (typeof value === "string") return resolveVarString(value);
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) value[i] = resolveVarsDeep(value[i]); return value; }
  if (value && typeof value === "object") { for (const k of Object.keys(value)) value[k] = resolveVarsDeep(value[k]); return value; }
  return value;
}

export function chart(el, option, opts = {}) {
  const inst = echarts.init(el, currentThemeName, { renderer: "canvas" });
  const merged = Object.assign({
    animationDuration: noAnim() ? 0 : 900, animationEasing: "cubicOut",
    animationDelay: noAnim() ? 0 : (i) => i * 40, animationDurationUpdate: noAnim() ? 0 : 500,
  }, option);
  resolveVarsDeep(merged);
  inst.setOption(merged);
  const ro = new ResizeObserver(() => inst.resize());
  ro.observe(el);
  instances.add({ inst, ro, el });
  if (opts.onClick) inst.on("click", opts.onClick);
  return inst;
}

export function disposeChartsIn(root) {
  for (const rec of Array.from(instances)) {
    if (!root || root.contains(rec.el)) {
      rec.ro.disconnect();
      try { rec.inst.dispose(); } catch {}
      instances.delete(rec);
    }
  }
}

export function reinitTheme() {
  for (const rec of Array.from(instances)) {
    const opt = rec.inst.getOption();
    rec.ro.disconnect(); rec.inst.dispose();
    const inst = echarts.init(rec.el, currentThemeName, { renderer: "canvas" });
    inst.setOption(opt);
    const ro = new ResizeObserver(() => inst.resize());
    ro.observe(rec.el);
    instances.delete(rec);
    instances.add({ inst, ro, el: rec.el });
  }
}

// Tooltip formatter builder: title + coloured rows + one "Bu ne?" help line, escaping every value.
export function tipHtml({ title, rows, help }) {
  let html = `<div style="font-weight:700;margin-bottom:4px;">${escapeHtml(title)}</div>`;
  for (const r of rows) {
    html += `<div style="display:flex;align-items:center;gap:6px;font-size:12.5px;margin:2px 0;">`;
    if (r.color) html += `<span style="width:8px;height:8px;border-radius:50%;background:${escapeHtml(r.color)};display:inline-block;"></span>`;
    html += `<span style="color:var(--muted);">${escapeHtml(r.label)}</span><b style="margin-left:auto;">${escapeHtml(String(r.value))}</b></div>`;
  }
  if (help) html += `<div style="margin-top:6px;padding-top:6px;border-top:1px solid var(--border-soft);font-size:11.5px;color:var(--faint);max-width:220px;">${escapeHtml(help)}</div>`;
  return html;
}

export const commonTooltip = { confine: true, appendToBody: false, renderMode: "html", backgroundColor: "var(--surface)", borderColor: "var(--border)", extraCssText: "box-shadow:var(--shadow-2);border-radius:10px;padding:10px 12px;" };
