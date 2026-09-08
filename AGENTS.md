# Fleet Information System (FIS) Agent Guide

## Required reading

Before changing code, read `.rules/AI_CODING_RULES.md`, then read the relevant rules for the files being changed:

- `.rules/CODEBASE_RULES.md` for repository boundaries and workflow
- `.rules/BACKEND_RULES.md` for C#, ASP.NET Core, EF Core, API, and database work
- `.rules/FRONTEND_RULES.md` for React and Next.js work
- `.rules/CSS_RULES.md` for styling
- `.rules/WEB_DESIGN_RULES.md` for accessibility and visual UX changes

Rules are cumulative. Do not invent a competing project structure or framework contract.

## Project reality

FIS is a modernization of a legacy fleet system. The implementation is split into a C# backend and a Next.js frontend:

```text
src/
├── Services/FIS.Api/             # ASP.NET Core .NET 8 REST API
├── Services/FIS.Web.Next/        # Next.js 16.3.4, React 19, TypeScript frontend
├── Core/FIS.Core.Domain/          # domain entities and contracts
├── Core/FIS.Core.Application/    # application services and use cases
├── Core/FIS.Core.Infrastructure/ # repositories and infrastructure
└── Data/FIS.Data.SqlServer/      # SQL Server data access
```

The legacy reference implementation is under `backup/sources/`. Docker and reverse-proxy configuration is under `docker/`. The Next app is a standalone pnpm package; it is not a workspace package and must not acquire unrelated monorepo infrastructure.

All application layers are in scope. A feature or compatibility slice may require changes in the Next frontend, API, Core, Data, or Docker. Keep each change in the layer that owns its behavior and update related layers together when the contract requires it.

## Non-negotiable boundaries

- Preserve the existing SQL Server schema exactly. Do not rename, add, remove, or change tables, columns, types, keys, or constraints. The app must run against both the client's current legacy schema and databases containing expanded modern objects, using explicit runtime fallbacks from modern fields/tables to their legacy counterparts.
- Keep business rules and legacy navigation flow intact while modernizing presentation and transport.
- Place behavior in its owning layer and update the complete execution path when a contract or compatibility fix spans API, Core, Data, deployment, and Next.js.
- The Next frontend consumes the existing REST API through server-only typed adapters. Browser code must not contain API secrets or bearer tokens.
- Use Next Server Components by default. Add Client Components only for browser state, events, or APIs that require the browser. Keep mutations in Server Actions or server routes where appropriate.
- Preserve the existing FIS cookie/session contract. Do not replace it with a new authentication framework as part of a page migration.
- Treat authorization as server-side enforcement. Never cache authentication, authorization decisions, or protected user-specific data.
- Treat missing expanded tables/columns as an expected compatibility state. Do not statically map optional columns in a way that makes EF queries fail on the legacy database; use guarded, parameterized compatibility access and keep shared authentication state synchronized when both stores exist.
- Use pnpm for JavaScript commands and .NET tooling for C# commands.
- Do not commit generated output, secrets, local environment files, or unrelated worktree changes.

## Legacy discovery

Before modernizing a module, trace its actual legacy entry point from `backup/sources/GGFIS_v2.0/Main.aspx`, following `onclick="m_click('...')"` targets. Consult the matching `*_Menu.aspx`, inventory the module's legacy pages, and inspect the relevant `*.aspx` and, when routing is unclear, `*.aspx.vb` files. Also map DTOs/controllers, legacy data models, controls, and datasets from:

- `backup/sources/GGFIS_Api/Components`
- `backup/sources/GGFIS_Api/Config`
- `backup/sources/GGFIS_Api/DataModel`
- `backup/sources/GGFIS_v2.0/Controls`
- `backup/sources/GGFIS_v2.0/DataSets`

If the real target cannot be located, stop and report the missing reference instead of guessing. Preserve screen order, navigation, labels, and business behavior.

## Verification

Run the smallest relevant checks, then the broader checks when the change warrants them:

```bash
pnpm --dir src/Services/FIS.Web.Next typecheck
pnpm --dir src/Services/FIS.Web.Next build
dotnet build FIS.sln --no-restore
```

For UI changes, verify loading, error, empty, keyboard, and narrow-screen states. For auth or deployment changes, test the actual route and Docker/nginx path when the environment is available.

Do not commit, push, or alter deployment state unless the user explicitly requests it.
