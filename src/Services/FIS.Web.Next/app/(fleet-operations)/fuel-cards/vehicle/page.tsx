import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  ApiUnavailable,
  FuelCardNotice,
  FuelCardTable,
  VehicleResults,
  VehicleSearchForm,
} from "@/app/(fleet-operations)/fuel-cards/_components";
import { queryValue } from "@/app/(fleet-operations)/fuel-cards/_utils";
import { saveFuelCardAction } from "@/app/(fleet-operations)/fuel-cards/actions";
import { FuelCardApiError, getFuelCardsByVehicle } from "@/lib/api/fleet-operations/api-fuel-cards";
import { getSession } from "@/lib/auth/session";
import { searchWorkshopVehicles } from "@/lib/api/fleet-operations/api-workshop";

function hasRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare("Fuelcards", undefined, { sensitivity: "accent" }) === 0,
  );
}

const FuelCardVehiclePageContent = renderFuelCardVehiclePageContent;

async function renderFuelCardVehiclePageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/fuel-cards/vehicle" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/fuel-cards/vehicle" />
      </main>
    );
  if (!hasRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to maintain Fuelcards.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const type = queryValue(query.type) === "GP" ? "GP" : "GG";
  const search = queryValue(query.search).trim();
  const selectedCode = Number(queryValue(query.vmfCode));
  const vmfCode = Number.isInteger(selectedCode) && selectedCode > 0 ? selectedCode : null;
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
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="fuel-card-vehicle-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Fuelcard maintenance</p>
              <h1 id="fuel-card-vehicle-title">Fuelcard Maintenance for a Vehicle</h1>
              <p>Search a vehicle by GG or GP number, then capture its fuelcard details.</p>
            </div>
            <Link className="button button-secondary" href="/fuel-cards">
              Menu
            </Link>
          </header>
          <FuelCardNotice query={query} />
          <VehicleSearchForm path="/fuel-cards/vehicle" type={type} search={search} />
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="fuel-card-vehicle-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Vehicle lookup</p>
                <h2 id="fuel-card-vehicle-results-title">Matching vehicles</h2>
              </div>
            </div>
            <VehicleResults
              vehicles={vehicles}
              path="/fuel-cards/vehicle"
              type={type}
              search={search}
              selectedCode={vmfCode}
            />
          </section>
          {selectedVehicle ? (
            <>
              <section
                className="vehicle-status-maintenance-panel"
                aria-labelledby="fuel-card-existing-title"
              >
                <div className="vehicle-form-section-header">
                  <div>
                    <p className="eyebrow">
                      {selectedVehicle.fleetNumber ||
                        selectedVehicle.registrationNumber ||
                        selectedVehicle.vmfCode}
                    </p>
                    <h2 id="fuel-card-existing-title">Existing fuelcards</h2>
                  </div>
                </div>
                <FuelCardTable cards={cards} />
              </section>
              <form className="vehicle-status-maintenance-panel" action={saveFuelCardAction}>
                <input
                  name="returnPath"
                  type="hidden"
                  value={`/fuel-cards/vehicle?type=${encodeURIComponent(type)}&search=${encodeURIComponent(search)}&vmfCode=${vmfCode}`}
                />
                <input name="vmfCode" type="hidden" value={selectedVehicle.vmfCode} />
                <input name="ggNumber" type="hidden" value={selectedVehicle.fleetNumber ?? ""} />
                <div className="vehicle-form-section-header">
                  <div>
                    <p className="eyebrow">Selected vehicle</p>
                    <h2>Add Fuelcard</h2>
                    <p>
                      {selectedVehicle.fleetNumber || "-"} /{" "}
                      {selectedVehicle.registrationNumber || "-"} ({selectedVehicle.vmfCode})
                    </p>
                  </div>
                </div>
                <div className="form-grid">
                  <div className="form-field">
                    <label className="form-label" htmlFor="fuel-card-counter">
                      Counter
                    </label>
                    <input
                      className="form-input"
                      id="fuel-card-counter"
                      name="counter"
                      type="number"
                      min="0"
                      defaultValue="1"
                      required
                    />
                  </div>
                  <div className="form-field">
                    <label className="form-label" htmlFor="fuel-card-number">
                      Card Number
                    </label>
                    <input
                      className="form-input"
                      id="fuel-card-number"
                      name="cardNumber"
                      maxLength={15}
                    />
                  </div>
                  <div className="form-field">
                    <label className="form-label" htmlFor="fuel-card-pan">
                      PAN Number
                    </label>
                    <input
                      className="form-input"
                      id="fuel-card-pan"
                      name="panNumber"
                      maxLength={15}
                    />
                  </div>
                </div>
                <div className="button-row">
                  <button className="button button-primary" type="submit">
                    Add
                  </button>
                  <Link className="button button-secondary" href="/fuel-cards/vehicle">
                    Clear
                  </Link>
                </div>
              </form>
            </>
          ) : null}
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof FuelCardApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/fuel-cards/vehicle" />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable path="/fuel-cards/vehicle" subject="Fuelcard maintenance" />
      </main>
    );
  }
}

export default function FuelCardVehiclePage(
  props: Parameters<typeof FuelCardVehiclePageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <FuelCardVehiclePageContent {...props} />
    </Suspense>
  );
}
