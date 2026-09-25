// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../bridge.js";
import * as store from "../store.js";
import { h, icon, chip, btn, ring, empty, toast, confirmDialog, confetti, escapeHtml, bindTooltips } from "../ui.js";

export default function masamPage(helpers) {
  let el, unsub;

  function mount(root) {
    el = root;
    render(store.get("snapshot"));
    unsub = store.on("snapshot", render);
  }
  function unmount() { unsub?.(); }
  function update() { render(store.get("snapshot")); }

  function render(snap) {
    el.innerHTML = "";
    if (!snap) { el.appendChild(h("div", { class: "empty" }, "Yükleniyor…")); return; }

    // ---- hero ----
    const hero = h("div", { class: "card hoverable", style: { "--i": 0 } });
    hero.appendChild(h("div", { class: "eyebrow" }, "MASAM"));
    hero.appendChild(h("div", { class: "greeting" }, snap.greeting));
    if (snap.dayTask) {
      const row = h("div", { style: { display: "flex", alignItems: "center", gap: "16px", marginTop: "18px", flexWrap: "wrap" } },
        h("div", { style: { fontSize: "18px", fontWeight: 700, flex: 1, minWidth: "260px" } }, snap.dayTask.text),
        btn("Başla", { variant: "primary", icon: "play", onClick: () => helpers.openFocus(snap.dayTask) }));
      hero.appendChild(row);
    } else {
      hero.appendChild(h("div", { style: { marginTop: "16px" } }, empty("check", "Bugün için tanımlı bir görev yok. Rahatına bak.")));
    }
    el.appendChild(hero);

    // ---- limit banner ----
    if (snap.limit?.full) {
      el.appendChild(h("div", { class: "banner warn", style: { "--i": 1 } }, icon("alert"),
        h("span", {}, "3/3 dolu. Yeni proje yok; önce birini bitir, beklet ya da dondur."), h("div", { class: "spacer" })));
    }

    // backend warnings (e.g. "senin vermediğin bir onay bulundu") were computed
    // but never shown anywhere — surface every one, red, until the underlying issue is resolved and it stops
    // coming back from the backend.
    for (const w of snap.warnings || []) {
      el.appendChild(h("div", { class: "banner", style: { "--i": 1 } }, icon("alert"), h("span", {}, w), h("div", { class: "spacer" })));
    }

    // ---- approvals ----
    const approvalsCard = h("div", { class: "card hoverable", style: { "--i": 2 } });
    approvalsCard.appendChild(h("div", { class: "card-head" }, icon("shield"), h("div", { class: "card-title" }, "Onay bekleyenler")));
    if (!snap.safety.length) approvalsCard.appendChild(empty("check", "Onay bekleyen bir şey yok."));
    else approvalsCard.appendChild(h("div", { class: "vstack", style: { gap: "12px" } }, ...snap.safety.map(safetyCard)));

    // LinkedIn taslak kutusu yalnızca kasada LinkedIn takvimi (40 Alanlar/LinkedIn Takvimi.md) varsa görünür.
    const linkedin = snap.approvals.linkedinFound;
    const miniGrid = h("div", { class: "grid " + (linkedin ? "grid-3" : "grid-2"), style: { marginTop: "16px" } },
      miniStat(String(snap.approvals.jobsAwaiting), "İş ilanı onayı", snap.approvals.jobsAwaiting ? "İş Başvuruları'nda gör" : "Bekleyen yok", () => helpers ? location.hash = "#/basvurular" : null),
      linkedin ? miniStat(String(snap.approvals.linkedinDrafts), "LinkedIn taslağı", snap.approvals.linkedinDrafts ? "Yayın takviminde onayını bekliyor" : "Bekleyen yok", null) : null,
      miniStat(String(snap.approvals.decisionProjects.length), "Karar bekleyen proje", snap.approvals.decisionProjects.join(", ") || "Yok", () => location.hash = "#/projeler"));
    approvalsCard.appendChild(miniGrid);
    el.appendChild(approvalsCard);

    // ---- todos ----
    const done = snap.todos.items.filter((t) => t.done).length;
    const todoCard = h("div", { class: "card hoverable", style: { "--i": 3 } });
    const head = h("div", { class: "card-head" }, icon("check"), h("div", { class: "card-title" }, "Yapılacaklarım"));
    head.appendChild(ringSmall(snap.todos.items.length ? Math.round((done / snap.todos.items.length) * 100) : 0, `${done}/${snap.todos.items.length}`));
    todoCard.appendChild(head);
    if (!snap.todos.found || !snap.todos.items.length) todoCard.appendChild(empty("doc", "Henüz yok."));
    else {
      const list = h("div", { class: "todo-list" });
      const sorted = [...snap.todos.items].sort((a, b) => (a.done === b.done ? 0 : a.done ? 1 : -1));
      for (const t of sorted) list.appendChild(todoRow(t));
      todoCard.appendChild(list);
    }
    el.appendChild(todoCard);

    // ---- expected ----
    if (snap.expected?.length) {
      const expCard = h("div", { class: "card hoverable", style: { "--i": 4 } });
      expCard.appendChild(h("div", { class: "card-head" }, icon("target"), h("div", { class: "card-title" }, "Senden beklenenler")));
      const wrap = h("div", { class: "grid grid-2" });
      for (const grp of snap.expected) {
        wrap.appendChild(h("div", {},
          h("div", { style: { fontWeight: 800, fontSize: "14.5px", marginBottom: "8px", color: "var(--teal)" } }, grp.project),
          ...grp.items.map((it) => h("div", { class: "bullet", style: { marginBottom: "6px" } }, it))));
      }
      expCard.appendChild(wrap);
      el.appendChild(expCard);
    }

    // ---- gunu kapat ----
    el.appendChild(h("div", { style: { display: "flex", justifyContent: "center", "--i": 5 } },
      btn("Günü kapat", { variant: "secondary", icon: "commit", onClick: async () => { try { await request("runShortcut", { id: "devir" }); toast("Günün kapatıldı. İyi çalışmalar.", "success"); } catch (e) { toast(e.message || "Olmadı.", "error"); } } })));

    bindTooltips(el);
  }

  function miniStat(num, label, sub, onClick) {
    const m = h("div", { class: "mini", style: onClick ? { cursor: "pointer" } : {} },
      h("div", { class: "num" }, num), h("div", { class: "lbl" }, label), h("div", { class: "txt" }, sub));
    if (onClick) m.addEventListener("click", onClick);
    return m;
  }

  function ringSmall(pct, label) {
    const wrap = h("div", { class: "ring-wrap", style: { width: "44px", height: "44px", marginLeft: "auto" } });
    const size = 44, stroke = 5, r = (size - stroke) / 2, c = 2 * Math.PI * r;
    const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    svg.setAttribute("width", size); svg.setAttribute("height", size); svg.style.transform = "rotate(-90deg)";
    const bg = document.createElementNS(svg.namespaceURI, "circle"); bg.setAttribute("cx", size / 2); bg.setAttribute("cy", size / 2); bg.setAttribute("r", r); bg.setAttribute("fill", "none"); bg.setAttribute("stroke", "var(--grey-soft)"); bg.setAttribute("stroke-width", stroke);
    const fg = document.createElementNS(svg.namespaceURI, "circle"); fg.setAttribute("cx", size / 2); fg.setAttribute("cy", size / 2); fg.setAttribute("r", r); fg.setAttribute("fill", "none"); fg.setAttribute("stroke", "var(--teal)"); fg.setAttribute("stroke-width", stroke); fg.setAttribute("stroke-linecap", "round"); fg.setAttribute("stroke-dasharray", c);
    requestAnimationFrame(() => fg.setAttribute("stroke-dashoffset", String(c - (pct / 100) * c)));
    fg.setAttribute("stroke-dashoffset", c);
    svg.appendChild(bg); svg.appendChild(fg); wrap.appendChild(svg);
    wrap.appendChild(h("div", { class: "ring-num", style: { fontSize: "10.5px" } }, label));
    return wrap;
  }

  function todoRow(t) {
    const circle = h("div", { class: "todo-circle" + (t.done ? " done" : "") });
    if (t.done) circle.appendChild(icon("check"));
    const text = h("div", { class: "todo-text" + (t.done ? " done" : "") }, t.text);
    const row = h("div", { class: "todo-row" }, circle, text);
    circle.addEventListener("click", () => onToggle(t.key, row, circle, text));
    row.addEventListener("click", (e) => { if (e.target === row || e.target === text) onToggle(t.key, row, circle, text); });
    return row;
  }

  async function onToggle(key, row, circle, text) {
    const list = row.parentElement;
    const before = new Map(Array.from(list.children).map((c) => [c, c.getBoundingClientRect()]));
    const wasDone = circle.classList.contains("done");
    circle.classList.toggle("done", !wasDone);
    circle.innerHTML = ""; if (!wasDone) circle.appendChild(icon("check"));
    text.classList.toggle("done", !wasDone);
    if (!wasDone) list.appendChild(row); else list.insertBefore(row, list.firstChild);
    flip(list, before);
    try {
      const res = await request("toggleTodo", { key });
      if (res.celebrate) { confetti(); toast("Aferin, bitti!", "success"); }
      const snap = store.get("snapshot");
      const t = snap?.todos?.items?.find((x) => x.key === key);
      if (t) t.done = res.done;
    } catch (e) {
      circle.classList.toggle("done", wasDone);
      text.classList.toggle("done", wasDone);
      toast(e.message || "Değişmedi, liste yenilendi.", "error");
      request("refresh").catch(() => {});
    }
  }
  function flip(list, before) {
    const after = new Map(Array.from(list.children).map((c) => [c, c.getBoundingClientRect()]));
    for (const [child, oldRect] of before) {
      const newRect = after.get(child);
      if (!newRect) continue;
      const dy = oldRect.top - newRect.top;
      if (Math.abs(dy) < 1) continue;
      child.style.transition = "none";
      child.style.transform = `translateY(${dy}px)`;
      requestAnimationFrame(() => { child.style.transition = "transform 320ms cubic-bezier(.2,.8,.2,1)"; child.style.transform = ""; });
    }
  }

  // A2: the backend always sends the FULL command in `komut` (bridge has no separate short/long fields —
  // see MIMARI §3.6 SafetyDto and Bridge/Dto.cs); the card itself decides whether to show it shortened and,
  // if so, offers "Tamamını gör" for the same string. Kept well under the safety gate's 4000-char cap.
  const KOMUT_KISA_LIMIT = 280;

  function safetyCard(s) {
    const card = h("div", { class: "card", style: { background: "var(--surface-2)", boxShadow: "none" } });
    // A2: the card is tied to the *specific* queued record via its hash summary (`summary`, matches
    // Bridge/Dto.cs SafetyDto.Summary). If the queue changed underneath us (bekleyenler.jsonl rewritten)
    // since this card was drawn, decide() below finds out from the server (`stale`) and we replace the
    // actions with a "değişmiş, onaylanamaz" notice in place.
    const komutTam = s.komut || "";
    const kisaltildi = komutTam.length > KOMUT_KISA_LIMIT;
    const commandBox = h("div", { class: "mono-box", style: { marginTop: "8px" } }, kisaltildi ? komutTam.slice(0, KOMUT_KISA_LIMIT) + "…" : komutTam);
    const commandWrap = h("div", { style: { marginTop: "0" } }, commandBox);
    if (kisaltildi) {
      commandWrap.appendChild(h("a", { class: "link", style: { display: "inline-block", marginTop: "6px", cursor: "pointer", color: "var(--teal)", fontWeight: 700, fontSize: "12.5px" }, onclick: () => showFullCommand(komutTam) }, "Tamamını gör →"));
    }
    card.appendChild(h("div", { style: { display: "flex", gap: "10px", alignItems: "flex-start" } },
      icon("alert", "tone-" + (s.critical ? "risk" : "dikkat")),
      h("div", { style: { flex: 1 } },
        h("div", { style: { fontWeight: 700, fontSize: "15px" } }, `${s.tool} bunu yapmak istiyor: ${s.islem}`),
        h("div", { style: { fontSize: "13px", color: "var(--muted)", marginTop: "2px" } }, s.hedef),
        commandWrap,
        h("div", { style: { fontSize: "12.5px", color: "var(--muted)", marginTop: "6px" } }, s.neden))));
    const actionsSlot = h("div", { style: { marginTop: "12px" } });
    actionsSlot.appendChild(h("div", { style: { display: "flex", gap: "8px" } },
      btn("Reddet", { variant: "ghost", sm: true, icon: "x", onClick: () => decide(s, false, actionsSlot) }),
      btn("Onayla", { variant: s.critical ? "danger" : "primary", sm: true, icon: "check", disabled: s.kesildi, onClick: () => decide(s, true, actionsSlot) })));
    // Onayla kapalıyken sebebi söylenmezse kullanıcı düğmenin neden çalışmadığını anlamıyor: kesik komutta açıklama göster.
    if (s.kesildi) {
      actionsSlot.appendChild(h("div", { class: "help-text", style: { marginTop: "8px", fontSize: "12.5px", color: "var(--muted)" } },
        "Bu komut çok uzun olduğu için tamamı kayda geçmedi; tamamını görmediğin bir komut onaylanamaz. " +
        "Reddet ve yapay zekâdan komutu kısaltmasını ya da işi küçük adımlara bölmesini iste."));
    }
    card.appendChild(actionsSlot);
    return card;
  }
  async function showFullCommand(komutTam) {
    const { modal } = await import("../ui.js");
    await modal({ title: "Komutun tamamı", body: h("div", { class: "mono-box", style: { maxHeight: "50vh", overflow: "auto" } }, komutTam), actions: [{ text: "Kapat", value: true, variant: "primary" }] });
  }
  function markChanged(actionsSlot) {
    actionsSlot.innerHTML = "";
    actionsSlot.appendChild(h("div", { class: "banner warn", style: { borderRadius: "10px", padding: "10px 14px" } },
      icon("alert"), h("span", {}, "Bu istek değişti, onaylanamaz. Liste yenilendi."), h("div", { class: "spacer" })));
  }
  async function decide(s, approve, actionsSlot) {
    let confirmCritical = false;
    if (approve && s.critical) {
      const box = h("label", { style: { display: "flex", gap: "8px", alignItems: "flex-start", fontSize: "14px", cursor: "pointer" } });
      const cb = h("input", { type: "checkbox" });
      box.appendChild(cb); box.appendChild(document.createTextNode("Komutu okudum, bir kez yapılsın."));
      const body = h("div", {}, h("p", {}, `${s.tool} şunu yapmak istiyor: ${s.islem}`), h("div", { class: "mono-box" }, s.komut), box);
      const { modal } = await import("../ui.js");
      const ok = await modal({ title: "Kritik onay", body, actions: [{ text: "Vazgeç", value: false, variant: "ghost" }, { text: "Onayla", value: true, variant: "danger" }] });
      if (!ok || !cb.checked) { if (ok && !cb.checked) toast("Kutucuğu işaretlemeden onaylanamaz.", "warn"); return; }
      confirmCritical = true;
    }
    try {
      // summary (matches Bridge/Payloads.cs DecideSafetyPayload.Summary) ties this decision to the exact
      // queue record shown here (id alone is not enough if the file was rewritten with a different command
      // under the same id) — a mismatch comes back as `stale`.
      await request("decideSafety", { id: s.id, approve, confirmCritical, summary: s.summary });
      toast(approve ? "Onaylandı." : "Reddedildi.", approve ? "success" : "info");
      request("refresh").catch(() => {});
    } catch (e) {
      if (e.code === "stale") { markChanged(actionsSlot); request("refresh").catch(() => {}); }
      else toast(e.message || "Olmadı.", "error");
    }
  }

  return { id: "masam", mount, update, unmount };
}
