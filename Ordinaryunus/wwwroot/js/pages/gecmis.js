// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../bridge.js";
import * as store from "../store.js";
import { h, icon, chip, fmtTime, fmtDayLabel, fmtShortDay, escapeHtml, bindTooltips } from "../ui.js";
import { chart, tipHtml, commonTooltip } from "../charts.js";

const KIND_ICON = { commit: "commit", istek: "message", gorev: "check", isaret: "check", is: "cpu", devir: "doc", karar: "sparkle" };
const KIND_LABEL = { commit: "Kayıt", istek: "İstek", gorev: "Görev", isaret: "İşaret", is: "Yapay zekâ işi", devir: "Devir", karar: "Karar" };

export default function gecmisPage(helpers) {
  let el, unsub, filter = "hepsi", query = "";

  function mount(root) { el = root; render(store.get("snapshot")); unsub = store.on("snapshot", render);
    document.addEventListener("keydown", onCtrlF); }
  function unmount() { unsub?.(); document.removeEventListener("keydown", onCtrlF); }
  function update() { render(store.get("snapshot")); }
  function onCtrlF(e) { if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "f") { const inp = el?.querySelector("#gecmis-search"); if (inp) { e.preventDefault(); inp.focus(); } } }

  function render(snap) {
    el.innerHTML = "";
    if (!snap) { el.appendChild(h("div", { class: "empty" }, "Yükleniyor…")); return; }
    el.appendChild(h("div", { class: "eyebrow" }, "GEÇMİŞ"));
    el.appendChild(h("div", { class: "page-title" }, "Neler yapıldı"));
    el.appendChild(h("div", { class: "page-sub" }, "Kasa kayıtları, istekler ve tamamlanan işler — hep birlikte."));

    const overview = store.get("overview");
    const chartCard = h("div", { class: "card", style: { "--i": 1 } });
    chartCard.appendChild(h("div", { class: "card-head" }, icon("trend"), h("div", { class: "card-title" }, "Son 30 gün")));
    const chartEl = h("div", { class: "chart-box", style: { height: "220px" } });
    chartCard.appendChild(chartEl);
    chartCard.appendChild(h("div", { class: "chart-help" }, "Her gün için kasa kaydı, istek ve devir notu sayısı."));
    el.appendChild(chartCard);
    requestAnimationFrame(async () => {
      let a30 = overview?.charts?.activity30;
      if (!a30) { try { const ov = await request("getBrainOverview"); store.set("overview", ov); a30 = ov.charts.activity30; } catch { return; } }
      chart(chartEl, buildActivityOption(a30));
    });

    const toolbar = h("div", { style: { display: "flex", gap: "10px", alignItems: "center", flexWrap: "wrap" } });
    const tabs = h("div", { class: "tabs" });
    [["hepsi", "Hepsi"], ["istegim", "İsteklerim"], ["yapilan", "Yapılanlar"], ["is", "Yapay zekâ işleri"]].forEach(([k, label]) => {
      const t = h("button", { class: "tab" + (filter === k ? " active" : ""), onclick: () => { filter = k; render(snap); } }, label);
      tabs.appendChild(t);
    });
    toolbar.appendChild(tabs);
    const search = h("input", { class: "field", id: "gecmis-search", placeholder: "Ara (Ctrl+F)", style: { maxWidth: "240px", marginLeft: "auto" }, value: query });
    search.addEventListener("input", () => { query = search.value; renderTimeline(snap); });
    toolbar.appendChild(search);
    el.appendChild(h("div", { class: "card", style: { "--i": 2 } }, toolbar, h("div", { id: "timeline-slot", style: { marginTop: "14px" } })));
    renderTimeline(snap);
    bindTooltips(el);
  }

  function renderTimeline(snap) {
    const slot = el.querySelector("#timeline-slot");
    if (!slot) return;
    slot.innerHTML = "";
    let items = snap.history;
    if (filter === "istegim") items = items.filter((i) => i.kind === "istek");
    else if (filter === "yapilan") items = items.filter((i) => ["gorev", "isaret", "commit"].includes(i.kind));
    else if (filter === "is") items = items.filter((i) => i.kind === "is");
    if (query) items = items.filter((i) => i.text.toLowerCase().includes(query.toLowerCase()));
    if (!items.length) { slot.appendChild(h("div", { class: "empty" }, "Kayıt yok.")); return; }
    let lastDay = null;
    for (const it of items) {
      if (it.day !== lastDay) { lastDay = it.day; slot.appendChild(h("div", { class: "work-group-label", style: { position: "sticky", top: "0", background: "var(--surface)" } }, fmtDayLabel(it.time))); }
      const row = h("div", { class: "work-row" },
        h("div", { class: "work-dot", style: { background: "var(--teal-mid)" } }),
        h("div", { class: "work-body" },
          h("div", { class: "work-title" }, it.text),
          h("div", { class: "work-meta" }, `${fmtTime(it.time)} · ${KIND_LABEL[it.kind] || it.kind}${it.tool ? " · " + it.tool : ""}${it.project ? " · " + it.project : ""}`)),
        it.tool ? chip(it.tool, it.tool === "claude" ? "teal" : "grey") : icon(KIND_ICON[it.kind] || "doc"));
      if (it.target) { row.style.cursor = "pointer"; row.addEventListener("click", () => helpers.openTarget(it.target)); }
      slot.appendChild(row);
    }
  }

  function buildActivityOption(a30) {
    const labels = a30.days.map(fmtShortDay);
    return {
      tooltip: { trigger: "axis", ...commonTooltip, formatter: (params) => {
        const i = params[0].dataIndex;
        return tipHtml({ title: labels[i], rows: [
          { color: "var(--teal)", label: "Kasa kaydı", value: a30.commits[i] },
          { color: "var(--slate)", label: "İstek", value: a30.requests[i] },
          { color: "var(--teal-2)", label: "Kayıt/karar notu", value: a30.kayit[i] } ],
          help: "O gün kaç kayıt, istek ve devir notu oldu." });
      } },
      legend: { data: ["Kayıt", "İstek", "Devir/Karar"], bottom: 0, textStyle: { color: "var(--muted)", fontSize: 11 } },
      grid: { left: 8, right: 8, top: 20, bottom: 36, containLabel: true },
      xAxis: { type: "category", data: labels, axisTick: { show: false } },
      yAxis: { type: "value" },
      series: [
        { name: "Kayıt", type: "bar", stack: "a", data: a30.commits, itemStyle: { color: "var(--teal)", borderRadius: [4, 4, 0, 0] }, barWidth: "60%" },
        { name: "İstek", type: "bar", stack: "a", data: a30.requests, itemStyle: { color: "var(--slate-2)" } },
        { name: "Devir/Karar", type: "line", data: a30.kayit, smooth: true, symbol: "none", lineStyle: { color: "var(--teal-2)", width: 2 } },
      ],
    };
  }

  return { id: "gecmis", mount, update, unmount };
}
