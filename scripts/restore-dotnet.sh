#!/usr/bin/env bash

set -euo pipefail

if [[ "${FIS_SKIP_DOTNET_RESTORE:-}" == "1" ]]; then
  exit 0
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$PROJECT_ROOT/.cache/dotnet-cli}"

cd "$PROJECT_ROOT"
dotnet restore FIS.sln
dotnet restore src/Tools/DatabaseMigrationTool/DatabaseMigrationTool.csproj
dotnet restore src/Tools/DatabaseAuditTool/DatabaseAuditTool.csproj
dotnet restore src/Tools/DatabaseBackfixTool/DatabaseBackfixTool.csproj
dotnet restore src/Tools/DatabaseInspector/DatabaseInspector.csproj
dotnet tool restore
