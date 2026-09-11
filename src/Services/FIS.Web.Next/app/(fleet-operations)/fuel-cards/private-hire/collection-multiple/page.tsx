import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  ApiUnavailable,
  FuelCardTable,
  VehicleResults,
  VehicleSearchForm,
} from "@/app/(fleet-operations)/fuel-cards/_components";
import { queryValue } from "@/app/(fleet-operations)/fuel-cards/_utils";
import {
  FuelCardApiError,
  getPrivateHireFuelCardsByRegistration,
} from "@/lib/api/fleet-operations/api-fuel-cards";
import { getSession } from "@/lib/auth/session";
import { searchWorkshopVehicles } from "@/lib/api/fleet-operations/api-workshop";

const PrivateHireFuelCardCollectionPageContent = renderPrivateHireFuelCardCollectionPageContent;

async function renderPrivateHireFuelCardCollectionPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/fuel-cards/private-hire/collection-multiple" />
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
        <section className="vehicle-card" aria-labelledby="private-hire-collection-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Private hire fuelcard maintenance</p>
              <h1 id="private-hire-collection-title">Collection for TWO or MORE Fuelcards</h1>
              <p>Find a private hire vehicle and review its current fuelcards before collection.</p>
            </div>
            <Link className="button button-secondary" href="/fuel-cards">
              Menu
            </Link>
          </header>
          <VehicleSearchForm
            path="/fuel-cards/private-hire/collection-multiple"
            type={type}
            search={search}
          />
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="private-hire-collection-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Vehicle lookup</p>
                <h2 id="private-hire-collection-results-title">Matching vehicles</h2>
              </div>
            </div>
            <VehicleResults
              vehicles={vehicles}
              path="/fuel-cards/private-hire/collection-multiple"
              type={type}
              search={search}
              selectedCode={vmfCode}
            />
          </section>
          {selectedVehicle ? (
            <section
              className="vehicle-status-maintenance-panel"
              aria-labelledby="private-hire-collection-cards-title"
            >
              <div className="vehicle-form-section-header">
                <div>
                  <p className="eyebrow">Selected vehicle</p>
                  <h2 id="private-hire-collection-cards-title">
                    Fuelcards available for collection
                  </h2>
                  <p>
                    {selectedVehicle.fleetNumber || "-"} / {registration || "-"} (
                    {selectedVehicle.vmfCode})
                  </p>
                </div>
              </div>
              {registration ? (
                <FuelCardTable cards={cards} privateHire />
              ) : (
                <p className="muted-copy">
                  The selected vehicle has no registration number for a private hire lookup.
                </p>
              )}
            </section>
          ) : null}
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof FuelCardApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/fuel-cards/private-hire/collection-multiple" />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable
          path="/fuel-cards/private-hire/collection-multiple"
          subject="Private hire fuelcard collection"
        />
      </main>
    );
  }
}

export default function PrivateHireFuelCardCollectionPage(
  props: Parameters<typeof PrivateHireFuelCardCollectionPageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <PrivateHireFuelCardCollectionPageContent {...props} />
    </Suspense>
  );
}
