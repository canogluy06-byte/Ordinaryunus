// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
// Tiny in-memory store with per-key subscriptions.
const data = new Map();
const subs = new Map(); // key -> Set(fn)

export function get(key) { return data.get(key); }
export function set(key, value) {
  data.set(key, value);
  const s = subs.get(key);
  if (s) for (const fn of s) fn(value);
}
export function patch(key, partial) {
  const cur = data.get(key) || {};
  const next = { ...cur, ...partial };
  set(key, next);
  return next;
}
export function on(key, fn) {
  if (!subs.has(key)) subs.set(key, new Set());
  subs.get(key).add(fn);
  return () => subs.get(key)?.delete(fn);
}

// Jobs kept as a Map<id, Job> for O(1) patch-in-place.
export function jobsMap() {
  if (!data.has("jobs")) data.set("jobs", new Map());
  return data.get("jobs");
}
export function setJobsFromList(list) {
  const m = new Map();
  for (const j of list) m.set(j.id, j);
  data.set("jobs", m);
  const s = subs.get("jobs");
  if (s) for (const fn of s) fn(m);
}
export function patchJob(job) {
  const m = jobsMap();
  m.set(job.id, job);
  const s = subs.get("jobs");
  if (s) for (const fn of s) fn(m);
}

// Small LRU for rendered notes.
const noteCache = new Map();
export function cacheNote(path, note) {
  noteCache.delete(path);
  noteCache.set(path, note);
  if (noteCache.size > 30) noteCache.delete(noteCache.keys().next().value);
}
export function getCachedNote(path) { return noteCache.get(path); }
export function clearNoteCache() { noteCache.clear(); }
