#!/bin/sh
# Wrapper that runs context-mode with the monitoring hook preloaded.
# `node --require` loads network-monitor.cjs (CommonJS — required because
# upstream package.json declares "type":"module") BEFORE the MCP server.
# cli.bundle.mjs with no args starts the MCP server on stdio.
exec node \
  --require /app/network-monitor.cjs \
  /app/cli.bundle.mjs \
  "$@"
