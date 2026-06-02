#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

step(){ printf "\033[0;36m-> %s\033[0m\n" "$*"; }
ok(){ printf "\033[0;32mOK  %s\033[0m\n" "$*"; }
warn(){ printf "\033[0;33m!   %s\033[0m\n" "$*"; }

run_repo(){
  step "$*"
  ( cd "$REPO_ROOT" && eval "$*" )
}

assert_docker(){
  docker ps >/dev/null 2>&1 || { echo "Docker is not ready" >&2; exit 1; }
}

is_valid_profile(){
  case "$1" in
    python-stdio|python-http|dotnet-stdio|dotnet-http|"") return 0 ;;
    *) return 1 ;;
  esac
}

ensure_rag_dotnet_stats_file(){
  mkdir -p "$REPO_ROOT/.rag"
  [ -f "$REPO_ROOT/.rag/index-stats-dotnet.md" ] || printf '# RAG Index Stats\n' > "$REPO_ROOT/.rag/index-stats-dotnet.md"
}

ensure_rag_python_stats_file(){
  mkdir -p "$REPO_ROOT/.rag"
  [ -f "$REPO_ROOT/.rag/index-stats.md" ] || printf '# RAG Index Stats\n' > "$REPO_ROOT/.rag/index-stats.md"
}

ensure_adguard_policy_files(){
  local wl="$REPO_ROOT/docker/adguard/team-whitelist.txt"
  local bl="$REPO_ROOT/docker/adguard/team-blacklist.txt"
  [ -f "$wl" ] || { printf '# Team whitelist (allow overrides)\n' > "$wl"; }
  [ -f "$bl" ] || { printf '# Team blacklist (block rules)\n' > "$bl"; }
  if ! grep -Fxq '||*^' "$bl"; then
    printf '||*^\n' >> "$bl"
    warn 'Added strict deny-all baseline rule to team-blacklist.txt: ||*^'
  fi
}

rag_create(){
  local profile="${1:-}"
  is_valid_profile "$profile" || { echo "Invalid profile: $profile" >&2; exit 2; }
  assert_docker
  run_repo "docker compose --profile rag --profile rag-dotnet --profile rag-python-http --profile rag-dotnet-http up -d qdrant"
  run_repo "docker compose build rag-tools"
  run_repo "docker compose build rag-dotnet"
  case "$profile" in
    ""|python-*) ensure_rag_python_stats_file; run_repo "docker compose --profile rag run --rm rag-tools python ingest.py" ;;
  esac
  case "$profile" in
    ""|dotnet-*) ensure_rag_dotnet_stats_file; run_repo "docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll" ;;
  esac
  [ "$profile" = "python-http" ] && run_repo "docker compose --profile rag-python-http up -d rag-python-http"
  [ "$profile" = "dotnet-http" ] && run_repo "docker compose --profile rag-dotnet-http up -d rag-dotnet-http"
  ok "RAG create completed"
}

rag_update(){
  local profile="${1:-}"
  is_valid_profile "$profile" || { echo "Invalid profile: $profile" >&2; exit 2; }
  assert_docker
  run_repo "docker compose --profile rag --profile rag-dotnet --profile rag-python-http --profile rag-dotnet-http up -d qdrant"
  case "$profile" in
    ""|python-*) ensure_rag_python_stats_file; run_repo "docker compose --profile rag run --rm rag-tools python ingest.py" ;;
  esac
  case "$profile" in
    ""|dotnet-*) ensure_rag_dotnet_stats_file; run_repo "docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll" ;;
  esac
  [ "$profile" = "python-http" ] && run_repo "docker compose --profile rag-python-http up -d rag-python-http"
  [ "$profile" = "dotnet-http" ] && run_repo "docker compose --profile rag-dotnet-http up -d rag-dotnet-http"
  ok "RAG update completed"
}

