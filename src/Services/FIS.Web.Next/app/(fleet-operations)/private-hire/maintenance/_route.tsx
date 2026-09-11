import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import ApiUnavailableCard from "@/components/app-shell/api-unavailable-card";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  PrivateHireContractorMaintenanceView,
  PrivateHireVehicleMaintenanceView,
} from "@/app/(fleet-operations)/private-hire/maintenance/private-hire-maintenance-views";
import { queryValue } from "@/app/(fleet-operations)/private-hire/_utils";
import {
  getPrivateHireContractor,
  getPrivateHireContractors,
  getPrivateHireContractorsPage,
  getPrivateHireVehicle,
  getPrivateHirePage,
  PrivateHireApiError,
  DEFAULT_PRIVATE_HIRE_PAGE_SIZE,
} from "@/lib/api/fleet-operations/api-private-hire";
import { getModels } from "@/lib/api/reference-data/api-models";
import { getSites } from "@/lib/api/reference-data/api-sites";
import { getSession } from "@/lib/auth/session";

const ROLE = "Private Hire Vehicles";
type Mode = "add" | "edit" | "delete" | "contractor-add" | "contractor-edit" | "contractor-delete";

function hasRole(roles: readonly string[]) {
  return roles.some((role) => role.localeCompare(ROLE, undefined, { sensitivity: "accent" }) === 0);
}

function modeValue(value: string): Mode {
  return [
    "add",
    "edit",
    "delete",
    "contractor-add",
    "contractor-edit",
    "contractor-delete",
  ].includes(value)
    ? (value as Mode)
    : "add";
}

function requestedPage(value: string) {
  const page = Number(value);
  return Number.isSafeInteger(page) && page > 0 ? page : 1;
}

const PrivateHireMaintenancePageContent = renderPrivateHireMaintenancePageContent;

async function renderPrivateHireMaintenancePageContent({
  searchParams,
  forcedMode,
  routePath = "/private-hire/maintenance",
}: Readonly<{
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
  forcedMode?: Mode;
  routePath?: string;
}>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (!hasRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to maintain Private Hire Vehicles.</h2>
        </section>
      </main>
    );

  const query = searchParams ? await searchParams : {};
  const mode = forcedMode ?? modeValue(queryValue(query.mode));
  const search = queryValue(query.search).trim();
  const page = requestedPage(queryValue(query.page));
  const requestedVehicle = Number(queryValue(query.phvCode));
  const phvCode =
    Number.isInteger(requestedVehicle) && requestedVehicle > 0 ? requestedVehicle : null;
  const requestedContractor = Number(queryValue(query.contractorId));
  const contractorId =
    Number.isInteger(requestedContractor) && requestedContractor > 0 ? requestedContractor : null;

  try {
    if (mode.startsWith("contractor")) {
      const [contractorPage, selected] = await Promise.all([
        getPrivateHireContractorsPage({ page, pageSize: DEFAULT_PRIVATE_HIRE_PAGE_SIZE }),
        contractorId ? getPrivateHireContractor(contractorId) : Promise.resolve(null),
      ]);
      return (
        <PrivateHireContractorMaintenanceView
          query={query}
          routePath={routePath}
          mode={mode}
          contractorPage={contractorPage}
          selected={selected}
          contractorId={contractorId}
        />
      );
    }

    const [vehiclePage, selected] = await Promise.all([
      mode === "add"
        ? Promise.resolve(null)
        : getPrivateHirePage({
            page,
            pageSize: DEFAULT_PRIVATE_HIRE_PAGE_SIZE,
            searchTerm: search || undefined,
          }),
      phvCode ? getPrivateHireVehicle(phvCode) : Promise.resolve(null),
    ]);
    const shouldLoadVehicleLookups = mode === "add" || (mode === "edit" && selected !== null);
    const [models, sites, contractors] = shouldLoadVehicleLookups
      ? await Promise.all([getModels(), getSites(), getPrivateHireContractors()])
      : [[], [], []];
    return (
      <PrivateHireVehicleMaintenanceView
        query={query}
        routePath={routePath}
        mode={mode}
        search={search}
        vehiclePage={vehiclePage}
        selected={selected}
        models={models}
        sites={sites}
        contractors={contractors}
      />
    );
  } catch (error) {
    if (error instanceof PrivateHireApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailableForMaintenance path={routePath} />
      </main>
    );
  }
}

export function PrivateHireMaintenancePage(
  props: Readonly<{
    searchParams?: Promise<Record<string, string | string[] | undefined>>;
    forcedMode?: Mode;
    routePath?: string;
  }>,
) {
  return (
    <StreamedRoute>
      <PrivateHireMaintenancePageContent {...props} />
    </StreamedRoute>
  );
}

function ApiUnavailableForMaintenance({ path }: Readonly<{ path: string }>) {
  return (
    <ApiUnavailableCard
      message="Private Hire maintenance could not be loaded."
      retryHref={path}
      secondaryHref="/login"
      secondaryLabel="Sign in"
      showIcon={false}
    />
  );
}

export default function PrivateHireMaintenancePageRoute({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <PrivateHireMaintenancePage searchParams={searchParams} />;
}
