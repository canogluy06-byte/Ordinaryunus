// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../../bridge.js";
import { h, icon, chip, btn, empty, skeleton, toneForSeverity } from "../../ui.js";

const AREA_LABEL = { proje: "Proje", not: "Not", gorev: "Görev", guvenlik: "Güvenlik", calisan: "Çalışan", sistem: "Sistem", fikir: "Fikir" };

export default function mountSorunlar(root, route, helpers) {
  let disposed = false, sev = "hepsi", area = "";
  root.appendChild(skeleton(6, 24));
  load();
  return () => { disposed = true; };

  async function load() {
    try {
      const res = await request("getProblems");
      if (disposed) return;
      render(res);
    } catch { root.innerHTML = ""; root.appendChild(empty("alert", "Sorunlar yüklenemedi.")); }
  }

  function render(data) {
    root.innerHTML = "";
    const toolbar = h("div", { style: { display: "flex", gap: "10px", flexWrap: "wrap", alignItems: "center" } });
    const chips = h("div", { class: "tabs" });
    [["hepsi", "Hepsi", data.counts.kritik + data.counts.dikkat + data.counts.bilgi], ["kritik", "Kritik", data.counts.kritik], ["dikkat", "Dikkat", data.counts.dikkat], ["bilgi", "Bilgi", data.counts.bilgi]].forEach(([k, label, count]) => {
      const t = h("button", { class: "tab" + (sev === k ? " active" : ""), onclick: () => { sev = k; render(data); } }, label, h("span", { class: "count" }, String(count)));
      chips.appendChild(t);
    });
    toolbar.appendChild(chips);
    const areaSel = h("select", { class: "field", style: { width: "180px", marginLeft: "auto" } }, h("option", { value: "" }, "Tüm alanlar"));
    Object.entries(AREA_LABEL).forEach(([k, label]) => areaSel.appendChild(h("option", { value: k }, label)));
    areaSel.value = area;
    areaSel.addEventListener("change", () => { area = areaSel.value; render(data); });
    toolbar.appendChild(areaSel);
    root.appendChild(h("div", { class: "card", style: { "--i": 0 } }, toolbar));

    let list = data.problems;
    if (sev !== "hepsi") list = list.filter((p) => p.severity === sev);
    if (area) list = list.filter((p) => p.area === area);
    const byProject = {};
    for (const p of list) (byProject[p.project || "Genel"] ||= []).push(p);

    if (!list.length) { root.appendChild(empty("check", "Bu filtrede sorun yok.")); return; }
    Object.entries(byProject).forEach(([proj, items], i) => {
      const card = h("div", { class: "card", style: { "--i": i + 1 } });
      card.appendChild(h("div", { class: "card-head" }, icon(proj === "Genel" ? "sistem" : "projeler"), h("div", { class: "card-title" }, proj)));
      items.forEach((p) => card.appendChild(problemRow(p)));
      root.appendChild(card);
    });
  }

  function problemRow(p) {
    const row = h("div", { class: "problem-row" },
      h("span", { class: "problem-dot", style: { background: p.severity === "kritik" ? "var(--red)" : p.severity === "dikkat" ? "var(--yellow)" : "var(--slate-2)" } }),
      h("div", { class: "problem-body" },
        h("div", { class: "problem-title" }, p.title),
        h("div", { style: { fontSize: "13px", color: "var(--muted)", marginTop: "2px" } }, p.detail),
        h("div", { class: "problem-fix" }, "Ne yapmalı: " + p.fix)),
      btn("Aç", { sm: true, variant: "ghost", icon: "chevron-right", onClick: () => helpers.openTarget(p.target) }));
    return row;
  }
}
