#!/usr/bin/env bash
# Run all RAG .NET tests (unit + E2E) with optional Qdrant startup.
#
# Cross-platform companion to run-tests.ps1 (macOS / Linux).
#
# USAGE
#   bash tools/rag-dotnet/run-tests.sh [--start-qdrant] [--unit-only] [--e2e-only] [--verbose]
#
# OPTIONS
#   --start-qdrant   Start Qdrant via `docker compose up -d qdrant` before
#                    tests, stop it in the finally block.
#   --unit-only      Run only unit tests (filter: Category!=E2E). No Qdrant needed.
#   --e2e-only       Run only E2E tests (filter: Category=E2E). Requires model + Qdrant.
#   --verbose        Pass --logger "console;verbosity=detailed" to dotnet test.
#
# EXAMPLES
#   # Download model first (one-time)
#   bash tools/rag-dotnet/download-model.sh
#
#   # Run all .NET tests, auto-start Qdrant
#   bash tools/rag-dotnet/run-tests.sh --start-qdrant
#
#   # Run only unit tests (no Qdrant needed)
#   bash tools/rag-dotnet/run-tests.sh --unit-only
#
#   # Run E2E tests with QDRANT_URL already set
#   QDRANT_URL=http://localhost:6333 bash tools/rag-dotnet/run-tests.sh --e2e-only

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"
TESTS_PROJ="$SCRIPT_DIR/src/RagTools.Tests/RagTools.Tests.csproj"
MODEL_DIR="$SCRIPT_DIR/model"

# ── Flags ─────────────────────────────────────────────────────────────────────
START_QDRANT=0; UNIT_ONLY=0; E2E_ONLY=0; VERBOSE=0
for arg in "$@"; do
  case "$arg" in
    --start-qdrant) START_QDRANT=1 ;;
    --unit-only)    UNIT_ONLY=1 ;;
    --e2e-only)     E2E_ONLY=1 ;;
    --verbose)      VERBOSE=1 ;;
    *) echo "Unknown argument: $arg"; exit 1 ;;
  esac
done

# ── Color helpers ─────────────────────────────────────────────────────────────
if [[ -t 1 ]]; then
  C_CYAN='\033[0;36m'; C_GREEN='\033[0;32m'; C_RED='\033[0;31m'
  C_YELLOW='\033[0;33m'; C_RESET='\033[0m'
else
  C_CYAN=''; C_GREEN=''; C_RED=''; C_YELLOW=''; C_RESET=''
fi

# ── Prereq checks ─────────────────────────────────────────────────────────────
if [[ ! -f "$TESTS_PROJ" ]]; then
  printf "${C_RED}Test project not found at %s${C_RESET}\n" "$TESTS_PROJ" >&2; exit 1
fi

if [[ ! -f "$MODEL_DIR/model.onnx" ]]; then
  if [[ $UNIT_ONLY -eq 1 ]]; then
    printf "${C_YELLOW}ONNX model not found — E2E tests will be skipped (model not needed for unit tests).${C_RESET}\n"
  else
    printf "${C_YELLOW}ONNX model not found at %s/model.onnx. Run: bash tools/rag-dotnet/download-model.sh${C_RESET}\n" "$MODEL_DIR"
  fi
fi

# ── Qdrant helpers ────────────────────────────────────────────────────────────
QDRANT_STARTED=0

start_qdrant() {
  printf "\n${C_CYAN}[rag-tests] Starting Qdrant via docker compose...${C_RESET}\n"
  (cd "$REPO_ROOT" && docker compose up -d qdrant)
  export QDRANT_URL='http://localhost:6333'
  local deadline=$(( $(date +%s) + 20 ))
  while [[ $(date +%s) -lt $deadline ]]; do
    if curl -sf 'http://localhost:6333/readyz' > /dev/null 2>&1; then
      printf "${C_GREEN}[rag-tests] Qdrant is ready.${C_RESET}\n"
      return 0
    fi
    sleep 0.5
  done
  printf "${C_YELLOW}[rag-tests] Qdrant may not be ready yet (health check timed out after 20s).${C_RESET}\n"
}

stop_qdrant() {
  printf "\n${C_CYAN}[rag-tests] Stopping Qdrant...${C_RESET}\n"
  (cd "$REPO_ROOT" && docker compose stop qdrant) > /dev/null
}

# ── Build filter and verbosity args ──────────────────────────────────────────
FILTER_ARGS=()
if [[ $UNIT_ONLY -eq 1 ]]; then
  FILTER_ARGS=(--filter 'Category!=E2E')
elif [[ $E2E_ONLY -eq 1 ]]; then
  FILTER_ARGS=(--filter 'Category=E2E')
fi

VERBOSITY_ARGS=()
if [[ $VERBOSE -eq 1 ]]; then
  VERBOSITY_ARGS=(--logger 'console;verbosity=detailed')
else
  VERBOSITY_ARGS=(--logger 'console;verbosity=normal')
fi

# ── Main ──────────────────────────────────────────────────────────────────────
EXIT_CODE=0

cleanup() {
  [[ $QDRANT_STARTED -eq 1 ]] && stop_qdrant || true
}
trap cleanup EXIT

if [[ $START_QDRANT -eq 1 ]]; then
  start_qdrant
  QDRANT_STARTED=1
fi

printf "\n${C_CYAN}[rag-tests] Running .NET tests...${C_RESET}\n"
dotnet test "$TESTS_PROJ" "${FILTER_ARGS[@]}" "${VERBOSITY_ARGS[@]}" --no-restore \
  || EXIT_CODE=$?

if [[ $EXIT_CODE -eq 0 ]]; then
  printf "\n${C_GREEN}[rag-tests] ALL TESTS PASSED${C_RESET}\n"
else
  printf "\n${C_RED}[rag-tests] SOME TESTS FAILED (exit %d)${C_RESET}\n" "$EXIT_CODE"
fi

exit $EXIT_CODE
