import Link from "next/link";

import { saveTrackingAction } from "@/app/(fleet-operations)/tracking/actions";
import DataTableHeader from "@/components/ui/data-table-header";
import { MenuSection } from "@/components/ui/menu-section";
import type { TrackingRecord } from "@/lib/api/fleet-operations/api-tracking";
import type { VehicleOption } from "@/lib/api/vehicles/api-vehicles";
import { valueOrDash, formatDate } from "./_utils";

function queryText(value: string | string[] | undefined) {
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

export function TrackingShell({
  title,
  description,
  children,
}: Readonly<{ title: string; description: string; children: React.ReactNode }>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="tracking-page-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Tracking</p>
            <h1 id="tracking-page-title">{title}</h1>
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

export function TrackingNotice({
  query,
}: Readonly<{ query: Record<string, string | string[] | undefined> }>) {
  const error = queryText(query.error);
  const saved = queryText(query.saved);
  const updated = queryText(query.updated);
  const message = error || saved || updated;
  if (!message) return null;
  return (
    <div
      className={`notice ${error ? "notice-error" : "notice-success"}`}
      role={error ? "alert" : "status"}
    >
      {message}
    </div>
  );
}

export function TrackingMenu() {
  return (
    <div className="vehicle-menu-tiles">
      <MenuSection title="Tracking Maintenance Menu">
        <Link className="vehicle-menu-link" href="/tracking/maintenance">
          1) Tracking Maintenance
        </Link>
        <Link className="vehicle-menu-link" href="/tracking/help">
          2) Tracking Information / Help
        </Link>
      </MenuSection>
      <MenuSection title="Tracking Reports">
        <Link className="vehicle-menu-link" href="/tracking/reports">
          3) Tracking Reports
        </Link>
      </MenuSection>
    </div>
  );
}

