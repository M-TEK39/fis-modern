import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import {
  CallCentreApiError,
  getCallCentreIncident,
  getCallCentreDataAccess,
  getCallCentreIncidents,
  getCallCentreSites,
  searchCallCentreVehicles,
  type CallCentreIncidentRecord,
  type CallCentreDataAccessEntry,
  type CallCentreSiteOption,
  type CallCentreVehicleOption,
} from "@/lib/api-call-centre";
import { getSession } from "@/lib/session";

const REPORTS_ROLE = "Reports";
const REPORT_MODES = [
  "one-reference",
  "one-vehicle",
  "all-reference",
  "dept-site-period",
  "statistics",
  "clo-report",
  "data-access",
  "open-calls",
] as const;

type ReportMode = (typeof REPORT_MODES)[number];
type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export type CallCentreReportPageProps = {
  searchParams: SearchParams;
  forcedMode?: string;
  routePath?: string;
};

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

function valueOrDash(value: string | number | null | undefined) {
  if (value === null || value === undefined || String(value).trim() === "") return "-";
  return String(value);
}

function normalizeDate(value: string | undefined) {
  const normalized = (value ?? "").trim().replaceAll("/", "-");
  return /^\d{4}-\d{2}-\d{2}$/.test(normalized) ? normalized : "";
}

function dateValue(value: string | null) {
  return value?.match(/^\d{4}-\d{2}-\d{2}/)?.[0] ?? "";
}

function timeValue(value: string | null) {
  if (!value) return "-";
  const isoMatch = value.match(/T(\d{2}:\d{2})/);
  return isoMatch?.[1] ?? value.match(/^(\d{2}:\d{2})/)?.[1] ?? value;
}

function modeValue(value: string) {
  const normalized = value.trim().toLowerCase();
  const aliases: Record<string, ReportMode> = {
    one_reference: "one-reference",
    one_vehicle: "one-vehicle",
    all_reference: "all-reference",
    dept_period: "dept-site-period",
    dept_site_period: "dept-site-period",
    statistics: "statistics",
    clo: "clo-report",
    clo_report: "clo-report",
    data_access: "data-access",
    open: "open-calls",
    open_calls: "open-calls",
  };
  const mode = aliases[normalized] ?? normalized;
  return REPORT_MODES.includes(mode as ReportMode) ? mode as ReportMode : null;
}

function incidentFilter(params: Record<string, string | string[] | undefined>) {
  const direct = queryValue(params.incident) ?? queryValue(params.incidentType);
  if (direct) return direct.trim().toLowerCase();
  if (queryValue(params.Radioacc)) return "accident";
  if (queryValue(params.Radiohig)) return "hijack";
  if (queryValue(params.Radiolos)) return "loss";
  if (queryValue(params.Radiorod)) return "road";
  return "all";
}

function matchesIncident(record: CallCentreIncidentRecord, selected: string) {
  if (selected === "all" || !selected) return true;
  const type = (record.incidentType ?? "").toLowerCase();
  if (selected === "accident") return type === "accident";
  if (selected === "hijack") return type === "hi-jack" || type === "hijack" || type === "highjack";
  if (selected === "loss") return type === "loss_theft" || type === "loss";
  if (selected === "road") return type === "road_assistance" || type === "road assistance";
  return type === selected;
}

function dateInRange(value: string | null, startDate: string, endDate: string) {
  const date = dateValue(value);
  return Boolean(date && (!startDate || date >= startDate) && (!endDate || date <= endDate));
}

function reportSite(record: CallCentreIncidentRecord) {
  return record.transportOfficerSite ?? record.driverSite;
}

function siteText(siteCode: number | null, sites: CallCentreSiteOption[]) {
  if (siteCode === null) return "-";
  const site = sites.find((candidate) => candidate.code === siteCode);
  return site ? `${site.departmentNumber ? `${site.departmentNumber} - ` : ""}${site.description}` : String(siteCode);
}

