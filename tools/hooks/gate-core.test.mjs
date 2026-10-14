import assert from 'node:assert/strict';
import os from 'node:os';
import path from 'node:path';
import { afterEach, describe, it } from 'node:test';
import {
  evaluate,
  grant,
  isGranted,
  isPathAllowed,
  loadConfig,
  markerDirectory,
  matchesToken,
  repositoryRoot,
  revoke,
} from './gate-core.mjs';

const config = loadConfig();
const sessions = [];
function newSession() {
  const sessionId = `test-${Date.now()}-${Math.random().toString(36).slice(2)}`;
  sessions.push(sessionId);
  return sessionId;
}
const noBypassEnv = {};
const editSource = (sessionId, filePath) =>
  evaluate({ event: 'edit', sessionId, filePaths: [filePath] }, { config, env: noBypassEnv });

afterEach(() => {
  while (sessions.length) revoke(sessions.pop());
});

describe('matchesToken', () => {
  it('matches an uppercase token at the start of the prompt', () => {
    assert.ok(matchesToken('IMPLEMENT', config.goTokens));
    assert.ok(matchesToken('  APPROVE IMPLEMENT phase 2', config.goTokens));
    assert.ok(matchesToken('IMPLEMENT, faza 2', config.goTokens));
  });
  it('ignores a token that is merely quoted later in a pasted log', () => {
    assert.ok(!matchesToken('CLI log: Denied... Edits are locked until the user writes IMPLEMENT', config.goTokens));
    assert.ok(!matchesToken('ok, IMPLEMENT!', config.goTokens));
  });
  it('is case-sensitive: ordinary lowercase prose never unlocks', () => {
    assert.ok(!matchesToken('implement the plan later', config.goTokens));
    assert.ok(!matchesToken('Implement', config.goTokens));
    assert.ok(!matchesToken('dawaj lecimy', config.goTokens));
  });
  it('APPROVE alone only accepts a plan and does not unlock', () => {
    assert.ok(!matchesToken('APPROVE', config.goTokens));
  });
  it('does not match a token embedded in a longer word', () => {
    assert.ok(!matchesToken('IMPLEMENTATION is long', config.goTokens));
    assert.ok(!matchesToken('REIMPLEMENT', config.goTokens));
    assert.ok(!matchesToken('IMPLEMENT-ish', config.goTokens));
  });
  it('does not match an empty prompt', () => {
    assert.ok(!matchesToken('   ', config.goTokens));
  });
});

describe('edit gate', () => {
  it('denies an edit of source code before GO and names the grant command', () => {
    const sessionId = newSession();
    const result = editSource(sessionId, 'ECommerceApp.Domain/Foo.cs');
    assert.equal(result.decision, 'deny');
    assert.match(result.reason, new RegExp(`gate-cli.mjs grant ${sessionId}`));
  });

  it('points to the permission guide and separates the gate from the tool permission layer', () => {
    const result = editSource(newSession(), 'ECommerceApp.Domain/Foo.cs');
    assert.match(result.reason, /tools\/hooks\/README\.md/);
    assert.match(result.reason, /separate layer/);
  });

  it('allows the same edit after the GO phrase in a prompt', () => {
    const sessionId = newSession();
    evaluate({ event: 'prompt', sessionId, prompt: 'APPROVE IMPLEMENT' }, { config, env: noBypassEnv });
    assert.equal(editSource(sessionId, 'ECommerceApp.Domain/Foo.cs').decision, 'allow');
  });

  it('allows the edit after an explicit grant (dialog approval path)', () => {
    const sessionId = newSession();
    grant(sessionId);
    assert.equal(editSource(sessionId, 'ECommerceApp.Domain/Foo.cs').decision, 'allow');
  });

  it('locks again after a revoke phrase', () => {
    const sessionId = newSession();
    grant(sessionId);
    evaluate({ event: 'prompt', sessionId, prompt: 'HOLD' }, { config, env: noBypassEnv });
    assert.equal(editSource(sessionId, 'ECommerceApp.Domain/Foo.cs').decision, 'deny');
  });

  it('isolates sessions: GO in one session does not unlock another', () => {
    const first = newSession();
    const second = newSession();
    grant(first);
    assert.equal(editSource(second, 'ECommerceApp.Domain/Foo.cs').decision, 'deny');
  });

  it('expires the marker after the TTL', () => {
    const sessionId = newSession();
    grant(sessionId);
    const afterTtl = Date.now() + (config.markerTtlHours + 1) * 3600 * 1000;
    assert.equal(isGranted(sessionId, config, afterTtl), false);
  });

  it('requires every path of a multi-file edit to be allowed', () => {
    const sessionId = newSession();
    const result = evaluate(
      { event: 'edit', sessionId, filePaths: ['.github/plans/x/01.md', 'ECommerceApp.Domain/Foo.cs'] },
      { config, env: noBypassEnv },
    );
    assert.equal(result.decision, 'deny');
  });

  it('denies an edit with no resolvable path (cannot prove it is allow-listed)', () => {
    const sessionId = newSession();
    const result = evaluate({ event: 'edit', sessionId, filePaths: [] }, { config, env: noBypassEnv });
    assert.equal(result.decision, 'deny');
  });
});

