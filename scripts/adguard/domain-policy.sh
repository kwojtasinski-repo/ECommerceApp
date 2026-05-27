#!/usr/bin/env bash
# ==============================================================================
#  domain-policy.sh — AdGuard DNS filter management CLI for ECommerceApp
#  Per ADR-0029: context-mode sandbox network egress policy
#
#  Bash parity for domain-policy.ps1. See that script's top comment block for
#  full design notes. This file replicates the same CLI surface for WSL/Linux
#  contributors.
# ==============================================================================
#
# USAGE
#   ./scripts/adguard/domain-policy.sh <subcommand> [args] [flags]
#
# TARGETS
#   blacklist   → docker/adguard/team-blacklist.txt  (block rules,    id=1001)
#   whitelist   → docker/adguard/team-whitelist.txt  (allow overrides,id=1002)
#
# SUBCOMMANDS
#   status [--verbose]                 Filter table from AdGuardHome.yaml
#   show <target|all> [--tail N] [--grep PATTERN]
#                                      Print contents
#   edit <target>                      Open in $EDITOR (fallback: code -w, vi)
#   import <target> <localfile>        Bulk append (dedup) + reload
#   add <target> <rule>                Single rule (dedup) + reload
#   reload                             docker compose restart adguard
#   help                               This message
#
# DESIGN
#   * Edits target HOST files (volume-mounted). No docker exec.
#   * Dedup: exact text match, case-sensitive, trim, skip comments (#, !).
#     No semantic dedup. No cross-file dedup (whitelist + blacklist is fine).
#   * Reload via `docker compose restart adguard` (~5s downtime).
#   * Never touches users:, DNS config, or container lifecycle beyond restart.
#   * Never auto-commits — prints a git commit hint instead.
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ADGUARD_DIR="$REPO_ROOT/docker/adguard"
BLACKLIST_FILE="$ADGUARD_DIR/team-blacklist.txt"
WHITELIST_FILE="$ADGUARD_DIR/team-whitelist.txt"
YAML_FILE="$ADGUARD_DIR/AdGuardHome.yaml"

# ── Color output ──────────────────────────────────────────────────────────────
if [ -t 1 ]; then
    C_GREEN='\033[0;32m'; C_CYAN='\033[0;36m'; C_YELLOW='\033[0;33m'
    C_RED='\033[0;31m'; C_GRAY='\033[0;90m'; C_RESET='\033[0m'
else
    C_GREEN=''; C_CYAN=''; C_YELLOW=''; C_RED=''; C_GRAY=''; C_RESET=''
fi
ok()    { printf "${C_GREEN}✓ %s${C_RESET}\n" "$1"; }
info()  { printf "${C_CYAN}ℹ %s${C_RESET}\n" "$1"; }
warn()  { printf "${C_YELLOW}⚠ %s${C_RESET}\n" "$1"; }
err()   { printf "${C_RED}✗ %s${C_RESET}\n" "$1" >&2; }

# ── Helpers ───────────────────────────────────────────────────────────────────
resolve_target() {
    case "$1" in
        blacklist) echo "$BLACKLIST_FILE" ;;
        whitelist) echo "$WHITELIST_FILE" ;;
        *) err "Unknown target '$1'. Valid: blacklist, whitelist."; exit 2 ;;
    esac
}

# Strip comments + blanks, trim. Reads stdin.
rule_lines() {
    grep -vE '^\s*([#!]|$)' | sed -E 's/^\s+|\s+$//g'
}

count_rules() {
    [ -f "$1" ] || { echo 0; return; }
    rule_lines < "$1" | wc -l | tr -d ' '
}

valid_rule() {
    local r="${1#"${1%%[![:space:]]*}"}"   # ltrim
    r="${r%"${r##*[![:space:]]}"}"          # rtrim
    [ -n "$r" ] || return 1
    case "$r" in '#'*|'!'*) return 1 ;; esac
    [[ "$r" =~ ^(@@)?(\|\|)?[a-zA-Z0-9.\-*?_]+\^?(\$[a-z=,~]*)?$ ]]
}

