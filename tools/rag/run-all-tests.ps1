#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run ALL RAG tests — Python (37 unit + 16 E2E) and .NET — in sequence.

.DESCRIPTION
    Master test runner. Starts Qdrant once, runs Python tests, then .NET tests,
    then stops Qdrant. Reports a combined exit code.

.PARAMETER SkipPython
    Skip the Python test suite entirely.

.PARAMETER SkipDotNet
    Skip the .NET test suite entirely.

.PARAMETER KeepQdrant
    Do not stop Qdrant after tests (useful when Qdrant is shared with other services).

.PARAMETER Verbose
    Pass verbose flags to both pytest and dotnet test.

.EXAMPLE
    # Full run — starts and stops Qdrant automatically
    pwsh tools/rag/run-all-tests.ps1

    # Skip .NET (e.g., model not downloaded)
    pwsh tools/rag/run-all-tests.ps1 -SkipDotNet

    # Keep Qdrant running after tests
    pwsh tools/rag/run-all-tests.ps1 -KeepQdrant

    # Verbose output
    pwsh tools/rag/run-all-tests.ps1 -Verbose
#>
[CmdletBinding()]
param (
    [switch]$SkipPython,
    [switch]$SkipDotNet,
    [switch]$KeepQdrant,
    [switch]$Verbose
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot    = $PSScriptRoot
$PythonTests = Join-Path $RepoRoot 'tools\rag\run-tests.ps1'
$DotNetTests = Join-Path $RepoRoot 'tools\rag-dotnet\run-tests.ps1'

$qdrantStarted = $false
$overallExit   = 0

function Start-Qdrant {
    Write-Host "`n[run-all-tests] Starting Qdrant..." -ForegroundColor Cyan
    docker compose up -d qdrant
    if ($LASTEXITCODE -ne 0) { Write-Error "Failed to start Qdrant." }

    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $null = Invoke-WebRequest -Uri 'http://localhost:6333/readyz' -UseBasicParsing -TimeoutSec 1 -ErrorAction Stop
            Write-Host "[run-all-tests] Qdrant is ready." -ForegroundColor Green
            $env:QDRANT_URL = 'http://localhost:6333'
            return
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    Write-Warning "[run-all-tests] Qdrant health check timed out."
}

function Stop-Qdrant {
    Write-Host "`n[run-all-tests] Stopping Qdrant..." -ForegroundColor Cyan
    docker compose stop qdrant | Out-Null
}

Write-Host "======================================" -ForegroundColor Yellow
Write-Host " RAG Tool — Full Test Suite"           -ForegroundColor Yellow
Write-Host "======================================" -ForegroundColor Yellow

try {
    Start-Qdrant
    $qdrantStarted = $true

    if (-not $SkipPython) {
        Write-Host "`n────── PYTHON TESTS ──────" -ForegroundColor Magenta
        $verboseFlag = if ($Verbose) { @('-Verbose') } else { @() }
        & $PythonTests @verboseFlag
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "[run-all-tests] Python tests FAILED (exit $LASTEXITCODE)"
            $overallExit = $LASTEXITCODE
        }
    } else {
        Write-Host "`n[run-all-tests] Skipping Python tests (-SkipPython)" -ForegroundColor Gray
    }

    if (-not $SkipDotNet) {
        Write-Host "`n────── .NET TESTS ──────" -ForegroundColor Magenta
        $verboseFlag = if ($Verbose) { @('-Verbose') } else { @() }
        & $DotNetTests @verboseFlag
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "[run-all-tests] .NET tests FAILED (exit $LASTEXITCODE)"
            if ($overallExit -eq 0) { $overallExit = $LASTEXITCODE }
        }
    } else {
        Write-Host "`n[run-all-tests] Skipping .NET tests (-SkipDotNet)" -ForegroundColor Gray
    }
}
finally {
    if ($qdrantStarted -and -not $KeepQdrant) { Stop-Qdrant }
}

Write-Host "`n======================================" -ForegroundColor Yellow
if ($overallExit -eq 0) {
    Write-Host " ALL TESTS PASSED" -ForegroundColor Green
} else {
    Write-Host " SOME TESTS FAILED (exit $overallExit)" -ForegroundColor Red
}
Write-Host "======================================" -ForegroundColor Yellow
exit $overallExit
