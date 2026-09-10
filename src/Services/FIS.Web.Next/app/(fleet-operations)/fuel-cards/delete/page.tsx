import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  ApiUnavailable,
  FuelCardNotice,
  FuelCardTable,
  queryValue,
  VehicleResults,
  VehicleSearchForm,
} from "@/app/(fleet-operations)/fuel-cards/_components";
import { deleteFuelCardAction } from "@/app/(fleet-operations)/fuel-cards/actions";
import { FuelCardApiError, getFuelCardsByVehicle } from "@/lib/api/fleet-operations/api-fuel-cards";
import { getSession } from "@/lib/auth/session";
import { searchWorkshopVehicles } from "@/lib/api/fleet-operations/api-workshop";

export default async function FuelCardDeletePage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/fuel-cards/delete" />
      </main>
    );
  if (
    !session.roles.some(
      (role) => role.localeCompare("Fuelcards", undefined, { sensitivity: "accent" }) === 0,
    )
  )
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to delete Fuelcards.</h2>
        </section>
      </main>
    );
  const query = await searchParams;
  const type = queryValue(query.type) === "GP" ? "GP" : "GG";
  const search = queryValue(query.search).trim();
  const selected = Number(queryValue(query.vmfCode));
  const vmfCode = Number.isInteger(selected) && selected > 0 ? selected : null;
  try {
    const vehicles = search
      ? (await searchWorkshopVehicles(search)).filter((vehicle) =>
          (type === "GP" ? vehicle.registrationNumber : vehicle.fleetNumber)
            ?.toLocaleLowerCase()
            .includes(search.toLocaleLowerCase()),
        )
      : [];
    const selectedVehicle = vmfCode
      ? vehicles.find((vehicle) => vehicle.vmfCode === vmfCode)
      : null;
    const cards = vmfCode ? await getFuelCardsByVehicle(vmfCode) : [];
    const returnPath = `/fuel-cards/delete?type=${encodeURIComponent(type)}&search=${encodeURIComponent(search)}&vmfCode=${vmfCode ?? ""}`;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="fuel-card-delete-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Fuelcard maintenance</p>
              <h1 id="fuel-card-delete-title">Delete a Fuelcard</h1>
              <p>Search a vehicle by GG or GP number, then delete its fuelcard record.</p>
            </div>
            <Link className="button button-secondary" href="/fuel-cards">
              Menu
            </Link>
          </header>
          <FuelCardNotice query={query} />
          <VehicleSearchForm path="/fuel-cards/delete" type={type} search={search} />
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="fuel-card-delete-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Vehicle lookup</p>
                <h2 id="fuel-card-delete-results-title">Matching vehicles</h2>
              </div>
            </div>
            <VehicleResults
              vehicles={vehicles}
              path="/fuel-cards/delete"
              type={type}
              search={search}
              selectedCode={vmfCode}
            />
          </section>
          {selectedVehicle ? (
            <section
              className="vehicle-status-maintenance-panel"
              aria-labelledby="fuel-card-delete-cards-title"
            >
              <div className="vehicle-form-section-header">
                <div>
                  <p className="eyebrow">Selected vehicle</p>
                  <h2 id="fuel-card-delete-cards-title">Fuelcards found</h2>
                  <p>
                    {selectedVehicle.fleetNumber || "-"} /{" "}
                    {selectedVehicle.registrationNumber || "-"} ({selectedVehicle.vmfCode})
                  </p>
                </div>
              </div>
              <FuelCardTable
                cards={cards}
                deleteAction={deleteFuelCardAction}
                returnPath={returnPath}
              />
            </section>
          ) : null}
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof FuelCardApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/fuel-cards/delete" />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable path="/fuel-cards/delete" subject="Fuelcard deletion" />
      </main>
    );
  }
}
