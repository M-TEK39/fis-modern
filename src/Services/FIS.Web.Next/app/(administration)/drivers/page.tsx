import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import RouteLoading from "@/components/app-shell/route-loading";
import DriverManagementSelector from "@/app/(administration)/drivers/driver-management-selector";
import {
  hasDriverAuthoriserManagementRole,
  parsePositiveInteger,
  getQueryValue,
} from "@/app/(administration)/drivers/access";
import {
  DriverManagementApiError,
  getDriverManagementDepartments,
  getDriverManagementSites,
} from "@/lib/api/reference-data/api-driver-management";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Driver and Authoriser Management.</h2>
      <p className="muted-copy">This workflow requires vehicle-management access.</p>
      <Link className="button button-secondary" href="/home">
        Home
      </Link>
    </section>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Driver-management reference data could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href="/drivers">
        Try again
      </Link>
    </section>
  );
}

async function DriverManagementContent({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") {
    redirect("/login");
  }
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/drivers" />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
  if (!hasDriverAuthoriserManagementRole(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  const query = await searchParams;
  const initialDepartmentCode = parsePositiveInteger(getQueryValue(query.departmentCode));
  const initialSiteCode = parsePositiveInteger(getQueryValue(query.siteCode));

  try {
    const [departments, sites] = await Promise.all([
      getDriverManagementDepartments(),
      getDriverManagementSites(),
    ]);

    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="driver-management-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Fleet administration</p>
              <h1 id="driver-management-title">Driver and Authoriser Management</h1>
              <p>Select a department and site first, matching the established legacy workflow.</p>
            </div>
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          </header>
          <DriverManagementSelector
            departments={departments}
            initialDepartmentCode={initialDepartmentCode}
            initialSiteCode={initialSiteCode}
            sites={sites}
          />
          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
            <form action={logoutAction}>
              <button className="button button-secondary" type="submit">
                Sign out
              </button>
            </form>
          </div>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof DriverManagementApiError && error.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/drivers" />
        </main>
      );
    }
    console.error(
      "FIS driver management reference data failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
}

export default function DriverManagementPage(props: Readonly<{ searchParams: SearchParams }>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <DriverManagementContent {...props} />
    </Suspense>
  );
}