function reportTitle(mode: ReportMode) {
  return {
    "one-reference": "Incident Report on ONE Reference Number",
    "one-vehicle": "Incident Report on ONE Vehicle",
    "all-reference": "Incident Report on ALL Call Numbers",
    "dept-site-period": "Call Centre Report for a Dept (Site)",
    statistics: "Call Centre Statistics",
    "clo-report": "CALL CENTRE Client Relations Officer's REPORT",
    "data-access": "Full Report on Data Access Detail",
    "open-calls": "Call Centre REPORT on OPEN Calls",
  }[mode];
}

function AccessRestricted() {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to run Call Centre reports.</h2><p className="muted-copy">This page requires the Reports role.</p></section>;
}

function ApiUnavailable({ path }: Readonly<{ path: string }>) {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">Service unavailable</p><h2>Call Centre report data could not be loaded.</h2><p className="muted-copy">The application is still running. Retry when the FIS API is available.</p><Link className="button button-primary" href={path}>Try again</Link></section>;
}

function ReportForm({ mode, params, sites, vehicles }: Readonly<{ mode: ReportMode; params: Record<string, string | string[] | undefined>; sites: CallCentreSiteOption[]; vehicles: CallCentreVehicleOption[] }>) {
  const searchType = (queryValue(params.searchType) ?? queryValue(params.Radio1) ?? "GP").toUpperCase() === "GG" || queryValue(params.Radio1) === "Radiogg" ? "GG" : "GP";
  const searchQuery = (queryValue(params.searchQuery) ?? queryValue(params.xggnum) ?? "").trim();
  const selectedVmfCode = positiveInt(queryValue(params.vmfCode) ?? queryValue(params.vmf));
  const referenceNumber = (queryValue(params.referenceNumber) ?? queryValue(params.cccode) ?? "").trim();
  const startDate = normalizeDate(queryValue(params.startDate) ?? queryValue(params.BDAT));
  const endDate = normalizeDate(queryValue(params.endDate) ?? queryValue(params.EDAT));
  const selectedSiteCode = positiveInt(queryValue(params.siteCode) ?? queryValue(params.xsite) ?? queryValue(params.Site_code));
  const department = (queryValue(params.department) ?? queryValue(params.XDEPT) ?? "").trim();
  const captureName = (queryValue(params.captureName) ?? queryValue(params.XNAME) ?? "").trim();
  const selectedIncident = incidentFilter(params);

  return <form className="vehicle-status-maintenance-panel" method="get"><input name="run" type="hidden" value="1" />{mode === "one-reference" ? <div className="form-grid"><div className="field"><label htmlFor="call-report-reference">Reference Number (GMT)</label><input id="call-report-reference" name="referenceNumber" inputMode="numeric" defaultValue={referenceNumber} required /></div></div> : null}{mode === "one-vehicle" ? <div className="form-grid"><fieldset className="vehicle-search-options"><legend>Find vehicle by</legend><label className="vehicle-checkbox-label"><input type="radio" name="searchType" value="GP" defaultChecked={searchType === "GP"} /> GP</label><label className="vehicle-checkbox-label"><input type="radio" name="searchType" value="GG" defaultChecked={searchType === "GG"} /> GG</label></fieldset><div className="field"><label htmlFor="call-report-vehicle-search">Vehicle Number</label><input id="call-report-vehicle-search" name="searchQuery" maxLength={30} defaultValue={searchQuery} required /></div><div className="field"><label htmlFor="call-report-vehicle">Vehicle</label><select id="call-report-vehicle" name="vmfCode" defaultValue={selectedVmfCode ?? ""}><option value="">Select vehicle...</option>{vehicles.map((vehicle) => <option key={vehicle.vmfCode} value={vehicle.vmfCode}>{vehicle.displayText}</option>)}</select>{vehicles.length === 0 && searchQuery ? <p className="form-hint">No exact vehicle match was found. Search by the other identifier.</p> : null}</div></div> : null}{mode === "all-reference" ? <div className="form-grid"><fieldset className="vehicle-search-options"><legend>Incident</legend>{[["all", "ALL"], ["accident", "Accident"], ["hijack", "Hi-Jack"], ["loss", "Loss"], ["road", "Road Assist"]].map(([value, label]) => <label className="vehicle-checkbox-label" key={value}><input type="radio" name="incident" value={value} defaultChecked={selectedIncident === value} /> {label}</label>)}</fieldset><DateField id="all-start" name="startDate" label="Begin Date" value={startDate} /><DateField id="all-end" name="endDate" label="End Date" value={endDate} /></div> : null}{mode === "dept-site-period" ? <div className="form-grid"><div className="field"><label htmlFor="call-report-site">Site</label><select id="call-report-site" name="siteCode" defaultValue={selectedSiteCode ?? ""} required><option value="">Select site</option>{sites.map((site) => <option key={site.code} value={site.code}>{site.departmentNumber ? `${site.departmentNumber} - ` : ""}{site.description}</option>)}</select></div><div className="field"><label htmlFor="call-report-department">Department number</label><input id="call-report-department" name="department" maxLength={30} defaultValue={department} /></div><DateField id="dept-start" name="startDate" label="Begin Date" value={startDate} required /><DateField id="dept-end" name="endDate" label="End Date" value={endDate} required /></div> : null}{mode === "statistics" ? <div className="form-grid"><DateField id="stats-start" name="startDate" label="Begin Date" value={startDate} required /><DateField id="stats-end" name="endDate" label="End Date" value={endDate} required /><div className="field"><label htmlFor="capture-name">Capture Person Last Name</label><input id="capture-name" name="captureName" maxLength={60} defaultValue={captureName} /></div></div> : null}{mode === "clo-report" ? <div className="form-grid"><DateField id="clo-start" name="startDate" label="Begin Date" value={startDate} required /><DateField id="clo-end" name="endDate" label="End Date" value={endDate} required /><div className="field"><label htmlFor="clo-department">Department number</label><input id="clo-department" name="department" maxLength={30} defaultValue={department} /></div></div> : null}{mode === "data-access" ? <div className="form-grid"><div className="field"><label htmlFor="access-reference">Reference Number (GMT)</label><input id="access-reference" name="referenceNumber" inputMode="numeric" defaultValue={referenceNumber} required /></div></div> : null}{mode === "open-calls" ? <div className="form-grid"><DateField id="open-start" name="startDate" label="Begin Date" value={startDate} required /><DateField id="open-end" name="endDate" label="End Date" value={endDate} required /><div className="field"><label htmlFor="open-site">Site</label><select id="open-site" name="siteCode" defaultValue={selectedSiteCode ?? ""}><option value="">All sites</option>{sites.map((site) => <option key={site.code} value={site.code}>{site.departmentNumber ? `${site.departmentNumber} - ` : ""}{site.description}</option>)}</select></div></div> : null}<div className="button-row"><button className="button button-primary" type="submit">Submit</button><Link className="button button-secondary" href="/call-centre/reports">Report Menu</Link></div></form>;
}

