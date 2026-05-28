#!/usr/bin/env node
// auto-cache.mjs — PostToolUse host-side extension (variant A).
// Wired in via .github/hooks/context-mode.json → posttooluse-chain.mjs (cross-platform Node).
//
// Responsibilities:
//   1. Detect RAG MCP tool calls (any tool exposed by an ecommerceapp-rag-* server).
//   2. Format the response into markdown the FTS5 chunker can index.
//   3. Persist into the context-mode FTS5 store via MCP stdio ctx_index.
//
// Tool list is discovered at runtime from .vscode/mcp.json (stdio variants) via
// MCP tools/list, cached in .rag-tools-cache.json with a 1h TTL. A hardcoded
// fallback list ships as a safety net for pure-HTTP setups where stdio
// introspection finds nothing.
//
// Two entry modes:
//   - hook mode (default): triggered by Copilot PostToolUse envelope on stdin.
//   - introspect mode (AUTO_CACHE_INTROSPECT=1): kicked off in the background
//     by a hook-mode run that observed a missing/stale tool cache. Writes the
//     cache and exits. Detached, so it never blocks the parent hook chain.
//
// Logging:
//   Default ON, writes to .github/hooks/auto-cache.log.
//   Set AUTO_CACHE_DEBUG=0 in the environment to disable.

import { createHash } from "node:crypto";
import { spawn } from "node:child_process";
import { appendFileSync, readFileSync, writeFileSync, statSync, unlinkSync } from "node:fs";
import { join, dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

// ─── Stable constants ─────────────────────────────────────────────────────
const RAG_NAME_PREFIX        = "ecommerceapp-rag";
const CACHE_TARGET_CONTAINER = "ecommerceapp-context-mode";
const CACHE_TARGET_TOOL      = "ctx_index";
const MCP_PROTOCOL           = "2025-11-25";
const CACHE_TTL_MS           = 60 * 60 * 1000;   // 1 hour
const SPAWN_TIMEOUT_MS       = 5000;             // ctx_index roundtrip cap
const INTROSPECT_TIMEOUT_MS  = 10000;            // per-server tools/list cap
const MIN_CONTENT_BYTES      = 100;              // skip near-empty payloads

// Fallback when discovery finds zero stdio RAG servers (pure HTTP setup,
// .vscode/mcp.json missing/unparseable, or all stdio servers failed to start).
// Keep in sync with currently-known RAG tools; the discovery layer normally
// supersedes this list.
const FALLBACK_TOOLS = new Set([
  "query_docs", "read_docs", "get_history", "get_adr_history", "query_docs_cached",
]);

// ─── Paths and logging ────────────────────────────────────────────────────
const __dirname        = dirname(fileURLToPath(import.meta.url));
const LOG_FILE         = join(__dirname, "auto-cache.log");
const TOOLS_CACHE_FILE = join(__dirname, ".rag-tools-cache.json");
const WORKSPACE_ROOT   = resolve(__dirname, "..", "..");
const VSCODE_MCP_JSON  = join(WORKSPACE_ROOT, ".vscode", "mcp.json");

const DEBUG = process.env.AUTO_CACHE_DEBUG !== "0";
const log = (msg) => {
  if (!DEBUG) return;
  try { appendFileSync(LOG_FILE, `[${new Date().toISOString()}] NODE ${msg}\n`); } catch { /* hooks must never throw */ }
};

// ─── Tiny helpers ─────────────────────────────────────────────────────────
const hash8 = (s) => createHash("sha256").update(s).digest("hex").slice(0, 8);
const today = () => new Date().toISOString().slice(0, 10);
const slug  = (s) => String(s).toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");

function safeJson(s) {
  if (!s) return null;
  try { return JSON.parse(s); } catch { return null; }
}

// Permissive JSONC reader — strips // line comments, /* */ block comments,
// and trailing commas so .vscode/mcp.json (JSONC) parses with JSON.parse.
// String-aware: does not eat // inside "http://..." or comment chars inside strings.
function stripJsonc(src) {
  let out = "";
  let i = 0;
  const n = src.length;
  let inString = false;
  let stringQuote = '"';
  while (i < n) {
    const ch = src[i];
    const next = src[i + 1];
    if (inString) {
      out += ch;
      if (ch === "\\" && i + 1 < n) { out += src[i + 1]; i += 2; continue; }
      if (ch === stringQuote) inString = false;
      i++; continue;
    }
    if (ch === '"' || ch === "'") { inString = true; stringQuote = ch; out += ch; i++; continue; }
    if (ch === "/" && next === "/") { while (i < n && src[i] !== "\n") i++; continue; }
    if (ch === "/" && next === "*") { i += 2; while (i < n && !(src[i] === "*" && src[i + 1] === "/")) i++; i += 2; continue; }
    out += ch; i++;
  }
  return out.replace(/,(\s*[\]}])/g, "$1");
}