describe('session start', () => {
  it('locks a session whose marker is older than the grace period', () => {
    const sessionId = newSession();
    grant(sessionId);
    const later = Date.now() + (config.freshGrantGraceSeconds + 5) * 1000;
    evaluate({ event: 'sessionStart', sessionId }, { config, env: noBypassEnv, nowMs: later });
    assert.equal(isGranted(sessionId, config, later), false);
  });
  it('keeps a GO that was granted by the same session first prompt just before start', () => {
    const sessionId = newSession();
    grant(sessionId);
    evaluate({ event: 'sessionStart', sessionId }, { config, env: noBypassEnv });
    assert.equal(isGranted(sessionId, config), true);
  });
});

describe('allow-list', () => {
  it('allows plan, research and report folders before GO', () => {
    assert.ok(isPathAllowed('.github/plans/hooks-gate/01-phase.md', config));
    assert.ok(isPathAllowed('docs/research/2026-10-13-x/README.md', config));
    assert.ok(isPathAllowed('docs/reports/run1/shot.png', config));
  });
  it('accepts Windows backslashes and absolute paths inside the repo', () => {
    assert.ok(isPathAllowed('.github\\plans\\x\\01.md', config));
    assert.ok(isPathAllowed(path.join(repositoryRoot, 'docs', 'research', 'a.md'), config));
  });
  it('rejects a traversal that escapes an allowed folder', () => {
    assert.ok(!isPathAllowed('.github/plans/../../ECommerceApp.Domain/Foo.cs', config));
  });
  it('does not let an agent write its own GO marker under the temp dir', () => {
    assert.ok(!isPathAllowed(path.join(markerDirectory(), 'some-session.go'), config));
    assert.ok(!isPathAllowed(path.join(markerDirectory(), 'errors.log'), config));
  });
  it('allows the OS temp dir and adapter-supplied directories, nothing else outside the repo', () => {
    const userDirectory = path.join(os.homedir(), '.example-tool');
    assert.ok(isPathAllowed(path.join(os.tmpdir(), 'scratch.txt'), config));
    assert.ok(isPathAllowed(path.join(userDirectory, 'plans', 'p.md'), config, [userDirectory]));
    assert.ok(!isPathAllowed(path.join(os.homedir(), 'other', 'f.txt'), config, [userDirectory]));
  });
});

describe('bypass and fail-open', () => {
  it('allows everything in CI / cloud environments', () => {
    const result = evaluate(
      { event: 'edit', sessionId: newSession(), filePaths: ['ECommerceApp.Domain/Foo.cs'] },
      { config, env: { GITHUB_ACTIONS: 'true' } },
    );
    assert.equal(result.decision, 'allow');
  });
  it('allows everything when the gate is switched off', () => {
    const result = evaluate(
      { event: 'edit', sessionId: newSession(), filePaths: ['ECommerceApp.Domain/Foo.cs'] },
      { config, env: { ECOMMERCEAPP_GATE: 'off' } },
    );
    assert.equal(result.decision, 'allow');
  });
  it('allows when the payload has no session id or is malformed', () => {
    assert.equal(evaluate({ event: 'edit', filePaths: ['x'] }, { config, env: noBypassEnv }).decision, 'allow');
    assert.equal(evaluate(null, { config, env: noBypassEnv }).decision, 'allow');
  });
});
