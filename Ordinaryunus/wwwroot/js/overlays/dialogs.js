// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../bridge.js";
import { h, icon, btn, toast } from "../ui.js";

export function openTamGazDialog() {
  return new Promise((resolve) => {
    const root = document.getElementById("overlay-root");
    root.style.pointerEvents = "auto";
    const scrim = h("div", { class: "overlay-scrim" });
    let picked = 5;
    const cards = h("div", { class: "grid grid-2", style: { gap: "10px" } });
    [2, 5, 10, 12].forEach((hrs) => {
      const c = h("div", { class: "card" + (hrs === picked ? " hoverable" : ""), style: { cursor: "pointer", textAlign: "center", padding: "18px", border: hrs === picked ? "2px solid var(--teal)" : "1px solid var(--border)" } },
        h("div", { style: { fontSize: "26px", fontWeight: 800 } }, hrs + " sa"),
        h("div", { style: { fontSize: "12px", color: "var(--muted)" } }, "Tam Gaz"));
      c.addEventListener("click", () => { picked = hrs; Array.from(cards.children).forEach((x) => x.style.border = "1px solid var(--border)"); c.style.border = "2px solid var(--teal)"; });
      cards.appendChild(c);
    });
    const box = h("div", { class: "modal" },
      h("div", { class: "modal-head" }, icon("rocket"), h("div", { class: "modal-title" }, "Tam Gaz Modu")),
      h("div", { class: "modal-body" }, h("p", { class: "help-text" }, "Seçtiğin süre boyunca zamanlanmış Claude görevi saatte bir çalışır ve gerekirse Codex'e iş devreder."), cards),
      h("div", { class: "modal-actions" },
        btn("Vazgeç", { variant: "ghost", onClick: () => close(null) }),
        btn("Başlat", { variant: "primary", icon: "rocket", onClick: async () => { try { await request("tamGazOn", { hours: picked }); toast("Tam Gaz açıldı.", "success"); close(true); } catch (e) { toast(e.message || "Başlatılamadı.", "error"); } } })),
    );
    function close(v) { scrim.classList.remove("show"); box.classList.remove("show"); setTimeout(() => { scrim.remove(); box.remove(); root.style.pointerEvents = "none"; }, 180); resolve(v); }
    scrim.addEventListener("click", () => close(null));
    root.appendChild(scrim); root.appendChild(box);
    requestAnimationFrame(() => { scrim.classList.add("show"); box.classList.add("show"); });
  });
}

export function openScriptOutput(title, promise) {
  const root = document.getElementById("overlay-root");
  root.style.pointerEvents = "auto";
  const scrim = h("div", { class: "overlay-scrim" });
  const out = h("div", { class: "mono-box", style: { minHeight: "120px", maxHeight: "40vh", overflow: "auto" } }, "Çalışıyor…");
  const statusChip = h("span", { class: "chip chip-teal" }, h("span", { class: "dot" }), "Çalışıyor");
  const box = h("div", { class: "modal wide",  },
    h("div", { class: "modal-head" }, h("div", { class: "modal-title" }, title), statusChip),
    h("div", { class: "modal-body" }, out),
    h("div", { class: "modal-actions" }, btn("Kapat", { variant: "ghost", onClick: () => close() })),
  );
  function close() { scrim.classList.remove("show"); box.classList.remove("show"); setTimeout(() => { scrim.remove(); box.remove(); root.style.pointerEvents = "none"; }, 180); }
  scrim.addEventListener("click", close);
  root.appendChild(scrim); root.appendChild(box);
  requestAnimationFrame(() => { scrim.classList.add("show"); box.classList.add("show"); });
  promise.then((res) => {
    out.textContent = res.output || "(çıktı yok)";
    statusChip.className = "chip " + (res.exitCode === 0 ? "chip-green" : "chip-red");
    statusChip.innerHTML = ""; statusChip.appendChild(h("span", { class: "dot" })); statusChip.appendChild(document.createTextNode(res.timedOut ? "Zaman aşımı" : "Çıkış " + res.exitCode));
  }).catch((e) => {
    out.textContent = e.message || "Hata oluştu.";
    statusChip.className = "chip chip-red"; statusChip.innerHTML = ""; statusChip.appendChild(h("span", { class: "dot" })); statusChip.appendChild(document.createTextNode("Hata"));
  });
}
