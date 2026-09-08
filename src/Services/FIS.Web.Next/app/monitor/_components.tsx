import Link from "next/link";

import { saveMonitorAction } from "@/app/monitor/actions";
import type { MonitorDriverOption, MonitorRecord } from "@/lib/api-monitor";
import type { SiteRecord } from "@/lib/api-sites";

export function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

export function formatDate(value: string | null | undefined) {
  return value?.slice(0, 10) || "-";
}

export function MonitorShell({
  title,
  description,
  children,
}: Readonly<{ title: string; description: string; children: React.ReactNode }>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="monitor-page-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Call Centre Monitoring</p>
            <h1 id="monitor-page-title">{title}</h1>
            <p>{description}</p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>
        {children}
      </section>
    </main>
  );
}

export function MonitorNotice({
  query,
}: Readonly<{ query: Record<string, string | string[] | undefined> }>) {
  const queryText = (value: string | string[] | undefined) =>
    Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
  const saved = valueOrDash(queryText(query.saved));
  const updated = valueOrDash(queryText(query.updated));
  const error = valueOrDash(queryText(query.error));
  const message = error !== "-" ? error : saved !== "-" ? saved : updated !== "-" ? updated : "";
  if (!message) return null;
  return (
    <div
      className={`notice ${error !== "-" ? "notice-error" : "notice-success"}`}
      role={error !== "-" ? "alert" : "status"}
    >
      {message}
    </div>
  );
}

export function MonitorMenu() {
  return (
    <div className="vehicle-menu-tiles">
      <section className="vehicle-menu-tile">
        <h2 className="vehicle-menu-header">Monitor Maintenance Menu</h2>
        <div className="vehicle-menu-body">
          <Link className="vehicle-menu-link" href="/monitor/help">
            MONITOR INQUIRY Information / Help
          </Link>
        </div>
      </section>
      <section className="vehicle-menu-tile">
        <h2 className="vehicle-menu-header">INQUIRY Section</h2>
        <div className="vehicle-menu-body">
          <Link className="vehicle-menu-link" href="/monitor/capture">
            1) Capture a New Inquiry
          </Link>
          <Link className="vehicle-menu-link" href="/monitor/edit">
            2) Edit / Update an Existing Inquiry
          </Link>
          <Link className="vehicle-menu-link" href="/monitor/reports">
            3) Reports
          </Link>
        </div>
      </section>
    </div>
  );
}

