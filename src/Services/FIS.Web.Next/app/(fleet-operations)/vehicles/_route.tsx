import Link from "next/link";
import { CarFront } from "lucide-react";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import ModulePageHeader from "@/components/app-shell/module-page-header";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import RouteLoading from "@/components/app-shell/route-loading";
import VehicleMasterClient from "@/app/(fleet-operations)/vehicles/vehicle-master-client";
import {
  hasVehicleInceptionAuthorizerRole,
  hasVehicleInceptionCapturerRole,
  hasVehicleMasterRole,
  hasRole,
} from "@/app/(fleet-operations)/vehicles/access";
import {
  getVehicleSnapshotPage,
  VehicleApiError,
  type VehicleSnapshotPage,
} from "@/lib/api/vehicles/api-vehicles";
import { getSession } from "@/lib/auth/session";

export type VehicleMasterPageProps = {
  searchParams: Promise<{ page?: string | string[] }>;
  routePath?: "/vehicles" | "/vehicle-orders" | "/Master-File/Vehicle_Master.aspx";
  pageTitle?: string;
  pageDescription?: string;
};

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Vehicle Master.</h2>
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
      <h2>Your vehicle snapshot could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/vehicles">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

const VehicleMasterContent = renderVehicleMasterContent;

async function renderVehicleMasterContent({ searchParams, routePath }: VehicleMasterPageProps) {
  await connection();
  const currentRoute = routePath ?? "/vehicles";
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath={currentRoute} />;
  }

  if (session.status === "unavailable") {
    return <ApiUnavailable />;
  }

  if (!hasVehicleMasterRole(session.roles)) {
    return <AccessRestricted />;
  }

  const query = await searchParams;
  const pageValue = Array.isArray(query.page) ? query.page[0] : query.page;
  const requestedPage = Number.parseInt(pageValue ?? "1", 10);
  const page = Number.isFinite(requestedPage) ? requestedPage : 1;

  let pageData: VehicleSnapshotPage;
  let snapshotError: string | undefined;
  try {
    pageData = await getVehicleSnapshotPage(page);
  } catch (error) {
    if (error instanceof VehicleApiError && error.reason === "unauthorized") {
      return <SessionRecovery returnPath={currentRoute} />;
    }

    if (error instanceof VehicleApiError && error.reason === "forbidden") {
      return <AccessRestricted />;
    }

    console.error(
      "FIS vehicle master request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    // The legacy Vehicle Master menu itself is not dependent on the optional
    // overview snapshot. Keep Add/Edit and the maintenance entry points usable
    // when a restored database has a snapshot/report compatibility problem;
    // show the operator the bounded failure on the overview instead of hiding
    // the entire module behind a generic API-unavailable page.
    pageData = {
      rows: [],
      contractsByVmf: {},
      page: 1,
      pageSize: 24,
      totalRecords: 0,
      totalPages: 1,
    };
    snapshotError = error instanceof VehicleApiError ? error.message : undefined;
  }

  const canCaptureInception = hasVehicleInceptionCapturerRole(session.roles);
  const canAuthorizeInception = hasVehicleInceptionAuthorizerRole(session.roles);

  return (
    <>
      <VehicleMasterClient
        pageData={pageData}
        routePath={currentRoute}
        menu={{
          canCaptureInception,
          canAuthorizeInception,
          canMaintainVehicleMaster: true,
          canViewDemoVehicles: hasRole(session.roles, "demo vehicles"),
        }}
        snapshotError={snapshotError}
      />
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/home">
          Home
        </Link>
        <form action={logoutAction}>
          <button className="button button-secondary" type="submit">
            Sign out
          </button>
        </form>
      </div>
    </>
  );
}

export default function VehicleMasterPage({
  pageTitle = "Vehicle Master Menu",
  pageDescription = "Vehicle master navigation and maintenance options.",
  ...props
}: VehicleMasterPageProps) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="vehicle-master-title">
        <ModulePageHeader
          icon={CarFront}
          eyebrow="Fleet administration"
          title={pageTitle}
          titleId="vehicle-master-title"
          description={pageDescription}
        />
        <Suspense fallback={<RouteLoading />}>
          <VehicleMasterContent {...props} />
        </Suspense>
      </section>
    </main>
  );
}
