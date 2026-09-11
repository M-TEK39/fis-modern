import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";
import {
  getFirmDateTowingReport,
  TowingApiError,
  type TowingRecord,
} from "@/lib/api/fleet-operations/api-towing";

const REPORTS_ROLE = "Reports";
export type TowingFirmDatePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};
function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
function hasReportsRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(REPORTS_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}
function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}
function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

async function TowingFirmDatePageContent({
  searchParams,
  routePath = "/towing/reports/firm-date",
}: TowingFirmDatePageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Firm towing reports could not be loaded.</h2>
        </section>
      </main>
    );
  if (!hasReportsRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to run Towing reports.</h2>
        </section>
      </main>
    );
  const query = await searchParams;
  const firmName = (getQueryValue(query.firmName) ?? getQueryValue(query.XNAME) ?? "")
    .trim()
    .slice(0, 45);
  const startDate = (getQueryValue(query.startDate) ?? getQueryValue(query.BDAT) ?? "").trim();
  const endDate = (getQueryValue(query.endDate) ?? getQueryValue(query.EDAT) ?? "").trim();
  const submitted = Boolean(firmName || startDate || endDate);
  let records: TowingRecord[] = [];
  try {
    if (submitted)
      records = await getFirmDateTowingReport(
        firmName,
        startDate || "1900-01-01",
        endDate || "2999-12-31",
      );
  } catch (error) {
    if (error instanceof TowingApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS towing firm date report failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Firm towing report could not be loaded.</h2>
          <Link className="button button-primary" href={routePath}>
            Try again
          </Link>
        </section>
      </main>
    );
  }
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="towing-firm-report-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Road Side Assistance</p>
            <h1 id="towing-firm-report-title">Road Side Call&apos;s for a Firm</h1>
            <p>Find calls for an assistance firm within the selected period.</p>
          </div>
          <Link className="button button-secondary" href="/towing/reports">
            Report Menu
          </Link>
        </header>
        <form className="vehicle-status-maintenance-panel" method="get">
          <div className="form-grid">
            <div className="form-field form-group-full">
              <label className="form-label" htmlFor="towing-firm-name">
                Firm Name
              </label>
              <input
                className="form-input"
                id="towing-firm-name"
                name="firmName"
                maxLength={45}
                defaultValue={firmName}
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="towing-firm-start">
                Begin Date
              </label>
              <input
                className="form-input"
                id="towing-firm-start"
                name="startDate"
                type="date"
                defaultValue={startDate}
                required
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="towing-firm-end">
                End Date
              </label>
              <input
                className="form-input"
                id="towing-firm-end"
                name="endDate"
                type="date"
                defaultValue={endDate}
                required
              />
            </div>
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Submit
            </button>
            <Link className="button button-secondary" href="/towing/reports">
              Report Menu
            </Link>
          </div>
        </form>
        {submitted ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="towing-firm-results"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Report results</p>
                <h2 id="towing-firm-results">Calls found</h2>
              </div>
            </div>
            {records.length === 0 ? (
              <div className="vehicle-empty-state">
                <p className="eyebrow">No requests found</p>
                <h2>No calls matched the selected firm and dates.</h2>
              </div>
            ) : (
              <div className="vehicle-table-wrapper">
                <table className="vehicle-table">
                  <caption className="sr-only">Firm towing calls</caption>
                  <thead>
                    <tr>
                      <th scope="col">Reference</th>
                      <th scope="col">VMF</th>
                      <th scope="col">Request Date</th>
                      <th scope="col">Location</th>
                      <th scope="col">Problem</th>
                    </tr>
                  </thead>
                  <tbody>
                    {records.map((item) => (
                      <tr key={item.towingCode}>
                        <td>{valueOrDash(item.callReference)}</td>
                        <td>{item.vmfCode}</td>
                        <td>{formatDate(item.requestDate)}</td>
                        <td>{valueOrDash(item.locationStart)}</td>
                        <td>{valueOrDash(item.vehicleProblem)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        ) : null}
      </section>
    </main>
  );
}

export default function TowingFirmDatePage(props: Parameters<typeof TowingFirmDatePageContent>[0]) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TowingFirmDatePageContent {...props} />
    </Suspense>
  );
}

type LegacyTowingFirmDatePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export function createLegacyTowingFirmDatePage(routePath: string) {
  return function LegacyTowingFirmDatePage({
    searchParams,
  }: Readonly<LegacyTowingFirmDatePageProps>) {
    return <TowingFirmDatePage routePath={routePath} searchParams={searchParams} />;
  };
}
