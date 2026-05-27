<#
================================================================================
  domain-policy.ps1 — AdGuard DNS filter management CLI for ECommerceApp
  Per ADR-0029: context-mode sandbox network egress policy
================================================================================

USAGE
  ./scripts/adguard/domain-policy.ps1 <subcommand> [args] [flags]

TARGETS
  blacklist    → docker/adguard/team-blacklist.txt  (blocking rules, id=1001)
  whitelist    → docker/adguard/team-whitelist.txt  (allow overrides,   id=1002)

INSPECTION (read-only)
  status [--verbose]
        Table of all AdGuard filters: id, name, enabled, source, rule count.
        --verbose: also print first 5 lines of each file-based filter.

  show <target> [--tail N] [--grep PATTERN]
        Print contents of target file.

  show all
        Concatenated contents of both targets with headers.

PRIMARY EDIT (file-first)
  edit <target>
        Open target file in $env:EDITOR (fallback: code -w, then notepad).
        Reloads AdGuard on editor exit.

  import <target> <localfile>
        Bulk append from local file, dedup against existing, reload.

CONVENIENCE EDIT
  add <target> <rule>
        Single rule append (dedup), reload.

CONTROL
  reload
        docker compose restart adguard (~5s downtime).

  help [subcommand]
        Usage info.

DESIGN NOTES
  * All file edits happen on the HOST. AdGuard sees them via volume bind
    (docker/adguard → /opt/adguardhome/conf). Zero `docker exec` for edits.
  * Dedup is EXACT text match (case-sensitive, trim whitespace). It does NOT
    cover: (a) semantic dedup (||evil.com^ vs ||www.evil.com^ are distinct),
    (b) cross-file overlap (same domain on both lists is legitimate —
    whitelist wins per AdGuard precedence).
  * Reload uses `docker compose restart adguard` — no credential handling.
  * NEVER touches: AdGuardHome.yaml users: block, DNS config, query log,
    container lifecycle beyond the single restart.
  * NEVER auto-commits or stages — git ops stay user-driven.

EXAMPLES
  ./scripts/adguard/domain-policy.ps1 status
  ./scripts/adguard/domain-policy.ps1 add blacklist "||malware-c2.io^"
  ./scripts/adguard/domain-policy.ps1 import blacklist ./threat-feed.txt
  ./scripts/adguard/domain-policy.ps1 edit whitelist
  ./scripts/adguard/domain-policy.ps1 show whitelist --tail 10
  ./scripts/adguard/domain-policy.ps1 reload
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Subcommand = 'help',

    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]]$Rest
)

$ErrorActionPreference = 'Stop'

# ── Paths ────────────────────────────────────────────────────────────────────
$RepoRoot       = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$AdGuardConfDir = Join-Path $RepoRoot 'docker\adguard'
$BlacklistFile  = Join-Path $AdGuardConfDir 'team-blacklist.txt'
$WhitelistFile  = Join-Path $AdGuardConfDir 'team-whitelist.txt'
$YamlFile       = Join-Path $AdGuardConfDir 'AdGuardHome.yaml'

# ── Helpers ──────────────────────────────────────────────────────────────────
function Write-Ok    { param([string]$m) Write-Host "✓ $m" -ForegroundColor Green }
function Write-Info  { param([string]$m) Write-Host "ℹ $m" -ForegroundColor Cyan }
function Write-Warn  { param([string]$m) Write-Host "⚠ $m" -ForegroundColor Yellow }
function Write-Err   { param([string]$m) Write-Host "✗ $m" -ForegroundColor Red }

function Resolve-Target {
    param([string]$Target)
    switch ($Target) {
        'blacklist' { return @{ Name = 'blacklist'; File = $BlacklistFile; Id = 1001 } }
        'whitelist' { return @{ Name = 'whitelist'; File = $WhitelistFile; Id = 1002 } }
        default {
            Write-Err "Unknown target '$Target'. Valid: blacklist, whitelist."
            exit 2
        }
    }
}