function DateField({ id, name, label, value, required = false }: Readonly<{ id: string; name: string; label: string; value: string; required?: boolean }>) {
  return <div className="field"><label htmlFor={id}>{label}</label><input id={id} name={name} type="date" defaultValue={value} required={required} /></div>;
}

const LEGACY_FIELDS: Array<{ key: keyof CallCentreIncidentRecord; label: string }> = [
  { key: "code", label: "Reference Number" },
  { key: "vmfCode", label: "VMF Code" },
  { key: "callDate", label: "Call Date" },
  { key: "callTime", label: "Call Time" },
  { key: "incidentType", label: "Incident Type" },
  { key: "incidentDescription", label: "Incident Description" },
  { key: "captureName", label: "Capture Name" },
  { key: "userAccessCode", label: "User Access Code" },
  { key: "callerName", label: "Caller Name" },
  { key: "callerTel", label: "Caller Telephone" },
  { key: "callerFax", label: "Caller Fax" },
  { key: "callerEmail", label: "Caller Email" },
  { key: "driverName", label: "Driver Name" },
  { key: "driverPersalNumber", label: "Driver Persal Number" },
  { key: "driverLicenceNumber", label: "Driver Licence Number" },
  { key: "driverBaseStation", label: "Driver Base Station" },
  { key: "driverSite", label: "Driver Site" },
  { key: "driverTel", label: "Driver Telephone" },
  { key: "driverCell", label: "Driver Cell" },
  { key: "driverFax", label: "Driver Fax" },
  { key: "driverEmail", label: "Driver Email" },
  { key: "incidentDate", label: "Incident Date" },
  { key: "incidentTime", label: "Incident Time" },
  { key: "incidentTown", label: "Incident Town" },
  { key: "incidentStreet", label: "Incident Street" },
  { key: "transportOfficerName", label: "Transport Officer Name" },
  { key: "transportOfficerTel", label: "Transport Officer Telephone" },
  { key: "transportOfficerFax", label: "Transport Officer Fax" },
  { key: "transportOfficerEmail", label: "Transport Officer Email" },
  { key: "transportOfficerSite", label: "Transport Officer Site" },
  { key: "counter", label: "Counter" },
  { key: "croNotification", label: "Inform CLO" },
  { key: "croRemarks", label: "CLO Remarks" },
  { key: "incidentRemarks", label: "Incident Remarks" },
  { key: "notifyListCode", label: "Notification List Code" },
  { key: "callClosed", label: "Call Closed" },
];

