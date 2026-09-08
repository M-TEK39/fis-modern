import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { logoutAction } from "@/app/actions/auth";
import LocationClient from "@/app/locations/location-client";
import SessionRecovery from "@/app/home/session-recovery";
import { getLocations, LocationApiError } from "@/lib/api-locations";
import { getSession } from "@/lib/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function StatusCard({
  title,
  message,
  routePath = "/locations",
}: Readonly<{ title: string; message: string; routePath?: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">{title}</p>
      <h2>{message}</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-secondary" href={routePath}>
        Try again
      </Link>
    </section>
  );
}

export default async function LocationsPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/locations" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard title="API unavailable" message="Location data could not be loaded." />
      </main>
    );

  const query = await searchParams;
  try {
    const locations = await getLocations();
    const saved = queryValue(query.saved);
    const error = queryValue(query.error);
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="locations-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Fleet reference data</p>
              <h1 id="locations-title">Location Management</h1>
              <p>Manage physical locations and geographic references.</p>
            </div>
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          </header>
          {saved === "1" ? (
            <div className="notice notice-success" role="status">
              <span aria-hidden="true">✓</span>
              <span>Location saved successfully.</span>
            </div>
          ) : null}
          {error ? (
            <div className="notice notice-error" role="alert">
              <span aria-hidden="true">!</span>
              <span>{error}</span>
            </div>
          ) : null}
          <LocationClient locations={locations} />
          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
            <form action={logoutAction}>
              <button className="button button-secondary" type="submit">
                Sign out
              </button>
            </form>
          </div>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof LocationApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/locations" />
        </main>
      );
    console.error(
      "FIS location list request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="Location service unavailable"
          message="Location information could not be loaded."
        />
      </main>
    );
  }
}
