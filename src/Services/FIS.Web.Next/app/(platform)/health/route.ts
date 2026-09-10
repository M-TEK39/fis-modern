/**
 * Process-only liveness probe for the container runtime.
 *
 * It deliberately avoids sessions, the FIS API, and the database so an
 * unhealthy dependency does not mask whether the Next.js process can serve.
 */
export function GET() {
  return new Response("ok", {
    headers: {
      "Cache-Control": "no-store",
      "Content-Type": "text/plain; charset=utf-8",
    },
  });
}