function Test-RuleSyntax {
    param([string]$Rule)
    $r = $Rule.Trim()
    if (-not $r) { return $false }
    if ($r.StartsWith('#') -or $r.StartsWith('!')) { return $false }
    # Accept basic AdBlock-style patterns: ||domain^, @@||domain^, plain hostname
    return ($r -match '^(@@)?(\|\|)?[a-zA-Z0-9.\-*?_]+\^?(\$[a-z=,~]*)?$')
}

function Get-FileRules {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return @() }
    return @(Get-Content $Path |
        Where-Object { $_ -and ($_.Trim()) -and -not ($_.Trim() -match '^[#!]') } |
        ForEach-Object { $_.Trim() })
}

function Add-RulesWithDedup {
    param([string]$Path, [string[]]$NewRules)
    $existing = Get-FileRules -Path $Path
    $candidate = @($NewRules | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not ($_ -match '^[#!]') })
    $toAdd = @($candidate | Where-Object { $_ -notin $existing })
    $skipped = $candidate.Count - $toAdd.Count
    if ($toAdd.Count -eq 0) {
        Write-Warn "No new rules added (all $($candidate.Count) already present)."
        return $false
    }
    if (-not (Test-Path $Path)) { New-Item -ItemType File -Path $Path -Force | Out-Null }
    Add-Content -Path $Path -Value $toAdd
    Write-Ok "Added $($toAdd.Count) new rule(s) to $(Split-Path $Path -Leaf); $skipped already present."
    return $true
}

function Invoke-AdGuardReload {
    Write-Info "Reloading AdGuard (docker compose restart adguard)…"
    Push-Location $RepoRoot
    try {
        docker compose restart adguard | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Err "docker compose restart failed (exit $LASTEXITCODE)."
            exit 1
        }
        Write-Ok "AdGuard restarted."
    } finally {
        Pop-Location
    }
}

function Show-TeamCommitReminder {
    param([string]$Target, [string]$FileName)
    $branch = "security/update-$Target-" + (Get-Date -Format 'yyyyMMdd-HHmmss')
    Write-Host ""
    Write-Warn "$FileName modified (committed file)."
    Write-Host "  Share with the team:" -ForegroundColor Yellow
    Write-Host "    git checkout -b $branch" -ForegroundColor Yellow
    Write-Host "    git add docker/adguard/$FileName" -ForegroundColor Yellow
    Write-Host "    git commit -m '<security|chore>(adguard): update $Target'" -ForegroundColor Yellow
    Write-Host "    git push origin $branch" -ForegroundColor Yellow
    Write-Host "    gh pr create --title '<security|chore>(adguard): update $Target'" -ForegroundColor Yellow
}

function Get-EditorCommand {
    if ($env:EDITOR) { return @($env:EDITOR) }
    $code = Get-Command code -ErrorAction SilentlyContinue
    if ($code) { return @('code', '-w') }
    return @('notepad')
}

function Get-YamlFilters {
    # Best-effort: parse filters: + whitelist_filters: blocks from AdGuardHome.yaml.
    # Returns array of @{ Id; Name; Url; Enabled; Section } objects.
    if (-not (Test-Path $YamlFile)) { return @() }
    $lines = Get-Content $YamlFile
    $filters = @()
    $section = $null
    $current = $null
    foreach ($line in $lines) {
        if ($line -match '^(filters|whitelist_filters):\s*$') {
            $section = $matches[1]
            continue
        }
        if ($section -and $line -match '^[a-z_]+:') {
            if ($current) { $filters += [pscustomobject]$current; $current = $null }
            $section = $null
            continue
        }
        if ($section -and $line -match '^\s*- enabled:\s*(true|false)\s*$') {
            if ($current) { $filters += [pscustomobject]$current }
            $current = @{ Section = $section; Enabled = $matches[1]; Url = ''; Name = ''; Id = '' }
            continue
        }
        if ($current -and $line -match '^\s*url:\s*(.+)\s*$') { $current.Url  = $matches[1].Trim() }
        if ($current -and $line -match '^\s*name:\s*(.+)\s*$') { $current.Name = $matches[1].Trim() }
        if ($current -and $line -match '^\s*id:\s*(\d+)\s*$')   { $current.Id   = $matches[1] }
    }
    if ($current) { $filters += [pscustomobject]$current }
    return $filters
}

