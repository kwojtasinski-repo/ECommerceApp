// session-policy.mjs — SessionStart hook: inject MCP resilience policy + deferred tool bootstrap.
// CANONICAL SOURCE for:
//   - MCP resilience policy (timeout, retry, fail-open contract)
//   - context-mode deferred tool bootstrap for VS Code Copilot
//
// VS Code Copilot reads hookSpecificOutput.additionalContext and prepends it to the model context.
// Runs automatically on every new session — users never need to write these rules manually.
//
// To update resilience rules or bootstrap query: edit THIS file only.
// Dockerfile-context-mode is the canonical source for context-mode version (CONTEXT_MODE_TAG).

// ── 1. MCP resilience policy ─────────────────────────────────────────────────
// Rules that apply to every MCP call (both context-mode and RAG servers).
const policy = [
  "<mcp_resilience_policy>",
  "MANDATORY for every MCP call this session:",
  "- Use timeout=15000 by default (fast-fail policy).",
  "- Only for explicitly long operations (for example ingest/build/full reindex): use timeout=300000.",
  "- After Cancelled/timeout: retry up to 5x with lighter shape (scope → pattern → caps → split → reduce).",
  "- Never repeat the same command verbatim after a cancel.",
  "- After 5 failed retries: emit UNABLE_TO_PROCESS, FAILED_STEP, REASON, NEXT_STEP_CONTINUED, RUN_STATUS=PARTIAL — then ask user to accept higher-token direct-file fallback.",
  "- context-mode runtime default: javascript. Any other runtime requires ctx_doctor check first; fallback to javascript/shell if unavailable.",
  "- Always end analysis runs with full ctx stats verbatim.",
  "</mcp_resilience_policy>",
].join("\n");

// ── 2. Context-mode tool bootstrap ───────────────────────────────────────────
// In VS Code Copilot, ctx_* tools may appear under the name prefix
// "context-mode_" (e.g. context-mode_ctx_execute, context-mode_ctx_batch_execute).
// They are available — attempt calling them directly. VS Code Copilot resolves
// the schema on first call. Do NOT wait, do NOT fall back to read_file first.
const bootstrap = [
  "<context_mode_bootstrap>",
  "Context-mode tools are available this session. Call them directly — no setup step needed.",
  "Tool names in VS Code Copilot use the prefix 'context-mode_':",
  "  context-mode_ctx_execute          — run JavaScript/shell code in sandbox",
  "  context-mode_ctx_batch_execute    — run multiple shell commands + auto-index output",
  "  context-mode_ctx_execute_file     — analyze a file without reading it into context",
  "  context-mode_ctx_search           — recall anything indexed in FTS5 knowledge base",
  "  context-mode_ctx_index            — store content for later recall",
  "  context-mode_ctx_fetch_and_index  — fetch a URL, keep raw bytes out of context",
  "  context-mode_ctx_stats            — show context savings this session",
  "  context-mode_ctx_doctor           — run diagnostics",
  "If a ctx_* call returns 'tool not found': look in available tools for the 'context-mode_' prefix,",
  "  then retry with the full name. Never fall back to read_file/grep_search just because the first call failed.",
  "Correct session flow:",
  "  1. context-mode_ctx_batch_execute — gather + index multiple commands in one call",
  "  2. context-mode_ctx_execute / ctx_execute_file — derive answers; bytes stay out of context",
  "  3. context-mode_ctx_search — recall indexed results",
  "  4. Classic tools (read_file / replace_string_in_file) ONLY for final file edits",
  "Never use read_file to explore or summarize — use context-mode_ctx_execute_file instead.",
  "</context_mode_bootstrap>",
].join("\n");

// ── 3. ctx_batch_execute output anti-pattern ─────────────────────────────────
// When ctx_batch_execute returns "Large tool result written to file: C:/Users/...",
// that path is on the HOST — the context-mode container cannot access it.
// Reading it via ctx_execute(fs.readFileSync("C:/...")) causes ENOENT → crash → Canceled.
// ctx_batch_execute auto-indexes all output into FTS5 — use ctx_search instead.
const batchAntiPattern = [
  "<ctx_batch_execute_output_rule>",
  "MANDATORY — when ctx_batch_execute returns 'Large tool result written to file: C:/...':",
  "  DO NOT: ctx_execute with fs.readFileSync('C:/Users/...') — that path is on the host, not in the container.",
  "  DO: ctx_search(queries: ['your question']) — output is already indexed in FTS5, query it directly.",
  "  DO: ctx_execute with /workspace/* paths only — container mount is /workspace.",
  "  Reading the AppData temp file from inside ctx_execute = guaranteed ENOENT → Canceled.",
  "</ctx_batch_execute_output_rule>",
].join("\n");

// ── 4. Tool invocation map ──────────────────────────────────────────────────
// Give weak models concrete call patterns instead of vague routing advice.
const invocationMap = [
  "<tool_invocation_map>",
  "WHEN YOU NEED DOCS OR DECISIONS:",
  "  - Use RAG first: query_docs(question) or get_history(id).",
  "WHEN YOU NEED TO ANALYZE LOCAL FILES OR OUTPUT:",
  "  - Use ctx_execute_file(path, language, code) for one file.",
  "  - Use ctx_batch_execute(commands, queries) for 3+ related shell/file commands.",
  "  - Use ctx_execute(language, code) for one small transform/parse/count/compare script.",
  "  - Use ctx_search(queries) to recall anything already indexed in FTS5.",
  "WHEN YOU NEED REUSABLE RESULTS:",
  "  - Use ctx_index(content, source) after you want to keep a result for later recall.",
  "WHEN YOU NEED PROJECT WEB CONTENT:",
  "  - Use ctx_fetch_and_index(url, source) first, then ctx_search(queries).",
  "WHEN YOU SEE 'written to file: C:/Users/...':",
  "  - Treat it as host-side output. Do not read that file from ctx_execute.",
  "  - Query the indexed result with ctx_search instead.",
  "</tool_invocation_map>",
].join("\n");

process.stdout.write(
  JSON.stringify({
    hookSpecificOutput: {
      hookEventName: "SessionStart",
      additionalContext: policy + "\n\n" + bootstrap + "\n\n" + batchAntiPattern + "\n\n" + invocationMap,
    },
  })
);
