import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import RouteLoading from "@/components/app-shell/route-loading";
import StatusMaintenanceClient from "@/app/(fleet-operations)/vehicles/status-maintenance/status-maintenance-client";
import {
  getSitesForVehicleStatus,
  getVehicleForStatus,
  searchVehiclesForStatus,
  VEHICLE_STATUS_OPTIONS,
  VehicleStatusApiError,
  type VehicleStatusSite,
  type VehicleStatusVehicle,
} from "@/lib/api/vehicles/api-vehicle-status";
import { getSession } from "@/lib/auth/session";

const VEHICLE_MANAGEMENT_PERMISSION = 1;
const VEHICLE_STATUS_ROLES = ["Acquisition", "Logistics", "TSS", "Workshop"];

type StatusMaintenancePageProps = {
  searchParams: Promise<{
    GGNumber?: string | string[];
    GGnum?: string | string[];
    vmfCode?: string | string[];
    ReferringURL?: string | string[];
    returnUrl?: string | string[];
    updated?: string | string[];
  }>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
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

function hasVehicleStatusRole(roles: readonly string[]) {
  return VEHICLE_STATUS_ROLES.some((role) =>
    roles.some(
      (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
    ),
  );
}

function safeReturnUrl(value: string | undefined) {
  return value?.startsWith("/") && !value.startsWith("//") ? value : "";
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to maintain vehicle statuses.</h2>
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
      <h2>Vehicle status information could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/vehicles/status-maintenance">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

function queryInitialVehicle(searchParams: Awaited<StatusMaintenancePageProps["searchParams"]>) {
  const rawVmfCode = getQueryValue(searchParams.vmfCode)?.trim() || "";
  const parsedVmfCode = Number(rawVmfCode);
  const rawGgNumber =
    getQueryValue(searchParams.GGNumber)?.trim() || getQueryValue(searchParams.GGnum)?.trim() || "";

  return {
    vmfCode: Number.isInteger(parsedVmfCode) && parsedVmfCode > 0 ? parsedVmfCode : null,
    ggNumber: rawGgNumber,
  };
}

async function resolveInitialVehicle(
  vmfCode: number | null,
  ggNumber: string,
): Promise<VehicleStatusVehicle | null> {
  if (vmfCode) {
    return getVehicleForStatus(vmfCode);
  }

  if (!ggNumber) {
    return null;
  }

  const matches = await searchVehiclesForStatus(ggNumber);
  const exact = matches.find(
    (vehicle) =>
      vehicle.fleetNumber?.localeCompare(ggNumber, undefined, { sensitivity: "accent" }) === 0,
  );
  const vehicle = exact || matches[0];
  return vehicle ? getVehicleForStatus(vehicle.vmfCode) : null;
}

async function StatusMaintenancePageContent({ searchParams }: StatusMaintenancePageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/vehicles/status-maintenance" />
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

  if (
    !hasVehicleManagementPermission(session.accessLevel) ||
    !hasVehicleStatusRole(session.roles)
  ) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  const query = await searchParams;
  const { vmfCode, ggNumber } = queryInitialVehicle(query);
  const returnUrl = safeReturnUrl(
    getQueryValue(query.returnUrl)?.trim() || getQueryValue(query.ReferringURL)?.trim(),
  );
  const updated = getQueryValue(query.updated) === "1";

  let selectedVehicle: VehicleStatusVehicle | null = null;
  let sites: VehicleStatusSite[] = [];

  try {
    selectedVehicle = await resolveInitialVehicle(vmfCode, ggNumber);
    if (selectedVehicle) {
      try {
        sites = await getSitesForVehicleStatus();
      } catch (error) {
        console.error(
          "FIS vehicle status site lookup failed",
          error instanceof Error ? error.message : "unknown error",
        );
      }
    }
  } catch (error) {
    if (error instanceof VehicleStatusApiError && error.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/vehicles/status-maintenance" />
        </main>
      );
    }

    console.error(
      "FIS vehicle status initial load failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section
        className="vehicle-card vehicle-status-maintenance-card"
        aria-labelledby="vehicle-status-title"
      >
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Vehicle master maintenance</p>
            <h1 id="vehicle-status-title">Vehicle Status Maintenance</h1>
            <p>Select a vehicle for status management and capture the next status.</p>
          </div>
          <Link className="button button-secondary" href="/vehicles">
            Vehicle Master
          </Link>
        </header>

        <StatusMaintenanceClient
          initialSearchTerm={ggNumber}
          initialVehicle={selectedVehicle}
          initialSites={sites}
          initialUpdated={updated}
          initialReturnUrl={returnUrl}
          statusOptions={VEHICLE_STATUS_OPTIONS}
        />

        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href={returnUrl || "/vehicles"}>
            {returnUrl ? "Return to Previous Page" : "Back to Vehicle Master"}
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

export default function StatusMaintenancePage(props: StatusMaintenancePageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <StatusMaintenancePageContent {...props} />
    </Suspense>
  );
}
