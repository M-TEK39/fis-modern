#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$PROJECT_ROOT/.cache/dotnet-cli}"

cd "$PROJECT_ROOT"
dotnet tool restore
exec dotnet tool run csharpier -- "$@"
