import { Suspense, type ReactNode } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import RouteLoading from "@/components/app-shell/route-loading";

export function JobCardPageFallback() {
  return <RouteLoading />;
}

export function JobCardPageBoundary({ children }: Readonly<{ children: ReactNode }>) {
  return <Suspense fallback={<JobCardPageFallback />}>{children}</Suspense>;
}

export function SessionProblem({ returnPath }: Readonly<{ returnPath: string }>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <SessionRecovery returnPath={returnPath} />
    </main>
  );
}

export function AccessRestricted({ message }: Readonly<{ message: string }>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>{message}</h2>
      </section>
    </main>
  );
}
