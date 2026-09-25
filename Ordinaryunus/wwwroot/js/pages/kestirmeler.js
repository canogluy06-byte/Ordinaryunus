// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../bridge.js";
import * as store from "../store.js";
import { h, icon, chip, toast, fmtRelative } from "../ui.js";
import { openScriptOutput } from "../overlays/dialogs.js";

// A13: each group gets its own accent so the grid reads at a glance instead of all-identical grey tiles.
const GROUP_ACCENT = { ac: "var(--teal)", bakim: "var(--slate)" };

export default function kestirmelerPage(helpers) {
  let el, unsub;
  function mount(root) { el = root; render(store.get("snapshot")); unsub = store.on("snapshot", render); }
  function unmount() { unsub?.(); }
  function update() { render(store.get("snapshot")); }

  function render(snap) {
    el.innerHTML = "";
    if (!snap) { el.appendChild(h("div", { class: "empty" }, "Yükleniyor…")); return; }
    el.appendChild(h("div", { class: "eyebrow" }, "KESTİRMELER"));
    el.appendChild(h("div", { class: "page-title" }, "Tek tık"));

    const ac = snap.shortcuts.filter((s) => s.group === "ac");
    const bakim = snap.shortcuts.filter((s) => s.group === "bakim");
    el.appendChild(h("div", { class: "card", style: { "--i": 1 } },
      h("div", { class: "card-head" }, icon("bolt"), h("div", { class: "card-title" }, "Aç")),
      h("div", { class: "grid grid-4" }, ...ac.map(tile))));
    el.appendChild(h("div", { class: "card", style: { "--i": 2 } },
      h("div", { class: "card-head" }, icon("cpu"), h("div", { class: "card-title" }, "Bakım işleri")),
      h("div", { class: "grid grid-3" }, ...bakim.map(tile))));
  }

  function tile(s) {
    const accent = GROUP_ACCENT[s.group] || "var(--teal)";
    // hazir === false: kestirmenin dayandığı beceri kurulu değil; kutucuk soluk görünür, tıklayınca nasıl kurulacağı söylenir.
    const soluk = s.hazir === false;
    const box = h("div", { class: "card hoverable shortcut-tile", title: soluk ? "Kurulu değil. Ayrıntı: kasa-araclari/KURULUM.md 3.6" : null, style: { boxShadow: "none", background: "var(--surface-2)", cursor: "pointer", textAlign: "center", padding: "22px 14px 16px", "--tile-accent": soluk ? "var(--grey-soft)" : accent, opacity: soluk ? "0.6" : null } },
      h("div", { class: "shortcut-icon" }, icon(s.icon || "bolt", "")),
      h("div", { style: { fontWeight: 700, marginTop: "10px" } }, s.title),
      h("div", { style: { fontSize: "12px", color: "var(--muted)", marginTop: "4px" } }, s.text),
      h("div", { class: "shortcut-lastrun" }, s.lastRun ? "Son çalışma: " + fmtRelative(s.lastRun) : "Henüz çalıştırılmadı"));
    box.addEventListener("click", async () => {
      try {
        const p = request("runShortcut", { id: s.id });
        if (s.group === "bakim") openScriptOutput(s.title, p);
        const res = await p;
        if (res.kind !== "script") toast(s.title, "info");
      } catch (e) { toast(e.message || "Olmadı.", "error"); }
    });
    return box;
  }

  return { id: "kestirmeler", mount, update, unmount };
}
