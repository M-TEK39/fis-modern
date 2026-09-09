#!/usr/bin/env bash

set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <migrations|audit|backfix|inspect> [tool arguments...]" >&2
  exit 64
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
tool="$1"
shift

# shellcheck source=load-local-env.sh
source "$SCRIPT_DIR/load-local-env.sh"
load_local_mssql_environment "$PROJECT_ROOT"

if [[ "${1:-}" == "--" ]]; then
  shift
fi

case "$tool" in
  migrations) project_path="src/Tools/DatabaseMigrationTool/DatabaseMigrationTool.csproj" ;;
  audit) project_path="src/Tools/DatabaseAuditTool/DatabaseAuditTool.csproj" ;;
  backfix) project_path="src/Tools/DatabaseBackfixTool/DatabaseBackfixTool.csproj" ;;
  inspect) project_path="src/Tools/DatabaseInspector/DatabaseInspector.csproj" ;;
  *)
    echo "Unknown database tool '$tool'. Use migrations, audit, backfix, or inspect." >&2
    exit 64
    ;;
esac

for argument in "$@"; do
  if [[ "$argument" == "--help" || "$argument" == "-h" ]]; then
    if [[ "$tool" == "inspect" ]]; then
      echo "Usage: pnpm db:inspect"
      echo "Connects to the configured database and lists base tables."
      exit 0
    fi

    cd "$PROJECT_ROOT"
    exec dotnet run --project "$project_path" -- "$@"
  fi
done

if [[ -z "${ConnectionStrings__Default:-}" ]]; then
  : "${MSSQL_SA_PASSWORD:?Set MSSQL_SA_PASSWORD or ConnectionStrings__Default before running pnpm db:${tool}.}"

  mssql_host_port="${MSSQL_HOST_PORT:-1433}"
  mssql_database="${MSSQL_DB_NAME:-fis_dev}"

  case "$mssql_database" in
    fis_dev|fis_test|fis_dev_*|fis_test_*) ;;
    *)
      echo "Refusing to construct a local connection for database '$mssql_database'. Use fis_dev, fis_test, or an explicitly supplied ConnectionStrings__Default." >&2
      exit 1
      ;;
  esac

  export ConnectionStrings__Default="Server=localhost,${mssql_host_port};Database=${mssql_database};User Id=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=False;TrustServerCertificate=True;"
  export DOTNET_ENVIRONMENT="${DOTNET_ENVIRONMENT:-Development}"
fi

if [[ "$tool" == "audit" && -z "${FIS_LEGACY_DDL_ROOT:-}" ]]; then
  export FIS_LEGACY_DDL_ROOT="$PROJECT_ROOT/backup/sources/GGMT.Database/SQLScripts/v2.0.0"
fi

cd "$PROJECT_ROOT"
exec dotnet run --project "$project_path" -- "$@"
