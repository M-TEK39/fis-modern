#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
server_mode="${1:-dev}"

case "$server_mode" in
  dev) web_script="dev:web" ;;
  start)
    web_script="start:web"
    if [[ ! -f "$PROJECT_ROOT/src/Services/FIS.Web.Next/.next/BUILD_ID" ]]; then
      echo "The production Next.js build is missing. Run: pnpm build:web" >&2
      exit 1
    fi
    ;;
  *)
    echo "Unknown local server mode '$server_mode'. Use dev or start." >&2
    exit 64
    ;;
esac

if ! docker compose -f "$PROJECT_ROOT/docker/docker-compose.dev.yml" ps --status running --services | grep -qx "mssql"; then
  echo "The local SQL Server container is not running. Start it first with: pnpm db:start" >&2
  exit 1
fi

cleanup() {
  local exit_code=$?

  trap - EXIT INT TERM
  for process_id in "${api_process_id:-}" "${web_process_id:-}"; do
    if [[ -n "$process_id" ]] && kill -0 "$process_id" 2>/dev/null; then
      kill "$process_id" 2>/dev/null || true
    fi
  done

  wait "${api_process_id:-}" 2>/dev/null || true
  wait "${web_process_id:-}" 2>/dev/null || true
  exit "$exit_code"
}

trap cleanup EXIT INT TERM

bash "$SCRIPT_DIR/dev-api.sh" "$server_mode" &
api_process_id=$!

(
  cd "$PROJECT_ROOT"
  export API_BASE_URL="${API_BASE_URL:-http://localhost:5010}"
  exec pnpm --filter @fis/web-next run "$web_script"
) &
web_process_id=$!

wait -n "$api_process_id" "$web_process_id"
