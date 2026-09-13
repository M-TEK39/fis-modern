import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SearchTypeFieldset from "@/components/ui/search-type-fieldset";
import {
  CallCentreApiError,
  getCallCentreCaptureStatistics,
  getCallCentreIncident,
  getCallCentreDataAccess,
  getCallCentreReportPage,
  getCallCentreSites,
  searchCallCentreVehicles,
  type CallCentreIncidentRecord,
  type CallCentreCaptureStatistics,
  type CallCentreReportPage,
  type CallCentreDataAccessEntry,
  type CallCentreSiteOption,
  type CallCentreVehicleOption,
} from "@/lib/api/fleet-operations/api-call-centre";
import { getSession } from "@/lib/auth/session";

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

function reportPageNumber(value: string | undefined) {
  return positiveInt(value) ?? 1;
}

function reportPageHref(
  routePath: string,
  params: Record<string, string | string[] | undefined>,
  page: number,
) {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (key === "page" || key === "pageSize") continue;
    const text = queryValue(value)?.trim();
    if (text) query.set(key, text);
  }
  query.set("page", String(page));
  return `${routePath}?${query.toString()}`;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
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
  return REPORT_MODES.includes(mode as ReportMode) ? (mode as ReportMode) : null;
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

function CallCentreReportPagination({
  report,
  pageHref,
}: Readonly<{
  report: Pick<CallCentreReportPage, "page" | "totalPages">;
  pageHref: (page: number) => string;
}>) {
  if (report.totalPages <= 1) return null;

  return (
    <nav className="vehicle-pagination report-print-hide" aria-label="Call Centre report pages">
      {report.page > 1 ? (
        <Link className="vehicle-pagination-button" href={pageHref(report.page - 1)}>
          Previous
        </Link>
      ) : (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Previous
        </span>
      )}
      <span className="vehicle-pagination-meta" aria-live="polite">
        Page {report.page} of {report.totalPages}
      </span>
      {report.page < report.totalPages ? (
        <Link className="vehicle-pagination-button" href={pageHref(report.page + 1)}>
          Next
        </Link>
      ) : (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Next
        </span>
      )}
    </nav>
  );
}

function reportSite(record: CallCentreIncidentRecord) {
  return record.transportOfficerSite ?? record.driverSite;
}

function siteText(siteCode: number | null, sites: CallCentreSiteOption[]) {
  if (siteCode === null) return "-";
  const site = sites.find((candidate) => candidate.code === siteCode);
  return site
    ? `${site.departmentNumber ? `${site.departmentNumber} - ` : ""}${site.description}`
    : String(siteCode);
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
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to run Call Centre reports.</h2>
      <p className="muted-copy">This page requires the Reports role.</p>
    </section>
  );
}

function ApiUnavailable({ path }: Readonly<{ path: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Service unavailable</p>
      <h2>Call Centre report data could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href={path}>
        Try again
      </Link>
    </section>
  );
}

const ReportForm = renderReportForm;

