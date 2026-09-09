#!/usr/bin/env bash

# Shell scripts must not source .env: a valid database password may contain
# shell metacharacters such as `&`. Read the local SQL Server settings as
# dotenv data instead, without evaluating any file content.
load_local_mssql_environment() {
  local project_root="$1"
  local env_file="$project_root/.env"
  local key
  local line
  local value

  [[ -f "$env_file" ]] || return

  for key in MSSQL_SA_PASSWORD MSSQL_DB_NAME MSSQL_HOST_PORT; do
    [[ -n "${!key:-}" ]] && continue

    while IFS= read -r line || [[ -n "$line" ]]; do
      line="${line%$'\r'}"
      [[ "$line" == "$key="* ]] || continue

      value="${line#"$key="}"
      if [[ ${#value} -ge 2 && ${value:0:1} == '"' && ${value: -1} == '"' ]]; then
        value="${value:1:${#value}-2}"
      elif [[ ${#value} -ge 2 && ${value:0:1} == "'" && ${value: -1} == "'" ]]; then
        value="${value:1:${#value}-2}"
      fi

      printf -v "$key" '%s' "$value"
      export "$key"
      break
    done < "$env_file"
  done
}
