import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { isRunningInsideCopilotCli, toClaudeOutput, toNormalizedInput } from './go-gate-adapter.mjs';

const adapterPath = fileURLToPath(new URL('./go-gate-adapter.mjs', import.meta.url));
const run = (payload, env = {}) =>
  spawnSync('node', [adapterPath], { input: JSON.stringify(payload), encoding: 'utf8', env: { ...process.env, CI: '', GITHUB_ACTIONS: '', ...env } });

describe('claude adapter mapping', () => {
  it('maps an Edit call to a normalized edit event', () => {
    const input = toNormalizedInput({ hook_event_name: 'PreToolUse', session_id: 's', tool_name: 'Edit', tool_input: { file_path: 'a/b.java' } });
    assert.deepEqual(input, { event: 'edit', sessionId: 's', filePaths: ['a/b.java'] });
  });
  it('ignores non-edit tools', () => {
    assert.equal(toNormalizedInput({ hook_event_name: 'PreToolUse', session_id: 's', tool_name: 'Bash', tool_input: { command: 'ls' } }), null);
  });
  it('maps a prompt to a normalized prompt event', () => {
    assert.deepEqual(toNormalizedInput({ hook_event_name: 'UserPromptSubmit', session_id: 's', prompt: 'IMPLEMENT' }), { event: 'prompt', sessionId: 's', prompt: 'IMPLEMENT' });
  });
  it('renders a deny in the PreToolUse output shape', () => {
    const output = toClaudeOutput({ hook_event_name: 'PreToolUse' }, { decision: 'deny', reason: 'r' });
    assert.equal(output.hookSpecificOutput.permissionDecision, 'deny');
  });
});

describe('claude adapter inside Copilot CLI', () => {
  it('detects Copilot CLI from its environment', () => {
    assert.equal(isRunningInsideCopilotCli({ COPILOT_CLI: '1' }), true);
    assert.equal(isRunningInsideCopilotCli({}), false);
  });
  it('stays silent and exits 0 for an edit that Claude would deny (real CLI-shaped payload)', () => {
    const patch = '*** Begin Patch\n*** Add File: ECommerceApp.Domain/Foo.cs\n+x\n*** End Patch\n';
    const payload = { hook_event_name: 'PreToolUse', session_id: 'cli-x', tool_name: 'Edit', tool_input: patch };
    const result = run(payload, { COPILOT_CLI: '1' });
    assert.equal(result.status, 0);
    assert.equal(result.stdout, '');
  });
});

describe('claude adapter process', () => {
  it('denies a source edit before GO and exits 0', () => {
    const result = run({ hook_event_name: 'PreToolUse', session_id: `adapter-${Date.now()}`, tool_name: 'Write', tool_input: { file_path: 'ECommerceApp.Domain/Foo.cs' } });
    assert.equal(result.status, 0);
    assert.equal(JSON.parse(result.stdout).hookSpecificOutput.permissionDecision, 'deny');
  });
  it('fails open (exit 0, no output) on malformed stdin', () => {
    const result = spawnSync('node', [adapterPath], { input: 'not json', encoding: 'utf8' });
    assert.equal(result.status, 0);
    assert.equal(result.stdout, '');
  });
});
