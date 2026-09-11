import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";

import { saveLicenseVehicleAction } from "@/app/(fleet-operations)/licenses/actions";
import SearchTypeFieldset from "@/components/ui/search-type-fieldset";
import { MenuSection } from "@/components/ui/menu-section";
import type { LicenseHistoryEntry, LicenseVehicleDetails } from "@/lib/api/vehicles/api-licenses";
import type { SiteRecord } from "@/lib/api/reference-data/api-sites";
import { formatDate, valueOrDash } from "@/app/(fleet-operations)/licenses/_utils";

function queryText(value: string | string[] | undefined) {
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

export function LicenseShell({
  title,
  description,
  children,
}: Readonly<{ title: string; description: string; children: React.ReactNode }>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="license-page-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Licences</p>
            <h1 id="license-page-title">{title}</h1>
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

export function LicenseNotice({
  query,
}: Readonly<{ query: Record<string, string | string[] | undefined> }>) {
  const error = queryText(query.error);
  const saved = queryText(query.saved);
  return error || saved ? (
    <div
      className={`notice ${error ? "notice-error" : "notice-success"}`}
      role={error ? "alert" : "status"}
    >
      {error || saved}
    </div>
  ) : null;
}

export function LicenseMenu() {
  return (
    <div className="vehicle-menu-tiles">
      <MenuSection title="Licence Maintenance Menu">
        <Link className="vehicle-menu-link" href="/licenses/help">
          Licence Maintenance Information / Help
        </Link>
        <Link className="vehicle-menu-link" href="/licenses/one-vehicle">
          1) Licence Maintenance for ONE Vehicle
        </Link>
        <Link className="vehicle-menu-link" href="/licenses/multi-collection">
          2) Collection for TWO or MORE Licences
        </Link>
        <Link className="vehicle-menu-link" href="/licenses/garage">
          3) Licence Available at Garage
        </Link>
      </MenuSection>
      <MenuSection title="Other Licence Options">
        <Link className="vehicle-menu-link" href="/licenses/model-fees">
          4) Table: Make &amp; Model with Licence Fees
        </Link>
        <Link className="vehicle-menu-link" href="/licenses/scan-certificate">
          5) Scan Licence Certificate
        </Link>
        <Link className="vehicle-menu-link" href="/licenses/reports/workgroup-latest">
          6) Workgroup Report
        </Link>
      </MenuSection>
      <MenuSection title="Licence Reports">
        <Link className="vehicle-menu-link" href="/licenses/reports">
          Licence Reports Menu
        </Link>
      </MenuSection>
    </div>
  );
}

export function LicenseVehicleSearch({
  mode,
  number,
  hiddenFields = {},
  clearHref = "/licenses/one-vehicle",
}: Readonly<{
  mode: "GG" | "GP";
  number: string;
  hiddenFields?: Record<string, string | undefined>;
  clearHref?: string;
}>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Vehicle lookup</p>
          <h2>Vehicle Number</h2>
        </div>
      </div>
      <div className="form-grid">
        <SearchTypeFieldset selectedType={mode} legend="Number type" name="mode" />
        <div className="form-field">
          <label className="form-label" htmlFor="license-vehicle-number">
            {mode === "GG" ? "GG Number" : "GP Number"}
          </label>
          <input
            className="form-input"
            id="license-vehicle-number"
            name="number"
            defaultValue={number}
            maxLength={50}
            required
          />
        </div>
      </div>
      <input name="lookup" type="hidden" value="1" />
      {Object.entries(hiddenFields).map(([key, value]) =>
        value ? <input key={key} name={key} type="hidden" value={value} /> : null,
      )}
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href={clearHref}>
          Clear
        </Link>
        <Link className="button button-secondary" href="/licenses">
          Menu
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
}: Readonly<{
  id: string;
  name: string;
  label: string;
  value: string | null | undefined;
  required?: boolean;
}>) {
  return (
    <div className="form-field">
      <label className="form-label" htmlFor={id}>
        {label}
      </label>
      <input
        className="form-input"
        id={id}
        name={name}
        type="date"
        defaultValue={formatDate(value) === "-" ? "" : formatDate(value)}
        required={required}
      />
    </div>
  );
}

