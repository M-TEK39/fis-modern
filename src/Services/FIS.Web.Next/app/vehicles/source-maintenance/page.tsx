import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { logoutAction } from "@/app/actions/auth";
import { saveVehicleSourceAction } from "@/app/vehicles/source-maintenance/actions";
import VehicleSourceClient from "@/app/vehicles/source-maintenance/vehicle-source-client";
import {
  getVehicleSources,
  VehicleSourceApiError,
  type VehicleSourcePage,
} from "@/lib/api-vehicle-sources";
import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

const VEHICLE_MANAGEMENT_PERMISSION = 1;

const ROUTE_PATHS = [
  "/vehicles/source-maintenance",
  "/Master-File/Vehicle_Source.aspx",
  "/Master-File/Add_Vehicle_Source.aspx",
  "/Master-File/Edit_Vehicle_Source.aspx",
  "/Master-File/Edit_Vehicle_Source_2.aspx",
] as const;

type VehicleSourcePageProps = {
  searchParams?: Promise<{ saved?: string | string[] }>;
  routePath?: (typeof ROUTE_PATHS)[number];
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

function StatusCard({ title, message }: Readonly<{ title: string; message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">{title}</p>
      <h2>{message}</h2>
    </section>
  );
}

export default async function VehicleSourcePage({
  searchParams,
  routePath = "/vehicles/source-maintenance",
}: VehicleSourcePageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard title="API unavailable" message="The vehicle source service is unavailable." />
      </main>
    );
  }

  if (!hasVehicleManagementPermission(session.accessLevel)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="Access restricted"
          message="You do not have permission to maintain vehicle sources."
        />
      </main>
    );
  }

  let sourcePage: VehicleSourcePage;
  try {
    sourcePage = await getVehicleSources();
  } catch (error) {
    if (error instanceof VehicleSourceApiError && error.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    }

    console.error(
      "FIS vehicle source initial load failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="API unavailable"
          message="Vehicle source information could not be loaded."
        />
      </main>
    );
  }

  const query = searchParams ? await searchParams : {};
  const saved = getQueryValue(query.saved) === "1";

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="vehicle-source-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Vehicle master maintenance</p>
            <h1 id="vehicle-source-title">Vehicle Source Maintenance</h1>
            <p>Add or edit vehicle source records.</p>
          </div>
          <Link className="button button-secondary" href="/vehicles">
            Vehicle Master
          </Link>
        </header>

        {saved ? (
          <div className="notice notice-success" role="status">
            <span aria-hidden="true">✓</span>
            <span>Vehicle source saved successfully.</span>
          </div>
        ) : null}

        <VehicleSourceClient
          sources={sourcePage.items}
          capabilities={sourcePage.capabilities}
          action={saveVehicleSourceAction}
          returnPath={routePath}
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
