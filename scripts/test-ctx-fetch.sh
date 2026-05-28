#!/usr/bin/env bash
# Quick ctx_fetch_and_index smoke test — calls the tool via `docker exec`
# with a known-safe public URL and prints the result.
#
# Cross-platform companion to test-ctx-fetch.ps1 (macOS / Linux).
#
# USAGE
#   bash scripts/test-ctx-fetch.sh

set -euo pipefail

REQ=$(cat <<'EOF'
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"ctx_fetch_and_index","arguments":{"url":"https://raw.githubusercontent.com/microsoft/vscode/main/README.md"}}}
EOF
)

OUT=$(printf '%s\n' "$REQ" | docker exec -i ecommerceapp-context-mode \
  node --require /app/network-monitor.cjs /app/cli.bundle.mjs 2>/dev/null)

if command -v jq > /dev/null 2>&1; then
  printf '%s\n' "$OUT" | while IFS= read -r line; do
    id=$(printf '%s' "$line" | jq -r '.id // empty' 2>/dev/null)
    if [[ "$id" == "2" ]]; then
      err=$(printf '%s' "$line" | jq -r '.error.message // empty' 2>/dev/null)
      if [[ -n "$err" ]]; then
        echo "ERROR: $err"
      else
        printf '%s' "$line" | jq -r '.result.content[]?.text // empty' 2>/dev/null
      fi
    fi
  done
else
  if command -v python3 > /dev/null 2>&1; then
    printf '%s\n' "$OUT" | python3 -c "
import sys, json
for line in sys.stdin:
    line = line.strip()
    if not line: continue
    try:
        obj = json.loads(line)
        if obj.get('id') == 2:
            if obj.get('error'):
                print('ERROR:', obj['error'].get('message', ''))
            else:
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
