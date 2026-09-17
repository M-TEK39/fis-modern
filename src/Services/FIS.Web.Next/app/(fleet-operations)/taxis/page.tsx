import DataTableHeader from "@/components/ui/data-table-header";

import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  TaxiNotice,
  TaxiPagination,
  TaxiRestricted,
  TaxiUnavailable,
} from "@/app/(fleet-operations)/taxis/_components";
import { dateValue, valueOrDash } from "@/app/(fleet-operations)/taxis/_utils";
import { MenuSection } from "@/components/ui/menu-section";
import {
  DEFAULT_TAXI_PAGE_SIZE,
  getTaxiPage,
  TaxiApiError,
} from "@/lib/api/fleet-operations/api-taxis";
import { getSession } from "@/lib/auth/session";
import {
  hasRecurringTaxiAccess,
  hasTaxiAccess,
} from "@/app/(fleet-operations)/taxis/access";

function requestedPage(value: string | string[] | undefined) {
  const candidate = Number(Array.isArray(value) ? value[0] : value);
  return Number.isInteger(candidate) && candidate > 0 ? candidate : 1;
}

function hasRole(roles: readonly string[]) {
  return hasTaxiAccess(roles);
}

async function TaxisPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/taxis" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/taxis" />
      </main>
    );
  if (!hasRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <TaxiRestricted />
      </main>
    );

  const query = await searchParams;
  try {
    const taxiPage = await getTaxiPage({
      page: requestedPage(query.page),
      pageSize: DEFAULT_TAXI_PAGE_SIZE,
    });
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="taxis-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Private Hire Vehicles</p>
              <h1 id="taxis-title">Taxi Maintenance Menu</h1>
              <p>Taxi requisitions, logs, maintenance, and reports.</p>
            </div>
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          </header>
          <TaxiNotice query={query} />
          <div className="vehicle-menu-tiles">
            <MenuSection title="Taxi Requisitions">
              <Link className="vehicle-menu-link" href="/taxis/requests?mode=add">
                1) Enter Taxi Requisition
              </Link>
              {hasRecurringTaxiAccess(session.roles) ? (
                <Link className="vehicle-menu-link" href="/taxis/requests?mode=recurring">
                  1.2) Book Recurring Taxi (weekdays)
                </Link>
              ) : null}
              <Link className="vehicle-menu-link" href="/taxis/requests?mode=add&previousBas=1">
                1.1) Enter Taxi Requisition using Previous Fin Years BAS Codes
              </Link>
              <Link className="vehicle-menu-link" href="/taxis/requests?mode=edit">
                2) Edit Taxi Requisition
              </Link>
              <Link className="vehicle-menu-link" href="/taxis/requests/cancel">
                3) Cancel Taxi Requisition
              </Link>
              <Link className="vehicle-menu-link" href="/taxis/requests/reprint">
                4) Re-Print A Requisition
              </Link>
              <Link className="vehicle-menu-link" href="/taxis/requests/pending">
                5) Pending Requests
              </Link>
              <Link className="vehicle-menu-link" href="/taxis/requests/pending-jia">
                6) Pending JIA Pick-ups
              </Link>
            </MenuSection>
            <MenuSection title="Taxi Logs">
              <Link className="vehicle-menu-link" href="/taxis/logs/enter">
                1) Enter Taxi logsheet
              </Link>
              <Link className="vehicle-menu-link" href="/taxis/logs/edit">
                2) Edit Taxi Logsheet
              </Link>
              <Link className="vehicle-menu-link" href="/taxis/logs/reprint">
                3) Reprint Taxi Log
              </Link>
              <Link className="vehicle-menu-link" href="/taxis/logs/white-log">
                3) Enter Taxi white log (GG vehicles not for claiming)
              </Link>
            </MenuSection>
            {hasRole(session.roles) ? (
              <MenuSection title="Maintenance">
                <Link className="vehicle-menu-link" href="/taxis/maintenance/info">
                  1) Taxi Information Maintenance
                </Link>
              </MenuSection>
            ) : null}
            <MenuSection title="Taxi Reports">
              <Link className="vehicle-menu-link" href="/taxis/reports/one-taxi-number">
                1) Report On One Taxi Number
              </Link>
              <Link className="vehicle-menu-link" href="/taxis/reports/logs-per-user">
                2) Number Of Taxi Logs Captured Per User For Date
              </Link>
              <Link className="vehicle-menu-link" href="/taxis/reports/old-requisitions">
                3) Old Requisitions For Period
              </Link>
              <Link className="vehicle-menu-link" href="/taxis/reports/taxis-per-company">
                4) Taxis Per Hire Company
              </Link>
              <Link className="vehicle-menu-link" href="/taxis/reports/taxis-per-department">
                5) List Of All Taxis In Various Departments
              </Link>
              <Link
                className="vehicle-menu-link"
                href="/taxis/reports/taxis-inservice-per-department"
              >
                6) List Of All Taxis In Service In Various Departments
              </Link>
              <Link className="vehicle-menu-link" href="/taxis/reports/logs-requisitions-status">
                7) Taxi Logs and Requisitions Status Reports
              </Link>
              <Link className="vehicle-menu-link" href="/taxis/reports/financial">
                8) Financial Reports: Taxis
              </Link>
            </MenuSection>
            <MenuSection title="Other Taxi Maintenance Options">
              <Link className="vehicle-menu-link" href="/taxis/scan-requisition">
                1) Scan Taxi Requisition
              </Link>
            </MenuSection>
          </div>
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="taxi-preview-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Live records</p>
                <h2 id="taxi-preview-title">Taxi records summary</h2>
              </div>
              <span className="muted-copy">{taxiPage.total} active records</span>
            </div>
            {taxiPage.total === 0 ? (
              <p className="muted-copy">No taxi records are available.</p>
            ) : (
              <>
                <div className="vehicle-table-wrapper">
                  <table className="vehicle-table">
                    <caption className="sr-only">Taxi records summary</caption>
                    <DataTableHeader
                      columns={[
                        { key: "column-1", label: <>Requisition</> },
                        { key: "column-2", label: <>Official</> },
                        { key: "column-3", label: <>Vehicle</> },
                        { key: "column-4", label: <>Department</> },
                        { key: "column-5", label: <>Date required</> },
                        { key: "column-6", label: <>Status</> },
                      ]}
                    />
                    <tbody>
                      {taxiPage.items.map((taxi) => (
                        <tr key={taxi.requestId}>
                          <td>
                            <Link href={`/taxis/requests?mode=edit&requestId=${taxi.requestId}`}>
                              {valueOrDash(taxi.rekNum)}
                            </Link>
                          </td>
                          <td>{valueOrDash(taxi.official)}</td>
                          <td>{valueOrDash(taxi.vmfCode)}</td>
                          <td>{valueOrDash(taxi.departmentName ?? taxi.departmentCode)}</td>
                          <td>{dateValue(taxi.dateRequired)}</td>
                          <td>
                            {taxi.cancelled
                              ? "Cancelled"
                              : taxi.driver
                                ? "Driver captured"
                                : "Pending"}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <TaxiPagination
                  path="/taxis"
                  query={query}
                  page={taxiPage.page}
                  totalPages={taxiPage.totalPages}
                />
              </>
            )}
          </section>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof TaxiApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/taxis" />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <TaxiUnavailable />
      </main>
    );
  }
}

export default function TaxisPage(props: Parameters<typeof TaxisPageContent>[0]) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TaxisPageContent {...props} />
    </Suspense>
  );
}