function readJsonc(path) {
  try {
    const raw = readFileSync(path, "utf8");
    return JSON.parse(stripJsonc(raw));
  } catch (e) {
    log(`readJsonc(${path}) failed: ${e?.message || e}`);
    return null;
  }
}

// ─── Tool-list discovery ──────────────────────────────────────────────────
function readToolsCache() {
  try {
    const stat = statSync(TOOLS_CACHE_FILE);
    const age = Date.now() - stat.mtimeMs;
    if (age > CACHE_TTL_MS) { log(`tools-cache stale (age=${Math.round(age / 1000)}s)`); return null; }
    const data = safeJson(readFileSync(TOOLS_CACHE_FILE, "utf8"));
    if (!data || !Array.isArray(data.tools)) return null;
    log(`tools-cache hit (age=${Math.round(age / 1000)}s, ${data.tools.length} tools, source=${data.source})`);
    return new Set(data.tools);
  } catch { return null; }
}

function writeToolsCache(tools, source) {
  try {
    writeFileSync(TOOLS_CACHE_FILE, JSON.stringify({
      generated_at: new Date().toISOString(),
      source, // "introspected" | "fallback"
      tools:  [...tools].sort(),
    }, null, 2));
    log(`tools-cache written (${tools.size} tools, source=${source})`);
  } catch (e) {
    log(`tools-cache write failed: ${e?.message || e}`);
  }
}

// Detach a child process that re-runs THIS script with AUTO_CACHE_INTROSPECT=1.
// The child does the discovery work and writes the cache without blocking the
// hook chain that triggered it. Uses a lock file (60s TTL) to coalesce
// concurrent kickoffs from rapid-fire hook invocations.
const LOCK_FILE   = join(__dirname, ".rag-tools-cache.lock");
const LOCK_TTL_MS = 60_000;

function kickoffBackgroundDiscovery() {
  try {
    try {
      const stat = statSync(LOCK_FILE);
      if (Date.now() - stat.mtimeMs < LOCK_TTL_MS) {
        log("background discovery already in flight (lock fresh) — skipping");
        return;
      }
    } catch { /* lock file missing — proceed */ }
    try { writeFileSync(LOCK_FILE, String(Date.now())); } catch { /* best effort */ }
    const child = spawn(process.execPath, [fileURLToPath(import.meta.url)], {
      detached: true,
      stdio:    "ignore",
      env:      { ...process.env, AUTO_CACHE_INTROSPECT: "1" },
    });
    child.unref();
    log("background discovery kicked off");
  } catch (e) {
    log(`background discovery spawn failed: ${e?.message || e}`);
  }
}

// Parse .vscode/mcp.json, return resolved stdio RAG server configs.
function listStdioRagServers() {
  const cfg = readJsonc(VSCODE_MCP_JSON);
  if (!cfg?.servers) return [];
  const out = [];
  for (const [name, server] of Object.entries(cfg.servers)) {
    if (!name.startsWith(RAG_NAME_PREFIX))               continue;
    if (server?.type !== "stdio")                        continue;
    if (!server?.command || !Array.isArray(server?.args)) continue;
    const subst = (v) => String(v).replace(/\$\{workspaceFolder\}/g, WORKSPACE_ROOT);
    const env = {};
    for (const [k, v] of Object.entries(server.env || {})) env[k] = subst(v);
    out.push({ name, command: subst(server.command), args: server.args.map(subst), env });
  }
  return out;
}

// Spawn an MCP stdio server, perform initialize + tools/list, return tool names.
function listToolsViaStdio(server) {
  return new Promise((doneResolve) => {
    const tools = [];
    let buf = "";
    let done = false;
    const finish = () => {
      if (done) return;
      done = true;
      try { child.kill(); } catch { /* noop */ }
      doneResolve(tools);
    };
    const child = spawn(server.command, server.args, {
      stdio: ["pipe", "pipe", "ignore"],
      env:   { ...process.env, ...server.env },
    });
    const timer = setTimeout(finish, INTROSPECT_TIMEOUT_MS);
    child.on("error", finish);
    child.on("exit",  () => { clearTimeout(timer); finish(); });
    child.stdout.on("data", (d) => {
      buf += d.toString();
      let nl;
      while ((nl = buf.indexOf("\n")) !== -1) {
        const line = buf.slice(0, nl).trim();
        buf = buf.slice(nl + 1);
        if (!line) continue;
        const msg = safeJson(line);
        if (msg?.id === 2 && msg.result?.tools) {
          for (const t of msg.result.tools) if (t?.name) tools.push(t.name);
          finish();
          return;
        }
      }
    });
    const send = (o) => { try { child.stdin.write(JSON.stringify(o) + "\n"); } catch { /* noop */ } };
    send({ jsonrpc: "2.0", id: 1, method: "initialize",
           params: { protocolVersion: MCP_PROTOCOL, capabilities: {}, clientInfo: { name: "auto-cache-introspect", version: "1" } } });
    send({ jsonrpc: "2.0", method: "notifications/initialized" });
    send({ jsonrpc: "2.0", id: 2, method: "tools/list" });
  });
}