function fieldValue(record: CallCentreIncidentRecord, key: keyof CallCentreIncidentRecord, sites: CallCentreSiteOption[]) {
  const value = record[key];
  if (key === "callDate" || key === "incidentDate") return dateValue(value as string | null);
  if (key === "callTime" || key === "incidentTime") return timeValue(value as string | null);
  if (key === "driverSite" || key === "transportOfficerSite") return siteText(value as number | null, sites);
  return valueOrDash(value as string | number | null | undefined);
}

function LegacyRecordDetails({ record, sites }: Readonly<{ record: CallCentreIncidentRecord; sites: CallCentreSiteOption[] }>) {
  return <details className="vehicle-record-details"><summary>Show full legacy fields</summary><div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Full Call_centre legacy record</caption><tbody>{LEGACY_FIELDS.map((field) => <tr key={field.key}><th scope="row">{field.label}</th><td>{fieldValue(record, field.key, sites)}</td></tr>)}</tbody></table></div></details>;
}

function RecordTable({ records, sites, title }: Readonly<{ records: CallCentreIncidentRecord[]; sites: CallCentreSiteOption[]; title: string }>) {
  if (records.length === 0) return <section className="vehicle-empty-state" aria-live="polite"><p className="eyebrow">No records found</p><h2>No incidents matched the selected filters.</h2><p className="muted-copy">Update the report criteria and submit again.</p></section>;
  return <section className="vehicle-status-maintenance-panel" aria-labelledby="call-centre-report-results-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Report results</p><h2 id="call-centre-report-results-title">{title}</h2></div><span className="form-hint">{records.length} record(s)</span></div><div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">{title}</caption><thead><tr><th scope="col">Reference Number</th><th scope="col">Incident</th><th scope="col">Call Date</th><th scope="col">Caller</th><th scope="col">Driver</th><th scope="col">Site</th><th scope="col">GG Number</th></tr></thead><tbody>{records.map((record) => <tr key={record.code}><td>{record.code}</td><td>{valueOrDash(record.incidentType)}</td><td>{valueOrDash(dateValue(record.callDate))}</td><td>{valueOrDash(record.callerName)}</td><td>{valueOrDash(record.driverName)}</td><td>{siteText(reportSite(record), sites)}</td><td>{valueOrDash(record.ggNumber)}</td></tr>)}</tbody></table></div>{records.map((record) => <LegacyRecordDetails key={`details-${record.code}`} record={record} sites={sites} />)}</section>;
}

