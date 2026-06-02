#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/lib/common.sh"

AREA=""
ACTION=""
PROFILE=""
DOMAIN=""
PASSWORD=""
NO_MENU=0

usage(){
  cat <<EOF
Usage:
  bash scripts/operations-center.sh                      # menu mode
  bash scripts/operations-center.sh --no-menu --area rag --action create --profile dotnet-http
  bash scripts/operations-center.sh --no-menu --area contextmode --action add-whitelist --domain example.com

Areas: rag | contextmode
Actions (rag): create|update|force-update|health|profiles
Actions (contextmode): create|update|force-update|fix|health|add-whitelist|add-blacklist|change-password
Profiles: python-stdio|python-http|dotnet-stdio|dotnet-http
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --no-menu) NO_MENU=1; shift ;;
    --area) AREA="$2"; shift 2 ;;
    --action) ACTION="$2"; shift 2 ;;
    --profile) PROFILE="$2"; shift 2 ;;
    --domain) DOMAIN="$2"; shift 2 ;;
    --password) PASSWORD="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown arg: $1" >&2; usage; exit 2 ;;
  esac
done

run_action(){
  case "$AREA::$ACTION" in
    rag::create) rag_create "$PROFILE" ;;
    rag::update) rag_update "$PROFILE" ;;
    rag::force-update) rag_force_update "$PROFILE" ;;
    rag::health) rag_health ;;
    rag::profiles) show_profiles ;;

    contextmode::create) context_create "${PASSWORD:-ThiIS_StrongP4SSWORD!}" ;;
    contextmode::update) context_update ;;
    contextmode::force-update) context_force_update "${PASSWORD:-ThiIS_StrongP4SSWORD!}" ;;
    contextmode::fix) context_fix ;;
    contextmode::health) context_health ;;
    contextmode::add-whitelist) [ -n "$DOMAIN" ] || { echo "--domain required" >&2; exit 2; }; adguard_add_whitelist "$DOMAIN" ;;
    contextmode::add-blacklist) [ -n "$DOMAIN" ] || { echo "--domain required" >&2; exit 2; }; adguard_add_blacklist "$DOMAIN" ;;
    contextmode::change-password) [ -n "$PASSWORD" ] || { echo "--password required" >&2; exit 2; }; adguard_change_password "$PASSWORD" ;;
    *) echo "Unsupported area/action: $AREA / $ACTION" >&2; exit 2 ;;
  esac
}

select_profile(){
  echo "RAG profile:"
  echo "1) Python STDIO"
  echo "2) Python HTTP"
  echo "3) .NET STDIO"
  echo "4) .NET HTTP"
  read -r -p "Select profile [1-4]: " c
  case "$c" in
    1) echo "python-stdio" ;;
    2) echo "python-http" ;;
    3) echo "dotnet-stdio" ;;
    4) echo "dotnet-http" ;;
    *) echo "python-stdio" ;;
  esac
}

if [[ $NO_MENU -eq 1 || ( -n "$AREA" && -n "$ACTION" ) ]]; then
  [[ -n "$AREA" && -n "$ACTION" ]] || { echo "In non-interactive mode provide --area and --action" >&2; exit 2; }
  run_action
  exit 0
fi

continueOperations=1
while [[ $continueOperations -eq 1 ]]; do
  echo
  echo "ECommerce Operations Center"
  echo "1) RAG"
  echo "2) ContextMode"
  echo "3) Exit"
  read -r -p "Choose area [1-3]: " c
  case "$c" in
    1)
      while true; do
        echo
        echo "RAG Menu"
        echo "1) Create environment"
        echo "2) Update environment"
        echo "3) Force Update"
        echo "4) Run ingest now"
        echo "5) RAG health checks"
        echo "6) Back"
        read -r -p "Choose option [1-6]: " rc
        case "$rc" in
          1) p="$(select_profile)"; rag_create "$p" ;;
          2) p="$(select_profile)"; rag_update "$p" ;;
          3) p="$(select_profile)"; rag_force_update "$p" ;;
          4)
             p="$(select_profile)"
             [[ "$p" == python-* ]] && ensure_rag_python_stats_file && run_repo "docker compose --profile rag run --rm rag-tools python ingest.py"
             [[ "$p" == dotnet-* ]] && ensure_rag_dotnet_stats_file && run_repo "docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll"
             ;;
          5) rag_health ;;
          6) break ;;
          *) warn "Choose 1-6" ;;
        esac
      done
      ;;
    2)
      while true; do
        echo
        echo "ContextMode Menu"
        echo "1) Create environment"
        echo "2) Update environment"
        echo "3) Force Update"
        echo "4) Fix Context Mode"
        echo "5) Add whitelist domain"
        echo "6) Add blacklist domain"
        echo "7) Change AdGuard password"
        echo "8) ContextMode health checks"
        echo "9) Back"
        read -r -p "Choose option [1-9]: " cc
        case "$cc" in
          1) context_create "ThiIS_StrongP4SSWORD!" ;;
          2) context_update ;;
          3) context_force_update "ThiIS_StrongP4SSWORD!" ;;
          4) context_fix ;;
          5) read -r -p "Domain (example.com): " d; adguard_add_whitelist "$d" ;;
          6) read -r -p "Domain (example.com): " d; adguard_add_blacklist "$d" ;;
          7) read -r -p "New AdGuard password: " p; adguard_change_password "$p" ;;
          8) context_health ;;
          9) break ;;
          *) warn "Choose 1-9" ;;
        esac
      done
      ;;
    3) continueOperations=0 ;;
    *) warn "Choose 1-3" ;;
  esac
done
