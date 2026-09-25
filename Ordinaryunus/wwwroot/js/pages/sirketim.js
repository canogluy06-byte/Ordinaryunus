// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../bridge.js";
import * as store from "../store.js";
import { h, icon, chip, btn, empty, fmtRelative, fmtTime, toast, confirmDialog, bindTooltips, scheduledTaskLabel } from "../ui.js";

const JOB_CHIP = { giris: ["Giriş gerekli", "red"], kota: ["Kota dolu", "yellow"], "kota-bekliyor": ["Kota bekliyor", "yellow"], hata: ["Hata", "red"] };

// "2 sa 14 dk kaldı" / "14 dk kaldı" / "Devam ediyor…" once resumeAt has passed (EK-v2.1 §1.3).
function fmtCountdown(ms) {
  if (ms <= 0) return "Devam ediyor…";
  const totalMin = Math.ceil(ms / 60000);
  const hrs = Math.floor(totalMin / 60), mins = totalMin % 60;
  return (hrs > 0 ? `${hrs} sa ` : "") + `${mins} dk kaldı`;
}

export default function sirketimPage(helpers) {
  let el, unsub, unsubJobs, tickTimer;

  function mount(root) {
    el = root;
    render(store.get("snapshot"));
    unsub = store.on("snapshot", render);
    // jobUpdated (main.js) patches store's "jobs" Map and mutates snapshot.company.jobs in place, but does
    // NOT re-publish "snapshot" — without this, a running job's state (e.g. kota-bekliyor -> çalışıyor after
    // "Şimdi dene", or -> durduruldu after "Durdur"/"İptal") would only show up after leaving and reopening
    // this page. Re-rendering from the same (already-updated) snapshot object is cheap and always current.
    unsubJobs = store.on("jobs", () => render(store.get("snapshot")));
    tickTimer = setInterval(() => tickElapsed(), 1000);
  }
  function unmount() { unsub?.(); unsubJobs?.(); clearInterval(tickTimer); }
  function update() { render(store.get("snapshot")); }

  function tickElapsed() {
    el?.querySelectorAll("[data-elapsed-start]").forEach((n) => {
      const start = new Date(n.dataset.elapsedStart).getTime();
      const sec = Math.max(0, Math.floor((Date.now() - start) / 1000));
      n.textContent = fmtElapsed(sec);
    });
    el?.querySelectorAll("[data-resume-at]").forEach((n) => {
      const resume = new Date(n.dataset.resumeAt).getTime();
      n.textContent = fmtCountdown(resume - Date.now());
    });
  }
  function fmtElapsed(sec) { const m = Math.floor(sec / 60), s = sec % 60; return `${m}:${String(s).padStart(2, "0")}`; }

  function render(snap) {
    el.innerHTML = "";
    if (!snap) { el.appendChild(h("div", { class: "empty" }, "Yükleniyor…")); return; }
    el.appendChild(h("div", { class: "eyebrow" }, "ŞİRKETİM"));
    el.appendChild(h("div", { class: "page-title" }, "Yapay zekâ ekibim"));
    const cli = snap.claudeCli;
    el.appendChild(h("div", { class: "page-sub" }, `Claude komut satırı ${cli.found ? (cli.version || "bulundu") : "bulunamadı"} · giriş: ${cli.auth === "tamam" ? "tamam" : cli.auth === "gerekli" ? "gerekli" : "bilinmiyor"}`));
    // C (EK-v2.1 §2b): keepAwake lives under `company` in the bridge (Bridge/Dto.cs CompanyDto.KeepAwake),
    // not at the snapshot root — matches Mapper.MapCompany().
    if (snap.company.keepAwake) {
      const ka = snap.company.keepAwake;
      el.appendChild(h("div", { class: "help-text", style: { display: "flex", alignItems: "center", gap: "6px", marginTop: "2px" } },
        icon("moon", ""), ka.on ? `Bilgisayar uyanık tutuluyor: ${ka.reason}` : "Bilgisayar uyanık tutulmuyor: şu an gereken iş yok."));
    }

    const card = h("div", { class: "card", style: { "--i": 1 } });
    card.appendChild(h("div", { class: "card-head" }, icon("cpu"), h("div", { class: "card-title" }, "Çalışan işler")));

    const claudeJobs = snap.company.jobs.filter((j) => j.tool === "claude");
    const codexJobs = snap.company.jobs.filter((j) => j.tool === "codex");
    group(card, "Claude işleri", claudeJobs.map(jobRow), "Şu an bir Claude işi yok.");
    group(card, "Codex işleri", codexJobs.map(jobRow), "Şu an bir Codex işi yok.");
    group(card, "Claude oturumları (son 15 dk)", (snap.company.claudeSessions || []).map(sessionRow), "Aktif oturum yok.");
    group(card, "Zamanlanmış görevler", (snap.company.scheduled || []).map(scheduledRow), "Tanımlı görev yok.");

    const tg = snap.company.tamGaz, watch = snap.company.watch;
    const tgRow = h("div", { style: { display: "flex", gap: "10px", flexWrap: "wrap", alignItems: "center" } },
      chip(tg.on ? `Tam Gaz · ${fmtTime(tg.end)}'a kadar` : "Tam Gaz kapalı", tg.on ? "teal" : "grey", { pulse: tg.on }),
      chip(watch.running ? "Codex nöbeti çalışıyor" : watch.waitingQuota ? "Codex kotası bekleniyor" : "Codex nöbeti boşta", watch.running ? "teal" : "grey", { pulse: watch.running }),
      tg.on ? btn("Kapat", { sm: true, variant: "danger", icon: "stop", onClick: async () => {
        const ok = await confirmDialog("Tam Gaz kapatılsın mı?", { okText: "Kapat" });
        if (!ok) return;
        try { await request("tamGazOff", {}); toast("Tam Gaz kapatıldı.", "success"); } catch (e) { toast(e.message || "Kapatılamadı.", "error"); }
      } }) : null);
    group(card, "Tam Gaz ve Codex nöbeti", [tgRow, h("div", { class: "mono-box", style: { fontSize: "12px" } }, (snap.company.tamGazLog || []).join("\n") || "Kayıt yok.")], null, true);

    const lockRows = (snap.company.locks || []).map((l) => h("div", { class: "work-row" }, h("div", { class: "work-dot", style: { background: l.stale ? "var(--red)" : "var(--yellow)" } }),
      h("div", { class: "work-body" }, h("div", { class: "work-title" }, l.project), h("div", { class: "work-meta" }, `${l.holder} · ${l.hours ?? "?"} saattir${l.stale ? " (eski)" : ""}`))));
    const taskRows = (snap.company.activeTasks || []).map((t) => h("div", { class: "work-row" }, h("div", { class: "work-dot", style: { background: "var(--teal)" } }),
      h("div", { class: "work-body" }, h("div", { class: "work-title" }, `${t.kimlik} · ${t.title}`), h("div", { class: "work-meta" }, `${t.project} · ${t.durumText} · ${t.atanan}`))));
    group(card, "Kilitler ve verilen görevler", [...lockRows, ...taskRows], "Açık kilit ya da görev yok.");

    el.appendChild(card);

    const rolesCard = h("div", { class: "card", style: { "--i": 2 } });
    rolesCard.appendChild(h("div", { class: "card-head" }, icon("sirketim"), h("div", { class: "card-title" }, "Roller")));
    const byDept = {};
    for (const r of snap.roles) (byDept[r.departman] ||= []).push(r);
    const deptOrder = Object.keys(byDept).sort((a, b) => (a === "Yönetim" ? -1 : b === "Yönetim" ? 1 : 0));
    for (const dep of deptOrder) {
      rolesCard.appendChild(h("div", { class: "work-group-label" }, dep));
      const grid = h("div", { class: "grid grid-3" });
      for (const r of byDept[dep]) grid.appendChild(roleTile(r));
      rolesCard.appendChild(grid);
    }
    el.appendChild(rolesCard);
    bindTooltips(el);
  }

  function group(card, label, rows, emptyText, skipLabel) {
    if (!skipLabel) card.appendChild(h("div", { class: "work-group-label" }, label));
    if (!rows.length && emptyText) { card.appendChild(h("div", { class: "help-text", style: { padding: "6px 4px 14px" } }, emptyText)); return; }
    rows.forEach((r) => card.appendChild(r instanceof Node ? r : h("div", {}, r)));
  }

  function jobRow(job) {
    const waitingQuota = job.state === "kota-bekliyor";
    const running = job.state === "calisiyor" || job.state === "basliyor";
    const dot = h("div", { class: "work-dot" + (running ? " breathing" : ""), style: { background: colorForTone(job.tone) } });
    const chips = h("div", { style: { display: "flex", gap: "6px", flexWrap: "wrap", marginTop: "4px" } });
    if (JOB_CHIP[job.state]) chips.appendChild(chip(JOB_CHIP[job.state][0], JOB_CHIP[job.state][1]));
    job.denials?.forEach((d) => chips.appendChild(chip("İzin verilmedi: " + d, "yellow")));
    job.safetyIds?.forEach((id) => chips.appendChild(chip("Onayını bekliyor #" + id, "yellow")));
    if (job.quiet) chips.appendChild(chip("10 dakikadır ses yok", "grey"));
    const elapsedEl = h("span", { "data-elapsed-start": job.start }, fmtElapsed(job.elapsedSec || 0));
    const metaParts = [job.now];
    if (!running) metaParts.push(job.stateText);
    if (job.steps) metaParts.push(`${job.steps} adım`);
    if (job.attempt > 1) metaParts.push(`${job.attempt}. deneme`);
    const metaLine = h("div", { class: "work-meta" }, metaParts.filter(Boolean).join(" · "));
    if (running) { metaLine.appendChild(document.createTextNode(" · ")); metaLine.appendChild(elapsedEl); }
    if (waitingQuota && job.resumeAt) { metaLine.appendChild(document.createTextNode(" · ")); metaLine.appendChild(h("span", { "data-resume-at": job.resumeAt, style: { fontWeight: 700, color: "var(--yellow-text)" } }, fmtCountdown(new Date(job.resumeAt).getTime() - Date.now()))); }
    const body = h("div", { class: "work-body" }, h("div", { class: "work-title" }, job.title), metaLine, chips);
    if (job.state === "giris" && job.fix) {
      body.appendChild(h("div", { class: "banner", style: { marginTop: "10px", borderRadius: "10px" } }, icon("alert"),
        h("div", { class: "spacer" }, h("div", {}, job.fix), h("div", { class: "help-text" }, "1. Başlat'a Terminal yaz ve aç. 2. Komutu yapıştır (Ctrl+V), Enter. 3. Açılan Claude'da /login yaz, tarayıcıda onayla. 4. Buraya dön, işi yeniden ver.")),
        btn("Komutu kopyala", { sm: true, variant: "ghost", onClick: () => request("copyText", { text: store.get("snapshot")?.claudeCli?.loginCommand || "" }) })));
    }
    const right = h("div", { class: "work-right" },
      btn("Detay", { sm: true, variant: "ghost", onClick: () => helpers.openJobDrawer(job.id) }),
      waitingQuota ? btn("Şimdi dene", { sm: true, variant: "secondary", icon: "play", onClick: async () => { try { await request("retryJobNow", { id: job.id }); toast("Şimdi deneniyor…", "info"); } catch (e) { toast(e.message || "Olmadı.", "error"); } } }) : null,
      waitingQuota ? btn("İptal", { sm: true, variant: "danger", onClick: async () => { const ok = await confirmDialog("Bu iş iptal edilsin mi?", { danger: true, okText: "İptal et" }); if (ok) { await request("cancelJob", { id: job.id }); } } }) : null,
      (!waitingQuota && job.canCancel) ? btn("Durdur", { sm: true, variant: "danger", onClick: async () => { const ok = await confirmDialog("Bu iş durdurulsun mu?", { danger: true, okText: "Durdur" }); if (ok) { await request("cancelJob", { id: job.id }); } } }) : null);
    return h("div", { class: "work-row" }, dot, body, right);
  }
  function colorForTone(tone) { return { teal: "var(--teal)", green: "var(--green)", red: "var(--red)", yellow: "var(--yellow)", grey: "var(--grey-mid)" }[tone] || "var(--grey-mid)"; }

  function sessionRow(s) {
    return h("div", { class: "work-row" }, h("div", { class: "work-dot", style: { background: s.state === "aktif" ? "var(--teal)" : s.state === "izin" ? "var(--yellow)" : "var(--grey-mid)" } }),
      h("div", { class: "work-body" }, h("div", { class: "work-title" }, s.title), h("div", { class: "work-meta" }, `${s.projectDir} · ${fmtRelative(s.lastActivity)}${s.state === "izin" ? " · izin bekliyor olabilir" : ""}`)));
  }
  function scheduledRow(t) {
    // t.nextRun is a ready-to-show Turkish phrase from the backend ("bugün 21:30", "haftalık", "27 Eyl Paz 19:00",
    // "her saat (Tam Gaz açıkken) · sonraki 10:14") — not an ISO timestamp, so it is shown as is, never through fmtTime.
    // t.name may still be the raw scheduled-task folder slug (e.g. "aksam-analizi") if the backend has not
    // mapped it yet — scheduledTaskLabel() always shows a Turkish title (E).
    return h("div", { class: "work-row" }, h("div", { class: "work-dot", style: { background: "var(--slate-2)" } }),
      h("div", { class: "work-body" }, h("div", { class: "work-title" }, scheduledTaskLabel(t.name)), h("div", { class: "work-meta" }, t.description + (t.nextRun ? " · sıradaki: " + t.nextRun : ""))));
  }
  function roleTile(r) {
    const tile = h("div", { class: "card hoverable", style: { boxShadow: "none", background: "var(--surface-2)", padding: "14px" } },
      h("div", { style: { fontWeight: 800, fontSize: "14.5px" } }, r.title),
      h("div", { style: { fontSize: "12px", color: "var(--muted)", margin: "4px 0 10px", minHeight: "32px" } }, r.ozet),
      h("div", { style: { display: "flex", gap: "6px" } },
        btn("İş ver", { sm: true, variant: "primary", onClick: () => helpers.openGiveWork({ role: r.title }) }),
        btn("Kartı aç", { sm: true, variant: "ghost", onClick: () => request("openInObsidian", { path: r.path }) })));
    return tile;
  }

  return { id: "sirketim", mount, update, unmount };
}
