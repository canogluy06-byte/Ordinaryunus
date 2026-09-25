// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../bridge.js";
import { h, icon, chip, btn, iconBtn, toast, confirmDialog, fmtTime, escapeHtml } from "../ui.js";

const STEP_ICON = { start: "play", think: "sparkle", text: "message", read: "file", edit: "doc", write: "doc", search: "search", web: "external", bash: "cpu", tool: "bolt", denied: "x", gate: "shield", result: "check", error: "alert" };

export async function openJobDrawer(jobId) {
  const scrim = document.querySelector(".drawer-scrim") || h("div", { class: "drawer-scrim" });
  const drawer = document.querySelector(".drawer") || h("div", { class: "drawer" });
  drawer.innerHTML = "";
  if (!scrim.isConnected) document.body.appendChild(scrim);
  if (!drawer.isConnected) document.body.appendChild(drawer);
  scrim.onclick = close;
  function close() { scrim.classList.remove("show"); drawer.classList.remove("show"); }
  drawer.appendChild(h("div", { class: "drawer-body" }, "Yükleniyor…"));
  requestAnimationFrame(() => { scrim.classList.add("show"); drawer.classList.add("show"); });

  try {
    const detail = await request("getJobDetail", { id: jobId });
    render(detail);
  } catch (e) {
    drawer.innerHTML = "";
    drawer.appendChild(h("div", { class: "drawer-body" }, "İş bulunamadı."));
  }

  function render(detail) {
    const job = detail.job;
    drawer.innerHTML = "";
    drawer.appendChild(h("div", { class: "drawer-head" },
      h("div", { style: { flex: 1 } },
        h("div", { style: { fontWeight: 800, fontSize: "17px" } }, job.title),
        h("div", { style: { color: "var(--muted)", fontSize: "13px", marginTop: "4px" } }, (job.tool === "claude" ? "Claude" : "Codex") + (job.role ? " · " + job.role : "") + (job.project ? " · " + job.project : "")),
      ),
      iconBtn("x", { onClick: close, ariaLabel: "Kapat" }),
    ));
    const body = h("div", { class: "drawer-body" });
    body.appendChild(chip(job.stateText, job.tone));
    if (job.fix) {
      body.appendChild(h("div", { class: "banner", style: { borderRadius: "12px" } }, icon("alert"), h("div", { class: "spacer" }, job.fix),
        job.loginCommand ? btn("Komutu kopyala", { sm: true, variant: "ghost", onClick: () => request("copyText", { text: detail.loginCommand || job.fix }) }) : null));
    }
    if (job.denials?.length) body.appendChild(h("div", {}, h("div", { class: "section-title" }, "İzin verilmedi"), ...job.denials.map((d) => chip(d, "yellow"))));
    body.appendChild(h("div", {}, h("div", { class: "section-title" }, icon("clock"), "Adımlar"),
      h("div", { class: "vstack", style: { gap: "8px", marginTop: "8px" } }, ...(detail.steps || []).map((s) =>
        h("div", { class: "step-row" }, h("span", { class: "t" }, fmtTime(s.t)), icon(STEP_ICON[s.kind] || "bolt"), h("span", {}, s.text))))));
    if (detail.resultHtml) body.appendChild(h("div", {}, h("div", { class: "section-title" }, "Sonuç"), h("div", { class: "prose", html: detail.resultHtml })));
    if (job.filesChanged?.length) body.appendChild(h("div", {}, h("div", { class: "section-title" }, "Değişen dosyalar"), ...job.filesChanged.map((f) => h("div", { class: "help-text" }, f))));
    const actions = h("div", { style: { display: "flex", gap: "8px", flexWrap: "wrap" } });
    if (job.resultPreview) actions.appendChild(btn("Sonucu kopyala", { variant: "ghost", sm: true, icon: "copy", onClick: () => request("copyText", { text: job.resultPreview }) }));
    actions.appendChild(btn("Günlüğü aç", { variant: "ghost", sm: true, icon: "folder", onClick: () => request("openFolder", { which: job.tool === "claude" ? "claudeIsleri" : "codexIsleri" }) }));
    if (job.canCancel) actions.appendChild(btn("Durdur", { variant: "danger", sm: true, icon: "stop", onClick: async () => { const ok = await confirmDialog("Bu iş durdurulsun mu?", { danger: true, okText: "Durdur" }); if (ok) { await request("cancelJob", { id: job.id }); toast("İş durduruldu.", "warn"); close(); } } }));
    body.appendChild(actions);
    drawer.appendChild(body);
  }
}
