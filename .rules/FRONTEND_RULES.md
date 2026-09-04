# FIS Next.js frontend rules

These rules apply to `src/Services/FIS.Web.Next`.

## App Router and rendering

- Use the App Router and keep pages/layouts as Server Components by default.
- Add `"use client"` only at the smallest boundary that needs browser state, event handlers, or browser-only APIs.
- Render stable shell and data on the server, then pass serializable props into focused Client Components. Do not move an entire route client-side to avoid designing the boundary.
- Keep `cacheComponents: true` and `partialPrefetching: true` in `next.config.ts` unless a measured, documented compatibility issue requires a change.
- Design loading, error, and empty states with `loading.tsx`, `error.tsx`, Suspense, or route-local states as appropriate.
- Instant navigation and prefetched output must never make protected data or authorization stale. Do not cache session lookup, permission checks, or user-specific responses.

## Data and authentication

- Use the existing C# REST API. Put server-only typed adapters in `src/Services/FIS.Web.Next/lib/` and mark modules that require it with `server-only` where appropriate.
- Keep API base URLs and secrets on the server. Forward the existing HttpOnly FIS session cookie; never put access tokens in localStorage, sessionStorage, query strings, or rendered HTML.
- Keep authentication and authorization decisions on the server. A Client Component may display state, but it cannot be the security boundary.
- Use Server Actions or server route handlers for mutations. Validate input again on the server, handle API errors explicitly, and revalidate only affected paths after success.
- Keep the established login, password reset, expired-password, Microsoft sign-in, and sign-out contracts. Do not silently introduce a parallel identity system.
- Do not call the C# API directly from a browser when doing so would expose credentials or bypass the server session boundary.

## TypeScript and component design

- Keep strict TypeScript. Model nullable legacy fields honestly and avoid `any`, non-null assertions, and unchecked casts as shortcuts.
- Derive request and response types from the actual API contract. Do not invent fields because a UI needs them.
- Keep components cohesive and compose existing primitives before adding new ones. Avoid boolean-prop explosions and duplicated form/table/modal implementations.
- Use accessible semantic HTML first. Interactive controls must be real buttons or links with accessible names and visible focus.
- Keep client bundles small; do not import server adapters, database code, Node-only modules, or secrets into Client Components.

## UI and CSS

- Use the shared classes and design tokens in `app/globals.css` before adding new styles.
- Keep reusable styles global and keep route-local CSS limited to genuinely unique layout or presentation.
- Preserve the legacy screen sequence, navigation labels, data meaning, and task flow while improving responsiveness and feedback.
- Every async operation needs a pending state, a recoverable error state, and an appropriate empty state. Do not leave a form stuck after a failed request.
- Prefer progressive enhancement: a user should understand the page and its primary action before client JavaScript finishes loading.

## Route conventions

- Match existing legacy routes where compatibility requires it, including case-sensitive legacy paths represented by App Router folders.
- Keep metadata and titles accurate for the route. Do not expose sensitive identifiers in metadata or URLs unless already part of the contract.
- Use `notFound()` for a missing resource and an error boundary for unexpected failures; do not render a successful empty page for an API failure.
- Keep redirects intentional and test authenticated, unauthenticated, unauthorized, expired-session, and API-unavailable paths.

## Verification

Run from the repository root:

```bash
pnpm --dir src/Services/FIS.Web.Next typecheck
pnpm --dir src/Services/FIS.Web.Next build
```

For visible UI changes, test keyboard navigation, focus order, narrow screens, loading/error/empty states, and the real route in a production build when possible. For authentication or Cache Component changes, verify session expiry, refresh/reconnect behavior, and authorization freshness in a browser.
