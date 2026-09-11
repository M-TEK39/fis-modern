import { Suspense, type ReactNode } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

/**
 * Keeps request-time session, permission, and protected-data work below a
 * route-local Suspense boundary when Cache Components are enabled.
 *
 * Route modules must still perform authorization in their async child. This
 * component deliberately renders only a generic loading state while that
 * child is resolving, so it cannot disclose protected route information.
 */
export function StreamedRoute({ children }: Readonly<{ children: ReactNode }>) {
  return <Suspense fallback={<RouteLoading />}>{children}</Suspense>;
}
