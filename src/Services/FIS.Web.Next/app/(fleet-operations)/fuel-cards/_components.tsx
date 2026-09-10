import Link from "next/link";

import type { FuelCardRecord } from "@/lib/api/fleet-operations/api-fuel-cards";
import type { WorkshopVehicle } from "@/lib/api/fleet-operations/api-workshop";

export function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

export function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

export function dateValue(value: string | null | undefined) {
  return value?.slice(0, 10) || "-";
}

export function VehicleSearchForm({
  path,
  type,
  search,
}: Readonly<{ path: string; type: string; search: string }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <fieldset className="vehicle-search-options">
        <legend>Search by</legend>
        <label className="vehicle-checkbox-label">
          <input type="radio" name="type" value="GG" defaultChecked={type !== "GP"} /> GG
        </label>
        <label className="vehicle-checkbox-label">
          <input type="radio" name="type" value="GP" defaultChecked={type === "GP"} /> GP Number
        </label>
      </fieldset>
      <div className="vehicle-search-row">
        <label className="sr-only" htmlFor={`${path.replaceAll("/", "-")}-search`}>
          {type === "GP" ? "GP number" : "GG number"}
        </label>
        <input
          className="vehicle-search"
          id={`${path.replaceAll("/", "-")}-search`}
          name="search"
          defaultValue={search}
          placeholder={type === "GP" ? "Enter GP number" : "Enter GG number"}
        />
        <button className="button button-primary" type="submit">
          Find vehicles
        </button>
        <Link className="button button-secondary" href="/fuel-cards">
          Menu
        </Link>
      </div>
    </form>
  );
}

export function VehicleResults({
  vehicles,
  path,
  type,
  search,
  selectedCode,
}: Readonly<{
  vehicles: WorkshopVehicle[];
  path: string;
  type: string;
  search: string;
  selectedCode?: number | null;
}>) {
  if (!search) return <p className="muted-copy">Enter a GG or GP number to find a vehicle.</p>;
  if (vehicles.length === 0)
    return <p className="muted-copy">No vehicles matched the current search.</p>;
  return (
    <div className="vehicle-table-wrapper" aria-live="polite">
      <table className="vehicle-table">
        <caption className="sr-only">Vehicles matching the fuelcard search</caption>
        <thead>
          <tr>
            <th scope="col">Vehicle</th>
            <th scope="col">GG Number</th>
            <th scope="col">GP Number</th>
            <th scope="col">Action</th>
          </tr>
        </thead>
        <tbody>
          {vehicles.slice(0, 100).map((vehicle) => {
            const params = new URLSearchParams({ type, search, vmfCode: String(vehicle.vmfCode) });
            return (
              <tr
                key={vehicle.vmfCode}
                className={selectedCode === vehicle.vmfCode ? "row-selected" : undefined}
              >
                <td>
                  {valueOrDash(vehicle.fleetNumber)} / {valueOrDash(vehicle.registrationNumber)} (
                  {vehicle.vmfCode})
                </td>
                <td>{valueOrDash(vehicle.fleetNumber)}</td>
                <td>{valueOrDash(vehicle.registrationNumber)}</td>
                <td>
                  <Link
                    className="button button-secondary button-small"
                    href={`${path}?${params.toString()}`}
                  >
                    Select
                  </Link>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

export function FuelCardTable({
  cards,
  privateHire = false,
  deleteAction,
  returnPath,
}: Readonly<{
  cards: FuelCardRecord[];
  privateHire?: boolean;
  deleteAction?: (formData: FormData) => void | Promise<void>;
  returnPath?: string;
}>) {
  if (cards.length === 0)
    return <p className="muted-copy">No fuelcard records found for this vehicle.</p>;
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">
          {privateHire ? "Private hire fuelcards" : "Fuelcards"}
        </caption>
        <thead>
          <tr>
            <th scope="col">Card Number</th>
            <th scope="col">PAN Number</th>
            <th scope="col">Counter</th>
            <th scope="col">Status</th>
            <th scope="col">Expiry</th>
            {deleteAction ? <th scope="col">Action</th> : null}
          </tr>
        </thead>
        <tbody>
          {cards.map((card) => (
            <tr key={card.fuelCardCode}>
              <td>{valueOrDash(card.cardNumber)}</td>
              <td>{valueOrDash(card.panNumber)}</td>
              <td>{valueOrDash(card.counter)}</td>
              <td>{valueOrDash(card.reason)}</td>
              <td>{dateValue(card.expireDate)}</td>
              {deleteAction ? (
                <td>
                  <form action={deleteAction}>
                    <input type="hidden" name="returnPath" value={returnPath ?? ""} />
                    <input type="hidden" name="fuelCardCode" value={card.fuelCardCode} />
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

export function ApiUnavailable({
  path,
  subject = "Fuelcards",
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

export function FuelCardNotice({
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
