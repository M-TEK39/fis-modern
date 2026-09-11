import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import LocationClient from "@/app/(administration)/locations/location-client";
import RouteLoading from "@/components/app-shell/route-loading";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  DEFAULT_LOCATION_PAGE_SIZE,
  getLocationsPage,
  LocationApiError,
} from "@/lib/api/reference-data/api-locations";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positivePage(value: string | undefined) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : 1;
}

function pageHref(query: Record<string, string | string[] | undefined>, page: number) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (key === "page" || value === undefined) continue;
    for (const item of Array.isArray(value) ? value : [value]) params.append(key, item);
  }
  if (page > 1) params.set("page", String(page));
  const queryString = params.toString();
  return queryString ? `/locations?${queryString}` : "/locations";
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

async function LocationsContent({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
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
  const requestedPage = positivePage(queryValue(query.page));
  try {
    const locationPage = await getLocationsPage({
      page: requestedPage,
      pageSize: DEFAULT_LOCATION_PAGE_SIZE,
    });
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
          <LocationClient
            locations={locationPage.items}
            page={locationPage.page}
            pageSize={locationPage.pageSize}
            total={locationPage.total}
            totalPages={locationPage.totalPages}
            previousHref={locationPage.page > 1 ? pageHref(query, locationPage.page - 1) : null}
            nextHref={
              locationPage.page < locationPage.totalPages
                ? pageHref(query, locationPage.page + 1)
                : null
            }
          />
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

export default function LocationsPage(props: Readonly<{ searchParams: SearchParams }>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LocationsContent {...props} />
    </Suspense>
  );
}
