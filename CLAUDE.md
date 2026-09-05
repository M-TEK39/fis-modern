# FIS project instructions

Read `.rules/AI_CODING_RULES.md` before every task, then read the specific rules that cover the files being changed. The rules in `.rules/` are the source of truth for this repository.

FIS keeps a legacy-compatible SQL Server schema and business process while moving the presentation layer to Next.js. The active frontend is `src/Services/FIS.Web.Next` (Next.js 16.3.4, React 19, TypeScript, pnpm). The C# backend remains in `src/Services/FIS.Api` and the domain/application/infrastructure/data projects remain under `src/Core` and `src/Data`.

Every application layer is available for the work. Slices may update the Next frontend, API, Core, Data, Docker, and the remaining Blazor project together when that is required by the real execution path. Keep changes in the layer that owns the behavior and preserve existing contracts unless the requested slice deliberately changes them.

Keep the Next app's App Router and Server Component architecture. Fetch protected data through server-only typed REST adapters, forward the existing HttpOnly FIS session cookie, and use Client Components only where browser interaction requires them. Keep Cache Components and instant navigation compatible with authorization freshness: never cache login state, authorization, or protected user-specific responses.

Do not change the database schema, introduce speculative dependencies, or rewrite legacy workflows. The backend must run against both the client's current legacy database and databases containing expanded modern tables/fields: prefer modern data when available and authoritative, then fall back through explicit runtime compatibility access to the legacy objects. Before a module migration, trace its entry point and menu in `backup/sources/GGFIS_v2.0/` and cross-reference the legacy API, controls, and datasets. Keep common styling in the Next app's `app/globals.css` and apply the accessibility rules in `.rules/WEB_DESIGN_RULES.md`.

Use pnpm, not npm:

```bash
pnpm --dir src/Services/FIS.Web.Next typecheck
pnpm --dir src/Services/FIS.Web.Next build
dotnet build FIS.sln --no-restore
```

Preserve unrelated worktree changes. Do not commit or push without an explicit request.