add_with_dedup() {
    local path="$1"; shift
    local -a new=("$@")
    [ -f "$path" ] || : > "$path"
    local added=0 skipped=0
    for rule in "${new[@]}"; do
        rule="$(echo "$rule" | sed -E 's/^\s+|\s+$//g')"
        [ -z "$rule" ] && continue
        case "$rule" in '#'*|'!'*) continue ;; esac
        if grep -Fxq -- "$rule" "$path" 2>/dev/null; then
            skipped=$((skipped + 1))
        else
            printf '%s\n' "$rule" >> "$path"
            added=$((added + 1))
        fi
    done
    if [ "$added" -eq 0 ]; then
        warn "No new rules added ($skipped already present)."
        return 1
    fi
    ok "Added $added new rule(s) to $(basename "$path"); $skipped already present."
    return 0
}

reload_adguard() {
    info "Reloading AdGuard (docker compose restart adguard)…"
    ( cd "$REPO_ROOT" && docker compose restart adguard >/dev/null )
    ok "AdGuard restarted."
}

commit_reminder() {
    local target="$1" file="$2"
    local branch="security/update-${target}-$(date +%Y%m%d-%H%M%S)"
    echo
    warn "$file modified (committed file)."
    printf "${C_YELLOW}  Share with the team:\n"
    printf "    git checkout -b %s\n" "$branch"
    printf "    git add docker/adguard/%s\n" "$file"
    printf "    git commit -m '<security|chore>(adguard): update %s'\n" "$target"
    printf "    git push origin %s\n" "$branch"
    printf "    gh pr create --title '<security|chore>(adguard): update %s'${C_RESET}\n" "$target"
}

pick_editor() {
    if [ -n "${EDITOR:-}" ]; then echo "$EDITOR"; return; fi
    if command -v code >/dev/null 2>&1; then echo "code -w"; return; fi
    if command -v vim  >/dev/null 2>&1; then echo "vim";    return; fi
    echo "vi"
}

# ── Subcommands ───────────────────────────────────────────────────────────────

