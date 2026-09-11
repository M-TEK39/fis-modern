#!/usr/bin/env bash

set -euo pipefail

project_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
app_root="$project_root/src/Services/FIS.Web.Next/app"
failures=0

while IFS= read -r -d '' page; do
  relative_path=${page#"$project_root/"}

  if rg -q 'export default async' "$page"; then
    printf '%s\n' "${relative_path}: page defaults must stay synchronous so request-time work can stream locally."
    failures=1
  fi

  # Next route modules are not a shared component surface. Compatibility aliases
  # must import reusable shells from a sibling non-route module; the two Next
  # metadata/static-params hooks are the only allowed named function exports.
  runtime_exports=$(
    rg -n '^export (async )?(function|class) ' "$page" \
      | rg -v '^(.*:)?export (async )?function (generateMetadata|generateStaticParams)\\b' \
      || true
  )
  if [[ -n "$runtime_exports" ]]; then
    printf '%s\n' "${relative_path}: page modules must not export reusable runtime components or helpers; move them to a sibling non-route module."
    printf '%s\n' "$runtime_exports"
    failures=1
  fi

  if ! rg -q 'await (connection\(|getSession\(|searchParams)|from "@/lib/api/' "$page"; then
    continue
  fi

  if ! rg -q '<Suspense|<StreamedRoute|JobCardPageBoundary' "$page"; then
    printf '%s\n' "${relative_path}: request-time page is missing a local streaming boundary."
    failures=1
  fi
done < <(find "$app_root" -name page.tsx -print0)

if (( failures )); then
  printf '%s\n' 'Instant-navigation guardrail failed. Keep protected request-time work inside a local generic Suspense child.' >&2
  exit 1
fi

printf '%s\n' 'Instant-navigation guardrail passed.'
