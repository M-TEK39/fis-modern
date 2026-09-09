import Link from "next/link";

import {
  createLogsheetAction,
  deleteLogsheetAction,
  updateLogsheetAction,
} from "@/app/log-sheets/actions";
import { MenuSection } from "@/components/ui/menu-section";
import type { LogsheetRecord } from "@/lib/api-logsheets";
import type { SiteRecord } from "@/lib/api-sites";
import type { VehicleOption } from "@/lib/api-vehicles";

export function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

export function formatDate(value: string | null | undefined) {
  return value?.slice(0, 10) || "-";
}

export function formatNumber(value: number) {
  return new Intl.NumberFormat("en-ZA", { maximumFractionDigits: 2 }).format(value);
}

export function LogsheetShell({
  title,
  description,
  children,
}: Readonly<{ title: string; description: string; children: React.ReactNode }>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="logsheet-page-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Logsheet management</p>
            <h1 id="logsheet-page-title">{title}</h1>
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

export function LogsheetMenu({ canManage }: Readonly<{ canManage: boolean }>) {
  return (
    <MenuSection title="Logsheet Maintenance Menu">
      <Link className="vehicle-menu-link" href="/log-sheets/help">
        Logsheet Maintenance Information / Help
      </Link>
      <Link className="vehicle-menu-link" href="/log-sheets/enter">
        1) Enter a Logsheet
      </Link>
      {canManage ? (
        <>
          <Link className="vehicle-menu-link" href="/log-sheets/edit">
            2) Edit a Logsheet
          </Link>
          <Link className="vehicle-menu-link" href="/log-sheets/delete">
            3) Delete a Logsheet
          </Link>
        </>
      ) : null}
      <Link className="vehicle-menu-link" href="/log-sheets/reports/captured">
        4) Report: Logsheets captured
      </Link>
      <Link className="vehicle-menu-link" href="/log-sheets/reports/total-km">
        5) Report: Total Km per Class Code
      </Link>
      {canManage ? (
        <Link className="vehicle-menu-link" href="/log-sheets/edit">
          8) Edit Logsheets
        </Link>
      ) : null}
    </MenuSection>
  );
}

export function VehicleSearchForm({
  action,
  search,
  mode,
  vmfCode,
  options,
  requisition,
}: Readonly<{
  action: string;
  search: string;
  mode: string;
  vmfCode: string;
  options: readonly VehicleOption[];
  requisition?: string;
}>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get" action={action}>
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Logsheet lookup</p>
          <h2>Find by requisition or GG/GP number</h2>
        </div>
      </div>
      {requisition !== undefined ? (
        <div className="form-field">
          <label className="form-label" htmlFor="logsheet-requisition-search">
            Requisition number
          </label>
          <input
            className="form-input"
            id="logsheet-requisition-search"
            name="requisition"
            defaultValue={requisition}
            maxLength={10}
          />
        </div>
      ) : null}
      <div className="vehicle-search-row">
        <label className="sr-only" htmlFor="logsheet-search-mode">
          Number type
        </label>
        <select className="form-select" id="logsheet-search-mode" name="mode" defaultValue={mode}>
          <option value="GG">GG number</option>
          <option value="GP">GP number</option>
        </select>
        <label className="sr-only" htmlFor="logsheet-search">
          Vehicle number
        </label>
        <input
          className="vehicle-search"
          id="logsheet-search"
          name="search"
          defaultValue={search}
          placeholder={mode === "GP" ? "Enter GP number" : "Enter GG number"}
        />
        <label className="sr-only" htmlFor="logsheet-vmf-code">
          Vehicle
        </label>
        <select
          className="form-select"
          id="logsheet-vmf-code"
          name="vmfCode"
          defaultValue={vmfCode}
        >
          <option value="">Select vehicle</option>
          {options.map((vehicle) => (
            <option key={vehicle.vmfCode} value={vehicle.vmfCode}>
              {valueOrDash(vehicle.fleetNumber)} / {valueOrDash(vehicle.registrationNumber)} (
              {vehicle.vmfCode})
            </option>
          ))}
        </select>
        <button className="button button-primary" type="submit">
          Find
        </button>
        <Link className="button button-secondary" href={action}>
          Clear
        </Link>
      </div>
    </form>
  );
}

