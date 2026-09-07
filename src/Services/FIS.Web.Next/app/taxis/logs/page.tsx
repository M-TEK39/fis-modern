import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { saveTaxiLogAction, saveTaxiWhiteLogAction } from "@/app/taxis/actions";
import { dateValue, queryValue, TaxiHeader, TaxiNotice, TaxiRestricted, TaxiUnavailable, timeValue, valueOrDash } from "@/app/taxis/_components";
import { getTaxiLogLookup, getTaxiLogReferences, TaxiApiError, type TaxiLogLookup, type TaxiLogReference } from "@/lib/api-taxis";
import { getSession } from "@/lib/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function dateInput(value: string | null | undefined) {
  return value?.slice(0, 10) ?? "";
}

function timeInput(value: string | null | undefined) {
  if (!value) return "";
  const match = value.match(/T(\d{2}:\d{2})/);
  return match?.[1] ?? value.slice(0, 5);
}

function numberValue(value: number | null | undefined) {
  return value === null || value === undefined ? "" : String(value);
}

function LookupForm({ mode, query }: Readonly<{ mode: string; query: Record<string, string | string[] | undefined> }>) {
  return <form className="vehicle-status-maintenance-panel" method="get"><input type="hidden" name="mode" value={mode} /><div className="vehicle-search-row"><label className="form-label" htmlFor="taxi-log-requisition">Requisition number</label><input className="vehicle-search" id="taxi-log-requisition" name="rekNum" required defaultValue={queryValue(query.rekNum)} placeholder="Enter requisition number" /><button className="button button-primary" type="submit">Find requisition</button></div></form>;
}

function WhiteLogForm() {
  return <form className="vehicle-status-maintenance-panel" action={saveTaxiWhiteLogAction}><input type="hidden" name="returnPath" value="/taxis/logs/white-log" /><div className="vehicle-form-section-header"><div><p className="eyebrow">GG vehicles not for claiming</p><h2>Enter taxi white log</h2></div></div><div className="form-grid"><div className="form-field"><label className="form-label" htmlFor="white-vmf">GG number *</label><input className="form-input" id="white-vmf" name="vmfCode" type="number" min="1" required /></div><div className="form-field"><label className="form-label" htmlFor="white-driver">Driver *</label><input className="form-input" id="white-driver" name="driver" maxLength={30} required /></div><div className="form-field"><label className="form-label" htmlFor="white-start-odo">Start odometer *</label><input className="form-input" id="white-start-odo" name="startOdo" type="number" min="0" required /></div><div className="form-field"><label className="form-label" htmlFor="white-start-date">Start date *</label><input className="form-input" id="white-start-date" name="startDate" type="date" required /></div><div className="form-field"><label className="form-label" htmlFor="white-end-odo">End odometer *</label><input className="form-input" id="white-end-odo" name="endOdo" type="number" min="0" required /></div><div className="form-field"><label className="form-label" htmlFor="white-end-date">End date *</label><input className="form-input" id="white-end-date" name="endDate" type="date" required /></div></div><div className="button-row"><button className="button button-primary" type="submit">Submit</button><Link className="button button-secondary" href="/taxis">Menu</Link></div></form>;
}

function LogForm({ lookup, references }: Readonly<{ lookup: TaxiLogLookup; references: TaxiLogReference }>) {
  const log = lookup.log;
  const contractorClasses = references.classes.filter((item) => item.contractorId === (lookup.contractorId ?? 0));
  return <form className="vehicle-status-maintenance-panel" action={saveTaxiLogAction}><input type="hidden" name="requestId" value={lookup.requestId} /><input type="hidden" name="rekNum" value={lookup.rekNum} /><input type="hidden" name="logId" value={log?.logId ?? ""} /><input type="hidden" name="returnPath" value={log ? "/taxis/logs/edit" : "/taxis/logs/enter"} /><div className="vehicle-form-section-header"><div><p className="eyebrow">{log ? "Edit taxi logsheet" : "Enter taxi logsheet"}</p><h2>Requisition {lookup.rekNum}</h2></div><span className="muted-copy">{dateValue(lookup.dateRequired)} at {timeValue(lookup.timeRequired)}</span></div><dl className="details-grid"><div><dt>Official</dt><dd>{valueOrDash(lookup.official)}</dd></div><div><dt>Department</dt><dd>{valueOrDash(lookup.departmentName ?? lookup.departmentCode)}</dd></div><div><dt>Vehicle class</dt><dd>{valueOrDash(lookup.vehicleTypeDescription ?? lookup.vehicleTypeCode)}</dd></div></dl><div className="form-grid"><div className="form-field"><label className="form-label" htmlFor="log-contractor">Service provider *</label><select className="form-select" id="log-contractor" name="contractorId" required defaultValue={lookup.contractorId ?? ""}><option value="">Select...</option>{references.contractors.map((item) => <option key={item.contractorId} value={item.contractorId}>{item.contractorName}</option>)}</select></div><div className="form-field"><label className="form-label" htmlFor="log-class">Class</label><select className="form-select" id="log-class" name="vehicleTypeCode" defaultValue={lookup.vehicleTypeCode ?? ""}><option value="">Quoted tariff</option>{contractorClasses.map((item) => <option key={item.classId} value={item.classId}>{item.description}</option>)}</select></div><div className="form-field"><label className="form-label" htmlFor="log-registration">Registration number *</label><input className="form-input" id="log-registration" name="registrationNumber" required defaultValue={lookup.registrationNumber ?? lookup.fleetNumber ?? ""} /></div><div className="form-field"><label className="form-label" htmlFor="log-driver">Driver *</label><input className="form-input" id="log-driver" name="driver" required defaultValue={lookup.driver ?? ""} /></div><div className="form-field"><label className="form-label" htmlFor="log-note">Log note</label><select className="form-select" id="log-note" name="taxiLogNoteCode" defaultValue={log?.taxiLogNoteCode ?? ""}><option value="">None</option>{references.notes.map((item) => <option key={item.noteCode} value={item.noteCode}>{item.description}</option>)}</select></div><div className="form-field"><label className="form-label" htmlFor="log-tariff">Quoted tariff</label><input className="form-input" id="log-tariff" name="quotedTariff" type="number" min="0" step="0.01" defaultValue={log?.quotedTariff ?? ""} /></div></div><div className="form-section"><h3 className="form-section-title">Driver journey</h3><div className="form-grid"><div className="form-field"><label className="form-label" htmlFor="log-start-odo">Start odometer *</label><input className="form-input" id="log-start-odo" name="driverStartOdo" type="number" min="0" required defaultValue={log?.driverStartOdo ?? ""} /></div><div className="form-field"><label className="form-label" htmlFor="log-start-date">Start date *</label><input className="form-input" id="log-start-date" name="driverStartDate" type="date" required defaultValue={dateInput(log?.driverStartDate)} /></div><div className="form-field"><label className="form-label" htmlFor="log-start-time">Start time *</label><input className="form-input" id="log-start-time" name="driverStartTime" type="time" required defaultValue={timeInput(log?.driverStartTime)} /></div><div className="form-field"><label className="form-label" htmlFor="log-end-odo">End odometer *</label><input className="form-input" id="log-end-odo" name="driverEndOdo" type="number" min="0" required defaultValue={log?.driverEndOdo ?? ""} /></div><div className="form-field"><label className="form-label" htmlFor="log-end-date">End date *</label><input className="form-input" id="log-end-date" name="driverEndDate" type="date" required defaultValue={dateInput(log?.driverEndDate)} /></div><div className="form-field"><label className="form-label" htmlFor="log-end-time">End time *</label><input className="form-input" id="log-end-time" name="driverEndTime" type="time" required defaultValue={timeInput(log?.driverEndTime)} /></div></div></div><div className="button-row"><button className="button button-primary" type="submit">{log ? "Update" : "Submit"}</button><Link className="button button-secondary" href="/taxis">Menu</Link></div></form>;
}

