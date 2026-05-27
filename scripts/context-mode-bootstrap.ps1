#!/usr/bin/env pwsh
<#
.SYNOPSIS
    One-shot bootstrap of the context-mode + AdGuard sandbox stack.
    Replaces the manual wizard at http://127.0.0.1:3000.

.DESCRIPTION
    Idempotent. Re-running only fills in what is missing.

    Steps:
      1. Create + chown the named docker volume for context-mode's session DB
      2. Generate AdGuard config (docker/adguard/AdGuardHome.yaml) with a
         bcrypt admin password — skips the first-run wizard
      3. Build the context-mode image (if missing)
      4. Start adguard + context-mode (recreate to pick up config + healthcheck)
      5. Wait for AdGuard :53 listener
      6. Run G.1 / G.2 / G.3 verification commands

.PARAMETER AdGuardUser
    Admin username for AdGuard UI. Default: admin

.PARAMETER AdGuardPassword
    Admin password. If omitted, a 24-char random password is generated and
    printed once. Stored only as a bcrypt hash inside AdGuardHome.yaml.

.PARAMETER ForceRegenerateAdGuard
    Overwrite an existing AdGuardHome.yaml (e.g. after rotating the password).

.PARAMETER SkipBuild
    Skip the docker compose build context-mode step (faster reruns).

.EXAMPLE
    pwsh ./scripts/context-mode-bootstrap.ps1

.EXAMPLE
    pwsh ./scripts/context-mode-bootstrap.ps1 -AdGuardPassword 'MyStrongPass123!'
