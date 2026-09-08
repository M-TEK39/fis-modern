import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import SessionRecovery from "@/app/home/session-recovery";
import RecoveredVehicleClient from "@/app/vehicles/recovered/recovered-vehicle-client";
import {
  getRecoveredVehicleDetails,
  getRecoveredVehicleSearch,
  RecoveredVehicleApiError,
  type RecoveredVehicleDetails,
  type RecoveredVehicleSearchMode,
  type RecoveredVehicleSearchResult,
} from "@/lib/api-recovered-vehicles";
import { getSession } from "@/lib/session";

export type RecoveredVehicleRoutePath =
  "/vehicles/recovered" | "/Master-File/Update_Recovered_GG.aspx";

export type RecoveredVehiclePageProps = {
  searchParams?: Promise<{
    GGnum?: string | string[];
    mode?: string | string[];
    search?: string | string[];
  }>;
  routePath?: RecoveredVehicleRoutePath;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function hasDemoVehicleRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare("Demo Vehicles", undefined, { sensitivity: "base" }) === 0,
  );
}

function StatusCard({
  title,
  message,
  href,
}: Readonly<{ title: string; message: string; href: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">{title}</p>
      <h2>{message}</h2>
      <div className="button-row">
        <Link className="button button-primary" href={href}>
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

function isUnauthorized(error: unknown) {
  return error instanceof RecoveredVehicleApiError && error.reason === "unauthorized";
}

function LoadingState() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading recovered vehicle details...</p>
    </div>
  );
}

type RecoveredVehicleInitialData = {
  searchTerm: string;
  mode: RecoveredVehicleSearchMode;
  matches: RecoveredVehicleSearchResult[];
  details: RecoveredVehicleDetails | null;
};

async function loadInitialData(
  searchParams: RecoveredVehiclePageProps["searchParams"],
): Promise<RecoveredVehicleInitialData> {
  const query = searchParams ? await searchParams : {};
  const mode = getQueryValue(query.mode)?.toUpperCase() === "GP" ? "GP" : "GG";
  const searchTerm = (getQueryValue(query.GGnum) ?? getQueryValue(query.search) ?? "").trim();

  if (!searchTerm) {
    return { searchTerm, mode, matches: [], details: null };
  }

  const matches = await getRecoveredVehicleSearch(searchTerm, mode);
  const details =
    matches.length === 1 ? await getRecoveredVehicleDetails(matches[0].vmfCode) : null;
  return { searchTerm, mode, matches, details };
}

async function RecoveredVehicleContent({
  searchParams,
  routePath,
}: Readonly<{
  searchParams?: RecoveredVehiclePageProps["searchParams"];
  routePath: RecoveredVehicleRoutePath;
}>) {
  try {
    const initialData = await loadInitialData(searchParams);
    return (
      <RecoveredVehicleClient
        initialSearchTerm={initialData.searchTerm}
        initialMode={initialData.mode}
        initialMatches={initialData.matches}
        initialDetails={initialData.details}
      />
    );
  } catch (error) {
    if (isUnauthorized(error)) {
      return <SessionRecovery returnPath={routePath} />;
    }

    console.error(
      "FIS recovered vehicle initial load failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <StatusCard
        title="API unavailable"
        message="Recovered vehicle information could not be loaded."
        href={routePath}
      />
    );
  }
}

export default async function RecoveredVehiclePage({
  searchParams,
  routePath = "/vehicles/recovered",
}: RecoveredVehiclePageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="API unavailable"
          message="The sign-in service is temporarily unavailable."
          href={routePath}
        />
      </main>
    );
  }

  if (!hasDemoVehicleRole(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="Access restricted"
          message="You do not have permission to update recovered vehicles."
          href="/vehicles"
        />
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="recovered-vehicle-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Vehicle master maintenance</p>
            <h1 id="recovered-vehicle-title">Update Recovered GG</h1>
            <p>Record a recovered vehicle while preserving its original GG, status, and history.</p>
          </div>
          <Link className="button button-secondary" href="/vehicles">
            Vehicle Master
          </Link>
        </header>
        <Suspense fallback={<LoadingState />}>
          <RecoveredVehicleContent searchParams={searchParams} routePath={routePath} />
        </Suspense>
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/vehicles">
            Back to Vehicle Master
          </Link>
          <Link className="button button-secondary" href="/vehicles/renumbered-report">
            Renumbered report
          </Link>
        </div>
      </section>
    </main>
  );
}
