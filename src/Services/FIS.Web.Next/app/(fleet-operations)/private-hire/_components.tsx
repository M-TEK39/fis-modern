import Link from "next/link";

import ApiUnavailableCard from "@/components/app-shell/api-unavailable-card";
import DataTableHeader from "@/components/ui/data-table-header";

import type { ModelRecord } from "@/lib/api/reference-data/api-models";
import type {
  PrivateHireContractorRecord,
  PrivateHireVehicleRecord,
} from "@/lib/api/fleet-operations/api-private-hire";
import type { SiteRecord } from "@/lib/api/reference-data/api-sites";
import { dateValue, queryValue, valueOrDash } from "@/app/(fleet-operations)/private-hire/_utils";
import {
  PrivateHireVehicleIdentityFields,
  PrivateHireVehicleOperationalFields,
} from "@/app/(fleet-operations)/private-hire/_vehicle-form-fields";

function privateHirePageHref(
  path: string,
  query: Record<string, string | string[] | undefined>,
  page: number,
) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (key === "page" || value === undefined) continue;
    for (const item of Array.isArray(value) ? value : [value]) {
      if (item) params.append(key, item);
    }
  }
  if (page > 1) params.set("page", String(page));
  const queryString = params.toString();
  return queryString ? `${path}?${queryString}` : path;
}

export function PrivateHirePagination({
  path,
  query,
  page,
  totalPages,
}: Readonly<{
  path: string;
  query: Record<string, string | string[] | undefined>;
  page: number;
  totalPages: number;
}>) {
  if (totalPages <= 1) return null;

  return (
    <nav className="vehicle-pagination" aria-label="Private Hire result pages">
      {page <= 1 ? (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Previous
        </span>
      ) : (
        <Link
          className="vehicle-pagination-button"
          href={privateHirePageHref(path, query, page - 1)}
        >
          Previous
        </Link>
      )}
      <span className="vehicle-pagination-meta" aria-live="polite">
        Page {page} of {totalPages}
      </span>
      {page >= totalPages ? (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Next
        </span>
      ) : (
        <Link
          className="vehicle-pagination-button"
          href={privateHirePageHref(path, query, page + 1)}
        >
          Next
        </Link>
      )}
    </nav>
  );
}

export function PrivateHireNotice({
  query,
}: Readonly<{ query: Record<string, string | string[] | undefined> }>) {
  const error = queryValue(query.error);
  const success = queryValue(query.saved) || queryValue(query.deleted);
  if (error)
    return (
      <div className="notice notice-error" role="alert">
        {error}
      </div>
    );
  if (success)
    return (
      <div className="notice notice-success" role="status">
        {success === "1" ? "Request completed successfully." : success}
      </div>
    );
  return null;
}

export function PrivateHireReportTableHeader({
  columns,
}: Readonly<{ columns: readonly string[] }>) {
  return (
    <thead>
      <tr>
        {columns.map((column) => (
          <th key={column} scope="col">
            {column}
          </th>
        ))}
      </tr>
    </thead>
  );
}

export function ApiUnavailable({
  path,
  subject = "Private Hire",
}: Readonly<{ path: string; subject?: string }>) {
  return (
    <ApiUnavailableCard
      message={`${subject} could not be loaded.`}
      retryHref={path}
      secondaryHref="/login"
      secondaryLabel="Sign in"
    />
  );
}