#>
[CmdletBinding()]
param(
    [string]$AdGuardUser = 'admin',
    [string]$AdGuardPassword,
    [switch]$ForceRegenerateAdGuard,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Write-Step($msg) { Write-Host "-> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "OK  $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "!   $msg" -ForegroundColor Yellow }
function Write-Fail($msg) { Write-Host "XX  $msg" -ForegroundColor Red }

# --- 1. Volume for context-mode session DB ------------------------------------
$volume = 'ecommerceapp_context-mode-data'
Write-Step "Ensuring docker volume '$volume' exists and is owned by UID 1000..."
docker volume create $volume | Out-Null
docker run --rm -v "${volume}:/data" alpine sh -c `
    "mkdir -p /data/sessions /data/content && chown -R 1000:1000 /data" | Out-Null
$check = docker run --rm --user 1000:1000 -v "${volume}:/data" alpine sh -c `
    "touch /data/.bootstrap-check && rm /data/.bootstrap-check && echo OK"
if ($check.Trim() -ne 'OK') { Write-Fail "Volume chown failed."; exit 1 }
Write-Ok "Volume '$volume' ready."

# --- 2. AdGuard config (skip first-run wizard) --------------------------------
$adguardConf = Join-Path $repoRoot 'docker/adguard/AdGuardHome.yaml'
$adguardTpl  = Join-Path $repoRoot 'docker/adguard/AdGuardHome.yaml.template'

if ((Test-Path $adguardConf) -and -not $ForceRegenerateAdGuard) {
    Write-Ok "AdGuardHome.yaml already exists - skipping (use -ForceRegenerateAdGuard to overwrite)."
} else {
    if (-not (Test-Path $adguardTpl)) {
        Write-Fail "Template missing: $adguardTpl"; exit 1
    }
    if (-not $AdGuardPassword) {
        Add-Type -AssemblyName System.Web
        $AdGuardPassword = [System.Web.Security.Membership]::GeneratePassword(24, 4)
        Write-Warn "Generated AdGuard password (printed once - store in your password manager):"
        Write-Host "  $AdGuardPassword" -ForegroundColor Magenta
    }

    Write-Step "Computing bcrypt hash via httpd:alpine..."
    $htOut = docker run --rm httpd:alpine htpasswd -nbBC 10 $AdGuardUser $AdGuardPassword
    if (-not $htOut) { Write-Fail "htpasswd produced no output."; exit 1 }
    $hash = ($htOut -split ':', 2)[1].Trim()
    if (-not $hash.StartsWith('$2')) { Write-Fail "Unexpected bcrypt format: $htOut"; exit 1 }

    Write-Step "Writing $adguardConf..."
    $yaml = (Get-Content $adguardTpl -Raw) `
        -replace '\$\{ADMIN_USER\}', $AdGuardUser `
        -replace '\$\{PASSWORD_HASH\}', [regex]::Escape($hash).Replace('\', '\\')
    # Simpler: do a literal token replace instead of regex
    $yaml = (Get-Content $adguardTpl -Raw)
    $yaml = $yaml.Replace('${ADMIN_USER}', $AdGuardUser).Replace('${PASSWORD_HASH}', $hash)
    [System.IO.File]::WriteAllText($adguardConf, $yaml)
    Write-Ok "AdGuardHome.yaml written (user='$AdGuardUser', bcrypt hash applied)."
}

# --- 3. Build context-mode image ----------------------------------------------
if (-not $SkipBuild) {
    Write-Step "Building context-mode image..."
    docker compose --profile context-mode build context-mode | Out-Null
    Write-Ok "Image built."
} else {
    Write-Warn "Skipping image build (-SkipBuild)."
}

# --- 4. Start (recreate) adguard + context-mode -------------------------------
Write-Step "Starting / recreating adguard + context-mode..."
docker compose --profile monitoring --profile context-mode up -d --force-recreate adguard context-mode | Out-Null
Write-Ok "Containers up."

# --- 5. Wait for AdGuard :53 listener (max 30s) -------------------------------
Write-Step "Waiting for AdGuard DNS on :53 (max 30s)..."
$ready = $false
for ($i = 0; $i -lt 30; $i++) {
    $listener = docker exec ecommerceapp-adguard sh -c "netstat -ln 2>/dev/null | grep ':53 ' || true" 2>$null
    if ($listener) { $ready = $true; break }
    Start-Sleep -Seconds 1
}
if ($ready) { Write-Ok "DNS :53 listener active." }
else        { Write-Fail "DNS :53 did NOT come up in 30s. Check: docker logs ecommerceapp-adguard" }

# --- 6. Gate verification (G.1 / G.2 / G.3) -----------------------------------
Write-Host ""
Write-Host "--- Gate verification ---" -ForegroundColor Cyan

# G.1
$g1 = docker exec ecommerceapp-adguard ls /opt/adguardhome/conf 2>$null
if ($g1 -match 'AdGuardHome\.yaml') { Write-Ok "G.1 AdGuardHome.yaml present" }
else { Write-Fail "G.1 AdGuardHome.yaml MISSING" }

# G.2
$g2 = docker exec ecommerceapp-adguard sh -c "netstat -ln 2>/dev/null | grep ':53 '" 2>$null
if ($g2) { Write-Ok "G.2 :53 listener present" }
else     { Write-Fail "G.2 :53 listener MISSING" }

# G.3
$g3 = docker exec ecommerceapp-context-mode nslookup raw.githubusercontent.com 172.28.0.2 2>&1
if ($g3 -match 'Address') { Write-Ok "G.3 sandbox can resolve raw.githubusercontent.com" }
else { Write-Fail "G.3 DNS resolution FAILED:`n$g3" }

# --- 7. Healthcheck status (informational) ------------------------------------
Start-Sleep -Seconds 5
$health = docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' ecommerceapp-context-mode 2>$null
Write-Host ""
Write-Host "context-mode healthcheck status: $health" -ForegroundColor Cyan

Write-Host ""
Write-Host "Bootstrap complete." -ForegroundColor Green
Write-Host ""
Write-Host "---  NEXT STEP: enable the MCP server in VS Code  ---" -ForegroundColor Yellow
Write-Host "  1. Open the repository:    code ."
Write-Host "  2. Open Copilot Chat:      Ctrl+Alt+I  (or sidebar Copilot icon)"
Write-Host "  3. Click the MCP tab at the top of the chat panel"
Write-Host "  4. Toggle ON:              ecommerceapp-context-mode"
Write-Host "  5. Wait ~2s - should show as 'Started' with 11 tools"
Write-Host ""
Write-Host "---  References  ---" -ForegroundColor Yellow
Write-Host "  UI:          http://127.0.0.1:3000  (login: $AdGuardUser)"
Write-Host "  MCP probe:   powershell -File scripts/test-mcp-handshake.ps1"
Write-Host "  Full guide:  docs/getting-started-context-mode.md"
Write-Host "  KI / FAQ:    .github/context/known-issues.md (KI-014)"
