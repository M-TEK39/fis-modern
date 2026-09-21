import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import RouteLoading from "@/components/app-shell/route-loading";
import AccessRestrictedCard from "@/components/app-shell/access-restricted-card";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import VehicleCreateClient from "@/app/(fleet-operations)/vehicles/create/vehicle-create-client";
import AuthorizedPrintQueue from "@/app/(fleet-operations)/vehicles/authorize/authorized-print-queue";
import {
  hasVehicleInceptionCapturerRole,
} from "@/app/(fleet-operations)/vehicles/access";
import {
  VehicleCreateApiError,
  getVehicleCreateReferenceData,
} from "@/lib/api/vehicles/api-vehicle-create";
import {
  getAuthorizedVehiclesQueue,
  VehicleAuthorizationApiError,
} from "@/lib/api/vehicles/api-vehicle-authorization";
import { getSession } from "@/lib/auth/session";

type VehicleCreateSearchParams = Record<string, string | string[] | undefined>;

function getPageValue(query: VehicleCreateSearchParams, key: string) {
  const value = query[key];
  const firstValue = Array.isArray(value) ? value[0] : value;
  const parsed = Number.parseInt(firstValue ?? "1", 10);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : 1;
}

function AccessRestricted() {
  return <AccessRestrictedCard message="You do not have permission to capture a new vehicle." />;
}

function ApiUnavailable({ detail }: Readonly<{ detail?: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>Vehicle reference data could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      {detail ? <p className="muted-copy">Details: {detail}</p> : null}
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

async function VehicleCreatePageContent({
  searchParams,
}: Readonly<{
  searchParams: Promise<VehicleCreateSearchParams>;
}>) {
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

  if (!hasVehicleInceptionCapturerRole(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  try {
    const query = await searchParams;
    const [referenceData, authorizedQueue] = await Promise.all([
      getVehicleCreateReferenceData(),
      getAuthorizedVehiclesQueue(getPageValue(query, "authorizedPage")),
    ]);
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
          <AuthorizedPrintQueue queue={authorizedQueue} />
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
    if (
      (error instanceof VehicleCreateApiError || error instanceof VehicleAuthorizationApiError) &&
      error.reason === "unauthorized"
    ) {
      return <SessionRecovery returnPath="/vehicles/create" />;
    }

    if (
      (error instanceof VehicleCreateApiError || error instanceof VehicleAuthorizationApiError) &&
      error.reason === "forbidden"
    ) {
      return (
        <main className="page-shell vehicle-page-shell">
          <AccessRestricted />
        </main>
      );
    }

    console.error(
      "FIS vehicle create reference data request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable
          detail={
            error instanceof VehicleCreateApiError || error instanceof VehicleAuthorizationApiError
              ? error.message
              : undefined
          }
        />
      </main>
    );
  }
}

export default function VehicleCreatePage(
  props: Readonly<{
    searchParams: Promise<VehicleCreateSearchParams>;
  }>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <VehicleCreatePageContent {...props} />
    </Suspense>
  );
}
