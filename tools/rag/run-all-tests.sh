#!/usr/bin/env bash
# Run ALL RAG tests — Python (unit + E2E) and .NET — in sequence.
#
# Cross-platform companion to run-all-tests.ps1 (macOS / Linux).
# Master test runner. Starts Qdrant once, runs Python tests, then .NET tests,
# then stops Qdrant. Reports a combined exit code.
#
# USAGE
#   bash tools/rag/run-all-tests.sh [options]
#
# OPTIONS
#   --skip-python    Skip the Python test suite entirely.
#   --skip-dotnet    Skip the .NET test suite entirely.
#   --keep-qdrant    Do not stop Qdrant after tests (useful when shared).
#   --verbose        Pass verbose flags to both pytest and dotnet test.
#
# EXAMPLES
#   # Full run — starts and stops Qdrant automatically
#   bash tools/rag/run-all-tests.sh
#
#   # Skip .NET (e.g., model not downloaded yet)
#   bash tools/rag/run-all-tests.sh --skip-dotnet
#
#   # Keep Qdrant running after tests
#   bash tools/rag/run-all-tests.sh --keep-qdrant
#
#   # Verbose output
#   bash tools/rag/run-all-tests.sh --verbose

set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
PYTHON_TESTS="$REPO_ROOT/tools/rag/run-tests.sh"
DOTNET_TESTS="$REPO_ROOT/tools/rag-dotnet/run-tests.sh"

# ── Flags ─────────────────────────────────────────────────────────────────────
SKIP_PYTHON=0; SKIP_DOTNET=0; KEEP_QDRANT=0; VERBOSE=0
for arg in "$@"; do
  case "$arg" in
    --skip-python) SKIP_PYTHON=1 ;;
    --skip-dotnet) SKIP_DOTNET=1 ;;
    --keep-qdrant) KEEP_QDRANT=1 ;;
    --verbose)     VERBOSE=1 ;;
    *) echo "Unknown argument: $arg"; exit 1 ;;
  esac
done

# ── Color helpers ─────────────────────────────────────────────────────────────
if [[ -t 1 ]]; then
  C_YELLOW='\033[0;33m'; C_GREEN='\033[0;32m'; C_RED='\033[0;31m'
  C_MAGENTA='\033[0;35m'; C_CYAN='\033[0;36m'; C_GRAY='\033[0;90m'; C_RESET='\033[0m'
else
  C_YELLOW=''; C_GREEN=''; C_RED=''; C_MAGENTA=''; C_CYAN=''; C_GRAY=''; C_RESET=''
fi

# ── Qdrant helpers ────────────────────────────────────────────────────────────
QDRANT_STARTED=0

start_qdrant() {
  printf "\n${C_CYAN}[run-all-tests] Starting Qdrant...${C_RESET}\n"
  (cd "$REPO_ROOT" && docker compose up -d qdrant)
  export QDRANT_URL='http://localhost:6333'
  local deadline=$(( $(date +%s) + 20 ))
  while [[ $(date +%s) -lt $deadline ]]; do
    if curl -sf 'http://localhost:6333/readyz' > /dev/null 2>&1; then
      printf "${C_GREEN}[run-all-tests] Qdrant is ready.${C_RESET}\n"
      return 0
    fi
    sleep 0.5
  done
  printf "${C_YELLOW}[run-all-tests] Qdrant health check timed out.${C_RESET}\n"
}

stop_qdrant() {
  printf "\n${C_CYAN}[run-all-tests] Stopping Qdrant...${C_RESET}\n"
  (cd "$REPO_ROOT" && docker compose stop qdrant) > /dev/null
}

# ── Main ──────────────────────────────────────────────────────────────────────
OVERALL_EXIT=0
VERBOSE_FLAG=()
[[ $VERBOSE -eq 1 ]] && VERBOSE_FLAG=(--verbose)

cleanup() {
  if [[ $QDRANT_STARTED -eq 1 && $KEEP_QDRANT -eq 0 ]]; then
    stop_qdrant
  fi
}
trap cleanup EXIT

printf "${C_YELLOW}======================================${C_RESET}\n"
printf "${C_YELLOW} RAG Tool — Full Test Suite${C_RESET}\n"
printf "${C_YELLOW}======================================${C_RESET}\n"

start_qdrant
QDRANT_STARTED=1

if [[ $SKIP_PYTHON -eq 0 ]]; then
  printf "\n${C_MAGENTA}────── PYTHON TESTS ──────${C_RESET}\n"
  bash "$PYTHON_TESTS" "${VERBOSE_FLAG[@]}" || {
    printf "${C_YELLOW}[run-all-tests] Python tests FAILED (exit $?)${C_RESET}\n"
    OVERALL_EXIT=$?
  }
else
  printf "\n${C_GRAY}[run-all-tests] Skipping Python tests (--skip-python)${C_RESET}\n"
fi

if [[ $SKIP_DOTNET -eq 0 ]]; then
  printf "\n${C_MAGENTA}────── .NET TESTS ──────${C_RESET}\n"
  bash "$DOTNET_TESTS" "${VERBOSE_FLAG[@]}" || {
    local_exit=$?
    printf "${C_YELLOW}[run-all-tests] .NET tests FAILED (exit $local_exit)${C_RESET}\n"
    [[ $OVERALL_EXIT -eq 0 ]] && OVERALL_EXIT=$local_exit
  }
else
  printf "\n${C_GRAY}[run-all-tests] Skipping .NET tests (--skip-dotnet)${C_RESET}\n"
fi

printf "\n${C_YELLOW}======================================${C_RESET}\n"
if [[ $OVERALL_EXIT -eq 0 ]]; then
  printf "${C_GREEN} ALL TESTS PASSED${C_RESET}\n"
else
  printf "${C_RED} SOME TESTS FAILED (exit %d)${C_RESET}\n" "$OVERALL_EXIT"
fi
printf "${C_YELLOW}======================================${C_RESET}\n"

exit $OVERALL_EXIT
