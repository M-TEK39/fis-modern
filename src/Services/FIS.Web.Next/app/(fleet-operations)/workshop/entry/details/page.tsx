import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { WorkshopEntryForm } from "@/app/(fleet-operations)/workshop/entry/workshop-entry-form";
import {
  getWorkshopVehicles,
  WorkshopApiError,
  type WorkshopVehicle,
} from "@/lib/api/fleet-operations/api-workshop";
import { getSession } from "@/lib/auth/session";

async function WorkshopEntryDetailsPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/workshop/entry/details" />
      </main>
    );
  if (
    !session.roles.some(
      (role) => role.localeCompare("Workshop", undefined, { sensitivity: "accent" }) === 0,
    )
  )
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>Access restricted.</h2>
        </section>
      </main>
    );
  const query = await searchParams;
  const search = Array.isArray(query.search) ? (query.search[0] ?? "") : (query.search ?? "");
  const type = Array.isArray(query.type) ? (query.type[0] ?? "GG") : (query.type ?? "GG");
  let vehicles: WorkshopVehicle[] = [];
  try {
    vehicles = await getWorkshopVehicles();
    if (search.trim()) {
      const term = search.trim().toLocaleLowerCase();
      vehicles = vehicles.filter((vehicle) =>
        (type === "GP" ? vehicle.registrationNumber : vehicle.fleetNumber)
          ?.toLocaleLowerCase()
          .includes(term),
      );
    }
  } catch (error) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Workshop entry</p>
          <h2>
            {error instanceof WorkshopApiError && error.reason === "unavailable"
              ? "The vehicle service is temporarily unavailable."
              : "Vehicles could not be loaded."}
          </h2>
          <Link className="button button-secondary" href="/workshop/entry">
            Back
          </Link>
        </section>
      </main>
    );
  }
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="workshop-add-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Workshop maintenance</p>
            <h1 id="workshop-add-title">Workshop Entry Details</h1>
            <p>Capture a new workshop entry.</p>
          </div>
          <Link className="button button-secondary" href="/workshop/entry">
            Back
          </Link>
        </header>
        <form className="vehicle-status-maintenance-panel" method="get">
          <fieldset className="vehicle-search-options">
            <legend>Find vehicle</legend>
            <label className="vehicle-checkbox-label">
              <input type="radio" name="type" value="GG" defaultChecked={type !== "GP"} /> GG
            </label>
            <label className="vehicle-checkbox-label">
              <input type="radio" name="type" value="GP" defaultChecked={type === "GP"} /> GP
            </label>
          </fieldset>
          <div className="vehicle-search-row">
            <label className="sr-only" htmlFor="new-workshop-vehicle-search">
              Vehicle number
            </label>
            <input
              className="vehicle-search"
              id="new-workshop-vehicle-search"
              name="search"
              defaultValue={search}
              placeholder={type === "GP" ? "Enter GP number" : "Enter GG number"}
            />
            <button className="button button-secondary" type="submit">
              Find vehicles
            </button>
          </div>
        </form>
        <WorkshopEntryForm record={null} vehicles={vehicles} returnPath="/workshop/entry/details" />
      </section>
    </main>
  );
}

export default function WorkshopEntryDetailsPage(
  props: Parameters<typeof WorkshopEntryDetailsPageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <WorkshopEntryDetailsPageContent {...props} />
    </Suspense>
  );
}
