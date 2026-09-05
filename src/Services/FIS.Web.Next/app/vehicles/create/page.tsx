import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import VehicleCreateClient from "@/app/vehicles/create/vehicle-create-client";
import { VehicleCreateApiError, getVehicleCreateReferenceData } from "@/lib/api-vehicle-create";
import { getSession } from "@/lib/session";

const VEHICLE_MANAGEMENT_PERMISSION = 1;

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
      <h2>You do not have permission to capture a new vehicle.</h2>
      <div className="button-row">
        <Link className="button button-secondary" href="/vehicles">
          Back to Vehicle Master
        </Link>
      </div>
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
      <h2>Vehicle reference data could not be loaded.</h2>
      <p className="muted-copy">The application is still running. Retry when the FIS API is available.</p>
      <div className="button-row">
        <Link className="button button-primary" href="/vehicles/create">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

export default async function VehicleCreatePage() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath="/vehicles/create" />;
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }

  if (!hasVehicleManagementPermission(session.accessLevel)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  const hasExplicitInceptionRoles = session.roles.some((role) =>
    ["vehicle inception capturer", "vehicle inception authorizer"].some(
      (candidate) => role.localeCompare(candidate, undefined, { sensitivity: "accent" }) === 0,
    ),
  );

  if (hasExplicitInceptionRoles && !hasRole(session.roles, "vehicle inception capturer")) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  try {
    const referenceData = await getVehicleCreateReferenceData();
    const today = new Date().toISOString().slice(0, 10);

    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="vehicle-create-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Vehicle master maintenance</p>
              <h1 id="vehicle-create-title">Add New Vehicle</h1>
              <p>Capture vehicle information for authorization.</p>
            </div>
            <Link className="button button-secondary" href="/vehicles">
              Vehicle Master
            </Link>
          </header>
          <VehicleCreateClient referenceData={referenceData} today={today} />
          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/vehicles">
              Back to Vehicle Master
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
    if (error instanceof VehicleCreateApiError && error.reason === "unauthorized") {
      return <SessionRecovery returnPath="/vehicles/create" />;
    }

    console.error("FIS vehicle create reference data request failed", error instanceof Error ? error.message : "unknown error");
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
}
