// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../bridge.js";
import { h, icon, btn, ring, toast, confetti } from "../ui.js";

let active = false;
let currentClose = null;
export function isFocusActive() { return active; }
// Screenshot mode chains several "shot" events (MIMARI §9.3) without any real user click, so it needs a way to
// dismiss whatever Focus session is on screen before moving to the next page — otherwise this full-viewport
// overlay (unlike the router-driven pages) never goes away and silently covers every screenshot after it.
export function closeFocus() { currentClose?.(); }

export function openFocus(task) {
  const root = document.getElementById("focus-overlay");
  root.innerHTML = "";
  root.classList.remove("hidden");
  active = true;

  let minutes = 25, seconds = minutes * 60, total = seconds, timer = null, done = false;

  const ringWrap = h("div", { class: "focus-ring-big" });
  const timerLabel = h("div", { class: "focus-timer" }, "25:00");

  function drawRing() {
    ringWrap.innerHTML = "";
    const size = 260, stroke = 12, r = (size - stroke) / 2, c = 2 * Math.PI * r;
    const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    svg.setAttribute("width", size); svg.setAttribute("height", size);
    svg.style.transform = "rotate(-90deg)";
    const bg = document.createElementNS(svg.namespaceURI, "circle");
    bg.setAttribute("cx", size / 2); bg.setAttribute("cy", size / 2); bg.setAttribute("r", r);
    // A12: track/ring colours follow the current theme (tokens.css) instead of a hardcoded dark palette,
    // so Odak modu is light when the app theme is light.
    bg.setAttribute("fill", "none"); bg.setAttribute("stroke", "var(--grey-soft)"); bg.setAttribute("stroke-width", stroke);
    const fg = document.createElementNS(svg.namespaceURI, "circle");
    fg.setAttribute("cx", size / 2); fg.setAttribute("cy", size / 2); fg.setAttribute("r", r);
    fg.setAttribute("fill", "none"); fg.setAttribute("stroke", "var(--teal)"); fg.setAttribute("stroke-width", stroke);
    fg.setAttribute("stroke-linecap", "round"); fg.setAttribute("stroke-dasharray", c);
    fg.id = "focus-fg-circle"; fg.dataset.c = c;
    svg.appendChild(bg); svg.appendChild(fg);
    ringWrap.appendChild(svg);
    ringWrap.appendChild(timerLabel);
    ringWrap.style.position = "relative";
    timerLabel.style.position = "absolute"; timerLabel.style.inset = "0"; timerLabel.style.display = "flex";
    timerLabel.style.alignItems = "center"; timerLabel.style.justifyContent = "center";
    updateRing();
  }
  function updateRing() {
    const fg = ringWrap.querySelector("#focus-fg-circle");
    if (!fg) return;
    const c = parseFloat(fg.dataset.c);
    const pct = seconds / total;
    fg.setAttribute("stroke-dashoffset", String(c * (1 - pct)));
    const m = Math.floor(seconds / 60), s = seconds % 60;
    timerLabel.textContent = `${String(m).padStart(2, "0")}:${String(s).padStart(2, "0")}`;
  }

  const choiceRow = h("div", { style: { display: "flex", gap: "10px" } });
  [25, 50].forEach((m) => {
    const b = btn(m + " dk", { variant: m === 25 ? "primary" : "ghost", onClick: () => { minutes = m; seconds = total = m * 60; drawRing(); choiceRow.querySelectorAll("button").forEach((x) => x.className = "btn btn-ghost"); b.className = "btn btn-primary"; } });
    choiceRow.appendChild(b);
  });

  const actions = h("div", { style: { display: "flex", gap: "12px" } },
    btn("Bitti", { variant: "primary", icon: "check", onClick: finish }),
    btn("Vazgeç", { variant: "ghost", onClick: close }),
  );

  root.appendChild(h("div", { class: "eyebrow" }, "ODAK MODU"));
  root.appendChild(h("div", { class: "focus-task" }, task?.text || "Tek işine odaklan."));
  root.appendChild(choiceRow);
  drawRing();
  root.appendChild(ringWrap);
  root.appendChild(actions);

  let lastActivityPing = 0;
  function tick() {
    if (done) return;
    seconds--;
    if (seconds < 0) { seconds = 0; finish(); return; }
    updateRing();
    const now = Date.now();
    if (now - lastActivityPing > 29000) { lastActivityPing = now; request("activity", { focusRunning: true }).catch(() => {}); }
  }
  timer = setInterval(tick, 1000);

  function onKey(e) {
    if (e.key === "Escape") close();
  }
  document.addEventListener("keydown", onKey);

  async function finish() {
    if (done) return; done = true;
    clearInterval(timer);
    if (task?.todoKey) {
      try { const res = await request("toggleTodo", { key: task.todoKey }); if (res.celebrate) confetti(); } catch {}
    } else { confetti(); }
    toast("Odak seansı bitti. Aferin!", "success");
    close();
  }
  function close() {
    done = true; clearInterval(timer);
    document.removeEventListener("keydown", onKey);
    root.classList.add("hidden");
    active = false;
    currentClose = null;
    // A3: while Odak runs we keep telling C# focusRunning:true (so idle auto-lock stays suppressed for the
    // whole session even without mouse movement). Without this final ping, C# would still believe a focus
    // countdown is running after we close and idle auto-lock would never resume. Also resets lastActivity
    // to now, so the user isn't locked out the instant a long focus session ends.
    request("activity", { focusRunning: false }).catch(() => {});
  }
  currentClose = close;
}
