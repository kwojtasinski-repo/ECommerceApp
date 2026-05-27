$req = @'
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"ctx_fetch_and_index","arguments":{"url":"https://raw.githubusercontent.com/microsoft/vscode/main/README.md"}}}
'@
$out = $req | docker exec -i ecommerceapp-context-mode node --require /app/network-monitor.cjs /app/cli.bundle.mjs 2>$null
$lines = $out -split "`n" | Where-Object { $_.Trim() -ne '' }
foreach ($l in $lines) {
    try {
        $j = $l | ConvertFrom-Json
        if ($j.id -eq 2) {
            if ($j.error) { Write-Host "ERROR: $($j.error.message)" -ForegroundColor Red }
            elseif ($j.result.content) {
                $j.result.content | ForEach-Object { Write-Host $_.text }
            }
        }
    } catch {}
}
