// GO gate core: no file edits until the user says go. Tool-agnostic on purpose — each
// tool's adapter maps its own payload onto evaluate()'s normalized input and prints its
// own output format. See docs/adr/0033/0033-go-gate-hooks.md.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const hooksDirectory = path.dirname(fileURLToPath(import.meta.url));
export const repositoryRoot = path.resolve(hooksDirectory, '..', '..');
export const defaultConfigPath = path.join(hooksDirectory, 'gate.config.json');

export function loadConfig(configPath = defaultConfigPath) {
  return JSON.parse(fs.readFileSync(configPath, 'utf8'));
}

export function markerDirectory() {
  return path.join(os.tmpdir(), 'ecommerceapp-go-gate');
}

function markerFile(sessionId) {
  const safeName = String(sessionId).replace(/[^A-Za-z0-9_.-]/g, '_');
  return path.join(markerDirectory(), `${safeName}.go`);
}

export function grant(sessionId) {
  fs.mkdirSync(markerDirectory(), { recursive: true });
  fs.writeFileSync(markerFile(sessionId), new Date().toISOString());
}

export function revoke(sessionId) {
  fs.rmSync(markerFile(sessionId), { force: true });
}

// Age of the marker in ms, or null when there is none.
export function markerAgeMs(sessionId, nowMs = Date.now()) {
  try {
    return nowMs - fs.statSync(markerFile(sessionId)).mtimeMs;
  } catch {
    return null;
  }
}

export function isGranted(sessionId, config, nowMs = Date.now()) {
  let stat;
  try {
    stat = fs.statSync(markerFile(sessionId));
  } catch {
    return false;
  }
  const ageMs = nowMs - stat.mtimeMs;
  if (ageMs > config.markerTtlHours * 3600 * 1000) {
    revoke(sessionId);
    return false;
  }
  return true;
}

// Uppercase tokens only (case-sensitive): ordinary lowercase prose such as "go through the diff
// and fix it" or "implement later" must never unlock edits. APPROVE alone is deliberately not a
// token: it accepts a plan, only IMPLEMENT unlocks edits.
export function matchesToken(prompt, tokens) {
  const text = String(prompt ?? '');
  // The token must START the prompt (optionally after "APPROVE"): "IMPLEMENT phase 2" or
  // "APPROVE IMPLEMENT". Anywhere-in-text matching was unlocked by pasted logs that quote the deny message.
  return tokens.some((token) => new RegExp(`^\\s*(?:APPROVE[\\s,.:;!-]+)?${token}(?![A-Za-z0-9_-])`).test(text));
}

function toPosix(filePath) {
  return filePath.replace(/\\/g, '/');
}

function globToRegExp(glob) {
  const escaped = toPosix(glob)
    .replace(/[.+^${}()|[\]]/g, '\\$&')
    .replace(/\*\*/g, '\u0000')
    .replace(/\*/g, '[^/]*')
    .replace(/\u0000/g, '.*');
  return new RegExp(`^${escaped}$`, os.platform() === 'win32' ? 'i' : '');
}

function isInside(parent, child) {
  const relative = path.relative(parent, child);
  return relative === '' || (!relative.startsWith('..') && !path.isAbsolute(relative));
}

// extraAllowedDirectories: absolute directories an adapter allows (a tool's own user dir).
export function isPathAllowed(filePath, config, extraAllowedDirectories = [], root = repositoryRoot) {
  const absolute = path.resolve(root, filePath);
  if (isInside(root, absolute)) {
    const relative = toPosix(path.relative(root, absolute));
    return config.allowedPathGlobs.some((glob) => globToRegExp(glob).test(relative));
  }
  // The marker directory lives under the temp dir; an agent must not be able to write its own GO marker.
  if (isInside(markerDirectory(), absolute)) return false;
  if (config.allowTempDir && isInside(os.tmpdir(), absolute)) return true;
  return extraAllowedDirectories.some((directory) => isInside(path.resolve(directory), absolute));
}

export function denyReason(sessionId) {
  return (
    'Edits are locked until the user writes IMPLEMENT. Ask the user whether to implement, using your ' +
    'interactive question tool if you have one, otherwise a plain-text question, and do not ' +
    'retry the edit until they answer. Only after the user explicitly approves in this ' +
    'conversation (dialog answer or their own message), unlock with: ' +
    `node tools/hooks/gate-cli.mjs grant ${sessionId}` +
    '. If your own permission system also refuses ("no one to approve"), that is a separate layer, not ' +
    'this gate: explain to the user in your own words which layer said no; see tools/hooks/README.md.'
  );
}

/**
 * input: { event: 'prompt' | 'edit' | 'sessionStart', sessionId, prompt?, filePaths? }
 * options: { config, env, extraAllowedDirectories, nowMs }
 * returns: { decision: 'allow' | 'deny', reason?, note? }
 */
export function evaluate(input, options = {}) {
  const config = options.config ?? loadConfig();
  const env = options.env ?? process.env;
  const nowMs = options.nowMs ?? Date.now();

  if (env[config.disableEnv] === 'off') return { decision: 'allow' };
  if (config.bypassEnv.some((name) => env[name])) return { decision: 'allow' };
  if (!input || !input.sessionId) return { decision: 'allow' };

  if (input.event === 'prompt') {
    if (matchesToken(input.prompt, config.revokeTokens)) {
      revoke(input.sessionId);
      return { decision: 'allow', note: 'GO gate: edits locked again for this session.' };
    }
    if (matchesToken(input.prompt, config.goTokens)) {
      grant(input.sessionId);
      return { decision: 'allow', note: 'GO gate: edits unlocked for this session.' };
    }
    return { decision: 'allow' };
  }

  if (input.event === 'sessionStart') {
    // A fresh session starts locked, unless its own first prompt granted GO just before this fired.
    const age = markerAgeMs(input.sessionId, nowMs);
    if (age !== null && age > config.freshGrantGraceSeconds * 1000) revoke(input.sessionId);
    return { decision: 'allow' };
  }

  if (input.event === 'edit') {
    if (isGranted(input.sessionId, config, nowMs)) return { decision: 'allow' };
    const filePaths = input.filePaths ?? [];
    const everyPathAllowed =
      filePaths.length > 0 &&
      filePaths.every((filePath) => isPathAllowed(filePath, config, options.extraAllowedDirectories));
    if (everyPathAllowed) return { decision: 'allow' };
    return { decision: 'deny', reason: denyReason(input.sessionId) };
  }

  return { decision: 'allow' };
}

export function logError(error) {
  try {
    fs.mkdirSync(markerDirectory(), { recursive: true });
    fs.appendFileSync(
      path.join(markerDirectory(), 'errors.log'),
      `${new Date().toISOString()} ${error?.stack ?? error}\n`,
    );
  } catch {
    // logging must never throw
  }
}

export async function readJsonStdin() {
  const chunks = [];
  for await (const chunk of process.stdin) chunks.push(chunk);
  return JSON.parse(Buffer.concat(chunks).toString('utf8'));
}
