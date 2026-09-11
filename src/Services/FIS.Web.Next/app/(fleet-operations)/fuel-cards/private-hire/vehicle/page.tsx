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
import { savePrivateHireFuelCardAction } from "@/app/(fleet-operations)/fuel-cards/actions";
import {
  FuelCardApiError,
  getPrivateHireFuelCardsByRegistration,
} from "@/lib/api/fleet-operations/api-fuel-cards";
import { getSession } from "@/lib/auth/session";
import { searchWorkshopVehicles } from "@/lib/api/fleet-operations/api-workshop";

const PrivateHireFuelCardVehiclePageContent = renderPrivateHireFuelCardVehiclePageContent;

async function renderPrivateHireFuelCardVehiclePageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/fuel-cards/private-hire/vehicle" />
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
          <h2>You do not have permission to maintain Fuelcards.</h2>
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
    const registration = selectedVehicle?.registrationNumber?.trim() || "";
    const cards = registration ? await getPrivateHireFuelCardsByRegistration(registration) : [];
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="private-hire-fuel-card-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Private hire fuelcard maintenance</p>
              <h1 id="private-hire-fuel-card-title">
                Private Hire Vehicle Fuelcards for a Vehicle
              </h1>
              <p>
                Search a private hire vehicle by GG or GP number, then capture its fuelcard details.
              </p>
            </div>
            <Link className="button button-secondary" href="/fuel-cards">
              Menu
            </Link>
          </header>
          <FuelCardNotice query={query} />
          <VehicleSearchForm path="/fuel-cards/private-hire/vehicle" type={type} search={search} />
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="private-hire-fuel-card-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Vehicle lookup</p>
                <h2 id="private-hire-fuel-card-results-title">Matching vehicles</h2>
              </div>
            </div>
            <VehicleResults
              vehicles={vehicles}
              path="/fuel-cards/private-hire/vehicle"
              type={type}
              search={search}
              selectedCode={vmfCode}
            />
          </section>
          {selectedVehicle ? (
            <>
              <section
                className="vehicle-status-maintenance-panel"
                aria-labelledby="private-hire-existing-cards-title"
              >
                <div className="vehicle-form-section-header">
                  <div>
                    <p className="eyebrow">{registration || "No registration"}</p>
                    <h2 id="private-hire-existing-cards-title">Existing private hire fuelcards</h2>
                  </div>
                </div>
                {registration ? (
                  <FuelCardTable cards={cards} privateHire />
                ) : (
                  <p className="muted-copy">
                    The selected vehicle has no registration number and cannot be linked to private
                    hire fuelcard data.
                  </p>
                )}
              </section>
              <form
                className="vehicle-status-maintenance-panel"
                action={savePrivateHireFuelCardAction}
              >
                <input
                  name="returnPath"
                  type="hidden"
                  value={`/fuel-cards/private-hire/vehicle?type=${encodeURIComponent(type)}&search=${encodeURIComponent(search)}&vmfCode=${vmfCode}`}
                />
                <input name="registrationNumber" type="hidden" value={registration} />
                <div className="vehicle-form-section-header">
                  <div>
                    <p className="eyebrow">Selected vehicle</p>
                    <h2>Add Private Hire Fuelcard</h2>
                    <p>
                      {selectedVehicle.fleetNumber || "-"} / {registration || "-"} (
                      {selectedVehicle.vmfCode})
                    </p>
                  </div>
                </div>
                <div className="form-grid">
                  <div className="form-field">
                    <label className="form-label" htmlFor="private-hire-fuel-card-counter">
                      Counter
                    </label>
                    <input
                      className="form-input"
                      id="private-hire-fuel-card-counter"
                      name="counter"
                      type="number"
                      min="0"
                      defaultValue="1"
                      required
                    />
                  </div>
                  <div className="form-field">
                    <label className="form-label" htmlFor="private-hire-fuel-card-number">
                      Card Number
                    </label>
                    <input
                      className="form-input"
                      id="private-hire-fuel-card-number"
                      name="cardNumber"
                      maxLength={50}
                    />
                  </div>
                  <div className="form-field">
                    <label className="form-label" htmlFor="private-hire-fuel-card-pan">
                      PAN Number
                    </label>
                    <input
                      className="form-input"
                      id="private-hire-fuel-card-pan"
                      name="panNumber"
                      maxLength={50}
                    />
                  </div>
                </div>
                <div className="button-row">
                  <button className="button button-primary" type="submit" disabled={!registration}>
                    Add
                  </button>
                  <Link className="button button-secondary" href="/fuel-cards/private-hire/vehicle">
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
          <SessionRecovery returnPath="/fuel-cards/private-hire/vehicle" />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable
          path="/fuel-cards/private-hire/vehicle"
          subject="Private hire fuelcard maintenance"
        />
      </main>
    );
  }
}

export default function PrivateHireFuelCardVehiclePage(
  props: Parameters<typeof PrivateHireFuelCardVehiclePageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <PrivateHireFuelCardVehiclePageContent {...props} />
    </Suspense>
  );
}