function LogSummary({ lookup }: Readonly<{ lookup: TaxiLogLookup }>) {
  const log = lookup.log;
  return <section className="vehicle-status-maintenance-panel" aria-labelledby="taxi-log-summary-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Taxi logsheet</p><h2 id="taxi-log-summary-title">{lookup.rekNum}</h2></div></div><dl className="details-grid"><div><dt>Official</dt><dd>{valueOrDash(lookup.official)}</dd></div><div><dt>Provider</dt><dd>{valueOrDash(lookup.contractorName ?? lookup.contractorId)}</dd></div><div><dt>Registration</dt><dd>{valueOrDash(lookup.registrationNumber ?? lookup.fleetNumber)}</dd></div><div><dt>Driver</dt><dd>{valueOrDash(lookup.driver)}</dd></div><div><dt>Start odometer</dt><dd>{valueOrDash(log?.driverStartOdo)}</dd></div><div><dt>End odometer</dt><dd>{valueOrDash(log?.driverEndOdo)}</dd></div><div><dt>Start journey</dt><dd>{dateValue(log?.driverStartDate)} {timeValue(log?.driverStartTime)}</dd></div><div><dt>End journey</dt><dd>{dateValue(log?.driverEndDate)} {timeValue(log?.driverEndTime)}</dd></div></dl></section>;
}

export default async function TaxiLogsPage({ searchParams, mode: forcedMode }: Readonly<{ searchParams: SearchParams; mode?: string }>) {
  await connection();
  const session = await getSession();
  const routePath = "/taxis/logs";
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/taxis/logs/enter" /></main>;
  if (!session.roles.some((role) => role.localeCompare("Private Hire Vehicles", undefined, { sensitivity: "accent" }) === 0)) return <main className="page-shell vehicle-page-shell"><TaxiRestricted subject="Taxi logs" /></main>;

  const query = await searchParams;
  const mode = forcedMode ?? (queryValue(query.mode) || "enter");
  const rekNum = queryValue(query.rekNum);
  if (mode === "white-log") return <main className="page-shell vehicle-page-shell"><section className="vehicle-card"><TaxiHeader title="Enter Taxi White Log" description="Record GG vehicle travel that is not submitted for claiming." /><TaxiNotice query={query} /><WhiteLogForm /></section></main>;

  try {
    let lookup: TaxiLogLookup | undefined;
    if (rekNum) lookup = await getTaxiLogLookup(rekNum, mode === "edit" ? "EDIT" : mode === "enter" ? "ENTER" : undefined);
    const references = lookup && mode !== "reprint" ? await getTaxiLogReferences() : undefined;
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-card"><TaxiHeader title={mode === "edit" ? "Edit Taxi Logsheet" : mode === "reprint" ? "Reprint Taxi Log" : "Enter Taxi Logsheet"} description="Look up a requisition before capturing or reviewing its taxi log." /><TaxiNotice query={query} /><LookupForm mode={mode} query={query} />{lookup ? mode === "reprint" ? <LogSummary lookup={lookup} /> : references ? <LogForm lookup={lookup} references={references} /> : null : null}</section></main>;
  } catch (error) {
    if (error instanceof TaxiApiError && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={`${routePath}/${mode}`} /></main>;
    return <main className="page-shell vehicle-page-shell"><TaxiUnavailable path={`${routePath}/${mode}`} subject="Taxi logs" /></main>;
  }
}
