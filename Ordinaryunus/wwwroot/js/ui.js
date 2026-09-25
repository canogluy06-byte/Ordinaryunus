// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
// Small DOM helpers, shared components and formatters. No framework.

export function h(tag, props, ...children) {
  const el = document.createElement(tag);
  if (props) {
    for (const [k, v] of Object.entries(props)) {
      if (v == null || v === false) continue;
      if (k === "class") el.className = v;
      else if (k === "style" && typeof v === "object") {
        // Object.assign(el.style, v) silently drops CSS custom properties: CSSStyleDeclaration only
        // reflects standard properties as settable named keys, so `style["--foo"] = x` is a silent no-op
        // in Chromium — it must go through setProperty(). (Found via kestirmeler.js's per-group
        // --tile-accent, which rendered as if unset until this was fixed — A13.)
        for (const [sk, sv] of Object.entries(v)) {
          if (sk.startsWith("--")) el.style.setProperty(sk, sv);
          else el.style[sk] = sv;
        }
      }
      else if (k.startsWith("on") && typeof v === "function") el.addEventListener(k.slice(2).toLowerCase(), v);
      else if (k === "html") el.innerHTML = v; // caller-vetted only (see §note.html contract)
      else if (k in el && k !== "list") { try { el[k] = v; } catch { el.setAttribute(k, v); } }
      else el.setAttribute(k, v);
    }
  }
  for (const c of children.flat(Infinity)) {
    if (c == null || c === false) continue;
    el.appendChild(c instanceof Node ? c : document.createTextNode(String(c)));
  }
  return el;
}

export function icon(name, cls = "") {
  const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.setAttribute("class", "icon " + cls);
  const use = document.createElementNS("http://www.w3.org/2000/svg", "use");
  use.setAttribute("href", "#i-" + name);
  svg.appendChild(use);
  return svg;
}

export function chip(text, tone = "grey", opts = {}) {
  const c = h("span", { class: `chip chip-${tone} ${opts.pulse ? "chip-pulse" : ""}` });
  if (!opts.noDot) c.appendChild(h("span", { class: "dot" }));
  c.appendChild(h("span", { class: "chip-label" }, text));
  return c;
}

export function btn(text, opts = {}) {
  const variant = opts.variant || "ghost";
  const b = h("button", { class: `btn btn-${variant} ${opts.sm ? "btn-sm" : ""} ${opts.className || ""}`, onclick: opts.onClick, disabled: opts.disabled, type: opts.type || "button", "aria-label": opts.ariaLabel || null });
  if (opts.icon) b.appendChild(icon(opts.icon));
  if (text) b.appendChild(document.createTextNode(text));
  return b;
}

export function iconBtn(name, opts = {}) {
  const b = h("button", { class: "icon-btn " + (opts.className || ""), onclick: opts.onClick, "aria-label": opts.ariaLabel || name, "data-tip": opts.tip || null });
  b.appendChild(icon(name));
  bindTooltip(b);
  return b;
}

export function empty(iconName, text) {
  return h("div", { class: "empty" }, icon(iconName, ""), h("div", {}, text));
}

export function skeleton(rows = 3, height = 16) {
  const wrap = h("div", { class: "vstack", style: { gap: "8px" } });
  for (let i = 0; i < rows; i++) wrap.appendChild(h("div", { class: "skel", style: { height: height + "px", width: i === rows - 1 ? "60%" : "100%" } }));
  return wrap;
}

