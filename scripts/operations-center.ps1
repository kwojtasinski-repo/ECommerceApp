param(
    [ValidateSet('rag')]
    [string]$Area,
    [ValidateSet('create','update','force-update','fix','health','add-whitelist','add-blacklist','change-password','profiles')]
    [string]$Action,
    [ValidateSet('python-stdio','python-http','dotnet-stdio','dotnet-http')]
    [string]$Profile,
    [string]$Domain,
    [string]$Password,
    [switch]$NoMenu
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. "$PSScriptRoot/lib/common.ps1"

function Invoke-Action {
    param([string]$A,[string]$Act)
    switch ("$A::$Act") {
        'rag::create' { Invoke-RagCreate -Profile $Profile }
        'rag::update' { Invoke-RagUpdate -Profile $Profile }
        'rag::force-update' { Invoke-RagForceUpdate -Profile $Profile }
        'rag::health' { Invoke-RagHealth }
        'rag::profiles' { Show-RagProfiles }

        default { throw "Unsupported combination: $A / $Act" }
    }
}

function Read-Choice([string]$Prompt, [int]$Min, [int]$Max) {
    while ($true) {
        $v = Read-Host $Prompt
        $n = 0
        if ([int]::TryParse($v, [ref]$n)) {
            if ($n -ge $Min -and $n -le $Max) { return $n }
        }
        Write-Warn "Choose a number between $Min and $Max."
    }
}

function Select-RagProfile {
    Write-Host ''
    Write-Host 'RAG profile:' -ForegroundColor Cyan
    Write-Host '1. Python STDIO'
    Write-Host '2. Python HTTP'
    Write-Host '3. .NET STDIO'
    Write-Host '4. .NET HTTP'
    $c = Read-Choice 'Select profile' 1 4
    switch ($c) {
        1 { return 'python-stdio' }
        2 { return 'python-http' }
        3 { return 'dotnet-stdio' }
        4 { return 'dotnet-http' }
    }
}

function Show-RagMenu {
    while ($true) {
        Write-Host ''
        Write-Host 'RAG Menu' -ForegroundColor Cyan
        Write-Host '1. Create environment'
        Write-Host '2. Update environment'
        Write-Host '3. Force Update (recreate + no-cache build)'
        Write-Host '4. Run ingest now'
        Write-Host '5. RAG health checks'
        Write-Host '6. Back'

        $c = Read-Choice 'Choose option' 1 6
        switch ($c) {
            1 { $p = Select-RagProfile; Invoke-RagCreate -Profile $p }
            2 { $p = Select-RagProfile; Invoke-RagUpdate -Profile $p }
            3 { $p = Select-RagProfile; Invoke-RagForceUpdate -Profile $p }
            4 {
                $p = Select-RagProfile
                if ($p -like 'python-*') {
                    Ensure-RagPythonStatsFile
                    Invoke-RepoCommand 'docker compose --profile rag run --rm rag-tools python ingest.py'
                }
                if ($p -like 'dotnet-*') {
                    Ensure-RagDotnetStatsFile
                    Invoke-RepoCommand 'docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll'
                }
            }
            5 { Invoke-RagHealth }
            6 { return }
        }
    }
}

if ($NoMenu -or ($Area -and $Action)) {
    if (-not $Area -or -not $Action) {
        throw 'For non-interactive mode, provide both -Area and -Action.'
    }
    Invoke-Action -A $Area -Act $Action
    exit 0
}

$continueOperations = $true
while ($continueOperations) {
    Write-Host ''
    Write-Host 'ECommerce Operations Center' -ForegroundColor Cyan
    Write-Host '1. RAG'
    Write-Host '2. Exit'
    $c = Read-Choice 'Choose area' 1 2
    switch ($c) {
        1 { Show-RagMenu }
        2 { $continueOperations = $false }
    }
}
