// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../../bridge.js";
import { h, icon, chip, btn, ring, progressBar, empty, skeleton, toneForHealth, fmtTime, fmtRelative, bindTooltips } from "../../ui.js";

const KANBAN_COLS = [["hazir", "Hazır"], ["verildi", "Verildi"], ["kontrol", "Kontrol"], ["tamam", "Tamam"], ["iptal", "İptal"]];

export default function mountProje(root, route, helpers) {
  let disposed = false;
  const name = decodeURIComponent(route.segs[2] || "");
  let section = route.query?.s || "genel";
  root.appendChild(skeleton(6, 24));
  load();
  return () => { disposed = true; };

  async function load() {
    try {
      const detail = await request("getProjectDetail", { name });
      if (disposed) return;
      render(detail);
    } catch { root.innerHTML = ""; root.appendChild(empty("alert", "Proje bulunamadı.")); }
  }

  function render(detail) {
    root.innerHTML = "";
    const p = detail.project, health = detail.health;
    root.appendChild(h("button", { class: "btn btn-ghost btn-sm", style: { alignSelf: "flex-start" }, onclick: () => location.hash = "#/beyin/genel" }, icon("chevron-left"), "Beyin'e dön"));

    const header = h("div", { class: "card", style: { "--i": 0, display: "flex", gap: "24px", alignItems: "center", flexWrap: "wrap" } });
    // Halka ilerleme değil sağlık puanıdır (ilerleme sağdaki "Bitti tanımı"); etiketsiz kalınca ikisi karıştırılıyordu.
    const saglikTip = "100 − 25×kritik − 6×dikkat − 1×bilgi; bu projenin açık sorunlarından hesaplanır. İlerleme değildir: ilerleme sağdaki \"Bitti tanımı\"dır.";
    header.appendChild(h("div", { class: "hero-ring-card", "data-tip": saglikTip, "data-tip-title": "Proje sağlığı" },
      ring(health?.score ?? p.health.score, { color: colorFor(health?.level || p.health.level) }), h("div", { class: "hero-ring-label" }, "Sağlık ", icon("info", ""))));
    const mid = h("div", { style: { flex: 1, minWidth: "260px" } });
    mid.appendChild(h("div", { style: { display: "flex", alignItems: "center", gap: "10px" } }, p.odak ? h("span", { style: { color: "var(--yellow)" } }, "★") : null, h("div", { style: { fontWeight: 800, fontSize: "24px" } }, p.name), chip(p.durum, "grey")));
    mid.appendChild(h("div", { style: { fontSize: "14.5px", color: "var(--muted)", marginTop: "6px" } }, "Sıradaki: " + (health?.nextStep || p.sonrakiAdimKisa)));
    if (p.kilit) mid.appendChild(h("div", { style: { marginTop: "8px" } }, chip(`Kilit: ${p.kilit.holder} · ${p.kilit.hours ?? "?"} sa`, p.kilit.stale ? "red" : "yellow")));
    header.appendChild(mid);
    const rightBox = h("div", { style: { minWidth: "220px" } });
    rightBox.appendChild(h("div", { style: { display: "flex", justifyContent: "space-between", fontSize: "12.5px", color: "var(--muted)" } }, h("span", {}, "Bitti tanımı"), h("span", {}, `${p.bitti.done}/${p.bitti.total}`)));
    rightBox.appendChild(progressBar(p.bitti.pct));
    if (p.daysToKill != null) { rightBox.appendChild(h("div", { style: { display: "flex", justifyContent: "space-between", fontSize: "12.5px", color: "var(--muted)", marginTop: "10px" } }, h("span", {}, "Bırakma tarihi"), h("span", {}, `${p.daysToKill} gün`))); rightBox.appendChild(progressBar(Math.max(0, 100 - p.daysToKill * 3), { red: p.daysToKill <= 7 })); }
    header.appendChild(rightBox);
    root.appendChild(header);
    bindTooltips(header);

    const tabs = h("div", { class: "tabs", style: { "--i": 1 } });
    const TABS = [["genel", "Genel"], ["kayit", `Kayıt (${detail.handoffs.length})`], ["kararlar", `Kararlar (${detail.decisions.length})`], ["gorevler", `Görevler (${detail.tasks.length})`], ["acik", `Açık kalanlar (${detail.openItems.length})`], ["dosyalar", `Dosyalar (${detail.files.length})`]];
    TABS.forEach(([id, label]) => tabs.appendChild(h("button", { class: "tab" + (section === id ? " active" : ""), onclick: () => { section = id; render(detail); } }, label)));
    root.appendChild(tabs);

    const content = h("div", { style: { "--i": 2 } });
    if (section === "genel") content.appendChild(genelTab(p, health, detail));
    else if (section === "kayit") content.appendChild(timeline(detail.handoffs, "Henüz devir notu yok."));
    else if (section === "kararlar") content.appendChild(timeline(detail.decisions, "Henüz karar yok."));
    else if (section === "gorevler") content.appendChild(kanban(detail.tasks));
    else if (section === "acik") content.appendChild(openItems(detail.openItems));
    else if (section === "dosyalar") content.appendChild(filesList(detail.files));
    root.appendChild(content);
  }
  function colorFor(level) { return level === "iyi" ? "var(--green)" : level === "dikkat" ? "var(--yellow)" : "var(--red)"; }

  function genelTab(p, health, detail) {
    const wrap = h("div", { class: "grid grid-2" });
    wrap.appendChild(h("div", { class: "card" }, h("div", { class: "card-head" }, icon("doc"), h("div", { class: "card-title" }, "Bitti tanımı")),
      h("div", { class: "vstack", style: { gap: "8px" } }, ...p.bitti.items.map((it) => h("div", { class: "todo-row" }, h("div", { class: "todo-circle" + (it.done ? " done" : "") }, it.done ? icon("check") : null), h("div", { class: "todo-text" + (it.done ? " done" : "") }, it.text))))));
    const rightCol = h("div", { class: "vstack", style: { gap: "20px" } });
    rightCol.appendChild(h("div", { class: "card" }, h("div", { class: "card-head" }, icon("alert"), h("div", { class: "card-title" }, "Engel")), h("div", {}, p.engel || "Yok")));
    if (health?.reasons?.length) rightCol.appendChild(h("div", { class: "card" }, h("div", { class: "card-head" }, icon("shield"), h("div", { class: "card-title" }, "Sağlık nedenleri")), ...health.reasons.map((r) => h("div", { class: "bullet" }, r))));
    if (detail.problems.length) rightCol.appendChild(h("div", { class: "card" }, h("div", { class: "card-head" }, icon("alert"), h("div", { class: "card-title" }, "Sorunlar")), ...detail.problems.map((pr) => h("div", { class: "problem-row", style: { cursor: "pointer" }, onclick: () => helpers.openTarget(pr.target) }, h("span", { class: "problem-dot", style: { background: "var(--yellow)" } }), h("div", { class: "problem-body" }, h("div", { class: "problem-title" }, pr.title))))));
    wrap.appendChild(rightCol);
    return wrap;
  }
  function timeline(items, emptyText) {
    if (!items.length) return empty("clock", emptyText);
    const wrap = h("div", { class: "vstack", style: { gap: "16px" } });
    items.forEach((it) => {
      const card = h("div", { class: "card", style: { boxShadow: "none", background: "var(--surface-2)" } });
      card.appendChild(h("div", { style: { display: "flex", gap: "10px", alignItems: "center" } }, chip(it.tool || "—", "grey"), h("div", { style: { fontWeight: 800 } }, `${it.date}${it.time ? " " + it.time : ""} — ${it.title}`)));
      it.fields.forEach((f) => card.appendChild(h("div", { style: { fontSize: "13.5px", marginTop: "6px" } }, h("b", {}, f.key + ": "), f.value)));
      wrap.appendChild(card);
    });
    return wrap;
  }
  function kanban(tasks) {
    const board = h("div", { class: "kanban" });
    KANBAN_COLS.forEach(([k, label]) => {
      const col = h("div", { class: "kanban-col" }, h("div", { class: "kanban-col-head" }, label, h("span", {}, String(tasks.filter((t) => t.durum === k).length))));
      tasks.filter((t) => t.durum === k).forEach((t) => col.appendChild(h("div", { class: "kanban-card" }, `${t.kimlik} · ${t.title}`)));
      board.appendChild(col);
    });
    return board;
  }
  function openItems(items) {
    if (!items.length) return empty("check", "Açık kalan bir şey yok.");
    return h("div", { class: "card" }, ...items.map((w) => h("div", { class: "bullet", style: { marginBottom: "8px" } }, w.text)));
  }
  function filesList(files) {
    if (!files.length) return empty("file", "Dosya yok.");
    const table = h("table", { class: "dtable" }, h("thead", {}, h("tr", {}, h("th", {}, "Dosya"), h("th", {}, "Tür"), h("th", {}, "Değişiklik"))),
      h("tbody", {}, ...files.map((f) => h("tr", { class: "rowhover" }, h("td", {}, f.path), h("td", {}, f.kind), h("td", {}, fmtRelative(f.modified))))));
    return h("div", { class: "card" }, table);
  }
}
