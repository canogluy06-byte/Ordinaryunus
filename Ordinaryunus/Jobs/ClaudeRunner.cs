// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
namespace Ordinaryunus.Jobs;

// Retired (EK-v2.1 §1-2): Ordinaryunus no longer starts claude.exe directly and ties its lifetime to this
// app's own Process object. "runClaude"/"runCodex" now hand the job to the vault's own job watchman
// (_sistem/araclar/is-nobetcisi.mjs) as a detached process, so a Claude/Codex job keeps running even if Ordinaryunus
// is closed, survives quota waits on its own, and resumes the same session once the quota resets. See
// Jobs/NobetciRunner.cs (start/cancel/retry-now signals) and Jobs/JobStore.cs (polls the watchman's own
// <damga>.jsonl / <damga>.nobet.json files instead of reading a redirected stdout stream).
//
// This file is kept in place (not deleted — file/folder deletion outside %TEMP% goes through the safety gate and a
// pending approval, #20260924-4b7f04, is queued for it) but intentionally defines nothing anymore: ClaudeCliNotFoundException
// moved to Jobs/ClaudeCli.cs, and the old ClaudeJob/ClaudeRunner types have no replacement (NobetciRunner + JobStore's
// TrackedJob cover the same ground).
