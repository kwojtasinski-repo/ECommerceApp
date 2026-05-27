/**
 * Network monitoring hook for context-mode.
 * Overrides net.Socket.connect BEFORE the MCP server starts.
 *
 * Logging policy (env-driven):
 *   CONTEXT_MODE_NETMON_VERBOSE=1 → log every connection (INFO + SUSPICIOUS) to stderr
 *   CONTEXT_MODE_NETMON_VERBOSE=0 (default) → only SUSPICIOUS hits to stderr; INFO silenced
 *
 * SUSPICIOUS (= outside private/loopback ranges) ALSO append to the alert file
 * regardless of the verbose flag, so audit trail is preserved.
 */
'use strict';

const net = require('net');
const fs  = require('fs');

const ALERT_LOG = '/workspace/.ctx-network-alerts.log';
const VERBOSE   = process.env.CONTEXT_MODE_NETMON_VERBOSE === '1';

// IP ranges treated as internal (safe)
const INTERNAL = [
  /^127\./,
  /^::1$/,
  /^localhost$/i,
  /^0\.0\.0\.0$/,
  /^10\./,
  /^172\.(1[6-9]|2\d|3[01])\./,
  /^192\.168\./,
  /^fd[0-9a-f]{2}:/i,   // IPv6 ULA fc00::/7
];

// Domains known to be benign internal MCP/Node infrastructure — always silenced
// even when SUSPICIOUS (DNS resolves outside ctx-net). Add cautiously.
const BENIGN_EXTERNAL = [
  /^registry\.npmjs\.org$/i,   // npm metadata fetched by some MCP runtimes
];

function classify(host) {
  return INTERNAL.some(r => r.test(String(host))) ? 'INFO' : 'SUSPICIOUS';
}

function isBenign(host) {
  return BENIGN_EXTERNAL.some(r => r.test(String(host)));
}

const _connect = net.Socket.prototype.connect;

net.Socket.prototype.connect = function (options, ...rest) {
  const host = typeof options === 'object'
    ? (options.host || options.hostname || 'unknown')
    : String(options);
  const port = typeof options === 'object' ? options.port : rest[0];
  const level = classify(host);
  const benign = level === 'SUSPICIOUS' && isBenign(host);
  const ts    = new Date().toISOString();
  const line  = `[NET-MONITOR] [${benign ? 'BENIGN' : level}] ${ts} → ${host}:${port}`;

  // stderr policy:
  //   verbose mode → everything
  //   default     → only true SUSPICIOUS (not benign, not info)
  if (VERBOSE || (level === 'SUSPICIOUS' && !benign)) {
    process.stderr.write(line + '\n');
  }

  // audit file: every truly suspicious hit, regardless of verbose flag
  if (level === 'SUSPICIOUS' && !benign) {
    try { fs.appendFileSync(ALERT_LOG, line + '\n'); } catch (_) { /* workspace unmounted */ }
  }

  return _connect.call(this, options, ...rest);
};

