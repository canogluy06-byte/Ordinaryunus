// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../bridge.js";
import * as store from "../store.js";
import { h, icon, chip, btn, progressBar, toneForHealth, bindTooltips } from "../ui.js";

const DURUM_TONE = { aktif: "teal", beklemede: "grey", bitti: "green", donduruldu: "red" };

export default function projelerPage(helpers) {
  let el, unsub;
  function mount(root) { el = root; render(store.get("snapshot")); unsub = store.on("snapshot", render); }
  function unmount() { unsub?.(); }
  function update() { render(store.get("snapshot")); }

  function render(snap) {
    el.innerHTML = "";
    if (!snap) { el.appendChild(h("div", { class: "empty" }, "Yükleniyor…")); return; }
    el.appendChild(h("div", { class: "eyebrow" }, "PROJELER"));
    el.appendChild(h("div", { class: "page-title" }, "Tüm projeler"));
    if (snap.limit.full) el.appendChild(h("div", { class: "banner warn" }, icon("alert"), h("span", {}, "3/3 dolu. Yeni proje yok; önce birini bitir, beklet ya da dondur."), h("div", { class: "spacer" })));

    const order = { aktif: 0, beklemede: 1, bitti: 2, donduruldu: 3 };
    const list = [...snap.projects].sort((a, b) => (order[a.durum] - order[b.durum]) || (b.odak - a.odak));
    const grid = h("div", { class: "grid grid-2", style: { "--i": 1 } });
    list.forEach((p) => grid.appendChild(card(p)));
    el.appendChild(grid);
    bindTooltips(el);
  }

  function card(p) {
    const box = h("div", { class: "card hoverable" });
    box.appendChild(h("div", { style: { display: "flex", alignItems: "center", gap: "10px" } },
      p.odak ? h("span", { style: { color: "var(--yellow)" } }, "★") : null,
      h("div", { style: { fontWeight: 800, fontSize: "18px", flex: 1 } }, p.name),
      chip(p.durum, DURUM_TONE[p.durum] || "grey"),
      chip(p.health.level, toneForHealth(p.health.level))));
    box.appendChild(h("div", { style: { fontSize: "14px", color: "var(--muted)", margin: "10px 0" } }, p.sonrakiAdimKisa || p.sonrakiAdim));
    box.appendChild(h("div", { style: { display: "flex", justifyContent: "space-between", fontSize: "12.5px", color: "var(--muted)", marginBottom: "4px" } }, h("span", {}, "Bitti tanımı"), h("span", {}, `${p.bitti.done}/${p.bitti.total}`)));
    box.appendChild(progressBar(p.bitti.pct));
    if (p.daysToKill != null) {
      box.appendChild(h("div", { style: { display: "flex", justifyContent: "space-between", fontSize: "12.5px", color: "var(--muted)", margin: "10px 0 4px" } }, h("span", {}, "Bırakma tarihine"), h("span", {}, `${p.daysToKill} gün`)));
      box.appendChild(progressBar(Math.max(0, 100 - p.daysToKill * 3), { red: p.daysToKill <= 7 }));
    }
    if (p.engel) box.appendChild(h("div", { style: { fontSize: "12.5px", color: "var(--muted)", marginTop: "10px", fontStyle: "italic" } }, "Engel: " + p.engel));
    if (p.kilit) box.appendChild(h("div", { style: { marginTop: "10px" } }, chip(`Kilit: ${p.kilit.holder} · ${p.kilit.hours ?? "?"} sa`, p.kilit.stale ? "red" : "yellow")));
    const actions = h("div", { style: { display: "flex", gap: "8px", marginTop: "14px", flexWrap: "wrap" } },
      btn("Beyin'de aç", { sm: true, variant: "secondary", icon: "beyin", onClick: () => helpers.openTarget({ type: "project", name: p.name, section: "genel" }) }),
      btn("Obsidian'da aç", { sm: true, variant: "ghost", icon: "external", onClick: () => request("openInObsidian", { path: p.path }) }),
      p.klasorVar ? btn("Klasörü aç", { sm: true, variant: "ghost", icon: "folder", onClick: () => request("openFolder", { which: "projeKlasoru", project: p.name }) }) : null);
    box.appendChild(actions);
    return box;
  }

  return { id: "projeler", mount, update, unmount };
}
