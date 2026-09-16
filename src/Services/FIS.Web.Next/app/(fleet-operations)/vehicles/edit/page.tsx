import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import RouteLoading from "@/components/app-shell/route-loading";
import AccessRestrictedCard from "@/components/app-shell/access-restricted-card";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { searchVehicleEditAction } from "@/app/(fleet-operations)/vehicles/edit/actions";
import { hasVehicleMasterRole } from "@/app/(fleet-operations)/vehicles/access";
import VehicleEditSearchClient from "@/app/(fleet-operations)/vehicles/edit/vehicle-edit-search-client";
import { getSession } from "@/lib/auth/session";

type VehicleEditSearchPageProps = {
  searchParams: Promise<{ searchTerm?: string | string[]; updated?: string | string[] }>;
};

function AccessRestricted() {
  return (
    <AccessRestrictedCard message="You do not have permission to edit Vehicle Master records." />
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

  if (!hasVehicleMasterRole(session.roles)) {
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
