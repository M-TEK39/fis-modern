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

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || value === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) ?? null;
}

function formatTime(value: string | null) {
  if (!value) {
    return null;
  }

  const timeStart = value.indexOf("T");
  return timeStart >= 0 ? value.slice(timeStart + 1, timeStart + 6) : value.slice(0, 5);
}

function reportFields(row: AccidentVehicleReportRow) {
  return [
    ["Prov Reg Number", valueOrDash(row.registrationNumber)],
    ["GG Number", valueOrDash(row.fleetNumber)],
    ["Garage", valueOrDash(row.locationDescription)],
    ["Accident Date", valueOrDash(formatDate(row.accidentDate))],
    ["Accident Time", valueOrDash(formatTime(row.accidentTime))],
    ["Accident Place", valueOrDash(row.accidentPlace)],
    ["Fin year", valueOrDash(row.financialYear)],
    ["Date Updated", valueOrDash(formatDate(row.dateUpdated))],
    ["Notified Garage", valueOrDash(row.notifiedGarage)],
    ["Notified Gar Date", valueOrDash(formatDate(row.notifiedGarageDate))],
    ["Notified - Trip Auth", valueOrDash(row.notifiedTripAuthority)],
    ["Notified - Trip Auth Date", valueOrDash(formatDate(row.notifiedTripAuthorityDate))],
    ["Description of Accident", valueOrDash(row.description)],
    ["Accident Category", valueOrDash(row.accidentTypeDescription)],
    ["Trip Authority", valueOrDash(row.tripAuthority)],
    ["Driver Name", valueOrDash(row.driverName)],
    ["Driver ID Number", valueOrDash(row.driverEmployNumber)],
    ["Driver Site", valueOrDash(row.departmentNumber)],
    ["Transport Officer", valueOrDash(row.transportOfficerName)],
    ["Transport Officer Tel", valueOrDash(row.transportOfficerTelephone)],
    ["HQ Reference", valueOrDash(row.hqReference)],
    ["GG Reference", valueOrDash(row.ggReference)],
    ["SA Reference", valueOrDash(row.saReference)],
    ["Case Number", valueOrDash(row.caseNumber)],
    ["Job Number", valueOrDash(row.caseNumber)],
    ["GG Car Damage Amount", valueOrDash(row.costOfRepair)],
    ["GG Car Damage Desc", valueOrDash(row.damageDescription)],
    ["Driver Fault", valueOrDash(row.driverFault)],
    ["Death", valueOrDash(row.death)],
    ["Injured", valueOrDash(row.injured)],
    ["Private Party Regno", valueOrDash(row.thirdPartyRegistration)],
    ["Third Party Owner", valueOrDash(row.thirdPartyOwner)],
    ["Private Car Damage", valueOrDash(row.thirdPartyClaim)],
    ["Priv Damage Pay Date", valueOrDash(formatDate(row.privateDamagePaymentDate))],
    ["Claim Against Dept", valueOrDash(row.insuranceClaim)],
    ["Claim Received", valueOrDash(row.claimReceived)],
    ["Cost Claim Agains Dept", valueOrDash(row.claimAgainstDepartment)],
    ["Claim Accept/Reject", valueOrDash(row.claimDecision)],
    ["Claim Reject Reason", valueOrDash(row.claimRejectReason)],
    ["Write Off Amount", valueOrDash(row.writeOffAmount)],
    ["Write Off Date", valueOrDash(formatDate(row.writeOffDate))],
    ["LetterHead", valueOrDash(row.letterhead)],
    ["Z181", valueOrDash(row.z181)],
    ["File Close Date", valueOrDash(formatDate(row.fileCloseDate))],
    ["Notes", valueOrDash(row.notes)],
  ] as const;
}

function LoadingState() {
  return <div className="loading-card" aria-busy="true"><span className="spinner" aria-hidden="true" /><p>Loading vehicle accident report...</p></div>;
}

function ErrorState() {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>The vehicle accident report could not be loaded.</h2><p className="muted-copy">Retry when the FIS API is available.</p><Link className="button button-primary" href="/accidents/reports/one-vehicle">Try again</Link></section>;
}

function ReportResult({ row, index }: { row: AccidentVehicleReportRow; index: number }) {
  const titleId = `vehicle-accident-report-${row.accidentCode}`;
  return (
    <article className="vehicle-status-maintenance-panel" aria-labelledby={titleId}>
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Accident {index + 1}</p>
          <h2 id={titleId}>Record {row.accidentCode}</h2>
        </div>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">Detailed accident report for record {row.accidentCode}</caption>
          <tbody>
            {reportFields(row).map(([label, value]) => (
              <tr key={label}>
                <th scope="row">{label}</th>
                <td>{value}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </article>
  );
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
            {rows.map((row, index) => <ReportResult key={row.accidentCode} row={row} index={index} />)}
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
