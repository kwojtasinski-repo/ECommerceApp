#!/usr/bin/env bash
# One-shot bootstrap of the context-mode + AdGuard sandbox stack.
# Cross-platform companion to scripts/context-mode-bootstrap.ps1 (macOS/Linux).
#
# Replaces the manual wizard at http://127.0.0.1:3000.
# Idempotent: re-running only fills in what is missing.
#
# Usage:
#   ./scripts/context-mode-bootstrap.sh
#   ADGUARD_PASSWORD='MyPass!' ./scripts/context-mode-bootstrap.sh
#   FORCE_REGENERATE=1 ./scripts/context-mode-bootstrap.sh
#   SKIP_BUILD=1 ./scripts/context-mode-bootstrap.sh
#
# Env vars:
#   ADGUARD_USER       (default: admin)
#   ADGUARD_PASSWORD   (default: auto-generated 24-char random; printed once)
#   FORCE_REGENERATE   1 = overwrite existing AdGuardHome.yaml
#   SKIP_BUILD         1 = skip `docker compose build`

set -euo pipefail

readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &> /dev/null && pwd)"
readonly REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." &> /dev/null && pwd)"
cd "$REPO_ROOT"

readonly VOLUME='ecommerceapp_context-mode-data'
readonly ADGUARD_USER="${ADGUARD_USER:-admin}"
readonly ADGUARD_CONF="docker/adguard/AdGuardHome.yaml"
readonly ADGUARD_TPL="docker/adguard/AdGuardHome.yaml.template"

# Color helpers
if [[ -t 1 ]]; then
    C_CYAN='\033[0;36m'; C_GREEN='\033[0;32m'; C_YELLOW='\033[0;33m'
    C_RED='\033[0;31m'; C_MAGENTA='\033[0;35m'; C_RESET='\033[0m'
else
    C_CYAN=''; C_GREEN=''; C_YELLOW=''; C_RED=''; C_MAGENTA=''; C_RESET=''
fi
log_step() { printf "${C_CYAN}-> %s${C_RESET}\n" "$*"; }
log_ok()   { printf "${C_GREEN}OK  %s${C_RESET}\n" "$*"; }
log_warn() { printf "${C_YELLOW}!   %s${C_RESET}\n" "$*"; }
log_fail() { printf "${C_RED}XX  %s${C_RESET}\n" "$*"; }

# --- 1. Volume for context-mode session DB ------------------------------------
log_step "Ensuring docker volume '$VOLUME' exists and is owned by UID 1000..."
docker volume create "$VOLUME" > /dev/null
docker run --rm -v "${VOLUME}:/data" alpine sh -c \
    "mkdir -p /data/sessions /data/content && chown -R 1000:1000 /data" > /dev/null
check=$(docker run --rm --user 1000:1000 -v "${VOLUME}:/data" alpine sh -c \
    "touch /data/.bootstrap-check && rm /data/.bootstrap-check && echo OK")
if [[ "$check" != "OK" ]]; then
    log_fail "Volume chown failed."; exit 1
fi
log_ok "Volume '$VOLUME' ready."

# --- 2. AdGuard config (skip first-run wizard) --------------------------------
if [[ -f "$ADGUARD_CONF" && "${FORCE_REGENERATE:-0}" != "1" ]]; then
    log_ok "AdGuardHome.yaml already exists - skipping (set FORCE_REGENERATE=1 to overwrite)."
else
    if [[ ! -f "$ADGUARD_TPL" ]]; then
        log_fail "Template missing: $ADGUARD_TPL"; exit 1
    fi
    if [[ -z "${ADGUARD_PASSWORD:-}" ]]; then
        ADGUARD_PASSWORD=$(LC_ALL=C tr -dc 'A-Za-z0-9!@#$%^&*' < /dev/urandom | head -c 24)
        log_warn "Generated AdGuard password (printed once - store in your password manager):"
        printf "  ${C_MAGENTA}%s${C_RESET}\n" "$ADGUARD_PASSWORD"
    fi
    log_step "Computing bcrypt hash via httpd:alpine..."
    ht_out=$(docker run --rm httpd:alpine htpasswd -nbBC 10 "$ADGUARD_USER" "$ADGUARD_PASSWORD")
    if [[ -z "$ht_out" ]]; then log_fail "htpasswd produced no output."; exit 1; fi
    hash="${ht_out#*:}"
    hash="${hash%$'\r'}"
    hash="${hash%$'\n'}"
    if [[ "$hash" != \$2* ]]; then log_fail "Unexpected bcrypt format: $ht_out"; exit 1; fi
    log_step "Writing $ADGUARD_CONF..."
    # Use python or sed for placeholder substitution — sed avoids extra deps
    # Escape forward slashes and the literal $ in the hash for sed
    esc_hash=$(printf '%s' "$hash" | sed -e 's/[\/&]/\\&/g')
    sed -e "s|\${ADMIN_USER}|${ADGUARD_USER}|g" \
        -e "s|\${PASSWORD_HASH}|${esc_hash}|g" \
        "$ADGUARD_TPL" > "$ADGUARD_CONF"
    log_ok "AdGuardHome.yaml written (user='$ADGUARD_USER', bcrypt hash applied)."
