import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { extendLeaseTariffAction } from "@/app/(fleet-operations)/full-maintenance-lease/actions";
import {
  AccessRestricted,
  ActionNotice,
  ApiUnavailable,
  FmlFrame,
  formatCurrency,
  formatDate,
  hasFmlPermission,
  vehicleLabel,
} from "@/app/(fleet-operations)/full-maintenance-lease/_components";
import { FmlApiError, getLeaseTariffs, type LeaseTariffRecord } from "@/lib/api/finance/api-fml";
import { getVehicleOptions, type VehicleOption } from "@/lib/api/vehicles/api-vehicles";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInteger(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

function formatInputDate(value: string) {
  return value.slice(0, 10);
}

export default async function FmlExtendPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/full-maintenance-lease/extend" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable message="The FML tariff extension form could not be opened." />
      </main>
    );
  if (!hasFmlPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const search = first(query.search) ?? "";
  const mode = first(query.mode) ?? "GG";
  const vmfCode = positiveInteger(first(query.vmfCode));
  const result = first(query.result);
  const message = first(query.message);
  let vehicles: VehicleOption[];
  try {
    vehicles = await getVehicleOptions();
  } catch (error) {
    return (
      <FmlFrame
        title="Extend Lease Tariff"
        description="Extend an existing lease tariff period for a vehicle."
      >
        <ApiUnavailable message={error instanceof FmlApiError ? error.message : undefined} />
      </FmlFrame>
    );
  }

  const vehicleMatches = vehicles
    .filter((vehicle) => {
      const value = mode.toUpperCase() === "GP" ? vehicle.registrationNumber : vehicle.fleetNumber;
      return !search.trim() || value?.toLowerCase().includes(search.trim().toLowerCase());
    })
    .slice(0, 100);
  let tariffs: LeaseTariffRecord[] = [];
  if (vmfCode) {
    try {
      tariffs = await getLeaseTariffs(vmfCode);
    } catch (error) {
      return (
        <FmlFrame
          title="Extend Lease Tariff"
          description="Extend an existing lease tariff period for a vehicle."
        >
          <ApiUnavailable message={error instanceof FmlApiError ? error.message : undefined} />
        </FmlFrame>
      );
    }
  }

  const selectedVehicle = vehicles.find((vehicle) => vehicle.vmfCode === vmfCode);
  return (
    <FmlFrame
      title="Extend Lease Tariff"
      description="Extend an existing lease tariff period for a vehicle."
    >
      <ActionNotice result={result} message={message} />
      <div className="notice notice-info" role="note">
        The legacy workflow edits the selected `LeaseTariff` period. Choose a vehicle, then submit
        the new end date for the tariff row.
      </div>
      <section className="form-card">
        <div className="form-card-header">
          <h2>Vehicle lookup</h2>
          <p>Search by GG fleet number or GP registration number.</p>
        </div>
        <div className="form-card-body">
          <form method="get" className="form-grid">
            <div className="form-field">
              <label className="form-label" htmlFor="fml-extend-search">
                Search number
              </label>
              <input
                className="form-input"
                id="fml-extend-search"
                name="search"
                defaultValue={search}
                placeholder="GG or GP number"
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="fml-extend-mode">
                Number type
              </label>
              <select className="form-select" id="fml-extend-mode" name="mode" defaultValue={mode}>
                <option value="GG">GG</option>
                <option value="GP">GP</option>
              </select>
            </div>
            <div className="form-actions form-group-full">
              <button className="button button-secondary" type="submit">
                Search vehicles
              </button>
            </div>
          </form>
          {search.trim() ? (
            vehicleMatches.length === 0 ? (
              <p className="muted-copy">No vehicles matched that search.</p>
            ) : (
              <div className="vehicle-table-wrapper">
                <table className="vehicle-table">
                  <caption className="sr-only">Vehicle search results</caption>
                  <thead>
                    <tr>
                      <th scope="col">Vehicle</th>
                      <th scope="col">Select</th>
                    </tr>
                  </thead>
                  <tbody>
                    {vehicleMatches.map((vehicle) => (
                      <tr key={vehicle.vmfCode}>
                        <td>{vehicleLabel(vehicle)}</td>
                        <td>
                          <Link
                            className="button button-secondary"
                            href={`/full-maintenance-lease/extend?search=${encodeURIComponent(search)}&mode=${encodeURIComponent(mode)}&vmfCode=${vehicle.vmfCode}`}
                          >
                            Select
                          </Link>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )
          ) : null}
        </div>
      </section>
      {selectedVehicle ? (
        <section className="form-card">
          <div className="form-card-header">
            <h2>Tariff periods</h2>
            <p>{vehicleLabel(selectedVehicle)}</p>
          </div>
          <div className="form-card-body">
            {tariffs.length === 0 ? (
              <p className="muted-copy">No lease tariff periods were found for this vehicle.</p>
            ) : (
              <div className="vehicle-table-wrapper">
                <table className="vehicle-table">
                  <caption className="sr-only">Lease tariff periods</caption>
                  <thead>
                    <tr>
                      <th scope="col">Start</th>
                      <th scope="col">Current end</th>
                      <th scope="col">Fixed tariff</th>
                      <th scope="col">Excess kilo tariff</th>
                      <th scope="col">New end date</th>
                      <th scope="col">Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {tariffs.map((tariff) => (
                      <tr key={tariff.leaseTariffCode}>
                        <td>{formatDate(tariff.startDate)}</td>
                        <td>{formatDate(tariff.endDate)}</td>
                        <td>{formatCurrency(tariff.fixedTariff)}</td>
                        <td>{formatCurrency(tariff.excessKiloTariff)}</td>
                        <td>
                          <form action={extendLeaseTariffAction} className="button-row">
                            <input
                              name="leaseTariffCode"
                              type="hidden"
                              value={tariff.leaseTariffCode}
                              readOnly
                            />
                            <input name="vmfCode" type="hidden" value={tariff.vmfCode} readOnly />
                            <input
                              name="startDate"
                              type="hidden"
                              value={formatInputDate(tariff.startDate)}
                              readOnly
                            />
                            <input
                              name="fixedTariff"
                              type="hidden"
                              value={tariff.fixedTariff}
                              readOnly
                            />
                            <input
                              name="excessKiloTariff"
                              type="hidden"
                              value={tariff.excessKiloTariff ?? ""}
                              readOnly
                            />
                            <label
                              className="sr-only"
                              htmlFor={`fml-new-end-${tariff.leaseTariffCode}`}
                            >
                              New end date for tariff {tariff.leaseTariffCode}
                            </label>
                            <input
                              className="form-input"
                              id={`fml-new-end-${tariff.leaseTariffCode}`}
                              name="endDate"
                              type="date"
                              defaultValue={formatInputDate(tariff.endDate)}
                              required
                            />
                            <button className="button button-primary" type="submit">
                              Extend
                            </button>
                          </form>
                        </td>
                        <td>
                          {tariff.active ? (
                            <span className="badge badge-success">Active</span>
                          ) : (
                            <span className="badge">Inactive</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </section>
      ) : (
        <section className="vehicle-status-card" role="status">
          <h2>Select a vehicle to continue</h2>
          <p className="muted-copy">
            Search above, then select a vehicle with lease tariff periods.
          </p>
        </section>
      )}
      <div className="button-row">
        <Link className="button button-secondary" href="/full-maintenance-lease">
          FML Menu
        </Link>
      </div>
    </FmlFrame>
  );
}
