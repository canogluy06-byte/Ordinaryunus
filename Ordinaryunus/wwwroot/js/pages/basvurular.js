// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../bridge.js";
import * as store from "../store.js";
import { h, icon, chip, btn, iconBtn, empty, toast, bindTooltips } from "../ui.js";

const DURUM_TONE = { "onay-bekliyor": "yellow", onaylandi: "teal", basvuruldu: "grey", mulakat: "green", olumsuz: "red", diger: "grey" };

export default function basvurularPage(helpers) {
  let el, unsub, selected = new Set();

  function mount(root) { el = root; render(store.get("snapshot")); unsub = store.on("snapshot", render); }
  function unmount() { unsub?.(); }
  function update() { render(store.get("snapshot")); }

  function render(snap) {
    el.innerHTML = "";
    if (!snap) { el.appendChild(h("div", { class: "empty" }, "Yükleniyor…")); return; }
    el.appendChild(h("div", { class: "eyebrow" }, "İŞ BAŞVURULARI"));
    el.appendChild(h("div", { class: "page-title" }, "Başvurular"));
    el.appendChild(h("div", { class: "page-sub" }, "Onayladıklarına Claude başvurur; Claude'a “onaylılara başvur” yaz."));

    if (!snap.applications.found) { el.appendChild(empty("basvurular", "Henüz iş başvuru listesi yok.")); return; }

    const actions = h("div", { style: { display: "flex", gap: "10px" } },
      btn(`Seçilenleri onayla (${selected.size})`, { variant: "primary", disabled: selected.size === 0, onClick: onApprove }),
      btn("Claude'a başvur de", { variant: "secondary", onClick: () => helpers.openGiveWork({ text: "İş Başvuruları'nda \"Onaylandı\" olan ilanlara başvur." }) }),
      btn("Belgeler klasörü", { variant: "ghost", icon: "folder", onClick: () => request("openFolder", { which: "basvuruBelgeleri" }) }));
    el.appendChild(h("div", { style: { "--i": 1 } }, actions));

    for (const sec of snap.applications.sections) {
      const card = h("div", { class: "card", style: { "--i": 2 } });
      card.appendChild(h("div", { class: "card-head" }, icon("basvurular"), h("div", { class: "card-title" }, sec.name), chip(String(sec.rows.length), "grey")));
      card.appendChild(buildTable(sec));
      el.appendChild(card);
    }
    bindTooltips(el);
  }

  function buildTable(sec) {
    const table = h("table", { class: "dtable" });
    const thead = h("thead", {}, h("tr", {}, h("th", {}, ""), ...sec.columns.map((c) => h("th", {}, c))));
    const tbody = h("tbody");
    for (const row of sec.rows) {
      const cb = row.awaiting ? h("input", { type: "checkbox", checked: selected.has(row.key), onchange: (e) => { if (e.target.checked) selected.add(row.key); else selected.delete(row.key); refreshActionButton(); } }) : "";
      const tr = h("tr", { class: "rowhover", style: { cursor: row.url ? "pointer" : "default" } },
        h("td", {}, cb),
        h("td", {}, row.no), h("td", { style: { fontWeight: 700 } }, row.firma), h("td", {}, row.pozisyon), h("td", {}, row.tur), h("td", {}, row.kanal),
        h("td", {}, row.sehir), h("td", {}, row.uygunluk), h("td", {}, row.eksikBeceri || "—"),
        h("td", {}, row.url ? iconBtn("external", { tip: "İlanı aç", onClick: (e) => { e.stopPropagation(); request("openUrl", { url: row.url }); } }) : "—"),
        h("td", {}, row.belgeler || "—"), h("td", {}, chip(row.durum, DURUM_TONE[row.durumKind] || "grey")), h("td", {}, row.not || ""));
      if (row.url) tr.addEventListener("dblclick", () => request("openUrl", { url: row.url }));
      tbody.appendChild(tr);
    }
    table.appendChild(thead); table.appendChild(tbody);
    return table;
  }
  function refreshActionButton() {
    const b = el.querySelector(".btn-primary");
    if (b) { b.textContent = ""; b.appendChild(document.createTextNode(`Seçilenleri onayla (${selected.size})`)); b.disabled = selected.size === 0; }
  }
  async function onApprove() {
    if (!selected.size) return;
    try {
      const res = await request("approveJobs", { keys: Array.from(selected) });
      toast(`${res.count} ilan onaylandı.`, "success");
      selected.clear();
      request("refresh").catch(() => {});
    } catch (e) { toast(e.message || "Olmadı.", "error"); }
  }

  return { id: "basvurular", mount, update, unmount };
}
