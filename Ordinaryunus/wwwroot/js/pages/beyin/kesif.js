// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../../bridge.js";
import * as store from "../../store.js";
import { h, icon, chip, btn, empty, skeleton, escapeHtml, bindTooltips } from "../../ui.js";

export default function mountKesif(root, route, helpers) {
  let disposed = false;
  let tree = null, filterText = "";
  let history = [], histIndex = -1;
  const openIds = new Set();

  const layout = h("div", { class: "explorer" });
  const treePane = h("div", { class: "tree-pane" },
    h("div", { class: "tree-search" }, h("input", { class: "field", id: "tree-filter", placeholder: "Ara (Ctrl+F)" })),
    h("div", { class: "tree-body" }, skeleton(8)));
  const readerPane = h("div", { class: "reader-pane" }, h("div", { class: "reader-body" }, skeleton(6, 20)));
  const infoPane = h("div", { class: "info-pane" });
  layout.appendChild(treePane); layout.appendChild(readerPane); layout.appendChild(infoPane);
  root.appendChild(layout);

  const filterInput = treePane.querySelector("#tree-filter");
  filterInput.addEventListener("input", () => { filterText = filterInput.value.toLowerCase(); renderTree(); });
  function onKey(e) { if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "f") { e.preventDefault(); filterInput.focus(); }
    if (e.altKey && e.key === "ArrowLeft") { e.preventDefault(); goHistory(-1); }
    if (e.altKey && e.key === "ArrowRight") { e.preventDefault(); goHistory(1); } }
  document.addEventListener("keydown", onKey);

  load();
  return () => { disposed = true; document.removeEventListener("keydown", onKey); };

  async function load() {
    try {
      const res = await request("getBrainTree");
      if (disposed) return;
      tree = res.nodes;
      openIds.add(tree[1]?.id); // open Projeler by default
      renderTree();
      const initPath = route.query?.path;
      if (initPath) openNote(decodeURIComponent(initPath), route.query.a);
      else readerPane.querySelector(".reader-body").replaceChildren(empty("doc", "Soldan bir not seç."));
    } catch { treePane.querySelector(".tree-body").replaceChildren(empty("alert", "Ağaç yüklenemedi.")); }
  }

  function renderTree() {
    const body = treePane.querySelector(".tree-body");
    body.innerHTML = "";
    const matches = (node) => !filterText || node.label.toLowerCase().includes(filterText) || (node.children || []).some(matches);
    (tree || []).filter(matches).forEach((n) => body.appendChild(renderNode(n, 0)));
  }
  function renderNode(node, depth) {
    const wrap = h("div", {});
    const hasChildren = node.children && node.children.length > 0;
    const isOpen = openIds.has(node.id) || !!filterText;
    const row = h("div", { class: "tree-node" + (isOpen ? " open" : ""), style: { paddingLeft: 8 + depth * 4 + "px" } });
    row.appendChild(hasChildren ? icon("chevron-right", "chev") : h("span", { style: { width: "14px", display: "inline-block" } }));
    row.appendChild(icon(iconFor(node), ""));
    row.appendChild(h("span", { class: "lbl" }, node.label));
    if (node.count != null) row.appendChild(h("span", { style: { fontSize: "11px", color: "var(--faint)" } }, String(node.count)));
    if (node.badge) row.appendChild(chip(node.badge.text, node.badge.tone, { noDot: true }));
    if (node.health) row.appendChild(h("span", { style: { width: "7px", height: "7px", borderRadius: "50%", background: node.health === "iyi" ? "var(--green)" : node.health === "dikkat" ? "var(--yellow)" : "var(--red)" } }));
    row.addEventListener("click", () => {
      if (hasChildren) { if (openIds.has(node.id)) openIds.delete(node.id); else openIds.add(node.id); renderTree(); }
      if (node.target) selectNode(node);
    });
    wrap.appendChild(row);
    if (hasChildren) {
      const childWrap = h("div", { class: "tree-children" + (isOpen ? " open" : "") });
      node.children.forEach((c) => childWrap.appendChild(renderNode(c, depth + 1)));
      wrap.appendChild(childWrap);
    }
    return wrap;
  }
  function iconFor(node) {
    const map = { home: "doc", folder: "folder", project: "projeler", doc: "doc", kayit: "commit", karar: "sparkle", task: "check", role: "sirketim", idea: "sparkle", area: "folder", research: "search", raw: "folder", journal: "doc", archive: "folder", system: "cpu", template: "doc", inbox: "inbox", ledger: "clock", file: "file", image: "file", code: "file", warning: "alert" };
    return map[node.icon] || "doc";
  }

  function selectNode(node) {
    if (node.target.type === "note") openNote(node.target.path, node.target.anchor);
    else if (node.target.type === "project") helpers.openTarget(node.target);
    else if (node.target.type === "ledger") openLedgerDay(node.target.day);
  }

  function goHistory(dir) {
    const ni = histIndex + dir;
    if (ni < 0 || ni >= history.length) return;
    histIndex = ni;
    openNote(history[histIndex], null, true);
  }
  async function openNote(path, anchor, fromHistory) {
    if (!fromHistory) { history = history.slice(0, histIndex + 1); history.push(path); histIndex = history.length - 1; }
    readerPane.querySelector(".reader-body")?.remove();
    readerPane.querySelector(".reader-head")?.remove();
    readerPane.appendChild(h("div", { class: "reader-body" }, skeleton(6, 20)));
    infoPane.innerHTML = "";
    let note;
    try { note = await request("getNote", { path }); } catch (e) {
      readerPane.querySelector(".reader-body").replaceChildren(empty("doc", "Bu not önizlemede yok."));
      return;
    }
    if (disposed) return;
    readerPane.innerHTML = "";
    readerPane.appendChild(h("div", { class: "reader-head" },
      h("div", { class: "reader-breadcrumb" }, note.breadcrumb.join(" / ")),
      h("div", { class: "reader-title" }, note.title)));
    const body = h("div", { class: "reader-body" }, h("div", { class: "prose", html: note.html }));
    readerPane.appendChild(body);
    body.querySelectorAll("a.wl").forEach((a) => a.addEventListener("click", (e) => { e.preventDefault(); openNote(a.dataset.note); }));
    body.querySelectorAll("a.ext").forEach((a) => a.addEventListener("click", (e) => { e.preventDefault(); request("openUrl", { url: a.dataset.url }); }));
    renderInfo(note);
    bindTooltips(readerPane);
  }
  async function openLedgerDay(day) {
    readerPane.innerHTML = "";
    readerPane.appendChild(h("div", { class: "reader-head" }, h("div", { class: "reader-breadcrumb" }, "İstek defteri"), h("div", { class: "reader-title" }, day)));
    const body = h("div", { class: "reader-body" }, skeleton(4));
    readerPane.appendChild(body);
    try {
      const res = await request("getLedger", { day, limit: 100 });
      body.innerHTML = "";
      const entries = res.days[0]?.entries || [];
      if (!entries.length) body.appendChild(empty("message", "O gün istek yok."));
      else entries.forEach((e) => body.appendChild(h("div", { class: "work-row" }, h("div", { class: "work-dot", style: { background: "var(--teal-mid)" } }), h("div", { class: "work-body" }, h("div", { class: "work-title" }, e.text), h("div", { class: "work-meta" }, `${e.tool} · ${e.project}`)))));
    } catch { body.replaceChildren(empty("alert", "Yüklenemedi.")); }
  }

  function renderInfo(note) {
    infoPane.innerHTML = "";
    infoPane.appendChild(h("div", { class: "section-title" }, "Özellikler"));
    infoPane.appendChild(kv("Tür", note.kind));
    infoPane.appendChild(kv("Kelime", String(note.words)));
    infoPane.appendChild(kv("Değişiklik", new Date(note.modified).toLocaleString("tr-TR")));
    if (note.project) infoPane.appendChild(kv("Proje", note.project));
    infoPane.appendChild(h("div", { class: "section-title", style: { marginTop: "6px" } }, "Bağlantılar"));
    infoPane.appendChild(kv("Giden", String(note.outLinks.length)));
    infoPane.appendChild(kv("Gelen", String(note.backLinks.length)));
    note.outLinks.forEach((l) => infoPane.appendChild(h("div", { class: "link-row", style: l.broken ? { color: "var(--red)" } : {} }, icon(l.broken ? "alert" : "link"), l.label, l.broken ? " (kırık)" : "")));
    if (note.problems.length) { infoPane.appendChild(h("div", { class: "section-title", style: { marginTop: "6px" } }, "Bu notun sorunları"));
      note.problems.forEach((p) => infoPane.appendChild(h("div", { class: "link-row" }, icon("alert"), p.title))); }
    infoPane.appendChild(h("div", { style: { display: "flex", gap: "8px", marginTop: "12px", flexWrap: "wrap" } },
      btn("Obsidian'da aç", { sm: true, variant: "secondary", icon: "external", onClick: () => request("openInObsidian", { path: note.path }) }),
      btn("Klasörde göster", { sm: true, variant: "ghost", icon: "folder", onClick: () => request("openFolder", { which: "notKonumu", path: note.path }) }),
      note.role ? btn("İş ver", { sm: true, variant: "primary", onClick: () => helpers.openGiveWork({ role: note.title }) }) : null));
  }
  function kv(k, v) { return h("div", { class: "info-kv" }, h("span", {}, k), h("b", {}, v)); }
}