fi

# --- 3. Build context-mode image ----------------------------------------------
if [[ "${SKIP_BUILD:-0}" != "1" ]]; then
    log_step "Building context-mode image..."
    docker compose --profile context-mode build context-mode > /dev/null
    log_ok "Image built."
else
    log_warn "Skipping image build (SKIP_BUILD=1)."
fi

# --- 4. Start (recreate) adguard + context-mode -------------------------------
log_step "Starting / recreating adguard + context-mode..."
docker compose --profile monitoring --profile context-mode up -d --force-recreate adguard context-mode > /dev/null
log_ok "Containers up."

# --- 5. Wait for AdGuard :53 listener (max 30s) -------------------------------
log_step "Waiting for AdGuard DNS on :53 (max 30s)..."
ready=0
for _ in $(seq 1 30); do
    if docker exec ecommerceapp-adguard sh -c "netstat -ln 2>/dev/null | grep ':53 '" > /dev/null 2>&1; then
        ready=1; break
    fi
    sleep 1
done
if [[ $ready -eq 1 ]]; then
    log_ok "DNS :53 listener active."
else
    log_fail "DNS :53 did NOT come up in 30s. Check: docker logs ecommerceapp-adguard"
fi

# --- 6. Gate verification (G.1 / G.2 / G.3) -----------------------------------
echo ""
printf "${C_CYAN}--- Gate verification ---${C_RESET}\n"

# G.1
g1=$(docker exec ecommerceapp-adguard ls /opt/adguardhome/conf 2>/dev/null || true)
if echo "$g1" | grep -q 'AdGuardHome.yaml'; then
    log_ok "G.1 AdGuardHome.yaml present"
else
    log_fail "G.1 AdGuardHome.yaml MISSING"
fi

# G.2
if docker exec ecommerceapp-adguard sh -c "netstat -ln 2>/dev/null | grep ':53 '" > /dev/null 2>&1; then
    log_ok "G.2 :53 listener present"
else
    log_fail "G.2 :53 listener MISSING"
fi

# G.3
g3=$(docker exec ecommerceapp-context-mode nslookup raw.githubusercontent.com 172.28.0.2 2>&1 || true)
if echo "$g3" | grep -q 'Address'; then
    log_ok "G.3 sandbox can resolve raw.githubusercontent.com"
else
    log_fail "G.3 DNS resolution FAILED:"
    echo "$g3"
fi

# --- 7. Healthcheck status (informational) ------------------------------------
sleep 5
health=$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' ecommerceapp-context-mode 2>/dev/null || echo "unknown")
echo ""
printf "${C_CYAN}context-mode healthcheck status: %s${C_RESET}\n" "$health"

echo ""
printf "${C_GREEN}Bootstrap complete.${C_RESET}\n"
echo ""
printf "${C_YELLOW}---  NEXT STEP: enable the MCP server in VS Code  ---${C_RESET}\n"
echo "  1. Open the repository:    code ."
echo "  2. Open Copilot Chat:      Ctrl+Alt+I  (or sidebar Copilot icon)"
echo "  3. Click the MCP tab at the top of the chat panel"
echo "  4. Toggle ON:              ecommerceapp-context-mode"
echo "  5. Wait ~2s — should show as 'Started' with 11 tools"
echo ""
printf "${C_YELLOW}---  References  ---${C_RESET}\n"
echo "  UI:          http://127.0.0.1:3000  (login: $ADGUARD_USER)"
echo "  MCP probe:   bash scripts/test-mcp-handshake.sh   # or .ps1 on Windows"
echo "  Full guide:  docs/getting-started-context-mode.md"
echo "  KI / FAQ:    .github/context/known-issues.md (KI-014)"
