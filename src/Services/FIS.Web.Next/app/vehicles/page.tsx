import Link from "next/link";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import VehicleMasterClient from "@/app/vehicles/vehicle-master-client";
import { getVehicleSnapshotPage, VehicleApiError } from "@/lib/api-vehicles";
import { getSession } from "@/lib/session";

const VEHICLE_MANAGEMENT_PERMISSION = 1;

export type VehicleMasterPageProps = {
  searchParams: Promise<{ page?: string | string[] }>;
  routePath?: "/vehicles" | "/vehicle-orders" | "/Master-File/Vehicle_Master.aspx";
  pageTitle?: string;
  pageDescription?: string;
};

function VehicleMasterFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading vehicle master...</p>
    </div>
  );
}

function hasVehicleManagementPermission(accessLevel?: string) {
  if (!accessLevel) {
    return false;
  }

  try {
    return (BigInt(accessLevel) & BigInt(VEHICLE_MANAGEMENT_PERMISSION)) === BigInt(VEHICLE_MANAGEMENT_PERMISSION);
  } catch {
    return false;
  }
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Vehicle Master.</h2>
    </section>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>Your vehicle snapshot could not be loaded.</h2>
      <p className="muted-copy">The application is still running. Retry when the FIS API is available.</p>
      <div className="button-row">
        <Link className="button button-primary" href="/vehicles">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

async function VehicleMasterContent({ searchParams, routePath }: VehicleMasterPageProps) {
  const currentRoute = routePath ?? "/vehicles";
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath={currentRoute} />;
  }

  if (session.status === "unavailable") {
    return <ApiUnavailable />;
  }

  if (!hasVehicleManagementPermission(session.accessLevel)) {
    return <AccessRestricted />;
  }

  const query = await searchParams;
  const pageValue = Array.isArray(query.page) ? query.page[0] : query.page;
  const requestedPage = Number.parseInt(pageValue ?? "1", 10);
  const page = Number.isFinite(requestedPage) ? requestedPage : 1;

  let pageData;
  try {
    pageData = await getVehicleSnapshotPage(page);
  } catch (error) {
    if (error instanceof VehicleApiError && error.reason === "unauthorized") {
      return <SessionRecovery returnPath={currentRoute} />;
    }

    console.error("FIS vehicle master request failed", error instanceof Error ? error.message : "unknown error");
    return <ApiUnavailable />;
  }

  const canCaptureInception = hasRole(session.roles, "vehicle inception capturer");
  const canAuthorizeInception = hasRole(session.roles, "vehicle inception authorizer");
  const hasInceptionRole = canCaptureInception || canAuthorizeInception;

  return (
    <>
      <VehicleMasterClient
        pageData={pageData}
        routePath={currentRoute}
        menu={{
          canCaptureInception: hasInceptionRole ? canCaptureInception : true,
          canAuthorizeInception: hasInceptionRole ? canAuthorizeInception : true,
          canMaintainVehicleMaster: true,
          canViewDemoVehicles: hasRole(session.roles, "demo vehicles"),
        }}
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
    </>
  );
}

export default function VehicleMasterPage({ pageTitle = "Vehicle Master Menu", pageDescription = "Vehicle master navigation and maintenance options.", ...props }: VehicleMasterPageProps) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="vehicle-master-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Fleet administration</p>
            <h1 id="vehicle-master-title">{pageTitle}</h1>
            <p>{pageDescription}</p>
          </div>
        </header>
        <Suspense fallback={<VehicleMasterFallback />}>
          <VehicleMasterContent {...props} />
        </Suspense>
      </section>
    </main>
  );
}
