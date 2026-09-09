#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
server_mode="${1:-dev}"

case "$server_mode" in
  dev) dotnet_configuration="Debug" ;;
  start) dotnet_configuration="Release" ;;
  *)
    echo "Unknown API server mode '$server_mode'. Use dev or start." >&2
    exit 64
    ;;
esac

: "${MSSQL_SA_PASSWORD:?Set MSSQL_SA_PASSWORD to the local SQL Server password before running local API commands.}"

mssql_host_port="${MSSQL_HOST_PORT:-1433}"
mssql_database="${MSSQL_DB_NAME:-fis_dev}"

case "$mssql_database" in
  fis_dev|fis_test|fis_dev_*|fis_test_*) ;;
  *)
    echo "Refusing to construct a local connection for database '$mssql_database'. Use fis_dev or fis_test." >&2
    exit 1
    ;;
esac

if ! docker compose -f "$PROJECT_ROOT/docker/docker-compose.dev.yml" ps --status running --services | grep -qx "mssql"; then
  echo "The local SQL Server container is not running. Start it first with: pnpm db:start" >&2
  exit 1
fi

# pnpm dev and pnpm start are deliberately local-only. They never reuse a
# ConnectionStrings__Default value from a deployment .env file.
export ConnectionStrings__Default="Server=localhost,${mssql_host_port};Database=${mssql_database};User Id=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=False;TrustServerCertificate=True;"
export ASPNETCORE_ENVIRONMENT="Development"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:5010}"
export ApiSettings__WebBaseUrl="${ApiSettings__WebBaseUrl:-http://localhost:3000}"
export Cors__Origins__0="${Cors__Origins__0:-http://localhost:3000}"

exec dotnet run --project "$PROJECT_ROOT/src/Services/FIS.Api/FIS.Api.csproj" --configuration "$dotnet_configuration" --no-launch-profile
