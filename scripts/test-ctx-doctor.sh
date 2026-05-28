#!/usr/bin/env bash
# Quick ctx_doctor smoke test — sends initialize + ctx_doctor call to
# context-mode via `docker exec` and prints the diagnostic report.
#
# Cross-platform companion to test-ctx-doctor.ps1 (macOS / Linux).
#
# USAGE
#   bash scripts/test-ctx-doctor.sh

set -euo pipefail

REQ=$(cat <<'EOF'
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"ctx_doctor","arguments":{}}}
EOF
)

OUT=$(printf '%s\n' "$REQ" | docker exec -i ecommerceapp-context-mode \
  node --require /app/network-monitor.cjs /app/cli.bundle.mjs 2>/dev/null)

if command -v jq > /dev/null 2>&1; then
  printf '%s\n' "$OUT" | while IFS= read -r line; do
    id=$(printf '%s' "$line" | jq -r '.id // empty' 2>/dev/null)
    if [[ "$id" == "2" ]]; then
      printf '%s' "$line" | jq -r '.result.content[]?.text // empty' 2>/dev/null
    fi
  done
else
  # Fallback: use python if available, otherwise print raw
  if command -v python3 > /dev/null 2>&1; then
    printf '%s\n' "$OUT" | python3 -c "
import sys, json
for line in sys.stdin:
    line = line.strip()
    if not line: continue
    try:
        obj = json.loads(line)
        if obj.get('id') == 2:
            for c in (obj.get('result') or {}).get('content') or []:
                print(c.get('text', ''))
    except Exception:
        pass
"
  else
    echo "(install jq or python3 for parsed output; raw JSON follows)"
    printf '%s\n' "$OUT"
  fi
fi