export function LogsheetTable({
  records,
  mode,
  returnPath,
}: Readonly<{
  records: readonly LogsheetRecord[];
  mode: "preview" | "edit" | "delete";
  returnPath: string;
}>) {
  if (records.length === 0) return <p className="muted-copy">No logsheets found.</p>;
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Vehicle logsheets</caption>
        <thead>
          <tr>
            <th scope="col">Requisition</th>
            <th scope="col">Vehicle</th>
            <th scope="col">Month</th>
            <th scope="col">Odometer</th>
            <th scope="col">Site</th>
            {mode !== "preview" ? <th scope="col">Action</th> : null}
          </tr>
        </thead>
        <tbody>
          {records.map((record) => (
            <tr key={record.logCode}>
              <td>{valueOrDash(record.requisitionNumber)}</td>
              <td>
                {valueOrDash(record.ggNumber ?? record.vmfCode)} /{" "}
                {valueOrDash(record.registrationNumber)}
              </td>
              <td>{formatDate(record.month)}</td>
              <td>
                {formatNumber(record.startOdometer)} - {formatNumber(record.endOdometer)}
              </td>
              <td>{valueOrDash(record.siteDescription ?? record.siteCode)}</td>
              {mode === "edit" ? (
                <td>
                  <Link
                    className="button button-secondary button-small"
                    href={`${returnPath}&edit=${record.logCode}`}
                  >
                    Edit
                  </Link>
                </td>
              ) : mode === "delete" ? (
                <td>
                  <form action={deleteLogsheetAction}>
                    <input name="returnPath" type="hidden" value={returnPath} />
                    <input name="logsheetId" type="hidden" value={record.logCode} />
                    <button className="button button-danger button-small" type="submit">
                      Delete
                    </button>
                  </form>
                </td>
              ) : null}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function LogsheetForm({
  record,
  vmfCode,
  sites,
  returnPath,
}: Readonly<{
  record: LogsheetRecord | null;
  vmfCode: number;
  sites: readonly SiteRecord[];
  returnPath: string;
}>) {
  const action = record ? updateLogsheetAction : createLogsheetAction;
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="logsheet-form-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{record ? `Logsheet #${record.logCode}` : "New logsheet"}</p>
          <h2 id="logsheet-form-title">{record ? "Edit logsheet" : "Enter a logsheet"}</h2>
        </div>
      </div>
      <form action={action}>
        <input name="returnPath" type="hidden" value={returnPath} />
        {record ? <input name="logsheetId" type="hidden" value={record.logCode} /> : null}
        <input name="vmfCode" type="hidden" value={vmfCode} />
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="logsheet-requisition">
              Requisition number
            </label>
            <input
              className="form-input"
              id="logsheet-requisition"
              name="requisition"
              required
              maxLength={10}
              defaultValue={record?.requisitionNumber ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="logsheet-month">
              Logsheet month
            </label>
            <input
              className="form-input"
              id="logsheet-month"
              name="month"
              required
              type="date"
              defaultValue={record?.month?.slice(0, 10) ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="logsheet-start-odo">
              Start odometer
            </label>
            <input
              className="form-input"
              id="logsheet-start-odo"
              name="startOdo"
              required
              min="0"
              step="any"
              type="number"
              defaultValue={record?.startOdometer ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="logsheet-end-odo">
              End odometer
            </label>
            <input
              className="form-input"
              id="logsheet-end-odo"
              name="endOdo"
              required
              min="0"
              step="any"
              type="number"
              defaultValue={record?.endOdometer ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="logsheet-days-used">
              Days used
            </label>
            <input
              className="form-input"
              id="logsheet-days-used"
              name="daysUsed"
              min="0"
              step="1"
              type="number"
              defaultValue={record?.daysUsed ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="logsheet-bundle">
              Batch number
            </label>
            <input
              className="form-input"
              id="logsheet-bundle"
              name="bundleNumber"
              min="1"
              step="1"
              type="number"
              defaultValue={record?.bundleNumber ?? ""}
            />
          </div>
          <div className="form-field form-group-full">
            <label className="form-label" htmlFor="logsheet-site">
              Site
            </label>
            <select
              className="form-select"
              id="logsheet-site"
              name="siteCode"
              required
              defaultValue={record?.siteCode ? String(record.siteCode) : ""}
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
            {record ? "Save changes" : "Save logsheet"}
          </button>
          <Link className="button button-secondary" href={returnPath}>
            Cancel
          </Link>
        </div>
      </form>
    </section>
  );
}

export function LogsheetReportTable({ records }: Readonly<{ records: readonly LogsheetRecord[] }>) {
  if (records.length === 0)
    return <p className="muted-copy">No records match the selected report criteria.</p>;
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Logsheet report results</caption>
        <thead>
          <tr>
            <th scope="col">Log ID</th>
            <th scope="col">GG number</th>
            <th scope="col">Captured</th>
            <th scope="col">Month</th>
            <th scope="col">Start ODO</th>
            <th scope="col">End ODO</th>
            <th scope="col">Site</th>
            <th scope="col">Requisition</th>
          </tr>
        </thead>
        <tbody>
          {records.map((record) => (
            <tr key={record.logCode}>
              <td>{record.logCode}</td>
              <td>{valueOrDash(record.ggNumber ?? record.vmfCode)}</td>
              <td>{formatDate(record.dateCreated)}</td>
              <td>{formatDate(record.month)}</td>
              <td>{formatNumber(record.startOdometer)}</td>
              <td>{formatNumber(record.endOdometer)}</td>
              <td>{valueOrDash(record.siteDescription ?? record.siteCode)}</td>
              <td>{valueOrDash(record.requisitionNumber)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
