import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import RouteLoading from "@/components/app-shell/route-loading";
import ApiUnavailableCard from "@/components/app-shell/api-unavailable-card";
import AccessRestrictedCard from "@/components/app-shell/access-restricted-card";
import {
  DEFAULT_RENUMBERED_REPORT_PAGE_SIZE,
  getRenumberedVehicleReportPage,
  VehicleApiError,
  type RenumberedVehicleReportPage,
  type RenumberedVehicleReportRow,
} from "@/lib/api/vehicles/api-vehicles";
import { getSession } from "@/lib/auth/session";

export type RenumberedReportRoutePath =
  "/vehicles/renumbered-report" | "/Master-File/RPT_renumbered.aspx";

export type RenumberedReportPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: RenumberedReportRoutePath;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getRequestedPage(value: string | string[] | undefined) {
  const parsed = Number.parseInt(getQueryValue(value) ?? "1", 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 1;
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
  return <AccessRestrictedCard message="You do not have permission to view renumbered vehicles." />;
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: RenumberedReportRoutePath }>) {
  return (
    <ApiUnavailableCard
      message="The renumbered vehicle report could not be loaded."
      retryHref={routePath}
      secondaryHref="/login"
      secondaryLabel="Sign in"
    />
  );
}

function ReportTable({
  rows,
  rowNumberOffset,
}: Readonly<{ rows: RenumberedVehicleReportRow[]; rowNumberOffset: number }>) {
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Report All Renumbered Vehicles results</caption>
        <DataTableHeader
          columns={[
            { key: "column-1", label: <>No.</> },
            { key: "column-2", label: <>Old GG Number</> },
            { key: "column-3", label: <>Status</> },
            { key: "column-4", label: <>New GG Number</> },
            { key: "column-5", label: <>Status</> },
            { key: "column-6", label: <>Fleet Number History of Renumbered</> },
          ]}
        />
        <tbody>
          {rows.length === 0 ? (
            <tr>
              <td colSpan={6}>No records found on this page.</td>
            </tr>
          ) : (
            rows.map((row, index) => (
              <tr key={`${row.oldVmfCode}-${row.newFleetNumber ?? "replacement"}`}>
                <td>{rowNumberOffset + index + 1}</td>
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
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}

function Pagination({
  page,
  totalPages,
  routePath,
  query,
}: Readonly<{
  page: number;
  totalPages: number;
  routePath: RenumberedReportRoutePath;
  query: Record<string, string | string[] | undefined>;
}>) {
  const pageHref = (nextPage: number) => {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(query)) {
      if (key === "page" || value === undefined) {
        continue;
      }

      if (Array.isArray(value)) {
        value.forEach((item) => params.append(key, item));
      } else {
        params.set(key, value);
      }
    }

    if (nextPage > 1) {
      params.set("page", String(nextPage));
    }

    const queryString = params.toString();
    return queryString ? `${routePath}?${queryString}` : routePath;
  };

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

async function RenumberedReportPageContent({
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
        <ApiUnavailable routePath={routePath} />
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

  const query = await searchParams;
  const requestedPage = getRequestedPage(query.page);

  let reportPage: RenumberedVehicleReportPage;
  try {
    reportPage = await getRenumberedVehicleReportPage(
      requestedPage,
      DEFAULT_RENUMBERED_REPORT_PAGE_SIZE,
    );
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
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  }

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

        {reportPage.total === 0 ? (
          <div className="vehicle-empty-state">
            <p className="eyebrow">No renumbered vehicles found</p>
            <p>No vehicle records currently contain a replacement GG number.</p>
          </div>
        ) : (
          <>
            <div className="vehicle-overview-header">
              <p className="muted-copy" aria-live="polite">
                {reportPage.total} vehicle{reportPage.total === 1 ? "" : "s"}
              </p>
            </div>
            <ReportTable
              rows={reportPage.items}
              rowNumberOffset={(reportPage.page - 1) * reportPage.pageSize}
            />
            <Pagination
              page={reportPage.page}
              totalPages={reportPage.totalPages}
              routePath={routePath}
              query={query}
            />
            <p className="vehicle-pagination-meta">
              Total records: {reportPage.total} | Showing {reportPage.items.length} on this page |
              Page size: {reportPage.pageSize}
            </p>
          </>
        )}
      </section>
    </main>
  );
}

export default function RenumberedReportPage(props: RenumberedReportPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <RenumberedReportPageContent {...props} />
    </Suspense>
  );
}
