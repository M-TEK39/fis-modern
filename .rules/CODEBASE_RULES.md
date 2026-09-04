# FIS codebase rules

## Repository map

```text
FIS.sln
src/Services/FIS.Api/             C# REST API
src/Services/FIS.Web.Next/        standalone Next.js frontend
src/Services/FIS.Web/             legacy Blazor project during migration
src/Core/FIS.Core.Domain/
src/Core/FIS.Core.Application/
src/Core/FIS.Core.Infrastructure/
src/Data/FIS.Data/
src/Data/FIS.Data.SqlServer/
src/Tools/DatabaseInspector/
src/Tools/DatabaseMigrationTool/
src/Tools/DatabaseSeederTool/
backup/sources/                    read-only legacy reference
docker/                            Dockerfiles, Compose, and nginx
```

The root guardrails are `AGENTS.md` and `CLAUDE.md`. Detailed rules live in `.rules/`. Keep these files tracked and do not replace them with links to another checkout.

## Ownership and placement

- Put Next routes, layouts, actions, server adapters, and styles under `src/Services/FIS.Web.Next`.
- Put reusable Next UI in that app's `components/` directory; keep route-specific composition next to its route.
- Put shared frontend types beside the adapter or feature that owns them unless an existing shared contract requires otherwise.
- Put API endpoints and transport DTOs in `src/Services/FIS.Api`.
- Put business behavior in Core and SQL Server access in Data.
- Put deployment changes in `docker/` and preserve the existing service names and network topology unless the task requires a routing change.
- Do not edit generated `bin/`, `obj/`, `.next/`, `dist/`, or build cache output.

## Frontend package

`src/Services/FIS.Web.Next` is a standalone package managed with pnpm. Use its declared scripts and do not add a root JavaScript workspace, task runner, or package alias layer for convenience.

```bash
pnpm --dir src/Services/FIS.Web.Next dev
pnpm --dir src/Services/FIS.Web.Next typecheck
pnpm --dir src/Services/FIS.Web.Next build
```

Keep `package.json`, `pnpm-lock.yaml`, `tsconfig.json`, and `next.config.ts` consistent. Dependencies must be intentional, versioned, and approved by the task.

## Backend package

Use the solution's existing project references and .NET 8 conventions:

```bash
dotnet build FIS.sln --no-restore
```

Do not move code between layers to avoid a reference problem. Do not introduce database schema changes, a second API style, or a second authentication contract.

## Docker and nginx

- Production Compose expects the SQL Server host and credentials through environment configuration; it does not provision a production database.
- `docker/docker-compose.dev.yml` owns the local SQL Server development container and initialization flow.
- Keep the API, legacy web, Next web, and nginx services on the existing network. Route only to healthy, intended upstreams.
- Do not commit certificates, tokens, passwords, or machine-specific host values.
- Validate nginx paths, cookie forwarding, headers, health checks, and WebSocket behavior when changing routing.

## Legacy compatibility

Before a UI module change, trace `backup/sources/GGFIS_v2.0/Main.aspx`, the relevant menu, and the actual page files. Cross-reference the legacy API components/data model and UI controls/datasets. Preserve workflow and screen sequencing; modernize the implementation, not the user's established process.

The backend is a dual-schema compatibility layer during modernization. It must operate against the client's existing legacy tables/fields as well as databases containing expanded modern objects. Use modern objects when available and authoritative, but retain explicit runtime fallbacks to the legacy objects. Never solve the mismatch with a schema migration, a guessed field mapping, or a statically mapped EF property that can make a legacy query fail because an optional column is absent.

## Git and verification

Inspect status before editing and preserve unrelated changes. Review the final diff for secrets, generated files, accidental schema edits, and route regressions. Run the smallest relevant checks and report any unavailable runtime dependency separately from source/build results. Do not commit or push without an explicit request.
