#!/usr/bin/env bash
set -euo pipefail

echo "WARNING: scripts/orchestrator.sh is deprecated. Use scripts/operations-center.sh." >&2
exec bash "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/operations-center.sh" "$@"
