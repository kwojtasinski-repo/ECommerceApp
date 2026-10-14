// Claude Code adapter for the GO gate (core: tools/hooks/gate-core.mjs).
// Maps Claude's hook payloads onto the core's normalized input and prints Claude's output format.
// Any failure here exits 0 (fail-open): a broken gate must never wedge the agents.
import os from 'node:os';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { evaluate, logError, readJsonStdin } from '../../tools/hooks/gate-core.mjs';

const editTools = new Set(['Edit', 'Write', 'NotebookEdit', 'MultiEdit']);
// Plans and per-project memory only; user-level settings/hooks stay gated.
const extraAllowedDirectories = ['plans', 'projects'].map((name) => path.join(os.homedir(), '.claude', name));

export function toNormalizedInput(payload) {
  const sessionId = payload.session_id;
  if (payload.hook_event_name === 'UserPromptSubmit') {
    return { event: 'prompt', sessionId, prompt: payload.prompt };
  }
  if (payload.hook_event_name === 'PreToolUse' && editTools.has(payload.tool_name)) {
    const toolInput = payload.tool_input ?? {};
    const filePath = toolInput.file_path ?? toolInput.notebook_path;
    return { event: 'edit', sessionId, filePaths: filePath ? [filePath] : [] };
  }
  return null;
}

export function toClaudeOutput(payload, result) {
  if (payload.hook_event_name === 'PreToolUse' && result.decision === 'deny') {
    return {
      hookSpecificOutput: {
        hookEventName: 'PreToolUse',
        permissionDecision: 'deny',
        permissionDecisionReason: result.reason,
      },
    };
  }
  if (payload.hook_event_name === 'UserPromptSubmit' && result.note) {
    return { hookSpecificOutput: { hookEventName: 'UserPromptSubmit', additionalContext: result.note } };
  }
  return null;
}

// Copilot CLI also loads .claude/settings.json hooks ("repo settings") and sets COPILOT_CLI. There
// the Copilot adapter owns the gate (its marker key differs), so this adapter must stay out.
export function isRunningInsideCopilotCli(env = process.env) {
  return Boolean(env.COPILOT_CLI);
}

async function main() {
  if (isRunningInsideCopilotCli()) return;
  const payload = await readJsonStdin();
  const input = toNormalizedInput(payload);
  if (!input) return;
  const output = toClaudeOutput(payload, evaluate(input, { extraAllowedDirectories }));
  if (output) process.stdout.write(JSON.stringify(output));
}

// Exported so a launcher (`node -e "import(...).then(m => m.run())"`) can start it without a path-dependent argv[1].
export function run() {
  return main().catch((error) => {
    logError(error);
    process.exit(0);
  });
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  run();
}
