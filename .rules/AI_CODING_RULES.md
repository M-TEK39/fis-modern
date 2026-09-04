# FIS AI coding rules

These rules apply to every change in this repository. They complement the root `AGENTS.md` and `CLAUDE.md`; the narrower rule file for the current path also applies.

## 1. Work deliberately

- State the requested outcome and inspect the real execution path before editing.
- Ask for confirmation before a substantial implementation, migration, dependency change, schema-related action, or deployment change. A confirmed slice remains limited to that slice.
- Preserve unrelated dirty changes. Never reset, clean, or overwrite work that is outside the requested scope.
- Do not guess at missing legacy behavior, API contracts, routes, credentials, or database objects. Trace the source or report the blocker.
- Do not create speculative abstractions, dependencies, endpoints, tables, or features.
- Use `apply_patch` for local edits. Keep diffs focused and readable.

## 2. Repository boundaries

```text
src/Services/FIS.Api/             ASP.NET Core REST API
src/Services/FIS.Web.Next/        Next.js 16.3.4 frontend
src/Services/FIS.Web/             legacy Blazor surface during migration
src/Core/FIS.Core.Domain/          domain model
src/Core/FIS.Core.Application/    application/use-case layer
src/Core/FIS.Core.Infrastructure/ infrastructure and repositories
src/Data/FIS.Data/                data contracts and shared data code
src/Data/FIS.Data.SqlServer/      SQL Server EF Core access
src/Tools/                         database inspection, migration, and seed tools
backup/sources/                    legacy reference code; do not treat as a write target
docker/                            containers and nginx routing
```

The Next app is a standalone pnpm package. Keep its dependencies and scripts in `src/Services/FIS.Web.Next/package.json`; do not add workspace, monorepo, or alternate data-layer infrastructure.

## 3. Database and business compatibility

- The SQL Server schema is immutable. No migrations or code may rename, add, remove, or alter tables, columns, types, keys, constraints, indexes, or relationships.
- The application must run against both the client's existing legacy schema and databases that also contain the expanded modern objects. Treat the database version as a runtime compatibility concern, not as a deployment prerequisite.
- Prefer an expanded modern table/column when it exists and contains the authoritative value; fall back to the corresponding legacy table/column when the modern object is absent or has no usable value. Keep the mapping explicit per feature and avoid split-brain writes.
- Do not add optional modern properties to a statically mapped EF entity when their columns may be absent. Use a guarded, parameterized compatibility projection/update or another existing-provider pattern that selects and writes only objects confirmed to exist.
- Authentication, authorization, password state, and business workflow must remain valid on both schema shapes. When both credential stores exist, keep their shared password/status state synchronized where the legacy application still depends on it.
- Before changing a data-backed feature, inspect the existing C# model/repository and the legacy API/data model. Use the existing names and nullability.
- Keep business rules, authorization boundaries, navigation order, labels, and screen sequencing compatible with the legacy system.
- Database tooling is for inspection and explicitly authorized compatibility work. Never run a destructive command against a real database as part of normal development.

## 4. Next.js rules

- Use the App Router. Server Components are the default; use a Client Component only for browser state, event handlers, or browser-only APIs.
- Keep `cacheComponents: true` and `partialPrefetching: true` compatible with navigation. Use cacheable public data deliberately, but never cache authentication, authorization, session lookup, or protected user-specific responses.
- Put typed REST calls in server-only modules under `src/Services/FIS.Web.Next/lib/`. Do not expose API credentials or browser bearer tokens. Forward the existing HttpOnly FIS session cookie from the server.
- Use Server Actions or server route handlers for mutations and revalidate only the affected route/data after a successful mutation.
- Keep the authenticated boundary dynamic and authorization-fresh. A fast navigation is not permission to serve stale protected data.
- Keep client bundles small: do not import server adapters, database code, or secrets into Client Components.
- Use TypeScript strictness and derive types from the actual API contract. Do not use `any` to silence a mismatch.

## 5. C# rules

- Keep HTTP concerns in `src/Services/FIS.Api`; keep domain and application behavior in their existing layers.
- Use async I/O, cancellation tokens where the surrounding contract supports them, structured logging, and consistent problem responses.
- Validate input at the API boundary, enforce authorization on the server, and avoid logging credentials, tokens, or personal data.
- Use EF Core/SQL Server conventions already present in the solution. Do not introduce a second persistence strategy for convenience.
- Do not put presentation concerns into Core or Data projects.

## 6. Legacy discovery

For every module UI change:

1. Start at `backup/sources/GGFIS_v2.0/Main.aspx` and follow `onclick="m_click('...')"` targets.
2. Locate the module's `*_Menu.aspx` and inventory its pages.
3. Read relevant `*.aspx` files; consult `*.aspx.vb` only when routing or behavior is unclear.
4. Cross-reference `backup/sources/GGFIS_Api/Components`, `Config`, and `DataModel`, plus `GGFIS_v2.0/Controls` and `DataSets`.

If the actual target is unavailable, stop rather than substituting a guessed workflow.

## 7. Security and operations

- Keep secrets in environment configuration or the deployment secret store. Track only safe placeholders and examples.
- Review Docker and nginx changes as a routing/security change: preserve API/frontend separation, health checks, TLS behavior, and trusted origins.
- Do not broaden CORS, disable certificate validation, weaken cookie flags, or bypass authorization to make local testing pass.

## 8. Verification and handoff

Run relevant checks with pnpm for the frontend:

```bash
pnpm --dir src/Services/FIS.Web.Next typecheck
pnpm --dir src/Services/FIS.Web.Next build
dotnet build FIS.sln --no-restore
```

For UI, auth, API, or deployment work, include the actual route and runtime path in verification. Report checks that were not possible and distinguish source/build proof from live deployment proof. Do not commit or push unless explicitly requested.