function StatisticsResults({ records, captureName, title }: Readonly<{ records: CallCentreIncidentRecord[]; captureName: string; title: string }>) {
  const groups = [...records.reduce((result, record) => { const name = record.captureName?.trim() || "(Unknown)"; result.set(name, (result.get(name) ?? 0) + 1); return result; }, new Map<string, number>())].sort((left, right) => right[1] - left[1] || left[0].localeCompare(right[0]));
  if (groups.length === 0) return <section className="vehicle-empty-state"><p className="eyebrow">No records found</p><h2>No statistics matched the selected filters.</h2></section>;
  return <section className="vehicle-status-maintenance-panel" aria-labelledby="call-centre-statistics-results-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Report results</p><h2 id="call-centre-statistics-results-title">{title}</h2></div><span className="form-hint">{records.length} incident(s)</span></div><div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Call Centre statistics by capture name</caption><thead><tr><th scope="col">Capture Name</th><th scope="col">Count</th></tr></thead><tbody>{groups.map(([name, count]) => <tr key={name}><td>{name}</td><td>{count}</td></tr>)}</tbody></table></div></section>;
}

function DataAccessResults({ record, sites, accessTableAvailable, entries, title }: Readonly<{ record: CallCentreIncidentRecord | null; sites: CallCentreSiteOption[]; accessTableAvailable: boolean; entries: CallCentreDataAccessEntry[]; title: string }>) {
  if (!record) return <section className="vehicle-empty-state" aria-live="polite"><p className="eyebrow">No records found</p><h2>No incident was found for that GMT reference.</h2></section>;
  return <><RecordTable records={[record]} sites={sites} title={title} /><section className="vehicle-status-maintenance-panel" aria-labelledby="call-centre-access-results-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Legacy Call_Centre_Counter</p><h2 id="call-centre-access-results-title">Data access detail</h2></div><span className="form-hint">{entries.length} access record(s)</span></div>{!accessTableAvailable ? <p className="muted-copy">The legacy access table is not available in this database. The primary Call_centre record remains available.</p> : entries.length === 0 ? <p className="muted-copy">No per-access history was recorded for this GMT reference.</p> : <div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Call Centre data access history</caption><thead><tr><th scope="col">Counter</th><th scope="col">Data Capture ID</th><th scope="col">Data Capture Date</th><th scope="col">Data Capture Time</th></tr></thead><tbody>{entries.map((entry, index) => <tr key={`${entry.dataCaptureId ?? "access"}-${entry.dataCaptureDate ?? "date"}-${index}`}><td>{valueOrDash(entry.counter)}</td><td>{valueOrDash(entry.dataCaptureId)}</td><td>{valueOrDash(dateValue(entry.dataCaptureDate))}</td><td>{timeValue(entry.dataCaptureTime)}</td></tr>)}</tbody></table></div>}</section></>;
}

