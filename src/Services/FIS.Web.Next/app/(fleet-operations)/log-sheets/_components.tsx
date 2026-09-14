import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";

import {
  createLogsheetAction,
  deleteLogsheetAction,
  updateLogsheetAction,
} from "@/app/(fleet-operations)/log-sheets/actions";
import { MenuSection } from "@/components/ui/menu-section";
import type {
  LogsheetContractOption,
  LogsheetRecord,
} from "@/lib/api/fleet-operations/api-logsheets";
import type { VehicleOption } from "@/lib/api/vehicles/api-vehicles";
import { formatDate, formatNumber, valueOrDash } from "@/app/(fleet-operations)/log-sheets/_utils";

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
  resetPage = false,
}: Readonly<{
  action: string;
  search: string;
  mode: string;
  vmfCode: string;
  options: readonly VehicleOption[];
  requisition?: string;
  resetPage?: boolean;
}>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get" action={action}>
      {resetPage ? <input name="page" type="hidden" value="1" /> : null}
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
  contracts,
  returnPath,
}: Readonly<{
  record: LogsheetRecord | null;
  vmfCode: number;
  contracts: readonly LogsheetContractOption[];
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
            <label className="form-label" htmlFor="logsheet-contract">
              Contract
            </label>
            <select
              className="form-select"
              id="logsheet-contract"
              name="contractCode"
              required
              defaultValue={record?.contractCode ? String(record.contractCode) : ""}
            >
              <option value="">Select contract</option>
              {contracts.map((contract) => (
                <option key={contract.contractCode} value={contract.contractCode}>
                  #{contract.contractCode} · {contract.startDate?.slice(0, 10) ?? "No start date"} · {contract.siteDescription ?? `Site ${contract.siteCode}`}
                </option>
              ))}
            </select>
            <p className="form-hint">The selected legacy contract determines the saved site and department.</p>
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
        <DataTableHeader
          columns={[
            { key: "column-1", label: <>Log ID</> },
            { key: "column-2", label: <>GG number</> },
            { key: "column-3", label: <>Captured</> },
            { key: "column-4", label: <>Month</> },
            { key: "column-5", label: <>Start ODO</> },
            { key: "column-6", label: <>End ODO</> },
            { key: "column-7", label: <>Site</> },
            { key: "column-8", label: <>Requisition</> },
          ]}
        />
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
