# FIS backend rules

These rules apply to C# changes in `src/Services/FIS.Api`, `src/Core`, `src/Data`, and `src/Tools`.

## Architecture

- `FIS.Api` owns HTTP transport, controllers, authentication wiring, authorization policies, OpenAPI, health checks, and API error mapping.
- `FIS.Core.Application` owns use cases and application services.
- `FIS.Core.Domain` owns domain entities, value objects, and domain contracts.
- `FIS.Core.Infrastructure` owns implementations that support application contracts.
- `FIS.Data.SqlServer` owns the existing EF Core SQL Server model and data access.
- `FIS.Data` owns shared data contracts that are already part of the solution.

Respect these dependencies. Do not put controllers in Core, SQL details in Domain, or UI behavior in the API.

## Immutable database contract

The database is a legacy SQL Server database and is a plug-compatible contract. Do not:

- create or edit schema migrations as part of a normal feature;
- rename, add, remove, or alter tables, columns, types, keys, constraints, indexes, or relationships;
- change stored-procedure/query semantics without tracing all existing consumers;
- replace SQL Server with another provider.

When a requested feature appears to require schema work, stop and report the conflict. Use `src/Tools/DatabaseInspector` to inspect existing objects and the approved migration/seed tools only for explicitly authorized compatibility work.

### Dual-schema compatibility

- The API must work with the client's current legacy database and with a database that has the expanded modern objects applied.
- Resolve modern-vs-legacy table and column availability at runtime through the existing SQL Server provider. Keep identifiers allow-listed, queries parameterized, and optional columns out of static EF projections when they may not exist.
- Prefer modern data when it is present and authoritative, then fall back to the exact legacy field/table and preserve its null and type semantics. Do not silently invent a replacement field or change a business rule to fit the modern model.
- For shared authentication state, update every available credential/status representation needed by the still-supported legacy process; a password change must not leave the legacy login path stale when both stores are present.
- A missing expanded object is an expected compatibility state, not a 500. A genuinely broken required legacy object remains an actionable dependency error and must be logged without exposing SQL details to the client.

## API implementation

- Match existing controller routes, response shapes, status codes, and legacy naming unless the request explicitly changes the contract.
- Use DTOs at the HTTP boundary; do not expose tracked EF entities directly.
- Validate input before application calls and return the solution's established problem response shape.
- Use async methods and cancellation where practical. Do not block on tasks or make synchronous network/database calls in request handlers.
- Keep queries parameterized and bounded. Avoid loading an unbounded table into memory for a list endpoint.
- Preserve pagination, filtering, sorting, and null semantics already expected by the legacy UI.
- Put cross-cutting concerns in the existing pipeline rather than duplicating them in every controller.

## Authentication and authorization

- Authenticate and authorize on the server for every protected endpoint; a hidden button is not an access control.
- Preserve the FIS cookie/session and Microsoft identity integration already present. Do not add a parallel identity store or silently change claims.
- Check authorization at the resource boundary, not only at page load. Re-check it for every mutation.
- Never log passwords, tokens, cookies, client secrets, or full sensitive request bodies.
- Keep password reset responses and timing resistant to account enumeration. Validate reset tokens server-side and expire them after use according to the existing contract.

## Reliability and observability

- Return actionable, stable errors to clients and log detailed diagnostics server-side with structured logging.
- Do not leak stack traces, connection strings, SQL text containing secrets, or internal paths in production responses.
- Apply timeouts and cancellation to external calls. Do not retry non-idempotent operations blindly.
- Keep health checks meaningful: distinguish process health from database/API dependency health.
- Maintain rate limiting and security headers already configured in the API.

## Dependencies and configuration

- Use the versions and package conventions already declared by the solution. No floating versions or unapproved package additions.
- Keep connection strings, Microsoft identity settings, mail settings, and signing secrets in environment/deployment configuration.
- Never commit real credentials. Documentation examples must use obviously fake placeholders.
- Preserve environment-specific configuration and do not make production depend on a local database container.

## Verification

At minimum, run:

```bash
dotnet build FIS.sln --no-restore
```

For endpoint changes, exercise the real route with authenticated, unauthorized, invalid, empty, and dependency-failure cases when the services are available. For database changes, verify against the actual legacy schema and state clearly if live connectivity was not available.