// ---------------- formatters ----------------
const nf = new Intl.NumberFormat("tr-TR");
export function fmtNum(n) { return n == null ? "—" : nf.format(n); }
export function fmtRelative(iso) {
  if (!iso) return "—";
  const d = new Date(iso), now = new Date();
  const diffMs = now - d, min = Math.round(diffMs / 60000);
  if (min < 1) return "az önce";
  if (min < 60) return `${min} dk önce`;
  const h = Math.round(min / 60);
  if (h < 24) return `${h} sa önce`;
  const days = Math.round(h / 24);
  if (days < 7) return `${days} gün önce`;
  return fmtDayLabel(iso);
}
const TR_DAYS = ["Pazar","Pazartesi","Salı","Çarşamba","Perşembe","Cuma","Cumartesi"];
const TR_MONTHS = ["Ocak","Şubat","Mart","Nisan","Mayıs","Haziran","Temmuz","Ağustos","Eylül","Ekim","Kasım","Aralık"];
export function fmtDayLabel(iso) {
  const d = new Date(iso);
  return `${d.getDate()} ${TR_MONTHS[d.getMonth()]} ${TR_DAYS[d.getDay()]}`;
}
export function fmtTime(iso) {
  const d = new Date(iso);
  return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
}
export function fmtShortDay(day) {
  const d = new Date(day + "T00:00:00");
  return `${d.getDate()} ${TR_MONTHS[d.getMonth()].slice(0, 3)}`;
}

