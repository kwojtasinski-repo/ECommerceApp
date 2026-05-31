#!/usr/bin/env bash
# Quick MCP handshake probe — sends initialize + tools/list to context-mode
# via `docker exec` and prints server info + tool count.
set -euo pipefail

req=$(cat <<'EOF'
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/list"}
EOF
)

out=$(printf '%s\n' "$req" | docker exec -i ecommerceapp-context-mode \
    sh -lc 'workspace="$CONTEXT_MODE_WORKSPACE"; [ -n "$workspace" ] || workspace=/workspace; cd "$workspace" 2>/dev/null || cd /workspace; exec node --require /app/network-monitor.cjs /app/cli.bundle.mjs' 2>/dev/null)

echo "raw lines: $(printf '%s\n' "$out" | grep -c .)"
if command -v jq > /dev/null 2>&1; then
    printf '%s\n' "$out" | head -n1 | jq -r '"server: " + .result.serverInfo.name + " v" + .result.serverInfo.version'
    printf '%s\n' "$out" | sed -n '2p' | jq -r '"tools (" + (.result.tools | length | tostring) + "): " + ([.result.tools[].name] | join(", "))'
else
    echo "(install jq for parsed output; raw JSON follows)"
    printf '%s\n' "$out"
fi
