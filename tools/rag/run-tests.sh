#!/usr/bin/env bash
# Run all RAG Python tests (unit + E2E) with optional Qdrant startup.
#
# Cross-platform companion to run-tests.ps1 (macOS / Linux).
# Runs the full Python test suite for tools/rag/ — unit + E2E tests.
# E2E tests require a running Qdrant instance.
#
# USAGE
#   bash tools/rag/run-tests.sh [--start-qdrant] [--unit-only] [--e2e-only] [--verbose]
#
# OPTIONS
#   --start-qdrant   Start Qdrant via `docker compose up -d qdrant` before
#                    tests, stop it in the finally block.
#   --unit-only      Run only unit tests (test_ingest_unit.py). No Qdrant needed.
#   --e2e-only       Run only E2E tests (test_ingest_e2e.py). Requires Qdrant.
#   --verbose        Pass -v to pytest for verbose per-test output.
#
# EXAMPLES
#   # Run unit tests only (no Qdrant needed)
#   bash tools/rag/run-tests.sh --unit-only
#
#   # Run all tests, auto-start Qdrant
#   bash tools/rag/run-tests.sh --start-qdrant
#
#   # Run all tests (Qdrant already running on localhost:6333)
#   bash tools/rag/run-tests.sh
#
#   # Run with verbose output
#   bash tools/rag/run-tests.sh --verbose --start-qdrant

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"

# On Linux/macOS venv bin is bin/, not Scripts/
PYTEST="$SCRIPT_DIR/.venv/bin/pytest"
UNIT_FILE="$SCRIPT_DIR/test_ingest_unit.py"
E2E_FILE="$SCRIPT_DIR/test_ingest_e2e.py"

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

# ── Prereq check ──────────────────────────────────────────────────────────────
if [[ ! -f "$PYTEST" ]]; then
  printf "${C_RED}Python venv not found at %s.${C_RESET}\n" "$PYTEST" >&2
  printf "Run: cd tools/rag && python -m venv .venv && .venv/bin/pip install -e '.[dev]'\n" >&2
  exit 1
fi

# ── Qdrant helpers ────────────────────────────────────────────────────────────
QDRANT_STARTED=0

start_qdrant() {
  printf "\n${C_CYAN}[rag-tests] Starting Qdrant via docker compose...${C_RESET}\n"
  (cd "$REPO_ROOT" && docker compose up -d qdrant)
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

# ── Main ──────────────────────────────────────────────────────────────────────
VERBOSE_FLAGS=()
[[ $VERBOSE -eq 1 ]] && VERBOSE_FLAGS=(-v)

EXIT_CODE=0

cleanup() {
  [[ $QDRANT_STARTED -eq 1 ]] && stop_qdrant || true
}
trap cleanup EXIT

if [[ $START_QDRANT -eq 1 ]]; then
  start_qdrant
  QDRANT_STARTED=1
fi

if [[ $E2E_ONLY -eq 0 ]]; then
  printf "\n${C_CYAN}[rag-tests] Running unit tests (%s)...${C_RESET}\n" "$UNIT_FILE"
  "$PYTEST" "$UNIT_FILE" "${VERBOSE_FLAGS[@]}" || { EXIT_CODE=$?; [[ $UNIT_ONLY -eq 1 ]] && exit $EXIT_CODE || true; }
fi

if [[ $UNIT_ONLY -eq 0 ]]; then
  printf "\n${C_CYAN}[rag-tests] Running E2E tests (%s)...${C_RESET}\n" "$E2E_FILE"
  "$PYTEST" "$E2E_FILE" "${VERBOSE_FLAGS[@]}" || EXIT_CODE=$?
fi

if [[ $EXIT_CODE -eq 0 ]]; then
  printf "\n${C_GREEN}[rag-tests] ALL TESTS PASSED${C_RESET}\n"
else
  printf "\n${C_RED}[rag-tests] SOME TESTS FAILED (exit %d)${C_RESET}\n" "$EXIT_CODE"
fi

exit $EXIT_CODE
