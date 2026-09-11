import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import RouteLoading from "@/components/app-shell/route-loading";
import VehicleStatusReportClient from "@/app/(fleet-operations)/vehicles/status/vehicle-status-report-client";
import {
  getVehicleStatusReport,
  VehicleStatusApiError,
} from "@/lib/api/vehicles/api-vehicle-status";
import { getSession } from "@/lib/auth/session";

const VEHICLE_MANAGEMENT_PERMISSION = 1;
const REPORTS_ROLE = "Reports";

type VehicleStatusReportPageProps = {
  routePath?: "/vehicles/status" | "/Vehicles/VehicleStatus.aspx";
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

function hasReportsRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(REPORTS_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function isUnauthorizedError(error: unknown) {
  return error instanceof VehicleStatusApiError && error.reason === "unauthorized";
}

function StatusCard({
  title,
  message,
  href,
}: Readonly<{ title: string; message: string; href: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">{title}</p>
      <h2>{message}</h2>
      <div className="button-row">
        <Link className="button button-primary" href={href}>
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

async function VehicleStatusReportContent({ routePath }: Readonly<{ routePath: string }>) {
  try {
    const report = await getVehicleStatusReport();

    return (
      <VehicleStatusReportClient
        initialReport={report}
        sites={report.sites}
        types={report.types}
        makes={report.makes.map((make) => ({ code: make.code, name: make.description }))}
      />
    );
  } catch (error) {
    if (isUnauthorizedError(error)) {
      return <SessionRecovery returnPath={routePath} />;
    }

    console.error(
      "FIS vehicle status report initial load failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <StatusCard
        title="API unavailable"
        message="Vehicle status information could not be loaded."
        href={routePath}
      />
    );
  }
}

async function VehicleStatusReportPageContent({
  routePath = "/vehicles/status",
}: VehicleStatusReportPageProps) {
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
        <StatusCard
          title="API unavailable"
          message="The sign-in service is temporarily unavailable."
          href={routePath}
        />
      </main>
    );
  }

  if (!hasVehicleManagementPermission(session.accessLevel)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="Access restricted"
          message="You do not have permission to view vehicle status reports."
          href="/vehicles"
        />
      </main>
    );
  }

  if (!hasReportsRole(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="Access restricted"
          message="You do not have permission to view vehicle status reports."
          href="/vehicles"
        />
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="vehicle-status-report-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Vehicle reports</p>
            <h1 id="vehicle-status-report-title">Vehicle List by Status Selection</h1>
            <p>All New and In Service vehicles with live filters.</p>
          </div>
          <Link className="button button-secondary" href="/vehicles">
            Vehicle Master
          </Link>
        </header>
        <Suspense fallback={<RouteLoading />}>
          <VehicleStatusReportContent routePath={routePath} />
        </Suspense>
      </section>
    </main>
  );
}

export default function VehicleStatusReportPage(props: VehicleStatusReportPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <VehicleStatusReportPageContent {...props} />
    </Suspense>
  );
}
