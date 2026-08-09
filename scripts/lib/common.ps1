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

function Show-RagProfiles {
    Write-Host 'Available RAG profiles:' -ForegroundColor Cyan
    Get-RagProfiles | ForEach-Object { Write-Host " - $_" }
}