# ── Subcommands ──────────────────────────────────────────────────────────────

function Invoke-Status {
    param([switch]$Verbose)
    Write-Host ""
    Write-Host "AdGuard filter state (from $YamlFile)" -ForegroundColor Cyan
    Write-Host ("─" * 100) -ForegroundColor DarkGray
    $filters = Get-YamlFilters
    if ($filters.Count -eq 0) {
        Write-Warn "Could not parse $YamlFile (file missing or empty?)."
        return
    }
    $rows = foreach ($f in $filters) {
        $source = if ($f.Url -like '/opt/*') { 'file:' + (Split-Path $f.Url -Leaf) } else { 'url:' + ($f.Url -replace '^https?://', '' | ForEach-Object { if ($_.Length -gt 50) { $_.Substring(0, 47) + '...' } else { $_ } }) }
        $rules = '-'
        if ($f.Url -like '/opt/*') {
            $local = Join-Path $AdGuardConfDir (Split-Path $f.Url -Leaf)
            if (Test-Path $local) { $rules = (Get-FileRules -Path $local).Count }
        }
        [pscustomobject]@{
            Id        = $f.Id
            Name      = $f.Name
            Enabled   = $f.Enabled
            Section   = $f.Section
            Source    = $source
            Rules     = $rules
        }
    }
    $rows | Format-Table -AutoSize | Out-String | Write-Host
    if ($Verbose) {
        foreach ($target in @('blacklist', 'whitelist')) {
            $info = Resolve-Target $target
            Write-Host "── First 5 lines of $($info.Name) ($($info.File | Split-Path -Leaf)) ──" -ForegroundColor DarkGray
            if (Test-Path $info.File) {
                Get-Content $info.File -TotalCount 5 | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
            } else {
                Write-Host "  (file missing)" -ForegroundColor DarkGray
            }
            Write-Host ""
        }
    }
}

function Invoke-Show {
    param([string[]]$InputArgs)
    if (-not $InputArgs -or $InputArgs.Count -eq 0) {
        Write-Err "Usage: show <target|all> [--tail N] [--grep PATTERN]"
        exit 2
    }
    $target = $InputArgs[0]
    $tail = 0
    $grep = $null
    for ($i = 1; $i -lt $InputArgs.Count; $i++) {
        if ($InputArgs[$i] -eq '--tail' -and $i + 1 -lt $InputArgs.Count) { $tail = [int]$InputArgs[$i + 1]; $i++ }
        elseif ($InputArgs[$i] -eq '--grep' -and $i + 1 -lt $InputArgs.Count) { $grep = $InputArgs[$i + 1]; $i++ }
    }
    $targets = if ($target -eq 'all') { @('blacklist', 'whitelist') } else { @($target) }
    foreach ($t in $targets) {
        $info = Resolve-Target $t
        Write-Host ""
        Write-Host "── $($info.Name) ($($info.File | Split-Path -Leaf)) ──" -ForegroundColor Cyan
        if (-not (Test-Path $info.File)) {
            Write-Warn "File missing: $($info.File)"
            continue
        }
        $content = Get-Content $info.File
        if ($grep) { $content = $content | Where-Object { $_ -match $grep } }
        if ($tail -gt 0) { $content = $content | Select-Object -Last $tail }
        $content | ForEach-Object { Write-Host $_ }
    }
}

function Invoke-Edit {
    param([string[]]$InputArgs)
    if (-not $InputArgs -or $InputArgs.Count -lt 1) {
        Write-Err "Usage: edit <target>"
        exit 2
    }
    $info = Resolve-Target $InputArgs[0]
    if (-not (Test-Path $info.File)) {
        Write-Warn "File missing, creating: $($info.File)"
        New-Item -ItemType File -Path $info.File -Force | Out-Null
    }
    $before = (Get-FileHash $info.File).Hash
    $editor = Get-EditorCommand
    Write-Info "Opening $($info.File | Split-Path -Leaf) in $($editor -join ' ')…"
    & $editor[0] $editor[1..($editor.Count - 1)] $info.File | Out-Null
    if ($LASTEXITCODE -ne 0 -and $editor[0] -ne 'notepad') {
        Write-Warn "Editor returned exit $LASTEXITCODE (continuing anyway)."
    }
    $after = (Get-FileHash $info.File).Hash
    if ($before -eq $after) {
        Write-Info "No changes detected. Skipping reload."
        return
    }
    Invoke-AdGuardReload
    Show-TeamCommitReminder -Target $info.Name -FileName ($info.File | Split-Path -Leaf)
}

