import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";

import ApiUnavailableCard from "@/components/app-shell/api-unavailable-card";
import SearchTypeFieldset from "@/components/ui/search-type-fieldset";
import type { FuelCardRecord } from "@/lib/api/fleet-operations/api-fuel-cards";
import type { WorkshopVehicle } from "@/lib/api/fleet-operations/api-workshop";
import { dateValue, queryValue, valueOrDash } from "./_utils";

export function VehicleSearchForm({
  path,
  type,
  search,
}: Readonly<{ path: string; type: string; search: string }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <SearchTypeFieldset selectedType={type} legend="Search by" name="type" gpLabel="GP Number" />
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
        <DataTableHeader
          columns={[
            { key: "column-1", label: <>Vehicle</> },
            { key: "column-2", label: <>GG Number</> },
            { key: "column-3", label: <>GP Number</> },
            { key: "column-4", label: <>Action</> },
          ]}
        />
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
  const columns = [
    { key: "card-number", label: "Card Number" },
    { key: "pan-number", label: "PAN Number" },
    { key: "counter", label: "Counter" },
    { key: "status", label: "Status" },
    { key: "expiry", label: "Expiry" },
    ...(deleteAction ? [{ key: "action", label: "Action" }] : []),
  ];
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">
          {privateHire ? "Private hire fuelcards" : "Fuelcards"}
        </caption>
        <DataTableHeader columns={columns} />
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
    <ApiUnavailableCard
      message={`${subject} could not be loaded.`}
      retryHref={path}
      secondaryHref="/login"
      secondaryLabel="Sign in"
    />
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
