// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../../bridge.js";
import * as store from "../../store.js";
import { h, icon, chip, ring, sparkline, countUp, fmtNum, fmtTime, fmtRelative, empty, progressBar, toneForHealth, toneForSeverity, bindTooltips } from "../../ui.js";
import { chart, tipHtml, commonTooltip } from "../../charts.js";

const HELP = {
  requests14: "İstek = Claude'a ya da Codex'e yazdığın bir mesaj. Zamanlanmış görevler sayılmaz.",
  commits14: "Kayıt (commit) = kasada kalıcı olarak kaydedilen bir değişiklik.",
  activity30: "O gün kaç kayıt, istek ve devir notu oldu.",
  projectStatus: "Projelerin durumuna göre dağılımı.",
  taskFunnel: "Görev paketleri hangi aşamada: hazır → verildi → kontrol → tamam.",
  progress: "Projenin bitti sayılması için gereken maddelerden kaçı işaretli.",
  kill: "Bu tarihe kadar kriter tutmazsa proje dondurulur.",
};

export default function mountGenel(root, route, helpers) {
  let disposed = false;
  root.appendChild(skeletonBlock());
  load();
  return () => { disposed = true; };

  async function load() {
    let ov;
    try { ov = await request("getBrainOverview"); } catch (e) { root.innerHTML = ""; root.appendChild(empty("alert", "Beyin verisi yüklenemedi.")); return; }
    if (disposed) return;
    store.set("overview", ov);
    render(ov);
  }

  function render(ov) {
    root.innerHTML = "";
    root.appendChild(hero(ov));
    root.appendChild(kpiGrid(ov));
    root.appendChild(projectsSection(ov));
    root.appendChild(doneNotDone(ov));
    root.appendChild(recentProblems(ov));
    root.appendChild(chartsGrid(ov));
    root.appendChild(bottomStrip(ov));
    bindTooltips(root);
  }

  function hero(ov) {
    const box = h("div", { class: "hero-brain card", style: { "--i": 0 } });
    box.appendChild(h("div", { class: "hero-headline" }, ...ov.headline.map((s) => h("p", {}, s))));
    // A10: base explanation of the formula, plus the backend's own dynamic note (ProblemDetector.HealthHint,
    // BrainOverview.health.note) when it has something specific to say about THIS score (e.g. pending
    // critical approvals counted as "dikkat" instead of zeroing the score) — empty string when it doesn't.
    const healthTip = "100 − 25×kritik − 6×dikkat − 1×bilgi. Onay bekleyen bir silme isteği tek başına puanı sıfırlamaz: karar verilene kadar \"dikkat\" olarak sayılır, \"kritik\" değil."
      + (ov.health.note ? " " + ov.health.note : "");
    const healthRing = h("div", { class: "hero-ring-card", "data-tip": healthTip, "data-tip-title": "Genel sağlık nasıl hesaplanır?" },
      ring(ov.health.score, { color: colorForLevel(ov.health.level) }), h("div", { class: "hero-ring-label" }, "Genel sağlık ", icon("info", "")));
    const focusRing = h("div", { class: "hero-ring-card" }, ring(ov.focus.pct ?? 0, { color: "var(--teal)" }), h("div", { class: "hero-ring-label" }, ov.focus.project ? "Odak: " + ov.focus.project : "Odak yok"));
    const counts = h("div", { class: "hero-counts" },
      countRow("var(--red)", ov.health.counts.kritik, "kritik"),
      countRow("var(--yellow)", ov.health.counts.dikkat, "dikkat"),
      countRow("var(--slate-2)", ov.health.counts.bilgi, "bilgi"));
    box.appendChild(healthRing); box.appendChild(focusRing); box.appendChild(counts);
    return box;
  }
  function countRow(color, num, label) { return h("div", { class: "count-row" }, h("span", { style: { width: "10px", height: "10px", borderRadius: "50%", background: color, display: "inline-block" } }), h("span", { class: "num" }, String(num)), h("span", {}, label)); }
  function colorForLevel(level) { return level === "iyi" ? "var(--green)" : level === "dikkat" ? "var(--yellow)" : "var(--red)"; }

  function kpiGrid(ov) {
    const grid = h("div", { class: "grid grid-5", style: { "--i": 1 } });
    ov.kpis.forEach((k, i) => {
      const tile = h("div", { class: "kpi", style: { "--i": i }, "data-tip": k.tooltip, "data-tip-title": k.label, onclick: () => k.target && helpers.openTarget(k.target) });
      tile.appendChild(h("div", { class: "label" }, k.label, icon("info", "")));
      tile.querySelector(".label svg").style.width = "12px"; tile.querySelector(".label svg").style.height = "12px"; tile.querySelector(".label svg").style.color = "var(--faint)";
      const isLong = k.value == null && String(k.display).length > 7;
      const numEl = h("div", { class: "num" + (k.tone === "warn" ? " warn" : "") + (isLong ? " small" : "") }, "");
      tile.appendChild(numEl);
      const sub = h("div", { class: "sub" });
      if (k.trend) sub.appendChild(h("span", { class: "trend " + (k.trend.delta > 0 ? "up" : k.trend.delta < 0 ? "down" : "flat") }, (k.trend.delta > 0 ? "▲" : k.trend.delta < 0 ? "▼" : "―") + " "));
      sub.appendChild(document.createTextNode(k.sub));
      tile.appendChild(sub);
      if (k.spark) tile.appendChild(sparkline(k.spark));
      if (k.progress != null) tile.appendChild(progressBar(k.progress));
      grid.appendChild(tile);
      requestAnimationFrame(() => { if (typeof k.value === "number") countUp(numEl, k.value); else numEl.textContent = k.display; });
    });
    return grid;
  }

  function projectsSection(ov) {
    const card = h("div", { class: "card", style: { "--i": 2 } });
    card.appendChild(h("div", { class: "card-head" }, icon("projeler"), h("div", { class: "card-title" }, "Projeler nasıl?")));
    const grid = h("div", { class: "vstack", style: { gap: "14px" } });
    ov.projects.forEach((p) => {
      const row = h("div", { class: "project-health-card card", style: { boxShadow: "none", background: "var(--surface-2)", cursor: "pointer" }, onclick: () => helpers.openTarget({ type: "project", name: p.name, section: "genel" }) });
      row.appendChild(h("div", { class: "gauge" }, ring(p.score, { size: 68, stroke: 7, color: colorForLevel(p.level) })));
      const body = h("div", { class: "body" });
      body.appendChild(h("div", { class: "name" }, p.odak ? "★ " : "", p.name, chip(p.durum, "grey")));
      body.appendChild(h("div", { class: "reasons" }, p.reasons.join(" · ") + (p.daysToKill != null ? ` · bırakmaya ${p.daysToKill} gün` : "")));
      body.appendChild(h("div", { class: "next" }, "Sıradaki: " + p.nextStep));
      row.appendChild(body);
      grid.appendChild(row);
    });
    card.appendChild(grid);
    return card;
  }

  function doneNotDone(ov) {
    const wrap = h("div", { class: "grid grid-2", style: { "--i": 3 } });
    const doneCard = h("div", { class: "card" }, h("div", { class: "card-head" }, icon("check"), h("div", { class: "card-title" }, "Neler yaptık")));
    if (!ov.done.length) doneCard.appendChild(empty("check", "Son 14 günde kayıt yok."));
    else doneCard.appendChild(h("div", { class: "vstack" }, ...ov.done.map((w) => workRow(w))));
    const notDoneCard = h("div", { class: "card" }, h("div", { class: "card-head" }, icon("clock"), h("div", { class: "card-title" }, "Neler kaldı")));
    if (!ov.notDone.length) notDoneCard.appendChild(empty("check", "Bekleyen bir şey yok."));
    else notDoneCard.appendChild(h("div", { class: "vstack" }, ...ov.notDone.map((w) => workRow(w))));
    wrap.appendChild(doneCard); wrap.appendChild(notDoneCard);
    return wrap;
  }
  function workRow(w) {
    const row = h("div", { class: "problem-row", style: w.target ? { cursor: "pointer" } : {} },
      h("span", { class: "problem-dot", style: { background: "var(--teal-mid)" } }),
      h("div", { class: "problem-body" }, h("div", { class: "problem-title" }, w.text), h("div", { class: "problem-fix" }, w.sourceLabel + (w.project ? " · " + w.project : ""))));
    if (w.target) row.addEventListener("click", () => helpers.openTarget(w.target));
    return row;
  }

  function recentProblems(ov) {
    const wrap = h("div", { class: "grid grid-2", style: { "--i": 4 } });
    const recentCard = h("div", { class: "card" }, h("div", { class: "card-head" }, icon("clock"), h("div", { class: "card-title" }, "Son olanlar")));
    if (!ov.recent.length) recentCard.appendChild(empty("clock", "Son 72 saatte olay yok."));
    else recentCard.appendChild(h("div", { class: "vstack" }, ...ov.recent.map((r) => h("div", { class: "problem-row", style: { cursor: r.target ? "pointer" : "default" }, onclick: () => r.target && helpers.openTarget(r.target) },
      h("span", { class: "problem-dot", style: { background: "var(--slate-2)" } }),
      h("div", { class: "problem-body" }, h("div", { class: "problem-title" }, r.title), h("div", { class: "problem-fix" }, fmtRelative(r.time) + (r.tool ? " · " + r.tool : "")))))));
    const probCard = h("div", { class: "card" }, h("div", { class: "card-head" }, icon("alert"), h("div", { class: "card-title" }, "En önemli sorunlar")));
    if (!ov.topProblems.length) probCard.appendChild(empty("check", "Sorun yok."));
    else probCard.appendChild(h("div", { class: "vstack" }, ...ov.topProblems.map((p) => h("div", { class: "problem-row", style: { cursor: "pointer" }, onclick: () => helpers.openTarget(p.target) },
      h("span", { class: "problem-dot", style: { background: p.severity === "kritik" ? "var(--red)" : p.severity === "dikkat" ? "var(--yellow)" : "var(--slate-2)" } }),
      h("div", { class: "problem-body" }, h("div", { class: "problem-title" }, p.title), h("div", { class: "problem-fix" }, "Ne yapmalı: " + p.fix))))));
    wrap.appendChild(recentCard); wrap.appendChild(probCard);
    return wrap;
  }

  function chartsGrid(ov) {
    const wrap = h("div", { class: "grid grid-3", style: { "--i": 5 } });
    wrap.appendChild(chartCard("İstekler, 14 gün", "requests14", ov));
    wrap.appendChild(chartCard("Kasa kayıtları, 14 gün", "commits14", ov));
    wrap.appendChild(chartCard("30 günlük etkinlik", "activity30", ov));
    wrap.appendChild(chartCard("Projeler", "projectStatus", ov));
    wrap.appendChild(chartCard("Görev hunisi", "taskFunnel", ov));
    wrap.appendChild(chartCard("Bitti tanımı", "progress", ov));
    wrap.appendChild(chartCard("Bırakma tarihine kalan gün", "kill", ov));
    return wrap;
  }
  function chartCard(title, key, ov) {
    const box = h("div", { class: "card" }, h("div", { class: "card-head" }, h("div", { class: "card-title", style: { fontSize: "15px" } }, title)));
    const el = h("div", { class: "chart-box", style: { height: "220px" } });
    box.appendChild(el);
    box.appendChild(h("div", { class: "chart-help" }, HELP[key]));
    requestAnimationFrame(() => chart(el, buildOption(key, ov)));
    return box;
  }

  function buildOption(key, ov) {
    const c = ov.charts;
    if (key === "requests14") return {
      tooltip: { trigger: "axis", ...commonTooltip, formatter: (p) => { const i = p[0].dataIndex; return tipHtml({ title: c.requests14.labels[i], rows: [{ color: "var(--teal)", label: "Claude", value: c.requests14.claude[i] }, { color: "var(--slate)", label: "Codex", value: c.requests14.codex[i] }], help: HELP.requests14 }); } },
      legend: { data: ["Claude", "Codex"], bottom: 0, textStyle: { color: "var(--muted)", fontSize: 11 } },
      grid: { top: 12, bottom: 34, left: 4, right: 4, containLabel: true },
      xAxis: { type: "category", data: c.requests14.labels }, yAxis: { type: "value" },
      series: [{ name: "Claude", type: "bar", stack: "a", data: c.requests14.claude, itemStyle: { color: "var(--teal)", borderRadius: [4, 4, 0, 0] } }, { name: "Codex", type: "bar", stack: "a", data: c.requests14.codex, itemStyle: { color: "var(--slate)" } }],
    };
    if (key === "commits14") { const avg = c.commits14.values.reduce((a, b) => a + b, 0) / c.commits14.values.length;
      return { tooltip: { trigger: "axis", ...commonTooltip, formatter: (p) => tipHtml({ title: c.commits14.labels[p[0].dataIndex], rows: [{ color: "var(--teal)", label: "Kayıt", value: c.commits14.values[p[0].dataIndex] }], help: HELP.commits14 }) },
        grid: { top: 12, bottom: 24, left: 4, right: 4, containLabel: true },
        xAxis: { type: "category", data: c.commits14.labels }, yAxis: { type: "value" },
        series: [{ type: "bar", data: c.commits14.values, itemStyle: { color: "var(--teal)", borderRadius: [4, 4, 0, 0] } }, { type: "line", data: c.commits14.values.map(() => avg), symbol: "none", lineStyle: { color: "var(--slate-2)", type: "dashed" } }] }; }
    if (key === "activity30") return buildHeatmap(c.activity30);
    if (key === "projectStatus") { const total = c.projectStatus.reduce((a, b) => a + b.count, 0);
      return { tooltip: { trigger: "item", ...commonTooltip, formatter: (p) => tipHtml({ title: p.name, rows: [{ color: p.color, label: "Proje sayısı", value: p.value }], help: HELP.projectStatus }) },
        series: [{ type: "pie", radius: ["55%", "78%"], label: { show: true, formatter: "{b}\n{c}", color: "var(--text)", fontSize: 11 }, labelLine: { length: 8 },
          data: c.projectStatus.map((s) => ({ name: s.label, value: s.count, itemStyle: { color: { aktif: "var(--teal)", beklemede: "var(--slate-2)", bitti: "var(--green)", donduruldu: "var(--grey-mid)" }[s.durum] } })) },
        ], graphic: { type: "text", left: "center", top: "center", style: { text: String(total), fontSize: 26, fontWeight: 800, fill: "var(--text)" } } }; }
    if (key === "taskFunnel") return {
      tooltip: { trigger: "item", ...commonTooltip, formatter: (p) => tipHtml({ title: p.name, rows: [{ color: "var(--teal)", label: "Görev", value: p.value }], help: HELP.taskFunnel }) },
      grid: { top: 8, bottom: 8, left: 8, right: 24, containLabel: true },
      xAxis: { type: "value" }, yAxis: { type: "category", data: c.taskFunnel.map((t) => t.label), inverse: true },
      series: [{ type: "bar", data: c.taskFunnel.map((t) => t.count), itemStyle: { color: "var(--teal)", borderRadius: [0, 6, 6, 0] }, barWidth: "55%", label: { show: true, position: "right", color: "var(--muted)" } }],
    };
    if (key === "progress") return {
      tooltip: { trigger: "item", ...commonTooltip, formatter: (p) => tipHtml({ title: p.name, rows: [{ color: "var(--teal)", label: "Tamamlanan", value: p.value + "%" }], help: HELP.progress }) },
      grid: { top: 8, bottom: 8, left: 8, right: 24, containLabel: true },
      xAxis: { type: "value", max: 100 }, yAxis: { type: "category", data: c.progress.map((p) => p.project), inverse: true },
      series: [{ type: "bar", data: c.progress.map((p) => p.pct), itemStyle: { color: "var(--teal)", borderRadius: [0, 6, 6, 0] }, barWidth: "50%", label: { show: true, position: "right", formatter: "{c}%", color: "var(--muted)" } }],
    };
    if (key === "kill") return {
      tooltip: { trigger: "item", ...commonTooltip, formatter: (p) => tipHtml({ title: p.name, rows: [{ label: "Kalan gün", value: p.value }], help: HELP.kill }) },
      grid: { top: 8, bottom: 8, left: 8, right: 24, containLabel: true },
      xAxis: { type: "value" }, yAxis: { type: "category", data: c.kill.map((k) => k.project), inverse: true },
      series: [{ type: "bar", data: c.kill.map((k) => ({ value: k.days, itemStyle: { color: k.days <= 7 ? "var(--red)" : "var(--teal)", borderRadius: [0, 6, 6, 0] } })), barWidth: "50%", label: { show: true, position: "right", color: "var(--muted)" } }],
    };
  }

  function buildHeatmap(a30) {
    const data = a30.days.map((d, i) => [d, a30.commits[i] + a30.requests[i] + a30.kayit[i] * 2]);
    const start = a30.days[0], end = a30.days[a30.days.length - 1];
    return {
      tooltip: { ...commonTooltip, formatter: (p) => tipHtml({ title: p.data[0], rows: [{ label: "Değer", value: p.data[1] }], help: HELP.activity30 }) },
      visualMap: { min: 0, max: Math.max(4, ...data.map((d) => d[1])), show: false, inRange: { color: ["var(--surface-2)", "var(--teal)"] } },
      calendar: { range: [start, end], cellSize: [18, 18], top: 10, left: 10, right: 10, yearLabel: { show: false }, dayLabel: { nameMap: "cn", color: "var(--faint)", fontSize: 9 }, monthLabel: { show: false }, itemStyle: { borderWidth: 3, borderColor: "var(--surface)" }, splitLine: { show: false } },
      series: [{ type: "heatmap", coordinateSystem: "calendar", data }],
    };
  }

  function bottomStrip(ov) {
    const wrap = h("div", { class: "grid grid-4", style: { "--i": 6 } });
    const analysisCard = h("div", { class: "card" }, h("div", { class: "card-head" }, icon("sparkle"), h("div", { class: "card-title", style: { fontSize: "15px" } }, "Analiz")), h("div", { class: "help-text" }, "Kural tabanlı, yapay zekâ değil"), h("div", { class: "vstack", style: { gap: "8px", marginTop: "10px" } }, ...ov.analysis.map((s) => h("div", { class: "bullet" }, s))));
    const deep = ov.deepAnalysis;
    const deepCard = h("div", { class: "card" }, h("div", { class: "card-head" }, icon("cpu"), h("div", { class: "card-title", style: { fontSize: "15px" } }, "Claude'un derin analizi")));
    if (!deep.found) deepCard.appendChild(empty("clock", "Henüz derin analiz notu yok. 70 Günlük/Analiz/ altına bir not eklenince burada görünür."));
    else { deepCard.appendChild(h("div", { class: "help-text" }, deep.date)); deepCard.appendChild(h("div", { class: "vstack", style: { gap: "6px", marginTop: "8px" } }, ...deep.lines.slice(0, 6).map((l) => h("div", { style: { fontSize: "13.5px" } }, l)))); deepCard.appendChild(h("div", { style: { marginTop: "10px" } }, h("a", { class: "link", style: { cursor: "pointer", color: "var(--teal)", fontWeight: 700, fontSize: "13px" }, onclick: () => request("openInObsidian", { path: deep.path }) }, "Tamamını aç →"))); }
    const incomeCard = h("div", { class: "card" }, h("div", { class: "card-head" }, icon("trend"), h("div", { class: "card-title", style: { fontSize: "15px" } }, "Gelir göstergesi")));
    if (!ov.income.found) incomeCard.appendChild(empty("trend", "İlk tahsilat bekleniyor.")); else { incomeCard.appendChild(ring(ov.income.pct, { color: "var(--teal)" })); incomeCard.appendChild(h("div", { class: "help-text", style: { textAlign: "center", marginTop: "6px" } }, `$${ov.income.monthUsd} / ${ov.income.targetUsd}`)); }
    const usageCard = h("div", { class: "card" }, h("div", { class: "card-head" }, icon("cpu"), h("div", { class: "card-title", style: { fontSize: "15px" } }, "Bugünkü kullanım")));
    if (!ov.usage) usageCard.appendChild(empty("cpu", "Veri yok.")); else usageCard.appendChild(h("div", { class: "vstack", style: { gap: "6px" } }, h("div", { style: { fontSize: "13.5px" } }, ov.usage.claudeText), h("div", { style: { fontSize: "13.5px" } }, ov.usage.codexText), h("div", { class: "help-text" }, "Aboneliklerin dolar maliyeti yok.")));
    wrap.appendChild(analysisCard); wrap.appendChild(deepCard); wrap.appendChild(incomeCard); wrap.appendChild(usageCard);
    return wrap;
  }

  function skeletonBlock() {
    const w = h("div", { class: "vstack", style: { gap: "20px" } });
    for (let i = 0; i < 3; i++) w.appendChild(h("div", { class: "skel", style: { height: "140px" } }));
    return w;
  }
}
