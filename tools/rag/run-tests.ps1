#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run all RAG tool tests (Python unit + E2E) with optional Qdrant startup.

.DESCRIPTION
    Runs the full Python test suite for tools/rag/ — 37 unit tests + 16 E2E tests.
    The E2E Qdrant test requires a running Qdrant instance. Pass -StartQdrant to
    start it automatically via docker compose before running (and stop it after).

.PARAMETER StartQdrant
    Start Qdrant with `docker compose up -d qdrant` before E2E tests, stop after.

.PARAMETER UnitOnly
    Run only the 37 unit tests (test_ingest_unit.py). Does not require Qdrant.

.PARAMETER E2EOnly
    Run only the 16 E2E tests (test_ingest_e2e.py). Requires Qdrant running.

.PARAMETER Verbose
    Pass -v to pytest for verbose per-test output.

.EXAMPLE
    # Run unit tests only (no Qdrant needed)
    pwsh tools/rag/run-tests.ps1 -UnitOnly

    # Run all tests, auto-start Qdrant
    pwsh tools/rag/run-tests.ps1 -StartQdrant

    # Run all tests (Qdrant already running on localhost:6333)
    pwsh tools/rag/run-tests.ps1

    # Run with verbose output
    pwsh tools/rag/run-tests.ps1 -Verbose -StartQdrant
#>
[CmdletBinding()]
param (
    [switch]$StartQdrant,
    [switch]$UnitOnly,
    [switch]$E2EOnly,
    [switch]$Verbose
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ScriptDir  = $PSScriptRoot
$Pytest     = Join-Path $ScriptDir '.venv\Scripts\pytest.exe'
$UnitFile   = Join-Path $ScriptDir 'test_ingest_unit.py'
$E2EFile    = Join-Path $ScriptDir 'test_ingest_e2e.py'
$RepoRoot   = (Resolve-Path (Join-Path $ScriptDir '..\..')).Path

if (-not (Test-Path $Pytest)) {
    Write-Error "Python venv not found at $Pytest. Run: cd tools/rag ; python -m venv .venv ; .venv\Scripts\pip install -e .[dev]"
}

$VerboseFlag = if ($Verbose) { @('-v') } else { @() }
$qdrantStarted = $false
$exitCode = 0

function Start-Qdrant {
    Write-Host "`n[rag-tests] Starting Qdrant via docker compose..." -ForegroundColor Cyan
    Push-Location $RepoRoot
    docker compose up -d qdrant
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Write-Error "Failed to start Qdrant."
    }
    Pop-Location

    # Wait for Qdrant to be ready (up to 20s)
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $null = Invoke-WebRequest -Uri 'http://localhost:6333/readyz' -UseBasicParsing -TimeoutSec 1 -ErrorAction Stop
            Write-Host "[rag-tests] Qdrant is ready." -ForegroundColor Green
            return
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    Write-Warning "[rag-tests] Qdrant may not be ready yet (health check timed out after 20s)."
}

function Stop-Qdrant {
    Write-Host "`n[rag-tests] Stopping Qdrant..." -ForegroundColor Cyan
    Push-Location $RepoRoot
    docker compose stop qdrant | Out-Null
    Pop-Location
}

try {
    if ($StartQdrant) {
        Start-Qdrant
        $qdrantStarted = $true
    }

    if (-not $E2EOnly) {
        Write-Host "`n[rag-tests] Running unit tests ($UnitFile)..." -ForegroundColor Cyan
        & $Pytest $UnitFile @VerboseFlag
        if ($LASTEXITCODE -ne 0) {
            $exitCode = $LASTEXITCODE
            if ($UnitOnly) { exit $exitCode }
        }
    }

    if (-not $UnitOnly) {
        Write-Host "`n[rag-tests] Running E2E tests ($E2EFile)..." -ForegroundColor Cyan
        & $Pytest $E2EFile @VerboseFlag
        if ($LASTEXITCODE -ne 0) { $exitCode = $LASTEXITCODE }
    }
}
finally {
    if ($qdrantStarted) { Stop-Qdrant }
}

if ($exitCode -eq 0) {
    Write-Host "`n[rag-tests] ALL TESTS PASSED" -ForegroundColor Green
} else {
    Write-Host "`n[rag-tests] SOME TESTS FAILED (exit $exitCode)" -ForegroundColor Red
}
exit $exitCode
