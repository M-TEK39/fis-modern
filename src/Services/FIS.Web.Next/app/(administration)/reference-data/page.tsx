import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

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
import RouteLoading from "@/components/app-shell/route-loading";
import { hasLegacyRole } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  DEFAULT_REFERENCE_DATA_PAGE_SIZE,
  getFuelTypesPage,
  getLicenseTypesPage,
  getUnitsOfMeasurePage,
  getVehicleTypesPage,
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

function pageValue(value: string | undefined) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : 1;
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

async function ReferenceDataContent({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
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
  if (!hasLegacyRole(session.roles, "Validation"))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const activeTab = tabValue(queryValue(query.tab));
  const page = pageValue(queryValue(query.page));
  try {
    if (activeTab === "fueltypes") {
      const result = await getFuelTypesPage({ page, pageSize: DEFAULT_REFERENCE_DATA_PAGE_SIZE });
      return (
        <ReferenceDataClient
          activeTab={activeTab}
          records={result.items}
          page={result.page}
          pageSize={result.pageSize}
          total={result.total}
          totalPages={result.totalPages}
          createAction={createFuelTypeAction}
          updateAction={updateFuelTypeAction}
          deleteAction={deleteFuelTypeAction}
          saved={queryValue(query.saved)}
          error={queryValue(query.error)}
        />
      );
    }
    if (activeTab === "units") {
      const result = await getUnitsOfMeasurePage({
        page,
        pageSize: DEFAULT_REFERENCE_DATA_PAGE_SIZE,
      });
      return (
        <ReferenceDataClient
          activeTab={activeTab}
          records={result.items}
          page={result.page}
          pageSize={result.pageSize}
          total={result.total}
          totalPages={result.totalPages}
          createAction={createUnitOfMeasureAction}
          updateAction={updateUnitOfMeasureAction}
          deleteAction={deleteUnitOfMeasureAction}
          saved={queryValue(query.saved)}
          error={queryValue(query.error)}
        />
      );
    }
    if (activeTab === "licenses") {
      const result = await getLicenseTypesPage({
        page,
        pageSize: DEFAULT_REFERENCE_DATA_PAGE_SIZE,
      });
      return (
        <ReferenceDataClient
          activeTab={activeTab}
          records={result.items}
          page={result.page}
          pageSize={result.pageSize}
          total={result.total}
          totalPages={result.totalPages}
          createAction={createLicenseTypeAction}
          updateAction={updateLicenseTypeAction}
          deleteAction={deleteLicenseTypeAction}
          saved={queryValue(query.saved)}
          error={queryValue(query.error)}
        />
      );
    }
    const result = await getVehicleTypesPage({ page, pageSize: DEFAULT_REFERENCE_DATA_PAGE_SIZE });
    return (
      <ReferenceDataClient
        activeTab={activeTab}
        records={result.items}
        page={result.page}
        pageSize={result.pageSize}
        total={result.total}
        totalPages={result.totalPages}
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

export default function ReferenceDataPage(props: Readonly<{ searchParams: SearchParams }>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <ReferenceDataContent {...props} />
    </Suspense>
  );
}