function Invoke-Import {
    param([string[]]$InputArgs)
    if (-not $InputArgs -or $InputArgs.Count -lt 2) {
        Write-Err "Usage: import <target> <localfile>"
        exit 2
    }
    $info = Resolve-Target $InputArgs[0]
    $src  = $InputArgs[1]
    if (-not (Test-Path $src)) {
        Write-Err "Source file not found: $src"
        exit 1
    }
    $newRules = @(Get-Content $src | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not ($_ -match '^[#!]') })
    if ($newRules.Count -eq 0) {
        Write-Warn "Source file has no rule lines (only comments/blanks)."
        return
    }
    $changed = Add-RulesWithDedup -Path $info.File -NewRules $newRules
    if ($changed) {
        Invoke-AdGuardReload
        Show-TeamCommitReminder -Target $info.Name -FileName ($info.File | Split-Path -Leaf)
    }
}

function Invoke-Add {
    param([string[]]$InputArgs)
    if (-not $InputArgs -or $InputArgs.Count -lt 2) {
        Write-Err "Usage: add <target> <rule>"
        exit 2
    }
    $info = Resolve-Target $InputArgs[0]
    $rule = $InputArgs[1].Trim()
    if (-not (Test-RuleSyntax $rule)) {
        Write-Err "Rule '$rule' does not look like a valid AdBlock-style filter."
        Write-Err "Expected patterns: ||domain.com^, @@||domain.com^, or plain hostname."
        exit 2
    }
    $changed = Add-RulesWithDedup -Path $info.File -NewRules @($rule)
    if ($changed) {
        Invoke-AdGuardReload
        Show-TeamCommitReminder -Target $info.Name -FileName ($info.File | Split-Path -Leaf)
    }
}

function Invoke-Reload {
    Invoke-AdGuardReload
}

function Invoke-Help {
    param([string]$Sub)
    $script = $MyInvocation.PSCommandPath
    if (-not $script) { $script = $PSCommandPath }
    $help = Get-Help $script -Full -ErrorAction SilentlyContinue
    if ($help) { $help | Out-String | Write-Host; return }
    # Fallback — print the comment block at the top of this file
    $lines = Get-Content $PSCommandPath
    $inBlock = $false
    foreach ($l in $lines) {
        if ($l -match '^<#') { $inBlock = $true; continue }
        if ($l -match '^#>') { break }
        if ($inBlock) { Write-Host $l }
    }
}

# ── Dispatcher ───────────────────────────────────────────────────────────────

$verboseFlag = $false
$cleanRest = @()
if ($Rest) {
    foreach ($r in $Rest) {
        if ($r -eq '--verbose' -or $r -eq '-v') { $verboseFlag = $true }
        else { $cleanRest += $r }
    }
}

switch ($Subcommand) {
    'status'  { Invoke-Status -Verbose:$verboseFlag }
    'show'    { Invoke-Show   -InputArgs $cleanRest }
    'edit'    { Invoke-Edit   -InputArgs $cleanRest }
    'import'  { Invoke-Import -InputArgs $cleanRest }
    'add'     { Invoke-Add    -InputArgs $cleanRest }
    'reload'  { Invoke-Reload }
    'help'    { Invoke-Help -Sub ($cleanRest | Select-Object -First 1) }
    default {
        Write-Err "Unknown subcommand '$Subcommand'."
        Write-Host "Run: ./scripts/adguard/domain-policy.ps1 help" -ForegroundColor Yellow
        exit 2
    }
}
