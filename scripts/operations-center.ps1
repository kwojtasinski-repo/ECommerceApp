param(
    [ValidateSet('rag','contextmode')]
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

        'contextmode::create' {
            $effectivePassword = if ($Password) { $Password } else { 'ThiIS_StrongP4SSWORD!' }
            Invoke-ContextCreate -Password $effectivePassword
        }
        'contextmode::update' { Invoke-ContextUpdate }
        'contextmode::force-update' {
            $effectivePassword = if ($Password) { $Password } else { 'ThiIS_StrongP4SSWORD!' }
            Invoke-ContextForceUpdate -Password $effectivePassword
        }
        'contextmode::fix' { Invoke-ContextFix }
        'contextmode::health' { Invoke-ContextHealth }
        'contextmode::add-whitelist' {
            if (-not $Domain) { throw 'Domain is required for add-whitelist.' }
            Invoke-AdGuardAddWhitelist -Domain $Domain
        }
        'contextmode::add-blacklist' {
            if (-not $Domain) { throw 'Domain is required for add-blacklist.' }
            Invoke-AdGuardAddBlacklist -Domain $Domain
        }
        'contextmode::change-password' {
            if (-not $Password) { throw 'Password is required for change-password.' }
            Invoke-AdGuardChangePassword -Password $Password
        }
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

function Show-ContextMenu {
    while ($true) {
        Write-Host ''
        Write-Host 'ContextMode Menu' -ForegroundColor Cyan
        Write-Host '1. Create environment'
        Write-Host '2. Update environment'
        Write-Host '3. Force Update (recreate + no-cache build)'
        Write-Host '4. Fix Context Mode'
        Write-Host '5. Add whitelist domain'
        Write-Host '6. Add blacklist domain'
        Write-Host '7. Change AdGuard password'
        Write-Host '8. ContextMode health checks'
        Write-Host '9. Back'

        $c = Read-Choice 'Choose option' 1 9
        switch ($c) {
            1 { Invoke-ContextCreate -Password 'ThiIS_StrongP4SSWORD!' }
            2 { Invoke-ContextUpdate }
            3 { Invoke-ContextForceUpdate -Password 'ThiIS_StrongP4SSWORD!' }
            4 { Invoke-ContextFix }
            5 { $d = Read-Host 'Domain (example.com)'; Invoke-AdGuardAddWhitelist -Domain $d }
            6 { $d = Read-Host 'Domain (example.com)'; Invoke-AdGuardAddBlacklist -Domain $d }
            7 { $pwd = Read-Host 'New AdGuard password'; Invoke-AdGuardChangePassword -Password $pwd }
            8 { Invoke-ContextHealth }
            9 { return }
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
    Write-Host '2. ContextMode'
    Write-Host '3. Exit'
    $c = Read-Choice 'Choose area' 1 3
    switch ($c) {
        1 { Show-RagMenu }
        2 { Show-ContextMenu }
        3 { $continueOperations = $false }
    }
}