async function runIntrospection() {
  try {
    const servers = listStdioRagServers();
    log(`introspection: found ${servers.length} stdio ${RAG_NAME_PREFIX}* server(s): ${servers.map(s => s.name).join(",") || "<none>"}`);
    if (!servers.length) {
      log(`WARNING: no stdio ${RAG_NAME_PREFIX}* servers in ${VSCODE_MCP_JSON}. Pure-HTTP setups are not auto-discovered (HTTP introspection is a TODO). Falling back to hardcoded tool list — new RAG tools added upstream will NOT be auto-cached until a stdio variant is re-enabled in .vscode/mcp.json or this fallback list is updated.`);
      writeToolsCache(FALLBACK_TOOLS, "fallback");
      return;
    }
    const merged = new Set();
    for (const server of servers) {
      const tools = await listToolsViaStdio(server);
      log(`  ${server.name} → ${tools.length} tools`);
      tools.forEach((t) => merged.add(t));
    }
    if (!merged.size) {
      log(`WARNING: stdio introspection returned 0 tools across ${servers.length} server(s). Check that the RAG servers start without error (try running the command from .vscode/mcp.json manually). Falling back to hardcoded tool list.`);
      writeToolsCache(FALLBACK_TOOLS, "fallback");
      return;
    }
    writeToolsCache(merged, "introspected");
  } finally {
    try { unlinkSync(LOCK_FILE); } catch { /* best effort */ }
  }
}

// Non-blocking: returns the best tool list we have for THIS hook fire. If the
// cache is missing/stale, kicks off background discovery and returns fallback.
function getRagTools() {
  const cached = readToolsCache();
  if (cached) return cached;
  log("tools-cache missing or stale — using fallback for this call + kicking off background discovery");
  kickoffBackgroundDiscovery();
  return FALLBACK_TOOLS;
}

// ─── Source-label derivation ──────────────────────────────────────────────
function sourceLabelFor(bareTool, input, response) {
  // Phase 7 L2 path: query_docs_cached returns a deterministic label already.
  if (typeof response?.source === "string" && response.source.startsWith("rag-cache-")) {
    return response.source;
  }
  if (bareTool === "get_history" || bareTool === "get_adr_history") {
    const id = String(input.id ?? "unknown").padStart(4, "0");
    return `rag-auto-adr${id}`;
  }
  const q = String(input.question || input.query || "").toLowerCase().trim();
  if (!q) return null;
  const adr = q.match(/(?:adr[\s-]*)?(\d{4})/);
  if (adr)        return `rag-auto-adr${adr[1]}-${hash8(q)}`;
  if (input.bc)   return `rag-auto-${slug(input.bc)}-${hash8(q)}`;
  const prefix = bareTool === "query_docs" ? "rag-auto-orient" : "rag-auto-q";
  return `${prefix}-${hash8(q)}`;
}

