// session-policy.mjs — SessionStart hook: inject MCP resilience policy as additionalContext.
// VS Code Copilot Chat reads hookSpecificOutput.additionalContext and prepends it to the model context.
// This runs automatically on every new session so users never need to write retry rules manually.

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

process.stdout.write(
  JSON.stringify({
    hookSpecificOutput: {
      hookEventName: "SessionStart",
      additionalContext: policy,
    },
  })
);
