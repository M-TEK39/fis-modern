import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import RouteLoading from "@/components/app-shell/route-loading";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { searchVehicleEditAction } from "@/app/(fleet-operations)/vehicles/edit/actions";
import VehicleEditSearchClient from "@/app/(fleet-operations)/vehicles/edit/vehicle-edit-search-client";
import { getSession } from "@/lib/auth/session";

const VEHICLE_MANAGEMENT_PERMISSION = 1;

type VehicleEditSearchPageProps = {
  searchParams: Promise<{ searchTerm?: string | string[]; updated?: string | string[] }>;
};

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

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to edit Vehicle Master records.</h2>
      <div className="button-row">
        <Link className="button button-secondary" href="/vehicles">
          Back to Vehicle Master
        </Link>
      </div>
    </section>
  );
}

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function VehicleEditSearchPageContent({ searchParams }: VehicleEditSearchPageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath="/vehicles/edit" />;
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>The vehicle search service is unavailable.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
        </section>
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

  const query = await searchParams;
  const initialSearchTerm = getQueryValue(query.searchTerm)?.trim() || "";
  const updatedVmfCode = getQueryValue(query.updated)?.trim() || "";

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="vehicle-edit-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Vehicle master maintenance</p>
            <h1 id="vehicle-edit-title">Edit a Vehicle</h1>
            <p>Search by GG or GP number, then update the vehicle master record.</p>
          </div>
          <Link className="button button-secondary" href="/vehicles">
            Vehicle Master
          </Link>
        </header>
        <VehicleEditSearchClient
          initialQuery={initialSearchTerm}
          initialState={
            updatedVmfCode
              ? {
                  status: "success",
                  message: `Vehicle ${updatedVmfCode} was updated successfully.`,
                  results: [],
                }
              : undefined
          }
          searchAction={searchVehicleEditAction}
        />
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
}

export default function VehicleEditSearchPage(props: VehicleEditSearchPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <VehicleEditSearchPageContent {...props} />
    </Suspense>
  );
}