// ─── Markdown formatting (shape-driven, tool-name agnostic) ───────────────
function markdownFor(bareTool, input, response) {
  // Pre-formatted (L2 path) — passthrough.
  if (typeof response?.markdown === "string") return response.markdown;

  // get_history shape: top-level chunks[].
  if (bareTool === "get_history" || bareTool === "get_adr_history") {
    const chunks = response?.chunks || [];
    if (!chunks.length) return null;
    return [
      `# ${bareTool}(id=${input.id ?? "?"})`,
      `> Auto-cached from RAG on ${today()}.`,
      "",
      ...chunks.map((c, i) => [
        `## chunk ${i + 1}: ${c.breadcrumb || c.rel_path || "fragment"}`,
        `**Path**: \`${c.rel_path || "unknown"}\``,
        "",
        c.text || c.content || "",
      ].join("\n")),
    ].join("\n\n");
  }

  // query_docs / read_docs / unknown query-shaped tool: prefer files, then
  // results, then hits — each entry may carry direct text OR nested chunks[].
  const items = response?.files || response?.results || response?.hits || [];
  if (!items.length) return null;

  const renderItem = (it) => {
    const header     = `## ${it.rel_path || it.path || "unknown"}`;
    const score      = `**Score**: ${it.score ?? "n/a"}`;
    const breadcrumb = it.breadcrumb ? `**Breadcrumb**: ${it.breadcrumb}\n` : "";
    const direct     = it.content || it.text || it.snippet || "";
    const nested = Array.isArray(it.chunks)
      ? it.chunks.map((c, i) => [
          `### chunk ${i + 1}${c.lines ? ` (lines ${c.lines})` : ""}${c.score != null ? ` — score ${c.score}` : ""}`,
          c.breadcrumb ? `**Breadcrumb**: ${c.breadcrumb}\n` : "",
          c.text || c.content || c.snippet || "",
        ].filter(Boolean).join("\n")).join("\n\n")
      : "";
    return [header, score, breadcrumb, direct, nested].filter(Boolean).join("\n");
  };

  return [
    `# ${bareTool}(${input.question || input.query || ""})`,
    `> Auto-cached from RAG on ${today()}.`,
    "",
    ...items.map(renderItem),
  ].join("\n\n");
}

// ─── MCP stdio call: write into context-mode FTS5 ─────────────────────────
function callCtxIndex(source, content) {
  return new Promise((doneResolve) => {
    const child = spawn("docker",
      ["exec", "-i", CACHE_TARGET_CONTAINER, "node", "/app/cli.bundle.mjs"],
      { stdio: ["pipe", "pipe", "ignore"] });
    let buf = "";
    let done = false;
    const finish = () => {
      if (done) return;
      done = true;
      try { child.kill(); } catch { /* noop */ }
      doneResolve();
    };
    const timer = setTimeout(finish, SPAWN_TIMEOUT_MS);
    child.on("error", finish);
    child.on("exit",  () => { clearTimeout(timer); finish(); });
    child.stdout.on("data", (d) => {
      buf += d.toString();
      if (buf.includes('"id":3') || buf.includes('"id": 3')) finish();
    });
    const send = (o) => { try { child.stdin.write(JSON.stringify(o) + "\n"); } catch { /* noop */ } };
    send({ jsonrpc: "2.0", id: 1, method: "initialize",
           params: { protocolVersion: MCP_PROTOCOL, capabilities: {}, clientInfo: { name: "auto-cache-writer", version: "1" } } });
    send({ jsonrpc: "2.0", method: "notifications/initialized" });
    send({ jsonrpc: "2.0", id: 3, method: "tools/call",
           params: { name: CACHE_TARGET_TOOL, arguments: { source, content } } });
  });
}

// ─── stdin / entry points ─────────────────────────────────────────────────
async function readStdin() {
  const chunks = [];
  for await (const c of process.stdin) chunks.push(c);
  return Buffer.concat(chunks).toString("utf8");
}

async function mainHook() {
  const raw = await readStdin();
  log(`hook fired; stdin-bytes=${raw.length}`);
  if (!raw.trim()) { log("empty stdin"); return; }

  const envelope = safeJson(raw);
  if (!envelope) { log(`envelope-parse-fail; first200=${raw.slice(0, 200).replace(/\n/g, "\\n")}`); return; }

  const toolName = envelope.tool_name || "";
  const bareTool = toolName.replace(/^mcp_[^_]+_/, "");
  log(`tool=${toolName} bare=${bareTool}`);

  const ragTools = getRagTools();
  if (!ragTools.has(bareTool)) { log(`skip non-RAG: ${bareTool}`); return; }

  const input = envelope.tool_input || {};
  const response = typeof envelope.tool_response === "string"
    ? safeJson(envelope.tool_response)
    : envelope.tool_response || envelope.tool_output;
  log(`response-type=${typeof response} response-shape=${response ? Object.keys(response).slice(0, 8).join(",") : "null"}`);
  if (!response || typeof response !== "object") { log("no usable response"); return; }

  const source = sourceLabelFor(bareTool, input, response);
  if (!source) { log("no source label derived"); return; }
  const content = markdownFor(bareTool, input, response);
  log(`source=${source} content-bytes=${content?.length ?? 0}`);
  if (!content || content.length < MIN_CONTENT_BYTES) { log("content too short"); return; }

  await callCtxIndex(source, content);
  log(`ctx_index call returned for source=${source}`);
}

const entry = process.env.AUTO_CACHE_INTROSPECT === "1" ? runIntrospection : mainHook;
entry().catch((e) => { log(`uncaught: ${e?.message || e}`); });
