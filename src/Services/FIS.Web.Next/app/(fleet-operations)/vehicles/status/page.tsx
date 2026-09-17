import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import StatusCardView from "@/components/app-shell/status-card";
import RouteLoading from "@/components/app-shell/route-loading";
import VehicleStatusReportClient from "@/app/(fleet-operations)/vehicles/status/vehicle-status-report-client";
import { hasVehicleMasterRole } from "@/app/(fleet-operations)/vehicles/access";
import {
  getVehicleStatusReport,
  VehicleStatusApiError,
} from "@/lib/api/vehicles/api-vehicle-status";
import { getSession } from "@/lib/auth/session";

const REPORTS_ROLE = "Reports";

type VehicleStatusReportPageProps = {
  routePath?: "/vehicles/status" | "/Vehicles/VehicleStatus.aspx";
};

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
    <StatusCardView
      title={title}
      message={message}
      retryHref={href}
      secondaryHref="/login"
      secondaryLabel="Sign in"
      showIcon
    />
  );
}

async function VehicleStatusReportContent({
  canManageRemarks,
  routePath,
}: Readonly<{ canManageRemarks: boolean; routePath: string }>) {
  try {
    const report = await getVehicleStatusReport();

    return <VehicleStatusReportClient canManageRemarks={canManageRemarks} initialReport={report} />;
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
          <VehicleStatusReportContent
            canManageRemarks={hasVehicleMasterRole(session.roles)}
            routePath={routePath}
          />
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
