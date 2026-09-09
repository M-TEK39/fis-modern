import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import VehicleAuthorizationClient from "@/app/vehicles/authorize/vehicle-authorization-client";
import {
  getVehicleAuthorizationQueues,
  VehicleAuthorizationApiError,
} from "@/lib/api-vehicle-authorization";
import { getSession } from "@/lib/session";

type VehicleAuthorizationSearchParams = Record<string, string | string[] | undefined>;

function getPageValue(query: VehicleAuthorizationSearchParams, key: string) {
  const value = query[key];
  const firstValue = Array.isArray(value) ? value[0] : value;
  const parsed = Number.parseInt(firstValue ?? "1", 10);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : 1;
}

const VEHICLE_MANAGEMENT_PERMISSION = 1;
const INCEPTION_ROLES = ["vehicle inception capturer", "vehicle inception authorizer"];

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function hasVehicleManagementPermission(accessLevel?: string) {
  if (!accessLevel) {
    return false;
  }

  try {
    return (
      (BigInt(accessLevel) & BigInt(VEHICLE_MANAGEMENT_PERMISSION)) ===
      BigInt(VEHICLE_MANAGEMENT_PERMISSION)
    );
  } catch {
    return false;
  }
}

function hasExplicitInceptionRole(roles: readonly string[]) {
  return roles.some((role) => INCEPTION_ROLES.some((candidate) => hasRole([role], candidate)));
}

function canAuthorize(roles: readonly string[], accessLevel?: string) {
  if (!hasVehicleManagementPermission(accessLevel)) {
    return false;
  }

  return hasRole(roles, "vehicle inception authorizer") || !hasExplicitInceptionRole(roles);
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to authorize captured vehicles.</h2>
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
      <h2>Vehicle authorization queues could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/vehicles/authorize">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

export default async function VehicleAuthorizationPage({
  searchParams,
}: Readonly<{
  searchParams: Promise<VehicleAuthorizationSearchParams>;
}>) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/vehicles/authorize" />
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

  if (!canAuthorize(session.roles, session.accessLevel)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  try {
    const query = await searchParams;
    const queues = await getVehicleAuthorizationQueues({
      pendingPage: getPageValue(query, "pendingPage"),
      rejectedPage: getPageValue(query, "rejectedPage"),
      authorizedPage: getPageValue(query, "authorizedPage"),
    });

    return (
      <main className="page-shell vehicle-page-shell">
        <section
          className="vehicle-card vehicle-authorization-card"
          aria-labelledby="vehicle-authorization-title"
        >
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Vehicle master workflow</p>
              <h1 id="vehicle-authorization-title">Authorize Captured Vehicle Information</h1>
              <p>Review rejected, awaiting, and authorized vehicle inception records.</p>
            </div>
          </header>
          <VehicleAuthorizationClient
            queues={queues}
            query={query}
            currentUserAccessCode={session.userAccessCode}
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
    if (error instanceof VehicleAuthorizationApiError && error.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/vehicles/authorize" />
        </main>
      );
    }

    console.error(
      "FIS vehicle authorization queue request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
}