export function MonitorTable({
  records,
  title = "Monitor inquiries",
}: Readonly<{ records: readonly MonitorRecord[]; title?: string }>) {
  if (records.length === 0) return <p className="muted-copy">No inquiries found.</p>;
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">{title}</caption>
        <thead>
          <tr>
            <th scope="col">Reference</th>
            <th scope="col">Capture date</th>
            <th scope="col">Vehicle</th>
            <th scope="col">Inquiry type</th>
            <th scope="col">Driver</th>
            <th scope="col">Site</th>
            <th scope="col">Description</th>
          </tr>
        </thead>
        <tbody>
          {records.map((record) => (
            <tr key={record.monitorCode}>
              <td>
                <Link href={`/monitor/edit?referenceNumber=${record.monitorCode}`}>
                  {record.monitorCode}
                </Link>
              </td>
              <td>{formatDate(record.captureDate)}</td>
              <td>{valueOrDash(record.fleetNumber ?? record.vmfCode)}</td>
              <td>{valueOrDash(record.inquiryType)}</td>
              <td>{valueOrDash(record.driverName ?? record.driverPersalNo)}</td>
              <td>{valueOrDash(record.driverSite)}</td>
              <td>{valueOrDash(record.inquiryDescription)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function driverLabel(driver: MonitorDriverOption) {
  const name = `${driver.firstname ?? ""} ${driver.surname ?? ""}`.trim() || "Unknown driver";
  return `${name} (${driver.code})`;
}

export function MonitorForm({
  record,
  vmfCode,
  sites,
  drivers,
  returnPath,
}: Readonly<{
  record: MonitorRecord | null;
  vmfCode: number;
  sites: readonly SiteRecord[];
  drivers: readonly MonitorDriverOption[];
  returnPath: string;
}>) {
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="monitor-form-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{record ? `Inquiry #${record.monitorCode}` : "New inquiry"}</p>
          <h2 id="monitor-form-title">
            {record ? "Edit / Update an Existing Inquiry" : "Capture a New Inquiry"}
          </h2>
        </div>
      </div>
      <form action={saveMonitorAction}>
        <input name="returnPath" type="hidden" value={returnPath} />
        {record ? <input name="monitorCode" type="hidden" value={record.monitorCode} /> : null}
        <input name="vmfCode" type="hidden" value={vmfCode} />
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="monitor-capture-date">
              Capture date
            </label>
            <input
              className="form-input"
              id="monitor-capture-date"
              name="captureDate"
              required
              type="date"
              defaultValue={formatDate(record?.captureDate)}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="monitor-inquiry-type">
              Inquiry type
            </label>
            <select
              className="form-select"
              id="monitor-inquiry-type"
              name="inquiryType"
              required
              defaultValue={record?.inquiryType ?? "Fuel_Consumption"}
            >
              <option value="Fuel_Consumption">Fuel Consumption</option>
              <option value="Fuel_overfill">Fuel overfill</option>
              <option value="NoFuel_6months">No fuel for 6 months</option>
              <option value="Oil_Consumption">Oil Consumption</option>
              <option value="Tyre_Usage">Tyre Usage</option>
              <option value="Card_Dormant">Card Dormant</option>
              <option value="NoMaintain_2000lt">No maintenance over 2000 litres</option>
            </select>
          </div>
          <div className="form-field form-group-full">
            <label className="form-label" htmlFor="monitor-description">
              Inquiry description
            </label>
            <input
              className="form-input"
              id="monitor-description"
              name="inquiryDescription"
              defaultValue={record?.inquiryDescription ?? ""}
            />
          </div>
          <div className="form-field form-group-full">
            <label className="form-label" htmlFor="monitor-driver-code">
              Driver
            </label>
            <select
              className="form-select"
              id="monitor-driver-code"
              name="driverCode"
              defaultValue=""
            >
              <option value="">Select driver (optional)</option>
              {drivers.map((driver) => (
                <option key={driver.code} value={driver.code}>
                  {driverLabel(driver)}
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="monitor-driver-name">
              Driver name
            </label>
            <input
              className="form-input"
              id="monitor-driver-name"
              name="driverName"
              maxLength={100}
              defaultValue={record?.driverName ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="monitor-driver-persal">
              Driver Persal number
            </label>
            <input
              className="form-input"
              id="monitor-driver-persal"
              name="driverPersalNo"
              maxLength={50}
              defaultValue={record?.driverPersalNo ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="monitor-driver-site">
              Driver site
            </label>
            <select
              className="form-select"
              id="monitor-driver-site"
              name="driverSite"
              defaultValue={record?.driverSite ?? ""}
            >
              <option value="">Select site</option>
              {sites.map((site) => (
                <option key={site.siteCode} value={site.siteCode}>
                  {site.description || "Unnamed site"} ({site.siteCode})
                </option>
              ))}
            </select>
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            {record ? "Save changes" : "Submit inquiry"}
          </button>
          <Link className="button button-secondary" href={returnPath}>
            Cancel
          </Link>
        </div>
      </form>
    </section>
  );
}

export function ReportTable({
  rows,
}: Readonly<{
  rows: readonly {
    monitorCode: number;
    vmfCode: number | null;
    captureDate: string | null;
    inquiryType: string;
    inquiryDescription: string;
    driverName: string;
    driverSite: number | null;
  }[];
}>) {
  if (rows.length === 0)
    return <p className="muted-copy">No inquiries match the selected criteria.</p>;
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Monitor report results</caption>
        <thead>
          <tr>
            <th scope="col">Reference</th>
            <th scope="col">Capture date</th>
            <th scope="col">Vehicle</th>
            <th scope="col">Inquiry type</th>
            <th scope="col">Driver</th>
            <th scope="col">Site</th>
            <th scope="col">Description</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.monitorCode}>
              <td>{row.monitorCode}</td>
              <td>{formatDate(row.captureDate)}</td>
              <td>{valueOrDash(row.vmfCode)}</td>
              <td>{valueOrDash(row.inquiryType)}</td>
              <td>{valueOrDash(row.driverName)}</td>
              <td>{valueOrDash(row.driverSite)}</td>
              <td>{valueOrDash(row.inquiryDescription)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
