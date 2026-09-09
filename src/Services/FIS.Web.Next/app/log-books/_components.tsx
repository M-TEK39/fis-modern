import Link from "next/link";

import { deleteLogbookAction } from "@/app/log-books/actions";
import { MenuSection } from "@/components/ui/menu-section";
import type { LogbookRecord } from "@/lib/api-logbooks";

export function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

export function formatDate(value: string | null | undefined) {
  return value?.slice(0, 10) || "-";
}

export function LogbookShell({
  title,
  description,
  children,
}: Readonly<{ title: string; description: string; children: React.ReactNode }>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="logbook-page-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Log book management</p>
            <h1 id="logbook-page-title">{title}</h1>
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

export function LogbookMenu() {
  return (
    <MenuSection title="Logbook Maintenance Menu">
      <Link className="vehicle-menu-link" href="/log-books/help">
        Logbook Maintenance Information / Help
      </Link>
      <Link className="vehicle-menu-link" href="/log-books/maintenance">
        1) Logbook Maintenance
      </Link>
      <Link className="vehicle-menu-link" href="/log-books/collection">
        2) Collection for TWO or More Logbooks
      </Link>
      <Link className="vehicle-menu-link" href="/log-books/delete">
        3) Delete a Logbook Handout
      </Link>
    </MenuSection>
  );
}

export function VehicleSearchForm({
  action,
  search,
  mode,
  vmfCode,
  options,
  multiple = false,
  selectedVmfCodes = [],
}: Readonly<{
  action: string;
  search: string;
  mode: string;
  vmfCode: string;
  options: readonly {
    vmfCode: number;
    fleetNumber: string | null;
    registrationNumber: string | null;
  }[];
  multiple?: boolean;
  selectedVmfCodes?: readonly number[];
}>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get" action={action}>
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Vehicle lookup</p>
          <h2>Find vehicles by GG or GP number</h2>
        </div>
      </div>
      <div className="vehicle-search-row">
        <label className="sr-only" htmlFor="logbook-search-mode">
          Number type
        </label>
        <select className="form-select" id="logbook-search-mode" name="mode" defaultValue={mode}>
          <option value="GG">GG number</option>
          <option value="GP">GP number</option>
        </select>
        <label className="sr-only" htmlFor="logbook-search">
          Vehicle number
        </label>
        <input
          className="vehicle-search"
          id="logbook-search"
          name="search"
          defaultValue={search}
          placeholder={mode === "GP" ? "Enter GP number" : "Enter GG number"}
        />
        <label className="sr-only" htmlFor="logbook-vmf-code">
          Vehicle
        </label>
        <select
          className="form-select"
          id="logbook-vmf-code"
          name="vmfCode"
          defaultValue={multiple ? undefined : vmfCode}
          multiple={multiple}
          size={multiple ? Math.min(Math.max(options.length, 3), 8) : undefined}
        >
          {!multiple ? <option value="">Select vehicle</option> : null}
          {options.map((vehicle) => (
            <option
              key={vehicle.vmfCode}
              value={vehicle.vmfCode}
              selected={multiple && selectedVmfCodes.includes(vehicle.vmfCode)}
            >
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
      {multiple ? (
        <p className="muted-copy">
          Use Ctrl/Cmd-click to select more than one vehicle, then choose Find to refresh the
          matches.
        </p>
      ) : null}
    </form>
  );
}

export function LogbookTable({
  records,
  mode,
  returnPath,
}: Readonly<{
  records: readonly LogbookRecord[];
  mode: "preview" | "maintenance" | "delete";
  returnPath: string;
}>) {
  if (records.length === 0) return <p className="muted-copy">No logbook handouts found.</p>;
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Logbook handouts</caption>
        <thead>
          <tr>
            <th scope="col">GG number</th>
            <th scope="col">Registration</th>
            <th scope="col">Begin number</th>
            <th scope="col">End number</th>
            <th scope="col">Handout date</th>
            <th scope="col">Site</th>
            <th scope="col">Receiver</th>
            {mode !== "preview" ? <th scope="col">Action</th> : null}
          </tr>
        </thead>
        <tbody>
          {records.map((record) => (
            <tr key={record.logbookCode}>
              <td>{valueOrDash(record.ggNumber ?? record.vmfCode)}</td>
              <td>{valueOrDash(record.registrationNumber)}</td>
              <td>{valueOrDash(record.beginNumber)}</td>
              <td>{valueOrDash(record.endNumber)}</td>
              <td>{formatDate(record.handoutDate)}</td>
              <td>{valueOrDash(record.siteDescription ?? record.siteCode)}</td>
              <td>{valueOrDash(record.receiverName)}</td>
              {mode === "maintenance" ? (
                <td>
                  <Link
                    className="button button-secondary button-small"
                    href={`${returnPath}&edit=${record.logbookCode}`}
                  >
                    Edit
                  </Link>
                </td>
              ) : mode === "delete" ? (
                <td>
                  <form action={deleteLogbookAction}>
                    <input name="returnPath" type="hidden" value={returnPath} />
                    <input name="logbookId" type="hidden" value={record.logbookCode} />
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
