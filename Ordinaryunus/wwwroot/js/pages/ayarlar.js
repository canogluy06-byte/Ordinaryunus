// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request, isMock } from "../bridge.js";
import * as store from "../store.js";
import { h, icon, chip, btn, toast, confirmDialog, bindTooltips, scheduledTaskLabel } from "../ui.js";
import { SCALE_STEPS, getScale, setScale } from "../scale.js";
import { IMZA, imzaSatiri } from "../imza.js";

export default function ayarlarPage(helpers) {
  let el, unsub, unsubScale;
  function mount(root) {
    el = root;
    render(store.get("snapshot"));
    unsub = store.on("snapshot", render);
    unsubScale = store.on("uiScale", () => render(store.get("snapshot")));
    request("getSettings").then((s) => { store.set("settingsCache", s); render(store.get("snapshot")); }).catch(() => {});
  }
  function unmount() { unsub?.(); unsubScale?.(); }
  function update() { render(store.get("snapshot")); }

  function render(snap) {
    el.innerHTML = "";
    if (!snap) { el.appendChild(h("div", { class: "empty" }, "Yükleniyor…")); return; }
    const s = snap.tools; const settings = store.get("settingsCache") || { uiScale: 100, lockMinutes: 10, usdRate: 41, animations: true, vaultPath: snap.vault.path };
    el.appendChild(h("div", { class: "eyebrow" }, "AYARLAR"));
    el.appendChild(h("div", { class: "page-title" }, "Ayarlar"));

    el.appendChild(h("div", { class: "card", style: { "--i": 1 } },
      h("div", { class: "card-head" }, icon("folder"), h("div", { class: "card-title" }, "Kasa")),
      h("div", { class: "mono-box" }, snap.vault.path),
      h("div", { style: { marginTop: "10px" } }, btn("Klasör seç", { variant: "ghost", icon: "folder", onClick: async () => { const r = await request("pickVaultFolder"); if (r.path) { await request("setSettings", { vaultPath: r.path }); toast("Kasa yolu güncellendi.", "success"); } } }))));

    const themeSeg = h("div", { class: "seg" },
      segBtn("Açık", store.get("theme") !== "dark", () => setTheme("light")),
      segBtn("Koyu", store.get("theme") === "dark", () => setTheme("dark")));
    const currentScale = getScale();
    const scaleSeg = h("div", { class: "seg scale-seg" }, ...SCALE_STEPS.map((v) => segBtn(v + "%", v === currentScale, () => setScale(v))));
    const animSwitch = h("button", { class: "switch" + (settings.animations !== false ? " on" : ""), onclick: (e) => { e.currentTarget.classList.toggle("on"); request("setSettings", { animations: e.currentTarget.classList.contains("on") }); } });
    el.appendChild(h("div", { class: "card", style: { "--i": 2 } },
      h("div", { class: "card-head" }, icon("sun"), h("div", { class: "card-title" }, "Görünüm")),
      row("Tema", themeSeg),
      row("Ekran boyutu", scaleSeg),
      h("div", { class: "help-text", style: { marginTop: "-8px", marginBottom: "6px" } }, "Yazılar küçük geliyorsa büyüt. Ctrl + artı da olur."),
      row("Hareketler", animSwitch)));

    const keepAwakeSwitch = h("button", { class: "switch" + (settings.keepAwake !== false ? " on" : ""), onclick: (e) => { e.currentTarget.classList.toggle("on"); request("setSettings", { keepAwake: e.currentTarget.classList.contains("on") }); } });
    el.appendChild(h("div", { class: "card", style: { "--i": 3 } },
      h("div", { class: "card-head" }, icon("lock"), h("div", { class: "card-title" }, "Güvenlik")),
      row("İş varken bilgisayar uyumasın", keepAwakeSwitch),
      h("div", { class: "help-text", style: { marginTop: "-8px", marginBottom: "6px" } }, "Bir Claude/Codex işi çalışırken ya da kota bekliyorken bilgisayar uykuya geçmez; ekran yine kapanabilir."),
      row("Otomatik kilit (dakika)", h("input", { class: "field", type: "number", min: 1, max: 240, value: settings.lockMinutes ?? 10, style: { width: "100px" }, onchange: (e) => request("setSettings", { lockMinutes: +e.target.value }) })),
      row("Şifre", btn("Şifreyi değiştir", { variant: "ghost", onClick: changePassword }))));

    el.appendChild(h("div", { class: "card", style: { "--i": 4 } },
      h("div", { class: "card-head" }, icon("trend"), h("div", { class: "card-title" }, "Gelir")),
      row("TL / USD kuru", h("input", { class: "field", type: "number", value: settings.usdRate ?? 41, style: { width: "100px" }, onchange: (e) => request("setSettings", { usdRate: +e.target.value }) }))));

    const connCard = h("div", { class: "card", style: { "--i": 5 } },
      h("div", { class: "card-head" }, icon("link"), h("div", { class: "card-title" }, "Bağlantılar")),
      conn("Claude hook'ları", s.claudeHooks.length > 0, s.claudeHooks.join(", ") || "yok"),
      conn("Claude alt ajanları", s.claudeAgents.length > 0, `${s.claudeAgents.length} ajan`),
      conn("Zamanlanmış görevler", s.scheduledTasks.length > 0, s.scheduledTasks.map((t) => scheduledTaskLabel(t.name)).join(", ")),
      conn("Codex MCP sunucuları", s.codexMcp.length > 0, s.codexMcp.join(", ") || "yok"),
      conn("Codex eklentileri", s.codexPlugins.length > 0, s.codexPlugins.map((p) => p.name).join(", ") || "yok"),
      conn("Codex hook'ları", s.codexHooks.length > 0, s.codexHooks.join(", ") || "yok"),
      conn("Obsidian kasası", s.vaultOk, s.vaultOk ? "bulundu" : "bulunamadı"),
      conn("Git", s.gitOk, s.gitOk ? "çalışıyor" : "yok"),
      h("div", { class: "help-text", style: { marginTop: "10px" } }, "Uygulama bu araçları gösterir ve açar; onlara doğrudan komut göndermez."));
    el.appendChild(connCard);
    el.appendChild(aboutCard());
    bindTooltips(el);
  }

  // Hakkında: yapımcı imzası, sürüm, lisans ve depo adresi. Bütün değerler imza.js'ten gelir (tek kaynak).
  function aboutCard() {
    const openRepo = () => request("openUrl", { url: IMZA.github }).catch((e) => toast(e.message || "Bağlantı açılamadı.", "error"));
    const copyRepo = async () => {
      try {
        await request("copyText", { text: IMZA.github });
        // Deneme köprüsü (mock.js) kendi bildirimini zaten gösteriyor; gerçek uygulamada bildirimi burası verir.
        if (!isMock) toast("Adres panoya kopyalandı.", "success");
      } catch (e) { toast(e.message || "Kopyalanamadı.", "error"); }
    };
    return h("div", { class: "card about-card", style: { "--i": 6 } },
      h("div", { class: "card-head" }, icon("info"), h("div", { class: "card-title" }, "Hakkında")),
      h("div", { class: "about-hero" },
        h("img", { class: "about-logo", src: "img/logo-64.png", alt: "" }),
        h("div", { class: "about-hero-text" },
          h("div", { class: "about-name" }, "Ordinaryunus", chip("v" + IMZA.surum, "teal", { noDot: true })),
          h("div", { class: "about-desc" }, "Yapay zekâ ekibini ve ikinci beynini tek ekrandan yönetmek için yaptım."))),
      row("Yapan", h("div", { class: "about-value" }, IMZA.yapan)),
      row("Sürüm", h("div", { class: "about-value" }, IMZA.surum)),
      row("Lisans", h("div", { class: "about-value" }, IMZA.lisans + " + ek şartlar")),
      row("GitHub", h("div", { class: "about-link" },
        h("span", { class: "about-url" }, IMZA.github),
        btn("Aç", { sm: true, variant: "ghost", icon: "external", onClick: openRepo }),
        btn("Kopyala", { sm: true, variant: "ghost", icon: "copy", onClick: copyRepo }))),
      // GPL-3.0'ın istediği yasal not (madde 0, "Appropriate Legal Notices") ve ek şartların 1-2. maddeleri:
      // yazar atfı bu kartta kalır; değiştirilmiş sürümler burada değiştirildiklerini belirtir.
      h("div", { class: "help-text about-foot" },
        imzaSatiri() + ". Bu program hiçbir garanti olmadan sunulur. GPL-3.0 ve ek şartları (EK-SARTLAR.md) altında " +
        "kopyalayabilir, değiştirebilir ve dağıtabilirsin; lisans metni depodaki LICENSE dosyasında. " +
        "Değiştirilmiş sürümler bunu bu kartta belirtmeli ve yukarıdaki yazar bilgisini korumalıdır."));
  }

  function row(label, control) {
    return h("div", { style: { display: "flex", alignItems: "center", justifyContent: "space-between", padding: "10px 0", borderBottom: "1px solid var(--border-soft)" } }, h("div", { style: { fontWeight: 600, fontSize: "14.5px" } }, label), control);
  }
  function conn(label, ok, detail) {
    return h("div", { style: { display: "flex", alignItems: "center", gap: "10px", padding: "8px 0" } }, h("span", { class: "light " + (ok ? "light-green" : "light-grey"), style: { width: "9px", height: "9px", borderRadius: "50%", background: ok ? "var(--green)" : "var(--grey-mid)" } }), h("div", { style: { fontWeight: 700, fontSize: "13.5px", minWidth: "220px" } }, label), h("div", { style: { fontSize: "12.5px", color: "var(--muted)" } }, detail));
  }
  function segBtn(text, active, onClick) {
    const b = h("button", { class: active ? "active" : "" }, text);
    b.addEventListener("click", () => {
      onClick();
      // setScale() (unlike setTheme()) triggers a synchronous full re-render via the "uiScale" store
      // subscription below, which can already have replaced this whole card — b would be a detached
      // node by the time we get here, so b.parentElement would be null.
      if (!b.isConnected) return;
      b.parentElement.querySelectorAll("button").forEach((x) => x.classList.remove("active"));
      b.classList.add("active");
    });
    return b;
  }
  function setTheme(theme) { document.documentElement.setAttribute("data-theme", theme); store.set("theme", theme); request("setSettings", { theme }); }
  async function changePassword() {
    const oldI = h("input", { class: "field", type: "password", placeholder: "Eski şifre" });
    const newI = h("input", { class: "field", type: "password", placeholder: "Yeni şifre (min 6)" });
    const repI = h("input", { class: "field", type: "password", placeholder: "Yeni şifre tekrar" });
    const { modal } = await import("../ui.js");
    const ok = await modal({ title: "Şifreyi değiştir", body: h("div", { class: "vstack", style: { gap: "10px" } }, oldI, newI, repI), actions: [{ text: "Vazgeç", value: false, variant: "ghost" }, { text: "Değiştir", value: true, variant: "primary" }] });
    if (!ok) return;
    if (newI.value !== repI.value || newI.value.length < 6) { toast("Yeni şifreler uyuşmuyor ya da çok kısa.", "error"); return; }
    try { await request("changePassword", { old: oldI.value, new: newI.value, repeat: repI.value }); toast("Şifre değiştirildi.", "success"); } catch (e) { toast(e.message || "Olmadı.", "error"); }
  }

  return { id: "ayarlar", mount, update, unmount };
}