cmd_status() {
    local verbose=0
    [ "${1:-}" = "--verbose" ] || [ "${1:-}" = "-v" ] && verbose=1

    echo
    printf "${C_CYAN}AdGuard filter state (from %s)${C_RESET}\n" "$YAML_FILE"
    printf '%s\n' "────────────────────────────────────────────────────────────────────────────────────────────────────"

    if [ ! -f "$YAML_FILE" ]; then
        warn "Could not read $YAML_FILE."
        return
    fi

    awk -v adir="$ADGUARD_DIR" '
        /^filters:/         { sec="filters";          next }
        /^whitelist_filters:/ { sec="whitelist_filters"; next }
        /^[a-z_]+:/         { sec=""; next }
        sec != "" && /^[ \t]*- enabled:/ {
            if (id != "") print id "\t" name "\t" en "\t" sec "\t" url
            id=""; name=""; url=""; en=""
            sub(/^[ \t]*- enabled:[ \t]*/, "", $0); en=$0
            next
        }
        sec != "" && /^[ \t]*url:/   { sub(/^[ \t]*url:[ \t]*/,   "", $0); url=$0  }
        sec != "" && /^[ \t]*name:/  { sub(/^[ \t]*name:[ \t]*/,  "", $0); name=$0 }
        sec != "" && /^[ \t]*id:/    { sub(/^[ \t]*id:[ \t]*/,    "", $0); id=$0   }
        END { if (id != "") print id "\t" name "\t" en "\t" sec "\t" url }
    ' "$YAML_FILE" | while IFS=$'\t' read -r id name en sec url; do
        local src rules
        case "$url" in
            /opt/*) src="file:$(basename "$url")"; rules="$(count_rules "$adir/$(basename "$url")")" ;;
            *)      src="url:${url#https://}"; src="${src#http://}"; rules="-" ;;
        esac
        printf "  %-5s %-50s %-7s %-18s %-40s %s\n" "$id" "$name" "$en" "$sec" "$src" "$rules"
    done

    if [ "$verbose" -eq 1 ]; then
        for t in blacklist whitelist; do
            local f; f="$(resolve_target "$t")"
            echo
            printf "${C_GRAY}── First 5 lines of %s (%s) ──${C_RESET}\n" "$t" "$(basename "$f")"
            [ -f "$f" ] && head -n 5 "$f" | sed "s/^/  /" || echo "  (file missing)"
        done
    fi
}

cmd_show() {
    [ $# -ge 1 ] || { err "Usage: show <target|all> [--tail N] [--grep PATTERN]"; exit 2; }
    local target="$1"; shift
    local tail=0 grep_pat=""
    while [ $# -gt 0 ]; do
        case "$1" in
            --tail) tail="$2"; shift 2 ;;
            --grep) grep_pat="$2"; shift 2 ;;
            *) shift ;;
        esac
    done
    local targets
    if [ "$target" = "all" ]; then targets="blacklist whitelist"; else targets="$target"; fi
    for t in $targets; do
        local f; f="$(resolve_target "$t")"
        echo
        printf "${C_CYAN}── %s (%s) ──${C_RESET}\n" "$t" "$(basename "$f")"
        [ -f "$f" ] || { warn "File missing: $f"; continue; }
        local content="$f"
        if [ -n "$grep_pat" ]; then
            if [ "$tail" -gt 0 ]; then grep -E "$grep_pat" "$f" | tail -n "$tail"
            else                       grep -E "$grep_pat" "$f"; fi
        else
            if [ "$tail" -gt 0 ]; then tail -n "$tail" "$f"
            else                       cat "$f"; fi
        fi
    done
}

cmd_edit() {
    [ $# -ge 1 ] || { err "Usage: edit <target>"; exit 2; }
    local f; f="$(resolve_target "$1")"
    [ -f "$f" ] || { warn "Creating $f"; : > "$f"; }
    local before; before="$(sha256sum "$f" | cut -d' ' -f1)"
    local ed; ed="$(pick_editor)"
    info "Opening $(basename "$f") in $ed…"
    # shellcheck disable=SC2086
    $ed "$f" || warn "Editor returned non-zero exit (continuing)."
    local after; after="$(sha256sum "$f" | cut -d' ' -f1)"
    if [ "$before" = "$after" ]; then info "No changes. Skipping reload."; return; fi
    reload_adguard
    commit_reminder "$1" "$(basename "$f")"
}

cmd_import() {
    [ $# -ge 2 ] || { err "Usage: import <target> <localfile>"; exit 2; }
    local f; f="$(resolve_target "$1")"
    local src="$2"
    [ -f "$src" ] || { err "Source file not found: $src"; exit 1; }
    local -a rules=()
    while IFS= read -r line; do rules+=("$line"); done < <(rule_lines < "$src")
    if [ "${#rules[@]}" -eq 0 ]; then warn "Source file has no rule lines."; return; fi
    if add_with_dedup "$f" "${rules[@]}"; then
        reload_adguard
        commit_reminder "$1" "$(basename "$f")"
    fi
}

cmd_add() {
    [ $# -ge 2 ] || { err "Usage: add <target> <rule>"; exit 2; }
    local f; f="$(resolve_target "$1")"
    local rule="$2"
    if ! valid_rule "$rule"; then
        err "Rule '$rule' is not a valid AdBlock-style filter."
        err "Expected: ||domain.com^, @@||domain.com^, or plain hostname."
        exit 2
    fi
    if add_with_dedup "$f" "$rule"; then
        reload_adguard
        commit_reminder "$1" "$(basename "$f")"
    fi
}

cmd_reload() { reload_adguard; }

cmd_help() {
    sed -n '/^# USAGE/,/^# ===/p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

# ── Dispatcher ────────────────────────────────────────────────────────────────
sub="${1:-help}"
shift || true
case "$sub" in
    status) cmd_status "$@" ;;
    show)   cmd_show   "$@" ;;
    edit)   cmd_edit   "$@" ;;
    import) cmd_import "$@" ;;
    add)    cmd_add    "$@" ;;
    reload) cmd_reload ;;
    help|-h|--help) cmd_help ;;
    *) err "Unknown subcommand '$sub'."; echo "Run: $0 help"; exit 2 ;;
esac
