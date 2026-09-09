# Fleet Information System

Fleet Information System (FIS) is a modernization of a fleet operations platform. It combines an ASP.NET Core REST API, a Next.js web application, SQL Server data access, and Docker-based development and deployment workflows.

The application supports fleet and vehicle administration, contracts, trips, logbooks, fuel cards, maintenance and workshop operations, incidents and losses, licensing, finance and tariffs, reports, user administration, and workflow notifications.

## Architecture

```text
src/
├── Services/FIS.Api/             ASP.NET Core 8 REST API
├── Services/FIS.Web.Next/        Next.js 16.3.4 and React 19 frontend
├── Core/FIS.Core.Domain/         Domain entities and contracts
├── Core/FIS.Core.Application/    Application services and use cases
├── Core/FIS.Core.Infrastructure/ Repositories and infrastructure services
├── Data/FIS.Data/                Shared data contracts
├── Data/FIS.Data.SqlServer/      EF Core and SQL Server access
└── Tools/                        Database inspection, migration, audit, and seed tools

docker/
├── docker-compose.dev.yml        Local SQL Server and containerized development stack
├── docker-compose.yml            Application deployment stack
├── Dockerfile.api                API image
├── Dockerfile.web-next           Next.js image
└── nginx/                        HTTPS reverse-proxy configuration
```

The Next.js application is a standalone pnpm package. The API and data layers are managed by the .NET solution. The application uses typed, server-only API adapters in the web layer and preserves the existing FIS session-cookie contract.

The data layer is designed for SQL Server installations with different supported schema shapes. Compatibility access is guarded at runtime so optional modern objects do not prevent operation against an existing database.

## Prerequisites

- Node.js 22 or newer
- pnpm 11.25 or newer
- .NET SDK 8
- Docker Engine or Docker Desktop with Docker Compose

Check the pinned .NET SDK in [`global.json`](global.json) and the JavaScript package versions in the root and frontend `package.json` files.

## Quick start

From the repository root:

```bash
cp .env.example .env
```

Edit `.env` and set a strong local SQL Server password in `MSSQL_SA_PASSWORD`. The local database defaults are `fis_dev` and port `1433`; change `MSSQL_DB_NAME` or `MSSQL_HOST_PORT` if required. Never commit `.env`.

Install dependencies, create the local database, and start the host-local application:

```bash
pnpm install
pnpm db:start
pnpm dev
```

The host-local services are then available at:

| Service | URL |
| --- | --- |
| Next.js web application | <http://localhost:3000> |
| API and development Swagger UI | <http://localhost:5010> |
| API health check | <http://localhost:5010/health> |
| Next.js health check | <http://localhost:3000/health> |

`pnpm db:start` starts SQL Server and runs the guarded local schema bootstrap. It does not populate demo records. Run `pnpm db:seed` when demo data is needed.

## Development commands

Run these commands from the repository root:

```bash
pnpm dev                  # Host API and Next.js development servers
pnpm dev:api              # API only
pnpm dev:web              # Next.js only
pnpm dev:docker           # Compose API and Next.js containers
pnpm dev:docker:down      # Stop the Compose development application
pnpm db:start             # Start local SQL Server and bootstrap its schema
pnpm db:seed              # Seed local development/demo data
pnpm db:status            # Show local SQL Server status
pnpm db:stop              # Stop local SQL Server without removing its volume
```

The host-local API deliberately connects to the local Docker database. It does not reuse a deployment connection string from `.env`. `pnpm dev:web` is useful for frontend-only work, but API-backed pages require the API to be running as well.

From the frontend package directory, equivalent commands include:

```bash
cd src/Services/FIS.Web.Next
pnpm dev
pnpm dev:web
pnpm typecheck
pnpm build
```

## Configuration

`.env.example` is the safe configuration template. The main configuration groups are:

| Group | Purpose |
| --- | --- |
| `MSSQL_*` | Local Docker SQL Server name, password, database, and host port |
| `DB_*` or `ConnectionStrings__Default` | External SQL Server connection for deployment or explicitly targeted tools |
| `ApiSettings__*` | API and web base URLs |
| `AzureAd__*` | Optional Microsoft Entra ID sign-in |
| `EmailSettings__*` | Microsoft Graph password-reset email delivery |
| `SendGrid__ApiKey` | Workflow notification email delivery |
| `FIS_API_HOST_PORT`, `FIS_WEB_HOST_PORT` | Host ports for the Compose development services |

Use environment variables or the deployment secret store for credentials, signing keys, tokens, certificates, and API keys. Do not place real values in tracked files or command output.

## Authentication and API usage

The API exposes controller-based REST endpoints under `/api`. Development mode serves Swagger at the API root. Authentication uses the HttpOnly `FIS_Access_Token` and `FIS_Refresh_Token` cookies; authorization is enforced server-side. The frontend forwards the server session to typed adapters and does not put API credentials or bearer tokens in browser storage.

Password recovery uses expiring, one-time links. Microsoft Entra ID sign-in is enabled only when its required settings are configured. Microsoft Graph and SendGrid integrations require valid provider configuration and permissions; a configured setting alone does not prove email delivery.

## Database tools

Database commands default to the local Docker database. Supplying `ConnectionStrings__Default` explicitly targets another database.

```bash
pnpm db:inspect
pnpm db:migrations                         # Read-only migration plan
pnpm db:audit                              # Read-only compatibility/data-quality audit
pnpm db:backfix -- plan --plan=reviewed-fix.json
```

Mutation commands require their explicit confirmation flags:

```bash
pnpm db:migrations -- apply --confirm=FIS-ADDITIVE-ONLY
pnpm db:backfix -- apply --plan=reviewed-fix.json --confirm=FIS-REVIEWED-DATA-FIX
```

Review the target, intended SQL, authorization, and recovery procedure before applying any change. Do not aim local commands at a client or production database without an approved connection string and change review.

## Production-like Docker deployment

The production Compose file runs the API, Next.js, and nginx services. It expects SQL Server to be supplied through environment configuration; it does not provision a database server.

1. Copy `.env.example` to `.env`.
2. Set `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER`, and `DB_PASSWORD`, or provide `ConnectionStrings__Default`.
3. Configure the required web URL, authentication, email, and secret settings.
4. Provide the TLS files expected by nginx: `docker/nginx/certs/fis.crt` and `docker/nginx/certs/fis.key`.
5. Start the stack:

```bash
docker compose -f docker/docker-compose.yml up -d --build
```

The default public endpoints are:

- Web: <https://localhost>
- API: <https://localhost:5443>

Useful operational commands:

```bash
docker compose -f docker/docker-compose.yml ps
docker compose -f docker/docker-compose.yml logs -f fis-api fis-web-next nginx
docker compose -f docker/docker-compose.yml down
```

The `tools` and `tunnel` Compose profiles are opt-in. Review their environment requirements before starting them.

## Quality checks

Run the smallest relevant check during development and the full set before handoff:

```bash
pnpm check-types
pnpm lint
pnpm format:check
pnpm build
```

The individual checks are also available as:

```bash
pnpm --dir src/Services/FIS.Web.Next typecheck
pnpm --dir src/Services/FIS.Web.Next build
dotnet build FIS.sln --no-restore
```

`pnpm lint` runs the configured frontend formatting check and the .NET solution build. `pnpm format` applies Prettier and the repository-pinned CSharpier formatter. There is currently no ESLint configuration.

For UI changes, verify keyboard access, focus behavior, narrow-screen layout, and loading, empty, error, validation, and success states. For authentication, API, database, or deployment changes, verify the actual route and runtime path in addition to source and build checks.

## Repository conventions

- Keep behavior in its owning application layer and update the complete API, Core, Data, frontend, and deployment path when a contract crosses layers.
- Preserve SQL Server compatibility and do not introduce unreviewed schema changes.
- Keep secrets in environment configuration; commit only documentation-safe placeholders.
- Preserve unrelated worktree changes and do not commit or push as part of routine local development.
