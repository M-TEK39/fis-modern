import Link from "next/link";

import type { PrivateHireContractorRecord, PrivateHireVehicleRecord } from "@/lib/api-private-hire";

export function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

export function dateValue(value: string | null | undefined) {
  return value?.slice(0, 10) || "-";
}

export function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
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

export function ApiUnavailable({
  path,
  subject = "Private Hire",
}: Readonly<{ path: string; subject?: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>{subject} could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href={path}>
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
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
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Private Hire vehicles</caption>
        <thead>
          <tr>
            <th scope="col">Registration</th>
            <th scope="col">Model</th>
            <th scope="col">Site</th>
            <th scope="col">Contractor</th>
            <th scope="col">Take-on</th>
            <th scope="col">Return</th>
            {selectPath ? <th scope="col">Action</th> : null}
          </tr>
        </thead>
        <tbody>
          {vehicles.slice(0, 200).map((vehicle) => (
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
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Private Hire contractors</caption>
        <thead>
          <tr>
            <th scope="col">Company</th>
            <th scope="col">Contact</th>
            <th scope="col">Phone</th>
            <th scope="col">Email</th>
            <th scope="col">Status</th>
            {selectPath ? <th scope="col">Action</th> : null}
          </tr>
        </thead>
        <tbody>
          {contractors.slice(0, 200).map((contractor) => (
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
}: Readonly<{
  vehicle?: PrivateHireVehicleRecord | null;
  action: (formData: FormData) => void | Promise<void>;
  returnPath: string;
}>) {
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
        <div className="form-field">
          <label className="form-label" htmlFor="phv-registration">
            Registration number
          </label>
          <input
            className="form-input"
            id="phv-registration"
            name="registrationNumber"
            defaultValue={textValue(vehicle?.registrationNumber)}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-model-code">
            Model code
          </label>
          <input
            className="form-input"
            id="phv-model-code"
            name="modelCode"
            type="number"
            min="1"
            defaultValue={numberValue(vehicle?.modelCode)}
            required
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="phv-model-description">
            Model description
          </label>
          <input
            className="form-input"
            id="phv-model-description"
            name="modelDescription"
            defaultValue={textValue(vehicle?.modelDescription)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-site">
            Site code
          </label>
          <input
            className="form-input"
            id="phv-site"
            name="siteCode"
            type="number"
            min="1"
            defaultValue={numberValue(vehicle?.siteCode)}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-contracted-to">
            Contracted to
          </label>
          <input
            className="form-input"
            id="phv-contracted-to"
            name="contractedTo"
            type="number"
            min="0"
            defaultValue={numberValue(vehicle?.contractedTo)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-contractor">
            Contractor ID
          </label>
          <input
            className="form-input"
            id="phv-contractor"
            name="contractorId"
            type="number"
            min="1"
            defaultValue={numberValue(vehicle?.contractorId)}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-engine">
            Engine number
          </label>
          <input
            className="form-input"
            id="phv-engine"
            name="engineNumber"
            defaultValue={textValue(vehicle?.engineNumber)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-chassis">
            Chassis number
          </label>
          <input
            className="form-input"
            id="phv-chassis"
            name="chassisNumber"
            defaultValue={textValue(vehicle?.chassisNumber)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-year">
            Year manufactured
          </label>
          <input
            className="form-input"
            id="phv-year"
            name="yearManufactured"
            defaultValue={textValue(vehicle?.yearManufactured)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-bank">
            Bank code
          </label>
          <input
            className="form-input"
            id="phv-bank"
            name="bankCode"
            defaultValue={textValue(vehicle?.bankCode)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-colour">
            Colour
          </label>
          <input
            className="form-input"
            id="phv-colour"
            name="colour"
            defaultValue={textValue(vehicle?.colour)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-tank">
            Tank capacity (litres)
          </label>
          <input
            className="form-input"
            id="phv-tank"
            name="tankCapacity"
            type="number"
            min="0"
            defaultValue={numberValue(vehicle?.tankCapacity)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-fuel-card">
            Fuel card
          </label>
          <input
            className="form-input"
            id="phv-fuel-card"
            name="fuelCard"
            defaultValue={textValue(vehicle?.fuelCard)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-fuel-receiver">
            Fuel card receiver
          </label>
          <input
            className="form-input"
            id="phv-fuel-receiver"
            name="fuelCardReceiver"
            defaultValue={textValue(vehicle?.fuelCardReceiver)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-take-on-date">
            Take-on date
          </label>
          <input
            className="form-input"
            id="phv-take-on-date"
            name="takeOnDate"
            type="date"
            defaultValue={
              dateValue(vehicle?.takeOnDate) === "-" ? "" : dateValue(vehicle?.takeOnDate)
            }
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-take-on-odo">
            Take-on odometer
          </label>
          <input
            className="form-input"
            id="phv-take-on-odo"
            name="takeOnOdo"
            type="number"
            min="0"
            defaultValue={numberValue(vehicle?.takeOnOdo)}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-return-date">
            Return date
          </label>
          <input
            className="form-input"
            id="phv-return-date"
            name="returnDate"
            type="date"
            defaultValue={
              dateValue(vehicle?.returnDate) === "-" ? "" : dateValue(vehicle?.returnDate)
            }
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-return-odo">
            Return odometer
          </label>
          <input
            className="form-input"
            id="phv-return-odo"
            name="returnOdo"
            type="number"
            min="0"
            defaultValue={numberValue(vehicle?.returnOdo)}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-km-tariff">
            Kilometre tariff
          </label>
          <input
            className="form-input"
            id="phv-km-tariff"
            name="kmTariff"
            type="number"
            min="0"
            step="0.01"
            defaultValue={numberValue(vehicle?.kmTariff)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-daily-tariff">
            Daily tariff
          </label>
          <input
            className="form-input"
            id="phv-daily-tariff"
            name="dailyTariff"
            type="number"
            min="0"
            step="0.01"
            defaultValue={numberValue(vehicle?.dailyTariff)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="phv-hourly-tariff">
            Hourly tariff
          </label>
          <input
            className="form-input"
            id="phv-hourly-tariff"
            name="hourlyTariff"
            type="number"
            min="0"
            step="0.01"
            defaultValue={numberValue(vehicle?.hourlyTariff)}
          />
        </div>
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