// ---------------- count up ----------------
export function countUp(el, to, opts = {}) {
  const fmt = opts.fmt || fmtNum;
  const from = opts.from ?? 0;
  if (document.documentElement.classList.contains("no-anim") || to == null) { el.textContent = to == null ? "—" : fmt(to); return; }
  const dur = 900, start = performance.now();
  function frame(t) {
    const p = Math.min(1, (t - start) / dur);
    const eased = 1 - Math.pow(1 - p, 3);
    el.textContent = fmt(Math.round(from + (to - from) * eased));
    if (p < 1) requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
}

// ---------------- ring ----------------
export function ring(pct, opts = {}) {
  const size = opts.size || 84, stroke = opts.stroke || 9, r = (size - stroke) / 2, c = 2 * Math.PI * r;
  const color = opts.color || "var(--teal)";
  const wrap = h("div", { class: "ring-wrap", style: { width: size + "px", height: size + "px" } });
  const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.setAttribute("width", size); svg.setAttribute("height", size); svg.setAttribute("viewBox", `0 0 ${size} ${size}`);
  svg.style.transform = "rotate(-90deg)";
  const bg = document.createElementNS(svg.namespaceURI, "circle");
  bg.setAttribute("cx", size / 2); bg.setAttribute("cy", size / 2); bg.setAttribute("r", r);
  bg.setAttribute("fill", "none"); bg.setAttribute("stroke", "var(--grey-soft)"); bg.setAttribute("stroke-width", stroke);
  const fg = document.createElementNS(svg.namespaceURI, "circle");
  fg.setAttribute("cx", size / 2); fg.setAttribute("cy", size / 2); fg.setAttribute("r", r);
  fg.setAttribute("fill", "none"); fg.setAttribute("stroke", color); fg.setAttribute("stroke-width", stroke);
  fg.setAttribute("stroke-linecap", "round"); fg.setAttribute("stroke-dasharray", c);
  fg.setAttribute("stroke-dashoffset", c);
  svg.appendChild(bg); svg.appendChild(fg);
  wrap.appendChild(svg);
  const label = h("div", { class: "ring-num", style: { fontSize: (size * 0.26) + "px" } }, "0");
  wrap.appendChild(label);
  requestAnimationFrame(() => { fg.style.strokeDashoffset = String(c - (Math.max(0, Math.min(100, pct)) / 100) * c); });
  countUp(label, Math.round(pct), { fmt: (n) => n + "%" });
  return wrap;
}

// ---------------- sparkline ----------------
export function sparkline(values, opts = {}) {
  const w = opts.w || 88, hgt = opts.h || 28;
  if (!values || !values.length) return h("svg", { width: w, height: hgt });
  const max = Math.max(...values, 1), min = Math.min(...values, 0);
  const span = Math.max(1, max - min);
  const step = w / (values.length - 1 || 1);
  const pts = values.map((v, i) => [i * step, hgt - ((v - min) / span) * (hgt - 4) - 2]);
  const d = pts.map((p, i) => (i === 0 ? "M" : "L") + p[0].toFixed(1) + "," + p[1].toFixed(1)).join(" ");
  const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.setAttribute("width", w); svg.setAttribute("height", hgt); svg.setAttribute("class", "spark");
  const path = document.createElementNS(svg.namespaceURI, "path");
  path.setAttribute("d", d); path.setAttribute("fill", "none"); path.setAttribute("stroke", opts.color || "var(--teal)");
  path.setAttribute("stroke-width", "2"); path.setAttribute("stroke-linecap", "round"); path.setAttribute("stroke-linejoin", "round");
  svg.appendChild(path);
  return svg;
}

// ---------------- tooltip ----------------
let tipEl = null;
function ensureTip() { if (!tipEl) tipEl = document.getElementById("tip"); return tipEl; }
export function bindTooltip(el) {
  let timer = null;
  const show = () => {
    const text = el.getAttribute("data-tip"); if (!text) return;
    const title = el.getAttribute("data-tip-title");
    const tip = ensureTip();
    tip.innerHTML = "";
    if (title) tip.appendChild(h("b", {}, title));
    tip.appendChild(document.createTextNode(text));
    const r = el.getBoundingClientRect();
    tip.classList.add("show");
    tip.style.left = "0px"; tip.style.top = "0px";
    const tw = tip.offsetWidth, th = tip.offsetHeight;
    let left = r.left + r.width / 2 - tw / 2; let top = r.top - th - 10;
    left = Math.max(8, Math.min(window.innerWidth - tw - 8, left));
    if (top < 4) top = r.bottom + 10;
    tip.style.left = left + "px"; tip.style.top = top + "px";
  };
  const hide = () => { const tip = ensureTip(); tip.classList.remove("show"); };
  el.addEventListener("mouseenter", () => { timer = setTimeout(show, 250); });
  el.addEventListener("mouseleave", () => { clearTimeout(timer); hide(); });
  el.addEventListener("focus", show);
  el.addEventListener("blur", hide);
}
export function bindTooltips(root = document) {
  root.querySelectorAll("[data-tip]").forEach(bindTooltip);
}

// ---------------- toast ----------------
export function toast(text, tone = "info") {
  const root = document.getElementById("toast-root");
  const iconName = { success: "check", error: "x", warn: "alert", info: "info" }[tone] || "info";
  const t = h("div", { class: `toast toast-${tone}` }, icon(iconName), h("span", {}, text));
  root.appendChild(t);
  setTimeout(() => { t.classList.add("leaving"); setTimeout(() => t.remove(), 240); }, 3600);
}

// ---------------- modal ----------------
export function modal({ title, body, actions, wide }) {
  return new Promise((resolve) => {
    const root = document.getElementById("overlay-root");
    root.style.pointerEvents = "auto";
    const scrim = h("div", { class: "overlay-scrim" });
    const box = h("div", { class: "modal" + (wide ? " wide" : "") });
    function close(result) {
      scrim.classList.remove("show"); box.classList.remove("show");
      setTimeout(() => { scrim.remove(); box.remove(); root.style.pointerEvents = "none"; }, 200);
      resolve(result);
    }
    const head = h("div", { class: "modal-head" }, h("div", { class: "modal-title" }, title), iconBtn("x", { onClick: () => close(null), ariaLabel: "Kapat" }));
    const bodyEl = h("div", { class: "modal-body" });
    if (body instanceof Node) bodyEl.appendChild(body); else if (typeof body === "string") bodyEl.textContent = body;
    const actionsEl = h("div", { class: "modal-actions" });
    (actions || []).forEach((a) => actionsEl.appendChild(btn(a.text, { variant: a.variant || "ghost", onClick: () => close(a.value) })));
    box.appendChild(head); box.appendChild(bodyEl); if (actions?.length) box.appendChild(actionsEl);
    scrim.addEventListener("click", () => close(null));
    root.appendChild(scrim); root.appendChild(box);
    requestAnimationFrame(() => { scrim.classList.add("show"); box.classList.add("show"); });
    const onKey = (e) => { if (e.key === "Escape") { close(null); document.removeEventListener("keydown", onKey); } };
    document.addEventListener("keydown", onKey);
    const focusable = box.querySelector("input,textarea,select,button");
    if (focusable) setTimeout(() => focusable.focus(), 60);
    box._close = close;
  });
}
export function confirmDialog(text, opts = {}) {
  return modal({
    title: opts.title || "Emin misin?",
    body: h("p", { style: { fontSize: "15px", lineHeight: "1.6" } }, text),
    actions: [{ text: opts.cancelText || "Vazgeç", value: false, variant: "ghost" }, { text: opts.okText || "Onayla", value: true, variant: opts.danger ? "danger" : "primary" }],
  });
}

// ---------------- confetti ----------------
export function confetti() {
  if (document.documentElement.classList.contains("no-anim")) return;
  const root = document.getElementById("confetti-root");
  const canvas = h("canvas", { width: window.innerWidth, height: window.innerHeight, style: { position: "fixed", inset: "0" } });
  root.appendChild(canvas);
  const ctx = canvas.getContext("2d");
  const colors = ["#0F766E", "#14B8A6", "#15803D", "#CA8A04"];
  const parts = Array.from({ length: 80 }, () => ({
    x: window.innerWidth / 2 + (Math.random() - 0.5) * 200, y: window.innerHeight * 0.35,
    vx: (Math.random() - 0.5) * 9, vy: -Math.random() * 9 - 3, g: 0.32,
    size: 4 + Math.random() * 4, color: colors[Math.floor(Math.random() * colors.length)], rot: Math.random() * Math.PI, vr: (Math.random() - 0.5) * 0.3,
  }));
  const start = performance.now();
  function frame(t) {
    const elapsed = t - start;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    for (const p of parts) {
      p.vy += p.g; p.x += p.vx; p.y += p.vy; p.rot += p.vr;
      ctx.save(); ctx.translate(p.x, p.y); ctx.rotate(p.rot); ctx.fillStyle = p.color;
      ctx.fillRect(-p.size / 2, -p.size / 2, p.size, p.size * 0.6); ctx.restore();
    }
    if (elapsed < 1200) requestAnimationFrame(frame); else canvas.remove();
  }
  requestAnimationFrame(frame);
}

// ---------------- misc ----------------
export function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}
export function toneForHealth(level) { return level === "iyi" ? "green" : level === "dikkat" ? "yellow" : "red"; }
export function toneForSeverity(sev) { return sev === "kritik" ? "red" : sev === "dikkat" ? "yellow" : "grey"; }