export function TrackingTable({
  records,
  title = "Tracking records",
  editable = true,
  returnPath,
}: Readonly<{
  records: readonly TrackingRecord[];
  title?: string;
  editable?: boolean;
  returnPath?: string;
}>) {
  if (records.length === 0) return <p className="muted-copy">No tracking records found.</p>;
  const columns = [
    { key: "tracker-number", label: "Tracker number" },
    { key: "vehicle", label: "Vehicle" },
    { key: "install-date", label: "Install date" },
    { key: "status", label: "Status" },
    { key: "type", label: "Type" },
    { key: "remove-date", label: "Remove date" },
    ...(editable ? [{ key: "action", label: "Action" }] : []),
  ];
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">{title}</caption>
        <DataTableHeader columns={columns} />
        <tbody>
          {records.map((record) => (
            <tr key={record.trackCode}>
              <td>{valueOrDash(record.trackNumber)}</td>
              <td>
                {valueOrDash(record.fleetNumber ?? record.registrationNumber ?? record.vmfCode)}
              </td>
              <td>{formatDate(record.installDate)}</td>
              <td>{valueOrDash(record.status)}</td>
              <td>{valueOrDash(record.type)}</td>
              <td>{formatDate(record.removeDate)}</td>
              {editable ? (
                <td>
                  <Link
                    className="button button-secondary"
                    href={
                      returnPath
                        ? `${returnPath}${returnPath.includes("?") ? "&" : "?"}trackCode=${encodeURIComponent(record.trackCode)}`
                        : `/tracking/maintenance?trackCode=${record.trackCode}`
                    }
                  >
                    Edit
                  </Link>
                </td>
              ) : null}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function TrackingForm({
  record,
  vmfCode,
  returnPath,
}: Readonly<{ record: TrackingRecord | null; vmfCode: number | null; returnPath: string }>) {
  const selectedType = record?.type === "Re-used" ? "Reused" : (record?.type ?? "New");
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="tracking-form-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">
            {record ? `Tracking record #${record.trackCode}` : "New tracking record"}
          </p>
          <h2 id="tracking-form-title">
            {record ? "Edit Tracking Record" : "Capture Tracking Record"}
          </h2>
        </div>
      </div>
      <form action={saveTrackingAction}>
        <input name="returnPath" type="hidden" value={returnPath} />
        {record ? <input name="trackCode" type="hidden" value={record.trackCode} /> : null}
        <input name="vmfCode" type="hidden" value={vmfCode ?? ""} />
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="tracking-number">
              Tracker number
            </label>
            <input
              className="form-input"
              id="tracking-number"
              name="trackNumber"
              defaultValue={record?.trackNumber ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="tracking-install-date">
              Install date
            </label>
            <input
              className="form-input"
              id="tracking-install-date"
              name="installDate"
              type="date"
              defaultValue={
                formatDate(record?.installDate) === "-" ? "" : formatDate(record?.installDate)
              }
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="tracking-status">
              Tracker status
            </label>
            <select
              className="form-select"
              id="tracking-status"
              name="trackStatus"
              defaultValue={record?.status ?? "Installed"}
            >
              <option value="Installed">Installed</option>
              <option value="Removed">Removed</option>
              <option value="Stolen">Stolen</option>
              <option value="None">None</option>
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="tracking-type">
              Tracker type
            </label>
            <select
              className="form-select"
              id="tracking-type"
              name="trackType"
              defaultValue={selectedType}
            >
              <option value="New">New</option>
              <option value="Reused">Re-used</option>
              <option value="None">None</option>
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="tracking-previous-gg">
              Previous GG
            </label>
            <input
              className="form-input"
              id="tracking-previous-gg"
              name="previousGg"
              defaultValue={record?.previousGg ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="tracking-follow-gg">
              Follow GG
            </label>
            <input
              className="form-input"
              id="tracking-follow-gg"
              name="followGg"
              defaultValue={record?.followGg ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="tracking-remove-date">
              Remove date
            </label>
            <input
              className="form-input"
              id="tracking-remove-date"
              name="removeDate"
              type="date"
              defaultValue={record?.removeDate ? formatDate(record.removeDate) : ""}
            />
          </div>
          <div className="form-field form-group-full">
            <label className="form-label" htmlFor="tracking-note">
              Notes
            </label>
            <textarea
              className="form-input"
              id="tracking-note"
              name="trackNote"
              defaultValue={record?.note ?? ""}
              rows={3}
            />
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            {record ? "Save changes" : "Submit"}
          </button>
          <Link className="button button-secondary" href={returnPath}>
            Cancel
          </Link>
        </div>
      </form>
    </section>
  );
}

export function TrackingReportTable({
  records,
  title = "Tracking report results",
}: Readonly<{ records: readonly TrackingRecord[]; title?: string }>) {
  return <TrackingTable records={records} title={title} editable={false} />;
}

export function VehicleLookup({
  options,
  mode,
  search,
  selectedVmfCode,
}: Readonly<{
  options: readonly VehicleOption[];
  mode: string;
  search: string;
  selectedVmfCode: number | null;
}>) {
  const normalized = search.trim().toLocaleLowerCase();
  const useRegistration = mode.toLocaleUpperCase() === "GP";
  const matches = normalized
    ? options
        .filter((vehicle) =>
          (useRegistration ? vehicle.registrationNumber : vehicle.fleetNumber)
            ?.toLocaleLowerCase()
            .includes(normalized),
        )
        .sort((left, right) => left.vmfCode - right.vmfCode)
    : [];
  return (
    <div className="vehicle-search-row">
      <label className="sr-only" htmlFor="tracking-lookup-mode">
        Lookup type
      </label>
      <select className="form-select" id="tracking-lookup-mode" name="mode" defaultValue={mode}>
        <option value="GG">GG number</option>
        <option value="GP">Registration number</option>
      </select>
      <label className="sr-only" htmlFor="tracking-lookup-search">
        Vehicle number
      </label>
      <input
        className="vehicle-search"
        id="tracking-lookup-search"
        name="search"
        defaultValue={search}
        placeholder={useRegistration ? "Enter registration number" : "Enter GG number"}
      />
      <label className="sr-only" htmlFor="tracking-lookup-match">
        Vehicle match
      </label>
      <select
        className="form-select"
        id="tracking-lookup-match"
        name="vmfCode"
        defaultValue={selectedVmfCode ?? ""}
      >
        <option value="">Select vehicle</option>
        {matches.map((vehicle) => (
          <option key={vehicle.vmfCode} value={vehicle.vmfCode}>
            {valueOrDash(vehicle.fleetNumber)} / {valueOrDash(vehicle.registrationNumber)} (
            {vehicle.vmfCode})
          </option>
        ))}
      </select>
      <button className="button button-primary" type="submit">
        Find / select
      </button>
    </div>
  );
}

export function DateRangeFields({
  startDate,
  endDate,
}: Readonly<{ startDate: string; endDate: string }>) {
  return (
    <div className="form-grid">
      <div className="form-field">
        <label className="form-label" htmlFor="tracking-report-start">
          From date
        </label>
        <input
          className="form-input"
          id="tracking-report-start"
          name="startDate"
          type="date"
          defaultValue={startDate}
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="tracking-report-end">
          To date
        </label>
        <input
          className="form-input"
          id="tracking-report-end"
          name="endDate"
          type="date"
          defaultValue={endDate}
        />
      </div>
    </div>
  );
}

export function TrackingReportDateForm({
  startDate,
  endDate,
}: Readonly<{ startDate: string; endDate: string }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <DateRangeFields startDate={startDate} endDate={endDate} />
      <input name="run" type="hidden" value="1" />
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href="/tracking/reports">
          Report menu
        </Link>
      </div>
    </form>
  );
}
