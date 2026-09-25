// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../bridge.js";
import { navigate, openTarget } from "../router.js";
import { h, icon, escapeHtml } from "../ui.js";

const PAGES = [
  { id: "masam", label: "Masam", icon: "masam" }, { id: "beyin", label: "Beyin", icon: "beyin" },
  { id: "sirketim", label: "Şirketim", icon: "sirketim" }, { id: "gecmis", label: "Geçmiş", icon: "gecmis" },
  { id: "basvurular", label: "İş Başvuruları", icon: "basvurular" }, { id: "projeler", label: "Projeler", icon: "projeler" },
  { id: "kestirmeler", label: "Kestirmeler", icon: "kestirmeler" }, { id: "ayarlar", label: "Ayarlar", icon: "ayarlar" },
];

let openState = false;
let currentClose = null;
export function isPaletteOpen() { return openState; }
// See focus.js closeFocus() for why screenshot mode needs this: a synthetic "shot" sequence must be able to
// dismiss an overlay itself opened, since there is no real click to do it.
export function closePalette() { currentClose?.(); }

export function openPalette(initialQuery = "") {
  if (openState) return;
  openState = true;
  const root = document.getElementById("overlay-root");
  root.style.pointerEvents = "auto";
  const scrim = h("div", { class: "overlay-scrim" });
  const input = h("input", { class: "palette-input", placeholder: "Sayfa ara ya da bir şey yaz…", value: initialQuery });
  const list = h("div", { class: "palette-list" });
  const box = h("div", { class: "palette-modal" }, input, list);
  let sel = 0, items = [];

  function close() {
    openState = false;
    currentClose = null;
    scrim.classList.remove("show"); box.classList.remove("show");
    setTimeout(() => { scrim.remove(); box.remove(); root.style.pointerEvents = "none"; }, 180);
    document.removeEventListener("keydown", onKey);
  }
  currentClose = close;
  function renderList(results) {
    items = results;
    list.innerHTML = "";
    if (!items.length) { list.appendChild(h("div", { class: "palette-item" }, "Sonuç yok.")); return; }
    items.forEach((it, i) => {
      // A4: page shortcuts are a single line; note results get a second, muted line (folder/path, and
      // where it matched) so two notes named the same ("Kasa" x4) are told apart instead of looking identical.
      const body = it.meta
        ? h("div", { style: { flex: 1, minWidth: 0 } }, h("div", { style: { overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" } }, it.label), h("div", { style: { fontSize: "12px", color: "var(--muted)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }, html: it.meta }))
        : h("span", {}, it.label);
      const row = h("div", { class: "palette-item" + (i === sel ? " sel" : ""), onclick: () => choose(i) }, icon(it.icon || "doc"), body);
      list.appendChild(row);
    });
  }
  function choose(i) {
    const it = items[i];
    if (!it) return;
    close();
    if (it.page) navigate("#/" + it.page);
    else if (it.target) openTarget(it.target);
  }
  async function search(q) {
    const base = PAGES.filter((p) => p.label.toLowerCase().includes(q.toLowerCase())).map((p) => ({ label: p.label, icon: p.icon, page: p.id }));
    if (!q) { sel = 0; renderList(base); return; }
    try {
      const res = await request("searchVault", { q, limit: 20 });
      // A4: several results can point at the very same note (one per matching line/heading) and several
      // different notes can share a title ("Kasa" x4) — dedupe by path and show the folder + matching
      // line (snippetHtml, already C#-sanitised) so each row is tellable apart at a glance.
      const seen = new Set();
      const searchItems = [];
      for (const r of res.results) {
        const dedupeKey = r.path || `${r.kind}:${r.title}`;
        if (seen.has(dedupeKey)) continue;
        seen.add(dedupeKey);
        const folder = r.path ? (r.path.includes("/") ? r.path.slice(0, r.path.lastIndexOf("/")) : "Kök") : (r.kind === "istek" ? "İstek defteri" : "");
        const meta = [folder ? escapeHtml(folder) : null, r.snippetHtml].filter(Boolean).join(" · ");
        searchItems.push({ label: r.title, icon: r.kind === "istek" ? "message" : "doc", target: r.target, meta });
      }
      sel = 0; renderList([...base, ...searchItems]);
    } catch { sel = 0; renderList(base); }
  }
  input.addEventListener("input", () => search(input.value));
  function onKey(e) {
    if (e.key === "Escape") { close(); return; }
    if (e.key === "ArrowDown") { e.preventDefault(); sel = Math.min(items.length - 1, sel + 1); renderList(items); }
    if (e.key === "ArrowUp") { e.preventDefault(); sel = Math.max(0, sel - 1); renderList(items); }
    if (e.key === "Enter") { e.preventDefault(); choose(sel); }
  }
  document.addEventListener("keydown", onKey);
  scrim.addEventListener("click", close);
  root.appendChild(scrim); root.appendChild(box);
  requestAnimationFrame(() => { scrim.classList.add("show"); box.classList.add("show"); input.focus(); });
  search(initialQuery);
}
