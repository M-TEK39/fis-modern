import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import {
  createFuelTypeAction,
  createLicenseTypeAction,
  createUnitOfMeasureAction,
  createVehicleTypeAction,
  deleteFuelTypeAction,
  deleteLicenseTypeAction,
  deleteUnitOfMeasureAction,
  deleteVehicleTypeAction,
  updateFuelTypeAction,
  updateLicenseTypeAction,
  updateUnitOfMeasureAction,
  updateVehicleTypeAction,
} from "@/app/(administration)/reference-data/actions";
import ReferenceDataClient from "@/app/(administration)/reference-data/reference-data-client";
import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  getFuelTypes,
  getLicenseTypes,
  getUnitsOfMeasure,
  getVehicleTypes,
  ReferenceDataApiError,
} from "@/lib/api/reference-data/api-reference-data";
import { getSession } from "@/lib/auth/session";

type Tab = "types" | "fueltypes" | "units" | "licenses";
type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
function tabValue(value: string | undefined): Tab {
  return value === "fueltypes" || value === "units" || value === "licenses" ? value : "types";
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to maintain reference data.</h2>
      <Link className="button button-secondary" href="/home">
        Home
      </Link>
    </section>
  );
}
function ApiUnavailable({ tab }: Readonly<{ tab: Tab }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Reference data could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href={`/reference-data?tab=${tab}`}>
          Try again
        </Link>
        <Link className="button button-secondary" href="/home">
          Home
        </Link>
      </div>
    </section>
  );
}

export default async function ReferenceDataPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/reference-data" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable tab="types" />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const activeTab = tabValue(queryValue(query.tab));
  try {
    if (activeTab === "fueltypes")
      return (
        <ReferenceDataClient
          activeTab={activeTab}
          records={await getFuelTypes()}
          createAction={createFuelTypeAction}
          updateAction={updateFuelTypeAction}
          deleteAction={deleteFuelTypeAction}
          saved={queryValue(query.saved)}
          error={queryValue(query.error)}
        />
      );
    if (activeTab === "units")
      return (
        <ReferenceDataClient
          activeTab={activeTab}
          records={await getUnitsOfMeasure()}
          createAction={createUnitOfMeasureAction}
          updateAction={updateUnitOfMeasureAction}
          deleteAction={deleteUnitOfMeasureAction}
          saved={queryValue(query.saved)}
          error={queryValue(query.error)}
        />
      );
    if (activeTab === "licenses")
      return (
        <ReferenceDataClient
          activeTab={activeTab}
          records={await getLicenseTypes()}
          createAction={createLicenseTypeAction}
          updateAction={updateLicenseTypeAction}
          deleteAction={deleteLicenseTypeAction}
          saved={queryValue(query.saved)}
          error={queryValue(query.error)}
        />
      );
    return (
      <ReferenceDataClient
        activeTab={activeTab}
        records={await getVehicleTypes()}
        createAction={createVehicleTypeAction}
        updateAction={updateVehicleTypeAction}
        deleteAction={deleteVehicleTypeAction}
        saved={queryValue(query.saved)}
        error={queryValue(query.error)}
      />
    );
  } catch (error) {
    if (error instanceof ReferenceDataApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`/reference-data?tab=${activeTab}`} />
        </main>
      );
    console.error(
      "FIS reference-data request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable tab={activeTab} />
      </main>
    );
  }
}
