import DataTableHeader from "@/components/ui/data-table-header";

import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { ReportPagination } from "@/app/(fleet-operations)/reports/_components";
import { getSession } from "@/lib/auth/session";
import { getTowTruckPage, TowingApiError } from "@/lib/api/fleet-operations/api-towing";

const TOWING_ROLE = "Towing";
const DATE_TIME_FORMATTER = new Intl.DateTimeFormat("en-ZA", {
  dateStyle: "medium",
  timeStyle: "short",
  timeZone: "Africa/Johannesburg",
});
function hasTowingRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(TOWING_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}
function valueOrDash(value: string | null | undefined) {
  return value?.trim() || "-";
}

function pageValue(value: string | string[] | undefined) {
  const first = Array.isArray(value) ? value[0] : value;
  const page = Number(first);
  return Number.isSafeInteger(page) && page > 0 ? page : 1;
}

async function TowTruckReportPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/towing/tow-truck-data/report" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Tow truck report could not be loaded.</h2>
        </section>
      </main>
    );
  if (!hasTowingRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to view tow truck data.</h2>
        </section>
      </main>
    );
  try {
    const query = await searchParams;
    const page = pageValue(query.page);
    const report = await getTowTruckPage("", page, 24);
    const printedAt = DATE_TIME_FORMATTER.format(new Date());
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="tow-truck-report-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Road Side Assistance</p>
              <h1 id="tow-truck-report-title">Tow Truck Info Report</h1>
              <p>All assistance firm general data from the legacy Tow_Truck table.</p>
            </div>
            <Link className="button button-secondary" href="/towing/reports">
              Report Menu
            </Link>
          </header>
          <p className="form-hint">Date printed: {printedAt}</p>
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">Tow truck information report</caption>
              <DataTableHeader
                columns={[
                  { key: "column-1", label: <>Name</> },
                  { key: "column-2", label: <>Tel Number</> },
                  { key: "column-3", label: <>Fax Number</> },
                  { key: "column-4", label: <>Area</> },
                ]}
              />
              <tbody>
                {report.items.map((truck) => (
                  <tr key={truck.towCode}>
                    <td>{valueOrDash(truck.name)}</td>
                    <td>{valueOrDash(truck.telephone)}</td>
                    <td>{valueOrDash(truck.fax)}</td>
                    <td>{valueOrDash(truck.area)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <ReportPagination
            report={report}
            pageHref={(requestedPage) => `/towing/tow-truck-data/report?page=${requestedPage}`}
            label="Tow truck report pages"
          />
          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/towing/tow-truck-data">
              Maintenance
            </Link>
            <Link className="button button-secondary" href="/towing">
              Towing Menu
            </Link>
          </div>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof TowingApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/towing/tow-truck-data/report" />
        </main>
      );
    console.error(
      "FIS tow truck report failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Tow truck report could not be loaded.</h2>
          <Link className="button button-primary" href="/towing/tow-truck-data/report">
            Try again
          </Link>
        </section>
      </main>
    );
  }
}

export default function TowTruckReportPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TowTruckReportPageContent searchParams={searchParams} />
    </Suspense>
  );
}
