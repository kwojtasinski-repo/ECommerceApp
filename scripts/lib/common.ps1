Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Write-Step([string]$Message) { Write-Host "-> $Message" -ForegroundColor Cyan }
function Write-Ok([string]$Message) { Write-Host "OK  $Message" -ForegroundColor Green }
function Write-Warn([string]$Message) { Write-Host "!   $Message" -ForegroundColor Yellow }
function Write-Fail([string]$Message) { Write-Host "XX  $Message" -ForegroundColor Red }

function Invoke-RepoCommand {
    param([Parameter(Mandatory = $true)][string]$Command)
    $repo = Get-RepoRoot
    Push-Location $repo
    try {
        Write-Step $Command
        Invoke-Expression $Command
        if ($LASTEXITCODE -ne 0) { throw "Command failed (exit $LASTEXITCODE): $Command" }
    }
    finally { Pop-Location }
}

function Test-DockerReady {
    try {
        docker ps | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Assert-Prereqs {
    if (-not (Test-DockerReady)) {
        throw 'Docker is not ready. Start Docker Desktop/daemon and retry.'
    }
}

function Get-RagProfiles {
    return @(
        'python-stdio',
        'python-http',
        'dotnet-stdio',
        'dotnet-http'
    )
}

function Assert-RagProfile([string]$Profile) {
    if (-not $Profile) { return }
    $allowed = Get-RagProfiles
    if ($Profile -notin $allowed) {
        throw "Invalid profile '$Profile'. Allowed: $($allowed -join ', ')"
    }
}

function Ensure-RagDotnetStatsFile {
    $repo = Get-RepoRoot
    $ragDir = Join-Path $repo '.rag'
    $stats = Join-Path $ragDir 'index-stats-dotnet.md'

    if (-not (Test-Path $ragDir)) {
        New-Item -ItemType Directory -Path $ragDir -Force | Out-Null
    }
    if (-not (Test-Path $stats)) {
        Set-Content -Path $stats -Value '# RAG Index Stats' -Encoding UTF8
    }
}

function Ensure-RagPythonStatsFile {
    $repo = Get-RepoRoot
    $ragDir = Join-Path $repo '.rag'
    $stats = Join-Path $ragDir 'index-stats.md'

    if (-not (Test-Path $ragDir)) {
        New-Item -ItemType Directory -Path $ragDir -Force | Out-Null
    }
    if (-not (Test-Path $stats)) {
        Set-Content -Path $stats -Value '# RAG Index Stats' -Encoding UTF8
    }
}

function Ensure-AdGuardPolicyFiles {
    $repo = Get-RepoRoot
    $wl = Join-Path $repo 'docker/adguard/team-whitelist.txt'
    $bl = Join-Path $repo 'docker/adguard/team-blacklist.txt'

    if (-not (Test-Path $wl)) {
        New-Item -ItemType File -Path $wl -Force | Out-Null
        Add-Content -Path $wl -Value '# Team whitelist (allow overrides)'
    }
    if (-not (Test-Path $bl)) {
        New-Item -ItemType File -Path $bl -Force | Out-Null
        Add-Content -Path $bl -Value '# Team blacklist (block rules)'
    }

    $blRules = Get-Content $bl -ErrorAction SilentlyContinue
    if ($blRules -notcontains '||*^') {
        Add-Content -Path $bl -Value '||*^'
        Write-Warn 'Added strict deny-all baseline rule to team-blacklist.txt: ||*^'
    }
}

function Invoke-RagCreate {
    param([string]$Profile)
    Assert-RagProfile $Profile
    Assert-Prereqs

    Invoke-RepoCommand 'docker compose --profile rag --profile rag-dotnet --profile rag-python-http --profile rag-dotnet-http up -d qdrant'
    Invoke-RepoCommand 'docker compose build rag-tools'
    Invoke-RepoCommand 'docker compose build rag-dotnet'

    if (-not $Profile -or $Profile -like 'python-*') {
        Ensure-RagPythonStatsFile
        Invoke-RepoCommand 'docker compose --profile rag run --rm rag-tools python ingest.py'
    }
    if (-not $Profile -or $Profile -like 'dotnet-*') {
        Ensure-RagDotnetStatsFile
        Invoke-RepoCommand 'docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll'
    }

    switch ($Profile) {
        'python-http' { Invoke-RepoCommand 'docker compose --profile rag-python-http up -d rag-python-http' }
        'dotnet-http' { Invoke-RepoCommand 'docker compose --profile rag-dotnet-http up -d rag-dotnet-http' }
    }

    Write-Ok 'RAG create completed.'
}

function Invoke-RagUpdate {
    param([string]$Profile)
    Assert-RagProfile $Profile
    Assert-Prereqs

    Invoke-RepoCommand 'docker compose --profile rag --profile rag-dotnet --profile rag-python-http --profile rag-dotnet-http up -d qdrant'

    if (-not $Profile -or $Profile -like 'python-*') {
        Ensure-RagPythonStatsFile
        Invoke-RepoCommand 'docker compose --profile rag run --rm rag-tools python ingest.py'
    }
    if (-not $Profile -or $Profile -like 'dotnet-*') {
        Ensure-RagDotnetStatsFile
        Invoke-RepoCommand 'docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll'
    }

    switch ($Profile) {
        'python-http' { Invoke-RepoCommand 'docker compose --profile rag-python-http up -d rag-python-http' }
        'dotnet-http' { Invoke-RepoCommand 'docker compose --profile rag-dotnet-http up -d rag-dotnet-http' }
    }

    Write-Ok 'RAG update completed.'
}

function Invoke-RagForceUpdate {
    param([string]$Profile)
    Assert-RagProfile $Profile
    Assert-Prereqs

    Invoke-RepoCommand 'docker compose build --no-cache rag-tools'
    Invoke-RepoCommand 'docker compose build --no-cache rag-dotnet'
    Invoke-RepoCommand 'docker compose --profile rag --profile rag-dotnet --profile rag-python-http --profile rag-dotnet-http up -d --force-recreate qdrant'

    if (-not $Profile -or $Profile -like 'python-*') {
        Ensure-RagPythonStatsFile
        Invoke-RepoCommand 'docker compose --profile rag run --rm rag-tools python ingest.py --force-full'
    }
    if (-not $Profile -or $Profile -like 'dotnet-*') {
        Ensure-RagDotnetStatsFile
        Invoke-RepoCommand 'docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll --force-full'
    }

    switch ($Profile) {
        'python-http' { Invoke-RepoCommand 'docker compose --profile rag-python-http up -d --force-recreate rag-python-http' }
        'dotnet-http' { Invoke-RepoCommand 'docker compose --profile rag-dotnet-http up -d --force-recreate rag-dotnet-http' }
    }

    Write-Ok 'RAG force update completed.'
}

function Invoke-RagHealth {
    Assert-Prereqs
    Invoke-RepoCommand 'docker ps --format "table {{.Names}}\t{{.Status}}"'
    Invoke-RepoCommand 'docker logs --tail 20 ecommerceapp-rag-dotnet-http-1'
    Invoke-RepoCommand 'docker logs --tail 20 ecommerceapp-rag-python-http-1'
}

function Invoke-ContextCreate {
    param([string]$Password = 'ThiIS_StrongP4SSWORD!')
    Assert-Prereqs
    Ensure-AdGuardPolicyFiles
    $repo = Get-RepoRoot
    $script = Join-Path $repo 'scripts/context-mode-bootstrap.ps1'
    Invoke-RepoCommand "powershell -NoProfile -ExecutionPolicy Bypass -File `"$script`" -AdGuardPassword `"$Password`" -ForceRegenerateAdGuard"
    Write-Ok 'ContextMode create completed.'
}

function Invoke-ContextUpdate {
    Assert-Prereqs
    Ensure-AdGuardPolicyFiles
    Invoke-RepoCommand 'docker compose --profile monitoring --profile context-mode build context-mode'
    Invoke-RepoCommand 'docker compose --profile monitoring --profile context-mode up -d --force-recreate adguard context-mode'
    Write-Ok 'ContextMode update completed.'
}

function Invoke-ContextForceUpdate {
    param([string]$Password = 'ThiIS_StrongP4SSWORD!')
    Assert-Prereqs
    Ensure-AdGuardPolicyFiles
    Invoke-RepoCommand 'docker compose --profile monitoring --profile context-mode build --no-cache context-mode'
    Invoke-RepoCommand 'docker compose --profile monitoring --profile context-mode up -d --force-recreate adguard context-mode'
    $repo = Get-RepoRoot
    $script = Join-Path $repo 'scripts/context-mode-bootstrap.ps1'
    Invoke-RepoCommand "powershell -NoProfile -ExecutionPolicy Bypass -File `"$script`" -AdGuardPassword `"$Password`" -ForceRegenerateAdGuard -SkipBuild"
    Write-Ok 'ContextMode force update completed.'
}

function Invoke-ContextFix {
    Assert-Prereqs
    Invoke-RepoCommand 'docker compose --profile monitoring --profile context-mode up -d adguard context-mode'
    $repo = Get-RepoRoot
    Invoke-RepoCommand "powershell -NoProfile -File `"$repo/scripts/test-mcp-handshake.ps1`""
    Invoke-RepoCommand "powershell -NoProfile -File `"$repo/scripts/test-ctx-doctor.ps1`""
    Invoke-RepoCommand 'docker logs --tail 25 ecommerceapp-context-mode'
    Write-Ok 'ContextMode fix sequence completed.'
}

function Invoke-ContextHealth {
    Assert-Prereqs
    Invoke-RepoCommand 'docker inspect ecommerceapp-context-mode --format "Health={{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}} Running={{.State.Running}}"'
    Invoke-RepoCommand 'docker logs --tail 25 ecommerceapp-context-mode'
}

function Invoke-AdGuardAddWhitelist {
    param([Parameter(Mandatory = $true)][string]$Domain)
    Assert-Prereqs
    $repo = Get-RepoRoot
    $cmd = "powershell -NoProfile -File `"$repo/scripts/adguard/domain-policy.ps1`" add whitelist `"@@||$Domain^`""
    Invoke-RepoCommand $cmd
}

function Invoke-AdGuardAddBlacklist {
    param([Parameter(Mandatory = $true)][string]$Domain)
    Assert-Prereqs
    $repo = Get-RepoRoot
    $cmd = "powershell -NoProfile -File `"$repo/scripts/adguard/domain-policy.ps1`" add blacklist `"||$Domain^`""
    Invoke-RepoCommand $cmd
}

function Invoke-AdGuardChangePassword {
    param([Parameter(Mandatory = $true)][string]$Password)
    Assert-Prereqs
    $repo = Get-RepoRoot
    $script = Join-Path $repo 'scripts/context-mode-bootstrap.ps1'
    Invoke-RepoCommand "powershell -NoProfile -ExecutionPolicy Bypass -File `"$script`" -AdGuardPassword `"$Password`" -ForceRegenerateAdGuard -SkipBuild"
    Write-Ok 'AdGuard password rotated.'
}

function Show-RagProfiles {
    Write-Host 'Available RAG profiles:' -ForegroundColor Cyan
    Get-RagProfiles | ForEach-Object { Write-Host " - $_" }
}
