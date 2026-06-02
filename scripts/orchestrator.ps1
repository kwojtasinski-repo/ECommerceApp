#!/usr/bin/env pwsh
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Warning 'scripts/orchestrator.ps1 is deprecated. Use scripts/operations-center.ps1.'
& "$PSScriptRoot/operations-center.ps1" @args
