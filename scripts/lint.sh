#!/usr/bin/env bash

set -uo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"

set +e
pnpm --dir "$PROJECT_ROOT" --filter @fis/web-next run format:check
web_exit_code=$?
dotnet build "$PROJECT_ROOT/FIS.sln" --no-restore
dotnet_exit_code=$?
set -e

if [[ "$web_exit_code" -ne 0 || "$dotnet_exit_code" -ne 0 ]]; then
  exit 1
fi
