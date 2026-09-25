// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import * as store from "../../store.js";
import { h } from "../../ui.js";
import { disposeChartsIn } from "../../charts.js";
import genel from "./genel.js";
import kesif from "./kesif.js";
import harita from "./harita.js";
import sorunlar from "./sorunlar.js";
import proje from "./proje.js";

const TABS = [
  { id: "genel", label: "Genel bakış" },
  { id: "kesif", label: "Keşfet" },
  { id: "harita", label: "Harita" },
  { id: "sorunlar", label: "Sorunlar" },
];

export default function beyinPage(helpers) {
  let el, tabBarEl, contentEl, cleanup = null, unsubProblems;

  function mount(root, route) {
    el = root;
    el.appendChild(h("div", { class: "eyebrow" }, "BEYİN"));
    el.appendChild(h("div", { class: "page-title" }, "Şu an ne haldeyiz"));
    el.appendChild(h("div", { class: "page-sub" }, "Genel özetten sorun listesine, tek notundan bütün bağlantı haritasına — hepsi burada."));
    tabBarEl = h("div", { class: "tabs", role: "tablist" });
    el.appendChild(tabBarEl);
    const underline = h("div", { class: "tab-underline" });
    tabBarEl.appendChild(underline);
    contentEl = h("div", { style: { marginTop: "20px" } });
    el.appendChild(contentEl);
    unsubProblems = store.on("problemCounts", () => renderTabs(currentRoute()));
    renderTabs(route);
    switchSub(route);
  }
  function update(route) { renderTabs(route); switchSub(route); }
  function unmount() { cleanup?.(); disposeChartsIn(contentEl); unsubProblems?.(); }
  let lastRoute = null;
  function currentRoute() { return lastRoute; }

  function renderTabs(route) {
    lastRoute = route;
    const problemCounts = store.get("problemCounts");
    const activeTab = route.tab === "proje" ? null : (route.tab || "genel");
    Array.from(tabBarEl.querySelectorAll(".tab")).forEach((b) => b.remove());
    TABS.forEach((t) => {
      const isActive = t.id === activeTab;
      const b = h("button", { class: "tab" + (isActive ? " active" : ""), role: "tab", "aria-selected": isActive, onclick: () => { location.hash = "#/beyin/" + t.id; } }, t.label);
      if (t.id === "sorunlar" && problemCounts) b.appendChild(h("span", { class: "count" }, String(problemCounts.kritik + problemCounts.dikkat + problemCounts.bilgi)));
      tabBarEl.appendChild(b);
    });
    requestAnimationFrame(() => {
      const idx = TABS.findIndex((t) => t.id === activeTab);
      const underline = tabBarEl.querySelector(".tab-underline");
      if (idx < 0) { underline.style.width = "0"; return; }
      const btnEl = tabBarEl.querySelectorAll(".tab")[idx];
      underline.style.width = btnEl.offsetWidth + "px";
      underline.style.transform = `translateX(${btnEl.offsetLeft}px)`;
    });
  }

  function switchSub(route) {
    cleanup?.(); disposeChartsIn(contentEl); contentEl.innerHTML = "";
    const tab = route.tab || "genel";
    let mod = genel;
    if (tab === "kesif") mod = kesif;
    else if (tab === "harita") mod = harita;
    else if (tab === "sorunlar") mod = sorunlar;
    else if (tab === "proje") mod = proje;
    contentEl.className = "stagger";
    cleanup = mod(contentEl, route, helpers) || null;
  }

  return { id: "beyin", mount, update, unmount };
}
