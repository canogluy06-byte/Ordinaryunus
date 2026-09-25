// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
// Bridge: talks to C# via window.chrome.webview when present, else to the mock bridge.
// Public API: request(type, payload, opts) -> Promise<payload>; on(type, fn) -> unsubscribe; isMock boolean.
import { mockRequest, mockSubscribe } from "./mock.js";

const hasWebView = !!(window.chrome && window.chrome.webview);
export const isMock = !hasWebView;

let nextId = 1;
const pending = new Map(); // id -> {resolve, reject, timer, type}
const listeners = new Map(); // type -> Set(fn)

const TIMEOUTS = { runShortcut: 70000, login: 15000 };
const DEFAULT_TIMEOUT = 30000;

function emit(type, payload) {
  const set = listeners.get(type);
  if (!set) return;
  for (const fn of set) {
    try { fn(payload); } catch (e) { console.error("listener error", type, e); }
  }
}

if (hasWebView) {
  window.chrome.webview.addEventListener("message", (ev) => {
    let msg = ev.data;
    if (typeof msg === "string") {
      try { msg = JSON.parse(msg); } catch { return; }
    }
    if (!msg || typeof msg !== "object") return;
    if (msg.id === 0) {
      emit(msg.type, msg.payload);
      return;
    }
    const p = pending.get(msg.id);
    if (!p) return;
    pending.delete(msg.id);
    clearTimeout(p.timer);
    if (msg.ok) p.resolve(msg.payload);
    else p.reject(msg.error || { code: "failed", message: "Bilinmeyen hata." });
  });
} else {
  // Mock event channel: mockSubscribe calls back with (type, payload) for events.
  mockSubscribe((type, payload) => emit(type, payload));
}

export function request(type, payload = {}, opts = {}) {
  const timeout = opts.timeout || TIMEOUTS[type] || DEFAULT_TIMEOUT;
  if (!hasWebView) {
    return mockRequest(type, payload, timeout);
  }
  const id = nextId++;
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      pending.delete(id);
      reject({ code: "failed", message: "Zaman aşımı." });
    }, timeout);
    pending.set(id, { resolve, reject, timer, type });
    try {
      window.chrome.webview.postMessage(JSON.stringify({ id, type, payload }));
    } catch (e) {
      clearTimeout(timer);
      pending.delete(id);
      reject({ code: "failed", message: String(e) });
    }
  });
}

export function on(type, fn) {
  if (!listeners.has(type)) listeners.set(type, new Set());
  listeners.get(type).add(fn);
  return () => listeners.get(type)?.delete(fn);
}
