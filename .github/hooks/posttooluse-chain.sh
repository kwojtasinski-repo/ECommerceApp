#!/usr/bin/env bash
# posttooluse-chain.sh — Bash PostToolUse fan-out wrapper (macOS/Linux).
#
# Fallback alternative to posttooluse-chain.mjs for environments where
# `node` is not on PATH. Prefer the .mjs version — it is cross-platform
# and produces no shell-window flash on Windows.
#
# Pipes the Copilot hook envelope (stdin) to both:
#   1. Upstream context-mode hook  (docker exec — session DB event capture)
#   2. Host-side auto-cache        (auto-cache.mjs — RAG → FTS5 auto-index)
#
# Logging: set AUTO_CACHE_DEBUG=0 to silence.
# Best-effort: any failure is suppressed. Always exits 0.

set -uo pipefail  # intentionally no -e so failures are swallowed

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG_FILE="$SCRIPT_DIR/auto-cache.log"
DEBUG="${AUTO_CACHE_DEBUG:-1}"

log() {
  [[ "$DEBUG" == "0" ]] && return 0
  printf '[%s] SH-CHAIN %s\n' "$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")" "$*" >> "$LOG_FILE" 2>/dev/null || true
}

# Read stdin once into a temp file so it can be piped twice.
TMP_INPUT="$(mktemp)"
trap 'rm -f "$TMP_INPUT"' EXIT
cat > "$TMP_INPUT"

BYTES=$(wc -c < "$TMP_INPUT")
log "wrapper fired; stdin-bytes=$BYTES"

if [[ "$BYTES" -eq 0 ]]; then
  log "empty stdin; exit"
  exit 0
fi

# ── 1. Upstream context-mode hook (session DB capture) ──────────────────────
docker exec -i ecommerceapp-context-mode \
  node /app/cli.bundle.mjs hook vscode-copilot posttooluse \
  < "$TMP_INPUT" > /dev/null 2>&1 && log "upstream-exit=0" || log "upstream-exit=$?"

# ── 2. Host-side auto-cache (RAG → ctx_index) ───────────────────────────────
AUTOCACHE="$SCRIPT_DIR/auto-cache.mjs"
if command -v node > /dev/null 2>&1; then
  node "$AUTOCACHE" < "$TMP_INPUT" > /dev/null 2>&1 && log "autocache-exit=0" || log "autocache-exit=$?"
else
  log "autocache skipped: node not on PATH"
fi

exit 0