// ---------------- scheduled task display names (E) ----------------
// The backend may still hand us the raw scheduled-task folder slug (SKILL.md frontmatter `name`, e.g.
// "aksam-analizi") instead of a Turkish title. Known ids get a fixed Turkish label; anything else falls
// back to "dashes -> spaces, capitalize first letter" so a brand-new scheduled task never shows raw kebab-case.
const SCHEDULED_TASK_LABELS = {
  "aksam-analizi": "Akşam analizi",
  "tam-gaz": "Tam Gaz işçisi",
};
export function scheduledTaskLabel(name) {
  if (!name) return "";
  if (SCHEDULED_TASK_LABELS[name]) return SCHEDULED_TASK_LABELS[name];
  if (!/[- ]/.test(name) && /[A-ZÇĞİÖŞÜ]/.test(name)) return name; // already looks like a title
  const spaced = name.replace(/[-_]+/g, " ").trim();
  return spaced.charAt(0).toLocaleUpperCase("tr-TR") + spaced.slice(1);
}
export function progressBar(pct, opts = {}) {
  const bar = h("div", { class: "pbar" + (opts.red ? " red" : "") });
  const span = h("span", { style: { width: "0%" } });
  bar.appendChild(span);
  requestAnimationFrame(() => { span.style.width = Math.max(0, Math.min(100, pct)) + "%"; });
  return bar;
}