export function LicenseDetailsForm({
  details,
  sites,
  returnPath,
  workflow = "one-vehicle",
}: Readonly<{
  details: LicenseVehicleDetails;
  sites: readonly SiteRecord[];
  returnPath: string;
  workflow?: "one-vehicle" | "multi-collection" | "garage";
}>) {
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="license-details-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Vehicle {details.vmfCode}</p>
          <h2 id="license-details-title">Licence Details</h2>
        </div>
      </div>
      <form action={saveLicenseVehicleAction}>
        <input name="vmfCode" type="hidden" value={details.vmfCode} />
        <input name="numberType" type="hidden" value={details.numberType} />
        <input name="number" type="hidden" value={details.number} />
        <input name="returnPath" type="hidden" value={returnPath} />
        <input name="workflow" type="hidden" value={workflow} />
        <div className="form-grid">
          <DateField
            id="license-exp-date"
            name="expDate"
            label="Exp Date"
            value={details.expDate}
            required
          />
          <div className="form-field">
            <label className="form-label" htmlFor="license-register-number">
              Register Num
            </label>
            <input
              className="form-input"
              id="license-register-number"
              name="registerNumber"
              defaultValue={details.registerNumber ?? ""}
              maxLength={50}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="license-reg-doc">
              Reg Doc
            </label>
            <input
              className="form-input"
              id="license-reg-doc"
              name="regDoc"
              defaultValue={details.regDoc ?? ""}
              maxLength={100}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="license-tare">
              Tare
            </label>
            <input
              className="form-input"
              id="license-tare"
              name="tare"
              type="number"
              min="0"
              defaultValue={details.tare ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="license-receiver">
              Receiver
            </label>
            <input
              className="form-input"
              id="license-receiver"
              name="receiver"
              defaultValue={details.receiver ?? ""}
              maxLength={100}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="license-receiver-id">
              Rec ID
            </label>
            <input
              className="form-input"
              id="license-receiver-id"
              name="receiverId"
              defaultValue={details.receiverId ?? ""}
              maxLength={50}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="license-receiver-tel">
              Rec Tel
            </label>
            <input
              className="form-input"
              id="license-receiver-tel"
              name="receiverTel"
              defaultValue={details.receiverTel ?? ""}
              maxLength={30}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="license-receiver-site">
              Rec Site
            </label>
            <select
              className="form-select"
              id="license-receiver-site"
              name="receiverSiteCode"
              defaultValue={details.receiverSiteCode ?? ""}
            >
              <option value="">Select site</option>
              {sites.map((site) => (
                <option key={site.siteCode} value={site.siteCode}>
                  {valueOrDash(site.departmentNumber)} - {valueOrDash(site.description)} (
                  {site.siteCode})
                </option>
              ))}
            </select>
          </div>
          <DateField
            id="license-date-collected"
            name="dateCollected"
            label="Date Collected"
            value={details.dateCollected}
          />
          <div className="form-field">
            <label className="form-label" htmlFor="license-cof-required">
              Cof Required
            </label>
            <input
              className="form-input"
              id="license-cof-required"
              name="cofRequired"
              defaultValue={details.cofRequired ?? ""}
              maxLength={1}
            />
          </div>
          <DateField
            id="license-cof-exp-date"
            name="cofExpDate"
            label="Cof Exp Date"
            value={details.cofExpDate}
          />
          <div className="form-field form-group-full">
            <label className="form-label" htmlFor="license-comments">
              Comments
            </label>
            <textarea
              className="form-input"
              id="license-comments"
              name="comments"
              defaultValue={details.comments ?? ""}
              maxLength={2000}
              rows={3}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="license-password">
              Password
            </label>
            <input
              className="form-input"
              id="license-password"
              name="password"
              type="password"
              maxLength={8}
              required
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="license-update-notes">
              History note
            </label>
            <input
              className="form-input"
              id="license-update-notes"
              name="updateNotes"
              maxLength={2000}
            />
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Save licence details
          </button>
          <Link className="button button-secondary" href={returnPath}>
            Menu
          </Link>
        </div>
      </form>
    </section>
  );
}

export function LicenseHistoryTable({
  entries,
}: Readonly<{ entries: readonly LicenseHistoryEntry[] }>) {
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="license-history-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Renewal history</p>
          <h2 id="license-history-title">Licence History</h2>
        </div>
        <span className="form-hint">
          {entries.length} record{entries.length === 1 ? "" : "s"}
        </span>
      </div>
      {entries.length === 0 ? (
        <p className="muted-copy">No historical licence captures are available for this vehicle.</p>
      ) : (
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Historical licence captures</caption>
            <DataTableHeader
              columns={[
                { key: "column-1", label: <>Captured At</> },
                { key: "column-2", label: <>Due Date</> },
                { key: "column-3", label: <>Register Number</> },
                { key: "column-4", label: <>Reg Doc</> },
                { key: "column-5", label: <>COF Date</> },
                { key: "column-6", label: <>Receiver</> },
                { key: "column-7", label: <>Receiver ID</> },
                { key: "column-8", label: <>Receiver Tel</> },
                { key: "column-9", label: <>Captured By</> },
                { key: "column-10", label: <>Notes</> },
              ]}
            />
            <tbody>
              {entries.map((row) => (
                <tr key={row.historyId}>
                  <td>{formatDate(row.capturedAt)}</td>
                  <td>{formatDate(row.dueDate)}</td>
                  <td>{valueOrDash(row.registerNumber)}</td>
                  <td>{valueOrDash(row.registrationDocument)}</td>
                  <td>{formatDate(row.cofDate)}</td>
                  <td>{valueOrDash(row.receiver)}</td>
                  <td>{valueOrDash(row.receiverId)}</td>
                  <td>{valueOrDash(row.receiverTel)}</td>
                  <td>{valueOrDash(row.capturedByEmail)}</td>
                  <td>{valueOrDash(row.updateNotes ?? row.comments)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
