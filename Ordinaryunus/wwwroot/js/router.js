// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
// Hash router. Page module contract: { id, mount(el, ctx), update(ctx, key), unmount() }.
import * as store from "./store.js";
import { disposeChartsIn } from "./charts.js";

const pages = new Map(); // id -> module
let current = null; // { mod, el }
let contentEl = null;

export function register(mod) { pages.set(mod.id, mod); }

export function contentRoot() { return contentEl; }

function parseHash() {
  const raw = location.hash.replace(/^#\/?/, "");
  const [pathPart, queryPart] = raw.split("?");
  const segs = pathPart.split("/").filter(Boolean);
  const query = Object.fromEntries(new URLSearchParams(queryPart || ""));
  let page = segs[0] || "masam";
  let tab = segs[1] || null;
  return { page, tab, segs, query };
}

export function currentRoute() { return parseHash(); }

export function navigate(hash) {
  if (location.hash === hash) { window.dispatchEvent(new HashChangeEvent("hashchange")); return; }
  location.hash = hash;
}

export function init(root) {
  contentEl = root;
  window.addEventListener("hashchange", render);
}

function pageIdFor(route) {
  if (route.page === "beyin") return "beyin";
  return pages.has(route.page) ? route.page : "masam";
}

export function render() {
  const route = parseHash();
  store.set("route", route);
  const id = pageIdFor(route);
  const mod = pages.get(id);
  if (!mod) return;
  if (current && current.mod !== mod) {
    disposeChartsIn(current.el);
    try { current.mod.unmount?.(); } catch (e) { console.error(e); }
    current.el.remove();
  }
  if (!current || current.mod !== mod) {
    const el = document.createElement("div");
    el.className = "page page-enter stagger";
    contentEl.appendChild(el);
    current = { mod, el };
    mod.mount(el, route);
  } else {
    mod.update?.(route, "route");
  }
  document.dispatchEvent(new CustomEvent("routechange", { detail: route }));
}

// Maps a Target (see MIMARI §3.4) to either a hash route or a bridge call.
export function openTarget(target, helpers) {
  if (!target) return;
  switch (target.type) {
    case "note":
      navigate(`#/beyin/kesif?path=${encodeURIComponent(target.path)}${target.anchor ? "&a=" + encodeURIComponent(target.anchor) : ""}`);
      break;
    case "project":
      navigate(`#/beyin/proje/${encodeURIComponent(target.name)}${target.section ? "?s=" + target.section : ""}`);
      break;
    case "page":
      navigate(`#/${target.page}${target.tab ? "/" + target.tab : ""}`);
      break;
    case "job":
      helpers?.openJobDrawer?.(target.id);
      break;
    case "ledger":
      navigate(`#/gecmis${target.day ? "?day=" + target.day : ""}`);
      break;
    case "url":
      helpers?.openUrl?.(target.url);
      break;
  }
}