export async function CallCentreReportPage({ searchParams, forcedMode, routePath = "/call-centre/reports" }: CallCentreReportPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><ApiUnavailable path={routePath} /></main>;
  if (!hasRole(session.roles, REPORTS_ROLE)) return <main className="page-shell vehicle-page-shell"><AccessRestricted /></main>;

  const params = await searchParams;
  const mode = modeValue(forcedMode ?? queryValue(params.mode) ?? "");
  if (!mode) notFound();

  const startDate = normalizeDate(queryValue(params.startDate) ?? queryValue(params.BDAT));
  const endDate = normalizeDate(queryValue(params.endDate) ?? queryValue(params.EDAT));
  const referenceNumber = positiveInt(queryValue(params.referenceNumber) ?? queryValue(params.cccode));
  const searchQuery = (queryValue(params.searchQuery) ?? queryValue(params.xggnum) ?? "").trim();
  const selectedVmfCode = positiveInt(queryValue(params.vmfCode) ?? queryValue(params.vmf));
  const searchType = queryValue(params.searchType) === "GG" || queryValue(params.Radio1) === "Radiogg" ? "GG" : "GP";
  const selectedSiteCode = positiveInt(queryValue(params.siteCode) ?? queryValue(params.xsite) ?? queryValue(params.Site_code));
  const department = (queryValue(params.department) ?? queryValue(params.XDEPT) ?? "").trim();
  const captureName = (queryValue(params.captureName) ?? queryValue(params.XNAME) ?? "").trim();
  const shouldRun = queryValue(params.run) === "1" || Boolean(referenceNumber || searchQuery || startDate || endDate);

  let records: CallCentreIncidentRecord[] = [];
  let sites: CallCentreSiteOption[] = [];
  let vehicles: CallCentreVehicleOption[] = [];
  let accessTableAvailable = false;
  let accessEntries: CallCentreDataAccessEntry[] = [];
  let loadError = "";

  try {
    if (mode === "one-reference") {
      if (referenceNumber && shouldRun) records = [await getCallCentreIncident(referenceNumber)];
    } else if (mode === "data-access") {
      if (referenceNumber && shouldRun) {
        const [record, access] = await Promise.all([getCallCentreIncident(referenceNumber), getCallCentreDataAccess(referenceNumber)]);
        records = [record];
        accessTableAvailable = access.accessTableAvailable;
        accessEntries = access.entries;
      }
    } else if (mode === "one-vehicle") {
      if (searchQuery) vehicles = await searchCallCentreVehicles(searchQuery);
      const matchedVmfCode = selectedVmfCode ?? (vehicles.length === 1 ? vehicles[0].vmfCode : null);
      if (matchedVmfCode && shouldRun) records = (await getCallCentreIncidents()).filter((record) => record.vmfCode === matchedVmfCode);
    } else if (mode === "dept-site-period" || mode === "clo-report" || mode === "open-calls") {
      sites = await getCallCentreSites();
      if (shouldRun) records = await getCallCentreIncidents();
    } else if (mode === "all-reference" || mode === "statistics") {
      if (shouldRun) records = await getCallCentreIncidents();
    }
  } catch (caughtError) {
    if (caughtError instanceof CallCentreApiError && caughtError.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
    loadError = caughtError instanceof CallCentreApiError && caughtError.reason === "not-found" ? "The requested call centre incident was not found." : "The call centre service is temporarily unavailable. Please try again.";
  }

  if (mode === "all-reference") records = records.filter((record) => matchesIncident(record, incidentFilter(params)) && dateInRange(record.callDate, startDate, endDate));
  if (mode === "dept-site-period") records = records.filter((record) => (selectedSiteCode === null || reportSite(record) === selectedSiteCode) && (!startDate || dateInRange(record.callDate, startDate, endDate)) && (!department || sites.find((site) => site.code === reportSite(record))?.departmentNumber?.toLowerCase().includes(department.toLowerCase())));
  if (mode === "clo-report") records = records.filter((record) => record.croNotification?.toUpperCase() === "Y" && dateInRange(record.callDate, startDate, endDate) && (!department || sites.find((site) => site.code === reportSite(record))?.departmentNumber?.toLowerCase().includes(department.toLowerCase())));
  if (mode === "open-calls") records = records.filter((record) => record.callClosed?.toUpperCase() !== "Y" && dateInRange(record.callDate, startDate, endDate) && (selectedSiteCode === null || reportSite(record) === selectedSiteCode));
  if (mode === "statistics") records = records.filter((record) => dateInRange(record.callDate, startDate, endDate) && (!captureName || record.captureName?.toLowerCase().includes(captureName.toLowerCase())));

  const title = reportTitle(mode);
  return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="call-centre-report-title"><header className="vehicle-page-header"><div><p className="eyebrow">Call Centre / Reports</p><h1 id="call-centre-report-title">{title}</h1><p>Legacy report filters and the complete Call_centre field contract are preserved.</p></div><Link className="button button-secondary" href="/call-centre/reports">Report Menu</Link></header>{loadError ? <div className="notice notice-error" role="alert">{loadError}</div> : null}<ReportForm mode={mode} params={params} sites={sites} vehicles={vehicles} />{mode === "statistics" && shouldRun && !loadError ? <StatisticsResults records={records} captureName={captureName} title={title} /> : null}{mode === "data-access" && shouldRun && !loadError ? <DataAccessResults record={records[0] ?? null} sites={sites} accessTableAvailable={accessTableAvailable} entries={accessEntries} title={title} /> : null}{mode !== "statistics" && mode !== "data-access" && shouldRun && !loadError ? <RecordTable records={records} sites={sites} title={title} /> : null}</section></main>;
}

export default async function CallCentreReportRoute({ searchParams }: { searchParams: SearchParams }) {
  return CallCentreReportPage({ searchParams });
}