function renderReportForm({
  mode,
  params,
  sites,
  vehicles,
}: Readonly<{
  mode: ReportMode;
  params: Record<string, string | string[] | undefined>;
  sites: CallCentreSiteOption[];
  vehicles: CallCentreVehicleOption[];
}>) {
  const searchType =
    (queryValue(params.searchType) ?? queryValue(params.Radio1) ?? "GP").toUpperCase() === "GG" ||
    queryValue(params.Radio1) === "Radiogg"
      ? "GG"
      : "GP";
  const searchQuery = (queryValue(params.searchQuery) ?? queryValue(params.xggnum) ?? "").trim();
  const selectedVmfCode = positiveInt(queryValue(params.vmfCode) ?? queryValue(params.vmf));
  const referenceNumber = (
    queryValue(params.referenceNumber) ??
    queryValue(params.cccode) ??
    ""
  ).trim();
  const startDate = normalizeDate(queryValue(params.startDate) ?? queryValue(params.BDAT));
  const endDate = normalizeDate(queryValue(params.endDate) ?? queryValue(params.EDAT));
  const selectedSiteCode = positiveInt(
    queryValue(params.siteCode) ?? queryValue(params.xsite) ?? queryValue(params.Site_code),
  );
  const department = (queryValue(params.department) ?? queryValue(params.XDEPT) ?? "").trim();
  const captureName = (queryValue(params.captureName) ?? queryValue(params.XNAME) ?? "").trim();
  const selectedIncident = incidentFilter(params);

  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <input name="run" type="hidden" value="1" />
      {mode === "one-reference" ? (
        <div className="form-grid">
          <div className="field">
            <label htmlFor="call-report-reference">Reference Number (GMT)</label>
            <input
              id="call-report-reference"
              name="referenceNumber"
              inputMode="numeric"
              defaultValue={referenceNumber}
              required
            />
          </div>
        </div>
      ) : null}
      {mode === "one-vehicle" ? (
        <div className="form-grid">
          <SearchTypeFieldset selectedType={searchType} legend="Find vehicle by" firstOption="GP" />
          <div className="field">
            <label htmlFor="call-report-vehicle-search">Vehicle Number</label>
            <input
              id="call-report-vehicle-search"
              name="searchQuery"
              maxLength={30}
              defaultValue={searchQuery}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="call-report-vehicle">Vehicle</label>
            <select id="call-report-vehicle" name="vmfCode" defaultValue={selectedVmfCode ?? ""}>
              <option value="">Select vehicle...</option>
              {vehicles.map((vehicle) => (
                <option key={vehicle.vmfCode} value={vehicle.vmfCode}>
                  {vehicle.displayText}
                </option>
              ))}
            </select>
            {vehicles.length === 0 && searchQuery ? (
              <p className="form-hint">
                No exact vehicle match was found. Search by the other identifier.
              </p>
            ) : null}
          </div>
        </div>
      ) : null}
      {mode === "all-reference" ? (
        <div className="form-grid">
          <fieldset className="vehicle-search-options">
            <legend>Incident</legend>
            {[
              ["all", "ALL"],
              ["accident", "Accident"],
              ["hijack", "Hi-Jack"],
              ["loss", "Loss"],
              ["road", "Road Assist"],
            ].map(([value, label]) => (
              <label className="vehicle-checkbox-label" key={value}>
                <input
                  type="radio"
                  name="incident"
                  value={value}
                  defaultChecked={selectedIncident === value}
                />{" "}
                {label}
              </label>
            ))}
          </fieldset>
          <DateField id="all-start" name="startDate" label="Begin Date" value={startDate} />
          <DateField id="all-end" name="endDate" label="End Date" value={endDate} />
        </div>
      ) : null}
      {mode === "dept-site-period" ? (
        <div className="form-grid">
          <div className="field">
            <label htmlFor="call-report-site">Site</label>
            <select
              id="call-report-site"
              name="siteCode"
              defaultValue={selectedSiteCode ?? ""}
              required
            >
              <option value="">Select site</option>
              {sites.map((site) => (
                <option key={site.code} value={site.code}>
                  {site.departmentNumber ? `${site.departmentNumber} - ` : ""}
                  {site.description}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="call-report-department">Department number</label>
            <input
              id="call-report-department"
              name="department"
              maxLength={30}
              defaultValue={department}
            />
          </div>
          <DateField
            id="dept-start"
            name="startDate"
            label="Begin Date"
            value={startDate}
            required
          />
          <DateField id="dept-end" name="endDate" label="End Date" value={endDate} required />
        </div>
      ) : null}
      {mode === "statistics" ? (
        <div className="form-grid">
          <DateField
            id="stats-start"
            name="startDate"
            label="Begin Date"
            value={startDate}
            required
          />
          <DateField id="stats-end" name="endDate" label="End Date" value={endDate} required />
          <div className="field">
            <label htmlFor="capture-name">Capture Person Last Name</label>
            <input id="capture-name" name="captureName" maxLength={60} defaultValue={captureName} />
          </div>
        </div>
      ) : null}
      {mode === "clo-report" ? (
        <div className="form-grid">
          <DateField
            id="clo-start"
            name="startDate"
            label="Begin Date"
            value={startDate}
            required
          />
          <DateField id="clo-end" name="endDate" label="End Date" value={endDate} required />
          <div className="field">
            <label htmlFor="clo-department">Department number</label>
            <input id="clo-department" name="department" maxLength={30} defaultValue={department} />
          </div>
        </div>
      ) : null}
      {mode === "data-access" ? (
        <div className="form-grid">
          <div className="field">
            <label htmlFor="access-reference">Reference Number (GMT)</label>
            <input
              id="access-reference"
              name="referenceNumber"
              inputMode="numeric"
              defaultValue={referenceNumber}
              required
            />
          </div>
        </div>
      ) : null}
      {mode === "open-calls" ? (
        <div className="form-grid">
          <DateField
            id="open-start"
            name="startDate"
            label="Begin Date"
            value={startDate}
            required
          />
          <DateField id="open-end" name="endDate" label="End Date" value={endDate} required />
          <div className="field">
            <label htmlFor="open-site">Site</label>
            <select id="open-site" name="siteCode" defaultValue={selectedSiteCode ?? ""}>
              <option value="">All sites</option>
              {sites.map((site) => (
                <option key={site.code} value={site.code}>
                  {site.departmentNumber ? `${site.departmentNumber} - ` : ""}
                  {site.description}
                </option>
              ))}
            </select>
          </div>
        </div>
      ) : null}
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href="/call-centre/reports">
          Report Menu
        </Link>
      </div>
    </form>
  );
}

function DateField({
  id,
  name,
  label,
  value,
  required = false,
}: Readonly<{ id: string; name: string; label: string; value: string; required?: boolean }>) {
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <input id={id} name={name} type="date" defaultValue={value} required={required} />
    </div>
  );
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

function fieldValue(
  record: CallCentreIncidentRecord,
  key: keyof CallCentreIncidentRecord,
  sites: CallCentreSiteOption[],
) {
  const value = record[key];
  if (key === "callDate" || key === "incidentDate") return dateValue(value as string | null);
  if (key === "callTime" || key === "incidentTime") return timeValue(value as string | null);
  if (key === "driverSite" || key === "transportOfficerSite")
    return siteText(value as number | null, sites);
  return valueOrDash(value as string | number | null | undefined);
}

function LegacyRecordDetails({
  record,
  sites,
}: Readonly<{ record: CallCentreIncidentRecord; sites: CallCentreSiteOption[] }>) {
  return (
    <details className="vehicle-record-details">
      <summary>Show full legacy fields</summary>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">Full Call_centre legacy record</caption>
          <tbody>
            {LEGACY_FIELDS.map((field) => (
              <tr key={field.key}>
                <th scope="row">{field.label}</th>
                <td>{fieldValue(record, field.key, sites)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </details>
  );
}

function RecordTable({
  records,
  sites,
  title,
  report,
  pageHref,
}: Readonly<{
  records: CallCentreIncidentRecord[];
  sites: CallCentreSiteOption[];
  title: string;
  report?: CallCentreReportPage | null;
  pageHref?: (page: number) => string;
}>) {
  if (records.length === 0)
    return (
      <section className="vehicle-empty-state" aria-live="polite">
        <p className="eyebrow">No records found</p>
        <h2>No incidents matched the selected filters.</h2>
        <p className="muted-copy">Update the report criteria and submit again.</p>
      </section>
    );
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="call-centre-report-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="call-centre-report-results-title">{title}</h2>
        </div>
        <span className="form-hint">{report?.total ?? records.length} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">{title}</caption>
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>Reference Number</> },
              { key: "column-2", label: <>Incident</> },
              { key: "column-3", label: <>Call Date</> },
              { key: "column-4", label: <>Caller</> },
              { key: "column-5", label: <>Driver</> },
              { key: "column-6", label: <>Site</> },
              { key: "column-7", label: <>GG Number</> },
            ]}
          />
          <tbody>
            {records.map((record) => (
              <tr key={record.code}>
                <td>{record.code}</td>
                <td>{valueOrDash(record.incidentType)}</td>
                <td>{valueOrDash(dateValue(record.callDate))}</td>
                <td>{valueOrDash(record.callerName)}</td>
                <td>{valueOrDash(record.driverName)}</td>
                <td>{siteText(reportSite(record), sites)}</td>
                <td>{valueOrDash(record.ggNumber)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {records.map((record) => (
        <LegacyRecordDetails key={`details-${record.code}`} record={record} sites={sites} />
      ))}
      {report && pageHref ? (
        <CallCentreReportPagination report={report} pageHref={pageHref} />
      ) : null}
    </section>
  );
}

function StatisticsResults({
  statistics,
  title,
}: Readonly<{ statistics: CallCentreCaptureStatistics; title: string }>) {
  const groups = statistics.items;
  if (groups.length === 0)
    return (
      <section className="vehicle-empty-state">
        <p className="eyebrow">No records found</p>
        <h2>No statistics matched the selected filters.</h2>
      </section>
    );
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="call-centre-statistics-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="call-centre-statistics-results-title">{title}</h2>
        </div>
        <span className="form-hint">{statistics.totalCalls} incident(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">Call Centre statistics by capture name</caption>
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>Capture Name</> },
              { key: "column-2", label: <>Count</> },
            ]}
          />
          <tbody>
            {groups.map((item) => (
              <tr key={item.captureName}>
                <td>{item.captureName}</td>
                <td>{item.count}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function DataAccessResults({
  record,
  sites,
  accessTableAvailable,
  entries,
  total,
  page,
  totalPages,
  pageHref,
  title,
}: Readonly<{
  record: CallCentreIncidentRecord | null;
  sites: CallCentreSiteOption[];
  accessTableAvailable: boolean;
  entries: CallCentreDataAccessEntry[];
  total: number;
  page: number;
  totalPages: number;
  pageHref: (page: number) => string;
  title: string;
}>) {
  if (!record)
    return (
      <section className="vehicle-empty-state" aria-live="polite">
        <p className="eyebrow">No records found</p>
        <h2>No incident was found for that GMT reference.</h2>
      </section>
    );
  return (
    <>
      <RecordTable records={[record]} sites={sites} title={title} />
      <section
        className="vehicle-status-maintenance-panel"
        aria-labelledby="call-centre-access-results-title"
      >
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy Call_Centre_Counter</p>
            <h2 id="call-centre-access-results-title">Data access detail</h2>
          </div>
          <span className="form-hint">{total} access record(s)</span>
        </div>
        {!accessTableAvailable ? (
          <p className="muted-copy">
            The legacy access table is not available in this database. The primary Call_centre
            record remains available.
          </p>
        ) : entries.length === 0 ? (
          <p className="muted-copy">No per-access history was recorded for this GMT reference.</p>
        ) : (
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">Call Centre data access history</caption>
              <DataTableHeader
                columns={[
                  { key: "column-1", label: <>Counter</> },
                  { key: "column-2", label: <>Data Capture ID</> },
                  { key: "column-3", label: <>Data Capture Date</> },
                  { key: "column-4", label: <>Data Capture Time</> },
                ]}
              />
              <tbody>
                {entries.map((entry) => (
                  <tr
                    key={
                      entry.counterCode ??
                      `${entry.dataCaptureId ?? "access"}-${entry.dataCaptureDate ?? "date"}-${entry.dataCaptureTime ?? "time"}`
                    }
                  >
                    <td>{valueOrDash(entry.counter)}</td>
                    <td>{valueOrDash(entry.dataCaptureId)}</td>
                    <td>{valueOrDash(dateValue(entry.dataCaptureDate))}</td>
                    <td>{timeValue(entry.dataCaptureTime)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <CallCentreReportPagination report={{ page, totalPages }} pageHref={pageHref} />
      </section>
    </>
  );
}

const CallCentreReportContent = renderCallCentreReportContent;

async function renderCallCentreReportContent({
  searchParams,
  forcedMode,
  routePath = "/call-centre/reports",
}: CallCentreReportPageProps) {
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
        <ApiUnavailable path={routePath} />
      </main>
    );
  if (!hasRole(session.roles, REPORTS_ROLE))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const params = await searchParams;
  const mode = modeValue(forcedMode ?? queryValue(params.mode) ?? "");
  if (!mode) notFound();

  const startDate = normalizeDate(queryValue(params.startDate) ?? queryValue(params.BDAT));
  const endDate = normalizeDate(queryValue(params.endDate) ?? queryValue(params.EDAT));
  const referenceNumber = positiveInt(
    queryValue(params.referenceNumber) ?? queryValue(params.cccode),
  );
  const searchQuery = (queryValue(params.searchQuery) ?? queryValue(params.xggnum) ?? "").trim();
  const selectedVmfCode = positiveInt(queryValue(params.vmfCode) ?? queryValue(params.vmf));
  const searchType =
    queryValue(params.searchType) === "GG" || queryValue(params.Radio1) === "Radiogg" ? "GG" : "GP";
  const selectedSiteCode = positiveInt(
    queryValue(params.siteCode) ?? queryValue(params.xsite) ?? queryValue(params.Site_code),
  );
  const department = (queryValue(params.department) ?? queryValue(params.XDEPT) ?? "").trim();
  const captureName = (queryValue(params.captureName) ?? queryValue(params.XNAME) ?? "").trim();
  const page = reportPageNumber(queryValue(params.page));
  const shouldRun =
    queryValue(params.run) === "1" ||
    Boolean(referenceNumber || searchQuery || startDate || endDate);

  let records: CallCentreIncidentRecord[] = [];
  let pagedReport: CallCentreReportPage | null = null;
  let statistics: CallCentreCaptureStatistics | null = null;
  let sites: CallCentreSiteOption[] = [];
  let vehicles: CallCentreVehicleOption[] = [];
  let accessTableAvailable = false;
  let accessEntries: CallCentreDataAccessEntry[] = [];
  let accessPage = 1;
  let accessTotal = 0;
  let accessTotalPages = 1;
  let loadError = "";

  try {
    if (mode === "one-reference") {
      if (referenceNumber && shouldRun) records = [await getCallCentreIncident(referenceNumber)];
    } else if (mode === "data-access") {
      if (referenceNumber && shouldRun) {
        const [record, access] = await Promise.all([
          getCallCentreIncident(referenceNumber),
          getCallCentreDataAccess(referenceNumber, page),
        ]);
        records = [record];
        accessTableAvailable = access.accessTableAvailable;
        accessEntries = access.entries;
        accessPage = access.page;
        accessTotal = access.total;
        accessTotalPages = access.totalPages;
      }
    } else if (mode === "one-vehicle") {
      if (searchQuery) vehicles = await searchCallCentreVehicles(searchQuery);
      const matchedVmfCode =
        selectedVmfCode ?? (vehicles.length === 1 ? vehicles[0].vmfCode : null);
      if (matchedVmfCode && shouldRun) {
        pagedReport = await getCallCentreReportPage({
          mode,
          page,
          vmfCode: matchedVmfCode,
        });
        records = pagedReport.items;
      }
    } else if (mode === "dept-site-period" || mode === "clo-report" || mode === "open-calls") {
      sites = await getCallCentreSites();
      if (shouldRun) {
        pagedReport = await getCallCentreReportPage({
          mode,
          page,
          startDate: startDate || undefined,
          endDate: endDate || undefined,
          siteCode: selectedSiteCode ?? undefined,
          department: department || undefined,
        });
        records = pagedReport.items;
      }
    } else if (mode === "all-reference") {
      if (shouldRun) {
        pagedReport = await getCallCentreReportPage({
          mode,
          page,
          incident: incidentFilter(params),
          startDate: startDate || undefined,
          endDate: endDate || undefined,
        });
        records = pagedReport.items;
      }
    } else if (mode === "statistics") {
      if (shouldRun) {
        statistics = await getCallCentreCaptureStatistics({
          startDate: startDate || undefined,
          endDate: endDate || undefined,
          captureName: captureName || undefined,
        });
      }
    }
  } catch (caughtError) {
    if (caughtError instanceof CallCentreApiError && caughtError.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    loadError =
      caughtError instanceof CallCentreApiError && caughtError.reason === "not-found"
        ? "The requested call centre incident was not found."
        : "The call centre service is temporarily unavailable. Please try again.";
  }

  const title = reportTitle(mode);
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="call-centre-report-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Call Centre / Reports</p>
            <h1 id="call-centre-report-title">{title}</h1>
            <p>Legacy report filters and the complete Call_centre field contract are preserved.</p>
          </div>
          <Link className="button button-secondary" href="/call-centre/reports">
            Report Menu
          </Link>
        </header>
        {loadError ? (
          <div className="notice notice-error" role="alert">
            {loadError}
          </div>
        ) : null}
        <ReportForm mode={mode} params={params} sites={sites} vehicles={vehicles} />
        {mode === "statistics" && shouldRun && !loadError && statistics ? (
          <StatisticsResults statistics={statistics} title={title} />
        ) : null}
        {mode === "data-access" && shouldRun && !loadError ? (
          <DataAccessResults
            record={records[0] ?? null}
            sites={sites}
            accessTableAvailable={accessTableAvailable}
            entries={accessEntries}
            total={accessTotal}
            page={accessPage}
            totalPages={accessTotalPages}
            pageHref={(requestedPage) => reportPageHref(routePath, params, requestedPage)}
            title={title}
          />
        ) : null}
        {mode !== "statistics" && mode !== "data-access" && shouldRun && !loadError ? (
          <RecordTable
            records={records}
            sites={sites}
            title={title}
            report={pagedReport}
            pageHref={(requestedPage) => reportPageHref(routePath, params, requestedPage)}
          />
        ) : null}
      </section>
    </main>
  );
}

export function CallCentreReportPage(props: CallCentreReportPageProps) {
  return (
    <StreamedRoute>
      <CallCentreReportContent {...props} />
    </StreamedRoute>
  );
}

export default function CallCentreReportRoute({ searchParams }: { searchParams: SearchParams }) {
  return <CallCentreReportPage searchParams={searchParams} />;
}
