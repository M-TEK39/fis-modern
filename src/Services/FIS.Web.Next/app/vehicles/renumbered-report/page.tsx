import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import SessionRecovery from "@/app/home/session-recovery";
import {
  getRenumberedVehicleReport,
  VehicleApiError,
  type RenumberedVehicleReportRow,
} from "@/lib/api-vehicles";
import { getSession } from "@/lib/session";

const PAGE_SIZE = 12;

type RenumberedReportPageProps = {
  searchParams: Promise<{ page?: string | string[] }>;
  routePath?: "/vehicles/renumbered-report" | "/Master-File/RPT_renumbered.aspx";
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrDash(value: string | null) {
  return value || "-";
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to view renumbered vehicles.</h2>
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
      <h2>The renumbered vehicle report could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/vehicles/renumbered-report">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

function ReportTable({ rows }: Readonly<{ rows: RenumberedVehicleReportRow[] }>) {
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <thead>
          <tr>
            <th scope="col">No.</th>
            <th scope="col">Old GG Number</th>
            <th scope="col">Status</th>
            <th scope="col">New GG Number</th>
            <th scope="col">Status</th>
            <th scope="col">Fleet Number History of Renumbered</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={`${row.oldVmfCode}-${row.newFleetNumber ?? "replacement"}`}>
              <td>{index + 1}</td>
              <td>
                <Link
                  href={`/vehicles/recovered?GGnum=${encodeURIComponent(row.oldFleetNumber ?? "")}`}
                >
                  {valueOrDash(row.oldFleetNumber)}
                </Link>
              </td>
              <td>{valueOrDash(row.oldStatusDescription)}</td>
              <td>{valueOrDash(row.newFleetNumber)}</td>
              <td>{valueOrDash(row.newStatusDescription)}</td>
              <td>-</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Pagination({
  page,
  totalPages,
  routePath,
}: Readonly<{ page: number; totalPages: number; routePath: string }>) {
  const pageHref = (nextPage: number) =>
    nextPage === 1 ? routePath : `${routePath}?page=${nextPage}`;

  if (totalPages <= 1) {
    return null;
  }

  return (
    <nav className="vehicle-pagination" aria-label="Renumbered vehicle report pagination">
      {page <= 1 ? (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Previous
        </span>
      ) : (
        <Link className="vehicle-pagination-button" href={pageHref(page - 1)}>
          Previous
        </Link>
      )}
      <span className="vehicle-pagination-meta" aria-live="polite">
        Page {page} of {totalPages}
      </span>
      {page >= totalPages ? (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Next
        </span>
      ) : (
        <Link className="vehicle-pagination-button" href={pageHref(page + 1)}>
          Next
        </Link>
      )}
    </nav>
  );
}

export default async function RenumberedReportPage({
  searchParams,
  routePath = "/vehicles/renumbered-report",
}: RenumberedReportPageProps) {
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
        <ApiUnavailable />
      </main>
    );
  }

  if (!hasRole(session.roles, "Demo Vehicles")) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  let rows: RenumberedVehicleReportRow[];
  try {
    rows = await getRenumberedVehicleReport();
  } catch (error) {
    if (error instanceof VehicleApiError && error.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    }

    console.error(
      "FIS renumbered vehicle report request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }

  const query = await searchParams;
  const requestedPage = Number.parseInt(getQueryValue(query.page) ?? "1", 10);
  const totalPages = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
  const page = Number.isFinite(requestedPage)
    ? Math.min(Math.max(requestedPage, 1), totalPages)
    : 1;
  const visibleRows = rows.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="renumbered-report-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Stolen &amp; recovered vehicles</p>
            <h1 id="renumbered-report-title">Report All Renumbered Vehicles</h1>
            <p>Renumbered vehicle records ordered by the old GG number.</p>
          </div>
          <Link className="button button-secondary" href="/vehicles">
            Vehicle Master
          </Link>
        </header>

        {rows.length === 0 ? (
          <div className="vehicle-empty-state">
            <p className="eyebrow">No renumbered vehicles found</p>
            <p>No vehicle records currently contain a replacement GG number.</p>
          </div>
        ) : (
          <>
            <div className="vehicle-overview-header">
              <p className="muted-copy" aria-live="polite">
                {rows.length} vehicle{rows.length === 1 ? "" : "s"}
              </p>
            </div>
            <ReportTable rows={visibleRows} />
            <Pagination page={page} totalPages={totalPages} routePath={routePath} />
          </>
        )}
      </section>
    </main>
  );
}