rag_force_update(){
  local profile="${1:-}"
  is_valid_profile "$profile" || { echo "Invalid profile: $profile" >&2; exit 2; }
  assert_docker
  run_repo "docker compose build --no-cache rag-tools"
  run_repo "docker compose build --no-cache rag-dotnet"
  run_repo "docker compose --profile rag --profile rag-dotnet --profile rag-python-http --profile rag-dotnet-http up -d --force-recreate qdrant"
  case "$profile" in
    ""|python-*) ensure_rag_python_stats_file; run_repo "docker compose --profile rag run --rm rag-tools python ingest.py --force-full" ;;
  esac
  case "$profile" in
    ""|dotnet-*) ensure_rag_dotnet_stats_file; run_repo "docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll --force-full" ;;
  esac
  [ "$profile" = "python-http" ] && run_repo "docker compose --profile rag-python-http up -d --force-recreate rag-python-http"
  [ "$profile" = "dotnet-http" ] && run_repo "docker compose --profile rag-dotnet-http up -d --force-recreate rag-dotnet-http"
  ok "RAG force update completed"
}

rag_health(){
  assert_docker
  run_repo "docker ps --format 'table {{.Names}}\t{{.Status}}'"
  run_repo "docker logs --tail 20 ecommerceapp-rag-dotnet-http-1 || true"
  run_repo "docker logs --tail 20 ecommerceapp-rag-python-http-1 || true"
}

context_create(){
  local password="${1:-ThiIS_StrongP4SSWORD!}"
  assert_docker
  ensure_adguard_policy_files
  run_repo "FORCE_REGENERATE=1 ADGUARD_PASSWORD='$password' bash scripts/context-mode-bootstrap.sh"
  ok "ContextMode create completed"
}

context_update(){
  assert_docker
  ensure_adguard_policy_files
  run_repo "docker compose --profile monitoring --profile context-mode build context-mode"
  run_repo "docker compose --profile monitoring --profile context-mode up -d --force-recreate adguard context-mode"
  ok "ContextMode update completed"
}

context_force_update(){
  local password="${1:-ThiIS_StrongP4SSWORD!}"
  assert_docker
  ensure_adguard_policy_files
  run_repo "docker compose --profile monitoring --profile context-mode build --no-cache context-mode"
  run_repo "docker compose --profile monitoring --profile context-mode up -d --force-recreate adguard context-mode"
  run_repo "FORCE_REGENERATE=1 SKIP_BUILD=1 ADGUARD_PASSWORD='$password' bash scripts/context-mode-bootstrap.sh"
  ok "ContextMode force update completed"
}

context_fix(){
  assert_docker
  run_repo "docker compose --profile monitoring --profile context-mode up -d adguard context-mode"
  run_repo "bash scripts/test-mcp-handshake.sh"
  run_repo "bash scripts/test-ctx-doctor.sh"
  run_repo "docker logs --tail 25 ecommerceapp-context-mode"
  ok "ContextMode fix sequence completed"
}

context_health(){
  assert_docker
  run_repo "docker inspect ecommerceapp-context-mode --format 'Health={{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}} Running={{.State.Running}}'"
  run_repo "docker logs --tail 25 ecommerceapp-context-mode"
}

adguard_add_whitelist(){
  local d="$1"
  assert_docker
  run_repo "bash scripts/adguard/domain-policy.sh add whitelist '@@||$d^'"
}

adguard_add_blacklist(){
  local d="$1"
  assert_docker
  run_repo "bash scripts/adguard/domain-policy.sh add blacklist '||$d^'"
}

adguard_change_password(){
  local password="$1"
  assert_docker
  run_repo "FORCE_REGENERATE=1 SKIP_BUILD=1 ADGUARD_PASSWORD='$password' bash scripts/context-mode-bootstrap.sh"
  ok "AdGuard password rotated"
}

show_profiles(){
  cat <<EOF
Available RAG profiles:
 - python-stdio
 - python-http
 - dotnet-stdio
 - dotnet-http
EOF
}
