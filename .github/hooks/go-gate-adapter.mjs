// Copilot adapter for the GO gate (core: tools/hooks/gate-core.mjs). One script serves both
// Copilot surfaces, which send different payload shapes (observed 2026-10-13, Phase 0 probe):
//   VS Code Chat : hook_event_name, session_id, tool_name, tool_input, prompt
//   Copilot CLI  : sessionId, toolName, toolArgs, prompt
// Any failure exits 0 (fail-open). A deliberate deny prints the decision as JSON in both documented
// shapes (top-level for the CLI, hookSpecificOutput for VS Code) and exits 0.
import os from 'node:os';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { evaluate, logError, readJsonStdin, repositoryRoot } from '../../tools/hooks/gate-core.mjs';

// OBSERVED (Phase 0 probe): VS Code apply_patch + create_file; CLI apply_patch (toolArgs = raw patch text).
// DOCUMENTED Copilot CLI tool names: edit, create.
// UNVERIFIED names kept for safety: replace_string_in_file, multi_replace_string_in_file,
// insert_edit_into_file, edit_notebook_file.
const editTools = new Set([
  'apply_patch',
  'create_file',
  'replace_string_in_file',
  'multi_replace_string_in_file',
  'insert_edit_into_file',
  'edit_notebook_file',
  'edit',
  'create',
]);
const extraAllowedDirectories = [path.join(os.homedir(), '.copilot')];

const patchHeader = /^\*\*\* (?:Add File|Update File|Delete File|Move to): (.+)$/gm;

function pathsFromPatch(patchText) {
  return [...String(patchText ?? '').matchAll(patchHeader)].map((match) => match[1].trim());
}

function toolArguments(payload, toolName) {
  const raw = payload.tool_input ?? payload.toolArgs ?? {};
  if (typeof raw !== 'string') return raw;
  // Copilot CLI sends apply_patch toolArgs as the raw patch text, not an object (observed).
  if (toolName === 'apply_patch') return { input: raw };
  try {
    return JSON.parse(raw);
  } catch {
    return {};
  }
}

export function extractFilePaths(toolName, args) {
  if (toolName === 'apply_patch') return pathsFromPatch(args.input ?? args.patch);
  if (Array.isArray(args.replacements)) {
    return args.replacements.map((replacement) => replacement.filePath).filter(Boolean);
  }
  const single = args.filePath ?? args.file_path ?? args.path;
  return single ? [single] : [];
}

// Copilot CLI subagents get their own sessionId with no link to the parent (observed), so all
// CLI sessions in a repo share one marker key. VS Code subagents reuse the parent session_id.
export const copilotCliScope = `copilot-cli-${createHash('sha1').update(repositoryRoot).digest('hex').slice(0, 8)}`;

export function toNormalizedInput(payload) {
  const isVsCode = 'hook_event_name' in payload;
  const sessionId = isVsCode ? payload.session_id : copilotCliScope;
  const event = payload.hook_event_name ?? null;
  if (!isVsCode && 'initialPrompt' in payload) return { event: 'sessionStart', sessionId };
  const isPrompt =
    event === 'UserPromptSubmit' || (event === null && 'prompt' in payload && !('toolName' in payload));
  if (isPrompt) return { event: 'prompt', sessionId, prompt: payload.prompt };

  const toolName = payload.tool_name ?? payload.toolName;
  if (!toolName || !editTools.has(toolName)) return null;
  return { event: 'edit', sessionId, filePaths: extractFilePaths(toolName, toolArguments(payload, toolName)) };
}

export function toDenyOutput(reason) {
  return {
    permissionDecision: 'deny',
    permissionDecisionReason: reason,
    hookSpecificOutput: {
      hookEventName: 'PreToolUse',
      permissionDecision: 'deny',
      permissionDecisionReason: reason,
    },
  };
}

async function main() {
  const payload = await readJsonStdin();
  const input = toNormalizedInput(payload);
  if (!input) return;
  const result = evaluate(input, { extraAllowedDirectories });
  if (input.event === 'edit' && result.decision === 'deny') {
    // Decision travels as JSON with exit 0: a PowerShell wrapper rewrites exit code 2 to 1, which
    // VS Code treats as a non-blocking warning, so the exit code cannot carry the deny.
    process.stdout.write(JSON.stringify(toDenyOutput(result.reason)));
  }
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
