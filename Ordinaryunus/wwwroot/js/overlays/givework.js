// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../bridge.js";
import * as store from "../store.js";
import { h, icon, btn, iconBtn, toast, confirmDialog } from "../ui.js";

const MAX = 4000;

export function openGiveWork(defaults = {}) {
  const root = document.getElementById("overlay-root");
  root.style.pointerEvents = "auto";
  const snapshot = store.get("snapshot") || { roles: [], projects: [] };
  const rolesByDept = {};
  for (const r of snapshot.roles || []) (rolesByDept[r.departman] ||= []).push(r);

  const textarea = h("textarea", { class: "field", placeholder: "Ne yapılsın? Örn: “İş Başvuruları’nda onaylı ilanlara başvur.”", maxlength: MAX });
  textarea.value = defaults.text || "";
  const count = h("div", { class: "field-count" }, `${textarea.value.length}/${MAX}`);
  textarea.addEventListener("input", () => { count.textContent = `${textarea.value.length}/${MAX}`; updatePreview(); });

  const roleSel = h("select", { class: "field" }, h("option", { value: "" }, "Rol seçilmedi"));
  for (const [dept, list] of Object.entries(rolesByDept)) {
    const grp = h("optgroup", { label: dept });
    for (const r of list) grp.appendChild(h("option", { value: r.title }, r.title));
    roleSel.appendChild(grp);
  }
  roleSel.value = defaults.role || "";
  roleSel.addEventListener("change", updatePreview);

  const projSel = h("select", { class: "field" }, h("option", { value: "" }, "Proje seçilmedi"));
  for (const p of snapshot.projects || []) projSel.appendChild(h("option", { value: p.name }, p.name));
  projSel.value = defaults.project || "";
  projSel.addEventListener("change", updatePreview);

  const previewBox = h("div", { class: "mono-box", style: { fontSize: "12.5px" } }, "");
  function composed() {
    const role = roleSel.value, text = textarea.value.trim(), proj = projSel.value;
    let p = role ? `${role}: ${text}` : text;
    if (proj) p += ` (Proje: ${proj})`;
    return p;
  }
  function updatePreview() { previewBox.textContent = composed() || "…"; }
  updatePreview();

  const copyMenuWrap = h("div", { style: { position: "relative", display: "inline-flex" } });
  const copyMenu = h("div", { class: "card", style: { position: "absolute", bottom: "48px", right: "0", padding: "6px", display: "none", width: "220px", zIndex: "5" } });
  copyMenu.appendChild(rowBtn("Claude için kopyala ve aç", () => doCopy("claude")));
  copyMenu.appendChild(rowBtn("Codex için kopyala ve aç", () => doCopy("codex")));
  function rowBtn(text, onClick) { return h("button", { class: "btn btn-ghost", style: { width: "100%", justifyContent: "flex-start", marginBottom: "4px" }, onclick: () => { copyMenu.style.display = "none"; onClick(); } }, text); }
  const copyToggle = btn("Sadece kopyala ▾", { variant: "ghost", onClick: () => { copyMenu.style.display = copyMenu.style.display === "none" ? "block" : "none"; } });
  copyMenuWrap.appendChild(copyMenu); copyMenuWrap.appendChild(copyToggle);

  // B: "İnternetten sayfa da okuyabilsin" maps to the runClaude/runCodex payload's `web` flag — the
  // nöbetçi only adds WebFetch to --allowedTools when this is checked (A1: off by default).
  const webCheck = h("input", { type: "checkbox", id: "give-work-web" });
  const webRow = h("label", { class: "give-work-web", for: "give-work-web" }, webCheck, "İnternetten sayfa da okuyabilsin (WebFetch)");

  const claudeBtn = btn("Claude'a ver — hemen başlasın", { variant: "primary", icon: "message", onClick: () => runFor("claude") });
  const codexBtn = btn("Codex'e ver", { variant: "secondary", icon: "cpu", onClick: () => runFor("codex") });

  const scrim = h("div", { class: "overlay-scrim" });
  const box = h("div", { class: "modal wide" },
    h("div", { class: "modal-head" }, h("div", { class: "modal-title" }, "Ne yapılsın?"), iconBtn("x", { onClick: close, ariaLabel: "Kapat" })),
    h("div", { class: "modal-body" },
      h("div", {}, h("label", { class: "field-label" }, "Görev"), textarea, count),
      h("div", { class: "grid grid-2" },
        h("div", {}, h("label", { class: "field-label" }, "Rol")), h("div", {}, h("label", { class: "field-label" }, "Proje (opsiyonel)")),
      ),
      h("div", { class: "grid grid-2" }, roleSel, projSel),
      h("div", {}, h("label", { class: "field-label" }, "Gidecek metin"), previewBox),
      webRow,
    ),
    h("div", { class: "modal-actions" }, copyMenuWrap, codexBtn, claudeBtn),
  );

  function close() { scrim.classList.remove("show"); box.classList.remove("show"); setTimeout(() => { scrim.remove(); box.remove(); root.style.pointerEvents = "none"; }, 200); document.removeEventListener("keydown", onKey); }
  function onKey(e) {
    if (e.key === "Escape") close();
    if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) runFor("claude");
  }
  document.addEventListener("keydown", onKey);
  scrim.addEventListener("click", close);

  async function doCopy(tool) {
    await request("copyWork", { tool, text: textarea.value.trim(), role: roleSel.value || null, project: projSel.value || null });
    close();
  }
  async function runFor(tool) {
    const text = textarea.value.trim();
    if (!text) { textarea.focus(); return; }
    const confirmText = tool === "claude"
      ? "Claude kasada arka planda çalışacak ve Claude aboneliğinden harcar. Sonucu Şirketim > Çalışan işler'de görürsün. Başlatılsın mı?"
      : "Codex kasada arka planda çalışacak ve ChatGPT kotandan harcar. Sonucu Claude kontrol eder. Başlatılsın mı?";
    const ok = await confirmDialog(confirmText, { okText: "Başlat", title: tool === "claude" ? "Claude'a ver" : "Codex'e ver" });
    if (!ok) return;
    try {
      const fn = tool === "claude" ? "runClaude" : "runCodex";
      await request(fn, { text, role: roleSel.value || null, project: projSel.value || null, web: webCheck.checked });
      toast((tool === "claude" ? "Claude" : "Codex") + " başlatıldı.", "success");
      close();
    } catch (err) {
      toast(err.message || "Başlatılamadı.", "error");
    }
  }

  root.appendChild(scrim); root.appendChild(box);
  requestAnimationFrame(() => { scrim.classList.add("show"); box.classList.add("show"); textarea.focus(); });
}
