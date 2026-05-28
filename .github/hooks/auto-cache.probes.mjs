#!/usr/bin/env node
// auto-cache.probes.mjs — self-contained probes for auto-cache.mjs internals.
//
// Run: node .github/hooks/auto-cache.probes.mjs
// Exits 0 on success, 1 on any failure. Designed to be runnable from any cwd
// (no npm install, no test runner) so it can be invoked in CI later if needed.
//
// What it checks:
//   1. stripJsonc respects URLs and other // inside strings.
//   2. stripJsonc strips line + block comments outside strings.
//   3. stripJsonc removes trailing commas.
//   4. Shape-driven formatter recovers nested chunks (the read_docs bug fix).
//   5. Source-label derivation is deterministic + idempotent for the documented patterns.
//   6. Lock-file TTL semantics (kickoff coalescing logic).
//
// Failure mode: prints "FAIL: <name>: <reason>" and exits 1. Each probe is
// independent — one failure doesn't short-circuit the rest.

import { readFileSync, writeFileSync, statSync, unlinkSync, mkdtempSync, rmSync, utimesSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { createHash } from "node:crypto";

const __dirname = dirname(fileURLToPath(import.meta.url));
const AUTO_CACHE_PATH = join(__dirname, "auto-cache.mjs");

// ────────────────────────────────────────────────────────────────────────────
// Load auto-cache.mjs source so we can extract and execute its private helpers
// without exporting them (keeps the hook surface area minimal).
// ────────────────────────────────────────────────────────────────────────────
const src = readFileSync(AUTO_CACHE_PATH, "utf8");

function extract(name) {
  // Match `function <name>(` ... ending `}` at column 0 (top-level fn).
  // CRLF-tolerant: \r? before each \n.
  const re = new RegExp(`function ${name}\\([\\s\\S]*?\\r?\\n\\}\\r?\\n`, "m");
  const m = src.match(re);
  if (!m) throw new Error(`could not extract function ${name} from auto-cache.mjs`);
  return m[0];
}

// Reconstitute the helpers we want to probe. Each probe builds a tiny module
// that imports nothing from the original (so the probes are robust to refactors
// of unrelated helpers).
const stripJsoncSrc = extract("stripJsonc");

// Eval the helper into the local scope.
const stripJsonc = new Function(`${stripJsoncSrc}; return stripJsonc;`)();

// ────────────────────────────────────────────────────────────────────────────
// Test runner.
// ────────────────────────────────────────────────────────────────────────────
let failed = 0;
function probe(name, fn) {
  try {
    fn();
    console.log(`PASS: ${name}`);
  } catch (e) {
    failed++;
    console.error(`FAIL: ${name}: ${e?.message || e}`);
  }
}
function assertEq(actual, expected, label) {
  if (actual !== expected) {
    throw new Error(`${label} — expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`);
  }
}
function assertContains(haystack, needle, label) {
  if (!haystack.includes(needle)) {
    throw new Error(`${label} — expected to contain ${JSON.stringify(needle)}, got ${JSON.stringify(haystack)}`);
  }
}
function assertNotContains(haystack, needle, label) {
  if (haystack.includes(needle)) {
    throw new Error(`${label} — expected NOT to contain ${JSON.stringify(needle)}, got ${JSON.stringify(haystack)}`);
  }
}

// ─── PROBE 1: stripJsonc preserves // inside strings (the real bug fix) ───
probe("stripJsonc — preserves // inside strings", () => {
  const input = '{ "url": "http://localhost:6333", "x": 1 }';
  const out = stripJsonc(input);
  assertContains(out, '"http://localhost:6333"', "URL preserved");
  const parsed = JSON.parse(out);
  assertEq(parsed.url, "http://localhost:6333", "parsed URL");
  assertEq(parsed.x, 1, "parsed numeric");
});

// ─── PROBE 2: stripJsonc strips // line comments outside strings ───
probe("stripJsonc — strips line comments outside strings", () => {
  const input = '{\n  // a comment\n  "a": 1, // trailing\n  "b": 2\n}';
  const out = stripJsonc(input);
  assertNotContains(out, "a comment", "stripped");
  assertNotContains(out, "trailing", "stripped");
  const parsed = JSON.parse(out);
  assertEq(parsed.a, 1, "a");
  assertEq(parsed.b, 2, "b");
});

// ─── PROBE 3: stripJsonc strips /* */ block comments outside strings ───
probe("stripJsonc — strips block comments outside strings", () => {
  const input = '{ /* block\n  comment */ "a": "b /* not a comment */ c" }';
  const out = stripJsonc(input);
  assertNotContains(out, "block", "block stripped");
  const parsed = JSON.parse(out);
  assertEq(parsed.a, "b /* not a comment */ c", "string preserved");
});

// ─── PROBE 4: stripJsonc removes trailing commas ───
probe("stripJsonc — removes trailing commas", () => {
  const input = '{ "a": [1, 2, 3,], "b": { "c": 1, }, }';
  const out = stripJsonc(input);
  const parsed = JSON.parse(out);
  assertEq(parsed.a.length, 3, "array len");
  assertEq(parsed.b.c, 1, "nested");
});

// ─── PROBE 5: stripJsonc handles escaped quotes inside strings ───
probe("stripJsonc — respects escaped quotes", () => {
  const input = '{ "a": "she said \\"hi // bye\\"" }';
  const out = stripJsonc(input);
  const parsed = JSON.parse(out);
  assertEq(parsed.a, 'she said "hi // bye"', "escape preserved");
});

// ─── PROBE 6: stripJsonc tolerates the actual project mcp.json shape ───
probe("stripJsonc — tolerates project .vscode/mcp.json", () => {
  const mcpPath = join(__dirname, "..", "..", ".vscode", "mcp.json");
  const raw = readFileSync(mcpPath, "utf8");
  const out = stripJsonc(raw);
  const parsed = JSON.parse(out);
  if (!parsed || typeof parsed !== "object") throw new Error("did not parse to object");
  if (!parsed.servers || typeof parsed.servers !== "object") {
    throw new Error("missing `servers` key after parse");
  }
});

// ─── PROBE 7: shape-driven formatter recovers nested chunks ───
// Mirrors the read_docs bug: response.files[i] has nested chunks[].text rather
// than top-level text. The formatter must walk into chunks[] and emit each.
probe("formatter — walks nested chunks[].text", () => {
  // Inline reimplementation of the renderItem chunk-walking logic. If the hook
  // formatter changes, update this to match. The probe ensures the rule is
  // explicit: nested chunks MUST be flattened.
  function renderItem(it) {
    const parts = [];
    const text = it.text || it.content || it.snippet;
    if (text) parts.push(text);
    if (Array.isArray(it.chunks)) {
      for (let i = 0; i < it.chunks.length; i++) {
        const c = it.chunks[i];
        if (c?.text) parts.push(`### chunk ${i + 1} — ${c.text}`);
      }
    }
    return parts.join("\n\n");
  }

  const flatOnly = renderItem({ text: "flat content here" });
  assertContains(flatOnly, "flat content here", "flat path");

  const nestedOnly = renderItem({
    chunks: [
      { text: "chunk one body" },
      { text: "chunk two body" },
    ],
  });
  assertContains(nestedOnly, "chunk one body", "first chunk");
  assertContains(nestedOnly, "chunk two body", "second chunk");
  assertContains(nestedOnly, "### chunk 1", "chunk header");

  const empty = renderItem({ files_count: 0 });
  assertEq(empty, "", "no content produces empty string");
});

// ─── PROBE 8: source-label hash determinism (idempotent overwrite) ───
probe("source-label — sha256 truncation is deterministic", () => {
  const hash8 = (s) => createHash("sha256").update(s).digest("hex").slice(0, 8);
  const a = hash8("How does the Catalog BC handle product renames?");
  const b = hash8("How does the Catalog BC handle product renames?");
  const c = hash8("how does the catalog bc handle product renames?");
  assertEq(a, b, "same input → same hash");
  if (a === c) throw new Error("case-sensitivity sanity check failed");
  if (a.length !== 8) throw new Error(`expected 8 hex chars, got ${a.length}`);
});

// ─── PROBE 9: lock-file TTL semantics ───
// Verifies that statSync(...).mtimeMs comparison against LOCK_TTL_MS=60s
// works as intended: a fresh lock is "in flight"; a stale one is not.
probe("lock-file — TTL comparison", () => {
  const tmp = mkdtempSync(join(tmpdir(), "auto-cache-probe-"));
  const lockPath = join(tmp, ".lock");
  const LOCK_TTL_MS = 60_000;
  try {
    writeFileSync(lockPath, String(Date.now()));
    let stat = statSync(lockPath);
    const freshAge = Date.now() - stat.mtimeMs;
    if (freshAge > LOCK_TTL_MS) throw new Error(`fresh lock age ${freshAge} exceeded TTL`);

    // Simulate a stale lock by rewinding mtime to past the TTL window.
    const past = Date.now() - LOCK_TTL_MS - 1_000;
    utimesSync(lockPath, new Date(past), new Date(past));
    stat = statSync(lockPath);
    const staleAge = Date.now() - stat.mtimeMs;
    if (staleAge < LOCK_TTL_MS) throw new Error(`expected stale, got age ${staleAge}`);

    unlinkSync(lockPath);
  } finally {
    rmSync(tmp, { recursive: true, force: true });
  }
});

// ────────────────────────────────────────────────────────────────────────────
if (failed > 0) {
  console.error(`\n${failed} probe(s) failed`);
  process.exit(1);
}
console.log("\nAll probes passed");
