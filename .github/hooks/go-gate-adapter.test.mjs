import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { copilotCliScope, extractFilePaths, toNormalizedInput } from './go-gate-adapter.mjs';

const adapterPath = fileURLToPath(new URL('./go-gate-adapter.mjs', import.meta.url));
const run = (payload) =>
  spawnSync('node', [adapterPath], {
    input: JSON.stringify(payload),
    encoding: 'utf8',
    env: { ...process.env, CI: '', GITHUB_ACTIONS: '' },
  });

// Shapes copied from real Phase 0 probe logs (2026-10-13), values anonymized.
const vscodePrompt = {
  timestamp: 't',
  hook_event_name: 'UserPromptSubmit',
  session_id: 'vs-1',
  transcript_path: 'x',
  prompt: 'IMPLEMENT',
  cwd: 'c:\\Projekty\\ECommerceApp',
};
const vscodeApplyPatch = {
  hook_event_name: 'PreToolUse',
  session_id: 'vs-1',
  tool_name: 'apply_patch',
  tool_input: {
    input: '*** Begin Patch\n*** Add File: c:\\Projekty\\ECommerceApp\\probe-edit.txt\n+hello\n*** End Patch',
    explanation: 'e',
  },
  tool_use_id: 'u',
  cwd: 'c',
};
const vscodeCreateFile = {
  hook_event_name: 'PreToolUse',
  session_id: 'vs-1',
  tool_name: 'create_file',
  tool_input: { filePath: 'c:\\Projekty\\ECommerceApp\\probe-subagent.txt', content: 'sub\n' },
};
const vscodeRead = {
  hook_event_name: 'PreToolUse',
  session_id: 'vs-1',
  tool_name: 'read_file',
  tool_input: { filePath: 'c:\\x', startLine: 1, endLine: 2 },
};
const cliPrompt = { sessionId: 'cli-1', timestamp: 1, cwd: 'C:\\Projekty\\ECommerceApp', prompt: 'IMPLEMENT' };
const cliView = { sessionId: 'cli-1', timestamp: 1, cwd: 'c', toolName: 'view', toolArgs: { path: 'C:\\x' } };

describe('copilot adapter mapping', () => {
  it('maps a VS Code prompt', () => {
    assert.deepEqual(toNormalizedInput(vscodePrompt), { event: 'prompt', sessionId: 'vs-1', prompt: 'IMPLEMENT' });
  });
  it('maps a CLI prompt (camelCase, no event name)', () => {
    assert.deepEqual(toNormalizedInput(cliPrompt), { event: 'prompt', sessionId: copilotCliScope, prompt: 'IMPLEMENT' });
  });
  it('extracts the path from an apply_patch Add File header', () => {
    assert.deepEqual(toNormalizedInput(vscodeApplyPatch).filePaths, ['c:\\Projekty\\ECommerceApp\\probe-edit.txt']);
  });
  it('extracts every file of a multi-file patch including moves', () => {
    const patch =
      '*** Begin Patch\n*** Update File: a.txt\n@@\n-x\n+y\n*** Move to: b.txt\n*** Delete File: c.txt\n*** End Patch';
    assert.deepEqual(extractFilePaths('apply_patch', { input: patch }), ['a.txt', 'b.txt', 'c.txt']);
  });
  it('extracts filePath from create_file', () => {
    assert.deepEqual(toNormalizedInput(vscodeCreateFile).filePaths, ['c:\\Projekty\\ECommerceApp\\probe-subagent.txt']);
  });
  it('extracts every filePath of a multi-replace call', () => {
    const args = { replacements: [{ filePath: 'a' }, { filePath: 'b' }] };
    assert.deepEqual(extractFilePaths('multi_replace_string_in_file', args), ['a', 'b']);
  });
  it('reads the CLI apply_patch toolArgs, which is the raw patch text (real payload)', () => {
    const payload = {
      sessionId: 'child-1',
      toolName: 'apply_patch',
      toolArgs: '*** Begin Patch\n*** Add File: probe-edit.txt\n+hello\n*** End Patch\n',
    };
    assert.deepEqual(toNormalizedInput(payload), { event: 'edit', sessionId: copilotCliScope, filePaths: ['probe-edit.txt'] });
  });
  it('shares one marker key across CLI sessions, including subagent sessions', () => {
    const parent = toNormalizedInput({ sessionId: 'parent', toolName: 'apply_patch', toolArgs: '*** Add File: a' });
    const child = toNormalizedInput({ sessionId: 'child', toolName: 'apply_patch', toolArgs: '*** Add File: a' });
    assert.equal(parent.sessionId, child.sessionId);
  });
  it('maps a CLI sessionStart (identified by initialPrompt), but ignores a VS Code SessionStart', () => {
    assert.deepEqual(toNormalizedInput({ sessionId: 's', source: 'new', initialPrompt: 'x', cwd: 'c' }), { event: 'sessionStart', sessionId: copilotCliScope });
    assert.equal(toNormalizedInput({ hook_event_name: 'SessionStart', session_id: 's', source: 'new', model: 'm' }), null);
  });
  it('accepts CLI toolArgs given as a JSON string', () => {
    const payload = { sessionId: 's', toolName: 'edit', toolArgs: '{"path":"x.java"}' };
    assert.deepEqual(toNormalizedInput(payload).filePaths, ['x.java']);
  });
  it('ignores read-only tools in both surfaces', () => {
    assert.equal(toNormalizedInput(vscodeRead), null);
    assert.equal(toNormalizedInput(cliView), null);
  });
  it('yields no paths for an apply_patch without headers, so the core denies it', () => {
    assert.deepEqual(extractFilePaths('apply_patch', { input: 'garbage' }), []);
  });
});

describe('copilot adapter process', () => {
  it('denies a source edit before GO: exit 0, both JSON shapes on stdout', () => {
    const result = run({
      ...vscodeCreateFile,
      session_id: `copilot-adapter-${Date.now()}`,
      tool_input: { filePath: 'ECommerceApp.Domain/Foo.cs' },
    });
    assert.equal(result.status, 0);
    const output = JSON.parse(result.stdout);
    assert.match(output.permissionDecisionReason, /Edits are locked until the user writes IMPLEMENT/);
    assert.equal(output.permissionDecision, 'deny');
    assert.equal(output.hookSpecificOutput.permissionDecision, 'deny');
  });
  it('allows an allow-listed path before GO', () => {
    const result = run({
      ...vscodeCreateFile,
      session_id: `copilot-adapter-${Date.now()}`,
      tool_input: { filePath: '.github/plans/x/01.md' },
    });
    assert.equal(result.status, 0);
  });
  it('fails open, logs the error, and stays silent on malformed stdin', () => {
    const tempDirectory = fs.mkdtempSync(path.join(os.tmpdir(), 'gate-copilot-error-'));
    const result = spawnSync('node', [adapterPath], {
      input: '{{{',
      encoding: 'utf8',
      env: { ...process.env, CI: '', GITHUB_ACTIONS: '', TMP: tempDirectory, TEMP: tempDirectory, TMPDIR: tempDirectory },
    });
    assert.equal(result.status, 0);
    assert.equal(result.stdout, '');
    assert.equal(fs.existsSync(path.join(tempDirectory, 'ecommerceapp-go-gate', 'errors.log')), true);
  });
});
