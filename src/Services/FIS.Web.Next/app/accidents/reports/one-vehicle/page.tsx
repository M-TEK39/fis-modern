import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import {
  AccidentApiError,
  getAccidentVehicleReport,
  type AccidentVehicleReportMode,
  type AccidentVehicleReportRow,
} from "@/lib/api-accidents";
import { getSession } from "@/lib/session";
import VehicleReportResult from "@/app/accidents/reports/vehicle-report-result";

const ACCIDENTS_ROLE = "Accidents";

type OneVehicleReportPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getMode(value: string | undefined): AccidentVehicleReportMode {
  return value === "fleet" || value === "gg" || value === "Radiogg" ? "fleet" : "registration";
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

function LoadingState() {
  return <div className="loading-card" aria-busy="true"><span className="spinner" aria-hidden="true" /><p>Loading vehicle accident report...</p></div>;
}

function ErrorState() {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>The vehicle accident report could not be loaded.</h2><p className="muted-copy">Retry when the FIS API is available.</p><Link className="button button-primary" href="/accidents/reports/one-vehicle">Try again</Link></section>;
}

async function OneVehicleReportContent({ searchParams }: OneVehicleReportPageProps) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <SessionRecovery returnPath="/accidents/reports/one-vehicle" />;
  if (session.status === "unavailable") return <ErrorState />;
  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return <section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to run accident reports.</h2></section>;
  }

  const query = await searchParams;
  const mode = getMode(getQueryValue(query.mode) ?? getQueryValue(query.Radio1));
  const searchTerm = (getQueryValue(query.searchTerm) ?? getQueryValue(query.xnumber) ?? "").trim();
  const shouldRun = getQueryValue(query.run) === "1" || searchTerm.length > 0;
  let rows: AccidentVehicleReportRow[] | null = null;
  if (shouldRun && searchTerm) {
    try {
      rows = await getAccidentVehicleReport(searchTerm, mode);
    } catch (error) {
      if (error instanceof AccidentApiError && error.reason === "unauthorized") return <SessionRecovery returnPath="/accidents/reports/one-vehicle" />;
      console.error("FIS accident vehicle report failed", error instanceof Error ? error.message : "unknown error");
      return <ErrorState />;
    }
  }

  return (
    <>
      <form className="vehicle-status-maintenance-panel" method="get">
        <fieldset className="vehicle-search-options">
          <legend>Find vehicle by</legend>
          <label className="vehicle-checkbox-label"><input type="radio" name="mode" value="registration" defaultChecked={mode === "registration"} /> GP</label>
          <label className="vehicle-checkbox-label"><input type="radio" name="mode" value="fleet" defaultChecked={mode === "fleet"} /> GG</label>
        </fieldset>
        <div className="field">
          <label htmlFor="one-vehicle-report-search">Number</label>
          <input id="one-vehicle-report-search" name="searchTerm" maxLength={8} defaultValue={searchTerm} required />
        </div>
        <input name="run" type="hidden" value="1" />
        <div className="button-row"><button className="button button-primary" type="submit">SUBMIT</button><Link className="button button-secondary" href="/accidents/reports">Report Menu</Link></div>
      </form>

      {rows !== null ? (
        rows.length === 0 ? (
          <div className="vehicle-empty-state"><p className="eyebrow">No vehicles found</p><h2>No accidents matched this vehicle number.</h2><p className="muted-copy">Try another GP or GG number.</p></div>
        ) : (
          <section aria-live="polite" aria-labelledby="vehicle-report-results-title">
            <div className="vehicle-form-section-header"><div><p className="eyebrow">Report results</p><h2 id="vehicle-report-results-title">Accidents found: {rows.length}</h2></div></div>
            {rows.map((row, index) => <VehicleReportResult key={row.accidentCode} row={row} index={index} />)}
          </section>
        )
      ) : null}

      <div className="vehicle-footer-actions"><Link className="button button-secondary" href="/accidents">Accident Menu</Link><Link className="button button-secondary" href="/home">Home</Link><form action={logoutAction}><button className="button button-secondary" type="submit">Sign out</button></form></div>
    </>
  );
}

export default async function OneVehicleAccidentReportPage({ searchParams }: OneVehicleReportPageProps) {
  await connection();
  return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="one-vehicle-report-title"><header className="vehicle-page-header"><div><p className="eyebrow">Accident reports</p><h1 id="one-vehicle-report-title">One Vehicle Accidents</h1><p>Search for all accident details by an exact GP or GG number.</p></div><Link className="button button-secondary" href="/accidents/reports">Report Menu</Link></header><Suspense fallback={<LoadingState />}><OneVehicleReportContent searchParams={searchParams} /></Suspense></section></main>;
}
