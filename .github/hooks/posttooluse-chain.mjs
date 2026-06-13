#!/usr/bin/env node
// posttooluse-chain.mjs — Cross-platform PostToolUse fan-out wrapper.
//
// Replaces posttooluse-chain.ps1 (Windows-only) as the default wiring.
// Works identically on Windows, macOS, and Linux — no PowerShell required.
// Wired via .github/hooks/context-mode.json PostToolUse command.
//
// Pipes the Copilot hook envelope (stdin) to both:
//   1. Upstream context-mode hook  (docker exec — session DB event capture)
//   2. Host-side auto-cache        (auto-cache.mjs — RAG → FTS5 auto-index)
//
// Logging: default ON (writes to auto-cache.log, same file as auto-cache.mjs).
// Set AUTO_CACHE_DEBUG=0 to silence all output.
//
// Best-effort: any failure is swallowed. Always exits 0 so the hook chain
// stays alive regardless of Docker / Node errors.

import { execFileSync }  from "node:child_process";
import { appendFileSync } from "node:fs";
import { join, dirname }  from "node:path";
import { fileURLToPath }  from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const LOG_FILE  = join(__dirname, "auto-cache.log");
const DEBUG     = process.env.AUTO_CACHE_DEBUG !== "0";

function parseEnvelope(buf) {
  try {
    return JSON.parse(buf.toString("utf8"));
  } catch {
    return null;
  }
}

function log(msg) {
  if (!DEBUG) return;
  try { appendFileSync(LOG_FILE, `[${new Date().toISOString()}] MJS-CHAIN ${msg}\n`); } catch { /* hooks must never throw */ }
}

const chunks = [];
process.stdin.on("data", chunk => chunks.push(chunk));
process.stdin.on("end", () => {
  const input = Buffer.concat(chunks);
  const envelope = parseEnvelope(input);
  const toolName = envelope?.tool_name ?? "unknown";
  const toolUseId = envelope?.tool_use_id ?? "unknown";
  log(`wrapper fired; stdin-bytes=${input.length}`);

  try {
    execFileSync("docker", [
      "exec", "-i", "ecommerceapp-context-mode",
      "sh", "-lc",
      `printf '%s PostToolUse chain tool=%s tool_use_id=%s\\n' "$(date -Iseconds)" ${JSON.stringify(toolName)} ${JSON.stringify(toolUseId)} >> /home/ctxmode/.context-mode/hooks.log`,
    ], { stdio: ["pipe", "ignore", "ignore"] });
    log("summary-marker=0");
  } catch (e) {
    log(`summary-marker=${e.status ?? "err"}: ${e.message ?? ""}`);
  }

  if (input.length === 0) {
    log("empty stdin; exit");
    process.exit(0);
  }

  // ── 1. Upstream context-mode hook (session DB capture) ──────────────────
  try {
    execFileSync("docker", [
      "exec", "-i", "-w", "/workspace", "ecommerceapp-context-mode",
      "context-mode-hook", "vscode-copilot", "posttooluse",
    ], { input, stdio: ["pipe", "ignore", "ignore"] });
    log("upstream-exit=0");
  } catch (e) {
    log(`upstream-exit=${e.status ?? "err"}: ${e.message ?? ""}`);
  }

  // ── 2. Host-side auto-cache (RAG → ctx_index) ──────────────────────────
  try {
    const autocache = join(__dirname, "auto-cache.mjs");
    execFileSync(process.execPath, [autocache], {
      input,
      stdio: ["pipe", "ignore", "pipe"],
      env: { ...process.env },
    });
    log("autocache-exit=0");
  } catch (e) {
    log(`autocache-exit=${e.status ?? "err"}`);
  }

  process.exit(0);
});
