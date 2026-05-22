#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run all RAG .NET tests (unit + E2E) with optional Qdrant startup.

.DESCRIPTION
    Runs dotnet test for the RagTools.Tests project. E2E tests require an ONNX model
    (tools/rag-dotnet/model/) and Qdrant. Pass -StartQdrant to auto-start Qdrant.

.PARAMETER StartQdrant
    Start Qdrant with `docker compose up -d qdrant` before tests, stop after.

.PARAMETER UnitOnly
    Run only unit tests (filter: Category!=E2E).

.PARAMETER E2EOnly
    Run only E2E tests (filter: Category=E2E). Requires model + Qdrant.

.PARAMETER Verbose
    Pass --logger "console;verbosity=detailed" to dotnet test.

.EXAMPLE
    # Download model first (one-time)
    pwsh tools/rag-dotnet/download-model.ps1

    # Run all .NET tests, auto-start Qdrant
    pwsh tools/rag-dotnet/run-tests.ps1 -StartQdrant

    # Run only unit tests (no Qdrant needed)
    pwsh tools/rag-dotnet/run-tests.ps1 -UnitOnly

    # Run E2E tests with QDRANT_URL already set
    $env:QDRANT_URL = 'http://localhost:6333'
    pwsh tools/rag-dotnet/run-tests.ps1 -E2EOnly
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
$RepoRoot   = (Resolve-Path (Join-Path $ScriptDir '..\..')).Path
$TestsProj  = Join-Path $ScriptDir 'src\RagTools.Tests\RagTools.Tests.csproj'
$ModelDir   = Join-Path $ScriptDir 'model'

if (-not (Test-Path $TestsProj)) {
    Write-Error "Test project not found at $TestsProj"
}

if (-not (Test-Path (Join-Path $ModelDir 'model.onnx'))) {
    if ($UnitOnly) {
        Write-Warning "ONNX model not found — E2E tests will be skipped (model not needed for unit tests)."
    } else {
        Write-Warning "ONNX model not found at $ModelDir/model.onnx. Run: pwsh tools/rag-dotnet/download-model.ps1"
    }
}

$verbosityArgs = if ($Verbose) {
    @('--logger', 'console;verbosity=detailed')
} else {
    @('--logger', 'console;verbosity=normal')
}

$filterArg = if ($UnitOnly) {
    @('--filter', 'Category!=E2E')
} elseif ($E2EOnly) {
    @('--filter', 'Category=E2E')
} else {
    @()
}

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

    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $null = Invoke-WebRequest -Uri 'http://localhost:6333/readyz' -UseBasicParsing -TimeoutSec 1 -ErrorAction Stop
            Write-Host "[rag-tests] Qdrant is ready." -ForegroundColor Green
            # Set QDRANT_URL for the dotnet test process to discover
            $env:QDRANT_URL = 'http://localhost:6333'
            return
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    Write-Warning "[rag-tests] Qdrant may not be ready yet."
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

    Write-Host "`n[rag-tests] Running .NET tests..." -ForegroundColor Cyan
    dotnet test $TestsProj @filterArg @verbosityArgs --no-restore
    $exitCode = $LASTEXITCODE
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
