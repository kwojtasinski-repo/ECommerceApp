<#
.SYNOPSIS
    Fixes missing checklist.md and migration-plan.md for Tier 2 and Tier 3 ADR folders.
    Run this after restructure-adrs.ps1 if those files are missing.

.EXAMPLE
    cd C:\Projekty\DotNet\ECommerceApp
    .\tools\fix-adr-missing-sections.ps1
#>

param([string]$RepoRoot = "C:\Projekty\DotNet\ECommerceApp")

$adrBase = Join-Path $RepoRoot "docs\adr"
$ErrorActionPreference = "Stop"

function OK   { param($m) Write-Host "  + $m" -ForegroundColor Green  }
function Warn { param($m) Write-Host "  ! $m" -ForegroundColor Yellow }
function Step { param($m) Write-Host "`n>> $m" -ForegroundColor Magenta }

function Write-UTF8([string]$path, [string]$content) {
    $dir = Split-Path $path -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllText($path, $content.TrimEnd() + "`n", [System.Text.Encoding]::UTF8)
    OK $path.Replace($RepoRoot, "").TrimStart("\")
}

function Get-Section([string[]]$lines, [string]$headingRegex) {
    $start = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match $headingRegex) { $start = $i; break }
    }
    if ($start -eq -1) { return $null }
    $lvl = ([regex]::Match($lines[$start], '^(#{1,6})\s').Groups[1].Value).Length
    $end  = $lines.Count
    for ($i = $start + 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^#{1,$lvl} ") { $end = $i; break }
    }
    return ($lines[$start..($end - 1)] -join "`n").Trim()
}

function Remove-Sections([string[]]$lines, [string[]]$headingRegexes) {
    $out = [System.Collections.Generic.List[string]]::new()
    $i = 0
    while ($i -lt $lines.Count) {
        $matched = $false
        foreach ($rx in $headingRegexes) {
            if ($lines[$i] -match $rx) {
                $lvl = ([regex]::Match($lines[$i], '^(#{1,6})\s').Groups[1].Value).Length
                $i++
                while ($i -lt $lines.Count -and $lines[$i] -notmatch "^#{1,$lvl} ") { $i++ }
                $matched = $true; break
            }
        }
        if (-not $matched) { $out.Add($lines[$i]); $i++ }
    }
    return $out.ToArray()
}

function Trim-Lines([string[]]$lines) {
    $end = $lines.Count - 1
    while ($end -ge 0 -and [string]::IsNullOrWhiteSpace($lines[$end])) { $end-- }
    if ($end -lt 0) { return @() }
    return $lines[0..$end]
}

# Extract and strip sections from an ADR main file
function Fix-Adr {
    param(
        [string]$adrFolder,
        [bool]  $extractChecklist,
        [bool]  $extractMigration
    )

    $mainFile = Get-ChildItem $adrFolder -Filter "0*.md" | Select-Object -First 1
    if (-not $mainFile) { Warn "No main file in $adrFolder"; return }

    $lines = [System.IO.File]::ReadAllLines($mainFile.FullName, [System.Text.Encoding]::UTF8)
    $stripRegexes = [System.Collections.Generic.List[string]]::new()

    if ($extractChecklist) {
        $ckPath = Join-Path $adrFolder "checklist.md"
        if (-not (Test-Path $ckPath)) {
            $section = Get-Section $lines '^## Conformance checklist'
            if ($section) {
                Write-UTF8 $ckPath $section
                $stripRegexes.Add('^## Conformance checklist')
            } else { Warn "Checklist not found in $($mainFile.Name)" }
        } else {
            # Already exists - still strip from main
            $stripRegexes.Add('^## Conformance checklist')
        }
    }

    if ($extractMigration) {
        $mpPath = Join-Path $adrFolder "migration-plan.md"
        if (-not (Test-Path $mpPath)) {
            $section = Get-Section $lines '^## Migration plan'
            if ($section) {
                Write-UTF8 $mpPath $section
                $stripRegexes.Add('^## Migration plan')
            } else { Warn "Migration plan not found in $($mainFile.Name)" }
        } else {
            # Already exists - still strip from main
            $stripRegexes.Add('^## Migration plan')
        }
    }

    if ($stripRegexes.Count -gt 0) {
        $cleaned = Remove-Sections $lines $stripRegexes.ToArray()
        $cleaned = Trim-Lines $cleaned
        [System.IO.File]::WriteAllLines($mainFile.FullName, $cleaned, [System.Text.Encoding]::UTF8)
        OK "Stripped from $($mainFile.Name): $($stripRegexes -join ', ')"
    }
}

# ── Tier 2 - checklist + migration plan both missing ────────────────────────
Step "Tier 2 - extracting checklist + migration plan"

@("0003","0006","0007","0008","0013","0022","0023","0024") | ForEach-Object {
    Write-Host "  ADR-$_"
    Fix-Adr -adrFolder "$adrBase\$_" -extractChecklist $true -extractMigration $true
}

# 0021 - has checklist (was misclassified as check=False), has migration plan
Write-Host "  ADR-0021"
Fix-Adr -adrFolder "$adrBase\0021" -extractChecklist $true -extractMigration $true

# ── Tier 3 - Move-Simple skipped extraction - now fix ───────────────────────
Step "Tier 3 - extracting checklist + migration plan"

# These have both
@("0001","0002","0004","0005","0018","0019","0020") | ForEach-Object {
    Write-Host "  ADR-$_"
    Fix-Adr -adrFolder "$adrBase\$_" -extractChecklist $true -extractMigration $true
}

# 0025 - has migration plan, no checklist
Write-Host "  ADR-0025 (migration plan only)"
Fix-Adr -adrFolder "$adrBase\0025" -extractChecklist $false -extractMigration $true

# 0026 - clean already
Write-Host "  ADR-0026 - skipped (already clean)"

# ── Final verification ───────────────────────────────────────────────────────
Write-Host "`n>> Verification" -ForegroundColor Magenta
$remaining = Get-ChildItem $adrBase -Recurse -Filter "*.md" |
    Where-Object { $_.Name -match "^0\d{3}-" } |
    ForEach-Object {
        Select-String $_.FullName -Pattern "^## Implementation Status|^## Conformance checklist|^## Migration plan" |
        ForEach-Object { "$($_.Path.Replace($RepoRoot,'').TrimStart('\')) L$($_.LineNumber): $($_.Line)" }
    }

if ($remaining) {
    Write-Host "  Still remaining:" -ForegroundColor Yellow
    $remaining | ForEach-Object { Write-Host "    $_" -ForegroundColor Yellow }
} else {
    Write-Host "  All noise sections removed from main ADR files." -ForegroundColor Green
}
