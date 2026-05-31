$req = @'
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/list"}
'@
$out = $req | docker exec -i ecommerceapp-context-mode sh -lc 'workspace="$CONTEXT_MODE_WORKSPACE"; [ -n "$workspace" ] || workspace=/workspace; cd "$workspace" 2>/dev/null || cd /workspace; exec node --require /app/network-monitor.cjs /app/cli.bundle.mjs' 2>$null
Write-Host "raw lines: $(($out | Measure-Object -Line).Lines)"
$lines = $out -split "`n" | Where-Object { $_.Trim() -ne '' }
foreach ($l in $lines) {
    try {
        $j = $l | ConvertFrom-Json
        if ($j.id -eq 1) { Write-Host "server: $($j.result.serverInfo.name) v$($j.result.serverInfo.version)" }
        if ($j.id -eq 2) { Write-Host "tools ($($j.result.tools.Count)): $(($j.result.tools.name) -join ', ')" }
    } catch {}
}
