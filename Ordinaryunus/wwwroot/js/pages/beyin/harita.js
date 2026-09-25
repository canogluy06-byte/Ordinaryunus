// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../../bridge.js";
import * as store from "../../store.js";
import { h, icon, btn, empty, skeleton } from "../../ui.js";
import { chart, disposeChartsIn, tipHtml, commonTooltip } from "../../charts.js";

const KIND_COLOR = { "proje-karti": "var(--teal)", kayit: "var(--teal-2)", gorev: "var(--slate)", rol: "var(--slate-2)", fikir: "var(--yellow)", not: "var(--teal-mid)", eksik: "var(--red)" };
const KIND_LABEL = { "proje-karti": "Proje kartı", kayit: "Kayıt", gorev: "Görev", rol: "Rol", fikir: "Fikir", not: "Not", eksik: "Eksik not" };

// A5: at rest only well-connected nodes get a text label; zooming in (roam) reveals more, so a dense
// vault doesn't render as unreadable label soup (Beyin Evreni will eventually replace this view, but
// the plain ECharts graph still needs to stay legible until then — see EK-v2.1 §3).
function labelDegreeThreshold(zoom) {
  if (zoom >= 3) return 2;
  if (zoom >= 1.8) return 3;
  return 4;
}

export default function mountHarita(root, route, helpers) {
  let disposed = false, inst = null, activeKinds = new Set(Object.keys(KIND_COLOR)), graph = null;
  let currentZoom = 1, currentNodes = [];

  const toolbar = h("div", { class: "graph-toolbar" });
  const projectSel = h("select", { class: "field", style: { width: "220px" } }, h("option", { value: "" }, "Tüm kasa"));
  const searchBox = h("input", { class: "field", placeholder: "Not ara…", style: { width: "200px" } });
  toolbar.appendChild(projectSel); toolbar.appendChild(searchBox);
  toolbar.appendChild(btn("Ortala", { variant: "ghost", sm: true, onClick: () => inst && inst.dispatchAction({ type: "graphRoam", zoom: 1 }) }));
  root.appendChild(toolbar);
  // A11: legend can reach ~15 kinds; a wrapping flex row would eat several lines of vertical space, so it
  // scrolls horizontally in one row instead (own row, below the toolbar controls).
  const legendWrap = h("div", { class: "legend-row" });
  root.appendChild(legendWrap);
  const canvas = h("div", { id: "graph-canvas" });
  root.appendChild(h("div", { style: { marginTop: "12px" } }, canvas));

  const snap = store.get("snapshot");
  (snap?.projects || []).forEach((p) => projectSel.appendChild(h("option", { value: p.name }, p.name)));
  projectSel.addEventListener("change", () => load(projectSel.value || null));
  searchBox.addEventListener("input", () => highlightSearch());

  load(null);
  return () => { disposed = true; disposeChartsIn(canvas); };

  async function load(project) {
    canvas.innerHTML = ""; canvas.appendChild(skeleton(1, 400));
    try {
      graph = await request("getGraph", { project });
      if (disposed) return;
      buildLegend();
      render();
    } catch { canvas.innerHTML = ""; canvas.appendChild(empty("graph", "Harita yüklenemedi.")); }
  }

  function buildLegend() {
    legendWrap.innerHTML = "";
    const kinds = graph.categories.map((c) => c.kind);
    activeKinds = new Set(kinds);
    kinds.forEach((k) => {
      const chip = h("span", { class: "legend-chip on" }, h("span", { class: "sw", style: { background: KIND_COLOR[k] || "var(--slate)" } }), KIND_LABEL[k] || k);
      chip.addEventListener("click", () => { if (activeKinds.has(k)) activeKinds.delete(k); else activeKinds.add(k); chip.classList.toggle("on"); render(); });
      legendWrap.appendChild(chip);
    });
  }

  function render() {
    canvas.innerHTML = "";
    currentZoom = 1;
    const threshold = labelDegreeThreshold(currentZoom);
    const nodes = graph.nodes.filter((n) => activeKinds.has(n.kind)).map((n) => ({
      id: n.id, name: n.name, symbolSize: 8 + Math.min(24, n.degree * 3), value: n.degree,
      itemStyle: { color: KIND_COLOR[n.kind] || "var(--slate)", borderColor: n.missing ? "var(--red)" : "transparent", borderWidth: n.missing ? 2 : 0, borderType: n.missing ? "dashed" : "solid" },
      label: { show: n.degree >= threshold }, kind: n.kind, project: n.project, modified: n.modified, missing: n.missing,
    }));
    currentNodes = nodes;
    const idSet = new Set(nodes.map((n) => n.id));
    const links = graph.links.filter((l) => idSet.has(l.source) && idSet.has(l.target)).map((l) => ({ source: l.source, target: l.target, lineStyle: { width: 1 + Math.min(3, l.count) } }));
    inst = chart(canvas, {
      tooltip: { ...commonTooltip, formatter: (p) => { if (p.dataType === "node") return tipHtml({ title: p.data.name, rows: [{ label: "Tür", value: KIND_LABEL[p.data.kind] || p.data.kind }, { label: "Bağlantı", value: p.data.value }, { label: "Son değişiklik", value: p.data.modified ? new Date(p.data.modified).toLocaleDateString("tr-TR") : "—" }] }); return ""; } },
      series: [{
        type: "graph", layout: "force", roam: true, draggable: true, legendHoverLink: false,
        force: { repulsion: 140, edgeLength: [40, 120], gravity: 0.08, layoutAnimation: !document.documentElement.classList.contains("no-anim") },
        emphasis: { focus: "adjacency", lineStyle: { width: 3 } }, lineStyle: { color: "var(--border)", curveness: 0.1 },
        data: nodes, links, label: { position: "right", fontSize: 10, color: "var(--muted)" },
      }],
    }, { onClick: (p) => { if (p.dataType === "node") helpers.openTarget({ type: "note", path: p.data.id.replace(/^[a-z]+:/, ""), anchor: null }); } });
    // A5: as the user zooms in with roam, lower the degree threshold so more labels appear — recomputing
    // just the label flags and merging them in is far cheaper than a full re-render on every wheel tick.
    inst.on("graphroam", (params) => {
      const zoom = typeof params?.zoom === "number" ? params.zoom : currentZoom;
      if (zoom === currentZoom) return;
      currentZoom = zoom;
      const nextThreshold = labelDegreeThreshold(currentZoom);
      inst.setOption({ series: [{ data: currentNodes.map((n) => ({ ...n, label: { show: n.value >= nextThreshold } })) }] });
    });
  }
  function highlightSearch() {
    if (!inst) return;
    const q = searchBox.value.toLowerCase();
    inst.dispatchAction({ type: "downplay" });
    if (!q) return;
    graph.nodes.forEach((n, i) => { if (n.name.toLowerCase().includes(q)) inst.dispatchAction({ type: "highlight", seriesIndex: 0, dataIndex: i }); });
  }
}
