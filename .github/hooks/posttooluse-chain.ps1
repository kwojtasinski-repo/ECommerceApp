# PostToolUse fan-out for context-mode integration.
#
# Reads the Copilot envelope from stdin once and pipes it to BOTH:
#   1. Upstream context-mode hook (SessionDB event capture)
#   2. Our auto-cache extension (RAG -> FTS5 auto-index, host-side)
#
# Logging: default ON (writes to .github/hooks/auto-cache.log).
# Set $env:AUTO_CACHE_DEBUG = '0' (or os-level env var AUTO_CACHE_DEBUG=0)
# to silence both the PS and Node tiers.
#
# Best-effort: any failure is suppressed; exit 0 keeps the hook chain alive.

$ErrorActionPreference = 'SilentlyContinue'

$debug = $env:AUTO_CACHE_DEBUG -ne '0'
$log   = Join-Path $PSScriptRoot 'auto-cache.log'

function Write-DebugLine($msg) {
    if (-not $debug) { return }
    try { Add-Content -Path $log -Value "[$((Get-Date).ToString('o'))] PS $msg" } catch { }
}

$in = [Console]::In.ReadToEnd()
Write-DebugLine "wrapper fired; stdin-bytes=$($in.Length)"
if ([string]::IsNullOrWhiteSpace($in)) { Write-DebugLine "empty stdin; exit"; exit 0 }

# 1. Upstream context-mode hook (unchanged behaviour).
$in | docker exec -i ecommerceapp-context-mode node /app/cli.bundle.mjs hook vscode-copilot posttooluse | Out-Null
Write-DebugLine "upstream-exit=$LASTEXITCODE"

# 2. Our host-side auto-cache (RAG -> ctx_index via MCP stdio).
$nodeOut = $in | node "$PSScriptRoot\auto-cache.mjs" 2>&1
Write-DebugLine "node-exit=$LASTEXITCODE node-out=$nodeOut"

exit 0
