import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import GarageAddForm from "@/app/accidents/garage/add/garage-add-form";
import {
  AccidentApiError,
  getAccidentReferenceData,
  getAccidentVehicleOptions,
  type GarageSearchType,
} from "@/lib/api-accidents";
import { getSession } from "@/lib/session";

const ACCIDENTS_ROLE = "Accidents";

type GarageAddPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getSearchType(value: string | undefined): GarageSearchType {
  return value === "GG" || value === "Radiogg" ? "GG" : "GP";
}

function getInitialVehicleCode(value: string | undefined) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function AddFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading accident capture...</p>
    </div>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>Vehicle options could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/accidents/garage/add">
          Try again
        </Link>
        <Link className="button button-secondary" href="/accidents/garage">
          Back to Search
        </Link>
      </div>
    </section>
  );
}

function NoVehicles({ searchTerm }: { searchTerm: string }) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">No vehicle found</p>
      <h2>
        {searchTerm ? `No vehicle matched “${searchTerm}”.` : "No active vehicles are available."}
      </h2>
      <p className="muted-copy">Return to the Garage search and choose a valid GG or GP number.</p>
      <div className="button-row">
        <Link className="button button-secondary" href="/accidents/garage">
          Back to Search
        </Link>
      </div>
    </section>
  );
}

async function GarageAddContent({ searchParams }: GarageAddPageProps) {
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath="/accidents/garage/add" />;
  }

  if (session.status === "unavailable") {
    return <ApiUnavailable />;
  }

  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to add garage accidents.</h2>
      </section>
    );
  }

  const query = await searchParams;
  const searchType = getSearchType(getQueryValue(query.type) ?? getQueryValue(query.Radio1));
  const searchTerm = (getQueryValue(query.q) ?? getQueryValue(query.txtGGNum) ?? "").trim();
  const initialVehicleCode = getInitialVehicleCode(
    getQueryValue(query.vmfCode) ?? getQueryValue(query.vmf),
  );
  const today = new Date().toISOString().slice(0, 10);

  let vehicleOptions;
  try {
    const [loadedVehicles, referenceData] = await Promise.all([
      getAccidentVehicleOptions(searchType, searchTerm),
      getAccidentReferenceData(),
    ]);
    vehicleOptions = loadedVehicles;
    if (vehicleOptions.length === 0) {
      return <NoVehicles searchTerm={searchTerm} />;
    }

    return (
      <>
        <GarageAddForm
          accidentTypes={referenceData.accidentTypes}
          initialSearchTerm={searchTerm}
          initialSearchType={searchType}
          initialVehicleCode={initialVehicleCode}
          sites={referenceData.sites}
          today={today}
          vehicleOptions={vehicleOptions}
        />
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/accidents/garage">
            Back to Search
          </Link>
          <Link className="button button-secondary" href="/accidents">
            Accident Menu
          </Link>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
          <form action={logoutAction}>
            <button className="button button-secondary" type="submit">
              Sign out
            </button>
          </form>
        </div>
      </>
    );
  } catch (error) {
    if (error instanceof AccidentApiError && error.reason === "unauthorized") {
      return <SessionRecovery returnPath="/accidents/garage/add" />;
    }

    console.error(
      "FIS garage accident vehicle options failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return <ApiUnavailable />;
  }
}

export default async function GarageAddPage({ searchParams }: GarageAddPageProps) {
  await connection();

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="garage-add-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident maintenance</p>
            <h1 id="garage-add-title">Accident Maintenance (Garage) - Add</h1>
            <p>Capture a new accident for a garage vehicle.</p>
          </div>
          <Link className="button button-secondary" href="/accidents/garage">
            Back to Search
          </Link>
        </header>
        <Suspense fallback={<AddFallback />}>
          <GarageAddContent searchParams={searchParams} />
        </Suspense>
      </section>
    </main>
  );
}