export function PrivateHireVehicleTable({
  vehicles,
  selectPath,
  mode,
}: Readonly<{
  vehicles: PrivateHireVehicleRecord[];
  selectPath?: string;
  mode?: string;
}>) {
  if (vehicles.length === 0) return <p className="muted-copy">No Private Hire vehicles found.</p>;
  const columns = [
    { key: "registration", label: "Registration" },
    { key: "model", label: "Model" },
    { key: "site", label: "Site" },
    { key: "contractor", label: "Contractor" },
    { key: "take-on", label: "Take-on" },
    { key: "return", label: "Return" },
    ...(selectPath ? [{ key: "action", label: "Action" }] : []),
  ];
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Private Hire vehicles</caption>
        <DataTableHeader columns={columns} />
        <tbody>
          {vehicles.map((vehicle) => (
            <tr key={vehicle.phvCode}>
              <td>
                {valueOrDash(vehicle.registrationNumber)}{" "}
                <span className="muted-copy">({vehicle.phvCode})</span>
              </td>
              <td>
                {valueOrDash(vehicle.modelDescription)}{" "}
                <span className="muted-copy">({vehicle.modelCode})</span>
              </td>
              <td>{valueOrDash(vehicle.siteCode)}</td>
              <td>{valueOrDash(vehicle.contractorId)}</td>
              <td>{dateValue(vehicle.takeOnDate)}</td>
              <td>{dateValue(vehicle.returnDate)}</td>
              {selectPath ? (
                <td>
                  <Link
                    className="button button-secondary button-small"
                    href={`${selectPath}?mode=${encodeURIComponent(mode ?? "edit")}&phvCode=${vehicle.phvCode}`}
                  >
                    Select
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

export function PrivateHireContractorTable({
  contractors,
  selectPath,
  mode,
}: Readonly<{
  contractors: PrivateHireContractorRecord[];
  selectPath?: string;
  mode?: string;
}>) {
  if (contractors.length === 0)
    return <p className="muted-copy">No Private Hire contractors found.</p>;
  const columns = [
    { key: "company", label: "Company" },
    { key: "contact", label: "Contact" },
    { key: "phone", label: "Phone" },
    { key: "email", label: "Email" },
    { key: "status", label: "Status" },
    ...(selectPath ? [{ key: "action", label: "Action" }] : []),
  ];
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Private Hire contractors</caption>
        <DataTableHeader columns={columns} />
        <tbody>
          {contractors.map((contractor) => (
            <tr key={contractor.contractorId}>
              <td>
                {valueOrDash(contractor.companyName)}{" "}
                <span className="muted-copy">({contractor.contractorId})</span>
              </td>
              <td>{valueOrDash(contractor.contactPerson)}</td>
              <td>{valueOrDash(contractor.phone)}</td>
              <td>{valueOrDash(contractor.email)}</td>
              <td>{valueOrDash(contractor.status)}</td>
              {selectPath ? (
                <td>
                  <Link
                    className="button button-secondary button-small"
                    href={`${selectPath}?mode=${encodeURIComponent(mode ?? "contractor-edit")}&contractorId=${contractor.contractorId}`}
                  >
                    Select
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

function textValue(value: string | null | undefined) {
  return value ?? "";
}

function numberValue(value: number | null | undefined) {
  return value === null || value === undefined ? "" : String(value);
}

export function PrivateHireVehicleForm({
  vehicle,
  action,
  returnPath,
  models,
  sites,
  contractors,
}: Readonly<{
  vehicle?: PrivateHireVehicleRecord | null;
  action: (formData: FormData) => void | Promise<void>;
  returnPath: string;
  models: readonly ModelRecord[];
  sites: readonly SiteRecord[];
  contractors: readonly PrivateHireContractorRecord[];
}>) {
  const sortedModels = models
    .slice()
    .sort((a, b) => a.modelDescription.localeCompare(b.modelDescription));
  const sortedSites = sites
    .slice()
    .sort((a, b) => (a.departmentNumber ?? "").localeCompare(b.departmentNumber ?? ""));
  const sortedContractors = contractors
    .slice()
    .sort((a, b) => a.companyName.localeCompare(b.companyName));

  return (
    <form className="vehicle-status-maintenance-panel" action={action}>
      <input type="hidden" name="returnPath" value={returnPath} />
      {vehicle ? <input type="hidden" name="phvCode" value={vehicle.phvCode} /> : null}
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{vehicle ? `Vehicle ${vehicle.phvCode}` : "New vehicle"}</p>
          <h2>{vehicle ? "Edit Private Hire Vehicle" : "Add Private Hire Vehicle"}</h2>
          <p>These fields map directly to the legacy Private_hire business columns.</p>
        </div>
      </div>
      <div className="form-grid">
        <PrivateHireVehicleIdentityFields
          vehicle={vehicle}
          models={sortedModels}
          sites={sortedSites}
          contractors={sortedContractors}
        />
        <PrivateHireVehicleOperationalFields vehicle={vehicle} />
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          {vehicle ? "Save changes" : "Add vehicle"}
        </button>
        <Link className="button button-secondary" href="/private-hire/maintenance-menu">
          Menu
        </Link>
      </div>
    </form>
  );
}

export function PrivateHireContractorForm({
  contractor,
  action,
  returnPath,
}: Readonly<{
  contractor?: PrivateHireContractorRecord | null;
  action: (formData: FormData) => void | Promise<void>;
  returnPath: string;
}>) {
  return (
    <form className="vehicle-status-maintenance-panel" action={action}>
      <input type="hidden" name="returnPath" value={returnPath} />
      {contractor ? (
        <input type="hidden" name="contractorId" value={contractor.contractorId} />
      ) : null}
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">
            {contractor ? `Contractor ${contractor.contractorId}` : "New contractor"}
          </p>
          <h2>{contractor ? "Edit Private Hire Contractor" : "Add Private Hire Contractor"}</h2>
          <p>Legacy contractor details remain available on both database shapes.</p>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="phc-company">
            Company name
          </label>
          <input
            className="form-input"
            id="phc-company"
            name="companyName"
            defaultValue={textValue(contractor?.companyName)}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phc-contact">
            Contact person
          </label>
          <input
            className="form-input"
            id="phc-contact"
            name="contactPerson"
            defaultValue={textValue(contractor?.contactPerson)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phc-phone">
            Telephone
          </label>
          <input
            className="form-input"
            id="phc-phone"
            name="phone"
            type="tel"
            defaultValue={textValue(contractor?.phone)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phc-email">
            Email address
          </label>
          <input
            className="form-input"
            id="phc-email"
            name="email"
            type="email"
            defaultValue={textValue(contractor?.email)}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="phc-physical">
            Physical address
          </label>
          <input
            className="form-input"
            id="phc-physical"
            name="physicalAddress"
            defaultValue={textValue(contractor?.physicalAddress)}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="phc-postal">
            Postal address
          </label>
          <input
            className="form-input"
            id="phc-postal"
            name="postalAddress"
            defaultValue={textValue(contractor?.postalAddress)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phc-fax">
            Fax / business registration
          </label>
          <input
            className="form-input"
            id="phc-fax"
            name="faxNumber"
            defaultValue={textValue(contractor?.faxNumber)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phc-status">
            Status
          </label>
          <select
            className="form-select"
            id="phc-status"
            name="status"
            defaultValue={contractor?.status === "Inactive" ? "Inactive" : "Active"}
          >
            <option value="Active">Active</option>
            <option value="Inactive">Inactive</option>
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phc-type">
            Type
          </label>
          <input
            className="form-input"
            id="phc-type"
            name="type"
            defaultValue={textValue(contractor?.type)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phc-project">
            Project name
          </label>
          <input
            className="form-input"
            id="phc-project"
            name="projectName"
            defaultValue={textValue(contractor?.projectName)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phc-project-start">
            Project begin date
          </label>
          <input
            className="form-input"
            id="phc-project-start"
            name="projectBeginDate"
            type="date"
            defaultValue={
              dateValue(contractor?.projectBeginDate) === "-"
                ? ""
                : dateValue(contractor?.projectBeginDate)
            }
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phc-project-end">
            Project end date
          </label>
          <input
            className="form-input"
            id="phc-project-end"
            name="projectEndDate"
            type="date"
            defaultValue={
              dateValue(contractor?.projectEndDate) === "-"
                ? ""
                : dateValue(contractor?.projectEndDate)
            }
          />
        </div>
        <label className="vehicle-checkbox-label">
          <input
            name="quotations"
            type="checkbox"
            defaultChecked={contractor?.quotations ?? false}
          />{" "}
          Quotations accepted
        </label>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          {contractor ? "Save changes" : "Add contractor"}
        </button>
        <Link className="button button-secondary" href="/private-hire/maintenance-menu">
          Menu
        </Link>
      </div>
    </form>
  );
}
